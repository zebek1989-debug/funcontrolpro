using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanControlPro.Presentation.ViewModels
{
    /// <summary>
    /// Presentation model dla temperatury sensora
    /// </summary>
    public class SensorViewModel : ObservableObject
    {
        private double _currentValue;
        private double _minValue;
        private double _maxValue;

        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        public double CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        public double MinValue
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value);
        }

        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        public override string ToString() => $"{Label}: {CurrentValue:F1}°C";
    }
}
