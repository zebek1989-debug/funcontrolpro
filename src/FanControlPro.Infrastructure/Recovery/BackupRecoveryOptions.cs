namespace FanControlPro.Infrastructure.Recovery;

public sealed class BackupRecoveryOptions
{
    public string DataRootPath { get; set; } = Path.Combine("data");

    public string BackupsDirectoryPath { get; set; } = Path.Combine("data", "backups");

    public string SnapshotFilePath { get; set; } = Path.Combine("data", "safe_state.snapshot");

    public int RetentionCount { get; set; } = 5;

    public int SchemaVersion { get; set; } = 1;

    public int GetSafeRetentionCount() => Math.Clamp(RetentionCount, 1, 20);

    public int GetSafeSchemaVersion() => Math.Max(1, SchemaVersion);
}
