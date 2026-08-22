using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Session
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("lastUsedAt")]
    public string? LastUsedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("isCurrent")]
    public bool IsCurrent { get; set; }
}
