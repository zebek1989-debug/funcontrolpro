using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using FanControlPro.Application.Onboarding;
using FanControlPro.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FanControlPro.Presentation;

public class OnboardingView : UserControl
{
    private readonly OnboardingViewModel _viewModel;
    private readonly StackPanel _contentPanel;
    private readonly Button _nextButton;
    private readonly Button _previousButton;
    private readonly ProgressBar _progressBar;

    public OnboardingView()
    {
        _viewModel = App.Host!.Services.GetRequiredService<OnboardingViewModel>();
        DataContext = _viewModel;

        _contentPanel = new StackPanel { Spacing = 20, Margin = new Thickness(20) };
        _nextButton = new Button { Content = "Dalej", HorizontalAlignment = HorizontalAlignment.Right };
        _previousButton = new Button { Content = "Wstecz", HorizontalAlignment = HorizontalAlignment.Left, IsVisible = false };
        _progressBar = new ProgressBar { Height = 4, IsIndeterminate = false };

        InitializeComponent();
        SetupBindings();
        _ = _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private void InitializeComponent()
    {
        var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

        // Progress bar
        mainPanel.Children.Add(_progressBar);

        // Content area
        var scrollViewer = new ScrollViewer
        {
            Content = _contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        mainPanel.Children.Add(scrollViewer);

        // Navigation buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        buttonPanel.Children.Add(_previousButton);
        buttonPanel.Children.Add(_nextButton);

        mainPanel.Children.Add(buttonPanel);

        Content = mainPanel;
    }

    private void SetupBindings()
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _nextButton.Click += OnNextButtonClick;
        _previousButton.Click += OnPreviousButtonClick;

        UpdateContent();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingViewModel.CurrentStep) ||
            e.PropertyName == nameof(OnboardingViewModel.IsCompleted) ||
            e.PropertyName == nameof(OnboardingViewModel.ClassificationSummary) ||
            e.PropertyName == nameof(OnboardingViewModel.EmptyStateMessage) ||
            e.PropertyName == nameof(OnboardingViewModel.RiskAcceptanceMessage) ||
            e.PropertyName == nameof(OnboardingViewModel.HasAcceptedRisk) ||
            e.PropertyName == nameof(OnboardingViewModel.RequiresControlConsent) ||
            e.PropertyName == nameof(OnboardingViewModel.HasVendorSoftwareConflict) ||
            e.PropertyName == nameof(OnboardingViewModel.HardwareDetectionMessage))
        {
            UpdateContent();
            return;
        }

