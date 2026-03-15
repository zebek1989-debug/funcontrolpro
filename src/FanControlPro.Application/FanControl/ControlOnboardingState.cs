namespace FanControlPro.Application.FanControl;

public sealed record ControlOnboardingState(
    bool HasAcceptedRisk,
    DateTimeOffset? AcceptedAtUtc,
    string? AcceptedBy);
