using FanControlPro.Infrastructure.FanControl;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace FanControlPro.Tests.FanControl;

public sealed class ChannelWriteCooldownGateTests
{
    [Fact]
    public async Task WaitForTurnAsync_ShouldDelaySequentialWritesOnSameChannel()
    {
        var gate = new ChannelWriteCooldownGate(Options.Create(new EcWriteSafetyOptions
        {
            MinimumWriteIntervalMs = 80
        }));

        await gate.WaitForTurnAsync("cpu_fan");

        var stopwatch = Stopwatch.StartNew();
        await gate.WaitForTurnAsync("cpu_fan");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 60);
    }

    [Fact]
    public async Task WaitForTurnAsync_ShouldNotDelayDifferentChannels()
    {
        var gate = new ChannelWriteCooldownGate(Options.Create(new EcWriteSafetyOptions
        {
            MinimumWriteIntervalMs = 100
        }));

        await gate.WaitForTurnAsync("cpu_fan");

        var stopwatch = Stopwatch.StartNew();
        await gate.WaitForTurnAsync("system_fan");
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 50);
    }
}
