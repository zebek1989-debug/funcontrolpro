using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class AsusEcControllerV2Tests
{
    [Fact]
    public async Task ShouldControlAsusChannel_WhenSupportIsFullControl()
    {
        var controller = new AsusEcControllerV2();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var canControl = await controller.CanControlAsync(channel);
        var result = await controller.SetSpeedAsync(channel, 35);

        Assert.True(canControl);
        Assert.True(result.Success);
        Assert.Equal(35, result.AppliedPercent);
    }

    [Fact]
    public async Task ShouldRejectCpuSpeedBelowSafeMinimum()
    {
        var controller = new AsusEcControllerV2();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var result = await controller.SetSpeedAsync(channel, 10);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.OutOfRange, result.FailureReason);
    }
}
