namespace FanControlPro.Application.FanControl.Safety;

public interface ISafetyMonitorServiceV2
{
    event EventHandler<HealthAttestation>? HealthAttestationChanged;

    Task EnterSafeModeAsync(SafeModeReason reason, CancellationToken cancellationToken = default);

    Task<bool> ValidateSensorHealthAsync(CancellationToken cancellationToken = default);

    Task<HealthAttestation> GetHealthAttestationAsync(CancellationToken cancellationToken = default);
}
