using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class ChannelWriteCooldownGate : IChannelWriteCooldownGate
{
    private readonly int _minimumWriteIntervalMs;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _channelLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DateTimeOffset> _lastWriteByChannel =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync = new();

    public ChannelWriteCooldownGate(IOptions<EcWriteSafetyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _minimumWriteIntervalMs = options.Value.GetSafeMinimumWriteIntervalMs();
    }

    public async Task WaitForTurnAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new ArgumentException("Channel id cannot be empty.", nameof(channelId));
        }

        if (_minimumWriteIntervalMs <= 0)
        {
            return;
        }

        var normalized = channelId.Trim();
        var channelLock = _channelLocks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTimeOffset.UtcNow;
            var minInterval = TimeSpan.FromMilliseconds(_minimumWriteIntervalMs);

            TimeSpan remainingDelay;
            lock (_sync)
            {
                if (_lastWriteByChannel.TryGetValue(normalized, out var previousWrite))
                {
                    var nextAllowedWrite = previousWrite + minInterval;
                    remainingDelay = nextAllowedWrite > now
                        ? nextAllowedWrite - now
                        : TimeSpan.Zero;
                }
                else
                {
                    remainingDelay = TimeSpan.Zero;
                }
            }

            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay, cancellationToken).ConfigureAwait(false);
            }

            lock (_sync)
            {
                _lastWriteByChannel[normalized] = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            channelLock.Release();
        }
    }
}
