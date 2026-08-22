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

public partial class LikesViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly AudioPlayerService _audioPlayer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<HistoryItem> Items { get; } = new();

    public bool IsEmpty => !IsLoading && Items.Count == 0 && !HasError;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public LikesViewModel()
    {
        _api = App.Services.Get<ApiService>();
        _audioPlayer = App.Services.Get<AudioPlayerService>();
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        Items.Clear();

        try
        {
            var result = await _api.GetLikesAsync(token);
            if (result.Success && result.Data is not null)
            {
                foreach (var item in result.Data.Where(i => i.Content is not null))
                {
                    Items.Add(item);
                }
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? "فشل تحميل المفضلة.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayItem(HistoryItem? item)
    {
        if (item?.Content?.AudioUrl is null || item.ContentId is null) return;

        var episode = new Episode
        {
            Id = item.Content.Id,
            AudioUrl = item.Content.AudioUrl,
            CoverImage = item.Content.CoverImage,
            Translations = item.Content.Translations ?? [],
        };

        var queue = Items
            .Where(i => i.Content?.AudioUrl is not null && i.ContentId is not null)
            .Select(i => new Episode
            {
                Id = i.Content!.Id,
                AudioUrl = i.Content.AudioUrl,
                CoverImage = i.Content.CoverImage,
                Translations = i.Content.Translations ?? [],
            })
            .ToList();

        var index = queue.FindIndex(e => e.Id == episode.Id);
        _audioPlayer.PlayQueue(queue, index >= 0 ? index : 0);
    }
}
