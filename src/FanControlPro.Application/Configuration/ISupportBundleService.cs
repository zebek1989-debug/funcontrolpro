namespace FanControlPro.Application.Configuration;

public interface ISupportBundleService
{
    Task<SupportBundleResult> ExportSupportBundleAsync(
        string? outputDirectory = null,
        CancellationToken cancellationToken = default);
}
