namespace FanControlPro.Application.FanControl.Safety;

public enum SafeModeReason
{
    None = 0,
    SensorSuspicious = 1,
    SensorLoss = 2,
    InvalidSensorReading = 3,
    TemperatureThresholdExceeded = 4,
    ControllerUnavailable = 5,
    ManualEmergency = 6
}
