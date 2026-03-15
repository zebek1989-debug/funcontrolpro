namespace FanControlPro.Application.Configuration;

public sealed class TrayProfileSwitchRequestedEventArgs : EventArgs
{
    public TrayProfileSwitchRequestedEventArgs(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));
        }

        ProfileName = profileName.Trim();
    }

    public string ProfileName { get; }
}
