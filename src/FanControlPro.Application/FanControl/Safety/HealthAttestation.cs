using FanControlPro.Application.Monitoring;

namespace FanControlPro.Application.FanControl.Safety;

public sealed record HealthAttestation(
    SafetyLevel Level,
    SafeModeReason Reason,
    string Message,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> FaultySensorIds,
    IReadOnlyList<SensorValidationIssue> ValidationIssues,
    IReadOnlyList<TemperatureAlert> TemperatureAlerts,
    bool EmergencyFanBoostActive)
{
    public static HealthAttestation WaitingForTelemetry(string message = "Safety monitor waiting for telemetry.") =>
        new(
            Level: SafetyLevel.Caution,
            Reason: SafeModeReason.SensorSuspicious,
            Message: message,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            FaultySensorIds: Array.Empty<string>(),
            ValidationIssues: Array.Empty<SensorValidationIssue>(),
            TemperatureAlerts: Array.Empty<TemperatureAlert>(),
            EmergencyFanBoostActive: false);

    public static HealthAttestation Healthy(string message = "Safety monitor healthy.") =>
        new(
            Level: SafetyLevel.Normal,
            Reason: SafeModeReason.None,
            Message: message,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            FaultySensorIds: Array.Empty<string>(),
            ValidationIssues: Array.Empty<SensorValidationIssue>(),
            TemperatureAlerts: Array.Empty<TemperatureAlert>(),
            EmergencyFanBoostActive: false);
}
