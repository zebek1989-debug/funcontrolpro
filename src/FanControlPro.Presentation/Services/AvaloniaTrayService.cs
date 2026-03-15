using Avalonia.Controls;
using FanControlPro.Application.Configuration;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Presentation.Services;

public sealed class AvaloniaTrayService : ITrayService
{
    private const string EmptyProfilesLabel = "Brak profili";

    private readonly ILogger<AvaloniaTrayService> _logger;
    private readonly TrayIcon _trayIcon;
    private readonly NativeMenuItem _showHideItem;
    private readonly NativeMenuItem _profilesRootItem;
    private readonly NativeMenu _profilesMenu;
    private readonly NativeMenuItem _fullSpeedItem;
    private readonly NativeMenuItem _exitItem;
    private TrayStatus _lastStatus = TrayStatus.Initial;
    private bool _disposed;

    public AvaloniaTrayService(ILogger<AvaloniaTrayService> logger)
    {
        _logger = logger;

        _trayIcon = new TrayIcon();
        _showHideItem = new NativeMenuItem("Hide");
        _profilesRootItem = new NativeMenuItem("Profiles");
        _profilesMenu = new NativeMenu();
        _fullSpeedItem = new NativeMenuItem("Emergency Full Speed");
        _exitItem = new NativeMenuItem("Exit");

        _profilesRootItem.Menu = _profilesMenu;

        _showHideItem.Click += OnShowHideClick;
        _fullSpeedItem.Click += (_, _) => FullSpeedRequested?.Invoke(this, EventArgs.Empty);
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _trayIcon.Clicked += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var menu = new NativeMenu();
        menu.Items.Add(_showHideItem);
        menu.Items.Add(_profilesRootItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_fullSpeedItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_exitItem);

        _trayIcon.Menu = menu;
        _trayIcon.ToolTipText = BuildToolTip(_lastStatus);
        TryConfigureIcon();
        UpdateProfiles(Array.Empty<string>(), _lastStatus.ActiveProfile);
    }

    public event EventHandler? ShowRequested;

    public event EventHandler? HideRequested;

    public event EventHandler? FullSpeedRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<TrayProfileSwitchRequestedEventArgs>? ProfileSwitchRequested;

    public void Show()
    {
        ThrowIfDisposed();
        _trayIcon.IsVisible = true;
    }

    public void Hide()
    {
        if (_disposed)
        {
            return;
        }

        _trayIcon.IsVisible = false;
    }

    public void UpdateStatus(TrayStatus status)
    {
        ThrowIfDisposed();

        _lastStatus = status ?? TrayStatus.Initial;
        _trayIcon.ToolTipText = BuildToolTip(_lastStatus);
        _showHideItem.Header = _lastStatus.IsMainWindowVisible ? "Hide" : "Show";
    }

    public void UpdateProfiles(IReadOnlyList<string> profiles, string activeProfile)
    {
        ThrowIfDisposed();

        _profilesMenu.Items.Clear();

        if (profiles.Count == 0)
        {
            _profilesMenu.Items.Add(new NativeMenuItem(EmptyProfilesLabel) { IsEnabled = false });
            return;
        }

        foreach (var profileName in profiles
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new NativeMenuItem(profileName)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = string.Equals(profileName, activeProfile, StringComparison.OrdinalIgnoreCase)
            };

            item.Click += (_, _) =>
                ProfileSwitchRequested?.Invoke(this, new TrayProfileSwitchRequestedEventArgs(profileName));

            _profilesMenu.Items.Add(item);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }

    private void OnShowHideClick(object? sender, EventArgs e)
    {
        if (_lastStatus.IsMainWindowVisible)
        {
            HideRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TryConfigureIcon()
    {
        var iconPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "fancontrol-tray.png"),
            Path.Combine(AppContext.BaseDirectory, "fancontrol-tray.png"),
            Path.Combine(AppContext.BaseDirectory, "fancontrol.ico"),
            Environment.ProcessPath
        };

        foreach (var iconPath in iconPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (!File.Exists(iconPath))
            {
                continue;
            }

            try
            {
                _trayIcon.Icon = new WindowIcon(iconPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tray icon candidate rejected: {Path}", iconPath);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Tray icon file was not found. Tray may be hidden on some systems.");
        }
        else
        {
            _logger.LogDebug("Tray icon file was not found.");
        }
    }

    private static string BuildToolTip(TrayStatus status)
    {
        var profile = string.IsNullOrWhiteSpace(status.ActiveProfile) ? "n/a" : status.ActiveProfile.Trim();
        var safety = string.IsNullOrWhiteSpace(status.SafetyState) ? "Unknown" : status.SafetyState.Trim();
        var message = string.IsNullOrWhiteSpace(status.StatusMessage) ? "Ready" : status.StatusMessage.Trim();

        return TrimToLength($"FanControl Pro | {profile} | {safety} | {message}", maxLength: 120);
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
