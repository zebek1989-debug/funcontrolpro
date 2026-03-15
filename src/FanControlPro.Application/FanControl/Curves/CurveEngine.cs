using FanControlPro.Domain.FanControl.Curves;

namespace FanControlPro.Application.FanControl.Curves;

public sealed class CurveEngine : ICurveEngine
{
    public int CalculateSpeedForTemperature(FanCurve curve, double temperatureCelsius)
    {
        var validation = ValidateCurve(curve);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid curve: {string.Join("; ", validation.Errors)}");
        }

        var ordered = curve.Points.OrderBy(point => point.TemperatureCelsius).ToArray();

        if (temperatureCelsius <= ordered[0].TemperatureCelsius)
        {
            return ClampToCurveRange(ordered[0].SpeedPercent, curve);
        }

        if (temperatureCelsius >= ordered[^1].TemperatureCelsius)
        {
            return ClampToCurveRange(ordered[^1].SpeedPercent, curve);
        }

        for (var i = 0; i < ordered.Length - 1; i++)
        {
            var left = ordered[i];
            var right = ordered[i + 1];

            if (temperatureCelsius < left.TemperatureCelsius || temperatureCelsius > right.TemperatureCelsius)
            {
                continue;
            }

            var temperatureSpan = right.TemperatureCelsius - left.TemperatureCelsius;
            if (temperatureSpan <= 0)
            {
                return ClampToCurveRange(left.SpeedPercent, curve);
            }

            var ratio = (temperatureCelsius - left.TemperatureCelsius) / temperatureSpan;
            var interpolated = left.SpeedPercent + (right.SpeedPercent - left.SpeedPercent) * ratio;

            return ClampToCurveRange((int)Math.Round(interpolated, MidpointRounding.AwayFromZero), curve);
        }

        return ClampToCurveRange(ordered[^1].SpeedPercent, curve);
    }

    public CurveValidationResult ValidateCurve(FanCurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(curve.ChannelId))
        {
            errors.Add("ChannelId is required.");
        }

        if (string.IsNullOrWhiteSpace(curve.SensorId))
        {
            errors.Add("SensorId is required.");
        }

        if (curve.Points is null)
        {
            errors.Add("Curve points are required.");
            return new CurveValidationResult(false, errors);
        }

        if (curve.Points.Count < 4 || curve.Points.Count > 8)
        {
            errors.Add("Curve must contain between 4 and 8 points.");
        }

        if (curve.HysteresisCelsius is < 1 or > 10)
        {
            errors.Add("Hysteresis must be between 1 and 10 C.");
        }

        if (curve.SmoothingFactor is < 0 or > 1)
        {
            errors.Add("Smoothing factor must be between 0 and 1.");
        }

        if (curve.MinimumAllowedPercent < 0 || curve.MinimumAllowedPercent > 100)
        {
            errors.Add("Minimum allowed speed must be between 0 and 100.");
        }

        if (curve.MaximumAllowedPercent < 0 || curve.MaximumAllowedPercent > 100)
        {
            errors.Add("Maximum allowed speed must be between 0 and 100.");
        }

        if (curve.MinimumAllowedPercent > curve.MaximumAllowedPercent)
        {
            errors.Add("Minimum allowed speed cannot exceed maximum allowed speed.");
        }

        FanCurvePoint? previous = null;
        for (var i = 0; i < curve.Points.Count; i++)
        {
            var point = curve.Points[i];

            if (point.TemperatureCelsius < 0 || point.TemperatureCelsius > 100)
            {
                errors.Add($"Point {i + 1}: temperature must be within 0-100 C.");
            }

            if (point.SpeedPercent < curve.MinimumAllowedPercent || point.SpeedPercent > curve.MaximumAllowedPercent)
            {
                errors.Add(
                    $"Point {i + 1}: speed must be within {curve.MinimumAllowedPercent}-{curve.MaximumAllowedPercent}%.");
            }

            if (previous is not null)
            {
                if (point.TemperatureCelsius <= previous.TemperatureCelsius)
                {
                    errors.Add($"Point {i + 1}: temperatures must strictly increase.");
                }

                if (point.SpeedPercent < previous.SpeedPercent)
                {
                    errors.Add($"Point {i + 1}: speed cannot decrease relative to previous point.");
                }
            }

            previous = point;
        }

        return errors.Count == 0
            ? CurveValidationResult.Valid
            : new CurveValidationResult(false, errors);
    }

    public CurveEvaluationResult Evaluate(
        FanCurve curve,
        double temperatureCelsius,
        CurveEvaluationState state)
    {
        var raw = CalculateSpeedForTemperature(curve, temperatureCelsius);

        var usedHysteresis = false;
        var usedSmoothing = false;
        var applied = raw;

        if (state.LastTemperatureCelsius is not null && state.LastAppliedSpeedPercent is not null)
        {
            var delta = Math.Abs(temperatureCelsius - state.LastTemperatureCelsius.Value);

            if (delta < curve.HysteresisCelsius)
            {
                applied = state.LastAppliedSpeedPercent.Value;
                usedHysteresis = true;
            }
            else if (curve.SmoothingFactor > 0)
            {
                var smoothed = state.LastAppliedSpeedPercent.Value +
                    ((raw - state.LastAppliedSpeedPercent.Value) * curve.SmoothingFactor);

                applied = ClampToCurveRange((int)Math.Round(smoothed, MidpointRounding.AwayFromZero), curve);
                usedSmoothing = applied != raw;
            }
        }

        var nextState = new CurveEvaluationState(temperatureCelsius, applied);

        return new CurveEvaluationResult(
            RawSpeedPercent: raw,
            AppliedSpeedPercent: applied,
            UsedHysteresis: usedHysteresis,
            UsedSmoothing: usedSmoothing,
            NextState: nextState);
    }

    private static int ClampToCurveRange(int percent, FanCurve curve)
    {
        return Math.Clamp(percent, curve.MinimumAllowedPercent, curve.MaximumAllowedPercent);
    }
}
