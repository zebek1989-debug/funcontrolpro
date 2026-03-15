namespace FanControlPro.Application.Monitoring;

public interface IMonitoringLoopService
{
    bool IsRunning { get; }

    Task StartAsync(MonitoringTargets targets, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
