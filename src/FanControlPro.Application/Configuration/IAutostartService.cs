namespace FanControlPro.Application.Configuration;

public interface IAutostartService
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    Task ConfigureAsync(
        bool enabled,
        bool startMinimizedToTray,
        TimeSpan startupDelay,
        CancellationToken cancellationToken = default);
}
