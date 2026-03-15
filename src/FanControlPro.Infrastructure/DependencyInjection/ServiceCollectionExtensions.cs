using FanControlPro.Application.FanControl;
using FanControlPro.Application.FanControl.Curves;
using FanControlPro.Application.FanControl.Profiles;
using FanControlPro.Application.FanControl.Safety;
using FanControlPro.Application.Configuration;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Application.Monitoring;
using FanControlPro.Application.Onboarding;
using FanControlPro.Infrastructure.Diagnostics;
using FanControlPro.Infrastructure.FanControl;
using FanControlPro.Infrastructure.HardwareDetection;
using FanControlPro.Infrastructure.Monitoring;
using FanControlPro.Infrastructure.Onboarding;
using FanControlPro.Infrastructure.Profiles;
using FanControlPro.Infrastructure.Recovery;
using FanControlPro.Infrastructure.Settings;
using FanControlPro.Infrastructure.SystemIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FanControlPro.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHardwareMonitoring(
        this IServiceCollection services,
        Action<HardwareDetectionOptions>? configureDetection = null,
        Action<MonitoringOptions>? configureMonitoring = null,
        Action<ControlOnboardingOptions>? configureControlOnboarding = null,
        Action<OnboardingOptions>? configureOnboarding = null,
        Action<ProfileStorageOptions>? configureProfiles = null,
        Action<SafetyMonitorOptions>? configureSafety = null,
        Action<BackupRecoveryOptions>? configureBackupRecovery = null,
        Action<DiagnosticsOptions>? configureDiagnostics = null,
        Action<AutostartOptions>? configureAutostart = null,
        Action<ApplicationSettingsStorageOptions>? configureAppSettings = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<HardwareDetectionOptions>();
        if (configureDetection is not null)
        {
            services.Configure(configureDetection);
        }

        services.AddOptions<MonitoringOptions>();
        if (configureMonitoring is not null)
        {
            services.Configure(configureMonitoring);
        }

        services.AddOptions<ControlOnboardingOptions>();
        if (configureControlOnboarding is not null)
        {
            services.Configure(configureControlOnboarding);
        }

        services.AddOptions<OnboardingOptions>();
        if (configureOnboarding is not null)
        {
            services.Configure(configureOnboarding);
        }

        services.AddOptions<ProfileStorageOptions>();
        if (configureProfiles is not null)
        {
            services.Configure(configureProfiles);
        }

        services.AddOptions<SafetyMonitorOptions>();
        if (configureSafety is not null)
        {
            services.Configure(configureSafety);
        }

        services.AddOptions<BackupRecoveryOptions>();
        if (configureBackupRecovery is not null)
        {
            services.Configure(configureBackupRecovery);
        }

        services.AddOptions<DiagnosticsOptions>();
        if (configureDiagnostics is not null)
        {
            services.Configure(configureDiagnostics);
        }

        services.AddOptions<AutostartOptions>();
        if (configureAutostart is not null)
        {
            services.Configure(configureAutostart);
        }

        services.AddOptions<ApplicationSettingsStorageOptions>();
        if (configureAppSettings is not null)
        {
            services.Configure(configureAppSettings);
        }

        services.TryAddSingleton<IHardwareProbe, LibreHardwareMonitorProbe>();
        services.TryAddSingleton<IHardwareCacheStore, HardwareJsonCacheStore>();
        services.TryAddSingleton<IHardwareDetector, HardwareDetector>();
        services.TryAddSingleton<ISensorReader, LibreHardwareMonitorSensorReader>();
        services.TryAddSingleton<ISensorSanityValidator, SensorSanityValidator>();
        services.TryAddSingleton<IMonitoringSampler, MonitoringSampler>();
        services.TryAddSingleton<IAppStateStore, AppStateStore>();
        services.TryAddSingleton<IMonitoringLoopService, MonitoringLoopService>();

        services.TryAddSingleton<IControlConsentStore, JsonControlConsentStore>();
        services.TryAddSingleton<IControlOnboardingService, ControlOnboardingService>();
        services.TryAddSingleton<IBackupServiceV2, BackupServiceV2>();
        services.TryAddSingleton<IRestoreManager, RestoreManager>();
        services.TryAddSingleton<IConfigurationHealthValidator, ConfigurationHealthValidator>();
        services.TryAddSingleton<IStartupRecoveryService, StartupRecoveryService>();
        services.TryAddSingleton<IDiagnosticsService, LogDiagnosticsService>();
        services.TryAddSingleton<ISupportBundleService, SupportBundleService>();
        services.TryAddSingleton<IAutostartService, TaskSchedulerAutostartService>();
        services.TryAddSingleton<IApplicationSettingsService, JsonApplicationSettingsService>();
        services.TryAddSingleton<IWriteCapabilityValidator, WriteCapabilityValidator>();
        services.TryAddSingleton<IManualFanControlService, ManualFanControlService>();
        services.TryAddSingleton<ICurveEngine, CurveEngine>();
        services.TryAddSingleton<IFanCurveService, FanCurveService>();
        services.TryAddSingleton<IProfileStore, JsonProfileStore>();
        services.TryAddSingleton<IProfileService, ProfileService>();
        services.TryAddSingleton<ISafetyMonitorServiceV2, SafetyMonitorServiceV2>();

        services.TryAddSingleton<AsusEcControllerV2>();
        services.TryAddSingleton<GigabyteEcControllerV2>();
        services.TryAddSingleton<MsiEcControllerV2>();
        services.TryAddSingleton<MonitoringOnlyController>();
        services.TryAddSingleton<IFanControllerFactory, FanControllerFactory>();
        services.TryAddSingleton<IOnboardingStateStore, JsonOnboardingStateStore>();
        services.TryAddSingleton<IOnboardingService, OnboardingService>();

        return services;
    }
}
