using System;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VirtualKey = Windows.System.VirtualKey;

namespace Fazlaka.Windows.Views;

public sealed partial class LockPage : Page
{
    private readonly SecurityService _security;
    private int _attempts;

    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public event EventHandler? Unlocked;

    public LockPage()
    {
        _security = App.Services.Get<SecurityService>();
        InitializeComponent();
    }

    private void OnUnlockClicked(object sender, RoutedEventArgs e)
    {
        TryUnlock(PinBox.Password);
    }

    private void OnPinKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            TryUnlock(PinBox.Password);
        }
    }

    private void TryUnlock(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            HasError = true;
            ErrorMessage = "أدخل الرمز.";
            Bindings.Update();
            return;
        }

        if (_security.VerifyPin(pin))
        {
            _attempts = 0;
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _attempts++;
            HasError = true;
            ErrorMessage = _attempts >= 5 ? "تم قفل الرمز مؤقتاً. حاول بعد 5 دقائق." : $"رمز خاطئ. ({_attempts}/5)";
            Bindings.Update();
            PinBox.Password = string.Empty;
        }
    }
}
