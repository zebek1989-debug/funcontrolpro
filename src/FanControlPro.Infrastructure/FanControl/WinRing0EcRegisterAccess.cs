using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace FanControlPro.Infrastructure.FanControl;

public sealed class WinRing0EcRegisterAccess : IEcRegisterAccess
{
    private const ushort EcDataPort = 0x62;
    private const ushort EcCommandPort = 0x66;
    private const byte EcReadCommand = 0x80;
    private const byte EcWriteCommand = 0x81;
    private const byte InputBufferFullMask = 0x02;
    private const byte OutputBufferFullMask = 0x01;

    private readonly ILogger<WinRing0EcRegisterAccess> _logger;
    private readonly int _operationTimeoutMs;
    private readonly int _pollDelayMs;
    private readonly object _stateSync = new();

    private bool _initAttempted;
    private bool _initialized;
    private bool _disposed;

    public WinRing0EcRegisterAccess(
        IOptions<EcWriteSafetyOptions> options,
        ILogger<WinRing0EcRegisterAccess> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        var value = options.Value;
        _operationTimeoutMs = value.GetSafeOperationTimeoutMs();
        _pollDelayMs = value.GetSafePollDelayMs();
    }

    public Task<EcWriteResult> WriteRegisterAsync(
        byte registerAddress,
        byte value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!EnsureInitialized(out var initMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(initMessage));
        }

        if (!WaitForInputBufferReady(cancellationToken, out var waitInputBeforeCommandMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(waitInputBeforeCommandMessage));
        }

        if (!TryWritePort(EcCommandPort, EcWriteCommand, out var writeCommandMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(writeCommandMessage));
        }

        if (!WaitForInputBufferReady(cancellationToken, out var waitInputBeforeAddressMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(waitInputBeforeAddressMessage));
        }

        if (!TryWritePort(EcDataPort, registerAddress, out var writeAddressMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(writeAddressMessage));
        }

        if (!WaitForInputBufferReady(cancellationToken, out var waitInputBeforeValueMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(waitInputBeforeValueMessage));
        }

        if (!TryWritePort(EcDataPort, value, out var writeValueMessage))
        {
            return Task.FromResult(EcWriteResult.Failed(writeValueMessage));
        }

