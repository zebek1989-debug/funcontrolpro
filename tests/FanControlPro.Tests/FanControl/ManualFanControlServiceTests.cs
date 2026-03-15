using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.FanControl;

public sealed class ManualFanControlServiceTests
{
    [Fact]
    public async Task SetSpeedAsync_ShouldRejectCpuBelow20Percent()
    {
        var service = CreateService();

        var result = await service.SetSpeedAsync("cpu_fan", 10, confirmLowCpuFan: true);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.OutOfRange, result.FailureReason);
    }

    [Fact]
    public async Task SetSpeedAsync_ShouldRequireConfirmationForCpuBelow30Percent()
    {
        var service = CreateService();

        var result = await service.SetSpeedAsync("cpu_fan", 25, confirmLowCpuFan: false);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.ValidationFailed, result.FailureReason);
    }

    [Fact]
    public async Task SetSpeedAsync_ShouldSynchronizeGroupMembers()
    {
        var service = CreateService();

        await service.AssignGroupAsync("system_fan", "Case Fans");
        await service.AssignGroupAsync("rear_fan", "Case Fans");

        var applyResult = await service.SetSpeedAsync("system_fan", 66, confirmLowCpuFan: true);
        var snapshots = await service.GetChannelsAsync();

        var systemFan = snapshots.Single(x => x.Id == "system_fan");
        var rearFan = snapshots.Single(x => x.Id == "rear_fan");

        Assert.True(applyResult.Success);
        Assert.Equal(66, systemFan.CurrentPercent);
        Assert.Equal(66, rearFan.CurrentPercent);
    }

    [Fact]
    public async Task ResetAsync_ShouldRestoreBaselineSpeed()
    {
        var service = CreateService();

        await service.SetSpeedAsync("system_fan", 72, confirmLowCpuFan: true);
        await service.ResetAsync("system_fan");

        var snapshots = await service.GetChannelsAsync();
        var systemFan = snapshots.Single(x => x.Id == "system_fan");

        Assert.Equal(40, systemFan.CurrentPercent);
    }

    [Fact]
    public async Task FullSpeedAllAsync_ShouldFailForMonitoringOnlyChannel()
    {
        var service = CreateService();

        var results = await service.FullSpeedAllAsync();

        Assert.Equal(4, results.Count);
        Assert.Contains(results, result => !result.Success && result.FailureReason == FanControlFailureReason.MonitoringOnly);
    }

    private static ManualFanControlService CreateService()
    {
        return new ManualFanControlService(
            controllerFactory: new FakeFactory(),
            logger: NullLogger<ManualFanControlService>.Instance);
    }

    private sealed class FakeFactory : IFanControllerFactory
    {
        public IFanControllerV2 Create(FanChannel channel)
        {
            return new FakeController(channel.SupportLevel == SupportLevel.FullControl);
        }
    }

    private sealed class FakeController : IFanControllerV2
    {
        private readonly bool _controllable;
        private int _currentPercent = 40;

        public FakeController(bool controllable)
        {
            _controllable = controllable;
        }

        public Task<bool> CanControlAsync(FanChannel channel)
        {
            return Task.FromResult(_controllable);
        }

        public Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
        {
            if (!_controllable)
            {
                return Task.FromResult(FanControlResult.Failed(
                    FanControlFailureReason.MonitoringOnly,
                    "Monitoring only."));
            }

            _currentPercent = percent;
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<int> GetCurrentSpeedAsync(FanChannel channel)
        {
            return Task.FromResult(_currentPercent);
        }

        public Task<HealthStatus> GetHealthStatusAsync()
        {
            return Task.FromResult(HealthStatus.Healthy());
        }
    }
}
