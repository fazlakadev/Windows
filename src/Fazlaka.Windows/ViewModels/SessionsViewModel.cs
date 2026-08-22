using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    private readonly ApiService _api;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasSuccessMessage;

    [ObservableProperty]
    private string? _successMessage;

    public ObservableCollection<Session> Sessions { get; } = new();

    public SessionsViewModel()
    {
        _api = App.Services.Get<ApiService>();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        Sessions.Clear();

        try
        {
            var result = await _api.GetSessionsAsync(token);
            if (result.Success && result.Data is not null)
            {
                foreach (var session in result.Data)
                {
                    Sessions.Add(session);
                }
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? "فشل تحميل الجلسات.";
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
    private async Task RevokeSessionAsync(Session? session, CancellationToken token)
    {
        if (session is null || session.Id is null) return;

        var ok = await _api.RevokeSessionAsync(session.Id, token);
        if (ok)
        {
            Sessions.Remove(session);
            HasSuccessMessage = true;
            SuccessMessage = "تم إنهاء الجلسة.";
        }
        else
        {
            HasError = true;
            ErrorMessage = "فشل إنهاء الجلسة.";
        }
    }

    [RelayCommand]
    private async Task RevokeAllAsync(CancellationToken token)
    {
        var ok = await _api.RevokeAllSessionsAsync(token);
        if (ok)
        {
            Sessions.Clear();
            HasSuccessMessage = true;
            SuccessMessage = "تم إنهاء جميع الجلسات.";
        }
        else
        {
            HasError = true;
            ErrorMessage = "فشل إنهاء الجلسات.";
        }
    }
}
