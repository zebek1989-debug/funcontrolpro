namespace FanControlPro.Application.Configuration;

public sealed class AutostartOptions
{
    public string TaskName { get; set; } = "FanControlPro.Autostart";

    public bool EnableAutostart { get; set; }

    public bool StartMinimizedToTray { get; set; } = true;

    public int StartupDelaySeconds { get; set; } = 30;

    public string? ExecutablePath { get; set; }
}
