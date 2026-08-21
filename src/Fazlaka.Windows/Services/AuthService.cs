using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fazlaka.Windows.Models;

namespace Fazlaka.Windows.Services;

public class AuthService
{
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromMinutes(5);

    private const string SuccessPageHtml =
        "<html><body style='font-family:sans-serif;text-align:center;padding-top:48px;'>" +
        "<h2>You are signed in</h2><p>You can close this window and return to Fazlaka.</p></body></html>";

    private const string BridgePageHtml =
        "<html><body style='font-family:sans-serif;text-align:center;padding-top:48px;'>" +
        "<h2>Completing sign-in&hellip;</h2>" +
        "<script>" +
        "var params = new URLSearchParams(location.hash.slice(1));" +
        "if (params.get('error')) { location.replace('/callback?error=' + encodeURIComponent(params.get('error'))); }" +
        "else { location.replace('/callback?' + params.toString()); }" +
        "</script></body></html>";

    private readonly ApiService _api;
    private readonly SettingsService _settings;

    public AuthService(ApiService api, SettingsService settings)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(_settings.AuthToken);

    public async Task<User> SignInAsync(CancellationToken cancellationToken = default)
    {
        var backendUrl = ApiService.BaseUrl.TrimEnd('/');
        var authUrl = $"{backendUrl}auth/desktop/google";
        OpenBrowser(authUrl);
        return null!;
    }

    public async Task<User> SignInGithubAsync(CancellationToken cancellationToken = default)
    {
        var backendUrl = ApiService.BaseUrl.TrimEnd('/');
        var authUrl = $"{backendUrl}auth/desktop/github";
        OpenBrowser(authUrl);
        return null!;
    }

    public async Task<User> SignInFacebookAsync(CancellationToken cancellationToken = default)
    {
        var backendUrl = ApiService.BaseUrl.TrimEnd('/');
        var authUrl = $"{backendUrl}auth/desktop/facebook";
        OpenBrowser(authUrl);
        return null!;
    }

    public void HandleDeepLinkAuth(string accessToken, string refreshToken)
    {
        _settings.AuthToken = accessToken;
        _settings.RefreshToken = refreshToken;
        _api.SetAuthToken(accessToken);
    }

    public async Task<User> RegisterAsync(string username, string name, string email, string password, CancellationToken cancellationToken = default)
    {
        var session = await _api.PostAuthRegisterAsync(username, name, email, password, cancellationToken)
            ?? throw new InvalidOperationException("register_failed");

        PersistSession(session);
        return session.User;
    }

    public async Task<User> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var session = await _api.PostAuthLoginAsync(email, password, cancellationToken)
            ?? throw new InvalidOperationException("login_failed");

        PersistSession(session);
        return session.User;
    }

    public void SignOut()
    {
        _api.SetAuthToken(null);
        _settings.ClearAll();
    }

    public Task SignOutAsync()
    {
        SignOut();
        return Task.CompletedTask;
    }

    private void PersistSession(AuthSession session)
    {
        _settings.AuthToken = session.AccessToken;
        _settings.RefreshToken = session.RefreshToken;
        _settings.UserId = session.User.Id.ToString();
        _settings.UserName = FirstNonEmpty(session.User.Name, string.Empty);
        _settings.UserEmail = FirstNonEmpty(session.User.Email, string.Empty);
        _settings.UserAvatar = FirstNonEmpty(session.User.AvatarUrl, string.Empty);
        _api.SetAuthToken(session.AccessToken);
    }

    private static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not open the system browser for sign-in.", ex);
        }
    }
}
