using FanControlPro.Application.Monitoring;

namespace FanControlPro.Application.FanControl.Safety;

public sealed class SafetyMonitorOptions
{
    public int WatchdogIntervalSeconds { get; set; } = 2;

    public int MaxSnapshotAgeSeconds { get; set; } = 5;

    public int CriticalSamplesForEmergency { get; set; } = 1;

    public int RecoverySamplesToNormal { get; set; } = 2;

    public string CpuTemperatureSensorId { get; set; } = "cpu_temp";

    public string GpuTemperatureSensorId { get; set; } = "gpu_temp";

    public double CpuCautionThresholdCelsius { get; set; } = 80;

    public double CpuEmergencyThresholdCelsius { get; set; } = 90;

    public double GpuCautionThresholdCelsius { get; set; } = 82;

    public double GpuEmergencyThresholdCelsius { get; set; } = 92;

    public string[] TemperatureSensorIds { get; set; } = { "cpu_temp", "gpu_temp", "mb_temp" };

    public string[] FanSensorIds { get; set; } = { "cpu_fan", "system_fan", "rear_fan", "gpu_fan" };

    public int GetSafeWatchdogIntervalSeconds() => Math.Clamp(WatchdogIntervalSeconds, 1, 10);

    public int GetSafeMaxSnapshotAgeSeconds() => Math.Clamp(MaxSnapshotAgeSeconds, 2, 30);

    public int GetSafeCriticalSamplesForEmergency() => Math.Clamp(CriticalSamplesForEmergency, 1, 5);

    public int GetSafeRecoverySamplesToNormal() => Math.Clamp(RecoverySamplesToNormal, 1, 10);

    public (double Caution, double Emergency) GetSafeCpuThresholds()
    {
        var caution = Math.Clamp(CpuCautionThresholdCelsius, 30, 120);
        var emergency = Math.Clamp(CpuEmergencyThresholdCelsius, caution + 1, 130);
        return (caution, emergency);
    }

    public (double Caution, double Emergency) GetSafeGpuThresholds()
    {
        var caution = Math.Clamp(GpuCautionThresholdCelsius, 30, 120);
        var emergency = Math.Clamp(GpuEmergencyThresholdCelsius, caution + 1, 130);
        return (caution, emergency);
    }

    public MonitoringTargets BuildMonitoringTargets()
    {
        return new MonitoringTargets(
            NormalizeSensorIds(TemperatureSensorIds),
            NormalizeSensorIds(FanSensorIds));
    }

    private static IReadOnlyList<string> NormalizeSensorIds(IEnumerable<string>? source)
    {
        if (source is null)
        {
            return Array.Empty<string>();
        }

        return source
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
