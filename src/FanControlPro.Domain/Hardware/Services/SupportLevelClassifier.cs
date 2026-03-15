using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;

namespace FanControlPro.Domain.Hardware.Services;

public static class SupportLevelClassifier
{
    public static SupportClassification Classify(HardwareProbeItem hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        var hasTelemetry = hardware.Sensors.Any(sensor =>
            sensor.Kind is SensorKind.Temperature or SensorKind.FanRpm or SensorKind.Load);

        if (hasTelemetry && hardware.HasWritePath && hardware.IsWritePathValidated)
        {
            return new SupportClassification(
                SupportLevel.FullControl,
                "Telemetry and validated write path are available.");
        }

        if (hasTelemetry)
        {
            if (hardware.HasWritePath && !hardware.IsWritePathValidated)
            {
                return new SupportClassification(
                    SupportLevel.MonitoringOnly,
                    "Write path is present but not validated yet.");
            }

            return new SupportClassification(
                SupportLevel.MonitoringOnly,
                "Read-only telemetry is available.");
        }

        return new SupportClassification(
            SupportLevel.Unsupported,
            "No reliable telemetry sensors found.");
    }
}
