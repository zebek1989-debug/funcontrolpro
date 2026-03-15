using CommunityToolkit.Mvvm.ComponentModel;

namespace FanControlPro.Presentation.ViewModels;

public sealed class FanCurvePointViewModel : ObservableObject
{
    private double _temperatureCelsius;
    private int _speedPercent;

    public double TemperatureCelsius
    {
        get => _temperatureCelsius;
        set => SetProperty(ref _temperatureCelsius, value);
    }

    public int SpeedPercent
    {
        get => _speedPercent;
        set => SetProperty(ref _speedPercent, value);
    }

    public override string ToString() =>
        $"{TemperatureCelsius:F1}C -> {SpeedPercent}%";
}
