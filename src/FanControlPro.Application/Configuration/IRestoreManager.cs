namespace FanControlPro.Application.Configuration;

public interface IRestoreManager
{
    Task<RestoreResult> RestoreLastKnownGoodAsync(CancellationToken cancellationToken = default);

    Task<bool> ValidateBackupIntegrityAsync(string backupPath, CancellationToken cancellationToken = default);
}
