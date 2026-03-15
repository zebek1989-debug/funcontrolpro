using FanControlPro.Domain.FanControl;

namespace FanControlPro.Application.FanControl;

public interface IWriteCapabilityValidator
{
    Task<WriteValidationResult> ValidateAsync(IFanControllerV2 controller, FanChannel channel);
}
