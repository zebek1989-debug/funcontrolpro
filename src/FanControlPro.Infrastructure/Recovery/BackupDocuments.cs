namespace FanControlPro.Infrastructure.Recovery;

internal static class BackupDocumentNames
{
    public const string ManifestFileName = "checksums.json";
    public const string MetadataFileName = "metadata.json";
}

internal sealed record BackupManifestDocument(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BackupFileEntryDocument> Files);

internal sealed record BackupFileEntryDocument(
    string OriginalPath,
    string RelativePath,
    string Sha256,
    long SizeBytes,
    DateTimeOffset LastWriteAtUtc);

internal sealed record BackupMetadataDocument(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    int FileCount,
    long TotalBytes);

internal sealed record SafeStateSnapshotDocument(
    int SchemaVersion,
    string LastKnownGoodBackupPath,
    DateTimeOffset UpdatedAtUtc);
