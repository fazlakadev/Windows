using System;
using System.Reflection;
using Fazlaka.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Fazlaka.Windows.Views;

public sealed partial class LoginPage : Page
{
    private readonly AuthService _authService;
    private bool _signingIn;

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
    }

    private async void OnSignInClicked(object sender, RoutedEventArgs e)
    {
        if (_signingIn)
        {
            return;
        }

        _signingIn = true;
        GoogleButton.IsEnabled = false;
        BusyPanel.Visibility = Visibility.Visible;
        HideError();

        try
        {
            await _authService.SignInAsync();
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _signingIn = false;
            GoogleButton.IsEnabled = true;
            BusyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowError(Exception ex)
    {
        ErrorText.Text = Friendly(ex);
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private static string Friendly(Exception ex) => ex switch
    {
        TimeoutException => "Ø§Ù†ØªÙ‡Øª Ù…Ù‡Ù„Ø© ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯Ø®ÙˆÙ„. ØªØ£ÙƒØ¯ Ù…Ù† Ø§ØªØµØ§Ù„Ùƒ Ø¨Ø§Ù„Ø¥Ù†ØªØ±Ù†Øª ÙˆØ­Ø§ÙˆÙ„ Ù…Ø±Ø© Ø£Ø®Ø±Ù‰.",
        InvalidOperationException ioe when ioe.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            => "Ù„Ù… ÙŠØªÙ… Ø¶Ø¨Ø· ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯Ø®ÙˆÙ„ Ø¹Ø¨Ø± Google Ø¹Ù„Ù‰ Ù‡Ø°Ø§ Ø§Ù„Ø¬Ù‡Ø§Ø². Ø£Ø¹Ø¯ ØªØ´ØºÙŠÙ„ Ø§Ù„ØªØ·Ø¨ÙŠÙ‚ Ø¨Ø¹Ø¯ Ø¶Ø¨Ø· Ù…ÙØ§ØªÙŠØ­ OAuth.",
        OperationCanceledException => "ØªÙ… Ø¥Ù„ØºØ§Ø¡ ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯Ø®ÙˆÙ„.",
        _ => $"ØªØ¹Ø°Ù‘Ø± ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯Ø®ÙˆÙ„: {ex.Message}",
    };
}
