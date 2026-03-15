using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class MonitoringOnlyControllerTests
{
    [Fact]
    public async Task ShouldAlwaysBlockControlWrites()
    {
        var controller = new MonitoringOnlyController();
        var channel = new FanChannel("sys_fan", "System Fan", null, SupportLevel.MonitoringOnly, IsCpuChannel: false);

        var canControl = await controller.CanControlAsync(channel);
        var result = await controller.SetSpeedAsync(channel, 50);

        Assert.False(canControl);
        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.MonitoringOnly, result.FailureReason);
    }
}
