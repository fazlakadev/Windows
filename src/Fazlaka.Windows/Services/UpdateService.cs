using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Fazlaka.Windows.Models;
using Velopack;

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
            var manager = new UpdateManager(update.DownloadUrl);
            var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (updateInfo is null)
            {
                if (update.IsBlockingUpdate)
                {
                    throw new InvalidOperationException(
                        $"Update {update.Version} is mandatory but no Velopack package was found at {update.DownloadUrl}.");
                }

                Debug.WriteLine("[Fazlaka] No Velopack package available for the announced update; skipping install.");
                return;
            }

            await manager.DownloadUpdatesAsync(
                updateInfo,
                percent => progress?.Report(percent / 100.0),
                cancellationToken).ConfigureAwait(false);
            manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Fazlaka] Failed to download/install update {update.Version}: {ex}");
            throw new InvalidOperationException($"Failed to install update {update.Version}.", ex);
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
