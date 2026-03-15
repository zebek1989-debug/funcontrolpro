namespace FanControlPro.Infrastructure.Onboarding;

public sealed class OnboardingOptions
{
    public string StateFilePath { get; set; } = Path.Combine("data", "onboarding-state.json");
}