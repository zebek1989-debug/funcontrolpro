using Microsoft.Extensions.Options;

namespace FanControlPro.Application.Monitoring;

public sealed class MonitoringSampler : IMonitoringSampler
{
    private readonly ISensorReader _sensorReader;
    private readonly ISensorSanityValidator _validator;
    private readonly IOptionsMonitor<MonitoringOptions> _optionsMonitor;
    private readonly Dictionary<string, int> _invalidStreakBySensor = new(StringComparer.OrdinalIgnoreCase);

    public MonitoringSampler(
        ISensorReader sensorReader,
        ISensorSanityValidator validator,
        IOptionsMonitor<MonitoringOptions> optionsMonitor)
    {
        _sensorReader = sensorReader;
        _validator = validator;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<MonitoringSnapshot> CaptureAsync(
        MonitoringTargets targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var validationIssues = new List<SensorValidationIssue>();
        var temperatures = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fanSpeeds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var sensorId in targets.TemperatureSensorIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temperature = await _sensorReader.ReadTemperatureAsync(sensorId, cancellationToken).ConfigureAwait(false);
            var issue = _validator.ValidateTemperature(sensorId, temperature);

            if (issue is null && temperature is not null)
            {
                temperatures[sensorId] = temperature.Value;
                ResetInvalidStreak(sensorId);
            }
            else if (issue is not null)
            {
                validationIssues.Add(issue);
                IncrementInvalidStreak(sensorId);
            }
        }

        foreach (var fanId in targets.FanSensorIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fanRpm = await _sensorReader.ReadFanSpeedAsync(fanId, cancellationToken).ConfigureAwait(false);
            var issue = _validator.ValidateFanSpeed(fanId, fanRpm);

            if (issue is null && fanRpm is not null)
            {
                fanSpeeds[fanId] = fanRpm.Value;
                ResetInvalidStreak(fanId);
            }
            else if (issue is not null)
            {
                validationIssues.Add(issue);
                IncrementInvalidStreak(fanId);
            }
        }

        var load = await _sensorReader.ReadSystemLoadAsync(cancellationToken).ConfigureAwait(false);
        validationIssues.AddRange(_validator.ValidateSystemLoad(load));

        var faultyThreshold = _optionsMonitor.CurrentValue.GetSafeFaultySensorThreshold();
        var faultySensors = _invalidStreakBySensor
            .Where(kvp => kvp.Value >= faultyThreshold)
            .Select(kvp => kvp.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MonitoringSnapshot(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            TemperaturesCelsius: temperatures,
            FanSpeedsRpm: fanSpeeds,
            SystemLoad: load,
            ValidationIssues: validationIssues,
            FaultySensorIds: faultySensors);
    }

    private void IncrementInvalidStreak(string sensorId)
    {
        _invalidStreakBySensor.TryGetValue(sensorId, out var streak);
        _invalidStreakBySensor[sensorId] = streak + 1;
    }

    private void ResetInvalidStreak(string sensorId)
    {
        _invalidStreakBySensor.Remove(sensorId);
    }
}
