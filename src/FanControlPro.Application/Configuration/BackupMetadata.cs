namespace FanControlPro.Application.Configuration;

public sealed record BackupMetadata(
    string BackupPath,
    DateTimeOffset CreatedAtUtc,
    int SchemaVersion,
    int FileCount,
    long TotalBytes);
