using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Domain.FanControl;

public sealed record FanChannel(
    string Id,
    string Name,
    string? Vendor,
    SupportLevel SupportLevel,
    bool IsCpuChannel,
    int MinimumPercent = 0,
    int MaximumPercent = 100)
{
    public int SafeMinimumPercent => IsCpuChannel ? Math.Max(20, MinimumPercent) : Math.Max(0, MinimumPercent);
}
