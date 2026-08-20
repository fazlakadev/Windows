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

    private static readonly JsonSerializerOptions SnakeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions CamelOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
        var url = $"episodes?search={Uri.EscapeDataString(query.Trim())}&limit={safeLimit}";
        return GetListAsync<Episode>(url, cancellationToken);
    }

    public Task<ApiResult<List<Season>>> GetSeasonsAsync(CancellationToken cancellationToken = default, int limit = 50)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Season>($"seasons?limit={safeLimit}", cancellationToken);
    }

    public Task<ApiResult<List<Episode>>> GetSeasonEpisodesAsync(long seasonId, CancellationToken cancellationToken = default, int limit = 100)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return GetListAsync<Episode>($"episodes?seasonId={seasonId}&limit={safeLimit}", cancellationToken);
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
            CamelOpts,
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
            CreateRequest(HttpMethod.Get, "auth/me", null), CamelOpts, cancellationToken).ConfigureAwait(false);

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
                Content = JsonContent.Create(new { idToken }, options: CamelOpts),
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>(CamelOpts, cancellationToken)
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

    private async Task<ApiResult<List<T>>> GetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        var result = await SendForJsonAsync<ApiResult<List<T>>>(
            CreateRequest(HttpMethod.Get, url, null), SnakeOpts, cancellationToken).ConfigureAwait(false);

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
            using (request)
            {
                using var response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
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
            Debug.WriteLine($"[Fazlaka] {request.Method} {request.RequestUri} failed: {ex.Message}");
            return default;
        }
    }

    private static ApiResult<T> Fail<T>(string error) => new()
    {
        Success = false,
        Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        Error = error,
    };

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
        public long Id { get; set; }

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
