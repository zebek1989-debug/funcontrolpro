using FanControlPro.Application.Configuration;
using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Curves;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Application.FanControl.Safety;
using FanControlPro.Application.Monitoring;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Curves;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.FanControl.Profiles;
using FanControlPro.Presentation.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FanControlPro.Tests.Presentation;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task SaveSettingsCommand_WithInvalidTheme_ShouldRejectWithoutPersist()
    {
        var context = CreateContext();
        var sut = CreateSut(context);
        await WaitForInitializationAsync(sut);

        sut.SelectedTheme = "invalid-theme";

        await sut.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(0, context.SettingsService.SaveCallCount);
        Assert.Equal("✗ Niepoprawny motyw aplikacji.", sut.SettingsStatusMessage);
    }

    [Fact]
    public async Task SaveSettingsCommand_WithValidTheme_ShouldPersistSettings()
    {
        var context = CreateContext();
        var sut = CreateSut(context);
        await WaitForInitializationAsync(sut);

        sut.SettingsPollingIntervalSeconds = 3;
        sut.SettingsCpuAlertThresholdCelsius = 85;
        sut.SettingsGpuAlertThresholdCelsius = 88;
        sut.SelectedTheme = ApplicationTheme.Dark.ToString();
        sut.SettingsEnableAutostart = true;
        sut.SettingsStartMinimizedToTray = false;
        sut.SettingsMinimizeToTrayOnClose = false;
        sut.SettingsStartupDelaySeconds = 45;
        sut.SettingsDefaultProfile = "Performance";

        await sut.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, context.SettingsService.SaveCallCount);
        Assert.Equal(ApplicationTheme.Dark, context.SettingsService.Current.Theme);
        Assert.Equal(85, context.SettingsService.Current.CpuAlertThresholdCelsius);
        Assert.Equal("Performance", context.SettingsService.Current.DefaultProfileName);
        Assert.Equal("✓ Ustawienia zapisane i zastosowane bez restartu.", sut.SettingsStatusMessage);
        Assert.Equal("✓ Zapisano ustawienia aplikacji.", sut.StatusMessage);
    }

    [Fact]
    public async Task ExportSupportBundleCommand_OnSuccess_ShouldSetPathAndStatus()
    {
        var context = CreateContext();
        context.SupportBundleService.Result = new SupportBundleResult(
            Success: true,
            BundlePath: @"C:\Temp\fancontrol\support.zip",
            IncludedFileCount: 4,
            Message: "ok");

        var sut = CreateSut(context);
        await WaitForInitializationAsync(sut);

        await sut.ExportSupportBundleCommand.ExecuteAsync(null);

        Assert.Equal(1, context.SupportBundleService.ExportCallCount);
        Assert.Equal(@"C:\Temp\fancontrol\support.zip", sut.LastSupportBundlePath);
        Assert.Contains("4 plików", sut.SupportBundleStatusMessage, StringComparison.Ordinal);
        Assert.Equal("✓ Support bundle został wyeksportowany.", sut.StatusMessage);
    }

    [Fact]
    public async Task RefreshDiagnosticsCommand_WhenServiceThrows_ShouldShowFallbackEntry()
    {
        var context = CreateContext();
        context.DiagnosticsService.ThrowOnRead = true;

        var sut = CreateSut(context);
        await WaitForInitializationAsync(sut);

        await sut.RefreshDiagnosticsCommand.ExecuteAsync(null);

        Assert.Single(sut.RecentDiagnostics);
        Assert.Equal("Nie udało się odczytać diagnostyki.", sut.RecentDiagnostics[0]);
    }

    [Fact]
    public async Task EmergencyFullSpeedCommand_WithPartialSuccess_ShouldReportRatio()
    {
        var context = CreateContext();
        context.ManualFanControlService.FullSpeedAllResults = new[]
        {
            FanControlResult.Succeeded(100, "cpu ok"),
            FanControlResult.Failed(FanControlFailureReason.MonitoringOnly, "gpu monitoring only")
        };

        var sut = CreateSut(context);
        await WaitForInitializationAsync(sut);

        await sut.EmergencyFullSpeedCommand.ExecuteAsync(null);

        Assert.Equal(1, context.ManualFanControlService.FullSpeedAllCallCount);
        Assert.Equal(
            "⚠ Emergency Full Speed: sukces 1/2. Część kanałów pozostała w Monitoring Only.",
            sut.StatusMessage);
    }

    private static DashboardViewModel CreateSut(TestContext context)
    {
        return new DashboardViewModel(
            NullLogger<DashboardViewModel>.Instance,
            context.ControlOnboardingService,
            context.ManualFanControlService,
            context.FanCurveService,
            context.ProfileService,
            context.SafetyMonitorService,
            context.DiagnosticsService,
            context.SupportBundleService,
            context.SettingsService);
    }

    private static TestContext CreateContext() => new();

    private static async Task WaitForInitializationAsync(DashboardViewModel sut)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (sut.FanChannels.Count > 0 &&
                sut.ProfileOptions.Count > 0 &&
                !string.Equals(sut.SettingsStatusMessage, "Ustawienia niezaładowane.", StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private sealed class TestContext
    {
        public FakeControlOnboardingService ControlOnboardingService { get; } = new();

        public FakeManualFanControlService ManualFanControlService { get; } = new();

        public FakeFanCurveService FanCurveService { get; } = new();

        public FakeProfileService ProfileService { get; } = new();

        public FakeSafetyMonitorService SafetyMonitorService { get; } = new();

        public FakeDiagnosticsService DiagnosticsService { get; } = new();

        public FakeSupportBundleService SupportBundleService { get; } = new();

        public FakeApplicationSettingsService SettingsService { get; } = new();
    }

    private sealed class FakeControlOnboardingService : IControlOnboardingService
    {
        private ControlOnboardingState _state = new(
            HasAcceptedRisk: false,
            AcceptedAtUtc: null,
            AcceptedBy: null);

        public Task<ControlOnboardingState> GetStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task<bool> HasAcceptedRiskAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state.HasAcceptedRisk);

        public Task<ControlOnboardingState> AcceptRiskAsync(string acceptedBy, CancellationToken cancellationToken = default)
        {
            _state = new ControlOnboardingState(true, DateTimeOffset.UtcNow, acceptedBy);
            return Task.FromResult(_state);
        }

        public Task<ControlOnboardingState> RevokeRiskAsync(string revokedBy, CancellationToken cancellationToken = default)
        {
            _state = new ControlOnboardingState(false, null, revokedBy);
            return Task.FromResult(_state);
        }
    }

    private sealed class FakeManualFanControlService : IManualFanControlService
    {
        private readonly List<FanChannelSnapshot> _channels =
        [
            new FanChannelSnapshot(
                Id: "cpu_fan",
                Name: "CPU Fan",
                Type: "CPU",
                CurrentPercent: 40,
                CurrentRpm: 1020,
                MinimumPercent: 20,
                MaximumPercent: 100,
                IsCpuChannel: true,
                CanControl: true,
                Status: "Full Control",
                AssignedGroup: "cpu"),
            new FanChannelSnapshot(
                Id: "gpu_fan",
                Name: "GPU Fan",
                Type: "GPU",
                CurrentPercent: 35,
                CurrentRpm: 980,
                MinimumPercent: 0,
                MaximumPercent: 100,
                IsCpuChannel: false,
                CanControl: true,
                Status: "Full Control",
                AssignedGroup: "gpu")
        ];

        public IReadOnlyList<string> AvailableGroups => ["cpu", "gpu", "case"];

        public IReadOnlyList<FanControlResult> FullSpeedAllResults { get; set; } =
        [
            FanControlResult.Succeeded(100, "cpu ok"),
            FanControlResult.Succeeded(100, "gpu ok")
        ];

        public int FullSpeedAllCallCount { get; private set; }

        public Task<IReadOnlyList<FanChannelSnapshot>> GetChannelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FanChannelSnapshot>>(_channels);

        public Task<FanControlResult> SetSpeedAsync(
            string channelId,
            int percent,
            bool confirmLowCpuFan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<FanControlResult> ResetAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(40, "Reset"));

        public Task<FanControlResult> FullSpeedAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(FanControlResult.Succeeded(100, "Full speed"));

        public Task<IReadOnlyList<FanControlResult>> FullSpeedAllAsync(CancellationToken cancellationToken = default)
        {
            FullSpeedAllCallCount++;
            return Task.FromResult(FullSpeedAllResults);
        }

        public Task AssignGroupAsync(string channelId, string? groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFanCurveService : IFanCurveService
    {
        public Task<FanCurve> GetOrCreateCurveAsync(
            string channelId,
            string sensorId,
            bool isCpuChannel,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDefaultCurve(channelId, sensorId, isCpuChannel));
        }

        public Task<CurveValidationResult> SaveCurveAsync(
            FanCurve curve,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurveValidationResult.Valid);
        }

        public Task<FanCurve> ResetToDefaultAsync(
            string channelId,
            string sensorId,
            bool isCpuChannel,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDefaultCurve(channelId, sensorId, isCpuChannel));
        }

        public Task<CurveEvaluationResult> PreviewAsync(
            string channelId,
            string sensorId,
            bool isCpuChannel,
            double temperatureCelsius,
            CancellationToken cancellationToken = default)
        {
            var applied = isCpuChannel ? 55 : 45;
            return Task.FromResult(
                new CurveEvaluationResult(
                    RawSpeedPercent: applied,
                    AppliedSpeedPercent: applied,
                    UsedHysteresis: false,
                    UsedSmoothing: false,
                    NextState: CurveEvaluationState.Empty));
        }

        public Task<FanControlResult> RunTestModeAsync(
            string channelId,
            string sensorId,
            bool isCpuChannel,
            double temperatureCelsius,
            bool confirmLowCpuFan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FanControlResult.Succeeded(60, "Test mode applied"));
        }

        private static FanCurve CreateDefaultCurve(string channelId, string sensorId, bool isCpuChannel)
        {
            return new FanCurve(
                ChannelId: channelId,
                SensorId: sensorId,
                Points:
                [
                    new FanCurvePoint(30, isCpuChannel ? 25 : 20),
                    new FanCurvePoint(55, 45),
                    new FanCurvePoint(75, 70)
                ],
                HysteresisCelsius: 2,
                SmoothingFactor: 0.35,
                MinimumAllowedPercent: isCpuChannel ? 20 : 0,
                MaximumAllowedPercent: 100);
        }
    }

    private sealed class FakeProfileService : IProfileService
    {
        private readonly List<FanProfile> _profiles =
        [
            new FanProfile(
                Name: "Balanced",
                Description: "Balanced default",
                IsPredefined: true,
                ChannelSettings:
                [
                    new FanProfileChannelSetting("cpu_fan", 40),
                    new FanProfileChannelSetting("gpu_fan", 35)
                ],
                UpdatedAtUtc: DateTimeOffset.UtcNow),
            new FanProfile(
                Name: "Performance",
                Description: "High performance",
                IsPredefined: true,
                ChannelSettings:
                [
                    new FanProfileChannelSetting("cpu_fan", 65),
                    new FanProfileChannelSetting("gpu_fan", 60)
                ],
                UpdatedAtUtc: DateTimeOffset.UtcNow)
        ];

        private FanProfile? _activeProfile;

        public Task<IReadOnlyList<FanProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FanProfile>>(_profiles);

        public Task<FanProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<FanProfile?>(_activeProfile);

        public Task<ProfileActivationResult> ActivateProfileAsync(string name, CancellationToken cancellationToken = default)
        {
            _activeProfile = _profiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

            if (_activeProfile is null)
            {
                return Task.FromResult(
                    new ProfileActivationResult(
                        Success: false,
                        ProfileName: name,
                        AppliedChannelCount: 0,
                        TotalChannelCount: 0,
                        Message: "Profile not found",
                        Errors: ["Profile not found"]));
            }

            return Task.FromResult(
                new ProfileActivationResult(
                    Success: true,
                    ProfileName: _activeProfile.Name,
                    AppliedChannelCount: _activeProfile.ChannelSettings.Count,
                    TotalChannelCount: _activeProfile.ChannelSettings.Count,
                    Message: "Profile activated",
                    Errors: Array.Empty<string>()));
        }

        public Task SaveProfileAsync(FanProfile profile, CancellationToken cancellationToken = default)
        {
            var existing = _profiles.FindIndex(item =>
                string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                _profiles[existing] = profile;
            }
            else
            {
                _profiles.Add(profile);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSafetyMonitorService : ISafetyMonitorServiceV2
    {
        public event EventHandler<HealthAttestation>? HealthAttestationChanged;

        public Task EnterSafeModeAsync(SafeModeReason reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ValidateSensorHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<HealthAttestation> GetHealthAttestationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(HealthAttestation.WaitingForTelemetry());

        public void Raise(HealthAttestation attestation)
            => HealthAttestationChanged?.Invoke(this, attestation);
    }

    private sealed class FakeDiagnosticsService : IDiagnosticsService
    {
        public bool ThrowOnRead { get; set; }

        public List<DiagnosticEvent> Events { get; } =
        [
            new DiagnosticEvent(
                TimestampUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
                Level: "Info",
                Message: "Startup complete")
        ];

        public Task<IReadOnlyList<DiagnosticEvent>> GetRecentEventsAsync(
            int maxEvents = 50,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("diagnostics unavailable");
            }

            return Task.FromResult<IReadOnlyList<DiagnosticEvent>>(Events.Take(maxEvents).ToArray());
        }
    }

    private sealed class FakeSupportBundleService : ISupportBundleService
    {
        public SupportBundleResult Result { get; set; } = new(
            Success: true,
            BundlePath: @"C:\Temp\support.zip",
            IncludedFileCount: 3,
            Message: "ok");

        public int ExportCallCount { get; private set; }

        public Task<SupportBundleResult> ExportSupportBundleAsync(
            string? outputDirectory = null,
            CancellationToken cancellationToken = default)
        {
            ExportCallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeApplicationSettingsService : IApplicationSettingsService
    {
        public event EventHandler<ApplicationSettings>? SettingsChanged;

        public ApplicationSettings Current { get; private set; } = ApplicationSettings.Default;

        public int SaveCallCount { get; private set; }

        public ApplicationSettingsValidationResult NextSaveResult { get; set; } =
            ApplicationSettingsValidationResult.Ok("ok");

        public Task<ApplicationSettings> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);

        public Task<ApplicationSettingsValidationResult> SaveAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            if (NextSaveResult.Success)
            {
                Current = settings;
                SettingsChanged?.Invoke(this, Current);
            }

            return Task.FromResult(NextSaveResult);
        }

        public Task<ApplicationSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
        {
            Current = ApplicationSettings.Default;
            SettingsChanged?.Invoke(this, Current);
            return Task.FromResult(Current);
        }
    }
}
