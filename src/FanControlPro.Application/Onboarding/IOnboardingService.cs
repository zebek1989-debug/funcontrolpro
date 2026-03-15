using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Application.Onboarding;

public interface IOnboardingService
{
    Task<OnboardingState> GetStateAsync(CancellationToken cancellationToken = default);

    Task CompleteStepAsync(OnboardingStep step, CancellationToken cancellationToken = default);

    Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default);

    Task<HardwareClassificationResult> ClassifyHardwareAsync(CancellationToken cancellationToken = default);
}

public enum OnboardingStep
{
    Welcome = 0,
    HardwareDetection = 1,
    HardwareClassification = 2,
    RiskAcceptance = 3,
    Completed = 4
}

public sealed record OnboardingState(
    OnboardingStep CurrentStep,
    bool IsCompleted,
    DateTimeOffset? CompletedAtUtc);

public sealed record HardwareClassificationResult(
    IReadOnlyList<HardwareComponentClassification> Components,
    bool HasFullControlComponents,
    bool HasMonitoringOnlyComponents,
    bool HasUnsupportedComponents);

public sealed record HardwareComponentClassification(
    string ComponentName,
    string ComponentType,
    SupportLevel Level,
    string Reason);
