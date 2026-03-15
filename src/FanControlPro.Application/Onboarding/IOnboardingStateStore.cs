using FanControlPro.Application.Onboarding;

namespace FanControlPro.Application.Onboarding;

public interface IOnboardingStateStore
{
    Task<OnboardingState?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default);
}