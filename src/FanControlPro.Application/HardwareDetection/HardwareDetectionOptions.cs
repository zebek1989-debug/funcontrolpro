namespace FanControlPro.Application.HardwareDetection;

public sealed class HardwareDetectionOptions
{
    public string CachePath { get; set; } = Path.Combine("data", "hardware.json");

    public bool LoadFromCacheWhenProbeUnavailable { get; set; } = true;
}
