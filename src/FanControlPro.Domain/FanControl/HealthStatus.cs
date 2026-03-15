using FanControlPro.Domain.FanControl.Enums;

namespace FanControlPro.Domain.FanControl;

public sealed record HealthStatus(
    ControllerHealthState State,
    string Message,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<string> Issues)
{
    public static HealthStatus Healthy(string message = "Controller healthy") =>
        new(ControllerHealthState.Healthy, message, DateTimeOffset.UtcNow, Array.Empty<string>());
}
