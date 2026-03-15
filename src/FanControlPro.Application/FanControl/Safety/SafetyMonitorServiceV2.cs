using FanControlPro.Application.Monitoring;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Application.FanControl.Safety;

public sealed class SafetyMonitorServiceV2 : ISafetyMonitorServiceV2, IDisposable
{
    private readonly IMonitoringLoopService _monitoringLoopService;
    private readonly IAppStateStore _appStateStore;
    private readonly IManualFanControlService _manualFanControlService;
    private readonly IOptionsMonitor<SafetyMonitorOptions> _optionsMonitor;
    private readonly IApplicationSettingsService? _settingsService;
    private readonly ILogger<SafetyMonitorServiceV2> _logger;

    private readonly SemaphoreSlim _startSemaphore = new(1, 1);
    private readonly object _sync = new();

    private bool _started;
    private int _criticalSampleStreak;
    private int _healthySampleStreak;
    private bool _emergencyFanBoostActive;

    private CancellationTokenSource? _watchdogCts;
    private Task? _watchdogTask;
    private HealthAttestation _attestation = HealthAttestation.WaitingForTelemetry();

    public SafetyMonitorServiceV2(
        IMonitoringLoopService monitoringLoopService,
        IAppStateStore appStateStore,
        IManualFanControlService manualFanControlService,
        IOptionsMonitor<SafetyMonitorOptions> optionsMonitor,
        ILogger<SafetyMonitorServiceV2> logger,
        IApplicationSettingsService? settingsService = null)
    {
        _monitoringLoopService = monitoringLoopService;
        _appStateStore = appStateStore;
        _manualFanControlService = manualFanControlService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _settingsService = settingsService;
    }

    public event EventHandler<HealthAttestation>? HealthAttestationChanged;

    public async Task EnterSafeModeAsync(SafeModeReason reason, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = _appStateStore.CurrentSnapshot;
        await EnterSafeModeCoreAsync(reason, snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ValidateSensorHealthAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return await ValidateSensorHealthCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HealthAttestation> GetHealthAttestationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            return _attestation;
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;
        Task? watchdogTask;

        lock (_sync)
        {
            cts = _watchdogCts;
            watchdogTask = _watchdogTask;
            _watchdogCts = null;
            _watchdogTask = null;
            _started = false;
        }

        if (cts is not null)
        {
            cts.Cancel();
        }

        if (watchdogTask is not null)
        {
            try
            {
                watchdogTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        cts?.Dispose();

        try
        {
            _monitoringLoopService.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Monitoring loop stop failed during safety monitor dispose.");
        }

        _startSemaphore.Dispose();
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _startSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            var targets = _optionsMonitor.CurrentValue.BuildMonitoringTargets();
            await _monitoringLoopService.StartAsync(targets, cancellationToken).ConfigureAwait(false);

            var watchdogCts = new CancellationTokenSource();
            lock (_sync)
            {
                _watchdogCts = watchdogCts;
                _watchdogTask = Task.Run(() => WatchdogLoopAsync(watchdogCts.Token), CancellationToken.None);
                _started = true;
            }

            _logger.LogInformation(
                "Safety monitor started (watchdog={IntervalSeconds}s, maxSnapshotAge={MaxAgeSeconds}s).",
                _optionsMonitor.CurrentValue.GetSafeWatchdogIntervalSeconds(),
                _optionsMonitor.CurrentValue.GetSafeMaxSnapshotAgeSeconds());
        }
        finally
        {
            _startSemaphore.Release();
        }
    }

    private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ValidateSensorHealthCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Safety watchdog iteration failed.");
            }

            var delaySeconds = _optionsMonitor.CurrentValue.GetSafeWatchdogIntervalSeconds();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<bool> ValidateSensorHealthCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _optionsMonitor.CurrentValue;
        var snapshot = _appStateStore.CurrentSnapshot;

        if (snapshot.CapturedAtUtc == DateTimeOffset.MinValue)
        {
            _criticalSampleStreak = 0;
            _healthySampleStreak = 0;
            UpdateAttestation(HealthAttestation.WaitingForTelemetry());
            return false;
        }

        var evaluation = EvaluateSnapshot(snapshot, options, _settingsService?.Current);

        if (evaluation.IsCritical)
        {
            _criticalSampleStreak++;
            _healthySampleStreak = 0;

            if (_criticalSampleStreak >= options.GetSafeCriticalSamplesForEmergency())
            {
                await EnterSafeModeCoreAsync(evaluation.Reason, snapshot, cancellationToken).ConfigureAwait(false);
                return false;
            }

            UpdateAttestation(new HealthAttestation(
                Level: SafetyLevel.Caution,
                Reason: evaluation.Reason,
                Message: "Safety warning detected. Waiting for next sample before emergency escalation.",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                FaultySensorIds: snapshot.FaultySensorIds,
                ValidationIssues: snapshot.ValidationIssues,
                TemperatureAlerts: evaluation.TemperatureAlerts,
                EmergencyFanBoostActive: _emergencyFanBoostActive));

            return false;
        }

        if (evaluation.IsCaution)
        {
            _criticalSampleStreak = 0;
            _healthySampleStreak = 0;

            var cautionMessage = evaluation.TemperatureAlerts.Count > 0
                ? "Temperature warning: threshold exceeded."
                : "Sensor warning: suspicious telemetry values detected.";

            UpdateAttestation(new HealthAttestation(
                Level: SafetyLevel.Caution,
                Reason: evaluation.Reason,
                Message: cautionMessage,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                FaultySensorIds: snapshot.FaultySensorIds,
                ValidationIssues: snapshot.ValidationIssues,
                TemperatureAlerts: evaluation.TemperatureAlerts,
                EmergencyFanBoostActive: _emergencyFanBoostActive));

            return false;
        }

        _criticalSampleStreak = 0;

        var currentLevel = GetCurrentLevel();
        if (currentLevel is SafetyLevel.Emergency or SafetyLevel.Shutdown)
        {
            _healthySampleStreak++;
            if (_healthySampleStreak < options.GetSafeRecoverySamplesToNormal())
            {
                UpdateAttestation(new HealthAttestation(
                    Level: SafetyLevel.Caution,
                    Reason: SafeModeReason.SensorSuspicious,
                    Message: "Recovery in progress. Waiting for consecutive healthy samples.",
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    FaultySensorIds: snapshot.FaultySensorIds,
                    ValidationIssues: snapshot.ValidationIssues,
                    TemperatureAlerts: evaluation.TemperatureAlerts,
                    EmergencyFanBoostActive: _emergencyFanBoostActive));

                return false;
            }
        }

        _healthySampleStreak = 0;
        _emergencyFanBoostActive = false;

        UpdateAttestation(new HealthAttestation(
            Level: SafetyLevel.Normal,
            Reason: SafeModeReason.None,
            Message: "Safety monitor healthy.",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            FaultySensorIds: snapshot.FaultySensorIds,
            ValidationIssues: snapshot.ValidationIssues,
            TemperatureAlerts: evaluation.TemperatureAlerts,
            EmergencyFanBoostActive: _emergencyFanBoostActive));

        return true;
    }

