using FanControlPro.Application.FanControl;

namespace FanControlPro.Tests.FanControl;

public sealed class ControlOnboardingServiceTests
{
    [Fact]
    public async Task AcceptRiskAsync_ShouldPersistConsent()
    {
        var store = new InMemoryConsentStore();
        var service = new ControlOnboardingService(store);

        await service.AcceptRiskAsync("qa-user");
        var hasAccepted = await service.HasAcceptedRiskAsync();

        Assert.True(hasAccepted);
    }

    [Fact]
    public async Task RevokeRiskAsync_ShouldDisableConsent()
    {
        var store = new InMemoryConsentStore();
        var service = new ControlOnboardingService(store);

        await service.AcceptRiskAsync("qa-user");
        await service.RevokeRiskAsync("qa-user");

        var state = await service.GetStateAsync();
        Assert.False(state.HasAcceptedRisk);
        Assert.Null(state.AcceptedAtUtc);
    }

    private sealed class InMemoryConsentStore : IControlConsentStore
    {
        private ControlOnboardingState? _state;

        public Task<ControlOnboardingState?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task SaveAsync(ControlOnboardingState state, CancellationToken cancellationToken = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }
}
