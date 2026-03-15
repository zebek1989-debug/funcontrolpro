namespace FanControlPro.Infrastructure.Settings;

public sealed class ApplicationSettingsStorageOptions
{
    public string SettingsFilePath { get; set; } = Path.Combine("data", "settings.json5");
}
