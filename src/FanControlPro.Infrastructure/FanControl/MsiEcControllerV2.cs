namespace FanControlPro.Infrastructure.FanControl;

public sealed class MsiEcControllerV2 : VendorEcControllerBase
{
    public MsiEcControllerV2() : base(new[] { "MSI" })
    {
    }
}
