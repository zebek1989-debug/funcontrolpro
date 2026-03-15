namespace FanControlPro.Application.FanControl;

public interface IControlOnboardingService
{
    Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default);

    Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default);

    Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default);
}
