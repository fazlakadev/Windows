using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class Playlist
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("translations")]
    public List<Translation> Translations { get; set; } = [];

    [JsonPropertyName("items")]
    public List<PlaylistItem>? Items { get; set; }

    [JsonPropertyName("owner")]
    public PlaylistOwner? Owner { get; set; }

    [JsonPropertyName("_count")]
    public PlaylistCount? Count { get; set; }

    [JsonIgnore]
    public string Title => GetTranslation("title");

    [JsonIgnore]
    public string Description => GetTranslation("description");

    [JsonIgnore]
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImage);

    [JsonIgnore]
    public int ItemCount => Count?.Items ?? Items?.Count ?? 0;

    [JsonIgnore]
    public string ItemCountDisplay => $"{ItemCount} {(ItemCount == 1 ? "حلقة" : "حلقة")}";

    [JsonIgnore]
    public bool IsPlatform => Kind == "platform";

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

public class PlaylistItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("episodeId")]
    public string? EpisodeId { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("episode")]
    public Episode? Episode { get; set; }
}

public class PlaylistOwner
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }
}

public class PlaylistCount
{
    [JsonPropertyName("items")]
    public int Items { get; set; }
}
