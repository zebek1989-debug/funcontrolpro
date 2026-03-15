using FanControlPro.Application.HardwareDetection;
using FanControlPro.Application.Onboarding;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;

namespace FanControlPro.Application.Onboarding;

public sealed class OnboardingService : IOnboardingService
{
    private readonly IOnboardingStateStore _stateStore;
    private readonly IHardwareDetector _hardwareDetector;

    public OnboardingService(
        IOnboardingStateStore stateStore,
        IHardwareDetector hardwareDetector)
    {
        _stateStore = stateStore;
        _hardwareDetector = hardwareDetector;
    }

    public async Task<OnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return state ?? new OnboardingState(OnboardingStep.Welcome, false, null);
    }

    public async Task CompleteStepAsync(OnboardingStep step, CancellationToken cancellationToken = default)
    {
        var currentState = await GetStateAsync(cancellationToken).ConfigureAwait(false);

        var newStep = step switch
        {
            OnboardingStep.Welcome => OnboardingStep.HardwareDetection,
            OnboardingStep.HardwareDetection => OnboardingStep.HardwareClassification,
            OnboardingStep.HardwareClassification => OnboardingStep.RiskAcceptance,
            OnboardingStep.RiskAcceptance => OnboardingStep.Completed,
            OnboardingStep.Completed => OnboardingStep.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(step))
        };

        var isCompleted = newStep == OnboardingStep.Completed;
        DateTimeOffset? completedAt = isCompleted ? DateTimeOffset.UtcNow : null;

        var newState = new OnboardingState(newStep, isCompleted, completedAt);
        await _stateStore.SaveAsync(newState, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        return state.IsCompleted;
    }

    public async Task<HardwareClassificationResult> ClassifyHardwareAsync(CancellationToken cancellationToken = default)
    {
        var detectionResult = await _hardwareDetector.DetectHardwareAsync(cancellationToken).ConfigureAwait(false);

        var classifications = new List<HardwareComponentClassification>();

        foreach (var component in detectionResult.Components)
        {
            var classification = new HardwareComponentClassification(
                ComponentName: component.Name,
                ComponentType: GetComponentTypeName(component.Type),
                Level: component.SupportLevel,
                Reason: component.SupportReason);

            classifications.Add(classification);
        }

        var hasFullControl = classifications.Any(c => c.Level == SupportLevel.FullControl);
        var hasMonitoringOnly = classifications.Any(c => c.Level == SupportLevel.MonitoringOnly);
        var hasUnsupported = classifications.Any(c => c.Level == SupportLevel.Unsupported);

        return new HardwareClassificationResult(
            Components: classifications,
            HasFullControlComponents: hasFullControl,
            HasMonitoringOnlyComponents: hasMonitoringOnly,
            HasUnsupportedComponents: hasUnsupported);
    }

    private static string GetComponentTypeName(HardwareComponentType type) => type switch
    {
        HardwareComponentType.Cpu => "CPU",
        HardwareComponentType.Gpu => "GPU",
        HardwareComponentType.Motherboard => "Motherboard",
        HardwareComponentType.Memory => "Memory",
        HardwareComponentType.Storage => "Storage",
        HardwareComponentType.Network => "Network",
        HardwareComponentType.Controller => "Controller",
        _ => "Unknown"
    };
}
