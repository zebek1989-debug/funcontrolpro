using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class ResilientFanController : IFanControllerV2
{
    private readonly IFanControllerV2 _primary;
    private readonly IFanControllerV2 _fallback;
    private readonly ILogger<ResilientFanController> _logger;
    private volatile bool _fallbackActive;

    public ResilientFanController(
        IFanControllerV2 primary,
        IFanControllerV2 fallback,
        ILogger<ResilientFanController> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<bool> CanControlAsync(FanChannel channel)
    {
        if (_fallbackActive)
        {
            return false;
        }

        try
        {
            return await _primary.CanControlAsync(channel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ActivateFallback(ex);
            return false;
        }
    }

    public async Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
    {
        if (_fallbackActive)
        {
            return await _fallback.SetSpeedAsync(channel, percent).ConfigureAwait(false);
        }

        try
        {
            var result = await _primary.SetSpeedAsync(channel, percent).ConfigureAwait(false);
            if (!result.Success && result.FailureReason == FanControlFailureReason.HardwareError)
            {
                ActivateFallback(message: result.Message);
                return await _fallback.SetSpeedAsync(channel, percent).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            ActivateFallback(ex);
            return await _fallback.SetSpeedAsync(channel, percent).ConfigureAwait(false);
        }
    }

    public async Task<int> GetCurrentSpeedAsync(FanChannel channel)
    {
        if (_fallbackActive)
        {
            return await _fallback.GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        }

        try
        {
            return await _primary.GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ActivateFallback(ex);
            return await _fallback.GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        }
    }

    public async Task<HealthStatus> GetHealthStatusAsync()
    {
        if (_fallbackActive)
        {
            return new HealthStatus(
                State: ControllerHealthState.Degraded,
                Message: "Primary controller failed. Monitoring-only fallback active.",
                CheckedAtUtc: DateTimeOffset.UtcNow,
                Issues: new[] { "Fallback activated after controller failure" });
        }

        try
        {
            return await _primary.GetHealthStatusAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ActivateFallback(ex);
            return await _fallback.GetHealthStatusAsync().ConfigureAwait(false);
        }
    }

    private void ActivateFallback(Exception ex)
    {
        _fallbackActive = true;
        _logger.LogError(ex, "Primary fan controller failed. Switching to monitoring-only fallback.");
    }

    private void ActivateFallback(string message)
    {
        _fallbackActive = true;
        _logger.LogWarning("Switching to monitoring-only fallback: {Message}", message);
    }
}
