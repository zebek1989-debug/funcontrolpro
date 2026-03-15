using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Curves;
using Microsoft.Extensions.Logging;

namespace FanControlPro.Application.FanControl.Curves;

public sealed class FanCurveService : IFanCurveService
{
    private readonly ICurveEngine _curveEngine;
    private readonly IManualFanControlService _manualControlService;
    private readonly ILogger<FanCurveService> _logger;

    private readonly Dictionary<string, FanCurve> _curvesByChannel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CurveEvaluationState> _stateByChannel = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public FanCurveService(
        ICurveEngine curveEngine,
        IManualFanControlService manualControlService,
        ILogger<FanCurveService> logger)
    {
        _curveEngine = curveEngine;
        _manualControlService = manualControlService;
        _logger = logger;
    }

    public Task<FanCurve> GetOrCreateCurveAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_curvesByChannel.TryGetValue(channelId, out var existing))
            {
                return Task.FromResult(existing);
            }

            var created = CreateDefaultCurve(channelId, sensorId, isCpuChannel);
            _curvesByChannel[channelId] = created;
            _stateByChannel[channelId] = CurveEvaluationState.Empty;

            return Task.FromResult(created);
        }
    }

    public Task<CurveValidationResult> SaveCurveAsync(
        FanCurve curve,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = _curveEngine.ValidateCurve(curve);
        if (!validation.IsValid)
        {
            return Task.FromResult(validation);
        }

        lock (_sync)
        {
            _curvesByChannel[curve.ChannelId] = curve;
            _stateByChannel[curve.ChannelId] = CurveEvaluationState.Empty;
        }

        _logger.LogInformation("Saved fan curve for channel {ChannelId}", curve.ChannelId);
        return Task.FromResult(validation);
    }

    public Task<FanCurve> ResetToDefaultAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var curve = CreateDefaultCurve(channelId, sensorId, isCpuChannel);
            _curvesByChannel[channelId] = curve;
            _stateByChannel[channelId] = CurveEvaluationState.Empty;
            return Task.FromResult(curve);
        }
    }

    public async Task<CurveEvaluationResult> PreviewAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        double temperatureCelsius,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var curve = await GetOrCreateCurveAsync(channelId, sensorId, isCpuChannel, cancellationToken).ConfigureAwait(false);

        CurveEvaluationState state;
        lock (_sync)
        {
            state = _stateByChannel.TryGetValue(channelId, out var existing)
                ? existing
                : CurveEvaluationState.Empty;
        }

        var evaluation = _curveEngine.Evaluate(curve, temperatureCelsius, state);

        lock (_sync)
        {
            _stateByChannel[channelId] = evaluation.NextState;
        }

        return evaluation;
    }

    public async Task<FanControlResult> RunTestModeAsync(
        string channelId,
        string sensorId,
        bool isCpuChannel,
        double temperatureCelsius,
        bool confirmLowCpuFan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var evaluation = await PreviewAsync(
            channelId,
            sensorId,
            isCpuChannel,
            temperatureCelsius,
            cancellationToken).ConfigureAwait(false);

        var applyResult = await _manualControlService.SetSpeedAsync(
            channelId,
            evaluation.AppliedSpeedPercent,
            confirmLowCpuFan,
            cancellationToken).ConfigureAwait(false);

        if (!applyResult.Success)
        {
            return applyResult;
        }

        return FanControlResult.Succeeded(
            applyResult.AppliedPercent,
            $"Curve test applied at {temperatureCelsius:F1}C -> {applyResult.AppliedPercent}%.",
            previousPercent: applyResult.PreviousPercent);
    }

    private static FanCurve CreateDefaultCurve(string channelId, string sensorId, bool isCpuChannel)
    {
        var minPercent = isCpuChannel ? 20 : 0;

        var points = isCpuChannel
            ? new[]
            {
                new FanCurvePoint(30, 20),
                new FanCurvePoint(45, 35),
                new FanCurvePoint(60, 55),
                new FanCurvePoint(75, 80),
                new FanCurvePoint(90, 100)
            }
            : new[]
            {
                new FanCurvePoint(30, 0),
                new FanCurvePoint(45, 25),
                new FanCurvePoint(60, 45),
                new FanCurvePoint(75, 70),
                new FanCurvePoint(90, 100)
            };

        return new FanCurve(
            ChannelId: channelId,
            SensorId: sensorId,
            Points: points,
            HysteresisCelsius: 2,
            SmoothingFactor: 0.35,
            MinimumAllowedPercent: minPercent,
            MaximumAllowedPercent: 100);
    }
}
