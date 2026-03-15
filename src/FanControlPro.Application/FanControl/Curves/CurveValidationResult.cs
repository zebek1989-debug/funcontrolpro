namespace FanControlPro.Application.FanControl.Curves;

public sealed record CurveValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static CurveValidationResult Valid { get; } = new(true, Array.Empty<string>());

    public static CurveValidationResult Invalid(params string[] errors) =>
        new(false, errors);
}
