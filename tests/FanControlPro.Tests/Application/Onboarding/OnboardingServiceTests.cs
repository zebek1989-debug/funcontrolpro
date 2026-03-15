using FanControlPro.Application.HardwareDetection;
using FanControlPro.Application.Onboarding;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using Moq;

namespace FanControlPro.Tests.Application.Onboarding;

public sealed class OnboardingServiceTests
{
    private readonly Mock<IOnboardingStateStore> _stateStoreMock;
    private readonly Mock<IHardwareDetector> _hardwareDetectorMock;
    private readonly OnboardingService _service;

    public OnboardingServiceTests()
    {
        _stateStoreMock = new Mock<IOnboardingStateStore>();
        _hardwareDetectorMock = new Mock<IHardwareDetector>();
        _service = new OnboardingService(_stateStoreMock.Object, _hardwareDetectorMock.Object);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsStoredState()
    {
        // Arrange
        var expectedState = new OnboardingState(OnboardingStep.HardwareDetection, false, null);
        _stateStoreMock.Setup(x => x.LoadAsync(default)).ReturnsAsync(expectedState);

        // Act
        var result = await _service.GetStateAsync();

        // Assert
        Assert.Equal(expectedState, result);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsDefaultState_WhenNoStoredState()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.LoadAsync(default)).ReturnsAsync((OnboardingState?)null);

        // Act
        var result = await _service.GetStateAsync();

        // Assert
        Assert.Equal(OnboardingStep.Welcome, result.CurrentStep);
        Assert.False(result.IsCompleted);
        Assert.Null(result.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteStepAsync_AdvancesToNextStep()
    {
        // Arrange
        var initialState = new OnboardingState(OnboardingStep.Welcome, false, null);
        _stateStoreMock.Setup(x => x.LoadAsync(default)).ReturnsAsync(initialState);

        // Act
        await _service.CompleteStepAsync(OnboardingStep.Welcome);

        // Assert
        _stateStoreMock.Verify(x => x.SaveAsync(
            It.Is<OnboardingState>(s =>
                s.CurrentStep == OnboardingStep.HardwareDetection &&
                !s.IsCompleted),
            default), Times.Once);
    }

    [Fact]
    public async Task CompleteStepAsync_MarksAsCompleted_OnFinalStep()
    {
        // Arrange
        var initialState = new OnboardingState(OnboardingStep.RiskAcceptance, false, null);
        _stateStoreMock.Setup(x => x.LoadAsync(default)).ReturnsAsync(initialState);

        // Act
        await _service.CompleteStepAsync(OnboardingStep.RiskAcceptance);

        // Assert
        _stateStoreMock.Verify(x => x.SaveAsync(
            It.Is<OnboardingState>(s =>
                s.CurrentStep == OnboardingStep.Completed &&
                s.IsCompleted &&
                s.CompletedAtUtc.HasValue),
            default), Times.Once);
    }

    [Fact]
    public async Task IsCompletedAsync_ReturnsTrue_WhenCompleted()
    {
        // Arrange
        var completedState = new OnboardingState(OnboardingStep.Completed, true, DateTimeOffset.UtcNow);
        _stateStoreMock.Setup(x => x.LoadAsync(default)).ReturnsAsync(completedState);

        // Act
        var result = await _service.IsCompletedAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ClassifyHardwareAsync_ReturnsClassificationResult()
    {
        // Arrange
        var mockComponents = new[]
        {
            new DetectedHardware(
                Id: "cpu1",
                Name: "CPU",
                Type: HardwareComponentType.Cpu,
                SupportLevel: SupportLevel.FullControl,
                SupportReason: "Full control available",
                Vendor: "Intel",
                Model: "i7-10700K",
                Sensors: Array.Empty<SensorSnapshot>())
        };

        var detectionResult = new DetectionResult(
            DateTimeOffset.UtcNow,
            mockComponents,
            LoadedFromCache: false);
        _hardwareDetectorMock.Setup(x => x.DetectHardwareAsync(default)).ReturnsAsync(detectionResult);

        // Act
        var result = await _service.ClassifyHardwareAsync();

        // Assert
        Assert.Single(result.Components);
        Assert.Equal("CPU", result.Components[0].ComponentName);
        Assert.Equal("CPU", result.Components[0].ComponentType);
        Assert.Equal(SupportLevel.FullControl, result.Components[0].Level);
        Assert.True(result.HasFullControlComponents);
        Assert.False(result.HasMonitoringOnlyComponents);
        Assert.False(result.HasUnsupportedComponents);
    }
}
