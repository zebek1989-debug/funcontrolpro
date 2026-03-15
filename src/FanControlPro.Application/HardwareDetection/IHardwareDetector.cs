using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;

namespace FanControlPro.Application.HardwareDetection;

public interface IHardwareDetector
{
    Task<DetectionResult> DetectHardwareAsync(CancellationToken cancellationToken = default);

    Task<SupportLevel> ClassifyHardwareAsync(
        HardwareProbeItem hardware,
        CancellationToken cancellationToken = default);
}
