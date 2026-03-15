using System.Text.Json;
using FanControlPro.Application.Configuration;
using FanControlPro.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class JsonApplicationSettingsServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetCurrentAsync_WhenSettingsFileMissing_CreatesDefaultsAndAppliesAutostart()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "data", "settings.json5");
            var autostart = new FakeAutostartService();
            var service = CreateService(settingsPath, autostart);

            var current = await service.GetCurrentAsync();

            Assert.Equal(ApplicationSettings.Default, current);
            Assert.True(File.Exists(settingsPath));

            var autostartCall = Assert.Single(autostart.ConfigureCalls);
            Assert.False(autostartCall.Enabled);
            Assert.True(autostartCall.StartMinimizedToTray);
            Assert.Equal(TimeSpan.FromSeconds(30), autostartCall.StartupDelay);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenSettingsAreValid_PersistsAndRaisesChangedEvent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json5");
            var autostart = new FakeAutostartService();
            var service = CreateService(settingsPath, autostart);

            await service.GetCurrentAsync();

            var savedSettings = ApplicationSettings.Default with
            {
                PollingIntervalSeconds = 4,
                CpuAlertThresholdCelsius = 76,
                GpuAlertThresholdCelsius = 79,
                Theme = ApplicationTheme.Dark,
                EnableAutostart = true,
                StartMinimizedToTray = false,
                MinimizeToTrayOnClose = false,
                StartupDelaySeconds = 120,
                DefaultProfileName = "Quiet"
            };

            ApplicationSettings? changedEventPayload = null;
            service.SettingsChanged += (_, payload) => changedEventPayload = payload;

            var result = await service.SaveAsync(savedSettings);

            Assert.True(result.Success);
            Assert.Equal(savedSettings, service.Current);
            Assert.Equal(savedSettings, changedEventPayload);
            Assert.Equal(2, autostart.ConfigureCalls.Count);
            Assert.Equal(TimeSpan.FromSeconds(120), autostart.ConfigureCalls[^1].StartupDelay);

            var persisted = await ReadPersistedSettingsAsync(settingsPath);
            Assert.Equal(savedSettings, persisted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenValueIsOutOfRange_ReturnsFailureAndKeepsExistingSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json5");
            var autostart = new FakeAutostartService();
            var service = CreateService(settingsPath, autostart);

            await service.GetCurrentAsync();

            var invalid = ApplicationSettings.Default with
            {
                PollingIntervalSeconds = 9
            };

            var result = await service.SaveAsync(invalid);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Contains("Polling interval", StringComparison.Ordinal));
            Assert.Equal(ApplicationSettings.Default, service.Current);
            Assert.Single(autostart.ConfigureCalls);

            var persisted = await ReadPersistedSettingsAsync(settingsPath);
            Assert.Equal(ApplicationSettings.Default, persisted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ResetToDefaultsAsync_WhenCustomSettingsWereSaved_RestoresDefaultConfiguration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json5");
            var autostart = new FakeAutostartService();
            var service = CreateService(settingsPath, autostart);

            await service.GetCurrentAsync();

            var customSave = await service.SaveAsync(ApplicationSettings.Default with
            {
                Theme = ApplicationTheme.Light,
                EnableAutostart = true,
                StartupDelaySeconds = 60,
                DefaultProfileName = "Turbo"
            });

            Assert.True(customSave.Success);

            var reset = await service.ResetToDefaultsAsync();

            Assert.Equal(ApplicationSettings.Default, reset);
            Assert.Equal(ApplicationSettings.Default, service.Current);
            Assert.Equal(3, autostart.ConfigureCalls.Count);
            Assert.Equal(TimeSpan.FromSeconds(30), autostart.ConfigureCalls[^1].StartupDelay);

            var persisted = await ReadPersistedSettingsAsync(settingsPath);
            Assert.Equal(ApplicationSettings.Default, persisted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonApplicationSettingsService CreateService(string settingsPath, FakeAutostartService autostart)
    {
        return new JsonApplicationSettingsService(
            Options.Create(new ApplicationSettingsStorageOptions
            {
                SettingsFilePath = settingsPath
            }),
            autostart,
            NullLogger<JsonApplicationSettingsService>.Instance);
    }

    private static async Task<ApplicationSettings> ReadPersistedSettingsAsync(string settingsPath)
    {
        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, SerializerOptions);
        Assert.NotNull(settings);
        return settings;
    }

    private sealed class FakeAutostartService : IAutostartService
    {
        public List<ConfigureCall> ConfigureCalls { get; } = new();

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ConfigureCalls.Count == 0)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(ConfigureCalls[^1].Enabled);
        }

        public Task ConfigureAsync(
            bool enabled,
            bool startMinimizedToTray,
            TimeSpan startupDelay,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConfigureCalls.Add(new ConfigureCall(enabled, startMinimizedToTray, startupDelay));
            return Task.CompletedTask;
        }
    }

    private sealed record ConfigureCall(
        bool Enabled,
        bool StartMinimizedToTray,
        TimeSpan StartupDelay);
}
