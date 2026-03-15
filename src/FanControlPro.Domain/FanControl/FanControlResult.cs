using FanControlPro.Domain.FanControl.Enums;

namespace FanControlPro.Domain.FanControl;

public sealed record FanControlResult(
    bool Success,
    int AppliedPercent,
    string Message,
    FanControlFailureReason FailureReason,
    bool RollbackApplied = false,
    int? PreviousPercent = null)
{
    public static FanControlResult Succeeded(int appliedPercent, string message, int? previousPercent = null) =>
        new(true, appliedPercent, message, FanControlFailureReason.None, RollbackApplied: false, PreviousPercent: previousPercent);

    public static FanControlResult Failed(
        FanControlFailureReason reason,
        string message,
        int appliedPercent = 0,
        bool rollbackApplied = false,
        int? previousPercent = null) =>
        new(false, appliedPercent, message, reason, rollbackApplied, previousPercent);
}
