namespace FanControlPro.Application.Configuration;

public interface IStartupRecoveryService
{
    Task<StartupRecoveryResult> EnsureHealthyStartupAsync(CancellationToken cancellationToken = default);
}
