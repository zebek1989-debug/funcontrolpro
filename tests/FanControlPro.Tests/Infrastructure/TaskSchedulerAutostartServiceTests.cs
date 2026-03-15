using FanControlPro.Application.Configuration;
using FanControlPro.Infrastructure.SystemIntegration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.Infrastructure;

public class TaskSchedulerAutostartServiceTests
{
    [Fact]
    public async Task ConfigureAsync_EnablesTaskWithMinimizedFlagAndDelay()
    {
        var commands = new List<(string FileName, string Arguments)>();
        var service = CreateService(
            options: new AutostartOptions
            {
                TaskName = "FanControlPro.Test",
                ExecutablePath = @"C:\Apps\FanControlPro\FanControlPro.exe"
            },
            commands: commands,
            queuedExitCodes: new Queue<int>(new[] { 0 }),
            isWindows: () => true);

        await service.ConfigureAsync(
            enabled: true,
            startMinimizedToTray: true,
            startupDelay: TimeSpan.FromSeconds(45));

        var command = Assert.Single(commands);
        Assert.Equal("schtasks", command.FileName);
        Assert.Contains("/Create", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("/TN \"FanControlPro.Test\"", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("/SC ONLOGON", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("/RL HIGHEST", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("--start-minimized", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("/DELAY 0000:45", command.Arguments, StringComparison.Ordinal);
        Assert.Contains("\\\"C:\\Apps\\FanControlPro\\FanControlPro.exe\\\"", command.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigureAsync_DisableRemovesTaskWhenItExists()
    {
        var commands = new List<(string FileName, string Arguments)>();
        var service = CreateService(
            options: new AutostartOptions { TaskName = "FanControlPro.Test" },
            commands: commands,
            queuedExitCodes: new Queue<int>(new[] { 0, 0 }),
            isWindows: () => true);

        await service.ConfigureAsync(
            enabled: false,
            startMinimizedToTray: true,
            startupDelay: TimeSpan.Zero);

        Assert.Equal(2, commands.Count);
        Assert.Contains("/Query /TN \"FanControlPro.Test\"", commands[0].Arguments, StringComparison.Ordinal);
        Assert.Contains("/Delete /TN \"FanControlPro.Test\" /F", commands[1].Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalseOnNonWindowsWithoutExecutingCommands()
    {
        var commands = new List<(string FileName, string Arguments)>();
        var service = CreateService(
            options: new AutostartOptions(),
            commands: commands,
            queuedExitCodes: new Queue<int>(),
            isWindows: () => false);

        var enabled = await service.IsEnabledAsync();

        Assert.False(enabled);
        Assert.Empty(commands);
    }

    private static TaskSchedulerAutostartService CreateService(
        AutostartOptions options,
        List<(string FileName, string Arguments)> commands,
        Queue<int> queuedExitCodes,
        Func<bool> isWindows)
    {
        return new TaskSchedulerAutostartService(
            Options.Create(options),
            NullLogger<TaskSchedulerAutostartService>.Instance,
            runCommandAsync: (fileName, arguments, _) =>
            {
                commands.Add((fileName, arguments));

                var exitCode = queuedExitCodes.Count > 0
                    ? queuedExitCodes.Dequeue()
                    : 0;

                return Task.FromResult(exitCode);
            },
            isWindows: isWindows);
    }
}
