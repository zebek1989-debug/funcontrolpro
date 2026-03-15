namespace FanControlPro.Application.Monitoring;

public sealed record MonitoringSnapshot(
    DateTimeOffset CapturedAtUtc,
    IReadOnlyDictionary<string, double> TemperaturesCelsius,
    IReadOnlyDictionary<string, int> FanSpeedsRpm,
    SystemLoadSnapshot SystemLoad,
    IReadOnlyList<SensorValidationIssue> ValidationIssues,
    IReadOnlyList<string> FaultySensorIds)
{
    public static MonitoringSnapshot Empty { get; } = new(
        CapturedAtUtc: DateTimeOffset.MinValue,
        TemperaturesCelsius: new Dictionary<string, double>(),
        FanSpeedsRpm: new Dictionary<string, int>(),
        SystemLoad: new SystemLoadSnapshot(0, 0, 0),
        ValidationIssues: Array.Empty<SensorValidationIssue>(),
        FaultySensorIds: Array.Empty<string>());
}
