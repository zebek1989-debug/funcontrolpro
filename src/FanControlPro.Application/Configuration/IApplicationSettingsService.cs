namespace FanControlPro.Application.Configuration;

public interface IApplicationSettingsService
{
    event EventHandler<ApplicationSettings>? SettingsChanged;

    ApplicationSettings Current { get; }

    Task<ApplicationSettings> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<ApplicationSettingsValidationResult> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default);

    Task<ApplicationSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
