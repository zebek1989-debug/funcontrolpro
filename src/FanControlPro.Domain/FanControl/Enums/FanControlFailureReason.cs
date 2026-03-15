namespace FanControlPro.Domain.FanControl.Enums;

public enum FanControlFailureReason
{
    None = 0,
    MonitoringOnly = 1,
    UnsupportedChannel = 2,
    OutOfRange = 3,
    ConsentRequired = 4,
    ValidationFailed = 5,
    HardwareError = 6
}
