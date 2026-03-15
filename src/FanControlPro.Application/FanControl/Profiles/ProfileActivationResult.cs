namespace FanControlPro.Application.FanControl.Profiles;

public sealed record ProfileActivationResult(
    bool Success,
    string ProfileName,
    int AppliedChannelCount,
    int TotalChannelCount,
    string Message,
    IReadOnlyList<string> Errors);
