namespace FanControlPro.Application.FanControl;

public sealed record FanChannelSnapshot(
    string Id,
    string Name,
    string Type,
    int CurrentPercent,
    int CurrentRpm,
    int MinimumPercent,
    int MaximumPercent,
    bool IsCpuChannel,
    bool CanControl,
    string Status,
    string AssignedGroup);
