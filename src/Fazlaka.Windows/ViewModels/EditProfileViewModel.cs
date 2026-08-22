using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class EditProfileViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _bio;

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

    public EditProfileViewModel()
    {
        _api = App.Services.Get<ApiService>();
        _settings = App.Services.Get<SettingsService>();
        DisplayName = _settings.UserName;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        IsLoading = true;
        try
        {
            var user = await _api.GetProfileAsync(token);
            if (user is not null)
            {
                DisplayName = user.Name;
                Username = user.UserName;
                Bio = user.Bio;
            }
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken token)
    {
        HasError = false;
        ErrorMessage = null;
        HasSuccessMessage = false;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length < 2)
        {
            HasError = true;
            ErrorMessage = "الاسم يجب أن يكون حرفين على الأقل.";
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _api.UpdateProfileAsync(
                name: DisplayName,
                username: Username,
                bio: Bio,
                cancellationToken: token);

            if (result.Success)
            {
                _settings.UserName = DisplayName;
                HasSuccessMessage = true;
                SuccessMessage = "تم حفظ التغييرات.";
            }
            else
            {
                HasError = true;
                ErrorMessage = result.Error ?? "فشل حفظ التغييرات.";
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
}
