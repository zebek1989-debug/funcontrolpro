namespace FanControlPro.Domain.Hardware.Models;

public sealed record DetectionResult(
    DateTimeOffset DetectedAtUtc,
    IReadOnlyList<DetectedHardware> Components,
    bool LoadedFromCache)
{
    public static DetectionResult Empty { get; } =
        new(DateTimeOffset.UtcNow, Array.Empty<DetectedHardware>(), LoadedFromCache: false);
}
