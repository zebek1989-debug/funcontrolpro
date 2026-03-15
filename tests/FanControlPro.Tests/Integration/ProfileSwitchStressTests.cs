using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Profiles;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace FanControlPro.Tests.Integration;

public sealed class ProfileSwitchStressTests
{
    [Fact]
    public async Task ActivateProfileAsync_ShouldHandleRapidSwitchingWithoutFailures()
    {
        const int iterations = 250;
        var sequence = new[] { "Silent", "Balanced", "Performance", "Custom" };

        var store = new InMemoryProfileStore();
        var manualControl = new FakeManualControlService();
        var service = new ProfileService(store, manualControl, NullLogger<ProfileService>.Instance);

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            var selectedProfile = sequence[i % sequence.Length];
            var result = await service.ActivateProfileAsync(selectedProfile);
            Assert.True(result.Success, $"Activation failed for '{selectedProfile}' at iteration {i}: {string.Join(" | ", result.Errors)}");
        }

        stopwatch.Stop();

        var activeProfile = await service.GetActiveProfileAsync();
        Assert.NotNull(activeProfile);
        Assert.Equal(sequence[(iterations - 1) % sequence.Length], activeProfile.Name);

        Assert.True(manualControl.SetCalls >= iterations, "Expected multiple control writes during stress run.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Stress profile switching took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private sealed class InMemoryProfileStore : IProfileStore
    {
        private readonly Dictionary<string, FanProfile> _profilesByName = new(StringComparer.OrdinalIgnoreCase);

        public string? ActiveProfileName { get; private set; }

        public Task<IReadOnlyList<FanProfile>> LoadCustomProfilesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FanProfile>>(_profilesByName.Values.ToArray());
        }

        public Task SaveCustomProfileAsync(FanProfile profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profilesByName[profile.Name] = profile;
            return Task.CompletedTask;
        }

        public Task<string?> LoadActiveProfileNameAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ActiveProfileName);
        }

        public Task SaveActiveProfileNameAsync(string profileName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActiveProfileName = profileName;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeManualControlService : IManualFanControlService
    {
        public int SetCalls { get; private set; }

        public IReadOnlyList<string> AvailableGroups { get; } = new[] { "None", "Case Fans", "Radiator" };

        public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(new[]
            {
                new FanChannelSnapshot("cpu_fan", "CPU Fan", "PWM", 42, 1250, 20, 100, true, true, "Full Control", "None"),
                new FanChannelSnapshot("sys_fan_1", "System Fan 1", "PWM", 38, 980, 0, 100, false, true, "Full Control", "Case Fans"),
                new FanChannelSnapshot("sys_fan_2", "System Fan 2", "PWM", 40, 1010, 0, 100, false, true, "Full Control", "Case Fans")
            });
        }

        public Task<FanControlResult> SetSpeedAsync(string channelId, int percent, bool confirmLowCpuFan, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCalls++;
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FanControlResult.Succeeded(40, "Reset"));
        }

        public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));
        }

        public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FanControlResult>>(Array.Empty<FanControlResult>());
        }

        public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
