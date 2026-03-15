namespace FanControlPro.Infrastructure.FanControl;

public sealed class AsusEcControllerV2 : VendorEcControllerBase
{
    public AsusEcControllerV2() : base(new[] { "ASUS" })
    {
    }
}
