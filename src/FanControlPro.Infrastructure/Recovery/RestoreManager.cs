using System.Diagnostics;
using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Infrastructure.Onboarding;
using FanControlPro.Infrastructure.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Recovery;

public sealed class RestoreManager : IRestoreManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IBackupServiceV2 _backupService;
    private readonly IOptions<BackupRecoveryOptions> _backupOptions;
    private readonly IOptions<ProfileStorageOptions> _profileOptions;
    private readonly IOptions<ControlOnboardingOptions> _onboardingOptions;
    private readonly IOptions<HardwareDetectionOptions> _hardwareOptions;
    private readonly IConfigurationHealthValidator _configurationHealthValidator;
    private readonly ILogger<RestoreManager> _logger;

    public RestoreManager(
        IBackupServiceV2 backupService,
        IOptions<BackupRecoveryOptions> backupOptions,
        IOptions<ProfileStorageOptions> profileOptions,
        IOptions<ControlOnboardingOptions> onboardingOptions,
        IOptions<HardwareDetectionOptions> hardwareOptions,
        IConfigurationHealthValidator configurationHealthValidator,
        ILogger<RestoreManager> logger)
    {
        _backupService = backupService;
        _backupOptions = backupOptions;
        _profileOptions = profileOptions;
        _onboardingOptions = onboardingOptions;
        _hardwareOptions = hardwareOptions;
        _configurationHealthValidator = configurationHealthValidator;
        _logger = logger;
    }

    public Task<bool> ValidateBackupIntegrityAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        return _backupService.ValidateBackupIntegrityAsync(backupPath, cancellationToken);
    }

    public async Task<RestoreResult> RestoreLastKnownGoodAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var candidates = await GetRestoreCandidatesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var backupPath in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var valid = await _backupService.ValidateBackupIntegrityAsync(backupPath, cancellationToken).ConfigureAwait(false);
                if (!valid)
                {
                    _logger.LogWarning("Skipping invalid backup {BackupPath}.", backupPath);
                    continue;
                }

                var restored = await RestoreFromBackupAsync(backupPath, cancellationToken).ConfigureAwait(false);
                if (restored)
                {
                    var validation = await _configurationHealthValidator.ValidateCurrentConfigurationAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (!validation.IsValid)
                    {
                        _logger.LogWarning(
                            "Restored backup {BackupPath} but configuration is still invalid. Issues: {Issues}",
                            backupPath,
                            string.Join(" | ", validation.Errors));
                        continue;
                    }

                    stopwatch.Stop();
                    return new RestoreResult(
                        Success: true,
                        RestoredFromBackup: true,
                        FallbackToSafeDefaults: false,
                        Message: $"Configuration restored from backup '{backupPath}'.",
                        RestoredBackupPath: backupPath,
                        Duration: stopwatch.Elapsed);
                }
            }

            var fallbackSucceeded = ApplySafeDefaults();
            stopwatch.Stop();

            return new RestoreResult(
                Success: fallbackSucceeded,
                RestoredFromBackup: false,
                FallbackToSafeDefaults: true,
                Message: fallbackSucceeded
                    ? "No valid backup found. Applied safe defaults."
                    : "No valid backup found and applying safe defaults failed.",
                RestoredBackupPath: null,
                Duration: stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Restore flow failed.");
            return new RestoreResult(
                Success: false,
                RestoredFromBackup: false,
                FallbackToSafeDefaults: false,
                Message: $"Restore flow failed due to unexpected error: {ex.Message}",
                RestoredBackupPath: null,
                Duration: stopwatch.Elapsed);
        }
    }

    private async Task<bool> RestoreFromBackupAsync(string backupPath, CancellationToken cancellationToken)
    {
        var normalizedBackupPath = ResolveBackupPath(backupPath);
        var manifestPath = Path.Combine(normalizedBackupPath, BackupDocumentNames.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifestDocument>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (manifest is null)
        {
            return false;
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = Path.Combine(normalizedBackupPath, file.RelativePath);
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var destinationPath = file.OriginalPath;
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var tempDestination = destinationPath + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var destination = File.Open(tempDestination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempDestination, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempDestination))
                {
                    File.Delete(tempDestination);
                }
            }
        }

        _logger.LogInformation("Restored configuration files from backup {BackupPath}.", backupPath);
        return true;
    }

    private async Task<IReadOnlyList<string>> GetRestoreCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>();

        var snapshotPath = ResolvePath(_backupOptions.Value.SnapshotFilePath, Path.Combine("data", "safe_state.snapshot"));
        if (File.Exists(snapshotPath))
        {
            await using var stream = File.OpenRead(snapshotPath);
            var snapshot = await JsonSerializer.DeserializeAsync<SafeStateSnapshotDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(snapshot?.LastKnownGoodBackupPath))
            {
                candidates.Add(snapshot.LastKnownGoodBackupPath);
            }
        }

        var history = await _backupService.GetBackupHistoryAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in history.OrderByDescending(x => x.CreatedAtUtc))
        {
            if (!string.IsNullOrWhiteSpace(item.BackupPath))
            {
                candidates.Add(item.BackupPath);
            }
        }

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool ApplySafeDefaults()
    {
        try
        {
            var consentPath = ResolvePath(_onboardingOptions.Value.ConsentFilePath, Path.Combine("data", "control-consent.json"));
            DeleteIfExists(consentPath);

            var activeProfilePath = ResolvePath(_profileOptions.Value.ActiveProfilePath, Path.Combine("data", "active-profile.json"));
            DeleteIfExists(activeProfilePath);

            var profilesDirectory = ResolvePath(_profileOptions.Value.ProfilesDirectoryPath, Path.Combine("data", "profiles"));
            if (Directory.Exists(profilesDirectory))
            {
                foreach (var path in Directory.GetFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    DeleteIfExists(path);
                }
            }

            var hardwarePath = ResolvePath(_hardwareOptions.Value.CachePath, Path.Combine("data", "hardware.json"));
            DeleteIfExists(hardwarePath);

            _logger.LogWarning("Applied safe defaults by cleaning persisted configuration files.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply safe defaults.");
            return false;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string ResolveBackupPath(string backupPath)
    {
        if (Path.IsPathRooted(backupPath))
        {
            return backupPath;
        }

        var backupRoot = ResolvePath(_backupOptions.Value.BackupsDirectoryPath, Path.Combine("data", "backups"));
        return Path.GetFullPath(Path.Combine(backupRoot, backupPath));
    }

    private static string ResolvePath(string configuredPath, string fallbackRelativePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = fallbackRelativePath;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
