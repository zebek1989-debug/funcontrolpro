namespace FanControlPro.Application.FanControl.Curves;

public sealed record CurveEvaluationState(
    double? LastTemperatureCelsius,
    int? LastAppliedSpeedPercent)
{
    public static CurveEvaluationState Empty { get; } = new(null, null);
}
