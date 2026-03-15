namespace FanControlPro.Application.Configuration;

public sealed record RestoreResult(
    bool Success,
    bool RestoredFromBackup,
    bool FallbackToSafeDefaults,
    string Message,
    string? RestoredBackupPath,
    TimeSpan Duration);
