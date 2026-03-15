using System.Globalization;
using FanControlPro.Application.HardwareDetection;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Domain.Hardware.Models;
using LibreHardwareMonitor.Hardware;

namespace FanControlPro.Infrastructure.HardwareDetection;

public sealed class LibreHardwareMonitorProbe : IHardwareProbe
{
    public Task<IReadOnlyList<HardwareProbeItem>> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<HardwareProbeItem>>(Array.Empty<HardwareProbeItem>());
        }

        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsMemoryEnabled = true,
            IsControllerEnabled = true
        };

        computer.Open();
        try
        {
            var probed = new List<HardwareProbeItem>();
            foreach (var hardware in computer.Hardware)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TraverseHardware(hardware, probed);
            }

            return Task.FromResult<IReadOnlyList<HardwareProbeItem>>(probed);
        }
        finally
        {
            computer.Close();
        }
    }

    private static void TraverseHardware(IHardware hardware, ICollection<HardwareProbeItem> output)
    {
        hardware.Update();

        foreach (var subHardware in hardware.SubHardware)
        {
            TraverseHardware(subHardware, output);
        }

        var sensors = hardware.Sensors
            .Where(sensor => sensor.Value.HasValue)
            .Select(MapSensor)
            .ToArray();

        var hasWritePath = sensors.Any(sensor => sensor.IsWritable);

        output.Add(new HardwareProbeItem(
            Id: hardware.Identifier.ToString(),
            Name: hardware.Name,
            Type: MapHardwareType(hardware.HardwareType),
            Vendor: GuessVendor(hardware.Name),
            Model: hardware.Name,
            Sensors: sensors,
            HasWritePath: hasWritePath,
            IsWritePathValidated: false));
    }

    private static SensorSnapshot MapSensor(ISensor sensor)
    {
        var value = Convert.ToDouble(sensor.Value ?? 0f, CultureInfo.InvariantCulture);
        var kind = MapSensorKind(sensor.SensorType);

        var isWritable = kind == SensorKind.ControlPercent ||
                         sensor.GetType().GetProperty("Control")?.GetValue(sensor) is not null;

        return new SensorSnapshot(
            Id: sensor.Identifier.ToString(),
            Name: sensor.Name,
            Kind: kind,
            Value: value,
            Unit: GetSensorUnit(kind),
            IsWritable: isWritable);
    }

    private static HardwareComponentType MapHardwareType(HardwareType hardwareType) =>
        hardwareType.ToString() switch
        {
            "Cpu" => HardwareComponentType.Cpu,
            "GpuNvidia" or "GpuAmd" or "GpuIntel" => HardwareComponentType.Gpu,
            "Mainboard" or "Motherboard" => HardwareComponentType.Motherboard,
            "Storage" => HardwareComponentType.Storage,
            "Memory" => HardwareComponentType.Memory,
            "SuperIO" or "EmbeddedController" => HardwareComponentType.Controller,
            "Network" => HardwareComponentType.Network,
            _ => HardwareComponentType.Unknown
        };

    private static SensorKind MapSensorKind(SensorType sensorType) =>
        sensorType.ToString() switch
        {
            "Temperature" => SensorKind.Temperature,
            "Fan" => SensorKind.FanRpm,
            "Load" => SensorKind.Load,
            "Control" => SensorKind.ControlPercent,
            "Voltage" => SensorKind.Voltage,
            "Power" => SensorKind.Power,
            _ => SensorKind.Unknown
        };

    private static string GetSensorUnit(SensorKind kind) =>
        kind switch
        {
            SensorKind.Temperature => "C",
            SensorKind.FanRpm => "RPM",
            SensorKind.Load => "%",
            SensorKind.ControlPercent => "%",
            SensorKind.Voltage => "V",
            SensorKind.Power => "W",
            _ => string.Empty
        };

    private static string? GuessVendor(string hardwareName)
    {
        if (hardwareName.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
            return "ASUS";
        }

        if (hardwareName.Contains("MSI", StringComparison.OrdinalIgnoreCase))
        {
            return "MSI";
        }

        if (hardwareName.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) ||
            hardwareName.Contains("AORUS", StringComparison.OrdinalIgnoreCase))
        {
            return "Gigabyte";
        }

        if (hardwareName.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            return "AMD";
        }

        if (hardwareName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        if (hardwareName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return "NVIDIA";
        }

        return null;
    }
}
