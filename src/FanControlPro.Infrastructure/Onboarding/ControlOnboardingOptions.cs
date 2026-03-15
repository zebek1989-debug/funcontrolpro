namespace FanControlPro.Infrastructure.Onboarding;

public sealed class ControlOnboardingOptions
{
    public string ConsentFilePath { get; set; } = Path.Combine("data", "control-consent.json");
}
