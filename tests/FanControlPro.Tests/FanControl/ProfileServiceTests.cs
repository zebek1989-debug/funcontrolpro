using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Profiles;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace FanControlPro.Tests.FanControl;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task GetProfilesAsync_ShouldIncludePredefinedProfiles()
    {
        var service = CreateService(new InMemoryProfileStore());

        var profiles = await service.GetProfilesAsync();

        Assert.Contains(profiles, p => p.Name == "Silent");
        Assert.Contains(profiles, p => p.Name == "Balanced");
        Assert.Contains(profiles, p => p.Name == "Performance");
        Assert.Contains(profiles, p => p.Name == "Custom");
    }

    [Fact]
    public async Task ActivateProfileAsync_ShouldApplySettingsToManualControl()
    {
        var store = new InMemoryProfileStore();
        var manual = new FakeManualControlService();
        var service = new ProfileService(store, manual, NullLogger<ProfileService>.Instance);

        var result = await service.ActivateProfileAsync("Performance");

        Assert.True(result.AppliedChannelCount > 0);
        Assert.True(manual.SetCalls > 0);
        Assert.Equal("Performance", result.ProfileName);
    }

    [Fact]
    public async Task SaveProfileAsync_ShouldPersistCustomProfile()
    {
        var store = new InMemoryProfileStore();
        var service = CreateService(store);

        var custom = new FanProfile(
            Name: "MyQuiet",
            Description: "my profile",
            IsPredefined: false,
            ChannelSettings: new[]
            {
                new FanProfileChannelSetting("cpu_fan", 35),
                new FanProfileChannelSetting("system_fan", 30)
            },
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        await service.SaveProfileAsync(custom);
        var profiles = await service.GetProfilesAsync();

        Assert.Contains(profiles, p => p.Name == "MyQuiet");
        Assert.Contains(store.SavedProfiles, p => p.Name == "MyQuiet");
    }

    [Fact]
    public async Task ActivateProfileAsync_ShouldPersistActiveProfileName()
    {
        var store = new InMemoryProfileStore();
        var service = CreateService(store);

        var result = await service.ActivateProfileAsync("Silent");

        Assert.True(result.Success);
        Assert.Equal("Silent", store.ActiveProfileName);
    }

    [Fact]
    public async Task GetActiveProfileAsync_ShouldRestorePersistedProfileName()
    {
        var store = new InMemoryProfileStore
        {
            ActiveProfileName = "Performance"
        };
        var service = CreateService(store);

        var active = await service.GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Equal("Performance", active.Name);
    }

    [Fact]
    public async Task SaveProfileAsync_ShouldRejectOverwritingPredefinedProfile()
    {
        var store = new InMemoryProfileStore();
        var service = CreateService(store);

        var profile = new FanProfile(
            Name: "Silent",
            Description: "override",
            IsPredefined: false,
            ChannelSettings: new[]
            {
                new FanProfileChannelSetting("cpu_fan", 40)
            },
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveProfileAsync(profile));
    }

    [Fact]
    public async Task ActivateProfileAsync_ShouldCompleteUnderOneSecond()
    {
        var service = CreateService(new InMemoryProfileStore());
        var stopwatch = Stopwatch.StartNew();

        var result = await service.ActivateProfileAsync("Balanced");

        stopwatch.Stop();
        Assert.True(result.Success);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Switching took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private static ProfileService CreateService(InMemoryProfileStore store)
    {
        return new ProfileService(
            store,
            new FakeManualControlService(),
            NullLogger<ProfileService>.Instance);
    }

    private sealed class InMemoryProfileStore : IProfileStore
    {
        public List<FanProfile> SavedProfiles { get; } = new();

        public string? ActiveProfileName { get; set; }

        public Task<IReadOnlyList<FanProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FanProfile>>(SavedProfiles.ToArray());
        }

        public Task SaveCustomProfileAsync(FanProfile profile, CancellationToken cancellationToken = default)
        {
            SavedProfiles.RemoveAll(existing => string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            SavedProfiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task<string?> LoadActiveProfileNameAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveProfileName);

        public Task SaveActiveProfileNameAsync(string profileName, CancellationToken cancellationToken = default)
        {
            ActiveProfileName = profileName;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeManualControlService : IManualFanControlService
    {
        public int SetCalls { get; private set; }

        public IReadOnlyList<string> AvailableGroups { get; } = new[] { "None", "All Fans" };

        public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(new[]
            {
                new FanChannelSnapshot("cpu_fan", "CPU Fan", "PWM", 40, 1200, 20, 100, true, true, "Full Control", "None"),
                new FanChannelSnapshot("system_fan", "System Fan", "PWM", 35, 950, 0, 100, false, true, "Full Control", "None")
            });
        }

        public Task<FanControlResult> SetSpeedAsync(string channelId, int percent, bool confirmLowCpuFan, CancellationToken cancellationToken = default)
        {
            SetCalls++;
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(40, "Reset"));

        public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));

        public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FanControlResult>>(Array.Empty<FanControlResult>());

        public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
