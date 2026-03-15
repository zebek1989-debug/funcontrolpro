using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using FanControlPro.Domain.Hardware.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Application.HardwareDetection;

public sealed class HardwareDetector : IHardwareDetector
{
    private readonly IHardwareProbe _probe;
    private readonly IHardwareCacheStore _cacheStore;
    private readonly HardwareDetectionOptions _options;
    private readonly ILogger<HardwareDetector> _logger;

    public HardwareDetector(
        IHardwareProbe probe,
        IHardwareCacheStore cacheStore,
        IOptions<HardwareDetectionOptions> options,
        ILogger<HardwareDetector> logger)
    {
        _probe = probe;
        _cacheStore = cacheStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DetectionResult> DetectHardwareAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HardwareProbeItem> probeItems;

        try
        {
            probeItems = await _probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware probe failed. Falling back to cache when possible.");
            return await TryLoadCacheOrEmptyAsync(cancellationToken).ConfigureAwait(false);
        }

        if (probeItems.Count == 0)
        {
            _logger.LogWarning("Hardware probe returned no components.");
            return await TryLoadCacheOrEmptyAsync(cancellationToken).ConfigureAwait(false);
        }

        var components = probeItems.Select(MapProbeToDetected).ToArray();
        var result = new DetectionResult(DateTimeOffset.UtcNow, components, LoadedFromCache: false);

        try
        {
            await _cacheStore.SaveAsync(result, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist hardware detection cache.");
        }

        return result;
    }

    public Task<SupportLevel> ClassifyHardwareAsync(
        HardwareProbeItem hardware,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var classification = SupportLevelClassifier.Classify(hardware);
        return Task.FromResult(classification.Level);
    }

    private async Task<DetectionResult> TryLoadCacheOrEmptyAsync(CancellationToken cancellationToken)
    {
        if (!_options.LoadFromCacheWhenProbeUnavailable)
        {
            return DetectionResult.Empty;
        }

        try
        {
            var cached = await _cacheStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                _logger.LogInformation("Loaded hardware information from cache.");
                return new DetectionResult(cached.DetectedAtUtc, cached.Components, LoadedFromCache: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load hardware cache.");
        }

        return DetectionResult.Empty;
    }

    private static DetectedHardware MapProbeToDetected(HardwareProbeItem probeItem)
    {
        var classification = SupportLevelClassifier.Classify(probeItem);

        return new DetectedHardware(
            Id: probeItem.Id,
            Name: probeItem.Name,
            Type: probeItem.Type,
            SupportLevel: classification.Level,
            SupportReason: classification.Reason,
            Vendor: probeItem.Vendor,
            Model: probeItem.Model,
            Sensors: probeItem.Sensors);
    }
}
