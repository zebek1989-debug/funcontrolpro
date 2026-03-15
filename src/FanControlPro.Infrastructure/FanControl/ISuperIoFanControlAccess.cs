using FanControlPro.Domain.FanControl;

namespace FanControlPro.Infrastructure.FanControl;

public readonly record struct SuperIoWriteAttemptResult(
    bool PathAvailable,
    bool Success,
    int AppliedPercent,
    string Message)
{
    public static SuperIoWriteAttemptResult PathUnavailable(string message) =>
        new(false, false, 0, message);

    public static SuperIoWriteAttemptResult Failed(string message) =>
        new(true, false, 0, message);

    public static SuperIoWriteAttemptResult Succeeded(int appliedPercent, string message) =>
        new(true, true, appliedPercent, message);
}

public readonly record struct SuperIoReadAttemptResult(
    bool PathAvailable,
    bool Success,
    int Percent,
    string Message)
{
    public static SuperIoReadAttemptResult PathUnavailable(string message) =>
        new(false, false, 0, message);

    public static SuperIoReadAttemptResult Failed(string message) =>
        new(true, false, 0, message);

    public static SuperIoReadAttemptResult Succeeded(int percent, string message) =>
        new(true, true, percent, message);
}

public interface ISuperIoFanControlAccess
{
    Task<SuperIoWriteAttemptResult> TrySetSpeedAsync(
        FanChannel channel,
        int percent,
        CancellationToken cancellationToken = default);

    Task<SuperIoReadAttemptResult> TryReadSpeedPercentAsync(
        FanChannel channel,
        CancellationToken cancellationToken = default);
}
