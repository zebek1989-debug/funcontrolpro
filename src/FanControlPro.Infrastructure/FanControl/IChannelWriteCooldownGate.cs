namespace FanControlPro.Infrastructure.FanControl;

public interface IChannelWriteCooldownGate
{
    Task WaitForTurnAsync(string channelId, CancellationToken cancellationToken = default);
}
