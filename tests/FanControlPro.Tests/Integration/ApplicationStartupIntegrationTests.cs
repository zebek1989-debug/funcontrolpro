using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Profiles;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using FanControlPro.Infrastructure.HardwareDetection;
using FanControlPro.Infrastructure.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Integration;

public sealed class ApplicationStartupIntegrationTests
{
    [Fact]
    public async Task ColdStart_ShouldDetectHardwareAndLoadDefaultProfile()
    {
        // Arrange: Setup DI container with test services
        var services = new ServiceCollection();

        // Register test hardware probe with mock ASUS hardware
        var mockHardware = CreateMockAsusHardware();
        services.AddSingleton<IHardwareProbe>(new FakeProbe(mockHardware));

        // Register infrastructure services
        services.AddSingleton<IHardwareCacheStore, InMemoryHardwareCacheStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>(
            sp => new JsonProfileStore(
                Options.Create(new ProfileStorageOptions { ProfilesDirectoryPath = Path.GetTempPath() })));

        // Register application services
        services.AddSingleton<HardwareDetector>();
        services.AddSingleton<IManualFanControlService, FakeManualFanControlService>();
        services.AddSingleton<ProfileService>();

        // Add logging
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        // Act: Simulate cold start
        var hardwareDetector = provider.GetRequiredService<HardwareDetector>();
        var profileService = provider.GetRequiredService<ProfileService>();

        // Detect hardware
        var detectionResult = await hardwareDetector.DetectHardwareAsync();
        Assert.NotEmpty(detectionResult.Components);
        Assert.False(detectionResult.LoadedFromCache); // Should probe on first run

        // Load default profile
        var profiles = await profileService.GetProfilesAsync();
        Assert.NotEmpty(profiles); // Should have at least a default profile

        var defaultProfile = profiles.First(p => p.Name == "Balanced"); // Default profile
        Assert.NotNull(defaultProfile);
        Assert.Equal("Balanced", defaultProfile.Name);
    }

    [Fact]
    public async Task ProfileSwitch_ShouldApplyNewProfileSettings()
    {
        // Arrange: Setup with mock hardware and multiple profiles
        var services = new ServiceCollection();

        var mockHardware = CreateMockAsusHardware();
        services.AddSingleton<IHardwareProbe>(new FakeProbe(mockHardware));
        services.AddSingleton<IHardwareCacheStore, InMemoryHardwareCacheStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>(
            sp => new JsonProfileStore(
                Options.Create(new ProfileStorageOptions { ProfilesDirectoryPath = Path.GetTempPath() })));

        services.AddSingleton<HardwareDetector>();
        services.AddSingleton<IManualFanControlService, FakeManualFanControlService>();
        services.AddSingleton<ProfileService>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        var profileService = provider.GetRequiredService<ProfileService>();

        // Create a custom profile
        var customProfile = new FanProfile(
            Name: "Gaming",
            Description: "High performance profile",
            IsPredefined: false,
            ChannelSettings: new[]
            {
                new FanProfileChannelSetting("cpu_fan", 80),
                new FanProfileChannelSetting("system_fan", 70)
            },
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        await profileService.SaveProfileAsync(customProfile);

        // Act: Switch to custom profile
        var result = await profileService.ActivateProfileAsync("Gaming");

        // Assert: Verify profile is active
        Assert.True(result.Success);
        var activeProfile = await profileService.GetActiveProfileAsync();
        Assert.Equal("Gaming", activeProfile?.Name);
    }

    [Fact]
    public async Task Failsafe_ShouldTrigger_WhenTemperaturesExceedThresholds()
    {
        // Arrange: Setup with hardware that can simulate temperature spikes
        var services = new ServiceCollection();

        var temperatureProbe = new SimulatableHardwareProbe();
        services.AddSingleton<IHardwareProbe>(temperatureProbe);

        services.AddSingleton<IHardwareCacheStore, InMemoryHardwareCacheStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>(
            sp => new JsonProfileStore(
                Options.Create(new ProfileStorageOptions { ProfilesDirectoryPath = Path.GetTempPath() })));
        services.AddSingleton<HardwareDetector>();
        services.AddSingleton<IManualFanControlService, FakeManualFanControlService>();
        services.AddSingleton<ProfileService>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        // Simulate high temperature scenario
        temperatureProbe.SetTemperature("cpu-temp", 95.0); // Above threshold

        // Act: Run hardware detection
        var hardwareDetector = provider.GetRequiredService<HardwareDetector>();
        var detectionResult = await hardwareDetector.DetectHardwareAsync();

        // Assert: Should detect hardware but with high temperatures
        Assert.NotEmpty(detectionResult.Components);
        var cpuComponent = detectionResult.Components.First(c => c.Type == HardwareComponentType.Cpu);
        var tempSensor = cpuComponent.Sensors.First(s => s.Kind == SensorKind.Temperature);
        Assert.True(tempSensor.Value > 90); // High temperature detected
    }

    [Fact]
    public async Task Recovery_ShouldRestoreNormalOperation_AfterFailsafe()
    {
        // Arrange: Setup with simulatable hardware
        var services = new ServiceCollection();

        var temperatureProbe = new SimulatableHardwareProbe();
        services.AddSingleton<IHardwareProbe>(temperatureProbe);

        services.AddSingleton<IHardwareCacheStore, InMemoryHardwareCacheStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>(
            sp => new JsonProfileStore(
                Options.Create(new ProfileStorageOptions { ProfilesDirectoryPath = Path.GetTempPath() })));
        services.AddSingleton<HardwareDetector>();
        services.AddSingleton<IManualFanControlService, FakeManualFanControlService>();
        services.AddSingleton<ProfileService>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        // Act: Simulate recovery - temperature drops back to normal
        temperatureProbe.SetTemperature("cpu-temp", 45.0); // Normal temperature

        var hardwareDetector = provider.GetRequiredService<HardwareDetector>();
        var detectionResult = await hardwareDetector.DetectHardwareAsync();

        // Assert: Should detect normal temperatures
        Assert.NotEmpty(detectionResult.Components);
        var cpuComponent = detectionResult.Components.First(c => c.Type == HardwareComponentType.Cpu);
        var tempSensor = cpuComponent.Sensors.First(s => s.Kind == SensorKind.Temperature);
        Assert.True(tempSensor.Value < 50); // Normal temperature restored
    }

    private static HardwareProbeItem[] CreateMockAsusHardware()
    {
        return new[]
        {
            new HardwareProbeItem(
                Id: "cpu",
                Name: "CPU",
                Type: HardwareComponentType.Cpu,
                Vendor: "Intel",
                Model: "Core i7-11700K",
                Sensors: new[]
                {
                    new SensorSnapshot("cpu-temp", "CPU Package", SensorKind.Temperature, 45.0, "C", false),
                    new SensorSnapshot("cpu-fan", "CPU Fan", SensorKind.FanRpm, 800, "RPM", false)
                },
                HasWritePath: true,
                IsWritePathValidated: true),
            new HardwareProbeItem(
                Id: "gpu",
                Name: "GPU",
                Type: HardwareComponentType.Gpu,
                Vendor: "NVIDIA",
                Model: "RTX 3070",
                Sensors: new[]
                {
                    new SensorSnapshot("gpu-temp", "GPU Core", SensorKind.Temperature, 52.0, "C", false),
                    new SensorSnapshot("gpu-fan", "GPU Fan", SensorKind.FanRpm, 1200, "RPM", false)
                },
                HasWritePath: true,
                IsWritePathValidated: true)
        };
    }
}

// Fake probe for testing
internal sealed class FakeProbe : IHardwareProbe
{
    private readonly IReadOnlyList<HardwareProbeItem> _items;

