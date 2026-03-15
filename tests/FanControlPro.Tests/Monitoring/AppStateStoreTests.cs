using FanControlPro.Application.Monitoring;

namespace FanControlPro.Tests.Monitoring;

public sealed class AppStateStoreTests
{
    [Fact]
    public void Publish_ShouldUpdateCurrentSnapshotAndRaiseEvent()
    {
        var store = new AppStateStore();
        var eventRaised = false;

        store.SnapshotUpdated += (_, snapshot) =>
        {
            eventRaised = true;
            Assert.Equal(45.2, snapshot.TemperaturesCelsius["cpu-temp"]);
        };

        var snapshot = new MonitoringSnapshot(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            TemperaturesCelsius: new Dictionary<string, double> { ["cpu-temp"] = 45.2 },
            FanSpeedsRpm: new Dictionary<string, int> { ["cpu-fan"] = 1200 },
            SystemLoad: new SystemLoadSnapshot(22, 14, 48),
            ValidationIssues: Array.Empty<SensorValidationIssue>(),
            FaultySensorIds: Array.Empty<string>());

        store.Publish(snapshot);

        Assert.True(eventRaised);
        Assert.Equal(snapshot, store.CurrentSnapshot);
    }
}
