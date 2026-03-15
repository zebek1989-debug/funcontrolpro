namespace FanControlPro.Application.Configuration;

public sealed record TrayStatus(
    string ActiveProfile,
    string SafetyState,
    string StatusMessage,
    bool IsMainWindowVisible)
{
    public static TrayStatus Initial { get; } = new(
        ActiveProfile: "Balanced",
        SafetyState: "Caution",
        StatusMessage: "Starting up...",
        IsMainWindowVisible: true);
}
