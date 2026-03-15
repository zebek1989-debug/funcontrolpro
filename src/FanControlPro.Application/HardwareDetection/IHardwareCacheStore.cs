using FanControlPro.Domain.Hardware.Models;

namespace FanControlPro.Application.HardwareDetection;

public interface IHardwareCacheStore
{
    Task SaveAsync(DetectionResult result, CancellationToken cancellationToken = default);

    Task<DetectionResult?> LoadAsync(CancellationToken cancellationToken = default);
}
