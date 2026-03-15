using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanControlPro.Application.FanControl;
using FanControlPro.Application.Onboarding;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;

namespace FanControlPro.Presentation.ViewModels;

public partial class OnboardingViewModel : ObservableObject
{
    private static readonly string[] VendorProcessTokens =
    {
        "armourycrate",
        "aisuite",
        "msicenter",
        "dragoncenter",
        "gigabytecontrolcenter",
        "aorus",
        "fanxpert",
        "icue",
        "nzxtcam"
    };

    private readonly ILogger<OnboardingViewModel> _logger;
    private readonly IOnboardingService _onboardingService;
    private readonly IControlOnboardingService _controlOnboardingService;

    [ObservableProperty]
    private OnboardingStep _currentStep = OnboardingStep.Welcome;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _welcomeMessage = "Witaj w FanControl Pro.\n\n" +
        "Monitoring Only: bezpieczny podglad temperatur i RPM bez zapisu PWM.\n" +
        "Full Control: reczna i automatyczna kontrola wentylatorow (wymaga zgody).\n\n" +
        "Do Full Control na Windows wymagane sa uprawnienia administratora.";

    [ObservableProperty]
    private string _hardwareDetectionMessage = "Wykrywamy dostepne komponenty sprzetowe...";

    [ObservableProperty]
    private bool _isDetectingHardware;

    [ObservableProperty]
    private ObservableCollection<HardwareComponentViewModel> _hardwareComponents = new();

    [ObservableProperty]
    private string _classificationSummary = "Analizujemy poziom wsparcia dla wykrytego sprzetu...";

    [ObservableProperty]
    private string _emptyStateMessage = string.Empty;

    [ObservableProperty]
    private bool _hasFullControlComponents;

    [ObservableProperty]
    private bool _hasMonitoringOnlyComponents;

    [ObservableProperty]
    private bool _hasUnsupportedComponents;

    [ObservableProperty]
    private bool _isRunningAsAdministrator = true;

    [ObservableProperty]
    private bool _hasVendorSoftwareConflict;

    [ObservableProperty]
    private string _vendorConflictDetails = string.Empty;

    [ObservableProperty]
    private string _riskAcceptanceMessage = "Przed wlaczeniem Full Control potwierdz ryzyko.";

    [ObservableProperty]
    private bool _hasAcceptedRisk;

    [ObservableProperty]
    private bool _canProceed;

    [ObservableProperty]
    private string _compatibilityDocumentationUrl = ResolveCompatibilityDocumentationUrl();

    public bool RequiresControlConsent =>
        HasFullControlComponents &&
        IsRunningAsAdministrator &&
        !HasVendorSoftwareConflict;

