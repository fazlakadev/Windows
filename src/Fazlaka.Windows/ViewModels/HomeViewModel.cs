using System;
using System.Collections.ObjectModel;
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

    public bool IsEmpty => !IsLoading && !HasError && LatestEpisodes.Count == 0;

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
            var result = await _apiService.GetLatestEpisodesAsync(token);

            LatestEpisodes.Clear();
            if (result.Success && result.Data is not null)
            {
                foreach (var episode in result.Data)
                {
                    LatestEpisodes.Add(episode);
                }

                FeaturedEpisode = LatestEpisodes.FirstOrDefault();
            }
            else
            {
                FeaturedEpisode = null;
                HasError = true;
                ErrorMessage = result.Error ?? result.Message ?? "Unable to load episodes right now.";
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
}
