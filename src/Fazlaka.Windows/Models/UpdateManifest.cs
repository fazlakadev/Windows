using System;
using System.Text.Json.Serialization;

namespace Fazlaka.Windows.Models;

public class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("release_notes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    [JsonPropertyName("min_supported_version")]
    public string MinSupportedVersion { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("needs_update")]
    public bool NeedsUpdate { get; set; }

    [JsonPropertyName("force_update")]
    public bool ForceUpdate { get; set; }

    [JsonIgnore]
    public bool HasDownload => !string.IsNullOrWhiteSpace(DownloadUrl);

    [JsonIgnore]
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);

    [JsonIgnore]
    public bool IsBlockingUpdate => Mandatory || ForceUpdate;

    [JsonIgnore]
    public string PublishedDisplay => PublishedAt?.ToLocalTime().ToString("MMM d, yyyy") ?? string.Empty;
}
