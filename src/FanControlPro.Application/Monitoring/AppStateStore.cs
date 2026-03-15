using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FanControlPro.Application.Monitoring;

public sealed class AppStateStore : IAppStateStore, INotifyPropertyChanged
{
    private MonitoringSnapshot _currentSnapshot = MonitoringSnapshot.Empty;

    public MonitoringSnapshot CurrentSnapshot
    {
        get => _currentSnapshot;
        private set
        {
            if (ReferenceEquals(_currentSnapshot, value))
            {
                return;
            }

            _currentSnapshot = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<MonitoringSnapshot>? SnapshotUpdated;

    public void Publish(MonitoringSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        CurrentSnapshot = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
