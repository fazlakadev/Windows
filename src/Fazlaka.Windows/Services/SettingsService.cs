using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Fazlaka.Windows.Services;

public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _lock = new();
    private SettingsData _data;

    public SettingsService()
    {
        _data = Load();
    }

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(_data.AuthToken);

    public string? AuthToken { get => _data.AuthToken; set { _data.AuthToken = value; Save(); } }
    public string? RefreshToken { get => _data.RefreshToken; set { _data.RefreshToken = value; Save(); } }
    public string? UserId { get => _data.UserId; set { _data.UserId = value; Save(); } }
    public string? UserName { get => _data.UserName; set { _data.UserName = value; Save(); } }
    public string? UserEmail { get => _data.UserEmail; set { _data.UserEmail = value; Save(); } }
    public string? UserAvatar { get => _data.UserAvatar; set { _data.UserAvatar = value; Save(); } }
    public string UpdateChannel { get => _data.UpdateChannel ?? "stable"; set { _data.UpdateChannel = value; Save(); } }

    private static SettingsData Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new SettingsData();
            }

            var json = File.ReadAllText(SettingsPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SettingsData();
            }

            return JsonSerializer.Deserialize<SettingsData>(json, SerializerOptions) ?? new SettingsData();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[Fazlaka] Failed to load settings, using defaults: {ex}");
            return new SettingsData();
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(_data, SerializerOptions);
                var tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.WriteLine($"[Fazlaka] Failed to save settings: {ex}");
            }
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _data = new SettingsData();
        }

        Save();
    }

    private sealed class SettingsData
    {
        public string? AuthToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserAvatar { get; set; }
        public string? UpdateChannel { get; set; }
    }
}
