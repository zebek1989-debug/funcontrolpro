using System.Globalization;
using FanControlPro.Application.Monitoring;
using LibreHardwareMonitor.Hardware;

namespace FanControlPro.Infrastructure.Monitoring;

public sealed class LibreHardwareMonitorSensorReader : ISensorReader
{
    public Task<double?> ReadTemperatureAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        return ReadSingleSensorAsync(sensorId, SensorType.Temperature, cancellationToken);
    }

    public async Task<int?> ReadFanSpeedAsync(string fanId, CancellationToken cancellationToken = default)
    {
        var value = await ReadSingleSensorAsync(fanId, SensorType.Fan, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            return null;
        }

        return (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    public Task<SystemLoadSnapshot> ReadSystemLoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
            {
                return new SystemLoadSnapshot(0, 0, 0);
            }

            return ExecuteWithComputer(computer =>
            {
                var loadSensors = EnumerateSensors(computer)
                    .Where(tuple => tuple.Sensor.SensorType == SensorType.Load && tuple.Sensor.Value.HasValue)
                    .ToArray();

                var cpuLoad = ResolveLoad(loadSensors.Where(IsCpuSensor).Select(tuple => tuple.Sensor));
                var gpuLoad = ResolveLoad(loadSensors.Where(IsGpuSensor).Select(tuple => tuple.Sensor));
                var memoryLoad = ResolveLoad(loadSensors.Where(IsMemorySensor).Select(tuple => tuple.Sensor));

                return new SystemLoadSnapshot(cpuLoad, gpuLoad, memoryLoad);
            });
        }, cancellationToken);
    }

    private static Task<double?> ReadSingleSensorAsync(
        string sensorId,
        SensorType sensorType,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
            {
                return (double?)null;
            }

            return ExecuteWithComputer(computer =>
            {
                var sensor = EnumerateSensors(computer)
                    .Select(tuple => tuple.Sensor)
                    .FirstOrDefault(sensor =>
                        sensor.SensorType == sensorType &&
                        string.Equals(sensor.Identifier.ToString(), sensorId, StringComparison.OrdinalIgnoreCase));

                if (sensor?.Value is null)
                {
                    return (double?)null;
                }

                return Convert.ToDouble(sensor.Value.Value, CultureInfo.InvariantCulture);
            });
        }, cancellationToken);
    }

    private static T ExecuteWithComputer<T>(Func<Computer, T> operation)
    {
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
            return operation(computer);
        }
        finally
        {
            computer.Close();
        }
    }

    private static IEnumerable<(IHardware Hardware, ISensor Sensor)> EnumerateSensors(Computer computer)
    {
        foreach (var hardware in computer.Hardware)
        {
            foreach (var tuple in EnumerateSensorsRecursive(hardware))
            {
                yield return tuple;
            }
        }
    }

    private static IEnumerable<(IHardware Hardware, ISensor Sensor)> EnumerateSensorsRecursive(IHardware hardware)
    {
        hardware.Update();

        foreach (var sensor in hardware.Sensors)
        {
            yield return (hardware, sensor);
        }

        foreach (var sub in hardware.SubHardware)
        {
            foreach (var tuple in EnumerateSensorsRecursive(sub))
            {
                yield return tuple;
            }
        }
    }

    private static bool IsCpuSensor((IHardware Hardware, ISensor Sensor) entry) =>
        entry.Hardware.HardwareType.ToString() == "Cpu";

    private static bool IsGpuSensor((IHardware Hardware, ISensor Sensor) entry)
    {
        var type = entry.Hardware.HardwareType.ToString();
        return type is "GpuNvidia" or "GpuAmd" or "GpuIntel";
    }

    private static bool IsMemorySensor((IHardware Hardware, ISensor Sensor) entry)
    {
        var hardwareType = entry.Hardware.HardwareType.ToString();
        return hardwareType == "Memory" ||
               entry.Sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase);
    }

    private static double ResolveLoad(IEnumerable<ISensor> sensors)
    {
        var materialized = sensors
            .Where(sensor => sensor.Value.HasValue)
            .ToArray();

        if (materialized.Length == 0)
        {
            return 0;
        }

        var total = materialized
            .FirstOrDefault(sensor => sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase));

        if (total?.Value is not null)
        {
            return total.Value.Value;
        }

        var average = materialized.Average(sensor => sensor.Value ?? 0f);
        return Math.Round(average, 2, MidpointRounding.AwayFromZero);
    }
}
