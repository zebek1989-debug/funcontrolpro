using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Infrastructure.FanControl;

public abstract class VendorEcControllerBase : IFanControllerV2
{
    private readonly HashSet<string> _supportedVendors;
    private readonly Dictionary<string, int> _channelSpeeds = new(StringComparer.OrdinalIgnoreCase);

    protected VendorEcControllerBase(IEnumerable<string> supportedVendors)
    {
        _supportedVendors = new HashSet<string>(supportedVendors, StringComparer.OrdinalIgnoreCase);
    }

    public virtual Task<bool> CanControlAsync(FanChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var vendorSupported = channel.Vendor is not null && _supportedVendors.Contains(channel.Vendor);
        var canControl = vendorSupported && channel.SupportLevel == SupportLevel.FullControl;
        return Task.FromResult(canControl);
    }

    public virtual async Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
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

        var previous = await GetCurrentSpeedAsync(channel).ConfigureAwait(false);
        _channelSpeeds[channel.Id] = percent;

        return FanControlResult.Succeeded(
            appliedPercent: percent,
            message: "Speed applied.",
            previousPercent: previous);
    }

    public virtual Task<int> GetCurrentSpeedAsync(FanChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (_channelSpeeds.TryGetValue(channel.Id, out var speed))
        {
            return Task.FromResult(speed);
        }

        var baseline = Math.Max(channel.SafeMinimumPercent, 40);
        _channelSpeeds[channel.Id] = baseline;
        return Task.FromResult(baseline);
    }

    public virtual Task<HealthStatus> GetHealthStatusAsync()
    {
        return Task.FromResult(new HealthStatus(
            State: ControllerHealthState.Healthy,
            Message: "Vendor EC controller ready.",
            CheckedAtUtc: DateTimeOffset.UtcNow,
            Issues: Array.Empty<string>()));
    }
}
