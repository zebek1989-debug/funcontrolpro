using System.IO.Compression;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Diagnostics;

public sealed class SupportBundleService : ISupportBundleService
{
    private readonly IOptions<DiagnosticsOptions> _options;
    private readonly ILogger<SupportBundleService> _logger;

    public SupportBundleService(
        IOptions<DiagnosticsOptions> options,
        ILogger<SupportBundleService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<SupportBundleResult> ExportSupportBundleAsync(
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _options.Value;
            var logsDirectory = ResolvePath(options.LogsDirectoryPath, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FanControlPro",
                "logs"));
            var dataDirectory = ResolvePath(options.DataRootPath, Path.Combine("data"));
            var targetDirectory = ResolvePath(
                outputDirectory ?? options.SupportBundlesDirectoryPath,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FanControlPro",
                    "support"));

            Directory.CreateDirectory(targetDirectory);

            var bundlePath = Path.Combine(
                targetDirectory,
                $"support-bundle-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.zip");

            var collectedFiles = CollectFiles(logsDirectory, dataDirectory);
            if (collectedFiles.Count == 0)
            {
                return new SupportBundleResult(
                    Success: false,
                    BundlePath: string.Empty,
                    IncludedFileCount: 0,
                    Message: "No diagnostic files were found to export.");
            }

            await using var zipStream = File.Create(bundlePath);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (absolutePath, entryName) in collectedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(absolutePath, entryName, CompressionLevel.Optimal);
                }
            }

            await zipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Support bundle exported to {BundlePath} ({FileCount} files).",
                bundlePath,
                collectedFiles.Count);

            return new SupportBundleResult(
                Success: true,
                BundlePath: bundlePath,
                IncludedFileCount: collectedFiles.Count,
                Message: "Support bundle exported successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export support bundle.");
            return new SupportBundleResult(
                Success: false,
                BundlePath: string.Empty,
                IncludedFileCount: 0,
                Message: "Failed to export support bundle.");
        }
    }

    private static List<(string AbsolutePath, string EntryName)> CollectFiles(string logsDirectory, string dataDirectory)
    {
        var files = new List<(string AbsolutePath, string EntryName)>();

        if (Directory.Exists(logsDirectory))
        {
            foreach (var path in Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly))
            {
                files.Add((path, Path.Combine("logs", Path.GetFileName(path))));
            }
        }

        if (Directory.Exists(dataDirectory))
        {
            foreach (var path in Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(dataDirectory, path);
                files.Add((path, Path.Combine("data", relative)));
            }
        }

        return files
            .GroupBy(item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolvePath(string configuredPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = fallbackPath;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
