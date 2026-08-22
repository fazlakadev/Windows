using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class HistoryItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("contentId")]
    public string? ContentId { get; set; }

    [JsonPropertyName("durationSec")]
    public int DurationSec { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("content")]
    public HistoryContent? Content { get; set; }
}

public class HistoryContent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("audioUrl")]
    public string? AudioUrl { get; set; }

    [JsonPropertyName("seasonTitle")]
    public string? SeasonTitle { get; set; }

    [JsonPropertyName("translations")]
    public System.Collections.Generic.List<Translation>? Translations { get; set; }
}

public class ViewStatus
{
    [JsonPropertyName("liked")]
    public bool Liked { get; set; }

    [JsonPropertyName("disliked")]
    public bool Disliked { get; set; }
}

public class SecurityEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }
}

public class ProfileUpdateResult
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }
}

public class TwoFactorSetupResponse
{
    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("otpauthUrl")]
    public string? OtpauthUrl { get; set; }

    [JsonPropertyName("qrCodeUrl")]
    public string? QrCodeUrl { get; set; }
}
