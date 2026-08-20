using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;
using Microsoft.UI.Dispatching;

namespace Fazlaka.Windows.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private const int DebounceDelayMilliseconds = 300;

    private readonly ApiService _apiService;
    private readonly AudioPlayerService _audioPlayer;
    private readonly DispatcherQueueTimer _debounceTimer;
    private int _searchGeneration;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasSearched;

    public ObservableCollection<Episode> Results { get; } = [];

    public bool IsEmpty => HasSearched && !IsSearching && !HasError && Results.Count == 0;

    public SearchViewModel()
    {
        _apiService = App.Services.Get<ApiService>();
        _audioPlayer = App.Services.Get<AudioPlayerService>();

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _debounceTimer = dispatcherQueue.CreateTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceDelayMilliseconds);
        _debounceTimer.IsRepeating = false;
        _debounceTimer.Tick += (_, _) => _ = ExecuteSearchAsync();
    }

    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _debounceTimer.Stop();
            ResetResults();
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    [RelayCommand]
    private Task SearchAsync(CancellationToken token) => ExecuteSearchAsync(token);

    [RelayCommand]
    private void ClearSearch()
    {
        _debounceTimer.Stop();
        Query = string.Empty;
        ResetResults();
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

    private async Task ExecuteSearchAsync(CancellationToken token = default)
    {
        var term = Query.Trim();
        if (term.Length == 0)
        {
            ResetResults();
            return;
        }

        var generation = ++_searchGeneration;
        IsSearching = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var result = await _apiService.SearchAsync(term, token);
            if (generation != _searchGeneration)
            {
                return;
            }

            Results.Clear();
            if (result.Success && result.Data is not null)
            {
                foreach (var episode in result.Data)
                {
                    Results.Add(episode);
                }
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? result.Message ?? "Search failed. Please try again.";
            }

            HasSearched = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (generation != _searchGeneration)
            {
                return;
            }

            HasError = true;
            ErrorMessage = ex.Message;
            HasSearched = true;
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsSearching = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    private void ResetResults()
    {
        _searchGeneration++;
        Results.Clear();
        HasSearched = false;
        HasError = false;
        ErrorMessage = null;
        IsSearching = false;
        OnPropertyChanged(nameof(IsEmpty));
    }
}
