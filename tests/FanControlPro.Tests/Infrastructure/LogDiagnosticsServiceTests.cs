using FanControlPro.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public sealed class LogDiagnosticsServiceTests
{
    [Fact]
    public async Task GetRecentEventsAsync_ShouldReturnNewestEventsFromLogFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fancontrolpro-diag-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);

        try
        {
            var logPath = Path.Combine(logs, "app-20260315.log");
            await File.WriteAllLinesAsync(logPath, new[]
            {
                "2026-03-15 10:00:00.000 +01:00 [INF] App started",
                "2026-03-15 10:00:01.000 +01:00 [WRN] Sensor warning",
                "2026-03-15 10:00:02.000 +01:00 [ERR] Safety failure"
            });

            var service = new LogDiagnosticsService(
                Options.Create(new DiagnosticsOptions
                {
                    LogsDirectoryPath = logs
                }),
                NullLogger<LogDiagnosticsService>.Instance);

            var events = await service.GetRecentEventsAsync(maxEvents: 2);

            Assert.Equal(2, events.Count);
            Assert.Equal("ERR", events[0].Level);
            Assert.Contains("Safety failure", events[0].Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("WRN", events[1].Level);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
