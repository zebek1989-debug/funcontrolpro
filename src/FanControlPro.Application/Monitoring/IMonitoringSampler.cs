namespace FanControlPro.Application.Monitoring;

public interface IMonitoringSampler
{
    Task<MonitoringSnapshot> CaptureAsync(MonitoringTargets targets, CancellationToken cancellationToken = default);
}
