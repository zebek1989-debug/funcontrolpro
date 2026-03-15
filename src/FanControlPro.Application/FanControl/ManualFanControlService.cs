using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Application.FanControl;

public sealed class ManualFanControlService : IManualFanControlService
{
    private const string NoGroup = "None";

    private readonly IFanControllerFactory _controllerFactory;
    private readonly ILogger<ManualFanControlService> _logger;

    private readonly Dictionary<string, ChannelRuntime> _channelsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public ManualFanControlService(
        IFanControllerFactory controllerFactory,
        ILogger<ManualFanControlService> logger)
    {
        _controllerFactory = controllerFactory;
        _logger = logger;

        AvailableGroups = new[] { NoGroup, "CPU Cooling", "Case Fans", "All Fans" };

        RegisterDefaultChannels();
    }

    public IReadOnlyList<string> AvailableGroups { get; }

    public async Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        ChannelRuntime[] runtimes;
        lock (_sync)
        {
            runtimes = _channelsById.Values.ToArray();
        }

        var snapshots = new List<FanChannelSnapshot>(runtimes.Length);

        foreach (var runtime in runtimes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentPercent = await runtime.Controller.GetCurrentSpeedAsync(runtime.Channel).ConfigureAwait(false);
            var canControl = await runtime.Controller.CanControlAsync(runtime.Channel).ConfigureAwait(false);

            var status = canControl
                ? "Full Control"
                : runtime.Channel.SupportLevel == SupportLevel.FullControl
                    ? "Control Locked"
                    : "Monitoring Only";

            snapshots.Add(new FanChannelSnapshot(
                Id: runtime.Channel.Id,
                Name: runtime.Channel.Name,
                Type: "PWM",
                CurrentPercent: currentPercent,
                CurrentRpm: ToSyntheticRpm(runtime.Channel, currentPercent),
                MinimumPercent: runtime.Channel.SafeMinimumPercent,
                MaximumPercent: runtime.Channel.MaximumPercent,
                IsCpuChannel: runtime.Channel.IsCpuChannel,
                CanControl: canControl,
                Status: status,
                AssignedGroup: runtime.AssignedGroup));
        }

        return snapshots
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<FanControlResult> SetSpeedAsync(
        string channelId,
        int percent,
        bool confirmLowCpuFan,
        CancellationToken cancellationToken = default)
    {
        return SetSpeedInternalAsync(
            channelId,
            percent,
            confirmLowCpuFan,
            propagateToGroup: true,
            cancellationToken);
    }

    public async Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var runtime = GetRuntime(channelId);
        return await SetSpeedInternalAsync(
            channelId,
            runtime.BaselinePercent,
            confirmLowCpuFan: true,
            propagateToGroup: false,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return SetSpeedInternalAsync(
            channelId,
            percent: 100,
            confirmLowCpuFan: true,
            propagateToGroup: false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
    {
        ChannelRuntime[] runtimes;
        lock (_sync)
        {
            runtimes = _channelsById.Values.ToArray();
        }

        var results = new List<FanControlResult>(runtimes.Length);
        foreach (var runtime in runtimes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await SetSpeedInternalAsync(
                runtime.Channel.Id,
                percent: 100,
                confirmLowCpuFan: true,
                propagateToGroup: false,
                cancellationToken).ConfigureAwait(false);

            results.Add(result);
        }

        return results;
    }

    public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = string.IsNullOrWhiteSpace(groupName) ? NoGroup : groupName.Trim();
        if (!AvailableGroups.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported group '{groupName}'.");
        }

        lock (_sync)
        {
            if (!_channelsById.TryGetValue(channelId, out var runtime))
            {
                throw new KeyNotFoundException($"Unknown fan channel '{channelId}'.");
            }

            runtime.AssignedGroup = normalized;
        }

        _logger.LogInformation("Assigned channel {ChannelId} to group {Group}", channelId, normalized);
        return Task.CompletedTask;
    }

    private async Task<FanControlResult> SetSpeedInternalAsync(
        string channelId,
        int percent,
        bool confirmLowCpuFan,
        bool propagateToGroup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = GetRuntime(channelId);

        if (percent < runtime.Channel.SafeMinimumPercent || percent > runtime.Channel.MaximumPercent)
        {
            return FanControlResult.Failed(
                FanControlFailureReason.OutOfRange,
                $"{runtime.Channel.Name}: dozwolony zakres to {runtime.Channel.SafeMinimumPercent}-{runtime.Channel.MaximumPercent}%.");
        }

        if (runtime.Channel.IsCpuChannel && percent < 30 && !confirmLowCpuFan)
        {
            return FanControlResult.Failed(
                FanControlFailureReason.ValidationFailed,
                "CPU_FAN poniżej 30% wymaga dodatkowego potwierdzenia.");
        }

        var result = await runtime.Controller.SetSpeedAsync(runtime.Channel, percent).ConfigureAwait(false);
        if (!result.Success)
        {
            return result;
        }

        if (!propagateToGroup || string.Equals(runtime.AssignedGroup, NoGroup, StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var groupMembers = GetGroupMembers(runtime.AssignedGroup)
            .Where(member => !string.Equals(member.Channel.Id, runtime.Channel.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var synced = 0;
        foreach (var member in groupMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syncResult = await SetSpeedInternalAsync(
                member.Channel.Id,
                percent,
                confirmLowCpuFan,
                propagateToGroup: false,
                cancellationToken).ConfigureAwait(false);

            if (syncResult.Success)
            {
                synced++;
            }
        }

        return FanControlResult.Succeeded(
            result.AppliedPercent,
            synced > 0
                ? $"{runtime.Channel.Name}: prędkość ustawiona i zsynchronizowana z {synced} kanałem(-ami)."
                : result.Message,
            previousPercent: result.PreviousPercent);
    }

    private ChannelRuntime GetRuntime(string channelId)
    {
        lock (_sync)
        {
            if (_channelsById.TryGetValue(channelId, out var runtime))
            {
                return runtime;
            }
        }

        throw new KeyNotFoundException($"Unknown fan channel '{channelId}'.");
    }

    private IReadOnlyList<ChannelRuntime> GetGroupMembers(string groupName)
    {
        lock (_sync)
        {
            return _channelsById.Values
                .Where(x => string.Equals(x.AssignedGroup, groupName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    private void RegisterDefaultChannels()
    {
        var defaults = new[]
        {
            new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true, MinimumPercent: 20),
            new FanChannel("system_fan", "System Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: false),
            new FanChannel("rear_fan", "Rear Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: false),
            new FanChannel("gpu_fan", "GPU Fan", "NVIDIA", SupportLevel.MonitoringOnly, IsCpuChannel: false)
        };

        foreach (var channel in defaults)
        {
            var controller = _controllerFactory.Create(channel);
            var runtime = new ChannelRuntime(channel, controller)
            {
                AssignedGroup = NoGroup
            };

            _channelsById[channel.Id] = runtime;
        }
    }

    private static int ToSyntheticRpm(FanChannel channel, int percent)
    {
        var baseline = channel.IsCpuChannel ? 550 : 420;
        return baseline + (percent * 24);
    }

    private sealed class ChannelRuntime
    {
        public ChannelRuntime(FanChannel channel, IFanControllerV2 controller)
        {
            Channel = channel;
            Controller = controller;
            BaselinePercent = Math.Max(channel.SafeMinimumPercent, 40);
        }

        public FanChannel Channel { get; }

        public IFanControllerV2 Controller { get; }

        public int BaselinePercent { get; }

        public string AssignedGroup { get; set; } = NoGroup;
    }
}
