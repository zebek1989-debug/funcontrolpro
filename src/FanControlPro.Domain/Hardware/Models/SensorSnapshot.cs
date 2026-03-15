using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Domain.Hardware.Models;

public sealed record SensorSnapshot(
    string Id,
    string Name,
    SensorKind Kind,
    double Value,
    string Unit,
    bool IsWritable);
