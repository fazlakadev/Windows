using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class ChangeEmailViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string? _newEmail;

    [ObservableProperty]
    private string? _otpCode;

    [ObservableProperty]
    private bool _isOtpSent;

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

    public ChangeEmailViewModel()
    {
        _api = App.Services.Get<ApiService>();
        _settings = App.Services.Get<SettingsService>();
    }

    [RelayCommand]
    private async Task RequestOtpAsync(CancellationToken token)
    {
        HasError = false;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewEmail))
        {
            HasError = true;
            ErrorMessage = "أدخل البريد الإلكتروني الجديد.";
            return;
        }

        IsLoading = true;
        try
        {
            var ok = await _api.RequestEmailChangeAsync(NewEmail, token);
            if (ok)
            {
                IsOtpSent = true;
                HasSuccessMessage = true;
                SuccessMessage = "تم إرسال رمز التحقق إلى بريدك الإلكتروني.";
            }
            else
            {
                HasError = true;
                ErrorMessage = "فشل إرسال الرمز. تأكد من صحة البريد.";
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
    private async Task ConfirmChangeAsync(CancellationToken token)
    {
        HasError = false;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(OtpCode) || OtpCode.Length < 4)
        {
            HasError = true;
            ErrorMessage = "أدخل رمز التحقق.";
            return;
        }

        IsLoading = true;
        try
        {
            var ok = await _api.ConfirmEmailChangeAsync(NewEmail!, OtpCode, token);
            if (ok)
            {
                HasSuccessMessage = true;
                SuccessMessage = "تم تغيير البريد الإلكتروني بنجاح.";
                IsOtpSent = false;
                OtpCode = null;
            }
            else
            {
                HasError = true;
                ErrorMessage = "الرمز غير صحيح أو انتهى.";
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
