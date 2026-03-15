using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FanControlPro.Application.Onboarding;
using FanControlPro.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace FanControlPro.Presentation;

public class MainWindow : Window
{
    private readonly IOnboardingService _onboardingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ContentControl _contentHost;
    private OnboardingViewModel? _onboardingViewModel;

    public MainWindow(
        IOnboardingService onboardingService,
        IServiceProvider serviceProvider)
    {
        _onboardingService = onboardingService;
        _serviceProvider = serviceProvider;

        _contentHost = new ContentControl();

        InitializeComponent();
        _ = InitializeStartupViewAsync();
    }

    private void InitializeComponent()
    {
        Title = "FanControl Pro v1.0";
        Width = 1000;
        Height = 600;
        Background = new SolidColorBrush(Color.Parse("#F5F5F5"));

        var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

        var topBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1976D2")),
            Padding = new Thickness(20, 15),
            Child = new TextBlock
            {
                Text = "FanControl Pro v1.0",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Colors.White)
            }
        };

        mainPanel.Children.Add(topBar);
        mainPanel.Children.Add(_contentHost);

        Content = mainPanel;
    }

    private async Task InitializeStartupViewAsync()
    {
        try
        {
            var onboardingCompleted = await _onboardingService.IsCompletedAsync();
            if (onboardingCompleted)
            {
                ShowDashboard();
            }
            else
            {
                ShowOnboarding();
            }
        }
        catch
        {
            ShowDashboard();
        }
    }

    private void ShowOnboarding()
    {
        _onboardingViewModel ??= _serviceProvider.GetRequiredService<OnboardingViewModel>();
        _onboardingViewModel.PropertyChanged -= OnOnboardingPropertyChanged;
        _onboardingViewModel.PropertyChanged += OnOnboardingPropertyChanged;

        _contentHost.Content = _serviceProvider.GetRequiredService<OnboardingView>();
    }

    private void ShowDashboard()
    {
        if (_onboardingViewModel is not null)
        {
            _onboardingViewModel.PropertyChanged -= OnOnboardingPropertyChanged;
        }

        _contentHost.Content = _serviceProvider.GetRequiredService<DashboardView>();
    }

    private void OnOnboardingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(OnboardingViewModel.IsCompleted), StringComparison.Ordinal))
        {
            return;
        }

        if (_onboardingViewModel?.IsCompleted == true)
        {
            ShowDashboard();
        }
    }
}
