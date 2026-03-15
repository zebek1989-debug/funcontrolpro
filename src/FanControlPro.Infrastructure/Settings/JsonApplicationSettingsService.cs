using System.Text.Json;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Settings;

public sealed class JsonApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private readonly IOptions<ApplicationSettingsStorageOptions> _storageOptions;
    private readonly IAutostartService _autostartService;
    private readonly ILogger<JsonApplicationSettingsService> _logger;

    private ApplicationSettings _current = ApplicationSettings.Default;
    private bool _loaded;

    public JsonApplicationSettingsService(
        IOptions<ApplicationSettingsStorageOptions> storageOptions,
        IAutostartService autostartService,
        ILogger<JsonApplicationSettingsService> logger)
    {
        _storageOptions = storageOptions;
        _autostartService = autostartService;
        _logger = logger;
    }

    public event EventHandler<ApplicationSettings>? SettingsChanged;

    public ApplicationSettings Current => _current;

    public async Task<ApplicationSettings> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _current;
    }

    public async Task<ApplicationSettingsValidationResult> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var validationErrors = Validate(settings);
        if (validationErrors.Count > 0)
        {
            return ApplicationSettingsValidationResult.Failed(validationErrors.ToArray());
        }

        var normalized = Normalize(settings);
        await PersistAsync(normalized, cancellationToken).ConfigureAwait(false);

        try
        {
            await _autostartService
                .ConfigureAsync(
                    normalized.EnableAutostart,
                    normalized.StartMinimizedToTray,
                    TimeSpan.FromSeconds(normalized.StartupDelaySeconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply autostart integration after settings save.");
            return ApplicationSettingsValidationResult.Failed("Autostart configuration failed.");
        }

        _current = normalized;
        SettingsChanged?.Invoke(this, normalized);

        return ApplicationSettingsValidationResult.Ok("Settings updated.");
    }

    public async Task<ApplicationSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var result = await SaveAsync(ApplicationSettings.Default, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(" | ", result.Errors));
        }

        return _current;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            var filePath = ResolvePath(_storageOptions.Value.SettingsFilePath);
            if (File.Exists(filePath))
            {
                try
                {
                    await using var stream = File.OpenRead(filePath);
                    var loaded = await JsonSerializer
                        .DeserializeAsync<ApplicationSettings>(stream, SerializerOptions, cancellationToken)
                        .ConfigureAwait(false);

                    _current = loaded is null ? ApplicationSettings.Default : Normalize(loaded);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse settings file. Falling back to defaults.");
                    _current = ApplicationSettings.Default;
                }
            }
            else
            {
                _current = ApplicationSettings.Default;
                await PersistAsync(_current, cancellationToken).ConfigureAwait(false);
            }

            await _autostartService
                .ConfigureAsync(
                    _current.EnableAutostart,
                    _current.StartMinimizedToTray,
                    TimeSpan.FromSeconds(_current.StartupDelaySeconds),
                    cancellationToken)
                .ConfigureAwait(false);

            _loaded = true;
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private async Task PersistAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        var filePath = ResolvePath(_storageOptions.Value.SettingsFilePath);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static ApplicationSettings Normalize(ApplicationSettings settings)
    {
        var defaultProfile = string.IsNullOrWhiteSpace(settings.DefaultProfileName)
            ? "Balanced"
            : settings.DefaultProfileName.Trim();

        return settings with
        {
            PollingIntervalSeconds = Math.Clamp(settings.PollingIntervalSeconds, 1, 5),
            CpuAlertThresholdCelsius = Math.Clamp(settings.CpuAlertThresholdCelsius, 30, 120),
            GpuAlertThresholdCelsius = Math.Clamp(settings.GpuAlertThresholdCelsius, 30, 120),
            StartupDelaySeconds = Math.Clamp(settings.StartupDelaySeconds, 0, 600),
            DefaultProfileName = defaultProfile
        };
    }

    private static List<string> Validate(ApplicationSettings settings)
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(settings.Theme))
        {
            errors.Add("Theme value is invalid.");
        }

        if (settings.PollingIntervalSeconds is < 1 or > 5)
        {
            errors.Add("Polling interval must be between 1 and 5 seconds.");
        }

        if (settings.CpuAlertThresholdCelsius is < 30 or > 120)
        {
            errors.Add("CPU alert threshold must be between 30C and 120C.");
        }

        if (settings.GpuAlertThresholdCelsius is < 30 or > 120)
        {
            errors.Add("GPU alert threshold must be between 30C and 120C.");
        }

        if (settings.StartupDelaySeconds is < 0 or > 600)
        {
            errors.Add("Startup delay must be between 0 and 600 seconds.");
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultProfileName))
        {
            errors.Add("Default profile name cannot be empty.");
        }

        return errors;
    }

    private static string ResolvePath(string configuredPath)
    {
        var fallbackPath = Path.Combine("data", "settings.json5");
        var path = string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath.Trim();

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
