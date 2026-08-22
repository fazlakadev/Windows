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

public partial class SeasonsViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly AudioPlayerService _audioPlayer;
    private string? _loadedSeasonId;

    [ObservableProperty]
    private ObservableCollection<Season> _seasons = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSeason))]
    [NotifyPropertyChangedFor(nameof(HasNoEpisodes))]
    private Season? _selectedSeason;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSeasons))]
    private bool _isLoadingSeasons;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoEpisodes))]
    private bool _isLoadingEpisodes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSeasons))]
    [NotifyPropertyChangedFor(nameof(HasNoEpisodes))]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<Episode> Episodes { get; } = [];

    public bool HasSelectedSeason => SelectedSeason is not null;

    public bool HasNoSeasons => !IsLoadingSeasons && !HasError && Seasons.Count == 0;

    public bool HasNoEpisodes => SelectedSeason is not null && !IsLoadingEpisodes && !HasError && Episodes.Count == 0;

    public SeasonsViewModel()
    {
        _apiService = App.Services.Get<ApiService>();
        _audioPlayer = App.Services.Get<AudioPlayerService>();
    }

    [RelayCommand]
    private async Task LoadSeasonsAsync(CancellationToken token)
    {
        if (IsLoadingSeasons)
        {
            return;
        }

        IsLoadingSeasons = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _apiService.GetSeasonsAsync(token);

            if (result.Success && result.Data is not null)
            {
                Seasons = new ObservableCollection<Season>(
                    result.Data.OrderBy(static season => season.SortOrder));
                OnPropertyChanged(nameof(HasNoSeasons));

                if (SelectedSeason is null || !Seasons.Contains(SelectedSeason))
                {
                    SelectedSeason = Seasons.FirstOrDefault();
                }
            }
            else
            {
                Seasons.Clear();
                Episodes.Clear();
                OnPropertyChanged(nameof(HasNoEpisodes));
                SelectedSeason = null;
                OnPropertyChanged(nameof(HasNoSeasons));
                HasError = true;
                ErrorMessage = result.Error ?? result.Message ?? "Unable to load seasons right now.";
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
            IsLoadingSeasons = false;
        }
    }

    [RelayCommand]
    private void SelectSeason(Season? season)
    {
        if (season is null)
        {
            return;
        }

        SelectedSeason = season;
    }

    [RelayCommand]
    private Task ReloadEpisodesAsync(CancellationToken token)
        => SelectedSeason is { } season
            ? LoadEpisodesCoreAsync(season.Id, token)
            : Task.CompletedTask;

    [RelayCommand]
    private void PlayEpisode(Episode? episode)
    {
        if (episode is null || !episode.HasAudio)
        {
            return;
        }

        _audioPlayer.Play(episode);
    }

    partial void OnSelectedSeasonChanged(Season? value)
    {
        if (value is null)
        {
            _loadedSeasonId = null;
            Episodes.Clear();
            OnPropertyChanged(nameof(HasNoEpisodes));
            return;
        }

        if (value.Id == _loadedSeasonId && Episodes.Count > 0)
        {
            return;
        }

        _ = LoadEpisodesCoreAsync(value.Id);
    }

    private async Task LoadEpisodesCoreAsync(string? seasonId, CancellationToken token = default)
    {
        _loadedSeasonId = seasonId;
        IsLoadingEpisodes = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _apiService.GetSeasonEpisodesAsync(seasonId, token);
            if (_loadedSeasonId != seasonId)
            {
                return;
            }

            Episodes.Clear();
            if (result.Success && result.Data is not null)
            {
                foreach (var episode in result.Data)
                {
                    Episodes.Add(episode);
                }
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? result.Message ?? "Unable to load episodes for this season.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_loadedSeasonId != seasonId)
            {
                return;
            }

            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (_loadedSeasonId == seasonId)
            {
                IsLoadingEpisodes = false;
                OnPropertyChanged(nameof(HasNoEpisodes));
            }
        }
    }
}
