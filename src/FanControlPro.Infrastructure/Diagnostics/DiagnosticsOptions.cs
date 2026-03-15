namespace FanControlPro.Infrastructure.Diagnostics;

public sealed class DiagnosticsOptions
{
    public string LogsDirectoryPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FanControlPro",
        "logs");

    public string DataRootPath { get; set; } = Path.Combine("data");

    public string SupportBundlesDirectoryPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FanControlPro",
        "support");
}
