namespace FanControlPro.Infrastructure.FanControl;

public sealed class EcWriteSafetyOptions
{
    public bool EnableHardwareAccess { get; set; }

    public bool PreferSuperIoControlPath { get; set; } = true;

    public string RequiredSuperIoChipToken { get; set; } = "NCT6798";

    public int MinimumWriteIntervalMs { get; set; } = 350;

    public int OperationTimeoutMs { get; set; } = 150;

    public int PollDelayMs { get; set; } = 1;

    public int RegisterScaleMaxValue { get; set; } = 100;

    public bool VerifyReadBack { get; set; } = true;

    public int ReadBackTolerancePercent { get; set; } = 4;

    // Keep this empty by default: register map must be validated on target hardware first.
    public Dictionary<string, int> AsusPwmRegisters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Optional hints for matching control sensors (name/identifier contains token).
    public Dictionary<string, string> AsusControlSensors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int GetSafeMinimumWriteIntervalMs() => Math.Clamp(MinimumWriteIntervalMs, 0, 5000);

    public int GetSafeOperationTimeoutMs() => Math.Clamp(OperationTimeoutMs, 10, 5000);

    public int GetSafePollDelayMs() => Math.Clamp(PollDelayMs, 0, 50);

    public int GetSafeRegisterScaleMaxValue() => Math.Clamp(RegisterScaleMaxValue, 1, 255);

    public int GetSafeReadBackTolerancePercent() => Math.Clamp(ReadBackTolerancePercent, 0, 25);

    public string? GetSafeRequiredSuperIoChipToken()
    {
        if (string.IsNullOrWhiteSpace(RequiredSuperIoChipToken))
        {
            return null;
        }

        return RequiredSuperIoChipToken.Trim();
    }

    public IReadOnlyDictionary<string, string> GetAsusControlSelectors()
    {
        if (AsusControlSensors.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return AsusControlSensors
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, byte> GetAsusRegisterMap()
    {
        if (AsusPwmRegisters.Count == 0)
        {
            return new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        }

        var normalized = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in AsusPwmRegisters)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (pair.Value is < 0 or > 0xFF)
            {
                continue;
            }

            normalized[pair.Key.Trim()] = (byte)pair.Value;
        }

        return normalized;
    }
}
