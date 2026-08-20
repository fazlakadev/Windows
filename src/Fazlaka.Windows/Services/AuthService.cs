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
    private const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string ClientIdEnvironmentVariable = "FAZLAKA_GOOGLE_CLIENT_ID";
    private const int ListenerMinPort = 49152;
    private const int ListenerMaxPortExclusive = 65536;
    private const int PortSelectionAttempts = 10;
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
        var clientId = ResolveClientId();
        var nonce = GenerateNonce();

        var (listener, port) = StartCallbackListener();
        try
        {
            var redirectUri = $"http://localhost:{port}/callback";
            var authorizationUrl = BuildAuthorizationUrl(clientId, redirectUri, nonce);
            OpenBrowser(authorizationUrl);

            var idToken = await WaitForCallbackAsync(listener, CallbackTimeout, cancellationToken);
            var claims = DecodeIdToken(idToken);
            ValidateNonce(claims, nonce);

            var session = await _api.PostAuthGoogleAsync(idToken, cancellationToken)
                ?? throw new InvalidOperationException("google_sign_in_failed");

            PersistSession(session, claims);
            return session.User;
        }
        finally
        {
            CleanupListener(listener);
        }
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

    private void PersistSession(AuthSession session, GoogleClaims claims)
    {
        _settings.AuthToken = session.AccessToken;
        _settings.RefreshToken = session.RefreshToken;
        _settings.UserId = FirstNonEmpty(claims.Sub, session.User.Id.ToString());
        _settings.UserName = FirstNonEmpty(session.User.Name, claims.Name);
        _settings.UserEmail = FirstNonEmpty(session.User.Email, claims.Email);
        _settings.UserAvatar = FirstNonEmpty(session.User.AvatarUrl, claims.Picture);
        _api.SetAuthToken(session.AccessToken);
    }

    private static string ResolveClientId()
    {
        var clientId = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                $"Google sign-in is not configured. Set the {ClientIdEnvironmentVariable} environment variable to your Google OAuth client ID.");
        }

        return clientId.Trim();
    }

    private static (HttpListener Listener, int Port) StartCallbackListener()
    {
        for (var attempt = 0; attempt < PortSelectionAttempts; attempt++)
        {
            var port = Random.Shared.Next(ListenerMinPort, ListenerMaxPortExclusive);
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException ex)
            {
                Debug.WriteLine($"[Fazlaka] Port {port} unavailable for OAuth callback: {ex.Message}");
                listener.Close();
            }
        }

        throw new InvalidOperationException("Could not find a free local port for the Google sign-in callback.");
    }

    private static string BuildAuthorizationUrl(string clientId, string redirectUri, string nonce)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid email profile",
            ["response_type"] = "id_token",
            ["nonce"] = nonce,
            ["prompt"] = "select_account",
        };

        var query = string.Join("&", parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return $"{GoogleAuthEndpoint}?{query}";
    }

    private static async Task<string> WaitForCallbackAsync(HttpListener listener, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(state =>
            ((TaskCompletionSource<string>)state!).TrySetCanceled(), completion);

        _ = Task.Run(async () =>
        {
            while (!completion.Task.IsCompleted)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    completion.TrySetException(ex);
                    return;
                }

                HandleCallbackContext(context, completion);
            }
        });

        try
        {
            return await completion.Task;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Google sign-in timed out before receiving a response.");
        }
    }

    private static void HandleCallbackContext(HttpListenerContext context, TaskCompletionSource<string> completion)
    {
        try
        {
            var query = context.Request.QueryString;
            var idToken = query["id_token"];
            var error = query["error"];

            if (string.IsNullOrWhiteSpace(idToken))
            {
                WriteResponse(context.Response, BridgePageHtml);
                return;
            }

            WriteResponse(context.Response, SuccessPageHtml);

            if (!string.IsNullOrWhiteSpace(error))
            {
                completion.TrySetException(new InvalidOperationException($"Google sign-in failed: {error}"));
            }
            else
            {
                completion.TrySetResult(idToken);
            }
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private static void WriteResponse(HttpListenerResponse response, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static GoogleClaims DecodeIdToken(string idToken)
    {
        var segments = idToken.Split('.');
        if (segments.Length < 2)
        {
            throw new FormatException("The ID token returned by Google is malformed.");
        }

        byte[] payloadBytes;
        try
        {
            payloadBytes = DecodeBase64Url(segments[1]);
        }
        catch (FormatException ex)
        {
            throw new FormatException("The ID token payload could not be decoded.", ex);
        }

        using var document = JsonDocument.Parse(payloadBytes);
        var root = document.RootElement;

        return new GoogleClaims(
            GetStringProperty(root, "sub"),
            GetStringProperty(root, "email"),
            GetStringProperty(root, "name"),
            GetStringProperty(root, "picture"),
            GetStringProperty(root, "nonce"));
    }

    private static void ValidateNonce(GoogleClaims claims, string expectedNonce)
    {
        if (!string.Equals(claims.Nonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The ID token nonce does not match the sign-in request (possible replay attack).");
        }
    }

    private static string GetStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    private static byte[] DecodeBase64Url(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 0:
                break;
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
            default:
                throw new FormatException("Invalid base64url length.");
        }

        return Convert.FromBase64String(base64);
    }

    private static string GenerateNonce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

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
            throw new InvalidOperationException("Could not open the system browser for Google sign-in.", ex);
        }
    }

    private static void CleanupListener(HttpListener? listener)
    {
        if (listener is null)
        {
            return;
        }

        try
        {
            listener.Stop();
        }
        catch
        {
        }

        try
        {
            listener.Close();
        }
        catch
        {
        }
    }

    private static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;

    private sealed record GoogleClaims(string Sub, string Email, string Name, string Picture, string Nonce);
}
