using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Models;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly UpdateService _updateService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isCheckingForUpdate;

    [ObservableProperty]
    private bool _isInstallingUpdate;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private UpdateManifest? _pendingUpdate;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasError;

    public bool IsBusy => IsCheckingForUpdate || IsInstallingUpdate;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                return "?";
            }

            var parts = UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return "?";
            }

            var first = char.ToUpperInvariant(parts[0][0]).ToString();
            var last = parts.Length > 1 ? char.ToUpperInvariant(parts[^1][0]).ToString() : string.Empty;
            return first + last;
        }
    }

    public event EventHandler? LoggedOut;

    public ProfileViewModel()
    {
        _settingsService = App.Services.Get<SettingsService>();
        _apiService = App.Services.Get<ApiService>();
        _authService = App.Services.Get<AuthService>();
        _updateService = App.Services.Get<UpdateService>();

        AppVersion = GetAppVersion();
    }

    [RelayCommand]
    private async Task LoadProfileAsync(CancellationToken token)
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = null;

        LoadCachedProfile();

        try
        {
            var result = await _apiService.GetCurrentUserAsync(token);
            if (result.Success && result.Data is not null)
            {
                ApplyUser(result.Data);
            }
            else if (string.IsNullOrWhiteSpace(UserName))
            {
                HasError = true;
                StatusMessage = result.Error ?? result.Message ?? "Unable to load your profile.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                HasError = true;
                StatusMessage = ex.Message;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync(CancellationToken token)
    {
        if (IsBusy)
        {
            return;
        }

        IsCheckingForUpdate = true;
        HasError = false;
        StatusMessage = null;

        try
        {
            var manifest = await _updateService.CheckForUpdateAsync(token);
            PendingUpdate = manifest;
            HasUpdate = manifest?.NeedsUpdate == true;
            StatusMessage = HasUpdate
                ? $"Version {manifest!.Version} is available."
                : "You are running the latest version.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync(CancellationToken token)
    {
        if (PendingUpdate is null || IsInstallingUpdate)
        {
            return;
        }

        IsInstallingUpdate = true;
        HasError = false;
        StatusMessage = "Downloading update...";

        try
        {
            await _updateService.DownloadAndInstallAsync(token);
            StatusMessage = "Update installed. Restart the app to finish.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsInstallingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _authService.SignOutAsync();
        }
        finally
        {
            LoggedOut?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void UninstallApp()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var uninstaller = Path.Combine(baseDir, "FazlakaSetup.exe");
            if (File.Exists(uninstaller))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uninstaller,
                    UseShellExecute = true,
                });
            }

            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fazlaka");

            var desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Fazlaka.lnk");

            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Fazlaka");
            if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);

            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Fazlaka", false);
            }
            catch { }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 >nul && rmdir /s /q \"{baseDir}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            Microsoft.UI.Xaml.Application.Current.Exit();
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 >nul && rmdir /s /q \"{AppContext.BaseDirectory}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private void LoadCachedProfile()
    {
        UserName = _settingsService.UserName ?? string.Empty;
        AvatarUrl = _settingsService.UserAvatar;
    }

    private void ApplyUser(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            UserName = user.Name;
        }

        UserEmail = user.Email;

        if (user.HasAvatar)
        {
            AvatarUrl = user.AvatarUrl;
        }
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
                      ?? typeof(ProfileViewModel).Assembly.GetName().Version
                      ?? new Version(1, 0, 0);
        return version.ToString(3);
    }
}
