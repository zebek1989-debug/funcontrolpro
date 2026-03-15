using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Domain.FanControl.Profiles;
using FanControlPro.Infrastructure.Profiles;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class JsonProfileStoreTests
{
    [Fact]
    public async Task SaveAndLoadCustomProfiles_ShouldRoundTripData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"fancontrolpro-profiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = CreateStore(tempDir);
            var profile = new FanProfile(
                Name: "MyProfile",
                Description: "Test profile",
                IsPredefined: false,
                ChannelSettings: new[]
                {
                    new FanProfileChannelSetting("cpu_fan", 45),
                    new FanProfileChannelSetting("system_fan", 35)
                },
                UpdatedAtUtc: DateTimeOffset.UtcNow);

            await store.SaveCustomProfileAsync(profile);
            var loaded = await store.LoadCustomProfilesAsync();

            Assert.Single(loaded);
            Assert.Equal("MyProfile", loaded[0].Name);
            Assert.Equal(2, loaded[0].ChannelSettings.Count);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoadActiveProfileName_ShouldRoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"fancontrolpro-active-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = CreateStore(tempDir);

            await store.SaveActiveProfileNameAsync("Balanced");
            var active = await store.LoadActiveProfileNameAsync();

            Assert.Equal("Balanced", active);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static JsonProfileStore CreateStore(string root)
    {
        var options = Options.Create(new ProfileStorageOptions
        {
            ProfilesDirectoryPath = Path.Combine(root, "profiles"),
            ActiveProfilePath = Path.Combine(root, "active-profile.json")
        });

        return new JsonProfileStore(options);
    }
}
