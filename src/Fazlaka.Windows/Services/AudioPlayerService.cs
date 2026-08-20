using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Dispatching;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Fazlaka.Windows.Models;

namespace Fazlaka.Windows.Services;

public class AudioPlayerService : IDisposable
{
    private const int PositionPollIntervalMilliseconds = 500;

    private readonly MediaPlayer _player;
    private readonly MediaPlaybackList _playbackList;
    private readonly List<Episode> _queue = [];
    private readonly List<MediaPlaybackItem> _items = [];
    private readonly object _gate = new();

    private readonly DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _positionTimer;
    private System.Threading.Timer? _fallbackTimer;
    private int _currentIndex = -1;
    private bool _disposed;

    public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

    public Episode? CurrentEpisode { get; private set; }

    public TimeSpan Position => _player.PlaybackSession.Position;

    public TimeSpan Duration => _player.PlaybackSession.NaturalDuration;

    public IReadOnlyList<Episode> Queue
    {
        get
        {
            lock (_gate)
            {
                return _queue.ToArray();
            }
        }
    }

    public event EventHandler? PlaybackStateChanged;

    public event EventHandler? PositionChanged;

    public event EventHandler? ProgressChanged;

    public event EventHandler? EpisodeChanged;

    public AudioPlayerService()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _playbackList = new MediaPlaybackList { AutoRepeatEnabled = false };
        _playbackList.CurrentItemChanged += OnCurrentItemChanged;

        _player = new MediaPlayer
        {
            AutoPlay = false,
            AudioCategory = MediaPlayerAudioCategory.Speech,
            CommandManager = { IsEnabled = false },
            Source = _playbackList,
        };
        _player.MediaFailed += OnMediaFailed;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;

