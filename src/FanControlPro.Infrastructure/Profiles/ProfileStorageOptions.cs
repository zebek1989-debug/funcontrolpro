namespace FanControlPro.Infrastructure.Profiles;

public sealed class ProfileStorageOptions
{
    public string ProfilesDirectoryPath { get; set; } = Path.Combine("data", "profiles");

    public string ActiveProfilePath { get; set; } = Path.Combine("data", "active-profile.json");
}
