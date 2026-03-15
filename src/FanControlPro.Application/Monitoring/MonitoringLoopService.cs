using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FanControlPro.Application.Configuration;
using System.IO;

namespace FanControlPro.Application.Monitoring;

public sealed class MonitoringLoopService : IMonitoringLoopService
{
    private readonly IMonitoringSampler _sampler;
    private readonly IAppStateStore _appStateStore;
    private readonly IOptionsMonitor<MonitoringOptions> _optionsMonitor;
    private readonly IApplicationSettingsService? _settingsService;
    private readonly ILogger<MonitoringLoopService> _logger;
    private readonly object _sync = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _fallbackTelemetryModeEnabled;

    public MonitoringLoopService(
        IMonitoringSampler sampler,
        IAppStateStore appStateStore,
        IOptionsMonitor<MonitoringOptions> optionsMonitor,
        ILogger<MonitoringLoopService> logger,
        IApplicationSettingsService? settingsService = null)
    {
        _sampler = sampler;
        _appStateStore = appStateStore;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _settingsService = settingsService;
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _loopTask is { IsCompleted: false };
            }
        }
    }

    public Task StartAsync(MonitoringTargets targets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        lock (_sync)
        {
            if (_loopTask is { IsCompleted: false })
            {
                return Task.CompletedTask;
            }

            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunAsync(targets, _loopCts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            loopTask = _loopTask;
            cts = _loopCts;
            _loopTask = null;
            _loopCts = null;
        }

        if (loopTask is null)
        {
            return;
        }

        cts?.Cancel();

        try
        {
            await loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Loop cancellation is expected during shutdown.
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private async Task RunAsync(MonitoringTargets targets, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_fallbackTelemetryModeEnabled)
            {
                _appStateStore.Publish(CreateFallbackSnapshot());
                await DelayByConfiguredIntervalAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var snapshot = await _sampler.CaptureAsync(targets, cancellationToken).ConfigureAwait(false);
                _appStateStore.Publish(snapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (TryEnableFallbackTelemetry(ex))
                {
                    _appStateStore.Publish(CreateFallbackSnapshot());
                }
                else
                {
                    _logger.LogError(ex, "Monitoring sampling loop failed.");
                }
            }

            await DelayByConfiguredIntervalAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DelayByConfiguredIntervalAsync(CancellationToken cancellationToken)
    {
        var refreshIntervalSeconds = _settingsService?.Current.PollingIntervalSeconds
            ?? _optionsMonitor.CurrentValue.GetSafeRefreshIntervalSeconds();
        refreshIntervalSeconds = Math.Clamp(refreshIntervalSeconds, 1, 5);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(refreshIntervalSeconds), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is expected during shutdown.
        }
    }

    private bool TryEnableFallbackTelemetry(Exception exception)
    {
        if (!IsMissingLibreHardwareMonitorDependency(exception))
        {
            return false;
        }

        if (_fallbackTelemetryModeEnabled)
        {
            return true;
        }

        _fallbackTelemetryModeEnabled = true;
        _logger.LogWarning(
            exception,
            "LibreHardwareMonitor backend unavailable. Falling back to zero telemetry to keep startup stable.");

        return true;
    }

    private static bool IsMissingLibreHardwareMonitorDependency(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is FileNotFoundException fileNotFound &&
                ContainsLibreHardwareMonitorToken(fileNotFound.FileName, fileNotFound.Message))
            {
                return true;
            }

            if (current is TypeLoadException typeLoad &&
                ContainsLibreHardwareMonitorToken(typeLoad.TypeName, typeLoad.Message))
            {
                return true;
            }

            if (current is DllNotFoundException dllNotFound &&
                ContainsLibreHardwareMonitorToken(dllNotFound.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLibreHardwareMonitorToken(params string?[] values)
    {
        foreach (var value in values)
        {
            if (value?.Contains("LibreHardwareMonitor", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static MonitoringSnapshot CreateFallbackSnapshot()
    {
        return new MonitoringSnapshot(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            TemperaturesCelsius: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            FanSpeedsRpm: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            SystemLoad: new SystemLoadSnapshot(0, 0, 0),
            ValidationIssues: Array.Empty<SensorValidationIssue>(),
            FaultySensorIds: Array.Empty<string>());
    }
}