        ConfigureSystemMediaTransportControls();
        StartPositionPolling();
    }

    public void Play(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!episode.HasAudio)
        {
            throw new InvalidOperationException($"Episode '{episode.Title}' has no audio URL.");
        }

        lock (_gate)
        {
            var index = _queue.FindIndex(e => e.Id == episode.Id);
            if (index < 0)
            {
                RebuildQueue([episode]);
                index = 0;
            }

            JumpToIndex(index);
            CurrentEpisode = episode;
            UpdateMetadata(episode);
            _player.Play();
            UpdatePlaybackStatus(MediaPlaybackStatus.Playing);
        }

        RaiseEpisodeChanged();
        RaisePlaybackStateChanged();
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPlaying)
        {
            return;
        }

        _player.Pause();
        UpdatePlaybackStatus(MediaPlaybackStatus.Paused);
        RaisePlaybackStateChanged();
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (CurrentEpisode is null || IsPlaying)
        {
            return;
        }

        _player.Play();
        UpdatePlaybackStatus(MediaPlaybackStatus.Playing);
        RaisePlaybackStateChanged();
    }

    public void TogglePlayPause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    public void SkipNext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_queue.Count == 0 || _currentIndex >= _queue.Count - 1)
            {
                return;
            }

            JumpToIndex(_currentIndex + 1);
            _player.Play();
            UpdatePlaybackStatus(MediaPlaybackStatus.Playing);
        }

        RaiseEpisodeChanged();
        RaisePlaybackStateChanged();
    }

    public void SkipPrevious()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_queue.Count == 0 || _currentIndex <= 0)
            {
                return;
            }

            JumpToIndex(_currentIndex - 1);
            _player.Play();
            UpdatePlaybackStatus(MediaPlaybackStatus.Playing);
        }

        RaiseEpisodeChanged();
        RaisePlaybackStateChanged();
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var session = _player.PlaybackSession;
        var target = position;
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }

        if (session.NaturalDuration > TimeSpan.Zero && target > session.NaturalDuration)
        {
            target = session.NaturalDuration;
        }

        session.Position = target;
        RaisePositionChanged();
        RaiseProgressChanged();
    }

    public void SetQueue(List<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            _player.Pause();
            RebuildQueue(episodes.Where(e => e.HasAudio).ToList());
            _currentIndex = -1;
            CurrentEpisode = null;
            UpdatePlaybackStatus(MediaPlaybackStatus.Stopped);
        }

        RaiseEpisodeChanged();
        RaisePlaybackStateChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _positionTimer?.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to stop position timer: {ex.Message}");
        }

        _fallbackTimer?.Dispose();
        _fallbackTimer = null;

        try
        {
            var smtc = _player.SystemMediaTransportControls;
            smtc.ButtonPressed -= OnSmtcButtonPressed;
            smtc.PlaybackStatus = MediaPlaybackStatus.Closed;

            _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
            _player.MediaFailed -= OnMediaFailed;
            _player.Pause();
            _player.Source = null;

            _playbackList.CurrentItemChanged -= OnCurrentItemChanged;
            _playbackList.Items.Clear();

            _queue.Clear();
            _items.Clear();
            _player.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to dispose audio player: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    private void RebuildQueue(List<Episode> episodes)
    {
        _playbackList.Items.Clear();
        _items.Clear();
        _queue.Clear();

        foreach (var episode in episodes)
        {
            var item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(episode.AudioUrl)));
            _items.Add(item);
            _queue.Add(episode);
            _playbackList.Items.Add(item);
        }
    }

    private void JumpToIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _currentIndex = index;
        var episode = _queue[index];
        CurrentEpisode = episode;
        _playbackList.MoveTo((uint)index);
        UpdateMetadata(episode);
    }

    private void ConfigureSystemMediaTransportControls()
    {
        var smtc = _player.SystemMediaTransportControls;
        smtc.IsEnabled = true;
        smtc.IsPlayEnabled = true;
        smtc.IsPauseEnabled = true;
        smtc.IsNextEnabled = true;
        smtc.IsPreviousEnabled = true;
        smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
        smtc.ButtonPressed += OnSmtcButtonPressed;
    }

    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play:
                Resume();
                break;
            case SystemMediaTransportControlsButton.Pause:
                Pause();
                break;
            case SystemMediaTransportControlsButton.Next:
                SkipNext();
                break;
            case SystemMediaTransportControlsButton.Previous:
                SkipPrevious();
                break;
        }
    }

    private void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
    {
        try
        {
            lock (_gate)
            {
                if (args.NewItem is null)
                {
                    _currentIndex = -1;
                    CurrentEpisode = null;
                    UpdatePlaybackStatus(MediaPlaybackStatus.Stopped);
                    RaisePlaybackStateChanged();
                    return;
                }

                var index = _items.IndexOf(args.NewItem);
                if (index >= 0 && index != _currentIndex)
                {
                    _currentIndex = index;
                    CurrentEpisode = _queue[index];
                    UpdateMetadata(CurrentEpisode);
                    RaiseEpisodeChanged();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to handle current item change: {ex.Message}");
        }
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        UpdatePlaybackStatus(MapStatus(sender.PlaybackState));
        RaisePlaybackStateChanged();
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"[Fazlaka] Media playback failed ({args.Error}): {args.ErrorMessage}");
        UpdatePlaybackStatus(MediaPlaybackStatus.Stopped);
        RaisePlaybackStateChanged();
    }

    private static MediaPlaybackStatus MapStatus(MediaPlaybackState state) => state switch
    {
        MediaPlaybackState.Playing => MediaPlaybackStatus.Playing,
        MediaPlaybackState.Paused => MediaPlaybackStatus.Paused,
        MediaPlaybackState.Buffering or MediaPlaybackState.Opening => MediaPlaybackStatus.Changing,
        _ => MediaPlaybackStatus.Stopped,
    };

    private void UpdateMetadata(Episode episode)
    {
        try
        {
            var updater = _player.SystemMediaTransportControls.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = episode.Title;
            updater.MusicProperties.Artist = "Fazlaka";
            updater.MusicProperties.AlbumTitle = string.IsNullOrWhiteSpace(episode.SeasonTitle)
                ? "Fazlaka"
                : episode.SeasonTitle;

            updater.Thumbnail = episode.HasCover &&
                                Uri.TryCreate(episode.CoverUrl, UriKind.Absolute, out var coverUri)
                ? RandomAccessStreamReference.CreateFromUri(coverUri)
                : null;

            updater.Update();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to update SMTC metadata: {ex.Message}");
        }
    }

    private void UpdatePlaybackStatus(MediaPlaybackStatus status)
    {
        try
        {
            _player.SystemMediaTransportControls.PlaybackStatus = status;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to update SMTC playback status: {ex.Message}");
        }
    }

    private void StartPositionPolling()
    {
        if (_dispatcher is not null)
        {
            _positionTimer = _dispatcher.CreateTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(PositionPollIntervalMilliseconds);
            _positionTimer.Tick += (_, _) =>
            {
                RaisePositionChanged();
                RaiseProgressChanged();
            };
            _positionTimer.Start();
        }
        else
        {
            var period = TimeSpan.FromMilliseconds(PositionPollIntervalMilliseconds);
            _fallbackTimer = new System.Threading.Timer(
                _ =>
                {
                    RaisePositionChanged();
                    RaiseProgressChanged();
                }, null, period, period);
        }
    }

    private void RaisePlaybackStateChanged() => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);

    private void RaisePositionChanged() => PositionChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseProgressChanged() => ProgressChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseEpisodeChanged() => EpisodeChanged?.Invoke(this, EventArgs.Empty);
}
