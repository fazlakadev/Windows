using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly ApiService _api;

    [ObservableProperty]
    private string? _currentPassword;

    [ObservableProperty]
    private string? _newPassword;

    [ObservableProperty]
    private string? _confirmPassword;

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

    public ChangePasswordViewModel()
    {
        _api = App.Services.Get<ApiService>();
    }

    [RelayCommand]
    private async Task ChangePasswordAsync(CancellationToken token)
    {
        HasError = false;
        ErrorMessage = null;
        HasSuccessMessage = false;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            HasError = true;
            ErrorMessage = "أدخل كلمة المرور الحالية.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            HasError = true;
            ErrorMessage = "يجب أن تكون كلمة المرور الجديدة 8 أحرف على الأقل.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            HasError = true;
            ErrorMessage = "كلمتا المرور غير متطابقتين.";
            return;
        }

        IsLoading = true;
        try
        {
            var ok = await _api.ChangePasswordAsync(CurrentPassword, NewPassword, token);
            if (ok)
            {
                HasSuccessMessage = true;
                SuccessMessage = "تم تغيير كلمة المرور بنجاح.";
                CurrentPassword = null;
                NewPassword = null;
                ConfirmPassword = null;
            }
            else
            {
                HasError = true;
                ErrorMessage = "كلمة المرور الحالية غير صحيحة.";
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
