namespace FanControlPro.Application.Monitoring;

public sealed class MonitoringOptions
{
    public int RefreshIntervalSeconds { get; set; } = 1;

    public int FaultySensorThreshold { get; set; } = 3;

    public int GetSafeRefreshIntervalSeconds() => Math.Clamp(RefreshIntervalSeconds, 1, 5);

    public int GetSafeFaultySensorThreshold() => Math.Max(1, FaultySensorThreshold);
}
