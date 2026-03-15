using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Profiles;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Application.FanControl.Profiles;

public sealed class ProfileService : IProfileService
{
    private static readonly string[] PredefinedProfileNames =
    {
        "Silent",
        "Balanced",
        "Performance"
    };

    private readonly IProfileStore _store;
    private readonly IManualFanControlService _manualControlService;
    private readonly ILogger<ProfileService> _logger;

    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly object _sync = new();

    private bool _initialized;
    private readonly Dictionary<string, FanProfile> _profilesByName = new(StringComparer.OrdinalIgnoreCase);
    private string _activeProfileName = "Balanced";

    public ProfileService(
        IProfileStore store,
        IManualFanControlService manualControlService,
        ILogger<ProfileService> logger)
    {
        _store = store;
        _manualControlService = manualControlService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FanProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            return _profilesByName.Values
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public async Task<FanProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            return _profilesByName.TryGetValue(_activeProfileName, out var profile)
                ? profile
                : null;
        }
    }

    public async Task<ProfileActivationResult> ActivateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ProfileActivationResult(
                Success: false,
                ProfileName: string.Empty,
                AppliedChannelCount: 0,
                TotalChannelCount: 0,
                Message: "Profile name cannot be empty.",
                Errors: new[] { "Missing profile name." });
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        FanProfile profile;
        lock (_sync)
        {
            if (!_profilesByName.TryGetValue(name.Trim(), out profile!))
            {
                return new ProfileActivationResult(
                    Success: false,
                    ProfileName: name,
                    AppliedChannelCount: 0,
                    TotalChannelCount: 0,
                    Message: "Profile not found.",
                    Errors: new[] { $"Unknown profile '{name}'." });
            }
        }

        var errors = new List<string>();
        var applied = 0;

        foreach (var setting in profile.ChannelSettings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _manualControlService.SetSpeedAsync(
                setting.ChannelId,
                setting.TargetPercent,
                confirmLowCpuFan: true,
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                applied++;
            }
            else
            {
                errors.Add($"{setting.ChannelId}: {result.Message}");
            }
        }

        lock (_sync)
        {
            _activeProfileName = profile.Name;
        }

        await _store.SaveActiveProfileNameAsync(profile.Name, cancellationToken).ConfigureAwait(false);

        var success = errors.Count == 0;
        var message = success
            ? $"Profile '{profile.Name}' activated."
            : $"Profile '{profile.Name}' activated partially ({applied}/{profile.ChannelSettings.Count}).";

        _logger.LogInformation(
            "Profile activation completed for {ProfileName}: applied {Applied}/{Total}",
            profile.Name,
            applied,
            profile.ChannelSettings.Count);

        return new ProfileActivationResult(
            Success: success,
            ProfileName: profile.Name,
            AppliedChannelCount: applied,
            TotalChannelCount: profile.ChannelSettings.Count,
            Message: message,
            Errors: errors);
    }

    public async Task SaveProfileAsync(FanProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Profile name cannot be empty.");
        }

        if (profile.ChannelSettings.Count == 0)
        {
            throw new InvalidOperationException("Profile must contain at least one channel setting.");
        }

        if (PredefinedProfileNames.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Predefined profiles cannot be overwritten.");
        }

        var normalizedSettings = profile.ChannelSettings
            .Select(setting => new FanProfileChannelSetting(setting.ChannelId, Math.Clamp(setting.TargetPercent, 0, 100)))
            .ToArray();

        var normalizedProfile = profile with
        {
            IsPredefined = false,
            ChannelSettings = normalizedSettings,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        lock (_sync)
        {
            _profilesByName[normalizedProfile.Name] = normalizedProfile;
        }

        await _store.SaveCustomProfileAsync(normalizedProfile, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved custom profile {ProfileName}", normalizedProfile.Name);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var channels = await _manualControlService.GetChannelsAsync(cancellationToken).ConfigureAwait(false);

            var predefined = CreatePredefinedProfiles(channels);
            foreach (var profile in predefined)
            {
                _profilesByName[profile.Name] = profile;
            }

            var customProfiles = await _store.LoadCustomProfilesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var profile in customProfiles)
            {
                _profilesByName[profile.Name] = profile;
            }

            var activeName = await _store.LoadActiveProfileNameAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(activeName) && _profilesByName.ContainsKey(activeName))
            {
                _activeProfileName = activeName;
            }
            else
            {
                _activeProfileName = "Balanced";
            }

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static IReadOnlyList<FanProfile> CreatePredefinedProfiles(IReadOnlyList<FanChannelSnapshot> channels)
    {
        var now = DateTimeOffset.UtcNow;

        var silentSettings = channels
            .Select(channel => new FanProfileChannelSetting(channel.Id, ClampToChannel(channel, channel.IsCpuChannel ? 30 : 35)))
            .ToArray();

        var balancedSettings = channels
            .Select(channel => new FanProfileChannelSetting(channel.Id, ClampToChannel(channel, channel.IsCpuChannel ? 50 : 55)))
            .ToArray();

        var performanceSettings = channels
            .Select(channel => new FanProfileChannelSetting(channel.Id, ClampToChannel(channel, channel.IsCpuChannel ? 80 : 90)))
            .ToArray();

        var customSettings = channels
            .Select(channel => new FanProfileChannelSetting(channel.Id, ClampToChannel(channel, channel.CurrentPercent)))
            .ToArray();

        return new[]
        {
            new FanProfile("Silent", "Low-noise profile", true, silentSettings, now),
            new FanProfile("Balanced", "Balanced temperature/noise profile", true, balancedSettings, now),
            new FanProfile("Performance", "Maximum cooling profile", true, performanceSettings, now),
            new FanProfile("Custom", "User defined profile", false, customSettings, now)
        };
    }

    private static int ClampToChannel(FanChannelSnapshot channel, int value)
    {
        return Math.Clamp(value, channel.MinimumPercent, channel.MaximumPercent);
    }
}
