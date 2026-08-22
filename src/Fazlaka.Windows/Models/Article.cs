using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Article
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("published")]
    public bool Published { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("seasonId")]
    public string? SeasonId { get; set; }

    [JsonPropertyName("translations")]
    public List<Translation> Translations { get; set; } = [];

    [JsonPropertyName("season")]
    public EpisodeSeason? Season { get; set; }

    [JsonIgnore]
    public string Title => GetTranslation("title");

    [JsonIgnore]
    public string Description => GetTranslation("description");

    [JsonIgnore]
    public string Excerpt => GetTranslation("excerpt");

    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImage);

    [JsonIgnore]
    public string SeasonTitle => Season?.Title ?? string.Empty;

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
            "excerpt" => t.Description ?? string.Empty,
            _ => string.Empty
        };
    }
}
