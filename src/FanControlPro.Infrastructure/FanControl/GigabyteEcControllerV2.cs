namespace FanControlPro.Infrastructure.FanControl;

public sealed class GigabyteEcControllerV2 : VendorEcControllerBase
{
    public GigabyteEcControllerV2() : base(new[] { "Gigabyte", "AORUS" })
    {
    }
}
