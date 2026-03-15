using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FanControlPro.Application.Configuration;
using FanControlPro.Infrastructure.DependencyInjection;
using FanControlPro.Presentation.Services;
using FanControlPro.Presentation.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace FanControlPro.Presentation
{
    public class App : Avalonia.Application
    {
        public static IHost? Host { get; private set; }
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

        private bool _allowWindowClose;
        private bool _minimizeToTrayOnClose = true;
        private readonly Dictionary<string, DateTimeOffset> _notificationCooldowns =
            new(StringComparer.OrdinalIgnoreCase);

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (Host == null)
            {
                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            .Enrich.FromLogContext()
                            .WriteTo.File(
                                path: System.IO.Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "FanControlPro", "logs", "app-.log"),
                                rollingInterval: Serilog.RollingInterval.Day,
                                retainedFileCountLimit: 14,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                            .CreateLogger();

                        services.AddLogging(builder => builder.AddSerilog());
                        services.AddHardwareMonitoring(
                            configureEcWriteSafety: options =>
                                context.Configuration.GetSection("EcWriteSafety").Bind(options));

                        services.AddSingleton<ITrayService, AvaloniaTrayService>();
                        services.AddSingleton<MainWindow>();
                        services.AddSingleton<DashboardView>();
                        services.AddSingleton<DashboardViewModel>();
                        services.AddSingleton<OnboardingView>();
                        services.AddSingleton<OnboardingViewModel>();
                    })
                    .Build();
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var startupLite = AppRuntimePolicy.HasArgument(
                    desktop.Args,
                    "--startup-lite",
                    "--skip-startup-services",
                    "--diag-startup");

                if (!startupLite)
                {
                    try
                    {
                        var startupRecovery = Host.Services.GetRequiredService<IStartupRecoveryService>();
                        var recoveryResult = startupRecovery.EnsureHealthyStartupAsync().GetAwaiter().GetResult();
                        Log.Information(
                            "Startup recovery: healthyBefore={HealthyBefore}, recovered={Recovered}, fallback={Fallback}, message={Message}",
                            recoveryResult.HealthyBeforeRecovery,
                            recoveryResult.Recovered,
                            recoveryResult.FallbackToSafeDefaults,
                            recoveryResult.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Startup recovery flow failed unexpectedly.");
                    }

                    try
                    {
                        var autostartService = Host.Services.GetRequiredService<IAutostartService>();
                        var autostartOptions = Host.Services.GetRequiredService<IOptions<AutostartOptions>>().Value;

                        var delay = TimeSpan.FromSeconds(Math.Max(0, autostartOptions.StartupDelaySeconds));
                        autostartService
                            .ConfigureAsync(
                                autostartOptions.EnableAutostart,
                                autostartOptions.StartMinimizedToTray,
                                delay)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Autostart integration failed to apply.");
                    }
                }

                var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                var viewModel = Host.Services.GetRequiredService<DashboardViewModel>();
                var disableTray = AppRuntimePolicy.HasArgument(desktop.Args, "--no-tray", "--disable-tray");
                ITrayService trayService = disableTray
                    ? new NullTrayService()
                    : Host.Services.GetRequiredService<ITrayService>();
                var applicationSettingsService = Host.Services.GetRequiredService<IApplicationSettingsService>();
                var notificationManager = new WindowNotificationManager(mainWindow)
                {
                    Position = NotificationPosition.BottomRight,
                    MaxItems = 3
                };

                if (!startupLite)
                {
                    try
                    {
                        var currentSettings = applicationSettingsService.GetCurrentAsync().GetAwaiter().GetResult();
                        ApplyTheme(currentSettings.Theme);
                        _minimizeToTrayOnClose = disableTray
                            ? false
                            : currentSettings.MinimizeToTrayOnClose;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to load initial application settings for UI behavior.");
                    }
                }
                else
                {
                    _minimizeToTrayOnClose = false;
                }

                mainWindow.DataContext = viewModel;
                desktop.MainWindow = mainWindow;

                NotifyCollectionChangedEventHandler profilesChanged = (_, _) =>
                    Dispatcher.UIThread.Post(() =>
                        trayService.UpdateProfiles(viewModel.ProfileOptions.ToArray(), viewModel.ActiveProfile));

                PropertyChangedEventHandler viewModelChanged = (_, eventArgs) =>
                {
                    if (string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.ActiveProfile), StringComparison.Ordinal) ||
                        string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.SafetyState), StringComparison.Ordinal) ||
                        string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.SafetyAlertsSummary), StringComparison.Ordinal) ||
                        string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.StatusMessage), StringComparison.Ordinal))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            trayService.UpdateProfiles(viewModel.ProfileOptions.ToArray(), viewModel.ActiveProfile);
                            trayService.UpdateStatus(CreateTrayStatus(mainWindow, viewModel));

                            if (string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.SafetyState), StringComparison.Ordinal))
                            {
                                MaybeNotifyFailsafe(
                                    notificationManager,
                                    viewModel.SafetyState,
                                    viewModel.SafetyStatusMessage);
                            }

                            if (string.Equals(eventArgs.PropertyName, nameof(DashboardViewModel.SafetyAlertsSummary), StringComparison.Ordinal))
                            {
                                MaybeNotifyTemperatureAlerts(
                                    notificationManager,
                                    viewModel.SafetyAlertsSummary);
                            }
                        });
                    }
                };

                EventHandler<WindowClosingEventArgs> onWindowClosing = (_, eventArgs) =>
                {
                    if (_allowWindowClose || !_minimizeToTrayOnClose)
                    {
                        return;
                    }

                    eventArgs.Cancel = true;
                    mainWindow.ShowInTaskbar = false;
                    mainWindow.Hide();
                    trayService.UpdateStatus(CreateTrayStatus(mainWindow, viewModel));
                };

                trayService.ShowRequested += (_, _) =>
                    Dispatcher.UIThread.Post(() => ShowMainWindow(mainWindow, trayService, viewModel));

                trayService.HideRequested += (_, _) =>
                    Dispatcher.UIThread.Post(() => HideMainWindow(mainWindow, trayService, viewModel));

                trayService.FullSpeedRequested += (_, _) =>
                    Dispatcher.UIThread.Post(() => _ = viewModel.EmergencyFullSpeedCommand.ExecuteAsync(null));

                trayService.ProfileSwitchRequested += (_, eventArgs) =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        var selectedProfile = viewModel.ProfileOptions.FirstOrDefault(profileName =>
                            string.Equals(profileName, eventArgs.ProfileName, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrWhiteSpace(selectedProfile))
                        {
                            viewModel.ActiveProfile = selectedProfile;
                        }
                    });

                EventHandler<ApplicationSettings> onSettingsChanged = (_, settings) =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        ApplyTheme(settings.Theme);
                        _minimizeToTrayOnClose = disableTray
                            ? false
                            : settings.MinimizeToTrayOnClose;
                        trayService.UpdateStatus(CreateTrayStatus(mainWindow, viewModel));
                    });

                trayService.ExitRequested += (_, _) =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        _allowWindowClose = true;
                        desktop.Shutdown();
                    });

                mainWindow.Closing += onWindowClosing;
                viewModel.PropertyChanged += viewModelChanged;
                viewModel.ProfileOptions.CollectionChanged += profilesChanged;
                applicationSettingsService.SettingsChanged += onSettingsChanged;

                trayService.UpdateProfiles(viewModel.ProfileOptions.ToArray(), viewModel.ActiveProfile);
                trayService.UpdateStatus(CreateTrayStatus(mainWindow, viewModel));

                MaybeNotifyVendorConflict(notificationManager);
                MaybeNotifyFailsafe(notificationManager, viewModel.SafetyState, viewModel.SafetyStatusMessage);
                MaybeNotifyTemperatureAlerts(notificationManager, viewModel.SafetyAlertsSummary);

                var startMinimized = AppRuntimePolicy.ShouldStartMinimized(desktop.Args);
                if (startMinimized)
                {
                    HideMainWindow(mainWindow, trayService, viewModel);
                }
                else
                {
                    ShowMainWindow(mainWindow, trayService, viewModel);
                    _ = EnsureMainWindowVisibleAsync(mainWindow, trayService, viewModel);
                }

                if (!disableTray)
                {
                    trayService.Show();
                }

                desktop.ShutdownRequested += (_, _) =>
                {
                    _allowWindowClose = true;
                    mainWindow.Closing -= onWindowClosing;
                    viewModel.PropertyChanged -= viewModelChanged;
                    viewModel.ProfileOptions.CollectionChanged -= profilesChanged;
                    applicationSettingsService.SettingsChanged -= onSettingsChanged;
                    trayService.Hide();
                    trayService.Dispose();

                    Host?.StopAsync().GetAwaiter().GetResult();
                    Log.CloseAndFlush();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task EnsureMainWindowVisibleAsync(
            MainWindow window,
            ITrayService trayService,
            DashboardViewModel viewModel)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1250)).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (window.IsVisible)
                {
                    return;
                }

                ShowMainWindow(window, trayService, viewModel);
            });
        }

        private static void ShowMainWindow(MainWindow window, ITrayService trayService, DashboardViewModel viewModel)
        {
            window.ShowInTaskbar = true;

            if (!window.IsVisible)
            {
                window.Show();
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            trayService.UpdateStatus(CreateTrayStatus(window, viewModel));
        }

        private static void HideMainWindow(MainWindow window, ITrayService trayService, DashboardViewModel viewModel)
        {
            window.ShowInTaskbar = false;
            window.Hide();
            trayService.UpdateStatus(CreateTrayStatus(window, viewModel));
        }

        private static TrayStatus CreateTrayStatus(MainWindow window, DashboardViewModel viewModel)
        {
            return new TrayStatus(
                ActiveProfile: viewModel.ActiveProfile,
                SafetyState: viewModel.SafetyState,
                StatusMessage: viewModel.StatusMessage,
                IsMainWindowVisible: window.IsVisible);
        }

        private void MaybeNotifyTemperatureAlerts(WindowNotificationManager manager, string alertsSummary)
        {
            if (string.IsNullOrWhiteSpace(alertsSummary) ||
                alertsSummary.StartsWith("Brak alertow", StringComparison.OrdinalIgnoreCase) ||
                alertsSummary.StartsWith("Brak alertów", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            NotifyWithCooldown(
                manager,
                key: $"temp:{alertsSummary}",
                cooldown: TimeSpan.FromMinutes(2),
                title: "Temperature Alert",
                message: alertsSummary,
                type: NotificationType.Warning);
        }

        private void MaybeNotifyFailsafe(WindowNotificationManager manager, string safetyState, string safetyMessage)
        {
            if (!string.Equals(safetyState, "Emergency", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(safetyState, "Shutdown", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            NotifyWithCooldown(
                manager,
                key: $"failsafe:{safetyState}",
                cooldown: TimeSpan.FromMinutes(5),
                title: "Failsafe Activated",
                message: string.IsNullOrWhiteSpace(safetyMessage)
                    ? "Safety monitor entered failsafe mode."
                    : safetyMessage,
                type: NotificationType.Error);
        }

        private void MaybeNotifyVendorConflict(WindowNotificationManager manager)
        {
            var processNames = DetectVendorToolProcesses();
            if (processNames.Count == 0)
            {
                return;
            }

            var details = string.Join(", ", processNames.Take(4));

            NotifyWithCooldown(
                manager,
                key: "vendor-conflict",
                cooldown: TimeSpan.FromMinutes(10),
                title: "Vendor Tool Conflict",
                message: $"Detected active vendor utility: {details}. Full Control may be blocked.",
                type: NotificationType.Warning);
        }

        private void NotifyWithCooldown(
            WindowNotificationManager manager,
            string key,
            TimeSpan cooldown,
            string title,
            string message,
            NotificationType type)
        {
            if (!TryAcquireNotificationSlot(key, cooldown))
            {
                return;
            }

            manager.Show(new Notification(
                title,
                message,
                type,
                TimeSpan.FromSeconds(8),
                onClick: null,
                onClose: null));
        }

        private bool TryAcquireNotificationSlot(string key, TimeSpan cooldown)
        {
            var now = DateTimeOffset.UtcNow;

            lock (_notificationCooldowns)
            {
                if (_notificationCooldowns.TryGetValue(key, out var previous) && now - previous < cooldown)
                {
                    return false;
                }

                _notificationCooldowns[key] = now;
                return true;
            }
        }

        private static IReadOnlyList<string> DetectVendorToolProcesses()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        var normalized = process.ProcessName
                            .Replace(" ", string.Empty, StringComparison.Ordinal)
                            .ToLowerInvariant();

                        if (VendorProcessTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal)))
                        {
                            names.Add(process.ProcessName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Vendor process detection failed.");
            }

            return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void ApplyTheme(ApplicationTheme theme)
        {
            RequestedThemeVariant = AppRuntimePolicy.ResolveThemeVariant(theme);
        }
    }
}
