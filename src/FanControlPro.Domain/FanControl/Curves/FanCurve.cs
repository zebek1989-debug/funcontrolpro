namespace FanControlPro.Domain.FanControl.Curves;

public sealed record FanCurve(
    string ChannelId,
    string SensorId,
    IReadOnlyList<FanCurvePoint> Points,
    int HysteresisCelsius = 2,
    double SmoothingFactor = 0.35,
    int MinimumAllowedPercent = 0,
    int MaximumAllowedPercent = 100)
{
    public FanCurve WithPoints(IReadOnlyList<FanCurvePoint> points)
    {
        return this with { Points = points };
    }
}
