using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanControlPro.Presentation.ViewModels
{
    public class FanChannelViewModel : ObservableObject
    {
        private int _currentRpm;
        private int _currentPercent;
        private int _requestedPercent;
        private string _status = "Monitoring Only";
        private string _assignedGroup = "None";
        private bool _canControl;
        private bool _confirmLowCpuUnder30;

        public FanChannelViewModel(
            Func<FanChannelViewModel, Task> applyAsync,
            Func<FanChannelViewModel, Task> resetAsync,
            Func<FanChannelViewModel, Task> fullSpeedAsync,
            Func<FanChannelViewModel, Task> assignGroupAsync)
        {
            ApplyCommand = new AsyncRelayCommand(() => applyAsync(this));
            ResetCommand = new AsyncRelayCommand(() => resetAsync(this));
            FullSpeedCommand = new AsyncRelayCommand(() => fullSpeedAsync(this));
            AssignGroupCommand = new AsyncRelayCommand(() => assignGroupAsync(this));
        }

        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Type { get; set; } = "PWM";

        public int MinimumPercent { get; set; }

        public int MaximumPercent { get; set; } = 100;

        public bool IsCpuChannel { get; set; }

        public bool CanControl
        {
            get => _canControl;
            set => SetProperty(ref _canControl, value);
        }

        public int CurrentRpm
        {
            get => _currentRpm;
            set => SetProperty(ref _currentRpm, value);
        }

        public int CurrentPercent
        {
            get => _currentPercent;
            set => SetProperty(ref _currentPercent, value);
        }

        public int RequestedPercent
        {
            get => _requestedPercent;
            set => SetProperty(ref _requestedPercent, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string AssignedGroup
        {
            get => _assignedGroup;
            set => SetProperty(ref _assignedGroup, value);
        }

        public bool ConfirmLowCpuUnder30
        {
            get => _confirmLowCpuUnder30;
            set => SetProperty(ref _confirmLowCpuUnder30, value);
        }

        public IReadOnlyList<string> AvailableGroups { get; set; } = Array.Empty<string>();

        public IAsyncRelayCommand ApplyCommand { get; }

        public IAsyncRelayCommand ResetCommand { get; }

        public IAsyncRelayCommand FullSpeedCommand { get; }

        public IAsyncRelayCommand AssignGroupCommand { get; }

        public override string ToString() =>
            $"{Label}: {CurrentRpm} RPM ({CurrentPercent}%), Group={AssignedGroup}, Status={Status}";
    }
}
