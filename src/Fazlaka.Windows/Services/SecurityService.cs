using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fazlaka.Windows.Services;

public class SecurityService
{
    private static readonly string SecurityDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fazlaka", "security");

    private static readonly string LockFilePath = Path.Combine(SecurityDir, "lock.json");

    private readonly SettingsService _settings;

    public SecurityService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Directory.CreateDirectory(SecurityDir);
    }

    public bool IsPinEnabled => LoadLock().PinHash is not null;

    public bool IsLocked { get; private set; } = true;

    public void SetPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
        {
            throw new ArgumentException("PIN must be at least 4 digits.");
        }

        var hash = HashPin(pin);
        var lockData = LoadLock();
        lockData.PinHash = hash;
        lockData.Enabled = true;
        SaveLock(lockData);
    }

    public void RemovePin()
    {
        var lockData = LoadLock();
        lockData.PinHash = null;
        lockData.Enabled = false;
        lockData.FailCount = 0;
        lockData.LastFail = null;
        SaveLock(lockData);
        IsLocked = false;
    }

    public bool VerifyPin(string pin)
    {
        var lockData = LoadLock();
        if (lockData.PinHash is null)
        {
            return true;
        }

        if (lockData.FailCount >= 5 && lockData.LastFail.HasValue)
        {
            var cooldown = TimeSpan.FromMinutes(5);
            if (DateTime.UtcNow - lockData.LastFail.Value < cooldown)
            {
                return false;
            }

            lockData.FailCount = 0;
            lockData.LastFail = null;
            SaveLock(lockData);
        }

        if (HashPin(pin) == lockData.PinHash)
        {
            lockData.FailCount = 0;
            lockData.LastFail = null;
            SaveLock(lockData);
            IsLocked = false;
            return true;
        }

        lockData.FailCount++;
        lockData.LastFail = DateTime.UtcNow;
        SaveLock(lockData);
        return false;
    }

    public void Lock()
    {
        if (IsPinEnabled)
        {
            IsLocked = true;
        }
    }

    public bool ShouldLockOnStart()
    {
        return IsPinEnabled;
    }

    public bool ShouldLockOnMinimize()
    {
        return IsPinEnabled;
    }

    private static string HashPin(string pin)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"fazlaka_pin_{pin}_2026");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static LockData LoadLock()
    {
        try
        {
            if (!File.Exists(LockFilePath))
            {
                return new LockData();
            }

            var json = File.ReadAllText(LockFilePath);
            return JsonSerializer.Deserialize<LockData>(json) ?? new LockData();
        }
        catch
        {
            return new LockData();
        }
    }

    private static void SaveLock(LockData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LockFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to save lock data: {ex.Message}");
        }
    }

    private sealed class LockData
    {
        public string? PinHash { get; set; }
        public bool Enabled { get; set; }
        public int FailCount { get; set; }
        public DateTime? LastFail { get; set; }
    }
}
