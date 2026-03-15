using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class GigabyteEcControllerV2Tests
{
    [Fact]
    public async Task ShouldAcceptAorusVendorAlias()
    {
        var controller = new GigabyteEcControllerV2();
        var channel = new FanChannel("sys_fan", "System Fan", "AORUS", SupportLevel.FullControl, IsCpuChannel: false);

        var canControl = await controller.CanControlAsync(channel);
        var result = await controller.SetSpeedAsync(channel, 70);

        Assert.True(canControl);
        Assert.True(result.Success);
        Assert.Equal(70, result.AppliedPercent);
    }
}
