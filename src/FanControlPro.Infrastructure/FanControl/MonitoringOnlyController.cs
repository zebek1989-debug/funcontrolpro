using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class MonitoringOnlyController : IFanControllerV2
{
    public Task<bool> CanControlAsync(FanChannel channel)
    {
        return Task.FromResult(false);
    }

    public Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
    {
        return Task.FromResult(FanControlResult.Failed(
            FanControlFailureReason.MonitoringOnly,
            "Control mode is disabled. Operating in Monitoring Only mode."));
    }

    public Task<int> GetCurrentSpeedAsync(FanChannel channel)
    {
        return Task.FromResult(Math.Max(channel.SafeMinimumPercent, 40));
    }

    public Task<HealthStatus> GetHealthStatusAsync()
    {
        return Task.FromResult(new HealthStatus(
            State: ControllerHealthState.Degraded,
            Message: "Monitoring-only fallback is active.",
            CheckedAtUtc: DateTimeOffset.UtcNow,
            Issues: new[] { "Write path unavailable" }));
    }
}
