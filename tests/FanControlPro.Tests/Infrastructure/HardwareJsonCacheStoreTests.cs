using FanControlPro.Application.HardwareDetection;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using FanControlPro.Infrastructure.HardwareDetection;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class HardwareJsonCacheStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_ShouldRoundTripDetectionResult()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fancontrolpro-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cachePath = Path.Combine(tempDirectory, "hardware.json");
            var store = new HardwareJsonCacheStore(Options.Create(new HardwareDetectionOptions
            {
                CachePath = cachePath
            }));

            var original = new DetectionResult(
                DateTimeOffset.UtcNow,
                new[]
                {
                    new DetectedHardware(
                        Id: "mb",
                        Name: "Mainboard",
                        Type: HardwareComponentType.Motherboard,
                        SupportLevel: SupportLevel.MonitoringOnly,
                        SupportReason: "Read-only telemetry is available.",
                        Vendor: "ASUS",
                        Model: "Z490-P",
                        Sensors: new[]
                        {
                            new SensorSnapshot("mb-temp", "Mainboard", SensorKind.Temperature, 42.8, "C", false)
                        })
                },
                LoadedFromCache: false);

            await store.SaveAsync(original);
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Components);
            Assert.Equal("Mainboard", loaded.Components[0].Name);
            Assert.Equal(SupportLevel.MonitoringOnly, loaded.Components[0].SupportLevel);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
