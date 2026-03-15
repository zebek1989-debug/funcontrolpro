namespace FanControlPro.Domain.FanControl.Curves;

public sealed record FanCurvePoint(
    double TemperatureCelsius,
    int SpeedPercent);