    private async Task EnterSafeModeCoreAsync(
        SafeModeReason reason,
        MonitoringSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var results = await _manualFanControlService.FullSpeedAllAsync(cancellationToken).ConfigureAwait(false);
        var successful = results.Count(result => result.Success);

        var options = _optionsMonitor.CurrentValue;
        var alerts = BuildTemperatureAlerts(snapshot, options, _settingsService?.Current);

        var level = successful > 0 ? SafetyLevel.Emergency : SafetyLevel.Shutdown;
        _emergencyFanBoostActive = successful > 0;
        _criticalSampleStreak = 0;
        _healthySampleStreak = 0;

        var message = level == SafetyLevel.Emergency
            ? $"Failsafe active ({reason}). Emergency Full Speed applied on {successful}/{results.Count} channels."
            : $"Failsafe escalation to shutdown ({reason}). Fan boost failed on all channels.";

        UpdateAttestation(new HealthAttestation(
            Level: level,
            Reason: reason,
            Message: message,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            FaultySensorIds: snapshot.FaultySensorIds,
            ValidationIssues: snapshot.ValidationIssues,
            TemperatureAlerts: alerts,
            EmergencyFanBoostActive: _emergencyFanBoostActive));

        _logger.LogWarning(
            "Safety safe mode entered: level={Level}, reason={Reason}, successfulChannels={Successful}/{Total}",
            level,
            reason,
            successful,
            results.Count);
    }

    private SafetyLevel GetCurrentLevel()
    {
        lock (_sync)
        {
            return _attestation.Level;
        }
    }

    private void UpdateAttestation(HealthAttestation next)
    {
        lock (_sync)
        {
            _attestation = next;
        }

        HealthAttestationChanged?.Invoke(this, next);
    }

