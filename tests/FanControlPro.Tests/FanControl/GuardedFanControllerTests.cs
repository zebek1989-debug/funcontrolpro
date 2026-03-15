using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class GuardedFanControllerTests
{
    [Fact]
    public async Task SetSpeedAsync_ShouldRequireConsent()
    {
        var onboarding = new FakeOnboardingService(hasAcceptedRisk: false);
        var validator = new FakeValidator(isValid: true);
        var inner = new AlwaysSuccessController();
        var guarded = new GuardedFanController(inner, onboarding, validator);

        var channel = new FanChannel("sys_fan", "System Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: false);
        var result = await guarded.SetSpeedAsync(channel, 55);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.ConsentRequired, result.FailureReason);
    }

    [Fact]
    public async Task SetSpeedAsync_ShouldRunValidationOnlyOncePerChannel()
    {
        var onboarding = new FakeOnboardingService(hasAcceptedRisk: true);
        var validator = new FakeValidator(isValid: true);
        var inner = new AlwaysSuccessController();
        var guarded = new GuardedFanController(inner, onboarding, validator);

        var channel = new FanChannel("sys_fan", "System Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: false);

        var first = await guarded.SetSpeedAsync(channel, 45);
        var second = await guarded.SetSpeedAsync(channel, 50);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, validator.CallCount);
    }

    private sealed class FakeOnboardingService : IControlOnboardingService
    {
        private readonly bool _hasAcceptedRisk;

        public FakeOnboardingService(bool hasAcceptedRisk)
        {
            _hasAcceptedRisk = hasAcceptedRisk;
        }

        public Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ControlOnboardingState(_hasAcceptedRisk, DateTimeOffset.UtcNow, "tester"));
        }

        public Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_hasAcceptedRisk);
        }

        public Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ControlOnboardingState(true, DateTimeOffset.UtcNow, acceptedBy));
        }

        public Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ControlOnboardingState(false, null, revokedBy));
        }
    }

    private sealed class FakeValidator : IWriteCapabilityValidator
    {
        private readonly bool _isValid;

        public FakeValidator(bool isValid)
        {
            _isValid = isValid;
        }

        public int CallCount { get; private set; }

        public Task<WriteValidationResult> ValidateAsync(IFanControllerV2 controller, FanChannel channel)
        {
            CallCount++;
            return Task.FromResult(new WriteValidationResult(_isValid, "ok", RollbackApplied: true, PreviousPercent: 40, TestPercent: 45));
        }
    }

    private sealed class AlwaysSuccessController : IFanControllerV2
    {
        public Task<bool> CanControlAsync(FanChannel channel) => Task.FromResult(true);

        public Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
            => Task.FromResult(FanControlResult.Succeeded(percent, "applied"));

        public Task<int> GetCurrentSpeedAsync(FanChannel channel) => Task.FromResult(40);

        public Task<HealthStatus> GetHealthStatusAsync() => Task.FromResult(HealthStatus.Healthy());
    }
}
