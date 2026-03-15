using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Domain.Hardware.Models;

public sealed record DetectedHardware(
    string Id,
    string Name,
    HardwareComponentType Type,
    SupportLevel SupportLevel,
    string SupportReason,
    string? Vendor,
    string? Model,
    IReadOnlyList<SensorSnapshot> Sensors);
