namespace FanControlPro.Infrastructure.FanControl;

public readonly record struct EcWriteResult(bool Success, string Message)
{
    public static EcWriteResult Ok(string message = "Write successful.") => new(true, message);

    public static EcWriteResult Failed(string message) => new(false, message);
}

public readonly record struct EcReadResult(bool Success, byte Value, string Message)
{
    public static EcReadResult Ok(byte value, string message = "Read successful.") => new(true, value, message);

    public static EcReadResult Failed(string message) => new(false, 0, message);
}

public interface IEcRegisterAccess : IDisposable
{
    Task<EcWriteResult> WriteRegisterAsync(byte registerAddress, byte value, CancellationToken cancellationToken = default);

    Task<EcReadResult> ReadRegisterAsync(byte registerAddress, CancellationToken cancellationToken = default);
}
