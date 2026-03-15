using FanControlPro.Application.FanControl;
using FanControlPro.Application.Onboarding;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Presentation.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.Presentation;

public sealed class OnboardingViewModelTests
{
    [Fact]
    public async Task NextStepCommand_OnRiskStepWithoutConsent_ShouldNotProceed()
    {
        var onboardingService = new FakeOnboardingService(
            new OnboardingState(OnboardingStep.RiskAcceptance, IsCompleted: false, CompletedAtUtc: null));
        var controlService = new FakeControlOnboardingService(
            new ControlOnboardingState(HasAcceptedRisk: false, AcceptedAtUtc: null, AcceptedBy: null));

        var sut = CreateSut(onboardingService, controlService);
        sut.CurrentStep = OnboardingStep.RiskAcceptance;
        sut.HasFullControlComponents = true;
        sut.IsRunningAsAdministrator = true;
        sut.HasVendorSoftwareConflict = false;
        sut.HasAcceptedRisk = false;

        await sut.NextStepCommand.ExecuteAsync(null);

        Assert.Empty(onboardingService.CompletedSteps);
        Assert.Equal(0, controlService.AcceptRiskCalls);
        Assert.Equal(OnboardingStep.RiskAcceptance, sut.CurrentStep);
        Assert.False(sut.CanProceed);
    }

    [Fact]
    public async Task NextStepCommand_OnRiskStepWithConsent_ShouldAcceptAndComplete()
    {
        var onboardingService = new FakeOnboardingService(
            new OnboardingState(OnboardingStep.RiskAcceptance, IsCompleted: false, CompletedAtUtc: null))
        {
            OnComplete = _ => new OnboardingState(OnboardingStep.Completed, IsCompleted: true, CompletedAtUtc: DateTimeOffset.UtcNow)
        };
        var controlService = new FakeControlOnboardingService(
            new ControlOnboardingState(HasAcceptedRisk: true, AcceptedAtUtc: DateTimeOffset.UtcNow, AcceptedBy: "tester"));

        var sut = CreateSut(onboardingService, controlService);
        sut.CurrentStep = OnboardingStep.RiskAcceptance;
        sut.HasFullControlComponents = true;
        sut.IsRunningAsAdministrator = true;
        sut.HasVendorSoftwareConflict = false;
        sut.HasAcceptedRisk = true;

        await sut.NextStepCommand.ExecuteAsync(null);

        Assert.Equal(1, controlService.AcceptRiskCalls);
        Assert.Single(onboardingService.CompletedSteps);
        Assert.Equal(OnboardingStep.RiskAcceptance, onboardingService.CompletedSteps[0]);
        Assert.Equal(OnboardingStep.Completed, sut.CurrentStep);
        Assert.True(sut.IsCompleted);
    }

    [Fact]
    public async Task RevokeRiskConsentCommand_ShouldClearConsent()
    {
        var onboardingService = new FakeOnboardingService(
            new OnboardingState(OnboardingStep.RiskAcceptance, IsCompleted: false, CompletedAtUtc: null));
        var controlService = new FakeControlOnboardingService(
            new ControlOnboardingState(HasAcceptedRisk: true, AcceptedAtUtc: DateTimeOffset.UtcNow, AcceptedBy: "tester"));

        var sut = CreateSut(onboardingService, controlService);
        sut.HasAcceptedRisk = true;

        await sut.RevokeRiskConsentCommand.ExecuteAsync(null);

        Assert.False(sut.HasAcceptedRisk);
        Assert.Equal(1, controlService.RevokeRiskCalls);
    }

    [Fact]
    public async Task PreviousStepCommand_ShouldMoveBackOneStep()
    {
        var onboardingService = new FakeOnboardingService(
            new OnboardingState(OnboardingStep.RiskAcceptance, IsCompleted: false, CompletedAtUtc: null));
        var controlService = new FakeControlOnboardingService(
            new ControlOnboardingState(HasAcceptedRisk: false, AcceptedAtUtc: null, AcceptedBy: null));

        var sut = CreateSut(onboardingService, controlService);
        sut.CurrentStep = OnboardingStep.RiskAcceptance;

        await sut.PreviousStepCommand.ExecuteAsync(null);

        Assert.Equal(OnboardingStep.HardwareClassification, sut.CurrentStep);
    }

    [Fact]
    public void CanProceed_OnRiskStep_ShouldDependOnConsentRequirement()
    {
        var onboardingService = new FakeOnboardingService(
            new OnboardingState(OnboardingStep.RiskAcceptance, IsCompleted: false, CompletedAtUtc: null));
        var controlService = new FakeControlOnboardingService(
            new ControlOnboardingState(HasAcceptedRisk: false, AcceptedAtUtc: null, AcceptedBy: null));

        var sut = CreateSut(onboardingService, controlService);
        sut.CurrentStep = OnboardingStep.RiskAcceptance;
        sut.HasFullControlComponents = true;
        sut.IsRunningAsAdministrator = true;
        sut.HasVendorSoftwareConflict = false;
        sut.HasAcceptedRisk = false;

        Assert.False(sut.CanProceed);

        sut.HasAcceptedRisk = true;

        Assert.True(sut.CanProceed);
    }

    private static OnboardingViewModel CreateSut(
        IOnboardingService onboardingService,
        IControlOnboardingService controlOnboardingService)
    {
        return new OnboardingViewModel(
            NullLogger<OnboardingViewModel>.Instance,
            onboardingService,
            controlOnboardingService);
    }

    private sealed class FakeOnboardingService : IOnboardingService
    {
        public FakeOnboardingService(OnboardingState initialState)
        {
            State = initialState;
        }

        public OnboardingState State { get; private set; }

        public List<OnboardingStep> CompletedSteps { get; } = new();

        public Func<OnboardingStep, OnboardingState>? OnComplete { get; set; }

        public Task<OnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(State);

        public Task CompleteStepAsync(OnboardingStep step, CancellationToken cancellationToken = default)
        {
            CompletedSteps.Add(step);
            if (OnComplete is not null)
            {
                State = OnComplete(step);
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(State.IsCompleted);

        public Task<HardwareClassificationResult> ClassifyHardwareAsync(CancellationToken cancellationToken = default)
        {
            var result = new HardwareClassificationResult(
                Components: new[]
                {
                    new HardwareComponentClassification(
                        ComponentName: "CPU Fan",
                        ComponentType: "Fan",
                        Level: SupportLevel.FullControl,
                        Reason: "Mock")
                },
                HasFullControlComponents: true,
                HasMonitoringOnlyComponents: false,
                HasUnsupportedComponents: false);

            return Task.FromResult(result);
        }
    }

    private sealed class FakeControlOnboardingService : IControlOnboardingService
    {
        public FakeControlOnboardingService(ControlOnboardingState state)
        {
            State = state;
        }

        public ControlOnboardingState State { get; private set; }

        public int AcceptRiskCalls { get; private set; }

        public int RevokeRiskCalls { get; private set; }

        public Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(State);

        public Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(State.HasAcceptedRisk);

        public Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default)
        {
            AcceptRiskCalls++;
            State = new ControlOnboardingState(true, DateTimeOffset.UtcNow, acceptedBy);
            return Task.FromResult(State);
        }

        public Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default)
        {
            RevokeRiskCalls++;
            State = new ControlOnboardingState(false, null, revokedBy);
            return Task.FromResult(State);
        }
    }
}
