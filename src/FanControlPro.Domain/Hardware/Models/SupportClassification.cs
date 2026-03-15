using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Domain.Hardware.Models;

public sealed record SupportClassification(
    SupportLevel Level,
    string Reason);
