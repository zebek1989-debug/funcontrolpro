namespace FanControlPro.Application.Monitoring;

public sealed class SensorSanityValidator : ISensorSanityValidator
{
    public SensorValidationIssue? ValidateTemperature(string sensorId, double? temperatureCelsius)
    {
        if (temperatureCelsius is null)
        {
            return new SensorValidationIssue(sensorId, "Temperature reading is missing.");
        }

        if (temperatureCelsius > 150)
        {
            return new SensorValidationIssue(sensorId, "Temperature is above safe sanity threshold (150C).", temperatureCelsius);
        }

        if (temperatureCelsius < -20)
        {
            return new SensorValidationIssue(sensorId, "Temperature is below expected range.", temperatureCelsius);
        }

        return null;
    }

    public SensorValidationIssue? ValidateFanSpeed(string sensorId, int? fanSpeedRpm)
    {
        if (fanSpeedRpm is null)
        {
            return new SensorValidationIssue(sensorId, "Fan RPM reading is missing.");
        }

        if (fanSpeedRpm < 0)
        {
            return new SensorValidationIssue(sensorId, "Fan RPM cannot be negative.", fanSpeedRpm);
        }

        if (fanSpeedRpm > 10_000)
        {
            return new SensorValidationIssue(sensorId, "Fan RPM is above sanity threshold (10000 RPM).", fanSpeedRpm);
        }

        return null;
    }

    public IReadOnlyList<SensorValidationIssue> ValidateSystemLoad(SystemLoadSnapshot loadSnapshot)
    {
        var issues = new List<SensorValidationIssue>(capacity: 3);

        ValidateLoad("system-load/cpu", loadSnapshot.CpuLoadPercent, issues);
        ValidateLoad("system-load/gpu", loadSnapshot.GpuLoadPercent, issues);
        ValidateLoad("system-load/memory", loadSnapshot.MemoryLoadPercent, issues);

        return issues;
    }

    private static void ValidateLoad(string sensorId, double loadPercent, ICollection<SensorValidationIssue> issues)
    {
        if (loadPercent < 0 || loadPercent > 100)
        {
            issues.Add(new SensorValidationIssue(sensorId, "Load percentage must be between 0 and 100.", loadPercent));
        }
    }
}
