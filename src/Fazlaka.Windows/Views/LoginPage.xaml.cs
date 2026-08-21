using System;
using System.IO;
using System.Reflection;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Fazlaka.Windows.Views;

public sealed partial class LoginPage : Page
{
    private readonly AuthService _authService;
    private bool _busy;
    private bool _isRegisterMode;

    public event EventHandler? LoginSucceeded;

    public string VersionDisplay { get; }

    public LoginPage()
    {
        _authService = App.Services.Get<AuthService>();
        VersionDisplay = "v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Resources["EntranceStory"] is Storyboard story)
        {
            story.Begin();
        }
        ToggleRegisterMode(false);
        LoadLogo();
    }

    private void LoadLogo()
    {
        try
        {
            var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
            var logoPath = Path.Combine(assetsDir, "logo.png");
            if (File.Exists(logoPath))
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                LogoImage.Source = bitmap;
            }
        }
        catch { }
    }

    private void ToggleRegisterMode(bool register)
    {
        _isRegisterMode = register;

        CardTitle.Text = register ? "أنشئ حسابك" : "مرحباً بعودتك";
        CardSubtitle.Text = register
            ? "سجّل حساباً جديداً واستمتع بالحلقات الصوتية العربية."
            : "سجّل الدخول لمتابعة استماعك واكتشف حلقات جديدة.";

        SubmitText.Text = register ? "إنشاء الحساب" : "تسجيل الدخول";
        UsernameGroup.Visibility = register ? Visibility.Visible : Visibility.Collapsed;
        NameGroup.Visibility = register ? Visibility.Visible : Visibility.Collapsed;

        if (register)
        {
            ToggleLabel.Text = "لديك حساب بالفعل؟  سجّل الدخول";
        }
        else
        {
            ToggleLabel.Text = "ليس لديك حساب؟  أنشئ حساباً";
        }
    }

    private void OnToggleTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        ToggleRegisterMode(!_isRegisterMode);
    }

    private async void OnSubmitClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        _busy = true;
        SubmitButton.IsEnabled = false;
        GoogleButton.IsEnabled = false;
        BusyPanel.Visibility = Visibility.Visible;
        BusyText.Text = _isRegisterMode ? "جارٍ إنشاء الحساب…" : "جارٍ تسجيل الدخول…";
        HideError();

        try
        {
            if (_isRegisterMode)
            {
                var username = UsernameBox.Text?.Trim() ?? string.Empty;
                var name = NameBox.Text?.Trim() ?? string.Empty;
                var email = EmailBox.Text?.Trim() ?? string.Empty;
                var password = PasswordBox.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ShowError("جميع الحقول مطلوبة.");
                    return;
                }

                if (password.Length < 8)
                {
                    ShowError("كلمة المرور يجب أن تكون 8 أحرف على الأقل.");
                    return;
                }

                await _authService.RegisterAsync(username, name, email, password);
            }
            else
            {
                var email = EmailBox.Text?.Trim() ?? string.Empty;
                var password = PasswordBox.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ShowError("البريد الإلكتروني وكلمة المرور مطلوبان.");
                    return;
                }

                await _authService.LoginAsync(email, password);
            }

            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
            SubmitButton.IsEnabled = true;
            GoogleButton.IsEnabled = true;
            BusyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnGoogleClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        _busy = true;
        GoogleButton.IsEnabled = false;
        SubmitButton.IsEnabled = false;
        BusyPanel.Visibility = Visibility.Visible;
        BusyText.Text = "بانتظار اكتمال تسجيل الدخول في المتصفح…";
        HideError();

        try
        {
            await _authService.SignInAsync();
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowError(Friendly(ex));
        }
        finally
        {
            _busy = false;
            GoogleButton.IsEnabled = true;
            SubmitButton.IsEnabled = true;
            BusyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void ShowError(Exception ex) => ShowError(Friendly(ex));

    private void HideError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private static string Friendly(Exception ex) => ex switch
    {
        TimeoutException => "انتهت مهلة تسجيل الدخول. تأكد من اتصالك بالإنترنت وحاول مرة أخرى.",
        InvalidOperationException ioe when ioe.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            => "لم تتم تهيئة تسجيل الدخول عبر Google على هذا الجهاز. أعد تثبيت التحديث بعد ضبط متغيرات OAuth.",
        InvalidOperationException ioe => ioe.Message,
        OperationCanceledException => "تم إلغاء تسجيل الدخول.",
        _ => $"حدث خطأ: {ex.Message}",
    };
}
