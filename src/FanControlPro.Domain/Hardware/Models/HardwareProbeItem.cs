using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Domain.Hardware.Models;

public sealed record HardwareProbeItem(
    string Id,
    string Name,
    HardwareComponentType Type,
    string? Vendor,
    string? Model,
    IReadOnlyList<SensorSnapshot> Sensors,
    bool HasWritePath,
    bool IsWritePathValidated);
