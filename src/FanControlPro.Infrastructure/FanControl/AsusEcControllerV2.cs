using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class AsusEcControllerV2 : VendorEcControllerBase
{
    private readonly ISuperIoFanControlAccess? _superIoFanControlAccess;
    private readonly IEcRegisterAccess? _ecRegisterAccess;
    private readonly IChannelWriteCooldownGate? _cooldownGate;
    private readonly ILogger<AsusEcControllerV2>? _logger;
    private readonly EcWriteSafetyOptions _options;
    private readonly IReadOnlyDictionary<string, byte> _registerMap;

    public AsusEcControllerV2() : base(new[] { "ASUS" })
    {
        _options = new EcWriteSafetyOptions();
        _registerMap = _options.GetAsusRegisterMap();
    }

    public AsusEcControllerV2(
        IEcRegisterAccess ecRegisterAccess,
        IOptions<EcWriteSafetyOptions> options,
        IChannelWriteCooldownGate cooldownGate,
        ILogger<AsusEcControllerV2> logger,
        ISuperIoFanControlAccess? superIoFanControlAccess = null)
        : base(new[] { "ASUS" })
    {
        ArgumentNullException.ThrowIfNull(ecRegisterAccess);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cooldownGate);

        _ecRegisterAccess = ecRegisterAccess;
        _cooldownGate = cooldownGate;
        _logger = logger;
        _superIoFanControlAccess = superIoFanControlAccess;
        _options = options.Value;
        _registerMap = _options.GetAsusRegisterMap();
    }

    public override async Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
    {
        if (!await CanControlAsync(channel).ConfigureAwait(false))
        {
            return FanControlResult.Failed(
                FanControlFailureReason.MonitoringOnly,
                "Channel is not controllable by this vendor controller.");
        }

        if (percent < channel.SafeMinimumPercent || percent > channel.MaximumPercent)
        {
            return FanControlResult.Failed(
                FanControlFailureReason.OutOfRange,
                $"Requested speed must be between {channel.SafeMinimumPercent}% and {channel.MaximumPercent}%.");
        }

        var previous = await base.GetCurrentSpeedAsync(channel).ConfigureAwait(false);

        if (_options.EnableHardwareAccess && _cooldownGate is not null)
        {
            await _cooldownGate.WaitForTurnAsync(channel.Id).ConfigureAwait(false);
        }

        var superIoAttempted = false;
        if (_options.PreferSuperIoControlPath && _superIoFanControlAccess is not null)
        {
            superIoAttempted = true;
            var superIoResult = await _superIoFanControlAccess
                .TrySetSpeedAsync(channel, percent)
                .ConfigureAwait(false);

            if (superIoResult.PathAvailable)
            {
                if (!superIoResult.Success)
                {
                    return FanControlResult.Failed(
                        FanControlFailureReason.HardwareError,
                        $"Super I/O write failed: {superIoResult.Message}",
                        previousPercent: previous);
                }

                await base.SetSpeedAsync(channel, superIoResult.AppliedPercent).ConfigureAwait(false);
                return FanControlResult.Succeeded(
                    appliedPercent: superIoResult.AppliedPercent,
                    message: $"Speed applied via ASUS Super I/O backend ({superIoResult.Message})",
                    previousPercent: previous);
            }

            _logger?.LogDebug(
                "Super I/O path unavailable for channel {ChannelId}: {Reason}",
                channel.Id,
                superIoResult.Message);
        }

        if (!TryResolveHardwarePath(channel, out var registerAddress, out var missingReason))
        {
            if (_options.EnableHardwareAccess)
            {
                _logger?.LogDebug(
                    "Hardware EC path unavailable for channel {ChannelId}. Falling back to simulation. Reason: {Reason}",
                    channel.Id,
                    missingReason);
            }

            if (superIoAttempted)
            {
                _logger?.LogDebug("Falling back to simulation after Super I/O path was unavailable.");
            }

            return await base.SetSpeedAsync(channel, percent).ConfigureAwait(false);
        }

        var registerValue = ToRegisterValue(percent, _options.GetSafeRegisterScaleMaxValue());
        var writeResult = await _ecRegisterAccess!.WriteRegisterAsync(registerAddress, registerValue).ConfigureAwait(false);
        if (!writeResult.Success)
        {
            _logger?.LogWarning(
                "Hardware EC write failed for channel {ChannelId} register 0x{Register:X2}: {Message}",
                channel.Id,
                registerAddress,
                writeResult.Message);

            return FanControlResult.Failed(
                FanControlFailureReason.HardwareError,
                $"Hardware EC write failed: {writeResult.Message}",
                previousPercent: previous);
        }

        if (_options.VerifyReadBack)
        {
            var readBack = await _ecRegisterAccess.ReadRegisterAsync(registerAddress).ConfigureAwait(false);
            if (!readBack.Success)
            {
                return FanControlResult.Failed(
                    FanControlFailureReason.HardwareError,
                    $"Read-back verification failed: {readBack.Message}",
                    previousPercent: previous);
            }

            var readBackPercent = ToPercent(readBack.Value, _options.GetSafeRegisterScaleMaxValue());
            var tolerance = _options.GetSafeReadBackTolerancePercent();
            if (Math.Abs(readBackPercent - percent) > tolerance)
            {
                return FanControlResult.Failed(
                    FanControlFailureReason.HardwareError,
                    $"Read-back mismatch. Requested {percent}%, got {readBackPercent}%.",
                    appliedPercent: readBackPercent,
                    previousPercent: previous);
            }
        }

        // Keep in-memory cache synchronized for downstream services and current tests.
        await base.SetSpeedAsync(channel, percent).ConfigureAwait(false);

        return FanControlResult.Succeeded(
            appliedPercent: percent,
            message: "Speed applied via ASUS EC backend.",
            previousPercent: previous);
    }

    public override async Task<int> GetCurrentSpeedAsync(FanChannel channel)
    {
        if (_options.PreferSuperIoControlPath && _superIoFanControlAccess is not null)
        {
            var superIoRead = await _superIoFanControlAccess
                .TryReadSpeedPercentAsync(channel)
                .ConfigureAwait(false);

            if (superIoRead.PathAvailable)
            {
                if (superIoRead.Success)
                {
                    return superIoRead.Percent;
                }

                _logger?.LogDebug(
                    "Super I/O read failed for channel {ChannelId}: {Reason}",
                    channel.Id,
                    superIoRead.Message);
            }
        }

        if (!TryResolveHardwarePath(channel, out var registerAddress, out _))
        {
            return await base.GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        }

        var readResult = await _ecRegisterAccess!.ReadRegisterAsync(registerAddress).ConfigureAwait(false);
        if (!readResult.Success)
        {
            _logger?.LogDebug(
                "Hardware EC read failed for channel {ChannelId} register 0x{Register:X2}: {Message}",
                channel.Id,
                registerAddress,
                readResult.Message);

            return await base.GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        }

        return ToPercent(readResult.Value, _options.GetSafeRegisterScaleMaxValue());
    }

    public override Task<HealthStatus> GetHealthStatusAsync()
    {
        if (!_options.EnableHardwareAccess)
        {
            return Task.FromResult(new HealthStatus(
                ControllerHealthState.Degraded,
                "ASUS controller is running in simulation mode (hardware path disabled).",
                DateTimeOffset.UtcNow,
                new[] { "Enable EcWriteSafetyOptions.EnableHardwareAccess to activate hardware write paths." }));
        }

        if (_options.PreferSuperIoControlPath)
        {
            if (_superIoFanControlAccess is null)
            {
                return Task.FromResult(new HealthStatus(
                    ControllerHealthState.Unavailable,
                    "ASUS Super I/O backend dependencies are not registered.",
                    DateTimeOffset.UtcNow,
                    new[] { "ISuperIoFanControlAccess is required for preferred Super I/O mode." }));
            }

            return Task.FromResult(HealthStatus.Healthy("ASUS Super I/O backend ready (preferred mode)."));
        }

        if (_ecRegisterAccess is null || _cooldownGate is null)
        {
            return Task.FromResult(new HealthStatus(
                ControllerHealthState.Unavailable,
                "ASUS EC backend dependencies are not registered.",
                DateTimeOffset.UtcNow,
                new[] { "IEcRegisterAccess and IChannelWriteCooldownGate are required." }));
        }

        if (_registerMap.Count == 0)
        {
            return Task.FromResult(new HealthStatus(
                ControllerHealthState.Degraded,
                "ASUS EC backend is enabled but no channel register map is configured.",
                DateTimeOffset.UtcNow,
                new[] { "Populate EcWriteSafetyOptions.AsusPwmRegisters with validated channel/register pairs." }));
        }

        return Task.FromResult(HealthStatus.Healthy("ASUS EC backend ready."));
    }

    private bool TryResolveHardwarePath(FanChannel channel, out byte registerAddress, out string reason)
    {
        registerAddress = 0;
        reason = string.Empty;

        if (!_options.EnableHardwareAccess)
        {
            reason = "Hardware access disabled in options.";
            return false;
        }

        if (_ecRegisterAccess is null || _cooldownGate is null)
        {
            reason = "Hardware services unavailable.";
            return false;
        }

        if (!_registerMap.TryGetValue(channel.Id, out registerAddress))
        {
            reason = $"No EC register mapping for channel '{channel.Id}'.";
            return false;
        }

        return true;
    }

    private static byte ToRegisterValue(int percent, int scaleMaxValue)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        var scaled = (normalized / 100d) * scaleMaxValue;
        return (byte)Math.Clamp((int)Math.Round(scaled, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static int ToPercent(byte registerValue, int scaleMaxValue)
    {
        var scaled = (registerValue / (double)scaleMaxValue) * 100d;
        return Math.Clamp((int)Math.Round(scaled, MidpointRounding.AwayFromZero), 0, 100);
    }
}
