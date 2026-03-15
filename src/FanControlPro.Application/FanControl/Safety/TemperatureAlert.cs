namespace FanControlPro.Application.FanControl.Safety;

public sealed record TemperatureAlert(
    string SensorId,
    double CurrentCelsius,
    double ThresholdCelsius,
    bool IsEmergency);
