using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;

namespace FanControlPro.Application.FanControl;

public sealed class WriteCapabilityValidator : IWriteCapabilityValidator
{
    public async Task<WriteValidationResult> ValidateAsync(IFanControllerV2 controller, FanChannel channel)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(channel);

        if (!await controller.CanControlAsync(channel).ConfigureAwait(false))
        {
            return new WriteValidationResult(
                IsValid: false,
                Message: "Controller cannot write to this channel.",
                RollbackApplied: false);
        }

        var previousPercent = await controller.GetCurrentSpeedAsync(channel).ConfigureAwait(false);

        var testPercent = previousPercent >= 95
            ? Math.Max(channel.SafeMinimumPercent, previousPercent - 5)
            : Math.Max(channel.SafeMinimumPercent, previousPercent + 5);

        var setResult = await controller.SetSpeedAsync(channel, testPercent).ConfigureAwait(false);
        if (!setResult.Success)
        {
            return new WriteValidationResult(
                IsValid: false,
                Message: $"Write validation failed during test write: {setResult.Message}",
                RollbackApplied: false,
                PreviousPercent: previousPercent,
                TestPercent: testPercent);
        }

        var rollbackResult = await controller.SetSpeedAsync(channel, previousPercent).ConfigureAwait(false);
        if (!rollbackResult.Success)
        {
            return new WriteValidationResult(
                IsValid: false,
                Message: "Write validation failed because rollback did not succeed.",
                RollbackApplied: false,
                PreviousPercent: previousPercent,
                TestPercent: testPercent);
        }

        return new WriteValidationResult(
            IsValid: true,
            Message: "Write validation succeeded.",
            RollbackApplied: true,
            PreviousPercent: previousPercent,
            TestPercent: testPercent);
    }
}
