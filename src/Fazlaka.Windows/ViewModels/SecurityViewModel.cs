using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fazlaka.Windows.Services;

namespace Fazlaka.Windows.ViewModels;

public partial class SecurityViewModel : ObservableObject
{
    private readonly SecurityService _security;
    private readonly ApiService _api;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isPinEnabled;

    [ObservableProperty]
    private bool _isSettingPin;

    [ObservableProperty]
    private string? _pinInput;

    [ObservableProperty]
    private string? _currentPinInput;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasSuccessMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _is2FaEnabled;

    [ObservableProperty]
    private bool _isSettingUp2Fa;

    [ObservableProperty]
    private string? _totpUri;

    [ObservableProperty]
    private string? _twoFaCode;

    public SecurityViewModel()
    {
        _security = App.Services.Get<SecurityService>();
        _api = App.Services.Get<ApiService>();
        _settings = App.Services.Get<SettingsService>();
        IsPinEnabled = _security.IsPinEnabled;
    }

    [RelayCommand]
    private void StartSetPin()
    {
        IsSettingPin = true;
        PinInput = null;
    }

    [RelayCommand]
    private void ConfirmSetPin()
    {
        if (string.IsNullOrWhiteSpace(PinInput) || PinInput.Length < 4)
        {
            HasError = true;
            ErrorMessage = "يجب أن يكون الرمز 4 أرقام على الأقل.";
            return;
        }

        _security.SetPin(PinInput);
        IsPinEnabled = true;
        IsSettingPin = false;
        PinInput = null;
        HasSuccessMessage = true;
        SuccessMessage = "تم تفعيل القفل بنجاح.";
    }

    [RelayCommand]
    private void CancelSetPin()
    {
        IsSettingPin = false;
        PinInput = null;
    }

    [RelayCommand]
    private void RemovePin()
    {
        if (!string.IsNullOrWhiteSpace(CurrentPinInput))
        {
            if (!_security.VerifyPin(CurrentPinInput))
            {
                HasError = true;
                ErrorMessage = "الرمز الحالي غير صحيح.";
                CurrentPinInput = null;
                return;
            }
        }

        _security.RemovePin();
        IsPinEnabled = false;
        CurrentPinInput = null;
        HasSuccessMessage = true;
        SuccessMessage = "تم إلغاء القفل.";
    }

    [RelayCommand]
    private async Task Load2FaStatusAsync(CancellationToken token)
    {
        try
        {
            var events = await _api.GetSecurityEventsAsync(token, limit: 1);
            Is2FaEnabled = events.Data is not null && events.Data.Count > 0;
        }
        catch { }
    }

    [RelayCommand]
    private async Task Setup2FaAsync(CancellationToken token)
    {
        IsSettingUp2Fa = true;
        HasError = false;
        ErrorMessage = null;

        var setup = await _api.Get2FaSetupAsync(token);
        if (setup?.OtpauthUrl is not null)
        {
            TotpUri = setup.OtpauthUrl;
        }
        else
        {
            HasError = true;
            ErrorMessage = "فشل إعداد المصادقة الثنائية.";
            IsSettingUp2Fa = false;
        }
    }

    [RelayCommand]
    private async Task Confirm2FaAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(TwoFaCode) || TwoFaCode.Length != 6)
        {
            HasError = true;
            ErrorMessage = "أدخل رمز مكون من 6 أرقام.";
            return;
        }

        var ok = await _api.Enable2FaAsync(TwoFaCode, token);
        if (ok)
        {
            Is2FaEnabled = true;
            IsSettingUp2Fa = false;
            TotpUri = null;
            TwoFaCode = null;
            HasSuccessMessage = true;
            SuccessMessage = "تم تفعيل المصادقة الثنائية.";
        }
        else
        {
            HasError = true;
            ErrorMessage = "الرمز غير صحيح. حاول مرة أخرى.";
        }
    }

    [RelayCommand]
    private async Task Disable2FaAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(TwoFaCode) || TwoFaCode.Length != 6)
        {
            HasError = true;
            ErrorMessage = "أدخل رمز المصادقة لإلغاء التفعيل.";
            return;
        }

        var ok = await _api.Disable2FaAsync(TwoFaCode, token);
        if (ok)
        {
            Is2FaEnabled = false;
            TwoFaCode = null;
            HasSuccessMessage = true;
            SuccessMessage = "تم إلغاء المصادقة الثنائية.";
        }
        else
        {
            HasError = true;
            ErrorMessage = "الرمز غير صحيح.";
        }
    }
}
