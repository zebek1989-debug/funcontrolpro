namespace FanControlPro.Application.Configuration;

public sealed record ApplicationSettingsValidationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    string Message)
{
    public static ApplicationSettingsValidationResult Ok(string message)
        => new(true, Array.Empty<string>(), message);

    public static ApplicationSettingsValidationResult Failed(params string[] errors)
        => new(false, errors, "Validation failed.");
}
