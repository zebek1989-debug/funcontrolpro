namespace FanControlPro.Application.Configuration;

public sealed record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
