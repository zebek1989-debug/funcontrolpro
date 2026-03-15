using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Curves;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Application.FanControl.Safety;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Curves;
using FanControlPro.Domain.FanControl.Profiles;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace FanControlPro.Presentation.ViewModels;

public class DashboardViewModel : ObservableObject
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IControlOnboardingService _onboardingService;
    private readonly IManualFanControlService _manualFanControlService;
    private readonly IFanCurveService _fanCurveService;
    private readonly IProfileService _profileService;
    private readonly ISafetyMonitorServiceV2 _safetyMonitorService;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly ISupportBundleService _supportBundleService;
    private readonly IApplicationSettingsService _applicationSettingsService;

    private string _statusMessage = "Starting up...";
    private string _activeProfile = "Balanced";
    private bool _isMonitoring;
    private bool _isRiskConfirmationChecked;
    private bool _hasAcceptedControlRisk;
    private string _controlOnboardingMessage =
        "Przed wejściem w tryb kontroli potwierdź, że rozumiesz ryzyko ręcznego sterowania wentylatorami.";

    private string _selectedCurveChannelId = string.Empty;
    private string _selectedCurveSensorId = "cpu_temp";
    private int _curveHysteresisCelsius = 2;
    private double _curveSmoothingFactor = 0.35;
    private double _curvePreviewTemperatureCelsius = 55;
    private int _curvePreviewSpeedPercent;
    private string _curveValidationMessage = "Krzywa niezaładowana.";
    private string _curveTestModeMessage = "";
    private bool _curveTestConfirmLowCpu;
    private double _newCurvePointTemperatureCelsius = 65;
    private int _newCurvePointSpeedPercent = 60;
    private bool _isProfileSystemReady;
    private bool _suppressProfileActivation;
    private string _safetyState = "Caution";
    private string _safetyStatusMessage = "Safety monitor waiting for telemetry.";
    private string _safetyAlertsSummary = "Brak alertów temperatury.";
    private string _supportBundleStatusMessage = "Brak wyeksportowanego pakietu.";
    private string _lastSupportBundlePath = string.Empty;
    private int _settingsPollingIntervalSeconds = 1;
    private int _settingsCpuAlertThresholdCelsius = 80;
    private int _settingsGpuAlertThresholdCelsius = 82;
    private string _selectedTheme = ApplicationTheme.System.ToString();
    private bool _settingsEnableAutostart;
    private bool _settingsStartMinimizedToTray = true;
    private bool _settingsMinimizeToTrayOnClose = true;
    private int _settingsStartupDelaySeconds = 30;
    private string _settingsDefaultProfile = "Balanced";
    private string _settingsStatusMessage = "Ustawienia niezaładowane.";

    public DashboardViewModel(
        ILogger<DashboardViewModel> logger,
        IControlOnboardingService onboardingService,
        IManualFanControlService manualFanControlService,
        IFanCurveService fanCurveService,
        IProfileService profileService,
        ISafetyMonitorServiceV2 safetyMonitorService,
        IDiagnosticsService diagnosticsService,
        ISupportBundleService supportBundleService,
        IApplicationSettingsService applicationSettingsService)
    {
        _logger = logger;
        _onboardingService = onboardingService;
        _manualFanControlService = manualFanControlService;
        _fanCurveService = fanCurveService;
        _profileService = profileService;
        _safetyMonitorService = safetyMonitorService;
        _diagnosticsService = diagnosticsService;
        _supportBundleService = supportBundleService;
        _applicationSettingsService = applicationSettingsService;

        Sensors = new ObservableCollection<SensorViewModel>();
        FanChannels = new ObservableCollection<FanChannelViewModel>();
        SystemLoad = new SystemLoadViewModel();
        ProfileOptions = new ObservableCollection<string>();
        RecentDiagnostics = new ObservableCollection<string>();
        ThemeOptions = new ObservableCollection<string>(Enum.GetNames<ApplicationTheme>());
        PollingIntervalOptions = new ObservableCollection<int>(Enumerable.Range(1, 5));
        TemperatureThresholdOptions = new ObservableCollection<int>(Enumerable.Range(50, 61));
        StartupDelayOptions = new ObservableCollection<int>(new[] { 0, 15, 30, 45, 60, 120, 180, 300, 600 });

        CurveChannelOptions = new ObservableCollection<string>();
        CurvePoints = new ObservableCollection<FanCurvePointViewModel>();

        LoadHardwareCommand = new AsyncRelayCommand(LoadHardwareAsync);
        ConfirmRiskConsentCommand = new AsyncRelayCommand(ConfirmRiskConsentAsync);
        EmergencyFullSpeedCommand = new AsyncRelayCommand(EmergencyFullSpeedAsync);
        RefreshFanControlCommand = new AsyncRelayCommand(RefreshFanChannelsAsync);

        AddCurvePointCommand = new RelayCommand(AddCurvePoint);
        RemoveCurvePointCommand = new RelayCommand<FanCurvePointViewModel>(RemoveCurvePoint);
        SaveCurveCommand = new AsyncRelayCommand(SaveCurveAsync);
        PreviewCurveCommand = new AsyncRelayCommand(PreviewCurveAsync);
        RunCurveTestModeCommand = new AsyncRelayCommand(RunCurveTestModeAsync);
        ResetCurveDefaultsCommand = new AsyncRelayCommand(ResetCurveDefaultsAsync);
        SaveCustomProfileCommand = new AsyncRelayCommand(SaveCustomProfileAsync);
        RefreshDiagnosticsCommand = new AsyncRelayCommand(RefreshDiagnosticsAsync);
        ExportSupportBundleCommand = new AsyncRelayCommand(ExportSupportBundleAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);

        InitializeMockTelemetryData();

        _safetyMonitorService.HealthAttestationChanged += OnHealthAttestationChanged;

        _ = LoadOnboardingStateAsync();
        _ = RefreshFanChannelsAsync();
        _ = LoadProfilesAsync();
        _ = InitializeSafetyMonitorAsync();
        _ = RefreshDiagnosticsAsync();
        _ = LoadSettingsAsync();
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (SetProperty(ref _activeProfile, value) &&
                _isProfileSystemReady &&
                !_suppressProfileActivation)
            {
                _ = ActivateSelectedProfileAsync(value);
            }
        }
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetProperty(ref _isMonitoring, value);
    }

    public bool IsRiskConfirmationChecked
    {
        get => _isRiskConfirmationChecked;
        set => SetProperty(ref _isRiskConfirmationChecked, value);
    }

    public bool HasAcceptedControlRisk
    {
        get => _hasAcceptedControlRisk;
        private set
        {
            if (SetProperty(ref _hasAcceptedControlRisk, value))
            {
                OnPropertyChanged(nameof(IsControlOnboardingRequired));
            }
        }
    }

    public bool IsControlOnboardingRequired => !HasAcceptedControlRisk;

    public string ControlOnboardingMessage
    {
        get => _controlOnboardingMessage;
        private set => SetProperty(ref _controlOnboardingMessage, value);
    }

    public ObservableCollection<SensorViewModel> Sensors { get; }

    public ObservableCollection<FanChannelViewModel> FanChannels { get; }

    public SystemLoadViewModel SystemLoad { get; }

    public ObservableCollection<string> ProfileOptions { get; }

    public ObservableCollection<string> CurveChannelOptions { get; }

    public ObservableCollection<FanCurvePointViewModel> CurvePoints { get; }

    public string SafetyState
    {
        get => _safetyState;
        private set => SetProperty(ref _safetyState, value);
    }

    public string SafetyStatusMessage
    {
        get => _safetyStatusMessage;
        private set => SetProperty(ref _safetyStatusMessage, value);
    }

    public string SafetyAlertsSummary
    {
        get => _safetyAlertsSummary;
        private set => SetProperty(ref _safetyAlertsSummary, value);
    }

    public string SupportBundleStatusMessage
    {
        get => _supportBundleStatusMessage;
        private set => SetProperty(ref _supportBundleStatusMessage, value);
    }

    public string LastSupportBundlePath
    {
        get => _lastSupportBundlePath;
        private set => SetProperty(ref _lastSupportBundlePath, value);
    }

    public ObservableCollection<string> RecentDiagnostics { get; }

    public ObservableCollection<string> ThemeOptions { get; }

    public ObservableCollection<int> PollingIntervalOptions { get; }

    public ObservableCollection<int> TemperatureThresholdOptions { get; }

    public ObservableCollection<int> StartupDelayOptions { get; }

    public int SettingsPollingIntervalSeconds
    {
        get => _settingsPollingIntervalSeconds;
        set => SetProperty(ref _settingsPollingIntervalSeconds, value);
    }

    public int SettingsCpuAlertThresholdCelsius
    {
        get => _settingsCpuAlertThresholdCelsius;
        set => SetProperty(ref _settingsCpuAlertThresholdCelsius, value);
    }

    public int SettingsGpuAlertThresholdCelsius
    {
        get => _settingsGpuAlertThresholdCelsius;
        set => SetProperty(ref _settingsGpuAlertThresholdCelsius, value);
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public bool SettingsEnableAutostart
    {
        get => _settingsEnableAutostart;
        set => SetProperty(ref _settingsEnableAutostart, value);
    }

    public bool SettingsStartMinimizedToTray
    {
        get => _settingsStartMinimizedToTray;
        set => SetProperty(ref _settingsStartMinimizedToTray, value);
    }

    public bool SettingsMinimizeToTrayOnClose
    {
        get => _settingsMinimizeToTrayOnClose;
        set => SetProperty(ref _settingsMinimizeToTrayOnClose, value);
    }

    public int SettingsStartupDelaySeconds
    {
        get => _settingsStartupDelaySeconds;
        set => SetProperty(ref _settingsStartupDelaySeconds, value);
    }

    public string SettingsDefaultProfile
    {
        get => _settingsDefaultProfile;
        set => SetProperty(ref _settingsDefaultProfile, value);
    }

    public string SettingsStatusMessage
    {
        get => _settingsStatusMessage;
        private set => SetProperty(ref _settingsStatusMessage, value);
    }

    public string SelectedCurveChannelId
    {
        get => _selectedCurveChannelId;
        set
        {
            if (SetProperty(ref _selectedCurveChannelId, value))
            {
                _ = LoadCurveForSelectedChannelAsync();
            }
        }
    }

    public string SelectedCurveSensorId
    {
        get => _selectedCurveSensorId;
        private set => SetProperty(ref _selectedCurveSensorId, value);
    }

    public int CurveHysteresisCelsius
    {
        get => _curveHysteresisCelsius;
        set => SetProperty(ref _curveHysteresisCelsius, value);
    }

    public double CurveSmoothingFactor
    {
        get => _curveSmoothingFactor;
        set => SetProperty(ref _curveSmoothingFactor, value);
    }

    public double CurvePreviewTemperatureCelsius
    {
        get => _curvePreviewTemperatureCelsius;
        set => SetProperty(ref _curvePreviewTemperatureCelsius, value);
    }

    public int CurvePreviewSpeedPercent
    {
        get => _curvePreviewSpeedPercent;
        private set => SetProperty(ref _curvePreviewSpeedPercent, value);
    }

    public string CurveValidationMessage
    {
        get => _curveValidationMessage;
        private set => SetProperty(ref _curveValidationMessage, value);
    }

    public string CurveTestModeMessage
    {
        get => _curveTestModeMessage;
        private set => SetProperty(ref _curveTestModeMessage, value);
    }

    public bool CurveTestConfirmLowCpu
    {
        get => _curveTestConfirmLowCpu;
        set => SetProperty(ref _curveTestConfirmLowCpu, value);
    }

    public double NewCurvePointTemperatureCelsius
    {
        get => _newCurvePointTemperatureCelsius;
        set => SetProperty(ref _newCurvePointTemperatureCelsius, value);
    }

    public int NewCurvePointSpeedPercent
    {
        get => _newCurvePointSpeedPercent;
        set => SetProperty(ref _newCurvePointSpeedPercent, value);
    }

    public IAsyncRelayCommand LoadHardwareCommand { get; }

    public IAsyncRelayCommand ConfirmRiskConsentCommand { get; }

    public IAsyncRelayCommand EmergencyFullSpeedCommand { get; }

    public IAsyncRelayCommand RefreshFanControlCommand { get; }

    public IRelayCommand AddCurvePointCommand { get; }

    public IRelayCommand<FanCurvePointViewModel> RemoveCurvePointCommand { get; }

    public IAsyncRelayCommand SaveCurveCommand { get; }

    public IAsyncRelayCommand PreviewCurveCommand { get; }

    public IAsyncRelayCommand RunCurveTestModeCommand { get; }

    public IAsyncRelayCommand ResetCurveDefaultsCommand { get; }

    public IAsyncRelayCommand SaveCustomProfileCommand { get; }

    public IAsyncRelayCommand RefreshDiagnosticsCommand { get; }

    public IAsyncRelayCommand ExportSupportBundleCommand { get; }

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ResetSettingsCommand { get; }

    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            var events = await _diagnosticsService.GetRecentEventsAsync(maxEvents: 20);

            RecentDiagnostics.Clear();
            foreach (var evt in events.OrderByDescending(item => item.TimestampUtc))
            {
                RecentDiagnostics.Add($"{evt.TimestampUtc:yyyy-MM-dd HH:mm:ss} [{evt.Level}] {evt.Message}");
            }

            if (RecentDiagnostics.Count == 0)
            {
                RecentDiagnostics.Add("Brak zdarzeń diagnostycznych.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh diagnostics timeline");
            RecentDiagnostics.Clear();
            RecentDiagnostics.Add("Nie udało się odczytać diagnostyki.");
        }
    }

    private async Task ExportSupportBundleAsync()
    {
        try
        {
            var result = await _supportBundleService.ExportSupportBundleAsync();
            if (result.Success)
            {
                LastSupportBundlePath = result.BundlePath;
                SupportBundleStatusMessage =
                    $"✓ Wyeksportowano pakiet ({result.IncludedFileCount} plików): {result.BundlePath}";
                StatusMessage = "✓ Support bundle został wyeksportowany.";
                await RefreshDiagnosticsAsync();
            }
            else
            {
                SupportBundleStatusMessage = "✗ " + result.Message;
                StatusMessage = "✗ Nie udało się wyeksportować support bundle.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export support bundle");
            SupportBundleStatusMessage = "✗ Błąd eksportu support bundle.";
            StatusMessage = "✗ Błąd podczas eksportu support bundle.";
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _applicationSettingsService.GetCurrentAsync();
            ApplySettingsToEditor(settings);
            SettingsStatusMessage = "✓ Ustawienia załadowane.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application settings");
            SettingsStatusMessage = "✗ Nie udało się załadować ustawień.";
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (!Enum.TryParse<ApplicationTheme>(SelectedTheme, ignoreCase: true, out var theme))
        {
            SettingsStatusMessage = "✗ Niepoprawny motyw aplikacji.";
            return;
        }

        var selectedDefaultProfile = string.IsNullOrWhiteSpace(SettingsDefaultProfile)
            ? ActiveProfile
            : SettingsDefaultProfile.Trim();

        var candidate = new ApplicationSettings(
            PollingIntervalSeconds: SettingsPollingIntervalSeconds,
            CpuAlertThresholdCelsius: SettingsCpuAlertThresholdCelsius,
            GpuAlertThresholdCelsius: SettingsGpuAlertThresholdCelsius,
            Theme: theme,
            EnableAutostart: SettingsEnableAutostart,
            StartMinimizedToTray: SettingsStartMinimizedToTray,
            MinimizeToTrayOnClose: SettingsMinimizeToTrayOnClose,
            StartupDelaySeconds: SettingsStartupDelaySeconds,
            DefaultProfileName: selectedDefaultProfile);

        var result = await _applicationSettingsService.SaveAsync(candidate);
        if (!result.Success)
        {
            SettingsStatusMessage = "✗ " + string.Join(" | ", result.Errors);
            StatusMessage = "✗ Ustawienia nie zostały zapisane.";
            return;
        }

        ApplySettingsToEditor(_applicationSettingsService.Current);
        SettingsStatusMessage = "✓ Ustawienia zapisane i zastosowane bez restartu.";
        StatusMessage = "✓ Zapisano ustawienia aplikacji.";
    }

    private async Task ResetSettingsAsync()
    {
        try
        {
            var settings = await _applicationSettingsService.ResetToDefaultsAsync();
            ApplySettingsToEditor(settings);
            SettingsStatusMessage = "✓ Przywrócono ustawienia domyślne.";
            StatusMessage = "✓ Ustawienia domyślne aktywne.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset application settings");
            SettingsStatusMessage = "✗ Nie udało się przywrócić ustawień domyślnych.";
            StatusMessage = "✗ Reset ustawień nie powiódł się.";
        }
    }

    private void ApplySettingsToEditor(ApplicationSettings settings)
    {
        SettingsPollingIntervalSeconds = settings.PollingIntervalSeconds;
        SettingsCpuAlertThresholdCelsius = (int)Math.Round(settings.CpuAlertThresholdCelsius, MidpointRounding.AwayFromZero);
        SettingsGpuAlertThresholdCelsius = (int)Math.Round(settings.GpuAlertThresholdCelsius, MidpointRounding.AwayFromZero);
        SelectedTheme = settings.Theme.ToString();
        SettingsEnableAutostart = settings.EnableAutostart;
        SettingsStartMinimizedToTray = settings.StartMinimizedToTray;
        SettingsMinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        SettingsStartupDelaySeconds = settings.StartupDelaySeconds;
        SettingsDefaultProfile = settings.DefaultProfileName;
    }

    private async Task InitializeSafetyMonitorAsync()
    {
        try
        {
            var attestation = await _safetyMonitorService.GetHealthAttestationAsync();
            ApplySafetyAttestation(attestation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize safety monitor");
            SafetyState = "Caution";
            SafetyStatusMessage = "✗ Safety monitor initialization failed.";
            SafetyAlertsSummary = "Brak danych alertów.";
        }
    }

    private void OnHealthAttestationChanged(object? sender, HealthAttestation attestation)
    {
        Dispatcher.UIThread.Post(() => ApplySafetyAttestation(attestation));
    }

    private void ApplySafetyAttestation(HealthAttestation attestation)
    {
        SafetyState = attestation.Level.ToString();
        SafetyStatusMessage = attestation.Message;

        if (attestation.TemperatureAlerts.Count == 0)
        {
            SafetyAlertsSummary = "Brak alertów temperatury.";
        }
        else
        {
            SafetyAlertsSummary = string.Join(
                " | ",
                attestation.TemperatureAlerts.Select(alert =>
                    $"{alert.SensorId}: {alert.CurrentCelsius:F1}C (>{alert.ThresholdCelsius:F1}C)"));
        }

        if (attestation.Level is SafetyLevel.Emergency or SafetyLevel.Shutdown)
        {
            StatusMessage = $"⚠ {attestation.Message}";
        }
    }

    private async Task LoadOnboardingStateAsync()
    {
        try
        {
            var state = await _onboardingService.GetStateAsync();
            HasAcceptedControlRisk = state.HasAcceptedRisk;

            if (HasAcceptedControlRisk)
            {
                ControlOnboardingMessage =
                    $"Tryb kontroli odblokowany ({state.AcceptedAtUtc:yyyy-MM-dd HH:mm}, {state.AcceptedBy}).";
            }
            else
            {
                ControlOnboardingMessage = "Tryb kontroli jest zablokowany do czasu potwierdzenia ryzyka.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load onboarding state");
            ControlOnboardingMessage = "Nie udało się odczytać stanu zgody. Kontrola pozostaje zablokowana.";
            HasAcceptedControlRisk = false;
        }
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            var profiles = await _profileService.GetProfilesAsync();

            ProfileOptions.Clear();
            foreach (var profileName in profiles.Select(profile => profile.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                ProfileOptions.Add(profileName);
            }

            var activeProfile = await _profileService.GetActiveProfileAsync();
            var appSettings = await _applicationSettingsService.GetCurrentAsync();
            var startupProfile = appSettings.DefaultProfileName;
            if (!string.IsNullOrWhiteSpace(startupProfile))
            {
                SettingsDefaultProfile = startupProfile;
            }

            var preferredProfile = !string.IsNullOrWhiteSpace(startupProfile)
                ? ProfileOptions.FirstOrDefault(name =>
                    string.Equals(name, startupProfile, StringComparison.OrdinalIgnoreCase))
                : null;

            _suppressProfileActivation = true;
            try
            {
                ActiveProfile =
                    preferredProfile
                    ?? activeProfile?.Name
                    ?? ProfileOptions.FirstOrDefault()
                    ?? "Balanced";
            }
            finally
            {
                _suppressProfileActivation = false;
            }

            _isProfileSystemReady = true;

            if (!string.IsNullOrWhiteSpace(ActiveProfile))
            {
                await ActivateSelectedProfileAsync(ActiveProfile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profiles");
            StatusMessage = "✗ Nie udało się załadować profili.";
        }
    }

    private async Task ActivateSelectedProfileAsync(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        try
        {
            var activation = await _profileService.ActivateProfileAsync(profileName);
            if (activation.Success)
            {
                StatusMessage = $"✓ Profil '{activation.ProfileName}' aktywny.";
            }
            else
            {
                var details = activation.Errors.Count > 0
                    ? " " + string.Join(" | ", activation.Errors)
                    : string.Empty;
                StatusMessage = $"⚠ {activation.Message}{details}";
            }

            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate profile {Profile}", profileName);
            StatusMessage = $"✗ Nie udało się aktywować profilu '{profileName}'.";
        }
    }

    private async Task SaveCustomProfileAsync()
    {
        try
        {
            var settings = FanChannels
                .Select(channel => new FanProfileChannelSetting(channel.Id, channel.CurrentPercent))
                .ToArray();

            if (settings.Length == 0)
            {
                StatusMessage = "⚠ Brak kanałów do zapisania profilu Custom.";
                return;
            }

            var profile = new FanProfile(
                Name: "Custom",
                Description: "User custom profile saved from current fan state",
                IsPredefined: false,
                ChannelSettings: settings,
                UpdatedAtUtc: DateTimeOffset.UtcNow);

            await _profileService.SaveProfileAsync(profile);

            if (!ProfileOptions.Contains("Custom", StringComparer.OrdinalIgnoreCase))
            {
                ProfileOptions.Add("Custom");
            }

            _suppressProfileActivation = true;
            try
            {
                ActiveProfile = "Custom";
            }
            finally
            {
                _suppressProfileActivation = false;
            }

            await ActivateSelectedProfileAsync("Custom");
            StatusMessage = "✓ Zapisano profil Custom i aktywowano go.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save custom profile");
            StatusMessage = "✗ Nie udało się zapisać profilu Custom.";
        }
    }

    private void InitializeMockTelemetryData()
    {
        _logger.LogInformation("Initializing telemetry mock data");

        Sensors.Clear();
        Sensors.Add(new SensorViewModel
        {
            Id = "cpu_temp",
            Label = "CPU Temperature",
            Type = "Temperature",
            CurrentValue = 45.5,
            MinValue = 32.0,
            MaxValue = 85.0
        });

        Sensors.Add(new SensorViewModel
        {
            Id = "gpu_temp",
            Label = "GPU Temperature",
            Type = "Temperature",
            CurrentValue = 52.3,
            MinValue = 30.0,
            MaxValue = 90.0
        });

        Sensors.Add(new SensorViewModel
        {
            Id = "mb_temp",
            Label = "Motherboard Temperature",
            Type = "Temperature",
            CurrentValue = 38.1,
            MinValue = 25.0,
            MaxValue = 75.0
        });

        SystemLoad.CpuLoad = 25.5;
        SystemLoad.GpuLoad = 10.0;
        SystemLoad.RamUsage = 7.2;

        StatusMessage = "✓ Hardware detected. Monitoring enabled.";
        IsMonitoring = true;
    }

    private async Task ConfirmRiskConsentAsync()
    {
        if (!IsRiskConfirmationChecked)
        {
            StatusMessage = "⚠ Zaznacz checkbox \"Rozumiem konsekwencje\" przed odblokowaniem trybu kontroli.";
            return;
        }

        try
        {
            var acceptedBy = Environment.UserName;
            var state = await _onboardingService.AcceptRiskAsync(acceptedBy);

            HasAcceptedControlRisk = state.HasAcceptedRisk;
            ControlOnboardingMessage =
                $"Tryb kontroli odblokowany ({state.AcceptedAtUtc:yyyy-MM-dd HH:mm}, {state.AcceptedBy}).";
            StatusMessage = "✓ Potwierdzono ryzyko. Możesz wejść w tryb kontroli wentylatorów.";
            _logger.LogInformation("Fan control risk consent accepted by {User}", state.AcceptedBy);

            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist risk consent");
            StatusMessage = "✗ Nie udało się zapisać potwierdzenia ryzyka.";
        }
    }

    private async Task RefreshFanChannelsAsync()
    {
        try
        {
            var snapshots = await _manualFanControlService.GetChannelsAsync();

            FanChannels.Clear();
            foreach (var snapshot in snapshots)
            {
                var channelVm = new FanChannelViewModel(
                    applyAsync: ApplyFanSpeedAsync,
                    resetAsync: ResetFanSpeedAsync,
                    fullSpeedAsync: FullSpeedFanAsync,
                    assignGroupAsync: AssignGroupAsync)
                {
                    Id = snapshot.Id,
                    Label = snapshot.Name,
                    Type = snapshot.Type,
                    CurrentRpm = snapshot.CurrentRpm,
                    CurrentPercent = snapshot.CurrentPercent,
                    RequestedPercent = snapshot.CurrentPercent,
                    Status = snapshot.Status,
                    CanControl = snapshot.CanControl,
                    IsCpuChannel = snapshot.IsCpuChannel,
                    MinimumPercent = snapshot.MinimumPercent,
                    MaximumPercent = snapshot.MaximumPercent,
                    AssignedGroup = snapshot.AssignedGroup,
                    AvailableGroups = _manualFanControlService.AvailableGroups
                };

                FanChannels.Add(channelVm);
            }

            UpdateCurveChannelOptions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh fan channels");
            StatusMessage = "✗ Nie udało się odświeżyć listy kanałów wentylatorów.";
        }
    }

    private void UpdateCurveChannelOptions()
    {
        var ids = FanChannels.Select(channel => channel.Id).ToArray();

        CurveChannelOptions.Clear();
        foreach (var id in ids)
        {
            CurveChannelOptions.Add(id);
        }

        if (CurveChannelOptions.Count == 0)
        {
            SelectedCurveChannelId = string.Empty;
            CurvePoints.Clear();
            CurveValidationMessage = "Brak kanałów do edycji krzywej.";
            return;
        }

        if (!CurveChannelOptions.Contains(SelectedCurveChannelId, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCurveChannelId = CurveChannelOptions[0];
        }
        else
        {
            _ = LoadCurveForSelectedChannelAsync();
        }
    }

    private async Task LoadCurveForSelectedChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCurveChannelId))
        {
            return;
        }

        var channel = FanChannels.FirstOrDefault(item =>
            string.Equals(item.Id, SelectedCurveChannelId, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
        {
            return;
        }

        try
        {
            var sensorId = ResolveSensorIdForChannel(channel.Id);
            var curve = await _fanCurveService.GetOrCreateCurveAsync(
                channel.Id,
                sensorId,
                channel.IsCpuChannel);

            SelectedCurveSensorId = curve.SensorId;
            CurveHysteresisCelsius = curve.HysteresisCelsius;
            CurveSmoothingFactor = curve.SmoothingFactor;
            CurvePoints.Clear();
            foreach (var point in curve.Points.OrderBy(point => point.TemperatureCelsius))
            {
                CurvePoints.Add(new FanCurvePointViewModel
                {
                    TemperatureCelsius = point.TemperatureCelsius,
                    SpeedPercent = point.SpeedPercent
                });
            }

            CurveValidationMessage = $"Załadowano krzywą dla {channel.Label}.";
            await PreviewCurveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load curve for channel {ChannelId}", channel.Id);
            CurveValidationMessage = "✗ Nie udało się załadować krzywej.";
        }
    }

    private void AddCurvePoint()
    {
        if (CurvePoints.Count >= 8)
        {
            CurveValidationMessage = "✗ Maksymalna liczba punktów to 8.";
            return;
        }

        CurvePoints.Add(new FanCurvePointViewModel
        {
            TemperatureCelsius = NewCurvePointTemperatureCelsius,
            SpeedPercent = NewCurvePointSpeedPercent
        });

        SortCurvePoints();
        CurveValidationMessage = "Dodano punkt kontrolny. Zapisz krzywą, aby utrwalić.";
    }

    private void RemoveCurvePoint(FanCurvePointViewModel? point)
    {
        if (point is null)
        {
            return;
        }

        CurvePoints.Remove(point);
        CurveValidationMessage = "Usunięto punkt. Zapisz krzywą, aby utrwalić.";
    }

    private async Task SaveCurveAsync()
    {
        var curve = BuildCurveFromEditor();
        if (curve is null)
        {
            return;
        }

        var validation = await _fanCurveService.SaveCurveAsync(curve);
        if (!validation.IsValid)
        {
            CurveValidationMessage = "✗ " + string.Join(" | ", validation.Errors);
            return;
        }

        CurveValidationMessage = "✓ Krzywa zapisana poprawnie.";
        await PreviewCurveAsync();
    }

    private async Task PreviewCurveAsync()
    {
        var curve = BuildCurveFromEditor();
        if (curve is null)
        {
            return;
        }

        var validation = await _fanCurveService.SaveCurveAsync(curve);
        if (!validation.IsValid)
        {
            CurveValidationMessage = "✗ " + string.Join(" | ", validation.Errors);
            return;
        }

        var channel = GetSelectedCurveChannel();
        if (channel is null)
        {
            return;
        }

        var evaluation = await _fanCurveService.PreviewAsync(
            curve.ChannelId,
            curve.SensorId,
            channel.IsCpuChannel,
            CurvePreviewTemperatureCelsius);

        CurvePreviewSpeedPercent = evaluation.AppliedSpeedPercent;
        CurveTestModeMessage =
            $"Preview: {CurvePreviewTemperatureCelsius:F1}C => {evaluation.AppliedSpeedPercent}% (raw: {evaluation.RawSpeedPercent}%, " +
            $"hysteresis: {(evaluation.UsedHysteresis ? "on" : "off")}, smoothing: {(evaluation.UsedSmoothing ? "on" : "off")}).";
    }

    private async Task RunCurveTestModeAsync()
    {
        var curve = BuildCurveFromEditor();
        if (curve is null)
        {
            return;
        }

        var channel = GetSelectedCurveChannel();
        if (channel is null)
        {
            return;
        }

        var validation = await _fanCurveService.SaveCurveAsync(curve);
        if (!validation.IsValid)
        {
            CurveValidationMessage = "✗ " + string.Join(" | ", validation.Errors);
            return;
        }

        var result = await _fanCurveService.RunTestModeAsync(
            curve.ChannelId,
            curve.SensorId,
            channel.IsCpuChannel,
            CurvePreviewTemperatureCelsius,
            CurveTestConfirmLowCpu || channel.ConfirmLowCpuUnder30);

        if (result.Success)
        {
            CurveTestModeMessage = "✓ " + result.Message;
            StatusMessage = "✓ Test mode: zastosowano wynik krzywej na kanale.";
        }
        else
        {
            CurveTestModeMessage = "✗ " + result.Message;
            StatusMessage = "✗ Test mode nie powiódł się.";
        }

        await RefreshFanChannelsAsync();
    }

    private async Task ResetCurveDefaultsAsync()
    {
        var channel = GetSelectedCurveChannel();
        if (channel is null)
        {
            return;
        }

        var sensorId = ResolveSensorIdForChannel(channel.Id);
        var curve = await _fanCurveService.ResetToDefaultAsync(channel.Id, sensorId, channel.IsCpuChannel);

        SelectedCurveSensorId = curve.SensorId;
        CurveHysteresisCelsius = curve.HysteresisCelsius;
        CurveSmoothingFactor = curve.SmoothingFactor;

        CurvePoints.Clear();
        foreach (var point in curve.Points)
        {
            CurvePoints.Add(new FanCurvePointViewModel
            {
                TemperatureCelsius = point.TemperatureCelsius,
                SpeedPercent = point.SpeedPercent
            });
        }

        CurveValidationMessage = "✓ Przywrócono domyślną krzywą.";
        await PreviewCurveAsync();
    }

    private FanCurve? BuildCurveFromEditor()
    {
        if (string.IsNullOrWhiteSpace(SelectedCurveChannelId))
        {
            CurveValidationMessage = "✗ Najpierw wybierz kanał do edycji krzywej.";
            return null;
        }

        var channel = GetSelectedCurveChannel();
        if (channel is null)
        {
            CurveValidationMessage = "✗ Nie znaleziono wybranego kanału.";
            return null;
        }

        SortCurvePoints();

        var points = CurvePoints
            .Select(point => new FanCurvePoint(point.TemperatureCelsius, point.SpeedPercent))
            .ToArray();

        return new FanCurve(
            ChannelId: SelectedCurveChannelId,
            SensorId: SelectedCurveSensorId,
            Points: points,
            HysteresisCelsius: CurveHysteresisCelsius,
            SmoothingFactor: CurveSmoothingFactor,
            MinimumAllowedPercent: channel.MinimumPercent,
            MaximumAllowedPercent: 100);
    }

    private FanChannelViewModel? GetSelectedCurveChannel()
    {
        return FanChannels.FirstOrDefault(channel =>
            string.Equals(channel.Id, SelectedCurveChannelId, StringComparison.OrdinalIgnoreCase));
    }

    private void SortCurvePoints()
    {
        var sorted = CurvePoints
            .OrderBy(point => point.TemperatureCelsius)
            .ToArray();

        if (sorted.SequenceEqual(CurvePoints))
        {
            return;
        }

        CurvePoints.Clear();
        foreach (var point in sorted)
        {
            CurvePoints.Add(point);
        }
    }

    private static string ResolveSensorIdForChannel(string channelId)
    {
        return channelId switch
        {
            "cpu_fan" => "cpu_temp",
            "gpu_fan" => "gpu_temp",
            _ => "mb_temp"
        };
    }

    private async Task ApplyFanSpeedAsync(FanChannelViewModel channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            var result = await _manualFanControlService.SetSpeedAsync(
                channel.Id,
                channel.RequestedPercent,
                channel.ConfirmLowCpuUnder30);

            ApplyOperationResult(channel, result, actionName: "SetSpeed");
            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply speed for {Channel}", channel.Id);
            StatusMessage = $"✗ {channel.Label}: błąd podczas ustawiania prędkości.";
        }
    }

    private async Task ResetFanSpeedAsync(FanChannelViewModel channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            var result = await _manualFanControlService.ResetAsync(channel.Id);
            ApplyOperationResult(channel, result, actionName: "Reset");
            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset speed for {Channel}", channel.Id);
            StatusMessage = $"✗ {channel.Label}: błąd podczas resetu prędkości.";
        }
    }

    private async Task FullSpeedFanAsync(FanChannelViewModel channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            var result = await _manualFanControlService.FullSpeedAsync(channel.Id);
            ApplyOperationResult(channel, result, actionName: "FullSpeed");
            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set full speed for {Channel}", channel.Id);
            StatusMessage = $"✗ {channel.Label}: błąd podczas ustawiania Full Speed.";
        }
    }

    private async Task AssignGroupAsync(FanChannelViewModel channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            await _manualFanControlService.AssignGroupAsync(channel.Id, channel.AssignedGroup);
            StatusMessage = $"✓ {channel.Label}: przypisano do grupy '{channel.AssignedGroup}'.";
            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign group for {Channel}", channel.Id);
            StatusMessage = $"✗ {channel.Label}: nie udało się przypisać grupy.";
        }
    }

    private async Task EmergencyFullSpeedAsync()
    {
        try
        {
            var results = await _manualFanControlService.FullSpeedAllAsync();
            var successCount = results.Count(result => result.Success);

            if (successCount == results.Count)
            {
                StatusMessage = "✓ Emergency Full Speed aktywowany dla wszystkich kanałów.";
            }
            else
            {
                StatusMessage =
                    $"⚠ Emergency Full Speed: sukces {successCount}/{results.Count}. Część kanałów pozostała w Monitoring Only.";
            }

            await RefreshFanChannelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute emergency full speed");
            StatusMessage = "✗ Nie udało się wykonać Emergency Full Speed.";
        }
    }

    private void ApplyOperationResult(FanChannelViewModel channel, FanControlResult result, string actionName)
    {
        if (result.Success)
        {
            StatusMessage = $"✓ {channel.Label}: {result.Message}";
            _logger.LogInformation(
                "{Action} succeeded for {ChannelId} at {Percent}%",
                actionName,
                channel.Id,
                result.AppliedPercent);
            return;
        }

        StatusMessage = $"✗ {channel.Label}: {result.Message}";
        _logger.LogWarning(
            "{Action} failed for {ChannelId}: {Message} ({Reason})",
            actionName,
            channel.Id,
            result.Message,
            result.FailureReason);
    }

    private async Task LoadHardwareAsync()
    {
        try
        {
            StatusMessage = "Detecting hardware...";
            _logger.LogInformation("Loading hardware information");

            await Task.Delay(1000);

            await RefreshFanChannelsAsync();
            StatusMessage = "✓ Hardware loaded successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading hardware");
            StatusMessage = $"✗ Error: {ex.Message}";
        }
    }
}
