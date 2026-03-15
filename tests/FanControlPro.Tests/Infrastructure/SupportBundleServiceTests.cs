using System.IO.Compression;
using FanControlPro.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class SupportBundleServiceTests
{
    [Fact]
    public async Task ExportSupportBundleAsync_ShouldCreateZipWithLogsAndData()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-bundle-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "logs");
        var data = Path.Combine(root, "data");
        var output = Path.Combine(root, "out");

        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(output);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(logs, "app-20260315.log"), "log-content");
            await File.WriteAllTextAsync(Path.Combine(data, "control-consent.json"), "{\"schemaVersion\":1}");
            await File.WriteAllTextAsync(Path.Combine(data, "hardware.json"), "{\"schemaVersion\":1}");

            var service = new SupportBundleService(
                Options.Create(new DiagnosticsOptions
                {
                    LogsDirectoryPath = logs,
                    DataRootPath = data,
                    SupportBundlesDirectoryPath = output
                }),
                NullLogger<SupportBundleService>.Instance);

            var result = await service.ExportSupportBundleAsync();

            Assert.True(result.Success);
            Assert.True(File.Exists(result.BundlePath));
            Assert.True(result.IncludedFileCount >= 3);

            using var archive = ZipFile.OpenRead(result.BundlePath);
            var entries = archive.Entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .ToArray();

            Assert.Contains("logs/app-20260315.log", entries);
            Assert.Contains("data/control-consent.json", entries);
            Assert.Contains("data/hardware.json", entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
