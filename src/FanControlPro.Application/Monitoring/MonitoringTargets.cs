namespace FanControlPro.Application.Monitoring;

public sealed record MonitoringTargets(
    IReadOnlyList<string> TemperatureSensorIds,
    IReadOnlyList<string> FanSensorIds)
{
    public static MonitoringTargets Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>());
}
