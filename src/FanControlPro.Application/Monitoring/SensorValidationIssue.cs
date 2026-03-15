namespace FanControlPro.Application.Monitoring;

public sealed record SensorValidationIssue(
    string SensorId,
    string Message,
    double? RawValue = null);
