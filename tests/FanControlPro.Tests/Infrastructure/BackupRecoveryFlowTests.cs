using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Infrastructure.Onboarding;
using FanControlPro.Infrastructure.Profiles;
using FanControlPro.Infrastructure.Recovery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class BackupRecoveryFlowTests
{
    [Fact]
    public async Task ValidateBackupIntegrityAsync_ShouldDetectChecksumMismatch()
    {
        var env = TestEnvironment.Create();
        try
        {
            await WriteValidConsentAsync(env.Paths.ConsentPath);
            Assert.True(await env.BackupService.CreateAtomicBackupAsync());

            var history = await env.BackupService.GetBackupHistoryAsync();
            var latest = Assert.Single(history);

            var relativeFilePath = await ReadFirstManifestRelativePathAsync(latest.BackupPath);
            var fileToCorrupt = Path.Combine(latest.BackupPath, relativeFilePath);
            await File.WriteAllTextAsync(fileToCorrupt, "{\"tampered\":true}");

            var isValid = await env.BackupService.ValidateBackupIntegrityAsync(latest.BackupPath);
            Assert.False(isValid);
        }
        finally
        {
            env.Dispose();
        }
    }

    [Fact]
    public async Task RestoreLastKnownGoodAsync_ShouldRecoverAfterCorruptedWrite()
    {
        var env = TestEnvironment.Create();
        try
        {
            await WriteValidConsentAsync(env.Paths.ConsentPath, acceptedBy: "before-crash");
            Assert.True(await env.BackupService.CreateAtomicBackupAsync());

            // Simulate crash during save: truncated/invalid JSON payload.
            await File.WriteAllTextAsync(env.Paths.ConsentPath, "{\"schemaVersion\":1,\"payload\":");

            var result = await env.RestoreManager.RestoreLastKnownGoodAsync();

            Assert.True(result.Success);
            Assert.True(result.RestoredFromBackup);
            Assert.False(result.FallbackToSafeDefaults);
            Assert.True(result.Duration < TimeSpan.FromSeconds(3), $"Restore took {result.Duration.TotalMilliseconds:F0} ms.");

            var validation = await env.Validator.ValidateCurrentConfigurationAsync();
            Assert.True(validation.IsValid);
        }
        finally
        {
            env.Dispose();
        }
    }

    [Fact]
    public async Task EnsureHealthyStartupAsync_ShouldRecoverWhenConfigIsCorrupted()
    {
        var env = TestEnvironment.Create();
        try
        {
            await WriteValidConsentAsync(env.Paths.ConsentPath, acceptedBy: "healthy");
            Assert.True(await env.BackupService.CreateAtomicBackupAsync());

            await File.WriteAllTextAsync(env.Paths.ConsentPath, "{broken-json");

            var startupResult = await env.StartupRecoveryService.EnsureHealthyStartupAsync();

            Assert.False(startupResult.HealthyBeforeRecovery);
            Assert.True(startupResult.Recovered);

            var finalValidation = await env.Validator.ValidateCurrentConfigurationAsync();
            Assert.True(finalValidation.IsValid);
        }
        finally
        {
            env.Dispose();
        }
    }

    private static async Task WriteValidConsentAsync(string path, string acceptedBy = "qa-user")
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            schemaVersion = 1,
            payload = new
            {
                hasAcceptedRisk = true,
                acceptedAtUtc = DateTimeOffset.UtcNow,
                acceptedBy
            }
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload);
    }

    private static async Task<string> ReadFirstManifestRelativePathAsync(string backupPath)
    {
        var manifestPath = Path.Combine(backupPath, "checksums.json");
        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty("files", out var files) ||
            files.ValueKind != JsonValueKind.Array ||
            files.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Missing backup files in manifest.");
        }

        var first = files[0];
        if (!first.TryGetProperty("relativePath", out var relativePathElement))
        {
            throw new InvalidOperationException("Missing relativePath in backup manifest.");
        }

        var relativePath = relativePathElement.GetString();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Manifest relativePath is empty.");
        }

        return relativePath;
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(
            string rootDirectory,
            TestPaths paths,
            BackupServiceV2 backupService,
            RestoreManager restoreManager,
            ConfigurationHealthValidator validator,
            StartupRecoveryService startupRecoveryService)
        {
            RootDirectory = rootDirectory;
            Paths = paths;
            BackupService = backupService;
            RestoreManager = restoreManager;
            Validator = validator;
            StartupRecoveryService = startupRecoveryService;
        }

        public string RootDirectory { get; }

        public TestPaths Paths { get; }

        public BackupServiceV2 BackupService { get; }

        public RestoreManager RestoreManager { get; }

        public ConfigurationHealthValidator Validator { get; }

        public StartupRecoveryService StartupRecoveryService { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-backup-tests-{Guid.NewGuid():N}");
            var dataRoot = Path.Combine(root, "data");
            var backups = Path.Combine(dataRoot, "backups");

            var paths = new TestPaths(
                DataRootPath: dataRoot,
                BackupsDirectoryPath: backups,
                SnapshotPath: Path.Combine(dataRoot, "safe_state.snapshot"),
                ConsentPath: Path.Combine(dataRoot, "control-consent.json"),
                ProfilesDirectoryPath: Path.Combine(dataRoot, "profiles"),
                ActiveProfilePath: Path.Combine(dataRoot, "active-profile.json"),
                HardwarePath: Path.Combine(dataRoot, "hardware.json"));

            Directory.CreateDirectory(paths.DataRootPath);
            Directory.CreateDirectory(paths.ProfilesDirectoryPath);

            var backupOptions = Options.Create(new BackupRecoveryOptions
            {
                DataRootPath = paths.DataRootPath,
                BackupsDirectoryPath = paths.BackupsDirectoryPath,
                SnapshotFilePath = paths.SnapshotPath,
                RetentionCount = 5,
                SchemaVersion = 1
            });

            var profileOptions = Options.Create(new ProfileStorageOptions
            {
                ProfilesDirectoryPath = paths.ProfilesDirectoryPath,
                ActiveProfilePath = paths.ActiveProfilePath
            });

            var onboardingOptions = Options.Create(new ControlOnboardingOptions
            {
                ConsentFilePath = paths.ConsentPath
            });

            var hardwareOptions = Options.Create(new HardwareDetectionOptions
            {
                CachePath = paths.HardwarePath
            });

            var backupService = new BackupServiceV2(
                backupOptions,
                profileOptions,
                onboardingOptions,
                hardwareOptions,
                NullLogger<BackupServiceV2>.Instance);

            var validator = new ConfigurationHealthValidator(
                profileOptions,
                onboardingOptions,
                hardwareOptions,
                NullLogger<ConfigurationHealthValidator>.Instance);

            var restoreManager = new RestoreManager(
                backupService,
                backupOptions,
                profileOptions,
                onboardingOptions,
                hardwareOptions,
                validator,
                NullLogger<RestoreManager>.Instance);

            var startupRecoveryService = new StartupRecoveryService(
                validator,
                restoreManager,
                NullLogger<StartupRecoveryService>.Instance);

            return new TestEnvironment(
                root,
                paths,
                backupService,
                restoreManager,
                validator,
                startupRecoveryService);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }

    private sealed record TestPaths(
        string DataRootPath,
        string BackupsDirectoryPath,
        string SnapshotPath,
        string ConsentPath,
        string ProfilesDirectoryPath,
        string ActiveProfilePath,
        string HardwarePath);
}
