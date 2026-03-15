using CommunityToolkit.Mvvm.ComponentModel;
using FanControlPro.Application.Onboarding;
using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Presentation.ViewModels;

public partial class HardwareComponentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _componentName;

    [ObservableProperty]
    private string _componentType;

    [ObservableProperty]
    private SupportLevel _supportLevel;

    [ObservableProperty]
    private string _reason;

    [ObservableProperty]
    private string _levelDisplayText;

    [ObservableProperty]
    private string _levelColor;

    public HardwareComponentViewModel(HardwareComponentClassification classification)
    {
        ComponentName = classification.ComponentName;
        ComponentType = classification.ComponentType;
        SupportLevel = classification.Level;
        Reason = classification.Reason;

        (LevelDisplayText, LevelColor) = SupportLevel switch
        {
            SupportLevel.FullControl => ("Pełna kontrola", "#4CAF50"),
            SupportLevel.MonitoringOnly => ("Tylko monitoring", "#FF9800"),
            SupportLevel.Unsupported => ("Nieobsługiwane", "#F44336"),
            _ => ("Nieznane", "#9E9E9E")
        };
    }
}