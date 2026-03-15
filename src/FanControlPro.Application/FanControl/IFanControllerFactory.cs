using FanControlPro.Domain.FanControl;

namespace FanControlPro.Application.FanControl;

public interface IFanControllerFactory
{
    IFanControllerV2 Create(FanChannel channel);
}
