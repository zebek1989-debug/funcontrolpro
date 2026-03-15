namespace FanControlPro.Application.FanControl.Curves;

public sealed record CurveEvaluationResult(
    int RawSpeedPercent,
    int AppliedSpeedPercent,
    bool UsedHysteresis,
    bool UsedSmoothing,
    CurveEvaluationState NextState);