        if (e.PropertyName == nameof(OnboardingViewModel.CanProceed))
        {
            UpdateButtons();
        }
    }

    private void UpdateContent()
    {
        _contentPanel.Children.Clear();

        switch (_viewModel.CurrentStep)
        {
            case OnboardingStep.Welcome:
                ShowWelcomeStep();
                break;
            case OnboardingStep.HardwareDetection:
                ShowHardwareDetectionStep();
                break;
            case OnboardingStep.HardwareClassification:
                ShowHardwareClassificationStep();
                break;
            case OnboardingStep.RiskAcceptance:
                ShowRiskAcceptanceStep();
                break;
            case OnboardingStep.Completed:
                ShowCompletedStep();
                break;
        }

        UpdateProgressBar();
        UpdateButtons();
    }

    private void ShowWelcomeStep()
    {
        var title = new TextBlock
        {
            Text = "Witaj w FanControl Pro",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var message = new TextBlock
        {
            Text = _viewModel.WelcomeMessage,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20
        };

        _contentPanel.Children.Add(title);
        _contentPanel.Children.Add(message);
    }

    private void ShowHardwareDetectionStep()
    {
        var title = new TextBlock
        {
            Text = "Wykrywanie sprzętu",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var message = new TextBlock
        {
            Text = _viewModel.HardwareDetectionMessage,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20
        };

        var progressRing = new ProgressBar
        {
            IsIndeterminate = _viewModel.IsDetectingHardware,
            Height = 20,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        _contentPanel.Children.Add(title);
        _contentPanel.Children.Add(message);
        _contentPanel.Children.Add(progressRing);
    }

    private void ShowHardwareClassificationStep()
    {
        var title = new TextBlock
        {
            Text = "Klasyfikacja sprzętu",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var summary = new TextBlock
        {
            Text = _viewModel.ClassificationSummary,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 20)
        };

        _contentPanel.Children.Add(title);
        _contentPanel.Children.Add(summary);

        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12)
        };

        if (_viewModel.HasFullControlComponents)
        {
            badges.Children.Add(CreateBadge("Full Control", "#2E7D32"));
        }

        if (_viewModel.HasMonitoringOnlyComponents)
        {
            badges.Children.Add(CreateBadge("Monitoring Only", "#EF6C00"));
        }

        if (_viewModel.HasUnsupportedComponents)
        {
            badges.Children.Add(CreateBadge("Unsupported", "#C62828"));
        }

        _contentPanel.Children.Add(badges);

        // Lista komponentów
        var componentsList = new ListBox
        {
            ItemsSource = _viewModel.HardwareComponents,
            Margin = new Thickness(0, 20, 0, 0)
        };

        componentsList.ItemTemplate = new FuncDataTemplate<HardwareComponentViewModel>((item, _) =>
        {
            var panel = new StackPanel { Spacing = 5, Margin = new Thickness(10) };

            var header = new TextBlock
            {
                Text = $"{item.ComponentName} ({item.ComponentType})",
                FontWeight = FontWeight.Bold
            };

            var level = new TextBlock
            {
                Text = item.LevelDisplayText,
                Foreground = new SolidColorBrush(Color.Parse(item.LevelColor))
            };

            var reason = new TextBlock
            {
                Text = item.Reason,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            };

            panel.Children.Add(header);
            panel.Children.Add(level);
            panel.Children.Add(reason);

            return panel;
        });

        _contentPanel.Children.Add(componentsList);

        var docsButton = new Button
        {
            Content = "Otworz liste kompatybilnosci",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0)
        };
        docsButton.Click += (_, _) => _ = _viewModel.OpenCompatibilityDocumentationCommand.ExecuteAsync(null);
        _contentPanel.Children.Add(docsButton);

        if (!string.IsNullOrWhiteSpace(_viewModel.EmptyStateMessage))
        {
            var emptyState = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFF3E0")),
                BorderBrush = new SolidColorBrush(Color.Parse("#FFB74D")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0),
                Child = new TextBlock
                {
                    Text = _viewModel.EmptyStateMessage,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#5D4037"))
                }
            };

            _contentPanel.Children.Add(emptyState);
        }
    }

    private void ShowRiskAcceptanceStep()
    {
        var title = new TextBlock
        {
            Text = "Ostrzeżenie bezpieczeństwa",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#F44336")),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var message = new TextBlock
        {
            Text = _viewModel.RiskAcceptanceMessage,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var checkBox = new CheckBox
        {
            Content = "Rozumiem ryzyko i chcę kontynuować",
            IsChecked = _viewModel.HasAcceptedRisk,
            Margin = new Thickness(0, 20, 0, 0),
            IsEnabled = _viewModel.RequiresControlConsent
        };

        checkBox.IsCheckedChanged += (s, e) =>
        {
            _viewModel.HasAcceptedRisk = checkBox.IsChecked == true;
        };

        _contentPanel.Children.Add(title);
        _contentPanel.Children.Add(message);
        _contentPanel.Children.Add(checkBox);

        if (_viewModel.HasAcceptedRisk)
        {
            var revokeButton = new Button
            {
                Content = "Cofnij zgode i pozostan w Monitoring Only",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0)
            };
            revokeButton.Click += (_, _) => _ = _viewModel.RevokeRiskConsentCommand.ExecuteAsync(null);
            _contentPanel.Children.Add(revokeButton);
        }
    }

    private void ShowCompletedStep()
    {
        var title = new TextBlock
        {
            Text = "Konfiguracja zakończona!",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#4CAF50")),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var message = new TextBlock
        {
            Text = "FanControl Pro jest gotowy do użycia.\nMożesz teraz przejść do głównego interfejsu aplikacji.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _contentPanel.Children.Add(title);
        _contentPanel.Children.Add(message);
    }

    private void UpdateProgressBar()
    {
        var progress = _viewModel.CurrentStep switch
        {
            OnboardingStep.Welcome => 20,
            OnboardingStep.HardwareDetection => 40,
            OnboardingStep.HardwareClassification => 60,
            OnboardingStep.RiskAcceptance => 80,
            OnboardingStep.Completed => 100,
            _ => 0
        };

        _progressBar.Value = progress;
    }

    private void UpdateButtons()
    {
        _nextButton.IsEnabled = _viewModel.CanProceed;
        _nextButton.Content = _viewModel.CurrentStep == OnboardingStep.RiskAcceptance ? "Zakończ" : "Dalej";
        _previousButton.IsVisible = _viewModel.CurrentStep != OnboardingStep.Welcome;
        _nextButton.IsVisible = _viewModel.CurrentStep != OnboardingStep.Completed;
    }

    private void OnNextButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = _viewModel.NextStepCommand.ExecuteAsync(null);
    }

    private void OnPreviousButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = _viewModel.PreviousStepCommand.ExecuteAsync(null);
    }

    private static Border CreateBadge(string text, string backgroundHex)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(backgroundHex)),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12
            }
        };
    }
}
