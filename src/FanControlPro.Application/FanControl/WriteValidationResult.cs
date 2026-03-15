namespace FanControlPro.Application.FanControl;

public sealed record WriteValidationResult(
    bool IsValid,
    string Message,
    bool RollbackApplied,
    int? PreviousPercent = null,
    int? TestPercent = null);
