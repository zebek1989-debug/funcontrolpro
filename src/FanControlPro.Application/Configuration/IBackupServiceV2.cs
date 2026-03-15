namespace FanControlPro.Application.Configuration;

public interface IBackupServiceV2
{
    Task<bool> CreateAtomicBackupAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(CancellationToken cancellationToken = default);

    Task<bool> ValidateBackupIntegrityAsync(string backupPath, CancellationToken cancellationToken = default);
}
