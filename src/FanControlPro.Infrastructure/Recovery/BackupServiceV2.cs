using System.Security.Cryptography;
using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Infrastructure.Onboarding;
using FanControlPro.Infrastructure.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Recovery;

public sealed class BackupServiceV2 : IBackupServiceV2
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptions<BackupRecoveryOptions> _backupOptions;
    private readonly IOptions<ProfileStorageOptions> _profileOptions;
    private readonly IOptions<ControlOnboardingOptions> _onboardingOptions;
    private readonly IOptions<HardwareDetectionOptions> _hardwareOptions;
    private readonly ILogger<BackupServiceV2> _logger;

    public BackupServiceV2(
        IOptions<BackupRecoveryOptions> backupOptions,
        IOptions<ProfileStorageOptions> profileOptions,
        IOptions<ControlOnboardingOptions> onboardingOptions,
        IOptions<HardwareDetectionOptions> hardwareOptions,
        ILogger<BackupServiceV2> logger)
    {
        _backupOptions = backupOptions;
        _profileOptions = profileOptions;
        _onboardingOptions = onboardingOptions;
        _hardwareOptions = hardwareOptions;
        _logger = logger;
    }

    public async Task<bool> CreateAtomicBackupAsync(CancellationToken cancellationToken = default)
    {
        string? tempDirectory = null;

        try
        {
            var options = _backupOptions.Value;
            var dataRootPath = ResolvePath(options.DataRootPath, Path.Combine("data"));
            var backupsDirectoryPath = ResolvePath(options.BackupsDirectoryPath, Path.Combine("data", "backups"));
            var snapshotPath = ResolvePath(options.SnapshotFilePath, Path.Combine("data", "safe_state.snapshot"));
            var createdAtUtc = DateTimeOffset.UtcNow;

            Directory.CreateDirectory(backupsDirectoryPath);

            tempDirectory = Path.Combine(backupsDirectoryPath, ".tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var trackedFiles = EnumerateTrackedFiles(dataRootPath);
            var entries = new List<BackupFileEntryDocument>(trackedFiles.Count);

            var externalFileIndex = 0;
            foreach (var originalPath in trackedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = BuildRelativeBackupPath(dataRootPath, originalPath, ref externalFileIndex);
                var destinationPath = Path.Combine(tempDirectory, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                await CopyFileAsync(originalPath, destinationPath, cancellationToken).ConfigureAwait(false);
                var checksum = await ComputeSha256HexAsync(destinationPath, cancellationToken).ConfigureAwait(false);
                var fileInfo = new FileInfo(destinationPath);

                entries.Add(new BackupFileEntryDocument(
                    OriginalPath: originalPath,
                    RelativePath: relativePath,
                    Sha256: checksum,
                    SizeBytes: fileInfo.Length,
                    LastWriteAtUtc: File.GetLastWriteTimeUtc(originalPath)));
            }

            var manifest = new BackupManifestDocument(
                SchemaVersion: options.GetSafeSchemaVersion(),
                CreatedAtUtc: createdAtUtc,
                Files: entries);

            var metadata = new BackupMetadataDocument(
                SchemaVersion: options.GetSafeSchemaVersion(),
                CreatedAtUtc: createdAtUtc,
                FileCount: entries.Count,
                TotalBytes: entries.Sum(entry => entry.SizeBytes));

            await WriteJsonAsync(Path.Combine(tempDirectory, BackupDocumentNames.ManifestFileName), manifest, cancellationToken)
                .ConfigureAwait(false);
            await WriteJsonAsync(Path.Combine(tempDirectory, BackupDocumentNames.MetadataFileName), metadata, cancellationToken)
                .ConfigureAwait(false);

            var backupDirectoryName = $"backup-{createdAtUtc:yyyyMMddTHHmmssfff}";
            var backupDirectoryPath = Path.Combine(backupsDirectoryPath, backupDirectoryName);
            Directory.Move(tempDirectory, backupDirectoryPath);
            tempDirectory = null;

            var snapshotDirectory = Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                Directory.CreateDirectory(snapshotDirectory);
            }

            var snapshot = new SafeStateSnapshotDocument(
                SchemaVersion: options.GetSafeSchemaVersion(),
                LastKnownGoodBackupPath: backupDirectoryPath,
                UpdatedAtUtc: DateTimeOffset.UtcNow);

            await WriteJsonAtomicallyAsync(snapshotPath, snapshot, cancellationToken).ConfigureAwait(false);

            ApplyRetention(backupsDirectoryPath, options.GetSafeRetentionCount());

            _logger.LogInformation("Created atomic backup at {BackupPath} with {FileCount} files.", backupDirectoryPath, entries.Count);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create atomic backup.");
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogDebug(cleanupEx, "Failed to clean temporary backup directory {TempDirectory}.", tempDirectory);
                }
            }
        }
    }

    public async Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        var options = _backupOptions.Value;
        var backupsDirectoryPath = ResolvePath(options.BackupsDirectoryPath, Path.Combine("data", "backups"));

        if (!Directory.Exists(backupsDirectoryPath))
        {
            return Array.Empty<BackupMetadata>();
        }

        var backupDirectories = Directory.GetDirectories(backupsDirectoryPath, "backup-*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var metadata = new List<BackupMetadata>(backupDirectories.Length);

        foreach (var backupPath in backupDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataPath = Path.Combine(backupPath, BackupDocumentNames.MetadataFileName);
            BackupMetadataDocument? document = null;

            if (File.Exists(metadataPath))
            {
                await using var stream = File.OpenRead(metadataPath);
                document = await JsonSerializer.DeserializeAsync<BackupMetadataDocument>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (document is null)
            {
                var fallbackCreatedAt = Directory.GetCreationTimeUtc(backupPath);
                metadata.Add(new BackupMetadata(
                    BackupPath: backupPath,
                    CreatedAtUtc: fallbackCreatedAt,
                    SchemaVersion: options.GetSafeSchemaVersion(),
                    FileCount: 0,
                    TotalBytes: 0));
            }
            else
            {
                metadata.Add(new BackupMetadata(
                    BackupPath: backupPath,
                    CreatedAtUtc: document.CreatedAtUtc,
                    SchemaVersion: document.SchemaVersion,
                    FileCount: document.FileCount,
                    TotalBytes: document.TotalBytes));
            }
        }

        return metadata
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.BackupPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool> ValidateBackupIntegrityAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return false;
        }

        try
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

                var filePath = Path.Combine(normalizedBackupPath, file.RelativePath);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var computed = await ComputeSha256HexAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(computed, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup integrity validation failed for {BackupPath}.", backupPath);
            return false;
        }
    }

    private List<string> EnumerateTrackedFiles(string dataRootPath)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var consentPath = ResolvePath(
            _onboardingOptions.Value.ConsentFilePath,
            Path.Combine("data", "control-consent.json"));
        AddIfExists(files, consentPath);

        var activeProfilePath = ResolvePath(
            _profileOptions.Value.ActiveProfilePath,
            Path.Combine("data", "active-profile.json"));
        AddIfExists(files, activeProfilePath);

        var profilesDirectory = ResolvePath(
            _profileOptions.Value.ProfilesDirectoryPath,
            Path.Combine("data", "profiles"));

        if (Directory.Exists(profilesDirectory))
        {
            foreach (var profilePath in Directory.GetFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                AddIfExists(files, profilePath);
            }
        }

        var hardwarePath = ResolvePath(
            _hardwareOptions.Value.CachePath,
            Path.Combine("data", "hardware.json"));
        AddIfExists(files, hardwarePath);

        return files
            .OrderBy(path => GetRelativeSortingPath(path, dataRootPath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIfExists(ISet<string> files, string path)
    {
        if (File.Exists(path))
        {
            files.Add(path);
        }
    }

    private static string BuildRelativeBackupPath(string dataRootPath, string absoluteFilePath, ref int externalFileIndex)
    {
        if (IsSubPathOf(absoluteFilePath, dataRootPath))
        {
            return Path.GetRelativePath(dataRootPath, absoluteFilePath);
        }

        externalFileIndex++;
        var safeFileName = ToSafeFileName(Path.GetFileName(absoluteFilePath));
        return Path.Combine("external", $"{externalFileIndex:D3}_{safeFileName}");
    }

    private static bool IsSubPathOf(string path, string basePath)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedBase = Path.GetFullPath(basePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(normalizedPath, normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedPath.StartsWith(
            normalizedBase + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeChars = fileName
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(safeChars);
    }

    private static string GetRelativeSortingPath(string absolutePath, string dataRootPath)
    {
        return IsSubPathOf(absolutePath, dataRootPath)
            ? Path.GetRelativePath(dataRootPath, absolutePath)
            : absolutePath;
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            await WriteJsonAsync(tempPath, value, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ApplyRetention(string backupsDirectoryPath, int keepCount)
    {
        var backups = Directory.GetDirectories(backupsDirectoryPath, "backup-*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var stalePath in backups.Skip(keepCount))
        {
            Directory.Delete(stalePath, recursive: true);
        }
    }

    private string ResolveBackupPath(string backupPath)
    {
        if (Path.IsPathRooted(backupPath))
        {
            return backupPath;
        }

        var backupsDirectoryPath = ResolvePath(_backupOptions.Value.BackupsDirectoryPath, Path.Combine("data", "backups"));
        return Path.GetFullPath(Path.Combine(backupsDirectoryPath, backupPath));
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
