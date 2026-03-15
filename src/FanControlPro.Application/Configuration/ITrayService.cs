namespace FanControlPro.Application.Configuration;

public interface ITrayService : IDisposable
{
    event EventHandler? ShowRequested;

    event EventHandler? HideRequested;

    event EventHandler? FullSpeedRequested;

    event EventHandler? ExitRequested;

    event EventHandler<TrayProfileSwitchRequestedEventArgs>? ProfileSwitchRequested;

    void Show();

    void Hide();

    void UpdateStatus(TrayStatus status);

    void UpdateProfiles(IReadOnlyList<string> profiles, string activeProfile);
}
