namespace FanControlPro.Domain.FanControl.Profiles;

public sealed record FanProfileChannelSetting(
    string ChannelId,
    int TargetPercent);
