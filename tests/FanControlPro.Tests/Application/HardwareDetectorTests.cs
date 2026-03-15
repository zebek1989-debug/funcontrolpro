using FanControlPro.Application.HardwareDetection;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Application;

public sealed class HardwareDetectorTests
{
    [Fact]
    public async Task DetectHardwareAsync_ShouldProbeAndPersist_WhenProbeReturnsData()
    {
        var probeItems = new[]
        {
            new HardwareProbeItem(
                Id: "cpu",
                Name: "CPU",
                Type: HardwareComponentType.Cpu,
                Vendor: "Intel",
                Model: "Core",
                Sensors: new[]
                {
                    new SensorSnapshot("cpu-temp", "CPU Package", SensorKind.Temperature, 55.4, "C", false)
                },
                HasWritePath: false,
                IsWritePathValidated: false)
        };

        var probe = new FakeProbe(probeItems);
        var cache = new InMemoryCacheStore();
        var detector = new HardwareDetector(
            probe,
            cache,
            Options.Create(new HardwareDetectionOptions()),
            NullLogger<HardwareDetector>.Instance);

        var result = await detector.DetectHardwareAsync();

        Assert.Single(result.Components);
        Assert.False(result.LoadedFromCache);
        Assert.Equal(1, cache.SaveCalls);
    }

    [Fact]
    public async Task DetectHardwareAsync_ShouldLoadCache_WhenProbeReturnsNoData()
    {
        var probe = new FakeProbe(Array.Empty<HardwareProbeItem>());
        var cached = new DetectionResult(
            DateTimeOffset.UtcNow.AddMinutes(-3),
            new[]
            {
                new DetectedHardware(
                    Id: "gpu",
                    Name: "GPU",
                    Type: HardwareComponentType.Gpu,
                    SupportLevel: SupportLevel.MonitoringOnly,
                    SupportReason: "Read-only telemetry is available.",
                    Vendor: "NVIDIA",
                    Model: "RTX",
                    Sensors: new[]
                    {
                        new SensorSnapshot("gpu-temp", "GPU Core", SensorKind.Temperature, 47.2, "C", false)
                    })
            },
            LoadedFromCache: false);

        var cache = new InMemoryCacheStore { Cached = cached };
        var detector = new HardwareDetector(
            probe,
            cache,
            Options.Create(new HardwareDetectionOptions
            {
                LoadFromCacheWhenProbeUnavailable = true
            }),
            NullLogger<HardwareDetector>.Instance);

        var result = await detector.DetectHardwareAsync();

        Assert.True(result.LoadedFromCache);
        Assert.Single(result.Components);
        Assert.Equal(0, cache.SaveCalls);
    }

    [Fact]
    public async Task ClassifyHardwareAsync_ShouldReturnSupportLevelFromClassifier()
    {
        var probe = new FakeProbe(Array.Empty<HardwareProbeItem>());
        var cache = new InMemoryCacheStore();
        var detector = new HardwareDetector(
            probe,
            cache,
            Options.Create(new HardwareDetectionOptions()),
            NullLogger<HardwareDetector>.Instance);

        var hardware = new HardwareProbeItem(
            Id: "mb",
            Name: "Motherboard",
            Type: HardwareComponentType.Motherboard,
            Vendor: "ASUS",
            Model: "Z490-P",
            Sensors: new[]
            {
                new SensorSnapshot("temp", "Mainboard", SensorKind.Temperature, 40, "C", false)
            },
            HasWritePath: true,
            IsWritePathValidated: false);

        var level = await detector.ClassifyHardwareAsync(hardware);

        Assert.Equal(SupportLevel.MonitoringOnly, level);
    }

    private sealed class FakeProbe : IHardwareProbe
    {
        private readonly IReadOnlyList<HardwareProbeItem> _items;

        public FakeProbe(IReadOnlyList<HardwareProbeItem> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<HardwareProbeItem>> ProbeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items);
        }
    }

    private sealed class InMemoryCacheStore : IHardwareCacheStore
    {
        public DetectionResult? Cached { get; set; }

        public int SaveCalls { get; private set; }

        public Task SaveAsync(DetectionResult result, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            Cached = result;
            return Task.CompletedTask;
        }

        public Task<DetectionResult?> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Cached);
        }
    }
}
