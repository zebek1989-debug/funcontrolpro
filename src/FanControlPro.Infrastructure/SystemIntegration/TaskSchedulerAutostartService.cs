using System.Diagnostics;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FanControlPro.Infrastructure.SystemIntegration;

public sealed class TaskSchedulerAutostartService : IAutostartService
{
    private readonly IOptions<AutostartOptions> _options;
    private readonly ILogger<TaskSchedulerAutostartService> _logger;
    private readonly Func<string, string, CancellationToken, Task<int>> _runCommandAsync;
    private readonly Func<bool> _isWindows;

    public TaskSchedulerAutostartService(
        IOptions<AutostartOptions> options,
        ILogger<TaskSchedulerAutostartService> logger,
        Func<string, string, CancellationToken, Task<int>>? runCommandAsync = null,
        Func<bool>? isWindows = null)
    {
        _options = options;
        _logger = logger;
        _runCommandAsync = runCommandAsync ?? RunCommandAsync;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (!_isWindows())
        {
            return false;
        }

        var taskName = ResolveTaskName(_options.Value.TaskName);
        try
        {
            var exitCode = await _runCommandAsync(
                    "schtasks",
                    BuildQueryArguments(taskName),
                    cancellationToken)
                .ConfigureAwait(false);

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autostart query failed for task {TaskName}", taskName);
            return false;
        }
    }

    public async Task ConfigureAsync(
        bool enabled,
        bool startMinimizedToTray,
        TimeSpan startupDelay,
        CancellationToken cancellationToken = default)
    {
        if (!_isWindows())
        {
            _logger.LogDebug("Skipping Task Scheduler autostart setup on non-Windows platform.");
            return;
        }

        var options = _options.Value;
        var taskName = ResolveTaskName(options.TaskName);

        if (enabled)
        {
            var executablePath = ResolveExecutablePath(options.ExecutablePath);
            var createArguments = BuildCreateArguments(
                taskName,
                executablePath,
                startMinimizedToTray,
                startupDelay);

            var exitCode = await _runCommandAsync(
                    "schtasks",
                    createArguments,
                    cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Could not configure autostart task '{taskName}'. schtasks exit code: {exitCode}.");
            }

            _logger.LogInformation(
                "Autostart task configured. Task={TaskName}, StartMinimized={StartMinimized}, Delay={DelaySeconds}s",
                taskName,
                startMinimizedToTray,
                Math.Max(0, (int)Math.Round(startupDelay.TotalSeconds)));

            return;
        }

        var exists = await IsEnabledAsync(cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return;
        }

        var deleteExitCode = await _runCommandAsync(
                "schtasks",
                BuildDeleteArguments(taskName),
                cancellationToken)
            .ConfigureAwait(false);

        if (deleteExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not remove autostart task '{taskName}'. schtasks exit code: {deleteExitCode}.");
        }

        _logger.LogInformation("Autostart task removed: {TaskName}", taskName);
    }

    internal static string BuildCreateArguments(
        string taskName,
        string executablePath,
        bool startMinimizedToTray,
        TimeSpan startupDelay)
    {
        var runTarget = BuildTaskRunTarget(executablePath, startMinimizedToTray);

        var arguments =
            $"/Create /TN \"{taskName}\" /SC ONLOGON /TR \"{runTarget}\" /RL HIGHEST /F";

        if (startupDelay > TimeSpan.Zero)
        {
            arguments += $" /DELAY {FormatDelay(startupDelay)}";
        }

        return arguments;
    }

    internal static string BuildDeleteArguments(string taskName)
        => $"/Delete /TN \"{taskName}\" /F";

    internal static string BuildQueryArguments(string taskName)
        => $"/Query /TN \"{taskName}\"";

    internal static string FormatDelay(TimeSpan delay)
    {
        var totalSeconds = (int)Math.Clamp(
            Math.Round(delay.TotalSeconds),
            min: 0,
            max: 9999 * 60 + 59);

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:0000}:{seconds:00}";
    }

    private static string BuildTaskRunTarget(string executablePath, bool startMinimizedToTray)
    {
        var escapedPath = $"\\\"{executablePath}\\\"";
        return startMinimizedToTray
            ? $"{escapedPath} --start-minimized"
            : escapedPath;
    }

    private static string ResolveTaskName(string? configuredName)
    {
        return string.IsNullOrWhiteSpace(configuredName)
            ? "FanControlPro.Autostart"
            : configuredName.Trim();
    }

    private static string ResolveExecutablePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var trimmedPath = configuredPath.Trim();
            if (IsWindowsStyleAbsolutePath(trimmedPath) || Path.IsPathRooted(trimmedPath))
            {
                return trimmedPath;
            }

            return Path.GetFullPath(trimmedPath);
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        throw new InvalidOperationException("Could not resolve executable path for Task Scheduler autostart.");
    }

    private static bool IsWindowsStyleAbsolutePath(string path)
    {
        return path.Length >= 3
               && char.IsLetter(path[0])
               && path[1] == ':'
               && (path[2] == '\\' || path[2] == '/');
    }

    private static async Task<int> RunCommandAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