        return Task.FromResult(EcWriteResult.Ok());
    }

    public Task<EcReadResult> ReadRegisterAsync(
        byte registerAddress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!EnsureInitialized(out var initMessage))
        {
            return Task.FromResult(EcReadResult.Failed(initMessage));
        }

        if (!WaitForInputBufferReady(cancellationToken, out var waitInputBeforeCommandMessage))
        {
            return Task.FromResult(EcReadResult.Failed(waitInputBeforeCommandMessage));
        }

        if (!TryWritePort(EcCommandPort, EcReadCommand, out var writeCommandMessage))
        {
            return Task.FromResult(EcReadResult.Failed(writeCommandMessage));
        }

        if (!WaitForInputBufferReady(cancellationToken, out var waitInputBeforeAddressMessage))
        {
            return Task.FromResult(EcReadResult.Failed(waitInputBeforeAddressMessage));
        }

        if (!TryWritePort(EcDataPort, registerAddress, out var writeAddressMessage))
        {
            return Task.FromResult(EcReadResult.Failed(writeAddressMessage));
        }

        if (!WaitForOutputBufferData(cancellationToken, out var waitOutputMessage))
        {
            return Task.FromResult(EcReadResult.Failed(waitOutputMessage));
        }

        if (!TryReadPort(EcDataPort, out var value, out var readValueMessage))
        {
            return Task.FromResult(EcReadResult.Failed(readValueMessage));
        }

        return Task.FromResult(EcReadResult.Ok(value));
    }

    public void Dispose()
    {
        lock (_stateSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_initialized)
            {
                return;
            }

            try
            {
                NativeMethods.DeinitializeOls();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                _logger.LogDebug(ex, "Failed to deinitialize WinRing0. Library may be missing.");
            }
            finally
            {
                _initialized = false;
            }
        }
    }

    private bool EnsureInitialized(out string message)
    {
        lock (_stateSync)
        {
            if (_disposed)
            {
                message = "EC backend has been disposed.";
                return false;
            }

            if (_initialized)
            {
                message = "EC backend initialized.";
                return true;
            }

            if (_initAttempted)
            {
                message = "EC backend initialization failed earlier.";
                return false;
            }

            _initAttempted = true;
            if (!OperatingSystem.IsWindows())
            {
                message = "EC backend requires Windows.";
                return false;
            }

            try
            {
                if (!NativeMethods.InitializeOls())
                {
                    message = "InitializeOls returned false. WinRing0 driver may be unavailable.";
                    return false;
                }

                _initialized = true;
                message = "EC backend initialized.";
                return true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                _logger.LogWarning(ex, "WinRing0 DLL/entry point not found.");
                message = "WinRing0 library was not found or has incompatible exports.";
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected WinRing0 initialization failure.");
                message = "Unexpected WinRing0 initialization failure.";
                return false;
            }
        }
    }

    private bool WaitForInputBufferReady(CancellationToken cancellationToken, out string message)
    {
        return WaitForStatusFlag(mask: InputBufferFullMask, expectedSet: false, cancellationToken, out message);
    }

    private bool WaitForOutputBufferData(CancellationToken cancellationToken, out string message)
    {
        return WaitForStatusFlag(mask: OutputBufferFullMask, expectedSet: true, cancellationToken, out message);
    }

    private bool WaitForStatusFlag(
        byte mask,
        bool expectedSet,
        CancellationToken cancellationToken,
        out string message)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(_operationTimeoutMs);

        while (DateTime.UtcNow <= deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryReadPort(EcCommandPort, out var status, out message))
            {
                return false;
            }

            var isSet = (status & mask) != 0;
            if (isSet == expectedSet)
            {
                message = "Status flag satisfied.";
                return true;
            }

            if (_pollDelayMs > 0)
            {
                Thread.Sleep(_pollDelayMs);
            }
        }

        message = expectedSet
            ? "Timed out waiting for EC output buffer."
            : "Timed out waiting for EC input buffer.";
        return false;
    }

    private static bool TryReadPort(ushort port, out byte value, out string message)
    {
        try
        {
            if (NativeMethods.ReadIoPortByte(port, out value))
            {
                message = "Port read successful.";
                return true;
            }

            message = $"ReadIoPortByte failed for port 0x{port:X2}.";
            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            value = 0;
            message = $"WinRing0 read failure for port 0x{port:X2}: {ex.Message}";
            return false;
        }
    }

    private static bool TryWritePort(ushort port, byte value, out string message)
    {
        try
        {
            if (NativeMethods.WriteIoPortByte(port, value))
            {
                message = "Port write successful.";
                return true;
            }

            message = $"WriteIoPortByte failed for port 0x{port:X2}.";
            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            message = $"WinRing0 write failure for port 0x{port:X2}: {ex.Message}";
            return false;
        }
    }

    private static class NativeMethods
    {
        public static bool InitializeOls()
        {
            return Environment.Is64BitProcess
                ? NativeMethods64.InitializeOls()
                : NativeMethods32.InitializeOls();
        }

        public static void DeinitializeOls()
        {
            if (Environment.Is64BitProcess)
            {
                NativeMethods64.DeinitializeOls();
            }
            else
            {
                NativeMethods32.DeinitializeOls();
            }
        }

        public static bool ReadIoPortByte(ushort port, out byte value)
        {
            return Environment.Is64BitProcess
                ? NativeMethods64.ReadIoPortByte(port, out value)
                : NativeMethods32.ReadIoPortByte(port, out value);
        }

        public static bool WriteIoPortByte(ushort port, byte value)
        {
            return Environment.Is64BitProcess
                ? NativeMethods64.WriteIoPortByte(port, value)
                : NativeMethods32.WriteIoPortByte(port, value);
        }
    }

    private static class NativeMethods64
    {
        [DllImport("WinRing0x64.dll", EntryPoint = "InitializeOls", SetLastError = true)]
        internal static extern bool InitializeOls();

        [DllImport("WinRing0x64.dll", EntryPoint = "DeinitializeOls", SetLastError = true)]
        internal static extern void DeinitializeOls();

        [DllImport("WinRing0x64.dll", EntryPoint = "ReadIoPortByte", SetLastError = true)]
        internal static extern bool ReadIoPortByte(ushort port, out byte value);

        [DllImport("WinRing0x64.dll", EntryPoint = "WriteIoPortByte", SetLastError = true)]
        internal static extern bool WriteIoPortByte(ushort port, byte value);
    }

    private static class NativeMethods32
    {
        [DllImport("WinRing0.dll", EntryPoint = "InitializeOls", SetLastError = true)]
        internal static extern bool InitializeOls();

        [DllImport("WinRing0.dll", EntryPoint = "DeinitializeOls", SetLastError = true)]
        internal static extern void DeinitializeOls();

        [DllImport("WinRing0.dll", EntryPoint = "ReadIoPortByte", SetLastError = true)]
        internal static extern bool ReadIoPortByte(ushort port, out byte value);

        [DllImport("WinRing0.dll", EntryPoint = "WriteIoPortByte", SetLastError = true)]
        internal static extern bool WriteIoPortByte(ushort port, byte value);
    }
}