    private static SnapshotEvaluation EvaluateSnapshot(
        MonitoringSnapshot snapshot,
        SafetyMonitorOptions options,
        ApplicationSettings? settings)
    {
        var now = DateTimeOffset.UtcNow;
        var staleLimit = TimeSpan.FromSeconds(options.GetSafeMaxSnapshotAgeSeconds());
        var isSnapshotStale = now - snapshot.CapturedAtUtc > staleLimit;

        var alerts = BuildTemperatureAlerts(snapshot, options, settings);
        var hasEmergencyTemperature = alerts.Any(alert => alert.IsEmergency);
        var hasCautionTemperature = alerts.Count > 0;

        var hasFaultySensors = snapshot.FaultySensorIds.Count > 0;
        var hasValidationIssues = snapshot.ValidationIssues.Count > 0;

        if (isSnapshotStale)
        {
            return new SnapshotEvaluation(
                IsHealthy: false,
                IsCaution: false,
                IsCritical: true,
                Reason: SafeModeReason.SensorLoss,
                TemperatureAlerts: alerts);
        }

        if (hasFaultySensors || hasValidationIssues || hasEmergencyTemperature)
        {
            var reason = hasEmergencyTemperature
                ? SafeModeReason.TemperatureThresholdExceeded
                : SafeModeReason.InvalidSensorReading;

            return new SnapshotEvaluation(
                IsHealthy: false,
                IsCaution: false,
                IsCritical: true,
                Reason: reason,
                TemperatureAlerts: alerts);
        }

        if (hasCautionTemperature)
        {
            return new SnapshotEvaluation(
                IsHealthy: false,
                IsCaution: true,
                IsCritical: false,
                Reason: SafeModeReason.TemperatureThresholdExceeded,
                TemperatureAlerts: alerts);
        }

        return new SnapshotEvaluation(
            IsHealthy: true,
            IsCaution: false,
            IsCritical: false,
            Reason: SafeModeReason.None,
            TemperatureAlerts: alerts);
    }

    private static IReadOnlyList<TemperatureAlert> BuildTemperatureAlerts(
        MonitoringSnapshot snapshot,
        SafetyMonitorOptions options,
        ApplicationSettings? settings)
    {
        var alerts = new List<TemperatureAlert>(capacity: 2);

        var (cpuCaution, cpuEmergency) = options.GetSafeCpuThresholds();
        if (settings is not null)
        {
            cpuCaution = Math.Clamp(settings.CpuAlertThresholdCelsius, 30, 120);
            cpuEmergency = Math.Clamp(cpuCaution + 8, cpuCaution + 1, 130);
        }

        if (TryGetTemperature(snapshot.TemperaturesCelsius, options.CpuTemperatureSensorId, out var cpuTemp) &&
            cpuTemp >= cpuCaution)
        {
            var isEmergency = cpuTemp >= cpuEmergency;
            alerts.Add(new TemperatureAlert(
                SensorId: options.CpuTemperatureSensorId,
                CurrentCelsius: cpuTemp,
                ThresholdCelsius: isEmergency ? cpuEmergency : cpuCaution,
                IsEmergency: isEmergency));
        }

        var (gpuCaution, gpuEmergency) = options.GetSafeGpuThresholds();
        if (settings is not null)
        {
            gpuCaution = Math.Clamp(settings.GpuAlertThresholdCelsius, 30, 120);
            gpuEmergency = Math.Clamp(gpuCaution + 8, gpuCaution + 1, 130);
        }

        if (TryGetTemperature(snapshot.TemperaturesCelsius, options.GpuTemperatureSensorId, out var gpuTemp) &&
            gpuTemp >= gpuCaution)
        {
            var isEmergency = gpuTemp >= gpuEmergency;
            alerts.Add(new TemperatureAlert(
                SensorId: options.GpuTemperatureSensorId,
                CurrentCelsius: gpuTemp,
                ThresholdCelsius: isEmergency ? gpuEmergency : gpuCaution,
                IsEmergency: isEmergency));
        }

        return alerts;
    }

    private static bool TryGetTemperature(
        IReadOnlyDictionary<string, double> temperatures,
        string sensorId,
        out double value)
    {
        if (temperatures.TryGetValue(sensorId, out value))
        {
            return true;
        }

        foreach (var entry in temperatures)
        {
            if (string.Equals(entry.Key, sensorId, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private sealed record SnapshotEvaluation(
        bool IsHealthy,
        bool IsCaution,
        bool IsCritical,
        SafeModeReason Reason,
        IReadOnlyList<TemperatureAlert> TemperatureAlerts);
}
