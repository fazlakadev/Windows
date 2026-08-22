using System;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class User
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? UserName { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonIgnore]
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    [JsonIgnore]
    public bool IsGoogleUser => string.Equals(Provider, "google", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "?";
            }

            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return "?";
            }

            var first = char.ToUpperInvariant(parts[0][0]).ToString();
            var last = parts.Length > 1 ? char.ToUpperInvariant(parts[^1][0]).ToString() : string.Empty;
            return first + last;
        }
    }
}
