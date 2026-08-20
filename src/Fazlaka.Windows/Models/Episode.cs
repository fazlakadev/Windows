using System;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Episode
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("audio_url")]
    public string AudioUrl { get; set; } = string.Empty;

    [JsonPropertyName("cover_url")]
    public string CoverUrl { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public TimeSpan? Duration { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("season_id")]
    public long SeasonId { get; set; }

    [JsonPropertyName("season_title")]
    public string SeasonTitle { get; set; } = string.Empty;

    [JsonPropertyName("is_played")]
    public bool IsPlayed { get; set; }

    [JsonPropertyName("view_count")]
    public long ViewCount { get; set; }

    [JsonIgnore]
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioUrl);

    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);

    [JsonIgnore]
    public string FormattedDuration => Duration is { } duration
        ? duration.Hours > 0
            ? $"{duration.Hours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}"
        : "--:--";

    [JsonIgnore]
    public string PublishedDisplay => PublishedAt?.ToLocalTime().ToString("MMM d, yyyy") ?? string.Empty;
}
