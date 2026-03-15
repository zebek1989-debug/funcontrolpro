using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Infrastructure.Onboarding;
using FanControlPro.Infrastructure.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Recovery;

public sealed class ConfigurationHealthValidator : IConfigurationHealthValidator
{
    private readonly IOptions<ProfileStorageOptions> _profileOptions;
    private readonly IOptions<ControlOnboardingOptions> _onboardingOptions;
    private readonly IOptions<HardwareDetectionOptions> _hardwareOptions;
    private readonly ILogger<ConfigurationHealthValidator> _logger;

    public ConfigurationHealthValidator(
        IOptions<ProfileStorageOptions> profileOptions,
        IOptions<ControlOnboardingOptions> onboardingOptions,
        IOptions<HardwareDetectionOptions> hardwareOptions,
        ILogger<ConfigurationHealthValidator> logger)
    {
        _profileOptions = profileOptions;
        _onboardingOptions = onboardingOptions;
        _hardwareOptions = hardwareOptions;
        _logger = logger;
    }

    public async Task<ConfigurationValidationResult> ValidateCurrentConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var consentPath = ResolvePath(_onboardingOptions.Value.ConsentFilePath, Path.Combine("data", "control-consent.json"));
        await ValidateJsonFileAsync(consentPath, errors, cancellationToken).ConfigureAwait(false);

        var activeProfilePath = ResolvePath(_profileOptions.Value.ActiveProfilePath, Path.Combine("data", "active-profile.json"));
        await ValidateJsonFileAsync(activeProfilePath, errors, cancellationToken).ConfigureAwait(false);

        var profilesDirectory = ResolvePath(_profileOptions.Value.ProfilesDirectoryPath, Path.Combine("data", "profiles"));
        if (Directory.Exists(profilesDirectory))
        {
            foreach (var path in Directory.GetFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                await ValidateJsonFileAsync(path, errors, cancellationToken).ConfigureAwait(false);
            }
        }

        var hardwarePath = ResolvePath(_hardwareOptions.Value.CachePath, Path.Combine("data", "hardware.json"));
        await ValidateJsonFileAsync(hardwarePath, errors, cancellationToken).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            _logger.LogWarning("Configuration health validation found {ErrorCount} issue(s).", errors.Count);
        }

        return new ConfigurationValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors);
    }

    private static async Task ValidateJsonFileAsync(string path, ICollection<string> errors, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("schemaVersion", out var schemaElement))
            {
                if (schemaElement.ValueKind != JsonValueKind.Number ||
                    !schemaElement.TryGetInt32(out var schemaVersion) ||
                    schemaVersion <= 0)
                {
                    errors.Add($"{path}: invalid schemaVersion.");
                }
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"{path}: invalid JSON ({ex.Message}).");
        }
        catch (Exception ex)
        {
            errors.Add($"{path}: cannot be read ({ex.Message}).");
        }
    }

    private static string ResolvePath(string configuredPath, string fallbackRelativePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = fallbackRelativePath;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
