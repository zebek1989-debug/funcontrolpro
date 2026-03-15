using CommunityToolkit.Mvvm.ComponentModel;

namespace FanControlPro.Presentation.ViewModels
{
    /// <summary>
    /// Presentation model dla system load
    /// </summary>
    public class SystemLoadViewModel : ObservableObject
    {
        private double _cpuLoad;
        private double _gpuLoad;
        private double _ramUsage;

        public double CpuLoad
        {
            get => _cpuLoad;
            set => SetProperty(ref _cpuLoad, value);
        }

        public double GpuLoad
        {
            get => _gpuLoad;
            set => SetProperty(ref _gpuLoad, value);
        }

        public double RamUsage
        {
            get => _ramUsage;
            set => SetProperty(ref _ramUsage, value);
        }
    }
}
