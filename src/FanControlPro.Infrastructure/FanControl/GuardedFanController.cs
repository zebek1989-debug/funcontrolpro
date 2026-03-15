using System.Collections.Concurrent;
using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class GuardedFanController : IFanControllerV2
{
    private readonly IFanControllerV2 _inner;
    private readonly IControlOnboardingService _onboardingService;
    private readonly IWriteCapabilityValidator _validator;
    private readonly ConcurrentDictionary<string, bool> _validatedChannels = new(StringComparer.OrdinalIgnoreCase);

    public GuardedFanController(
        IFanControllerV2 inner,
        IControlOnboardingService onboardingService,
        IWriteCapabilityValidator validator)
    {
        _inner = inner;
        _onboardingService = onboardingService;
        _validator = validator;
    }

    public async Task<bool> CanControlAsync(FanChannel channel)
    {
        if (!await _onboardingService.HasAcceptedRiskAsync().ConfigureAwait(false))
        {
            return false;
        }

        return await _inner.CanControlAsync(channel).ConfigureAwait(false);
    }

    public async Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
    {
        if (!await _onboardingService.HasAcceptedRiskAsync().ConfigureAwait(false))
        {
            return FanControlResult.Failed(
                FanControlFailureReason.ConsentRequired,
                "Control mode requires explicit risk acknowledgement.");
        }

        if (!_validatedChannels.ContainsKey(channel.Id))
        {
            var validation = await _validator.ValidateAsync(_inner, channel).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return FanControlResult.Failed(
                    FanControlFailureReason.ValidationFailed,
                    validation.Message,
                    rollbackApplied: validation.RollbackApplied,
                    previousPercent: validation.PreviousPercent);
            }

            _validatedChannels[channel.Id] = true;
        }

        return await _inner.SetSpeedAsync(channel, percent).ConfigureAwait(false);
    }

    public Task<int> GetCurrentSpeedAsync(FanChannel channel)
    {
        return _inner.GetCurrentSpeedAsync(channel);
    }

    public async Task<HealthStatus> GetHealthStatusAsync()
    {
        var baseStatus = await _inner.GetHealthStatusAsync().ConfigureAwait(false);
        if (await _onboardingService.HasAcceptedRiskAsync().ConfigureAwait(false))
        {
            return baseStatus;
        }

        return new HealthStatus(
            State: ControllerHealthState.Degraded,
            Message: "Control locked until user accepts risk onboarding.",
            CheckedAtUtc: DateTimeOffset.UtcNow,
            Issues: baseStatus.Issues.Concat(new[] { "User consent missing" }).ToArray());
    }
}
