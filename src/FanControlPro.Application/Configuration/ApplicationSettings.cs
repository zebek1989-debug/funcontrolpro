namespace FanControlPro.Application.Configuration;

public sealed record ApplicationSettings(
    int PollingIntervalSeconds,
    double CpuAlertThresholdCelsius,
    double GpuAlertThresholdCelsius,
    ApplicationTheme Theme,
    bool EnableAutostart,
    bool StartMinimizedToTray,
    bool MinimizeToTrayOnClose,
    int StartupDelaySeconds,
    string DefaultProfileName)
{
    public static ApplicationSettings Default { get; } = new(
        PollingIntervalSeconds: 1,
        CpuAlertThresholdCelsius: 80,
        GpuAlertThresholdCelsius: 82,
        Theme: ApplicationTheme.System,
        EnableAutostart: false,
        StartMinimizedToTray: true,
        MinimizeToTrayOnClose: true,
        StartupDelaySeconds: 30,
        DefaultProfileName: "Balanced");
}
