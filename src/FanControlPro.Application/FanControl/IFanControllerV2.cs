using FanControlPro.Domain.FanControl;

namespace FanControlPro.Application.FanControl;

public interface IFanControllerV2
{
    Task<bool> CanControlAsync(FanChannel channel);

    Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent);

    Task<int> GetCurrentSpeedAsync(FanChannel channel);

    Task<HealthStatus> GetHealthStatusAsync();
}
