using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Infrastructure.Recovery;

public sealed class StartupRecoveryService : IStartupRecoveryService
{
    private readonly IConfigurationHealthValidator _configurationHealthValidator;
    private readonly IRestoreManager _restoreManager;
    private readonly ILogger<StartupRecoveryService> _logger;

    public StartupRecoveryService(
        IConfigurationHealthValidator configurationHealthValidator,
        IRestoreManager restoreManager,
        ILogger<StartupRecoveryService> logger)
    {
        _configurationHealthValidator = configurationHealthValidator;
        _restoreManager = restoreManager;
        _logger = logger;
    }

    public async Task<StartupRecoveryResult> EnsureHealthyStartupAsync(CancellationToken cancellationToken = default)
    {
        var before = await _configurationHealthValidator.ValidateCurrentConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);

        if (before.IsValid)
        {
            return new StartupRecoveryResult(
                HealthyBeforeRecovery: true,
                Recovered: false,
                FallbackToSafeDefaults: false,
                Message: "Configuration healthy.");
        }

        _logger.LogWarning(
            "Configuration corruption detected on startup. Attempting restore. Issues: {Issues}",
            string.Join(" | ", before.Errors));

        var restore = await _restoreManager.RestoreLastKnownGoodAsync(cancellationToken).ConfigureAwait(false);
        var after = await _configurationHealthValidator.ValidateCurrentConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);

        if (restore.Success && after.IsValid)
        {
            return new StartupRecoveryResult(
                HealthyBeforeRecovery: false,
                Recovered: true,
                FallbackToSafeDefaults: restore.FallbackToSafeDefaults,
                Message: restore.Message);
        }

        return new StartupRecoveryResult(
            HealthyBeforeRecovery: false,
            Recovered: false,
            FallbackToSafeDefaults: restore.FallbackToSafeDefaults,
            Message: restore.Success
                ? "Recovery executed but configuration is still invalid."
                : "Recovery failed.");
    }
}
