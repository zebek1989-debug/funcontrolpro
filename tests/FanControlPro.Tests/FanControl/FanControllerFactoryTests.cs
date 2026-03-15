using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.FanControl;

public sealed class FanControllerFactoryTests
{
    [Fact]
    public async Task Create_ShouldResolveAsusControllerPath_WhenVendorMatches()
    {
        var factory = CreateFactory(consentAccepted: true);
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var controller = factory.Create(channel);
        var canControl = await controller.CanControlAsync(channel);

        Assert.True(canControl);
    }

    [Fact]
    public async Task Create_ShouldFallbackToMonitoringOnly_WhenVendorUnknown()
    {
        var factory = CreateFactory(consentAccepted: true);
        var channel = new FanChannel("sys_fan", "System Fan", "UnknownVendor", SupportLevel.FullControl, IsCpuChannel: false);

        var controller = factory.Create(channel);
        var canControl = await controller.CanControlAsync(channel);

        Assert.False(canControl);
    }

    private static FanControllerFactory CreateFactory(bool consentAccepted)
    {
        var onboarding = new FakeOnboardingService(consentAccepted);
        var validator = new AlwaysPassValidator();

        return new FanControllerFactory(
            new AsusEcControllerV2(),
            new GigabyteEcControllerV2(),
            new MsiEcControllerV2(),
            new MonitoringOnlyController(),
            onboarding,
            validator,
            NullLoggerFactory.Instance);
    }

    private sealed class FakeOnboardingService : IControlOnboardingService
    {
        private readonly bool _accepted;

        public FakeOnboardingService(bool accepted)
        {
            _accepted = accepted;
        }

        public Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ControlOnboardingState(_accepted, DateTimeOffset.UtcNow, "tester"));

        public Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_accepted);

        public Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new ControlOnboardingState(true, DateTimeOffset.UtcNow, acceptedBy));

        public Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new ControlOnboardingState(false, null, revokedBy));
    }

    private sealed class AlwaysPassValidator : IWriteCapabilityValidator
    {
        public Task<WriteValidationResult> ValidateAsync(IFanControllerV2 controller, FanChannel channel)
            => Task.FromResult(new WriteValidationResult(true, "ok", RollbackApplied: true, PreviousPercent: 40, TestPercent: 45));
    }
}
