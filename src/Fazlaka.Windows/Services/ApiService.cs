using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fazlaka.Windows.Models;

namespace Fazlaka.Windows.Services;

public class ApiService
{
    public const string BaseUrl = "https://back-end-hq0is.faable.link/api/v1/";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    public ApiService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _http.DefaultRequestHeaders.Add("User-Agent", "Fazlaka.Windows/1.0");
        if (!string.IsNullOrEmpty(settings.AuthToken))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AuthToken);
        }
    }

    public void SetAuthToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<ApiResult<List<Episode>>> GetLatestEpisodesAsync(CancellationToken cancellationToken = default, int limit = 20)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Episode>($"episodes?limit={safeLimit}", cancellationToken);
    }

    public Task<ApiResult<List<Episode>>> SearchAsync(string query, CancellationToken cancellationToken = default, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(Fail<List<Episode>>("Empty search query."));
        }

        var safeLimit = Math.Clamp(limit, 1, 100);
        var url = $"search?q={Uri.EscapeDataString(query.Trim())}&type=episode&limit={safeLimit}";
        return GetListAsync<Episode>(url, cancellationToken);
    }

    public Task<ApiResult<List<Season>>> GetSeasonsAsync(CancellationToken cancellationToken = default, int limit = 50)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Season>($"seasons?limit={safeLimit}", cancellationToken);
    }

    public Task<ApiResult<List<Episode>>> GetSeasonEpisodesAsync(string? seasonId, CancellationToken cancellationToken = default, int limit = 100)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Episode>($"episodes?seasonId={seasonId}&limit={safeLimit}", cancellationToken);
    }

    public Task<ApiResult<List<Playlist>>> GetPlaylistsAsync(CancellationToken cancellationToken = default, int limit = 50)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Playlist>($"playlists?limit={safeLimit}", cancellationToken);
    }

    public Task<ApiResult<List<Article>>> GetArticlesAsync(CancellationToken cancellationToken = default, int limit = 50)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Article>($"articles?limit={safeLimit}", cancellationToken);
    }

    public async Task<UpdateManifest?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            currentVersion = "0.0.0";
        }

        var dto = await SendForJsonAsync<AppVersionCheckResponse>(
            CreateRequest(HttpMethod.Get, "app-version/check", new Dictionary<string, string?>
            {
                ["x-app-version"] = currentVersion,
                ["x-app-platform"] = "WINDOWS",
            }),
            JsonOpts,
            cancellationToken).ConfigureAwait(false);

        if (dto is null || string.IsNullOrWhiteSpace(dto.Version))
        {
            return null;
        }

        return ToManifest(dto);
    }

    public async Task<ApiResult<User>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendForJsonAsync<ApiResult<ProfileDto>>(
            CreateRequest(HttpMethod.Get, "auth/me", null), JsonOpts, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return Fail<User>("Unable to reach the server.");
        }

        if (!result.Success || result.Data is null)
        {
            return new ApiResult<User>
            {
                Success = false,
                Timestamp = result.Timestamp,
                Message = result.Message,
                Error = result.Error ?? "Unable to load your profile.",
            };
        }

        return new ApiResult<User>
        {
            Success = true,
            Timestamp = result.Timestamp,
            Data = ToUser(result.Data),
        };
    }

    public async Task<User?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? result.Data : null;
    }

    public Task<ApiResult<List<HistoryItem>>> GetHistoryAsync(CancellationToken cancellationToken = default, int limit = 50, int page = 1)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<HistoryItem>($"views/history?limit={safeLimit}&page={page}", cancellationToken);
    }

    public async Task<bool> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, "views/history", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> TrackViewAsync(string contentType, string contentId, int durationSec = 0, bool completed = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "views/track")
            {
                Content = JsonContent.Create(new { contentType, contentId, durationSec, completed }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task<ApiResult<List<HistoryItem>>> GetLikesAsync(CancellationToken cancellationToken = default, int limit = 50, int page = 1)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<HistoryItem>($"likes/history?limit={safeLimit}&page={page}", cancellationToken);
    }

    public async Task<bool> ToggleLikeAsync(string contentType, string contentId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"likes/{contentType}/{contentId}", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<ViewStatus?> GetLikeStatusAsync(string contentType, string contentId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"likes/{contentType}/{contentId}/status", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ViewStatus>(JsonOpts, cancellationToken).ConfigureAwait(false);
        }
        catch { return null; }
    }

    public Task<ApiResult<List<Session>>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return GetListAsync<Session>("auth/sessions", cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"auth/sessions/{sessionId}", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> RevokeAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rt = _settings.RefreshToken;
            using var request = new HttpRequestMessage(HttpMethod.Delete, "auth/sessions")
            {
                Content = JsonContent.Create(new { refreshToken = rt }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<ApiResult<ProfileUpdateResult>> UpdateProfileAsync(string? name = null, string? username = null, string? bio = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new Dictionary<string, object>();
            if (name is not null) body["name"] = name;
            if (username is not null) body["username"] = username;
            if (bio is not null) body["bio"] = bio;

            using var request = new HttpRequestMessage(HttpMethod.Patch, "users/me")
            {
                Content = JsonContent.Create(body, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new ApiResult<ProfileUpdateResult> { Success = false, Error = errText, Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
            }
            var result = await response.Content.ReadFromJsonAsync<ApiResult<ProfileUpdateResult>>(JsonOpts, cancellationToken).ConfigureAwait(false);
            return result ?? new ApiResult<ProfileUpdateResult> { Success = false, Error = "Failed to parse response.", Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
        }
        catch (Exception ex)
        {
            return new ApiResult<ProfileUpdateResult> { Success = false, Error = ex.Message, Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
        }
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/change-password")
            {
                Content = JsonContent.Create(new { currentPassword, newPassword }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, "users/me", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<TwoFactorSetupResponse?> Get2FaSetupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "auth/2fa/totp/setup", null);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(JsonOpts, cancellationToken).ConfigureAwait(false);
        }
        catch { return null; }
    }

    public async Task<bool> Enable2FaAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/2fa/totp/enable")
            {
                Content = JsonContent.Create(new { code }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> Disable2FaAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/2fa/totp/disable")
            {
                Content = JsonContent.Create(new { code }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task<ApiResult<List<SecurityEvent>>> GetSecurityEventsAsync(CancellationToken cancellationToken = default, int page = 1, int limit = 20)
    {
        return GetListAsync<SecurityEvent>($"auth/security/events?page={page}&limit={limit}", cancellationToken);
    }

    public async Task<bool> RequestEmailChangeAsync(string newEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/change-email/request")
            {
                Content = JsonContent.Create(new { newEmail }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ConfirmEmailChangeAsync(string newEmail, string otp, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/change-email")
            {
                Content = JsonContent.Create(new { newEmail, otp }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = _settings.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken }, options: JsonOpts),
            };

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(JsonOpts, cancellationToken)
                .ConfigureAwait(false);

            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
            {
                return false;
            }

            _settings.AuthToken = body.AccessToken;
            if (!string.IsNullOrWhiteSpace(body.RefreshToken))
            {
                _settings.RefreshToken = body.RefreshToken;
            }

            SetAuthToken(body.AccessToken);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Token refresh failed: {ex.Message}");
            return false;
        }
    }

    public async Task<AuthSession?> PostAuthGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ArgumentException("Google ID token is required.", nameof(idToken));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/google/native")
            {
                Content = JsonContent.Create(new { idToken }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>(JsonOpts, cancellationToken)
                .ConfigureAwait(false);

            if (result?.Data?.AccessToken is null)
            {
                Debug.WriteLine("[Fazlaka] Google auth response did not contain an access token.");
                return null;
            }

            var user = result.Data.User is null ? new User() : ToUser(result.Data.User);
            return new AuthSession(result.Data.AccessToken, result.Data.RefreshToken ?? string.Empty, user);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            Debug.WriteLine($"[Fazlaka] Google token exchange failed: {ex.Message}");
            return null;
        }
    }

    public async Task<AuthSession?> PostAuthRegisterAsync(string username, string name, string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/register")
            {
                Content = JsonContent.Create(new { username, name, email, password }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>(JsonOpts, cancellationToken)
                .ConfigureAwait(false);

            if (result?.Data?.AccessToken is null)
            {
                Debug.WriteLine("[Fazlaka] Register response did not contain an access token.");
                return null;
            }

            var user = result.Data.User is null ? new User() : ToUser(result.Data.User);
            return new AuthSession(result.Data.AccessToken, result.Data.RefreshToken ?? string.Empty, user);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[Fazlaka] Register failed: {ex.Message}");
            throw new InvalidOperationException("البريد الإلكتروني أو اسم المستخدم مستخدم بالفعل، أو البيانات غير صحيحة.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or TaskCanceledException or InvalidOperationException)
        {
            Debug.WriteLine($"[Fazlaka] Register failed: {ex.Message}");
            return null;
        }
    }

    public async Task<AuthSession?> PostAuthLoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
            {
                Content = JsonContent.Create(new { email, password }, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>(JsonOpts, cancellationToken)
                .ConfigureAwait(false);

            if (result?.Data?.AccessToken is null)
            {
                Debug.WriteLine("[Fazlaka] Login response did not contain an access token.");
                return null;
            }

            var user = result.Data.User is null ? new User() : ToUser(result.Data.User);
            return new AuthSession(result.Data.AccessToken, result.Data.RefreshToken ?? string.Empty, user);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[Fazlaka] Login failed: {ex.Message}");
            throw new InvalidOperationException("البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or TaskCanceledException or InvalidOperationException)
        {
            Debug.WriteLine($"[Fazlaka] Login failed: {ex.Message}");
            return null;
        }
    }

    private async Task<ApiResult<List<T>>> GetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        var result = await SendForJsonAsync<ApiResult<List<T>>>(
            CreateRequest(HttpMethod.Get, url, null), JsonOpts, cancellationToken).ConfigureAwait(false);

        return result ?? Fail<List<T>>("Unable to reach the server.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, Dictionary<string, string?>? headers)
    {
        var request = new HttpRequestMessage(method, url);
        if (headers is not null)
        {
            foreach (var pair in headers)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                }
            }
        }

        return request;
    }

    private async Task<T?> SendForJsonAsync<T>(
        HttpRequestMessage request,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendCoreAsync<T>(request, options, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }

            if (request.RequestUri is null)
            {
                return default;
            }

            if (request.Headers.Authorization is not null &&
                !string.IsNullOrWhiteSpace(_settings.RefreshToken))
            {
                var refreshed = await RefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                if (refreshed)
                {
                    using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
                    foreach (var h in request.Headers)
                    {
                        if (h.Key != "Authorization")
                        {
                            retry.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        }
                    }
                    retry.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", _settings.AuthToken);
                    return await SendCoreAsync<T>(retry, options, cancellationToken).ConfigureAwait(false);
                }
            }

            return default;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    private async Task<T?> SendCoreAsync<T>(
        HttpRequestMessage request,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using (request)
            {
                using var response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine($"[Fazlaka] 401 from {request.Method} {request.RequestUri}");
                    DebugLog($"API 401 {request.Method} {request.RequestUri}");
                    return default;
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            Debug.WriteLine($"[Fazlaka] {request.Method} {request.RequestUri} failed: {ex.GetType().Name}: {ex.Message}");
            if (ex is JsonException jex)
            {
                Debug.WriteLine($"[Fazlaka] JSON error: {jex.Message}");
            }
            DebugLog($"API FAIL {request.Method} {request.RequestUri}: {ex.GetType().Name}: {ex.Message}");
            return default;
        }
    }

    private static ApiResult<T> Fail<T>(string error) => new()
    {
        Success = false,
        Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        Error = error,
    };

    private static void DebugLog(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fazlaka");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "debug.log");
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] PID={Environment.ProcessId} {message}\n";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Best effort
        }
    }

    private static UpdateManifest ToManifest(AppVersionCheckResponse dto) => new()
    {
        Version = dto.Version ?? string.Empty,
        DownloadUrl = dto.DownloadUrl ?? string.Empty,
        ReleaseNotes = dto.ReleaseNotes ?? string.Empty,
        MinSupportedVersion = dto.MinVersion ?? string.Empty,
        Mandatory = dto.ForceUpdate,
        ForceUpdate = dto.ForceUpdate,
        NeedsUpdate = dto.NeedsUpdate,
        HtmlUrl = dto.HtmlUrl ?? string.Empty,
        PublishedAt = ParseTimestamp(dto.PublishedAt),
    };

    private static User ToUser(ProfileDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name ?? string.Empty,
        Email = dto.Email ?? string.Empty,
        AvatarUrl = dto.AvatarUrl ?? string.Empty,
        Provider = dto.Provider ?? string.Empty,
    };

    private static DateTime? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private sealed class ProfileDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("avatarUrl")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("provider")]
        public string? Provider { get; set; }
    }

    private sealed class AuthResponseDto
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("user")]
        public ProfileDto? User { get; set; }
    }

    private sealed class RefreshTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }
    }

    private sealed class AppVersionCheckResponse
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tagName")]
        public string? TagName { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("publishedAt")]
        public string? PublishedAt { get; set; }

        [JsonPropertyName("htmlUrl")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("minVersion")]
        public string? MinVersion { get; set; }

        [JsonPropertyName("forceUpdate")]
        public bool ForceUpdate { get; set; }

        [JsonPropertyName("needsUpdate")]
        public bool NeedsUpdate { get; set; }
    }
}

public sealed record AuthSession(string AccessToken, string RefreshToken, User User);
