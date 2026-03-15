namespace FanControlPro.Application.Configuration;

public sealed record StartupRecoveryResult(
    bool HealthyBeforeRecovery,
    bool Recovered,
    bool FallbackToSafeDefaults,
    string Message);
