using FanControlPro.Application.Monitoring;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Monitoring;

public sealed class MonitoringSamplerTests
{
    [Fact]
    public async Task CaptureAsync_ShouldReturnValidReadings_WhenValuesAreWithinRange()
    {
        var reader = new ScriptedSensorReader(
            temperatureReadings: new Dictionary<string, Queue<double?>>
            {
                ["cpu-temp"] = new Queue<double?>(new double?[] { 50.5 })
            },
            fanReadings: new Dictionary<string, Queue<int?>>
            {
                ["cpu-fan"] = new Queue<int?>(new int?[] { 1333 })
            },
            loadReadings: new Queue<SystemLoadSnapshot>(new[] { new SystemLoadSnapshot(35, 20, 42) }));

        var sampler = new MonitoringSampler(
            reader,
            new SensorSanityValidator(),
            new StaticOptionsMonitor<MonitoringOptions>(new MonitoringOptions()));

        var snapshot = await sampler.CaptureAsync(new MonitoringTargets(
            TemperatureSensorIds: new[] { "cpu-temp" },
            FanSensorIds: new[] { "cpu-fan" }));

        Assert.Equal(50.5, snapshot.TemperaturesCelsius["cpu-temp"]);
        Assert.Equal(1333, snapshot.FanSpeedsRpm["cpu-fan"]);
        Assert.Empty(snapshot.ValidationIssues);
        Assert.Empty(snapshot.FaultySensorIds);
    }

    [Fact]
    public async Task CaptureAsync_ShouldMarkSensorAsFaulty_AfterThreshold()
    {
        var reader = new ScriptedSensorReader(
            temperatureReadings: new Dictionary<string, Queue<double?>>
            {
                ["cpu-temp"] = new Queue<double?>(new double?[] { 170.0, 171.0, 172.0 })
            },
            fanReadings: new Dictionary<string, Queue<int?>>
            {
                ["cpu-fan"] = new Queue<int?>(new int?[] { 1200, 1200, 1200 })
            },
            loadReadings: new Queue<SystemLoadSnapshot>(new[]
            {
                new SystemLoadSnapshot(20, 20, 20),
                new SystemLoadSnapshot(20, 20, 20),
                new SystemLoadSnapshot(20, 20, 20)
            }));

        var sampler = new MonitoringSampler(
            reader,
            new SensorSanityValidator(),
            new StaticOptionsMonitor<MonitoringOptions>(new MonitoringOptions
            {
                FaultySensorThreshold = 3
            }));

        var targets = new MonitoringTargets(
            TemperatureSensorIds: new[] { "cpu-temp" },
            FanSensorIds: new[] { "cpu-fan" });

        await sampler.CaptureAsync(targets);
        await sampler.CaptureAsync(targets);
        var snapshot = await sampler.CaptureAsync(targets);

        Assert.Contains(snapshot.FaultySensorIds, id => id == "cpu-temp");
    }

    private sealed class ScriptedSensorReader : ISensorReader
    {
        private readonly Dictionary<string, Queue<double?>> _temperatureReadings;
        private readonly Dictionary<string, Queue<int?>> _fanReadings;
        private readonly Queue<SystemLoadSnapshot> _loadReadings;

        public ScriptedSensorReader(
            Dictionary<string, Queue<double?>> temperatureReadings,
            Dictionary<string, Queue<int?>> fanReadings,
            Queue<SystemLoadSnapshot> loadReadings)
        {
            _temperatureReadings = temperatureReadings;
            _fanReadings = fanReadings;
            _loadReadings = loadReadings;
        }

        public Task<double?> ReadTemperatureAsync(string sensorId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DequeueOrKeepLast(_temperatureReadings, sensorId));
        }

        public Task<int?> ReadFanSpeedAsync(string fanId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DequeueOrKeepLast(_fanReadings, fanId));
        }

        public Task<SystemLoadSnapshot> ReadSystemLoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_loadReadings.Count == 0)
            {
                return Task.FromResult(new SystemLoadSnapshot(0, 0, 0));
            }

            var snapshot = _loadReadings.Count > 1
                ? _loadReadings.Dequeue()
                : _loadReadings.Peek();

            return Task.FromResult(snapshot);
        }

        private static T? DequeueOrKeepLast<T>(IDictionary<string, Queue<T?>> readings, string id)
        {
            if (!readings.TryGetValue(id, out var queue) || queue.Count == 0)
            {
                return default;
            }

            if (queue.Count == 1)
            {
                return queue.Peek();
            }

            return queue.Dequeue();
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
