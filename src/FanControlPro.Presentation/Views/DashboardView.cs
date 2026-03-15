using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using FanControlPro.Presentation.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace FanControlPro.Presentation;

public class DashboardView : UserControl
{
    private readonly StackPanel _fanControlContainer;
    private readonly StackPanel _curvePointsContainer;
    private readonly Border _curveChartHost;
    private readonly TextBlock _curveValidationText;
    private readonly TextBlock _curveTestMessageText;

    private readonly Slider _hysteresisSlider;
    private readonly Slider _smoothingSlider;
    private readonly Slider _previewTemperatureSlider;
    private readonly TextBox _newPointTemperatureBox;
    private readonly TextBox _newPointSpeedBox;
    private readonly CheckBox _curveCpuConfirmCheck;

    private DashboardViewModel? _boundViewModel;

    public DashboardView()
    {
        _fanControlContainer = new StackPanel { Spacing = 10 };
        _curvePointsContainer = new StackPanel { Spacing = 8 };

        _curveChartHost = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FAFAFA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8)
        };

        _curveValidationText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            Text = "Krzywa niezaładowana."
        };

        _curveTestMessageText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#546E7A")),
            Text = ""
        };

        _hysteresisSlider = new Slider
        {
            Minimum = 1,
            Maximum = 10,
            Value = 2,
            Width = 180
        };

        _smoothingSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0.35,
            Width = 180
        };

        _previewTemperatureSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 55,
            Width = 220
        };

        _newPointTemperatureBox = new TextBox
        {
            Width = 90,
            Text = "65"
        };

        _newPointSpeedBox = new TextBox
        {
            Width = 80,
            Text = "60"
        };

        _curveCpuConfirmCheck = new CheckBox
        {
            Content = "Potwierdź test CPU_FAN < 30%",
            IsChecked = false
        };

        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        var root = new StackPanel
        {
            Spacing = 12,
            Orientation = Orientation.Vertical,
            Margin = new Thickness(20)
        };

        root.Children.Add(new TextBlock
        {
            Text = "FanControl Pro Dashboard",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1976D2"))
        });

        root.Children.Add(CreateOnboardingCard());

        var statusText = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#2E7D32"))
        };
        statusText.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        root.Children.Add(statusText);

        root.Children.Add(CreateSafetyStatusCard());
        root.Children.Add(CreateDiagnosticsCard());

        root.Children.Add(CreateProfileBar());
        root.Children.Add(CreateSectionHeader("Application Settings"));
        root.Children.Add(CreateSettingsCard());

        root.Children.Add(CreateSectionHeader("Manual PWM Control"));
        root.Children.Add(CreateControlToolbar());
        root.Children.Add(_fanControlContainer);

        root.Children.Add(CreateSectionHeader("Curve Editor"));
        root.Children.Add(CreateCurveEditorCard());

        root.Children.Add(CreateSectionHeader("Temperatures"));
        root.Children.Add(CreateValueLine("CPU: 45.5°C (Min: 32°C, Max: 85°C)"));
        root.Children.Add(CreateValueLine("GPU: 52.3°C (Min: 30°C, Max: 90°C)"));
        root.Children.Add(CreateValueLine("Motherboard: 38.1°C (Min: 25°C, Max: 75°C)"));

        root.Children.Add(CreateSectionHeader("System Load"));
        root.Children.Add(CreateValueLine("CPU: 25.5%"));
        root.Children.Add(CreateValueLine("GPU: 10.0%"));
        root.Children.Add(CreateValueLine("RAM: 7.2%"));

        root.Children.Add(new TextBlock
        {
            Text = "Ready for operations",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#999")),
            Margin = new Thickness(0, 8, 0, 0)
        });

        Content = new ScrollViewer { Content = root };
    }

    private static Border CreateOnboardingCard()
    {
        var container = new StackPanel { Spacing = 8 };

        container.Children.Add(new TextBlock
        {
            Text = "Control Mode Safety Gate",
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#E65100"))
        });

        var message = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#5D4037"))
        };
        message.Bind(TextBlock.TextProperty, new Binding("ControlOnboardingMessage"));
        container.Children.Add(message);

        var checkbox = new CheckBox
        {
            Content = "Rozumiem konsekwencje ręcznego sterowania wentylatorami"
        };
        checkbox.Bind(CheckBox.IsCheckedProperty, new Binding("IsRiskConfirmationChecked", BindingMode.TwoWay));
        checkbox.Bind(CheckBox.IsEnabledProperty, new Binding("IsControlOnboardingRequired"));
        container.Children.Add(checkbox);

        var button = new Button
        {
            Content = "Unlock Control Mode",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6)
        };
        button.Bind(Button.CommandProperty, new Binding("ConfirmRiskConsentCommand"));
        button.Bind(Button.IsEnabledProperty, new Binding("IsControlOnboardingRequired"));
        container.Children.Add(button);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFF8E1")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = container
        };
    }

    private static Border CreateControlToolbar()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };

        var emergencyButton = new Button
        {
            Content = "Emergency Full Speed",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6),
            Background = new SolidColorBrush(Color.Parse("#C62828")),
            Foreground = new SolidColorBrush(Colors.White)
        };
        emergencyButton.Bind(Button.CommandProperty, new Binding("EmergencyFullSpeedCommand"));

        var refreshButton = new Button
        {
            Content = "Refresh Channels",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6),
            Margin = new Thickness(8, 0, 0, 0)
        };
        refreshButton.Bind(Button.CommandProperty, new Binding("RefreshFanControlCommand"));

        var hint = new TextBlock
        {
            Text = "CPU_FAN: minimum 20%, a poniżej 30% wymagane potwierdzenie.",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#5D4037")),
            Margin = new Thickness(12, 0, 0, 0)
        };

        Grid.SetColumn(emergencyButton, 0);
        Grid.SetColumn(refreshButton, 1);
        Grid.SetColumn(hint, 2);

        grid.Children.Add(emergencyButton);
        grid.Children.Add(refreshButton);
        grid.Children.Add(hint);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFF3E0")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = grid
        };
    }

    private static Border CreateSafetyStatusCard()
    {
        var container = new StackPanel { Spacing = 4 };

        container.Children.Add(new TextBlock
        {
            Text = "Safety Monitor",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#B71C1C"))
        });

        var state = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#37474F"))
        };
        state.Bind(TextBlock.TextProperty, new Binding("SafetyState")
        {
            StringFormat = "State: {0}"
        });
        container.Children.Add(state);

        var message = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            TextWrapping = TextWrapping.Wrap
        };
        message.Bind(TextBlock.TextProperty, new Binding("SafetyStatusMessage"));
        container.Children.Add(message);

        var alerts = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#5D4037")),
            TextWrapping = TextWrapping.Wrap
        };
        alerts.Bind(TextBlock.TextProperty, new Binding("SafetyAlertsSummary"));
        container.Children.Add(alerts);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFEBEE")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = container
        };
    }

    private static Border CreateDiagnosticsCard()
    {
        var container = new StackPanel { Spacing = 6 };

        container.Children.Add(new TextBlock
        {
            Text = "Diagnostics",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#263238"))
        });

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };

        var refreshButton = new Button
        {
            Content = "Refresh Timeline",
            Padding = new Thickness(10, 4)
        };
        refreshButton.Bind(Button.CommandProperty, new Binding("RefreshDiagnosticsCommand"));

        var exportButton = new Button
        {
            Content = "Export Support Bundle",
            Padding = new Thickness(10, 4),
            Margin = new Thickness(8, 0, 0, 0)
        };
        exportButton.Bind(Button.CommandProperty, new Binding("ExportSupportBundleCommand"));

        Grid.SetColumn(refreshButton, 0);
        Grid.SetColumn(exportButton, 1);

        actions.Children.Add(refreshButton);
        actions.Children.Add(exportButton);
        container.Children.Add(actions);

        var bundleStatus = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            TextWrapping = TextWrapping.Wrap
        };
        bundleStatus.Bind(TextBlock.TextProperty, new Binding("SupportBundleStatusMessage"));
        container.Children.Add(bundleStatus);

        var timelineHeader = new TextBlock
        {
            Text = "Recent Events",
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#37474F"))
        };
        container.Children.Add(timelineHeader);

        var timeline = new ItemsControl
        {
            MaxHeight = 140
        };
        timeline.Bind(ItemsControl.ItemsSourceProperty, new Binding("RecentDiagnostics"));
        container.Children.Add(new ScrollViewer
        {
            MaxHeight = 140,
            Content = timeline
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F3F7FA")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = container
        };
    }

    private static Border CreateProfileBar()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,180,Auto,*")
        };

        var label = new TextBlock
        {
            Text = "Profile",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontWeight = FontWeight.Medium
        };

        var profileCombo = new ComboBox
        {
            Width = 170
        };
        profileCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("ProfileOptions"));
        profileCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("ActiveProfile", BindingMode.TwoWay));

        var saveCustomButton = new Button
        {
            Content = "Save As Custom",
            Margin = new Thickness(8, 0, 0, 0)
        };
        saveCustomButton.Bind(Button.CommandProperty, new Binding("SaveCustomProfileCommand"));

        var activeIndicator = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            Margin = new Thickness(12, 0, 0, 0),
            FontSize = 12
        };
        activeIndicator.Bind(TextBlock.TextProperty, new Binding("ActiveProfile")
        {
            Mode = BindingMode.OneWay,
            StringFormat = "Active: {0}"
        });

        Grid.SetColumn(label, 0);
        Grid.SetColumn(profileCombo, 1);
        Grid.SetColumn(saveCustomButton, 2);
        Grid.SetColumn(activeIndicator, 3);

        row.Children.Add(label);
        row.Children.Add(profileCombo);
        row.Children.Add(saveCustomButton);
        row.Children.Add(activeIndicator);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#ECEFF1")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = row
        };
    }

    private static Border CreateSettingsCard()
    {
        var container = new StackPanel { Spacing = 8 };

        container.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#263238"))
        });

        var pollingRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,120,Auto,120,Auto,120,*") };

        pollingRow.Children.Add(new TextBlock
        {
            Text = "Polling (s)",
            VerticalAlignment = VerticalAlignment.Center
        });

        var pollingCombo = new ComboBox { Width = 110 };
        pollingCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("PollingIntervalOptions"));
        pollingCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SettingsPollingIntervalSeconds", BindingMode.TwoWay));
        Grid.SetColumn(pollingCombo, 1);
        pollingRow.Children.Add(pollingCombo);

        var cpuLabel = new TextBlock
        {
            Text = "CPU alert (C)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(cpuLabel, 2);
        pollingRow.Children.Add(cpuLabel);

        var cpuThresholdCombo = new ComboBox { Width = 110 };
        cpuThresholdCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("TemperatureThresholdOptions"));
        cpuThresholdCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SettingsCpuAlertThresholdCelsius", BindingMode.TwoWay));
        Grid.SetColumn(cpuThresholdCombo, 3);
        pollingRow.Children.Add(cpuThresholdCombo);

        var gpuLabel = new TextBlock
        {
            Text = "GPU alert (C)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(gpuLabel, 4);
        pollingRow.Children.Add(gpuLabel);

        var gpuThresholdCombo = new ComboBox { Width = 110 };
        gpuThresholdCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("TemperatureThresholdOptions"));
        gpuThresholdCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SettingsGpuAlertThresholdCelsius", BindingMode.TwoWay));
        Grid.SetColumn(gpuThresholdCombo, 5);
        pollingRow.Children.Add(gpuThresholdCombo);

        container.Children.Add(pollingRow);

        var behaviorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,160,Auto,160,Auto,120,*") };

        behaviorRow.Children.Add(new TextBlock
        {
            Text = "Theme",
            VerticalAlignment = VerticalAlignment.Center
        });

        var themeCombo = new ComboBox { Width = 150 };
        themeCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("ThemeOptions"));
        themeCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedTheme", BindingMode.TwoWay));
        Grid.SetColumn(themeCombo, 1);
        behaviorRow.Children.Add(themeCombo);

        var delayLabel = new TextBlock
        {
            Text = "Startup delay (s)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(delayLabel, 2);
        behaviorRow.Children.Add(delayLabel);

        var delayCombo = new ComboBox { Width = 150 };
        delayCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("StartupDelayOptions"));
        delayCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SettingsStartupDelaySeconds", BindingMode.TwoWay));
        Grid.SetColumn(delayCombo, 3);
        behaviorRow.Children.Add(delayCombo);

        var defaultProfileLabel = new TextBlock
        {
            Text = "Default profile",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(defaultProfileLabel, 4);
        behaviorRow.Children.Add(defaultProfileLabel);

        var defaultProfileCombo = new ComboBox { Width = 110 };
        defaultProfileCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("ProfileOptions"));
        defaultProfileCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SettingsDefaultProfile", BindingMode.TwoWay));
        Grid.SetColumn(defaultProfileCombo, 5);
        behaviorRow.Children.Add(defaultProfileCombo);

        container.Children.Add(behaviorRow);

        var startupToggles = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var autostartCheck = new CheckBox
        {
            Content = "Autostart po logowaniu"
        };
        autostartCheck.Bind(CheckBox.IsCheckedProperty, new Binding("SettingsEnableAutostart", BindingMode.TwoWay));
        startupToggles.Children.Add(autostartCheck);

        var minimizedCheck = new CheckBox
        {
            Content = "Start zminimalizowany do tray"
        };
        minimizedCheck.Bind(CheckBox.IsCheckedProperty, new Binding("SettingsStartMinimizedToTray", BindingMode.TwoWay));
        startupToggles.Children.Add(minimizedCheck);

        var closeToTrayCheck = new CheckBox
        {
            Content = "X minimalizuje do tray"
        };
        closeToTrayCheck.Bind(CheckBox.IsCheckedProperty, new Binding("SettingsMinimizeToTrayOnClose", BindingMode.TwoWay));
        startupToggles.Children.Add(closeToTrayCheck);

        container.Children.Add(startupToggles);

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var saveButton = new Button
        {
            Content = "Save Settings",
            Padding = new Thickness(10, 4)
        };
        saveButton.Bind(Button.CommandProperty, new Binding("SaveSettingsCommand"));
        actionRow.Children.Add(saveButton);

        var resetButton = new Button
        {
            Content = "Reset Defaults",
            Padding = new Thickness(10, 4)
        };
        resetButton.Bind(Button.CommandProperty, new Binding("ResetSettingsCommand"));
        actionRow.Children.Add(resetButton);

        container.Children.Add(actionRow);

        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            TextWrapping = TextWrapping.Wrap
        };
        statusText.Bind(TextBlock.TextProperty, new Binding("SettingsStatusMessage"));
        container.Children.Add(statusText);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = container
        };
    }

    private Border CreateCurveEditorCard()
    {
        var card = new StackPanel { Spacing = 10 };

        var channelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,180,Auto,Auto,Auto") };

        var channelLabel = new TextBlock
        {
            Text = "Channel",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var channelCombo = new ComboBox
        {
            Width = 170,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        channelCombo.Bind(ItemsControl.ItemsSourceProperty, new Binding("CurveChannelOptions"));
        channelCombo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedCurveChannelId", BindingMode.TwoWay));

        var saveButton = new Button { Content = "Save Curve", Margin = new Thickness(8, 0, 0, 0) };
        saveButton.Bind(Button.CommandProperty, new Binding("SaveCurveCommand"));

        var previewButton = new Button { Content = "Preview", Margin = new Thickness(8, 0, 0, 0) };
        previewButton.Bind(Button.CommandProperty, new Binding("PreviewCurveCommand"));

        var resetButton = new Button { Content = "Reset Defaults", Margin = new Thickness(8, 0, 0, 0) };
        resetButton.Bind(Button.CommandProperty, new Binding("ResetCurveDefaultsCommand"));

        Grid.SetColumn(channelLabel, 0);
        Grid.SetColumn(channelCombo, 1);
        Grid.SetColumn(saveButton, 2);
        Grid.SetColumn(previewButton, 3);
        Grid.SetColumn(resetButton, 4);

        channelRow.Children.Add(channelLabel);
        channelRow.Children.Add(channelCombo);
        channelRow.Children.Add(saveButton);
        channelRow.Children.Add(previewButton);
        channelRow.Children.Add(resetButton);

        var tuningRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,180,Auto,180,Auto,*") };

        var hysteresisLabel = new TextBlock
        {
            Text = "Hysteresis (C)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        _hysteresisSlider.ValueChanged += (_, _) =>
        {
            if (_boundViewModel is not null)
            {
                _boundViewModel.CurveHysteresisCelsius = (int)Math.Round(_hysteresisSlider.Value, MidpointRounding.AwayFromZero);
            }
        };

        var smoothingLabel = new TextBlock
        {
            Text = "Smoothing",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0)
        };

        _smoothingSlider.ValueChanged += (_, _) =>
        {
            if (_boundViewModel is not null)
            {
                _boundViewModel.CurveSmoothingFactor = Math.Round(_smoothingSlider.Value, 2, MidpointRounding.AwayFromZero);
            }
        };

        var addPointButton = new Button
        {
            Content = "Add Point",
            Margin = new Thickness(12, 0, 0, 0)
        };
        addPointButton.Bind(Button.CommandProperty, new Binding("AddCurvePointCommand"));

        Grid.SetColumn(hysteresisLabel, 0);
        Grid.SetColumn(_hysteresisSlider, 1);
        Grid.SetColumn(smoothingLabel, 2);
        Grid.SetColumn(_smoothingSlider, 3);
        Grid.SetColumn(addPointButton, 4);

        tuningRow.Children.Add(hysteresisLabel);
        tuningRow.Children.Add(_hysteresisSlider);
        tuningRow.Children.Add(smoothingLabel);
        tuningRow.Children.Add(_smoothingSlider);
        tuningRow.Children.Add(addPointButton);

        var addPointRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,100,Auto,90,*") };

        var tempLabel = new TextBlock
        {
            Text = "New point: Temp",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var speedLabel = new TextBlock
        {
            Text = "Speed %",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0)
        };

        _newPointTemperatureBox.LostFocus += (_, _) =>
        {
            if (_boundViewModel is null)
            {
                return;
            }

            if (double.TryParse(_newPointTemperatureBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _boundViewModel.NewCurvePointTemperatureCelsius = value;
            }
            else
            {
                _newPointTemperatureBox.Text = _boundViewModel.NewCurvePointTemperatureCelsius.ToString("F1", CultureInfo.InvariantCulture);
            }
        };

        _newPointSpeedBox.LostFocus += (_, _) =>
        {
            if (_boundViewModel is null)
            {
                return;
            }

            if (int.TryParse(_newPointSpeedBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                _boundViewModel.NewCurvePointSpeedPercent = value;
            }
            else
            {
                _newPointSpeedBox.Text = _boundViewModel.NewCurvePointSpeedPercent.ToString(CultureInfo.InvariantCulture);
            }
        };

        Grid.SetColumn(tempLabel, 0);
        Grid.SetColumn(_newPointTemperatureBox, 1);
        Grid.SetColumn(speedLabel, 2);
        Grid.SetColumn(_newPointSpeedBox, 3);

        addPointRow.Children.Add(tempLabel);
        addPointRow.Children.Add(_newPointTemperatureBox);
        addPointRow.Children.Add(speedLabel);
        addPointRow.Children.Add(_newPointSpeedBox);

        var previewRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,240,Auto,Auto") };

        var previewLabel = new TextBlock
        {
            Text = "Preview Temp (C)",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        _previewTemperatureSlider.ValueChanged += (_, _) =>
        {
            if (_boundViewModel is null)
            {
                return;
            }

            _boundViewModel.CurvePreviewTemperatureCelsius =
                Math.Round(_previewTemperatureSlider.Value, 1, MidpointRounding.AwayFromZero);
        };

        var testButton = new Button { Content = "Test Mode", Margin = new Thickness(8, 0, 0, 0) };
        testButton.Bind(Button.CommandProperty, new Binding("RunCurveTestModeCommand"));

        _curveCpuConfirmCheck.IsCheckedChanged += (_, _) =>
        {
            if (_boundViewModel is not null)
            {
                _boundViewModel.CurveTestConfirmLowCpu = _curveCpuConfirmCheck.IsChecked ?? false;
            }
        };

        Grid.SetColumn(previewLabel, 0);
        Grid.SetColumn(_previewTemperatureSlider, 1);
        Grid.SetColumn(testButton, 2);
        Grid.SetColumn(_curveCpuConfirmCheck, 3);

        previewRow.Children.Add(previewLabel);
        previewRow.Children.Add(_previewTemperatureSlider);
        previewRow.Children.Add(testButton);
        previewRow.Children.Add(_curveCpuConfirmCheck);

        card.Children.Add(channelRow);
        card.Children.Add(tuningRow);
        card.Children.Add(addPointRow);
        card.Children.Add(previewRow);
        card.Children.Add(_curvePointsContainer);
        card.Children.Add(_curveChartHost);
        card.Children.Add(_curveValidationText);
        card.Children.Add(_curveTestMessageText);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = card
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachViewModelHandlers();

        _boundViewModel = DataContext as DashboardViewModel;
        AttachViewModelHandlers();

        RebuildFanRows();
        RebuildCurveRows();
        UpdateCurveChart();
        UpdateCurveStatus();
    }

    private void AttachViewModelHandlers()
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _boundViewModel.FanChannels.CollectionChanged += OnFanChannelsCollectionChanged;
        _boundViewModel.CurvePoints.CollectionChanged += OnCurvePointsCollectionChanged;
        _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;

        foreach (var point in _boundViewModel.CurvePoints)
        {
            point.PropertyChanged += OnCurvePointPropertyChanged;
        }

        SyncCurveInputControls();
    }

    private void DetachViewModelHandlers()
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _boundViewModel.FanChannels.CollectionChanged -= OnFanChannelsCollectionChanged;
        _boundViewModel.CurvePoints.CollectionChanged -= OnCurvePointsCollectionChanged;
        _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        foreach (var point in _boundViewModel.CurvePoints)
        {
            point.PropertyChanged -= OnCurvePointPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardViewModel.CurveValidationMessage)
            or nameof(DashboardViewModel.CurveTestModeMessage)
            or nameof(DashboardViewModel.CurvePreviewSpeedPercent)
            or nameof(DashboardViewModel.CurvePreviewTemperatureCelsius)
            or nameof(DashboardViewModel.CurveHysteresisCelsius)
            or nameof(DashboardViewModel.CurveSmoothingFactor)
            or nameof(DashboardViewModel.CurveTestConfirmLowCpu))
        {
            SyncCurveInputControls();
            UpdateCurveStatus();
            UpdateCurveChart();
        }
    }

    private void OnFanChannelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildFanRows();
    }

    private void OnCurvePointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldPoint in e.OldItems.OfType<FanCurvePointViewModel>())
            {
                oldPoint.PropertyChanged -= OnCurvePointPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newPoint in e.NewItems.OfType<FanCurvePointViewModel>())
            {
                newPoint.PropertyChanged += OnCurvePointPropertyChanged;
            }
        }

        RebuildCurveRows();
        UpdateCurveChart();
    }

    private void OnCurvePointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FanCurvePointViewModel.TemperatureCelsius)
            or nameof(FanCurvePointViewModel.SpeedPercent))
        {
            UpdateCurveChart();
        }
    }

    private void RebuildFanRows()
    {
        _fanControlContainer.Children.Clear();

        if (_boundViewModel is null)
        {
            return;
        }

        if (_boundViewModel.FanChannels.Count == 0)
        {
            _fanControlContainer.Children.Add(new TextBlock
            {
                Text = "Brak wykrytych kanałów wentylatorów.",
                Foreground = new SolidColorBrush(Color.Parse("#757575"))
            });
            return;
        }

        foreach (var channel in _boundViewModel.FanChannels)
        {
            _fanControlContainer.Children.Add(CreateFanChannelControl(channel));
        }
    }

    private void RebuildCurveRows()
    {
        _curvePointsContainer.Children.Clear();

        if (_boundViewModel is null)
        {
            return;
        }

        if (_boundViewModel.CurvePoints.Count == 0)
        {
            _curvePointsContainer.Children.Add(new TextBlock
            {
                Text = "Dodaj punkty krzywej (minimum 4, maksimum 8).",
                Foreground = new SolidColorBrush(Color.Parse("#757575"))
            });
            return;
        }

        foreach (var point in _boundViewModel.CurvePoints)
        {
            _curvePointsContainer.Children.Add(CreateCurvePointRow(point));
        }
    }

    private Control CreateCurvePointRow(FanCurvePointViewModel point)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,120,Auto,120,Auto") };

        var tempLabel = new TextBlock
        {
            Text = "Temp",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var tempBox = new TextBox
        {
            Text = point.TemperatureCelsius.ToString("F1", CultureInfo.InvariantCulture),
            Width = 110
        };

        tempBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(tempBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                point.TemperatureCelsius = value;
            }
            else
            {
                tempBox.Text = point.TemperatureCelsius.ToString("F1", CultureInfo.InvariantCulture);
            }
        };

        var speedLabel = new TextBlock
        {
            Text = "Speed %",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 6, 0)
        };

        var speedBox = new TextBox
        {
            Text = point.SpeedPercent.ToString(CultureInfo.InvariantCulture),
            Width = 110
        };

        speedBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(speedBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                point.SpeedPercent = value;
            }
            else
            {
                speedBox.Text = point.SpeedPercent.ToString(CultureInfo.InvariantCulture);
            }
        };

        var removeButton = new Button
        {
            Content = "Remove",
            Margin = new Thickness(8, 0, 0, 0)
        };

        removeButton.Click += (_, _) =>
        {
            _boundViewModel?.RemoveCurvePointCommand.Execute(point);
        };

        Grid.SetColumn(tempLabel, 0);
        Grid.SetColumn(tempBox, 1);
        Grid.SetColumn(speedLabel, 2);
        Grid.SetColumn(speedBox, 3);
        Grid.SetColumn(removeButton, 4);

        row.Children.Add(tempLabel);
        row.Children.Add(tempBox);
        row.Children.Add(speedLabel);
        row.Children.Add(speedBox);
        row.Children.Add(removeButton);

        return row;
    }

    private void SyncCurveInputControls()
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _hysteresisSlider.Value = _boundViewModel.CurveHysteresisCelsius;
        _smoothingSlider.Value = _boundViewModel.CurveSmoothingFactor;
        _previewTemperatureSlider.Value = _boundViewModel.CurvePreviewTemperatureCelsius;

        _newPointTemperatureBox.Text = _boundViewModel.NewCurvePointTemperatureCelsius.ToString("F1", CultureInfo.InvariantCulture);
        _newPointSpeedBox.Text = _boundViewModel.NewCurvePointSpeedPercent.ToString(CultureInfo.InvariantCulture);
        _curveCpuConfirmCheck.IsChecked = _boundViewModel.CurveTestConfirmLowCpu;
    }

    private void UpdateCurveStatus()
    {
        if (_boundViewModel is null)
        {
            _curveValidationText.Text = "Krzywa niezaładowana.";
            _curveTestMessageText.Text = "";
            return;
        }

        _curveValidationText.Text = _boundViewModel.CurveValidationMessage;
        _curveTestMessageText.Text = _boundViewModel.CurveTestModeMessage;
    }

    private void UpdateCurveChart()
    {
        if (_boundViewModel is null)
        {
            _curveChartHost.Child = new TextBlock { Text = "Brak danych krzywej." };
            return;
        }

        var sortedPoints = _boundViewModel.CurvePoints
            .OrderBy(point => point.TemperatureCelsius)
            .ToArray();

        if (sortedPoints.Length == 0)
        {
            _curveChartHost.Child = new TextBlock
            {
                Text = "Dodaj punkty krzywej, aby zobaczyć wykres.",
                Foreground = new SolidColorBrush(Color.Parse("#757575"))
            };
            return;
        }

        const double width = 420;
        const double height = 210;
        const double left = 36;
        const double top = 12;
        const double right = width - 12;
        const double bottom = height - 28;

        var canvas = new Canvas
        {
            Width = width,
            Height = height
        };

        var axisColor = new SolidColorBrush(Color.Parse("#B0BEC5"));
        var curveColor = new SolidColorBrush(Color.Parse("#1E88E5"));

        var xAxis = new Line
        {
            StartPoint = new Point(left, bottom),
            EndPoint = new Point(right, bottom),
            Stroke = axisColor,
            StrokeThickness = 1
        };

        var yAxis = new Line
        {
            StartPoint = new Point(left, top),
            EndPoint = new Point(left, bottom),
            Stroke = axisColor,
            StrokeThickness = 1
        };

        canvas.Children.Add(xAxis);
        canvas.Children.Add(yAxis);

        var polylinePoints = new AvaloniaList<Point>();
        foreach (var point in sortedPoints)
        {
            var plotPoint = ToPlotPoint(point.TemperatureCelsius, point.SpeedPercent, left, top, right, bottom);
            polylinePoints.Add(plotPoint);

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Color.Parse("#1976D2"))
            };
            Canvas.SetLeft(dot, plotPoint.X - 3.5);
            Canvas.SetTop(dot, plotPoint.Y - 3.5);
            canvas.Children.Add(dot);
        }

        var curveLine = new Polyline
        {
            Points = polylinePoints,
            Stroke = curveColor,
            StrokeThickness = 2
        };

        canvas.Children.Add(curveLine);

        var previewTemp = _boundViewModel.CurvePreviewTemperatureCelsius;
        var previewSpeed = _boundViewModel.CurvePreviewSpeedPercent;
        var previewPoint = ToPlotPoint(previewTemp, previewSpeed, left, top, right, bottom);

        var previewLine = new Line
        {
            StartPoint = new Point(previewPoint.X, top),
            EndPoint = new Point(previewPoint.X, bottom),
            Stroke = new SolidColorBrush(Color.Parse("#FF7043")),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 }
        };

        var previewDot = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = new SolidColorBrush(Color.Parse("#FF7043"))
        };

        Canvas.SetLeft(previewDot, previewPoint.X - 4.5);
        Canvas.SetTop(previewDot, previewPoint.Y - 4.5);

        canvas.Children.Add(previewLine);
        canvas.Children.Add(previewDot);

        var legend = new TextBlock
        {
            Text =
                $"Preview {previewTemp:F1}C -> {previewSpeed}% | Points: {sortedPoints.Length}",
            Foreground = new SolidColorBrush(Color.Parse("#455A64")),
            FontSize = 12
        };
        Canvas.SetLeft(legend, left);
        Canvas.SetTop(legend, bottom + 6);
        canvas.Children.Add(legend);

        _curveChartHost.Child = canvas;
    }

    private static Point ToPlotPoint(
        double temperature,
        int speedPercent,
        double left,
        double top,
        double right,
        double bottom)
    {
        var temp = Math.Clamp(temperature, 0, 100);
        var speed = Math.Clamp(speedPercent, 0, 100);

        var x = left + ((temp / 100d) * (right - left));
        var y = bottom - ((speed / 100d) * (bottom - top));

        return new Point(x, y);
    }

    private static Control CreateFanChannelControl(FanChannelViewModel channel)
    {
        var cardStack = new StackPanel { Spacing = 8 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new TextBlock
        {
            Text = channel.Label,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1E1E1E"))
        };

        var status = new TextBlock
        {
            Text = channel.Status,
            FontSize = 12,
            Foreground = new SolidColorBrush(channel.CanControl ? Color.Parse("#2E7D32") : Color.Parse("#9E9E9E"))
        };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(status, 1);
        header.Children.Add(title);
        header.Children.Add(status);

        cardStack.Children.Add(header);

        var metrics = new TextBlock
        {
            Text = $"Current: {channel.CurrentRpm} RPM ({channel.CurrentPercent}%)",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#616161"))
        };
        cardStack.Children.Add(metrics);

        var speedGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,Auto,Auto,Auto") };

        var slider = new Slider
        {
            Minimum = channel.MinimumPercent,
            Maximum = channel.MaximumPercent,
            Value = channel.RequestedPercent,
            IsEnabled = channel.CanControl,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var numericInput = new TextBox
        {
            Text = channel.RequestedPercent.ToString(CultureInfo.InvariantCulture),
            Width = 76,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = channel.CanControl,
            Margin = new Thickness(0, 0, 8, 0)
        };

        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty)
            {
                return;
            }

            var value = (int)Math.Round(slider.Value, MidpointRounding.AwayFromZero);
            channel.RequestedPercent = value;
            numericInput.Text = value.ToString(CultureInfo.InvariantCulture);
        };

        numericInput.LostFocus += (_, _) =>
        {
            if (!int.TryParse(numericInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inputValue))
            {
                numericInput.Text = channel.RequestedPercent.ToString(CultureInfo.InvariantCulture);
                return;
            }

            var clamped = Math.Clamp(inputValue, channel.MinimumPercent, channel.MaximumPercent);
            channel.RequestedPercent = clamped;
            slider.Value = clamped;
            numericInput.Text = clamped.ToString(CultureInfo.InvariantCulture);
        };

        var applyButton = new Button
        {
            Content = "Apply",
            Command = channel.ApplyCommand,
            IsEnabled = channel.CanControl,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var resetButton = new Button
        {
            Content = "Reset",
            Command = channel.ResetCommand,
            IsEnabled = channel.CanControl,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var fullButton = new Button
        {
            Content = "Full Speed",
            Command = channel.FullSpeedCommand,
            IsEnabled = channel.CanControl
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(numericInput, 1);
        Grid.SetColumn(applyButton, 2);
        Grid.SetColumn(resetButton, 3);
        Grid.SetColumn(fullButton, 4);

        speedGrid.Children.Add(slider);
        speedGrid.Children.Add(numericInput);
        speedGrid.Children.Add(applyButton);
        speedGrid.Children.Add(resetButton);
        speedGrid.Children.Add(fullButton);

        cardStack.Children.Add(speedGrid);

        var groupGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,Auto,*") };

        var groupCombo = new ComboBox
        {
            ItemsSource = channel.AvailableGroups,
            SelectedItem = channel.AssignedGroup,
            IsEnabled = channel.CanControl
        };

        groupCombo.SelectionChanged += (_, _) =>
        {
            channel.AssignedGroup = groupCombo.SelectedItem?.ToString() ?? "None";
        };

        var assignGroupButton = new Button
        {
            Content = "Set Group",
            Command = channel.AssignGroupCommand,
            IsEnabled = channel.CanControl,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var groupHelp = new TextBlock
        {
            Text = "Kanały w tej samej grupie będą sterowane synchronicznie.",
            Foreground = new SolidColorBrush(Color.Parse("#757575")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        Grid.SetColumn(groupCombo, 0);
        Grid.SetColumn(assignGroupButton, 1);
        Grid.SetColumn(groupHelp, 2);

        groupGrid.Children.Add(groupCombo);
        groupGrid.Children.Add(assignGroupButton);
        groupGrid.Children.Add(groupHelp);

        cardStack.Children.Add(groupGrid);

        if (channel.IsCpuChannel)
        {
            var cpuConfirm = new CheckBox
            {
                Content = "Potwierdzam, że chcę ustawić CPU_FAN poniżej 30%",
                IsChecked = channel.ConfirmLowCpuUnder30,
                IsEnabled = channel.CanControl
            };

            cpuConfirm.IsCheckedChanged += (_, _) =>
                channel.ConfirmLowCpuUnder30 = cpuConfirm.IsChecked ?? false;
            cardStack.Children.Add(cpuConfirm);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = cardStack
        };
    }

    private static TextBlock CreateSectionHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1976D2")),
            Margin = new Thickness(0, 10, 0, 4)
        };
    }

    private static TextBlock CreateValueLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#333"))
        };
    }
}
