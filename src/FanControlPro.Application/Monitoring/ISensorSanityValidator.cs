namespace FanControlPro.Application.Monitoring;

public interface ISensorSanityValidator
{
    SensorValidationIssue? ValidateTemperature(string sensorId, double? temperatureCelsius);

    SensorValidationIssue? ValidateFanSpeed(string sensorId, int? fanSpeedRpm);

    IReadOnlyList<SensorValidationIssue> ValidateSystemLoad(SystemLoadSnapshot loadSnapshot);
}
