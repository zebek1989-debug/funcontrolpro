using FanControlPro.Application.Configuration;

namespace FanControlPro.Presentation.Services;

public sealed class NullTrayService : ITrayService
{
    public event EventHandler? ShowRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? HideRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? FullSpeedRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? ExitRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<TrayProfileSwitchRequestedEventArgs>? ProfileSwitchRequested
    {
        add { }
        remove { }
    }

    public void Show()
    {
    }

    public void Hide()
    {
    }

    public void UpdateStatus(TrayStatus status)
    {
    }

    public void UpdateProfiles(IReadOnlyList<string> profiles, string activeProfile)
    {
    }

    public void Dispose()
    {
    }
}
