using FanControlPro.Application.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Monitoring;

public sealed class MonitoringLoopServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldRunInBackgroundAndPublishSnapshot()
    {
        var sampler = new FakeSampler();
        var stateStore = new AppStateStore();
        var optionsMonitor = new StaticOptionsMonitor<MonitoringOptions>(new MonitoringOptions
        {
            RefreshIntervalSeconds = 1
        });

        var loop = new MonitoringLoopService(
            sampler,
            stateStore,
            optionsMonitor,
            NullLogger<MonitoringLoopService>.Instance);

        var snapshotPublished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        stateStore.SnapshotUpdated += (_, _) => snapshotPublished.TrySetResult(true);

        await loop.StartAsync(new MonitoringTargets(new[] { "cpu-temp" }, new[] { "cpu-fan" }));

        var completed = await Task.WhenAny(snapshotPublished.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        await loop.StopAsync();

        Assert.Same(snapshotPublished.Task, completed);
        Assert.True(snapshotPublished.Task.Result);
        Assert.True(sampler.CallCount >= 1);
    }

    [Fact]
    public void MonitoringOptions_ShouldClampRefreshIntervalToOneToFiveSeconds()
    {
        var optionsLow = new MonitoringOptions { RefreshIntervalSeconds = -10 };
        var optionsHigh = new MonitoringOptions { RefreshIntervalSeconds = 99 };

        Assert.Equal(1, optionsLow.GetSafeRefreshIntervalSeconds());
        Assert.Equal(5, optionsHigh.GetSafeRefreshIntervalSeconds());
    }

    [Fact]
    public async Task StartAsync_WhenLibreHardwareMonitorDependencyIsMissing_ShouldSwitchToFallbackTelemetry()
    {
        var sampler = new MissingLibreHardwareMonitorSampler();
        var stateStore = new AppStateStore();
        var optionsMonitor = new StaticOptionsMonitor<MonitoringOptions>(new MonitoringOptions
        {
            RefreshIntervalSeconds = 1
        });

        var loop = new MonitoringLoopService(
            sampler,
            stateStore,
            optionsMonitor,
            NullLogger<MonitoringLoopService>.Instance);

        var snapshotPublished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        stateStore.SnapshotUpdated += (_, _) => snapshotPublished.TrySetResult(true);

        await loop.StartAsync(new MonitoringTargets(new[] { "cpu-temp" }, new[] { "cpu-fan" }));

        var completed = await Task.WhenAny(snapshotPublished.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(snapshotPublished.Task, completed);
        Assert.True(snapshotPublished.Task.Result);

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        var snapshot = stateStore.CurrentSnapshot;
        Assert.Equal(1, sampler.CallCount);
        Assert.NotEqual(DateTimeOffset.MinValue, snapshot.CapturedAtUtc);
        Assert.Empty(snapshot.ValidationIssues);
        Assert.Empty(snapshot.FaultySensorIds);

        await loop.StopAsync();
    }

    private sealed class FakeSampler : IMonitoringSampler
    {
        public int CallCount { get; private set; }

        public Task<MonitoringSnapshot> CaptureAsync(MonitoringTargets targets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            var snapshot = new MonitoringSnapshot(
                CapturedAtUtc: DateTimeOffset.UtcNow,
                TemperaturesCelsius: new Dictionary<string, double> { ["cpu-temp"] = 41.1 },
                FanSpeedsRpm: new Dictionary<string, int> { ["cpu-fan"] = 1210 },
                SystemLoad: new SystemLoadSnapshot(15, 10, 38),
                ValidationIssues: Array.Empty<SensorValidationIssue>(),
                FaultySensorIds: Array.Empty<string>());

            return Task.FromResult(snapshot);
        }
    }

    private sealed class MissingLibreHardwareMonitorSampler : IMonitoringSampler
    {
        public int CallCount { get; private set; }

        public Task<MonitoringSnapshot> CaptureAsync(MonitoringTargets targets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            throw new FileNotFoundException(
                "Could not load file or assembly 'LibreHardwareMonitorLib, Version=0.9.6.0'.",
                "LibreHardwareMonitorLib");
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

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
