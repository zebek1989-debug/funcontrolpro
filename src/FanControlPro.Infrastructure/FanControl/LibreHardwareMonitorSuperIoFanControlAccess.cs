using FanControlPro.Domain.FanControl;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class LibreHardwareMonitorSuperIoFanControlAccess : ISuperIoFanControlAccess
{
    private readonly EcWriteSafetyOptions _options;
    private readonly ILogger<LibreHardwareMonitorSuperIoFanControlAccess> _logger;
    private readonly IReadOnlyDictionary<string, string> _selectorsByChannel;
    private readonly string? _requiredChipToken;

    public LibreHardwareMonitorSuperIoFanControlAccess(
        IOptions<EcWriteSafetyOptions> options,
        ILogger<LibreHardwareMonitorSuperIoFanControlAccess> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _selectorsByChannel = _options.GetAsusControlSelectors();
        _requiredChipToken = _options.GetSafeRequiredSuperIoChipToken();
    }

    public Task<SuperIoWriteAttemptResult> TrySetSpeedAsync(
        FanChannel channel,
        int percent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableHardwareAccess)
        {
            return Task.FromResult(SuperIoWriteAttemptResult.PathUnavailable("Hardware access disabled."));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(SuperIoWriteAttemptResult.PathUnavailable("Super I/O control requires Windows."));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ExecuteWithComputer(computer =>
            {
                if (!TryResolveControlSensor(computer, channel, out var resolved, out var reason))
                {
                    return SuperIoWriteAttemptResult.PathUnavailable(reason);
                }

                if (!TrySetSoftwareControlPercent(resolved.ControlObject, percent, out var writeReason))
                {
                    return SuperIoWriteAttemptResult.Failed(writeReason);
                }

                resolved.Hardware.Update();
                var applied = TryReadControlPercent(resolved.Sensor, out var observedPercent)
                    ? observedPercent
                    : percent;

                return SuperIoWriteAttemptResult.Succeeded(
                    applied,
                    $"Super I/O write applied for {resolved.Sensor.Name}.");
            });
        }, cancellationToken);
    }

    public Task<SuperIoReadAttemptResult> TryReadSpeedPercentAsync(
        FanChannel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableHardwareAccess)
        {
            return Task.FromResult(SuperIoReadAttemptResult.PathUnavailable("Hardware access disabled."));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(SuperIoReadAttemptResult.PathUnavailable("Super I/O control requires Windows."));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ExecuteWithComputer(computer =>
            {
                if (!TryResolveControlSensor(computer, channel, out var resolved, out var reason))
                {
                    return SuperIoReadAttemptResult.PathUnavailable(reason);
                }

                resolved.Hardware.Update();
                if (!TryReadControlPercent(resolved.Sensor, out var percent))
                {
                    return SuperIoReadAttemptResult.Failed(
                        $"Control sensor value is unavailable for {resolved.Sensor.Name}.");
                }

                return SuperIoReadAttemptResult.Succeeded(percent, "Super I/O control sensor read successful.");
            });
        }, cancellationToken);
    }

    private static T ExecuteWithComputer<T>(Func<Computer, T> operation)
    {
        var computer = new Computer
        {
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = false,
            IsMemoryEnabled = false
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

    private bool TryResolveControlSensor(
        Computer computer,
        FanChannel channel,
        out ResolvedControlSensor resolved,
        out string reason)
    {
        resolved = default;
        reason = string.Empty;

        var candidates = EnumerateSensors(computer)
            .Where(entry => entry.Sensor.SensorType == SensorType.Control)
            .Where(entry => IsSuperIoCandidate(entry.Hardware))
            .Select(entry => new
            {
                entry.Hardware,
                entry.Sensor,
                ControlObject = GetControlObject(entry.Sensor),
                SensorIdentifier = entry.Sensor.Identifier.ToString()
            })
            .Where(entry => entry.ControlObject is not null)
            .ToArray();

        if (candidates.Length == 0)
        {
            reason = "No writable Super I/O control sensors were discovered.";
            return false;
        }

        var selector = ResolveSelector(channel);
        if (!string.IsNullOrWhiteSpace(selector))
        {
            var bySelector = candidates.FirstOrDefault(entry =>
                entry.Sensor.Name.Contains(selector, StringComparison.OrdinalIgnoreCase) ||
                entry.SensorIdentifier.Contains(selector, StringComparison.OrdinalIgnoreCase));

            if (bySelector is not null)
            {
                resolved = new ResolvedControlSensor(bySelector.Hardware, bySelector.Sensor, bySelector.ControlObject!);
                return true;
            }

            reason = $"No Super I/O control sensor matched selector '{selector}' for channel '{channel.Id}'.";
            return false;
        }

        var tokenMatched = candidates
            .FirstOrDefault(entry => MatchesChannelHeuristics(channel, entry.Sensor.Name, entry.SensorIdentifier));

        if (tokenMatched is not null)
        {
            resolved = new ResolvedControlSensor(tokenMatched.Hardware, tokenMatched.Sensor, tokenMatched.ControlObject!);
            return true;
        }

        resolved = new ResolvedControlSensor(candidates[0].Hardware, candidates[0].Sensor, candidates[0].ControlObject!);
        reason = "Using fallback Super I/O control sensor because no heuristic match was found.";
        return true;
    }

    private bool IsSuperIoCandidate(IHardware hardware)
    {
        var typeName = hardware.HardwareType.ToString();
        var superIoType = typeName.Contains("SuperIO", StringComparison.OrdinalIgnoreCase);

        var nameOrParentSuggestsSuperIo =
            HardwareOrParentsContain(hardware, "Nuvoton") ||
            HardwareOrParentsContain(hardware, "ITE") ||
            HardwareOrParentsContain(hardware, "Fintek") ||
            HardwareOrParentsContain(hardware, "Super I/O") ||
            HardwareOrParentsContain(hardware, "SuperIO");

        if (!superIoType && !nameOrParentSuggestsSuperIo)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_requiredChipToken))
        {
            return true;
        }

        return HardwareOrParentsContain(hardware, _requiredChipToken);
    }

    private static bool HardwareOrParentsContain(IHardware hardware, string token)
    {
        var current = hardware;
        while (current is not null)
        {
            if (current.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                current.Identifier.ToString().Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private string? ResolveSelector(FanChannel channel)
    {
        if (_selectorsByChannel.TryGetValue(channel.Id, out var selector) &&
            !string.IsNullOrWhiteSpace(selector))
        {
            return selector;
        }

        return null;
    }

    private static bool MatchesChannelHeuristics(FanChannel channel, string sensorName, string sensorIdentifier)
    {
        var text = $"{sensorName} {sensorIdentifier}";

        static bool Match(string input, params string[] tokens) =>
            tokens.Any(token => input.Contains(token, StringComparison.OrdinalIgnoreCase));

        if (channel.Id.Contains("cpu", StringComparison.OrdinalIgnoreCase) ||
            channel.Name.Contains("cpu", StringComparison.OrdinalIgnoreCase))
        {
            return Match(text, "cpu", "cpu fan");
        }

        if (channel.Id.Contains("rear", StringComparison.OrdinalIgnoreCase) ||
            channel.Name.Contains("rear", StringComparison.OrdinalIgnoreCase))
        {
            return Match(text, "rear", "cha2", "chassis #2", "chassis2");
        }

        if (channel.Id.Contains("system", StringComparison.OrdinalIgnoreCase) ||
            channel.Name.Contains("system", StringComparison.OrdinalIgnoreCase) ||
            channel.Name.Contains("case", StringComparison.OrdinalIgnoreCase))
        {
            return Match(text, "system", "cha1", "chassis #1", "chassis1", "case");
        }

        return Match(text, channel.Name);
    }

    private bool TrySetSoftwareControlPercent(object controlObject, int percent, out string reason)
    {
        var controlType = controlObject.GetType();

        TrySetControlModeSoftware(controlObject);

        var setSoftwareMethod = controlType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
                string.Equals(method.Name, "SetSoftware", StringComparison.Ordinal) &&
                method.GetParameters().Length == 1);

        if (setSoftwareMethod is null)
        {
            reason = $"Control object '{controlType.Name}' does not expose SetSoftware.";
            return false;
        }

        var parameter = setSoftwareMethod.GetParameters()[0];
        if (!TryConvertPercent(percent, parameter.ParameterType, out var converted))
        {
            reason = $"Unsupported SetSoftware parameter type '{parameter.ParameterType.Name}'.";
            return false;
        }

        try
        {
            setSoftwareMethod.Invoke(controlObject, new[] { converted });
            reason = "SetSoftware invoked.";
            return true;
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogWarning(ex, "Super I/O SetSoftware invocation failed.");
            reason = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Super I/O SetSoftware invocation failed.");
            reason = ex.Message;
            return false;
        }
    }

    private static void TrySetControlModeSoftware(object controlObject)
    {
        var controlType = controlObject.GetType();
        var modeProperty = controlType.GetProperty("ControlMode", BindingFlags.Instance | BindingFlags.Public);
        if (modeProperty is null || !modeProperty.CanWrite)
        {
            return;
        }

        var modeType = modeProperty.PropertyType;
        if (!modeType.IsEnum)
        {
            return;
        }

        var software = Enum.GetNames(modeType)
            .FirstOrDefault(name => string.Equals(name, "Software", StringComparison.OrdinalIgnoreCase));

        if (software is null)
        {
            return;
        }

        var enumValue = Enum.Parse(modeType, software, ignoreCase: true);
        modeProperty.SetValue(controlObject, enumValue);
    }

    private static bool TryConvertPercent(int percent, Type targetType, out object? converted)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (effectiveType == typeof(float))
        {
            converted = (float)clamped;
            return true;
        }

        if (effectiveType == typeof(double))
        {
            converted = (double)clamped;
            return true;
        }

        if (effectiveType == typeof(byte))
        {
            converted = (byte)clamped;
            return true;
        }

        if (effectiveType == typeof(short))
        {
            converted = (short)clamped;
            return true;
        }

        if (effectiveType == typeof(ushort))
        {
            converted = (ushort)clamped;
            return true;
        }

        if (effectiveType == typeof(int))
        {
            converted = clamped;
            return true;
        }

        converted = null;
        return false;
    }

    private static object? GetControlObject(ISensor sensor)
    {
        var property = sensor.GetType().GetProperty("Control", BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(sensor);
    }

    private static bool TryReadControlPercent(ISensor sensor, out int percent)
    {
        percent = 0;
        if (sensor.Value is null)
        {
            return false;
        }

        percent = Math.Clamp((int)Math.Round(sensor.Value.Value, MidpointRounding.AwayFromZero), 0, 100);
        return true;
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

    private readonly record struct ResolvedControlSensor(
        IHardware Hardware,
        ISensor Sensor,
        object ControlObject);
}
