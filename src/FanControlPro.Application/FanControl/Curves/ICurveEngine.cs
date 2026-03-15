using FanControlPro.Domain.FanControl.Curves;

namespace FanControlPro.Application.FanControl.Curves;

public interface ICurveEngine
{
    int CalculateSpeedForTemperature(FanCurve curve, double temperatureCelsius);

    CurveValidationResult ValidateCurve(FanCurve curve);

    CurveEvaluationResult Evaluate(
        FanCurve curve,
        double temperatureCelsius,
        CurveEvaluationState state);
}
