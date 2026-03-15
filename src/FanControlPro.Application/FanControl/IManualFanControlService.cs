using FanControlPro.Domain.FanControl;

namespace FanControlPro.Application.FanControl;

public interface IManualFanControlService
{
    IReadOnlyList<string> AvailableGroups { get; }

    Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default);

    Task<FanControlResult> SetSpeedAsync(
        string channelId,
        int percent,
        bool confirmLowCpuFan,
        CancellationToken cancellationToken = default);

    Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default);

    Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default);

    Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default);
}
