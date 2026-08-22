using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly AudioPlayerService _audioPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Episode? _featuredEpisode;

    public ObservableCollection<Episode> LatestEpisodes { get; } = [];
    public ObservableCollection<Season> LatestSeasons { get; } = [];
    public ObservableCollection<Playlist> LatestPlaylists { get; } = [];

    public bool IsEmpty => !IsLoading && !HasError && LatestEpisodes.Count == 0 && LatestSeasons.Count == 0 && LatestPlaylists.Count == 0;

    public HomeViewModel()
    {
        _apiService = App.Services.Get<ApiService>();
        _audioPlayer = App.Services.Get<AudioPlayerService>();
    }

    [RelayCommand]
    private Task LoadDataAsync(CancellationToken token) => LoadCoreAsync(token, setRefreshing: false);

    [RelayCommand]
    private Task RefreshAsync(CancellationToken token) => LoadCoreAsync(token, setRefreshing: true);

    private async Task LoadCoreAsync(CancellationToken token, bool setRefreshing)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        if (setRefreshing)
        {
            IsRefreshing = true;
        }

        try
        {
            var episodesTask = _apiService.GetLatestEpisodesAsync(token, 12);
            var seasonsTask = _apiService.GetSeasonsAsync(token, 6);
            var playlistsTask = _apiService.GetPlaylistsAsync(token, 6);

            await Task.WhenAll(episodesTask, seasonsTask, playlistsTask);

            var episodeResult = await episodesTask;
            var seasonResult = await seasonsTask;
            var playlistResult = await playlistsTask;

            Log($"Home episodes: success={episodeResult.Success}, count={episodeResult.Data?.Count ?? 0}, error={episodeResult.Error}");
            Log($"Home seasons: success={seasonResult.Success}, count={seasonResult.Data?.Count ?? 0}, error={seasonResult.Error}");
            Log($"Home playlists: success={playlistResult.Success}, count={playlistResult.Data?.Count ?? 0}, error={playlistResult.Error}");

            LatestEpisodes.Clear();
            if (episodeResult.Success && episodeResult.Data is not null)
            {
                foreach (var episode in episodeResult.Data)
                {
                    LatestEpisodes.Add(episode);
                }
                FeaturedEpisode = LatestEpisodes.FirstOrDefault();
            }

            LatestSeasons.Clear();
            if (seasonResult.Success && seasonResult.Data is not null)
            {
                foreach (var season in seasonResult.Data)
                {
                    LatestSeasons.Add(season);
                }
            }

            LatestPlaylists.Clear();
            if (playlistResult.Success && playlistResult.Data is not null)
            {
                foreach (var playlist in playlistResult.Data)
                {
                    LatestPlaylists.Add(playlist);
                }
            }

            if (!episodeResult.Success && !seasonResult.Success && !playlistResult.Success)
            {
                HasError = true;
                ErrorMessage = episodeResult.Error ?? episodeResult.Message ?? "Unable to load content right now.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
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

    [RelayCommand]
    private void PlayFeatured() => PlayEpisode(FeaturedEpisode);

    private static void Log(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fazlaka");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "debug.log");
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] PID={Environment.ProcessId} {message}\n";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Best effort
        }
    }
}
