namespace FanControlPro.Application.Configuration;

public sealed record SupportBundleResult(
    bool Success,
    string BundlePath,
    int IncludedFileCount,
    string Message);