    public OnboardingViewModel(
        ILogger<OnboardingViewModel> logger,
        IOnboardingService onboardingService,
        IControlOnboardingService controlOnboardingService)
    {
        _logger = logger;
        _onboardingService = onboardingService;
        _controlOnboardingService = controlOnboardingService;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            var state = await _onboardingService.GetStateAsync();
            var controlState = await _controlOnboardingService.GetStateAsync();

            CurrentStep = state.CurrentStep;
            IsCompleted = state.IsCompleted;
            HasAcceptedRisk = controlState.HasAcceptedRisk;
            IsRunningAsAdministrator = DetectAdministratorRights();

            var conflict = DetectVendorSoftwareConflict();
            HasVendorSoftwareConflict = conflict.HasConflict;
            VendorConflictDetails = conflict.Details;

            UpdateRiskAcceptanceMessage();
            UpdateCanProceed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize onboarding");
        }
    }

    [RelayCommand]
    private async Task NextStepAsync()
    {
        try
        {
            if (CurrentStep == OnboardingStep.RiskAcceptance)
            {
                if (RequiresControlConsent && !HasAcceptedRisk)
                {
                    UpdateRiskAcceptanceMessage();
                    UpdateCanProceed();
                    return;
                }

                if (HasAcceptedRisk)
                {
                    await _controlOnboardingService.AcceptRiskAsync(Environment.UserName);
                }
                else
                {
                    await _controlOnboardingService.RevokeRiskAsync(Environment.UserName);
                }
            }

            await _onboardingService.CompleteStepAsync(CurrentStep);

            var newState = await _onboardingService.GetStateAsync();
            CurrentStep = newState.CurrentStep;
            IsCompleted = newState.IsCompleted;

            if (CurrentStep == OnboardingStep.HardwareDetection)
            {
                await PerformHardwareDetectionAsync();
            }
            else if (CurrentStep == OnboardingStep.HardwareClassification)
            {
                await PerformHardwareClassificationAsync();
            }

            UpdateCanProceed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proceed to next onboarding step");
        }
    }

    [RelayCommand]
    private Task PreviousStepAsync()
    {
        CurrentStep = CurrentStep switch
        {
            OnboardingStep.HardwareDetection => OnboardingStep.Welcome,
            OnboardingStep.HardwareClassification => OnboardingStep.HardwareDetection,
            OnboardingStep.RiskAcceptance => OnboardingStep.HardwareClassification,
            OnboardingStep.Completed => OnboardingStep.RiskAcceptance,
            _ => CurrentStep
        };

        UpdateCanProceed();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenCompatibilityDocumentationAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = CompatibilityDocumentationUrl,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open compatibility documentation link");
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RevokeRiskConsentAsync()
    {
        try
        {
            await _controlOnboardingService
                .RevokeRiskAsync(Environment.UserName);

            HasAcceptedRisk = false;
            UpdateRiskAcceptanceMessage();
            UpdateCanProceed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke risk consent");
        }
    }

    private async Task PerformHardwareDetectionAsync()
    {
        IsDetectingHardware = true;
        HardwareDetectionMessage = "Skanowanie sprzetu...";

        try
        {
            await Task.Delay(1200);
            HardwareDetectionMessage = "Wykrywanie zakonczone pomyslnie.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware detection failed");
            HardwareDetectionMessage = "Wykrywanie sprzetu nie powiodlo sie.";
        }
        finally
        {
            IsDetectingHardware = false;
        }
    }

    private async Task PerformHardwareClassificationAsync()
    {
        try
        {
            var result = await _onboardingService.ClassifyHardwareAsync();

            HardwareComponents.Clear();
            foreach (var component in result.Components)
            {
                HardwareComponents.Add(new HardwareComponentViewModel(component));
            }

            HasFullControlComponents = result.HasFullControlComponents;
            HasMonitoringOnlyComponents = result.HasMonitoringOnlyComponents;
            HasUnsupportedComponents = result.HasUnsupportedComponents;

            IsRunningAsAdministrator = DetectAdministratorRights();

            var conflict = DetectVendorSoftwareConflict();
            HasVendorSoftwareConflict = conflict.HasConflict;
            VendorConflictDetails = conflict.Details;

            EmptyStateMessage = BuildEmptyStateMessage(result);
            ClassificationSummary = BuildClassificationSummary(result);

            UpdateRiskAcceptanceMessage();
            UpdateCanProceed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware classification failed");
            ClassificationSummary = "Blad podczas klasyfikacji sprzetu.";
        }
    }

    private string BuildClassificationSummary(HardwareClassificationResult result)
    {
        var summary = "Podsumowanie wykrytego sprzetu:\n";

        if (result.HasFullControlComponents)
        {
            summary += "- Full Control dostepny na czesci kanalow.\n";
        }

        if (result.HasMonitoringOnlyComponents)
        {
            summary += "- Monitoring Only dostepny.\n";
        }

        if (result.HasUnsupportedComponents)
        {
            summary += "- Czesc komponentow nie jest obslugiwana.\n";
        }

        summary += RequiresControlConsent
            ? "\nMozesz wlaczyc Full Control po potwierdzeniu ryzyka."
            : "\nAplikacja pozostanie w trybie Monitoring Only, dopoki warunki Full Control nie beda spelnione.";

        return summary;
    }

    private string BuildEmptyStateMessage(HardwareClassificationResult result)
    {
        var reasons = new List<string>();

        if (!result.HasFullControlComponents)
        {
            reasons.Add("Brak wspieranego kontrolera: Full Control jest niedostepny dla wykrytego sprzetu.");
        }

        if (!IsRunningAsAdministrator)
        {
            reasons.Add("Brak uprawnien administratora: uruchom aplikacje jako administrator, aby odblokowac Full Control.");
        }

        if (HasVendorSoftwareConflict)
        {
            var details = string.IsNullOrWhiteSpace(VendorConflictDetails)
                ? "wykryto aktywne narzedzie producenta"
                : $"wykryto aktywne narzedzia producenta ({VendorConflictDetails})";
            reasons.Add($"Konflikt z oprogramowaniem producenta: {details}.");
        }

        return reasons.Count == 0
            ? string.Empty
            : string.Join("\n", reasons);
    }

    private void UpdateRiskAcceptanceMessage()
    {
        if (!HasFullControlComponents)
        {
            RiskAcceptanceMessage =
                "Tryb Full Control jest niedostepny dla wykrytego sprzetu. Kontynuuj w Monitoring Only.";
            return;
        }

        if (!IsRunningAsAdministrator)
        {
            RiskAcceptanceMessage =
                "Brak uprawnien administratora. Full Control pozostaje zablokowany do czasu uruchomienia aplikacji jako administrator.";
            return;
        }

        if (HasVendorSoftwareConflict)
        {
            var details = string.IsNullOrWhiteSpace(VendorConflictDetails)
                ? "wykryto aktywne narzedzie producenta"
                : $"wykryto aktywne narzedzia producenta: {VendorConflictDetails}";

            RiskAcceptanceMessage =
                $"Wykryto potencjalny konflikt ({details}). Zalecamy zamkniecie tych aplikacji przed wlaczeniem Full Control.";
            return;
        }

        RiskAcceptanceMessage =
            "Wazne ostrzezenie: reczne sterowanie wentylatorami moze doprowadzic do przegrzania sprzetu. " +
            "Potwierdz ryzyko, aby wlaczyc Full Control.";
    }

    partial void OnCurrentStepChanged(OnboardingStep value)
    {
        UpdateCanProceed();
    }

    partial void OnHasAcceptedRiskChanged(bool value)
    {
        UpdateCanProceed();
    }

    partial void OnHasFullControlComponentsChanged(bool value)
    {
        OnPropertyChanged(nameof(RequiresControlConsent));
        UpdateRiskAcceptanceMessage();
        UpdateCanProceed();
    }

    partial void OnIsRunningAsAdministratorChanged(bool value)
    {
        OnPropertyChanged(nameof(RequiresControlConsent));
        UpdateRiskAcceptanceMessage();
        UpdateCanProceed();
    }

    partial void OnHasVendorSoftwareConflictChanged(bool value)
    {
        OnPropertyChanged(nameof(RequiresControlConsent));
        UpdateRiskAcceptanceMessage();
        UpdateCanProceed();
    }

    partial void OnIsDetectingHardwareChanged(bool value)
    {
        UpdateCanProceed();
    }

    private void UpdateCanProceed()
    {
        CanProceed = CurrentStep switch
        {
            OnboardingStep.Welcome => true,
            OnboardingStep.HardwareDetection => !IsDetectingHardware,
            OnboardingStep.HardwareClassification => true,
            OnboardingStep.RiskAcceptance => !RequiresControlConsent || HasAcceptedRisk,
            OnboardingStep.Completed => false,
            _ => false
        };
    }

    private static bool DetectAdministratorRights()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static (bool HasConflict, string Details) DetectVendorSoftwareConflict()
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    var normalizedName = process.ProcessName
                        .Replace(" ", string.Empty, StringComparison.Ordinal)
                        .ToLowerInvariant();

                    if (VendorProcessTokens.Any(token => normalizedName.Contains(token, StringComparison.Ordinal)))
                    {
                        matches.Add(process.ProcessName);
                    }
                }
            }
        }
        catch
        {
            return (false, string.Empty);
        }

        if (matches.Count == 0)
        {
            return (false, string.Empty);
        }

        var details = string.Join(", ", matches.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(4));
        return (true, details);
    }

    private static string ResolveCompatibilityDocumentationUrl()
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "supported-hardware.md");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return "https://github.com/FanControlPro/supported-hardware";
    }
}
