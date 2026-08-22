using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Episode
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("seasonId")]
    public string? SeasonId { get; set; }

    [JsonPropertyName("episodeNumber")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("audioUrl")]
    public string? AudioUrl { get; set; }

    [JsonPropertyName("videoUrl")]
    public string? VideoUrl { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("published")]
    public bool Published { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("viewsCount")]
    public int ViewsCount { get; set; }

    [JsonPropertyName("likesCount")]
    public int LikesCount { get; set; }

    [JsonPropertyName("commentsCount")]
    public int CommentsCount { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("translations")]
    public List<Translation> Translations { get; set; } = [];

    [JsonPropertyName("season")]
    public EpisodeSeason? Season { get; set; }

    [JsonIgnore]
    public string Title => GetTranslation("title");

    [JsonIgnore]
    public string Description => GetTranslation("description");

    [JsonIgnore]
    public string SeasonTitle => Season?.Title ?? string.Empty;

    [JsonIgnore]
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioUrl);

    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImage);

    [JsonIgnore]
    public string FormattedDuration
    {
        get
        {
            if (Duration is not double dur || dur <= 0) return "--:--";
            return dur >= 3600
                ? $"{(int)(dur / 3600)}:{((int)(dur % 3600) / 60):D2}:{((int)(dur % 60)):D2}"
                : $"{(int)(dur / 60)}:{((int)(dur % 60)):D2}";
        }
    }

    [JsonIgnore]
    public string PublishedDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(PublishedAt)) return string.Empty;
            if (DateTime.TryParse(PublishedAt, out var dt))
                return dt.ToLocalTime().ToString("MMM d, yyyy");
            return string.Empty;
        }
    }

    private string GetTranslation(string field)
    {
        var t = Translations.FirstOrDefault(x => x.Locale == "ar")
                ?? Translations.FirstOrDefault();
        if (t == null) return string.Empty;
        return field switch
        {
            "title" => t.Title ?? string.Empty,
            "description" => t.Description ?? string.Empty,
            _ => string.Empty
        };
    }
}

public class Translation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class EpisodeSeason
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("published")]
    public bool Published { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("translations")]
    public List<Translation> Translations { get; set; } = [];

    [JsonIgnore]
    public string Title
    {
        get
        {
            var t = Translations.FirstOrDefault(x => x.Locale == "ar")
                    ?? Translations.FirstOrDefault();
            return t?.Title ?? string.Empty;
        }
    }
}
