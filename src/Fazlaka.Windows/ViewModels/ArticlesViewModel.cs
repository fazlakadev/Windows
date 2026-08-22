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

public partial class ArticlesViewModel : ObservableObject
{
    private readonly ApiService _apiService;

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

    public ObservableCollection<Article> Articles { get; } = [];

    public bool IsEmpty => !IsLoading && !HasError && Articles.Count == 0;

    public ArticlesViewModel()
    {
        _apiService = App.Services.Get<ApiService>();
    }

    [RelayCommand]
    private Task LoadDataAsync(CancellationToken token) => LoadCoreAsync(token, false);

    [RelayCommand]
    private Task RefreshAsync(CancellationToken token) => LoadCoreAsync(token, true);

    private async Task LoadCoreAsync(CancellationToken token, bool setRefreshing)
    {
        if (IsLoading) return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        if (setRefreshing) IsRefreshing = true;

        try
        {
            var result = await _apiService.GetArticlesAsync(token);
            Articles.Clear();
            if (result.Success && result.Data is not null)
            {
                foreach (var article in result.Data)
                    Articles.Add(article);
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? result.Message ?? "Unable to load articles.";
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
            IsRefreshing = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
