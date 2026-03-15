using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Curves;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.FanControl.Curves;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.FanControl;

public sealed class FanCurveServiceTests
{
    [Fact]
    public async Task GetOrCreateCurveAsync_ShouldCreateDefaultCurveForCpu()
    {
        var service = CreateService();

        var curve = await service.GetOrCreateCurveAsync("cpu_fan", "cpu_temp", isCpuChannel: true);

        Assert.Equal("cpu_fan", curve.ChannelId);
        Assert.True(curve.Points.Count >= 4);
        Assert.Equal(20, curve.MinimumAllowedPercent);
    }

    [Fact]
    public async Task SaveCurveAsync_ShouldReturnInvalid_WhenCurveIsInvalid()
    {
        var service = CreateService();
        var invalidCurve = new FanCurve(
            ChannelId: "cpu_fan",
            SensorId: "cpu_temp",
            Points: new[]
            {
                new FanCurvePoint(40, 20),
                new FanCurvePoint(30, 30),
                new FanCurvePoint(20, 40),
                new FanCurvePoint(10, 50)
            },
            HysteresisCelsius: 2,
            SmoothingFactor: 0.3,
            MinimumAllowedPercent: 20,
            MaximumAllowedPercent: 100);

        var result = await service.SaveCurveAsync(invalidCurve);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task RunTestModeAsync_ShouldApplySpeedThroughManualControl()
    {
        var service = CreateService();

        var result = await service.RunTestModeAsync(
            channelId: "cpu_fan",
            sensorId: "cpu_temp",
            isCpuChannel: true,
            temperatureCelsius: 70,
            confirmLowCpuFan: true);

        Assert.True(result.Success);
        Assert.Contains("Curve test applied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FanCurveService CreateService()
    {
        return new FanCurveService(
            curveEngine: new CurveEngine(),
            manualControlService: new FakeManualControlService(),
            logger: NullLogger<FanCurveService>.Instance);
    }

    private sealed class FakeManualControlService : IManualFanControlService
    {
        public IReadOnlyList<string> AvailableGroups { get; } = new[] { "None", "All Fans" };

        public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(new[]
            {
                new FanChannelSnapshot(
                    Id: "cpu_fan",
                    Name: "CPU Fan",
                    Type: "PWM",
                    CurrentPercent: 40,
                    CurrentRpm: 1250,
                    MinimumPercent: 20,
                    MaximumPercent: 100,
                    IsCpuChannel: true,
                    CanControl: true,
                    Status: "Full Control",
                    AssignedGroup: "None")
            });
        }

        public Task<FanControlResult> SetSpeedAsync(string channelId, int percent, bool confirmLowCpuFan, CancellationToken cancellationToken = default)
        {
            if (channelId == "cpu_fan" && percent < 20)
            {
                return Task.FromResult(FanControlResult.Failed(
                    FanControlFailureReason.OutOfRange,
                    "CPU fan minimum is 20%."));
            }

            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(40, "Reset"));

        public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));

        public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FanControlResult>>(new[] { FanControlResult.Succeeded(100, "Full speed") });

        public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
