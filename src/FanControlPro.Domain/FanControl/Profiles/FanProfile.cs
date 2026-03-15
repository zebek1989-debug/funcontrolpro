namespace FanControlPro.Domain.FanControl.Profiles;

public sealed record FanProfile(
    string Name,
    string Description,
    bool IsPredefined,
    IReadOnlyList<FanProfileChannelSetting> ChannelSettings,
    DateTimeOffset UpdatedAtUtc);
