namespace FanControlPro.Application.FanControl;

public interface IControlConsentStore
{
    Task<ControlOnboardingState?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ControlOnboardingState state, CancellationToken cancellationToken = default);
}
