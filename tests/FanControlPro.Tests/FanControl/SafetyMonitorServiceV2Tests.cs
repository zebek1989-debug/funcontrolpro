using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Safety;
using FanControlPro.Application.Monitoring;
using FanControlPro.Domain.FanControl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.FanControl;

public sealed class SafetyMonitorServiceV2Tests
{
    [Fact]
    public async Task ValidateSensorHealthAsync_ShouldEnterEmergency_WhenSnapshotIsStale()
    {
        var appStateStore = new AppStateStore();
        appStateStore.Publish(CreateSnapshot(capturedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-10)));

        var manualControl = new FakeManualFanControlService();
        using var service = CreateService(
            appStateStore,
            manualControl,
            new SafetyMonitorOptions
            {
                WatchdogIntervalSeconds = 1,
                MaxSnapshotAgeSeconds = 2,
                CriticalSamplesForEmergency = 1
            });

        var attestation = await WaitForAttestationAsync(
            service,
            condition: state => state.Level == SafetyLevel.Emergency && state.Reason == SafeModeReason.SensorLoss,
            timeout: TimeSpan.FromSeconds(4));

        Assert.True(manualControl.FullSpeedAllCalls >= 1);
        Assert.True(attestation.EmergencyFanBoostActive);
    }

    [Fact]
    public async Task ValidateSensorHealthAsync_ShouldEnterEmergency_WhenSensorReadingsAreInvalid()
    {
        var appStateStore = new AppStateStore();
        appStateStore.Publish(CreateSnapshot(
            capturedAtUtc: DateTimeOffset.UtcNow,
            validationIssues: new[]
            {
                new SensorValidationIssue("cpu_temp", "Temperature reading is missing.")
            }));

        var manualControl = new FakeManualFanControlService();
        using var service = CreateService(
            appStateStore,
            manualControl,
            new SafetyMonitorOptions
            {
                WatchdogIntervalSeconds = 1,
                MaxSnapshotAgeSeconds = 5,
                CriticalSamplesForEmergency = 1
            });

        var attestation = await WaitForAttestationAsync(
            service,
            condition: state => state.Level == SafetyLevel.Emergency && state.Reason == SafeModeReason.InvalidSensorReading,
            timeout: TimeSpan.FromSeconds(4));

        Assert.True(manualControl.FullSpeedAllCalls >= 1);
        Assert.Equal(SafeModeReason.InvalidSensorReading, attestation.Reason);
    }

    [Fact]
    public async Task ValidateSensorHealthAsync_ShouldRecoverToNormal_AfterHealthySamples()
    {
        var appStateStore = new AppStateStore();
        appStateStore.Publish(CreateSnapshot(
            capturedAtUtc: DateTimeOffset.UtcNow,
            faultySensorIds: new[] { "cpu_temp" }));

        var manualControl = new FakeManualFanControlService();
        using var service = CreateService(
            appStateStore,
            manualControl,
            new SafetyMonitorOptions
            {
                WatchdogIntervalSeconds = 1,
                MaxSnapshotAgeSeconds = 20,
                CriticalSamplesForEmergency = 1,
                RecoverySamplesToNormal = 2
            });

        await WaitForAttestationAsync(
            service,
            condition: state => state.Level == SafetyLevel.Emergency,
            timeout: TimeSpan.FromSeconds(4));

        appStateStore.Publish(CreateSnapshot(
            capturedAtUtc: DateTimeOffset.UtcNow,
            temperatures: new Dictionary<string, double>
            {
                ["cpu_temp"] = 45,
                ["gpu_temp"] = 48
            },
            faultySensorIds: Array.Empty<string>(),
            validationIssues: Array.Empty<SensorValidationIssue>()));

        var recovered = await WaitForAttestationAsync(
            service,
            condition: state => state.Level == SafetyLevel.Normal,
            timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(SafetyLevel.Normal, recovered.Level);
        Assert.False(recovered.EmergencyFanBoostActive);
    }

    private static MonitoringSnapshot CreateSnapshot(
        DateTimeOffset capturedAtUtc,
        IReadOnlyDictionary<string, double>? temperatures = null,
        IReadOnlyList<string>? faultySensorIds = null,
        IReadOnlyList<SensorValidationIssue>? validationIssues = null)
    {
        return new MonitoringSnapshot(
            CapturedAtUtc: capturedAtUtc,
            TemperaturesCelsius: temperatures ?? new Dictionary<string, double>
            {
                ["cpu_temp"] = 50,
                ["gpu_temp"] = 55
            },
            FanSpeedsRpm: new Dictionary<string, int>
            {
                ["cpu_fan"] = 1250
            },
            SystemLoad: new SystemLoadSnapshot(25, 18, 42),
            ValidationIssues: validationIssues ?? Array.Empty<SensorValidationIssue>(),
            FaultySensorIds: faultySensorIds ?? Array.Empty<string>());
    }

    private static SafetyMonitorServiceV2 CreateService(
        IAppStateStore appStateStore,
        IManualFanControlService manualFanControlService,
        SafetyMonitorOptions options)
    {
        return new SafetyMonitorServiceV2(
            new FakeMonitoringLoopService(),
            appStateStore,
            manualFanControlService,
            new StaticOptionsMonitor<SafetyMonitorOptions>(options),
            NullLogger<SafetyMonitorServiceV2>.Instance);
    }

    private static async Task<HealthAttestation> WaitForAttestationAsync(
        ISafetyMonitorServiceV2 service,
        Func<HealthAttestation, bool> condition,
        TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<HealthAttestation>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, HealthAttestation state)
        {
            if (condition(state))
            {
                tcs.TrySetResult(state);
            }
        }

        service.HealthAttestationChanged += Handler;
        try
        {
            var current = await service.GetHealthAttestationAsync();
            if (condition(current))
            {
                return current;
            }

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Assert.Same(tcs.Task, completed);
            return await tcs.Task;
        }
        finally
        {
            service.HealthAttestationChanged -= Handler;
        }
    }

    private sealed class FakeMonitoringLoopService : IMonitoringLoopService
    {
        public bool IsRunning { get; private set; }

        public Task StartAsync(MonitoringTargets targets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeManualFanControlService : IManualFanControlService
    {
        public int FullSpeedAllCalls { get; private set; }

        public IReadOnlyList<string> AvailableGroups { get; } = new[] { "None" };

        public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(new[]
            {
                new FanChannelSnapshot("cpu_fan", "CPU Fan", "PWM", 55, 1240, 20, 100, true, true, "Full Control", "None")
            });
        }

        public Task<FanControlResult> SetSpeedAsync(string channelId, int percent, bool confirmLowCpuFan, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FanControlResult.Succeeded(55, "Reset"));
        }

        public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));
        }

        public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FullSpeedAllCalls++;
            return Task.FromResult<IReadOnlyList<FanControlResult>>(new[]
            {
                FanControlResult.Succeeded(100, "Full speed"),
                FanControlResult.Succeeded(100, "Full speed")
            });
        }

        public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
