using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Fazlaka.Windows.Models;

namespace Fazlaka.Windows.Services;

public class UpdateService
{
    private readonly ApiService _api;
    private readonly SettingsService _settings;
    private UpdateManifest? _pendingUpdate;

    public UpdateService(ApiService api)
        : this(api, new SettingsService())
    {
    }

    public UpdateService(ApiService api, SettingsService settings)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string UpdateChannel => _settings.UpdateChannel;

    public async Task<UpdateManifest?> CheckForUpdateAsync(CancellationToken cancellationToken = default, string? currentVersion = null)
    {
        var version = string.IsNullOrWhiteSpace(currentVersion) ? GetCurrentVersion() : currentVersion!;

        try
        {
            var manifest = await _api.CheckForUpdateAsync(version, cancellationToken);
            _pendingUpdate = manifest is { NeedsUpdate: true }
                ? manifest
                : null;
            return _pendingUpdate;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Update check failed: {ex.Message}");
            return null;
        }
    }

    public Task DownloadAndInstallAsync(CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("No update is available. Call CheckForUpdateAsync first.");
        }

        return DownloadAndInstallCoreAsync(_pendingUpdate, progress, cancellationToken);
    }

    public Task DownloadAndInstallAsync(UpdateManifest update, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(update);
        return DownloadAndInstallCoreAsync(update, progress, CancellationToken.None);
    }

    private async Task DownloadAndInstallCoreAsync(UpdateManifest update, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!update.HasDownload)
        {
            throw new InvalidOperationException($"Update {update.Version} has no download URL.");
        }

        try
        {
            var installDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                ?? AppContext.BaseDirectory;

            using var http = new HttpClient();
            var zipBytes = await http.GetByteArrayAsync(update.DownloadUrl, cancellationToken).ConfigureAwait(false);

            var tempZip = Path.Combine(Path.GetTempPath(), $"Fazlaka-update-{update.Version}.zip");
            await File.WriteAllBytesAsync(tempZip, zipBytes, cancellationToken).ConfigureAwait(false);

            var tempExtract = Path.Combine(Path.GetTempPath(), $"Fazlaka-update-{update.Version}");
            if (Directory.Exists(tempExtract))
                Directory.Delete(tempExtract, true);
            ZipFile.ExtractToDirectory(tempZip, tempExtract);

            var setupExe = FindSetupInExtracted(tempExtract);
            if (setupExe is not null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = setupExe,
                    UseShellExecute = true
                });
            }
            else
            {
                var appExe = Path.Combine(installDir, "Fazlaka.exe");
                CopyDirectory(tempExtract, installDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    UseShellExecute = true
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to install update {update.Version}: {ex}");
            throw new InvalidOperationException($"Failed to install update {update.Version}.", ex);
        }
    }

    private static string? FindSetupInExtracted(string dir)
    {
        foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(exe).Contains("Setup", StringComparison.OrdinalIgnoreCase))
                return exe;
        }
        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }

    public string GetCurrentVersion()
    {
        try
        {
            var entryVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            if (entryVersion is not null && (entryVersion.Major > 0 || entryVersion.Minor > 0))
            {
                return FormatVersion(entryVersion);
            }

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            return assemblyVersion is null ? "1.0.0" : FormatVersion(assemblyVersion);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Could not determine current version: {ex.Message}");
            return "1.0.0";
        }
    }

    private static string FormatVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}
