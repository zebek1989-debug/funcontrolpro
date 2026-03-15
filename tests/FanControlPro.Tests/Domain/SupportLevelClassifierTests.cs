using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using FanControlPro.Domain.Hardware.Services;

namespace FanControlPro.Tests.Domain;

public sealed class SupportLevelClassifierTests
{
    [Fact]
    public void Should_Classify_AsFullControl_WhenTelemetryAndValidatedWritePathExist()
    {
        var hardware = CreateProbeItem(
            sensors: new[] { new SensorSnapshot("temp-1", "CPU Package", SensorKind.Temperature, 52.4, "C", false) },
            hasWritePath: true,
            isWritePathValidated: true);

        var result = SupportLevelClassifier.Classify(hardware);

        Assert.Equal(SupportLevel.FullControl, result.Level);
    }

    [Fact]
    public void Should_Classify_AsMonitoringOnly_WhenTelemetryExistsWithoutValidatedWritePath()
    {
        var hardware = CreateProbeItem(
            sensors: new[] { new SensorSnapshot("fan-1", "CPU Fan", SensorKind.FanRpm, 1200, "RPM", false) },
            hasWritePath: true,
            isWritePathValidated: false);

        var result = SupportLevelClassifier.Classify(hardware);

        Assert.Equal(SupportLevel.MonitoringOnly, result.Level);
        Assert.Contains("not validated", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_Classify_AsUnsupported_WhenNoTelemetrySensorsExist()
    {
        var hardware = CreateProbeItem(
            sensors: new[] { new SensorSnapshot("ctrl-1", "Control", SensorKind.ControlPercent, 30, "%", true) },
            hasWritePath: true,
            isWritePathValidated: true);

        var result = SupportLevelClassifier.Classify(hardware);

        Assert.Equal(SupportLevel.Unsupported, result.Level);
    }

    private static HardwareProbeItem CreateProbeItem(
        IReadOnlyList<SensorSnapshot> sensors,
        bool hasWritePath,
        bool isWritePathValidated)
    {
        return new HardwareProbeItem(
            Id: "hw-1",
            Name: "Mock Motherboard",
            Type: HardwareComponentType.Motherboard,
            Vendor: "Mock",
            Model: "Mock-1",
            Sensors: sensors,
            HasWritePath: hasWritePath,
            IsWritePathValidated: isWritePathValidated);
    }
}
