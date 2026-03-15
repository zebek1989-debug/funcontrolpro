namespace FanControlPro.Application.FanControl;

public sealed class ControlOnboardingService : IControlOnboardingService
{
    private readonly IControlConsentStore _store;

    public ControlOnboardingService(IControlConsentStore store)
    {
        _store = store;
    }

    public async Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return state ?? new ControlOnboardingState(false, null, null);
    }

    public async Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        return state.HasAcceptedRisk;
    }

    public async Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(acceptedBy))
        {
            acceptedBy = Environment.UserName;
        }

        var state = new ControlOnboardingState(
            HasAcceptedRisk: true,
            AcceptedAtUtc: DateTimeOffset.UtcNow,
            AcceptedBy: acceptedBy);

        await _store.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }

    public async Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(revokedBy))
        {
            revokedBy = Environment.UserName;
        }

        var state = new ControlOnboardingState(
            HasAcceptedRisk: false,
            AcceptedAtUtc: null,
            AcceptedBy: revokedBy);

        await _store.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }
}
