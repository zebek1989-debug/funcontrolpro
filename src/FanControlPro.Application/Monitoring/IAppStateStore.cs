namespace FanControlPro.Application.Monitoring;

public interface IAppStateStore
{
    MonitoringSnapshot CurrentSnapshot { get; }

    event EventHandler<MonitoringSnapshot>? SnapshotUpdated;

    void Publish(MonitoringSnapshot snapshot);
}
