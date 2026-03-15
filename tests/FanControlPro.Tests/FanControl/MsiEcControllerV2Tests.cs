using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class MsiEcControllerV2Tests
{
    [Fact]
    public async Task ShouldNotControlWhenSupportIsMonitoringOnly()
    {
        var controller = new MsiEcControllerV2();
        var channel = new FanChannel("sys_fan", "System Fan", "MSI", SupportLevel.MonitoringOnly, IsCpuChannel: false);

        var canControl = await controller.CanControlAsync(channel);

        Assert.False(canControl);
    }
}
