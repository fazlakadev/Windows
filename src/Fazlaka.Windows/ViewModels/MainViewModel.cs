using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _currentUserName;

    [ObservableProperty]
    private string? _currentUserAvatar;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel()
    {
        _authService = App.Services.Get<AuthService>();
        _settingsService = App.Services.Get<SettingsService>();
        _updateService = App.Services.Get<UpdateService>();
    }

    public void CheckLoginState()
    {
        IsLoggedIn = !string.IsNullOrEmpty(_settingsService.AuthToken);
        CurrentUserName = _settingsService.UserName;
        CurrentUserAvatar = _settingsService.UserAvatar;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.SignOutAsync();
        CheckLoginState();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var manifest = await _updateService.CheckForUpdateAsync();
            HasUpdate = manifest?.NeedsUpdate == true;
        }
        catch (Exception)
        {
            HasUpdate = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
