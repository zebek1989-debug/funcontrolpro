using FanControlPro.Application.FanControl.Curves;
using FanControlPro.Domain.FanControl.Curves;

namespace FanControlPro.Tests.FanControl;

public sealed class CurveEngineTests
{
    private readonly CurveEngine _engine = new();

    [Fact]
    public void CalculateSpeedForTemperature_ShouldInterpolateLinearly()
    {
        var curve = CreateCurve(
            new FanCurvePoint(30, 20),
            new FanCurvePoint(50, 40),
            new FanCurvePoint(70, 70),
            new FanCurvePoint(90, 100));

        var speed = _engine.CalculateSpeedForTemperature(curve, 60);

        Assert.Equal(55, speed);
    }

    [Fact]
    public void ValidateCurve_ShouldFail_WhenPointCountOutsideRange()
    {
        var curve = CreateCurve(
            new FanCurvePoint(30, 20),
            new FanCurvePoint(60, 50),
            new FanCurvePoint(90, 90));

        var validation = _engine.ValidateCurve(curve);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, message => message.Contains("between 4 and 8", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCurve_ShouldFail_WhenTemperatureNotIncreasing()
    {
        var curve = CreateCurve(
            new FanCurvePoint(30, 20),
            new FanCurvePoint(50, 40),
            new FanCurvePoint(50, 60),
            new FanCurvePoint(80, 90));

        var validation = _engine.ValidateCurve(curve);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, message => message.Contains("strictly increase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_ShouldUseHysteresis_WhenDeltaBelowThreshold()
    {
        var curve = CreateCurve(
            new FanCurvePoint(30, 20),
            new FanCurvePoint(50, 40),
            new FanCurvePoint(70, 70),
            new FanCurvePoint(90, 100));

        var state = new CurveEvaluationState(LastTemperatureCelsius: 55, LastAppliedSpeedPercent: 50);

        var evaluation = _engine.Evaluate(curve, temperatureCelsius: 56, state);

        Assert.True(evaluation.UsedHysteresis);
        Assert.Equal(50, evaluation.AppliedSpeedPercent);
    }

    [Fact]
    public void Evaluate_ShouldSmooth_WhenDeltaExceedsHysteresis()
    {
        var curve = CreateCurve(
            new FanCurvePoint(30, 20),
            new FanCurvePoint(50, 40),
            new FanCurvePoint(70, 70),
            new FanCurvePoint(90, 100));

        var state = new CurveEvaluationState(LastTemperatureCelsius: 45, LastAppliedSpeedPercent: 30);

        var evaluation = _engine.Evaluate(curve, temperatureCelsius: 70, state);

        Assert.True(evaluation.UsedSmoothing);
        Assert.True(evaluation.AppliedSpeedPercent < evaluation.RawSpeedPercent);
        Assert.True(evaluation.AppliedSpeedPercent > 30);
    }

    private static FanCurve CreateCurve(params FanCurvePoint[] points)
    {
        return new FanCurve(
            ChannelId: "cpu_fan",
            SensorId: "cpu_temp",
            Points: points,
            HysteresisCelsius: 2,
            SmoothingFactor: 0.5,
            MinimumAllowedPercent: 20,
            MaximumAllowedPercent: 100);
    }
}
