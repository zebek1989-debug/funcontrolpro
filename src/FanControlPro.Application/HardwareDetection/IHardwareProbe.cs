using FanControlPro.Domain.Hardware.Models;

namespace FanControlPro.Application.HardwareDetection;

public interface IHardwareProbe
{
    Task<IReadOnlyList<HardwareProbeItem>> ProbeAsync(CancellationToken cancellationToken = default);
}
