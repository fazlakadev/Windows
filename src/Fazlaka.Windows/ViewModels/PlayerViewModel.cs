using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;
using Microsoft.UI.Dispatching;

namespace Fazlaka.Windows.ViewModels;

public partial class PlayerViewModel : ObservableObject
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _positionTimer;
    private bool _syncingFromPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEpisode))]
    private Episode? _currentEpisode;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SliderMaximum))]
    private double _durationSeconds;

    [ObservableProperty]
    private string _positionText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    public bool HasEpisode => CurrentEpisode is not null;

    public double SliderMaximum => DurationSeconds > 0 ? DurationSeconds : 1;

    public PlayerViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _audioPlayer = App.Services.Get<AudioPlayerService>();

        _positionTimer = _dispatcherQueue.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _positionTimer.Tick += OnPositionTimerTick;

        _audioPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioPlayer.ProgressChanged += OnProgressChanged;

        SyncFromPlayer();
        UpdateTimerState();
    }

    public void SyncFromPlayer()
    {
        _syncingFromPlayer = true;
        try
        {
            CurrentEpisode = _audioPlayer.CurrentEpisode;
            IsPlaying = _audioPlayer.IsPlaying;
            ApplyProgress(_audioPlayer.Position, _audioPlayer.Duration);
        }
        finally
        {
            _syncingFromPlayer = false;
        }
    }

    public void Detach()
    {
        _positionTimer.Stop();
        _audioPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioPlayer.ProgressChanged -= OnProgressChanged;
    }

    partial void OnIsPlayingChanged(bool value) => UpdateTimerState();

    [RelayCommand]
    private void PlayPause()
    {
        if (!HasEpisode)
        {
            return;
        }

        _audioPlayer.TogglePlayPause();
    }

    [RelayCommand]
    private void SkipNext() => _audioPlayer.SkipNext();

    [RelayCommand]
    private void SkipPrevious() => _audioPlayer.SkipPrevious();

    [RelayCommand]
    private void Seek(double seconds)
    {
        if (!HasEpisode || DurationSeconds <= 0)
        {
            return;
        }

        var clamped = Math.Clamp(seconds, 0, DurationSeconds);
        var target = TimeSpan.FromSeconds(clamped);
        _audioPlayer.Seek(target);
        ApplyProgress(target, TimeSpan.FromSeconds(DurationSeconds));
    }

    [RelayCommand]
    private void PlayEpisode(Episode? episode)
    {
        if (episode is null || !episode.HasAudio)
        {
            return;
        }

        _audioPlayer.Play(episode);
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
        => RunOnUi(() =>
        {
            SyncFromPlayer();
            UpdateTimerState();
        });

    private void OnProgressChanged(object? sender, EventArgs e)
        => RunOnUi(() =>
        {
            if (_syncingFromPlayer)
            {
                return;
            }

            ApplyProgress(_audioPlayer.Position, _audioPlayer.Duration);
        });

    private void OnPositionTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_syncingFromPlayer)
        {
            return;
        }

        ApplyProgress(_audioPlayer.Position, _audioPlayer.Duration);
    }

    private void ApplyProgress(TimeSpan position, TimeSpan duration)
    {
        var safeDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var safePosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (safeDuration > TimeSpan.Zero && safePosition > safeDuration)
        {
            safePosition = safeDuration;
        }

        PositionSeconds = safePosition.TotalSeconds;
        DurationSeconds = safeDuration.TotalSeconds;
        PositionText = FormatTime(safePosition);
        DurationText = FormatTime(safeDuration);
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private void UpdateTimerState()
    {
        if (IsPlaying && HasEpisode)
        {
            if (!_positionTimer.IsRunning)
            {
                _positionTimer.Start();
            }
        }
        else
        {
            if (_positionTimer.IsRunning)
            {
                _positionTimer.Stop();
            }
        }
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }
}
