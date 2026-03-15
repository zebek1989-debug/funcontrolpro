using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Curves;

namespace FanControlPro.Application.FanControl.Curves;

public interface IFanCurveService
{
    Task<FanCurve> GetOrCreateCurveAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        CancellationToken cancellationToken = default);

    Task<CurveValidationResult> SaveCurveAsync(
        FanCurve curve,
        CancellationToken cancellationToken = default);

    Task<FanCurve> ResetToDefaultAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        CancellationToken cancellationToken = default);

    Task<CurveEvaluationResult> PreviewAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        double temperatureCelsius,
        CancellationToken cancellationToken = default);

    Task<FanControlResult> RunTestModeAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        double temperatureCelsius,
        bool confirmLowCpuFan,
        CancellationToken cancellationToken = default);
}
