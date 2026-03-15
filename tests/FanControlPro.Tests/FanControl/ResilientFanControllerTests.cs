using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.FanControl;

public sealed class ResilientFanControllerTests
{
    [Fact]
    public async Task SetSpeedAsync_ShouldFallbackToMonitoringOnly_WhenPrimaryThrows()
    {
        var primary = new ThrowingController();
        var fallback = new MonitoringOnlyController();
        var resilient = new ResilientFanController(primary, fallback, NullLogger<ResilientFanController>.Instance);

        var channel = new FanChannel("sys_fan", "System Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: false);

        var result = await resilient.SetSpeedAsync(channel, 60);
        var health = await resilient.GetHealthStatusAsync();

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.MonitoringOnly, result.FailureReason);
        Assert.Equal(ControllerHealthState.Degraded, health.State);
    }

    private sealed class ThrowingController : IFanControllerV2
    {
        public Task<bool> CanControlAsync(FanChannel channel) => Task.FromResult(true);

        public Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
        {
            throw new InvalidOperationException("I/O failure");
        }

        public Task<int> GetCurrentSpeedAsync(FanChannel channel)
        {
            throw new InvalidOperationException("I/O failure");
        }

        public Task<HealthStatus> GetHealthStatusAsync()
        {
            throw new InvalidOperationException("I/O failure");
        }
    }
}
