using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Season
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("cover_url")]
    public string CoverUrl { get; set; } = string.Empty;

    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; set; }

    [JsonPropertyName("episodes")]
    public List<Episode> Episodes { get; set; } = [];

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; }

    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);

    [JsonIgnore]
    public bool HasEpisodes => Episodes.Count > 0;

    [JsonIgnore]
    public string EpisodesDisplay => $"{EpisodeCount} episode{(EpisodeCount == 1 ? string.Empty : "s")}";

    [JsonIgnore]
    public string PublishedDisplay => PublishedAt?.ToLocalTime().ToString("MMM d, yyyy") ?? string.Empty;
}