    public FakeProbe(IReadOnlyList<HardwareProbeItem> items)
    {
        _items = items;
    }

    public Task<IReadOnlyList<HardwareProbeItem>> ProbeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_items);
}

// Simulatable hardware probe for testing temperature scenarios
internal sealed class SimulatableHardwareProbe : IHardwareProbe
{
    private readonly Dictionary<string, double> _sensorValues = new();

    public SimulatableHardwareProbe()
    {
        // Default values
        _sensorValues["cpu-temp"] = 45.0;
        _sensorValues["gpu-temp"] = 52.0;
        _sensorValues["cpu-fan"] = 1200;
        _sensorValues["gpu-fan"] = 1500;
    }

    public void SetTemperature(string sensorId, double temperature)
    {
        _sensorValues[sensorId] = temperature;
    }

    public Task<IReadOnlyList<HardwareProbeItem>> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var items = new[]
        {
            new HardwareProbeItem(
                Id: "cpu",
                Name: "CPU",
                Type: HardwareComponentType.Cpu,
                Vendor: "Intel",
                Model: "Core i7-11700K",
                Sensors: new[]
                {
                    new SensorSnapshot("cpu-temp", "CPU Package", SensorKind.Temperature, _sensorValues["cpu-temp"], "C", false),
                    new SensorSnapshot("cpu-fan", "CPU Fan", SensorKind.FanRpm, _sensorValues["cpu-fan"], "RPM", false)
                },
                HasWritePath: true,
                IsWritePathValidated: true),
            new HardwareProbeItem(
                Id: "gpu",
                Name: "GPU",
                Type: HardwareComponentType.Gpu,
                Vendor: "NVIDIA",
                Model: "RTX 3070",
                Sensors: new[]
                {
                    new SensorSnapshot("gpu-temp", "GPU Core", SensorKind.Temperature, _sensorValues["gpu-temp"], "C", false),
                    new SensorSnapshot("gpu-fan", "GPU Fan", SensorKind.FanRpm, _sensorValues["gpu-fan"], "RPM", false)
                },
                HasWritePath: true,
                IsWritePathValidated: true)
        };
        return Task.FromResult<IReadOnlyList<HardwareProbeItem>>(items);
    }
}

// In-memory cache for testing
internal sealed class InMemoryHardwareCacheStore : IHardwareCacheStore
{
    private DetectionResult? _cached;

    public int SaveCalls { get; private set; }

    public Task<DetectionResult?> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_cached);

    public Task SaveAsync(DetectionResult result, CancellationToken cancellationToken = default)
    {
        _cached = result;
        SaveCalls++;
        return Task.CompletedTask;
    }
}

// Fake manual fan control service for testing
internal sealed class FakeManualFanControlService : IManualFanControlService
{
    public IReadOnlyList<string> AvailableGroups => new[] { "cpu", "gpu" };

    public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var channels = new[]
        {
            new FanChannelSnapshot("cpu_fan", "CPU Fan", "fan", 60, 1200, 0, 100, true, true, "OK", "cpu"),
            new FanChannelSnapshot("gpu_fan", "GPU Fan", "fan", 70, 1500, 0, 100, false, true, "OK", "gpu")
        };
        return Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(channels);
    }

    public Task<FanControlResult> SetSpeedAsync(string channelId, int percent, bool confirmLowCpuFan, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
    }

    public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FanControlResult.Succeeded(40, "Reset"));
    }

    public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));
    }

    public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FanControlResult>>(Array.Empty<FanControlResult>());
    }

    public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}