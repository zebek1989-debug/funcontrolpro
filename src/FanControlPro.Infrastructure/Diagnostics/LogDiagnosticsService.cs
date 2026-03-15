using System.Globalization;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.Diagnostics;

public sealed class LogDiagnosticsService : IDiagnosticsService
{
    private readonly IOptions<DiagnosticsOptions> _options;
    private readonly ILogger<LogDiagnosticsService> _logger;

    public LogDiagnosticsService(
        IOptions<DiagnosticsOptions> options,
        ILogger<LogDiagnosticsService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiagnosticEvent>> GetRecentEventsAsync(
        int maxEvents = 50,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(maxEvents, 1, 500);
        var logsDirectory = ResolvePath(_options.Value.LogsDirectoryPath);
        if (!Directory.Exists(logsDirectory))
        {
            return Array.Empty<DiagnosticEvent>();
        }

        var logFiles = Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var events = new List<DiagnosticEvent>(limit);

        foreach (var file in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                events.Add(ParseLine(line));
                if (events.Count >= limit)
                {
                    return events
                        .OrderByDescending(evt => evt.TimestampUtc)
                        .Take(limit)
                        .ToArray();
                }
            }
        }

        return events
            .OrderByDescending(evt => evt.TimestampUtc)
            .Take(limit)
            .ToArray();
    }

    private DiagnosticEvent ParseLine(string line)
    {
        try
        {
            var firstBracket = line.IndexOf('[');
            var level = "INF";
            var message = line;
            var timestamp = DateTimeOffset.UtcNow;

            if (firstBracket > 0)
            {
                var timestampText = line[..firstBracket].Trim();
                if (DateTimeOffset.TryParse(
                    timestampText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed))
                {
                    timestamp = parsed.ToUniversalTime();
                }

                var secondBracket = line.IndexOf(']', firstBracket + 1);
                if (secondBracket > firstBracket)
                {
                    level = line[(firstBracket + 1)..secondBracket].Trim();
                    message = line[(secondBracket + 1)..].Trim();
                }
            }

            return new DiagnosticEvent(timestamp, level, message);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse diagnostic log line.");
            return new DiagnosticEvent(DateTimeOffset.UtcNow, "UNK", line);
        }
    }

    private static string ResolvePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FanControlPro",
                "logs");
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
