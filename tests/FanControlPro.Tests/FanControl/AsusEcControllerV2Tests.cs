using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;
using FanControlPro.Infrastructure.FanControl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FanControlPro.Tests.FanControl;

public sealed class AsusEcControllerV2Tests
{
    [Fact]
    public async Task ShouldControlAsusChannel_WhenSupportIsFullControl()
    {
        var controller = new AsusEcControllerV2();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var canControl = await controller.CanControlAsync(channel);
        var result = await controller.SetSpeedAsync(channel, 35);

        Assert.True(canControl);
        Assert.True(result.Success);
        Assert.Equal(35, result.AppliedPercent);
    }

    [Fact]
    public async Task ShouldRejectCpuSpeedBelowSafeMinimum()
    {
        var controller = new AsusEcControllerV2();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var result = await controller.SetSpeedAsync(channel, 10);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.OutOfRange, result.FailureReason);
    }

    [Fact]
    public async Task ShouldUseHardwarePath_WhenEnabledAndRegisterMapped()
    {
        var ecAccess = new FakeEcRegisterAccess();
        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = true,
            VerifyReadBack = true,
            RegisterScaleMaxValue = 100,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 55);

        Assert.True(result.Success);
        Assert.Equal(55, result.AppliedPercent);
        Assert.Equal(1, ecAccess.WriteCalls);
        Assert.Equal(0x90, ecAccess.LastWriteRegister);
        Assert.Equal(55, ecAccess.LastWriteValue);
    }

    [Fact]
    public async Task ShouldReturnHardwareError_WhenHardwareWriteFails()
    {
        var ecAccess = new FakeEcRegisterAccess { FailWrites = true };
        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = true,
            VerifyReadBack = false,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 50);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.HardwareError, result.FailureReason);
        Assert.Equal(1, ecAccess.WriteCalls);
    }

    [Fact]
    public async Task ShouldFallbackToSimulation_WhenHardwareDisabled()
    {
        var ecAccess = new FakeEcRegisterAccess();
        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = false,
            VerifyReadBack = true,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 44);

        Assert.True(result.Success);
        Assert.Equal(44, result.AppliedPercent);
        Assert.Equal(0, ecAccess.WriteCalls);
    }

    [Fact]
    public async Task ShouldPreferSuperIoPath_WhenAvailable()
    {
        var ecAccess = new FakeEcRegisterAccess();
        var superIo = new FakeSuperIoFanControlAccess
        {
            WriteResult = SuperIoWriteAttemptResult.Succeeded(58, "super-io")
        };

        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = true,
            PreferSuperIoControlPath = true,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance,
            superIo);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 58);

        Assert.True(result.Success);
        Assert.Equal(58, result.AppliedPercent);
        Assert.Equal(1, superIo.WriteCalls);
        Assert.Equal(0, ecAccess.WriteCalls);
    }

    [Fact]
    public async Task ShouldFallbackToEc_WhenSuperIoPathUnavailable()
    {
        var ecAccess = new FakeEcRegisterAccess();
        var superIo = new FakeSuperIoFanControlAccess
        {
            WriteResult = SuperIoWriteAttemptResult.PathUnavailable("no controls")
        };

        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = true,
            PreferSuperIoControlPath = true,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance,
            superIo);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 53);

        Assert.True(result.Success);
        Assert.Equal(1, superIo.WriteCalls);
        Assert.Equal(1, ecAccess.WriteCalls);
    }

    [Fact]
    public async Task ShouldReturnHardwareError_WhenSuperIoPathFails()
    {
        var ecAccess = new FakeEcRegisterAccess();
        var superIo = new FakeSuperIoFanControlAccess
        {
            WriteResult = SuperIoWriteAttemptResult.Failed("write failed")
        };

        var options = Options.Create(new EcWriteSafetyOptions
        {
            EnableHardwareAccess = true,
            PreferSuperIoControlPath = true,
            AsusPwmRegisters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["cpu_fan"] = 0x90
            }
        });

        var controller = new AsusEcControllerV2(
            ecAccess,
            options,
            new NoOpCooldownGate(),
            NullLogger<AsusEcControllerV2>.Instance,
            superIo);

        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);
        var result = await controller.SetSpeedAsync(channel, 50);

        Assert.False(result.Success);
        Assert.Equal(FanControlFailureReason.HardwareError, result.FailureReason);
        Assert.Equal(1, superIo.WriteCalls);
        Assert.Equal(0, ecAccess.WriteCalls);
    }

    private sealed class NoOpCooldownGate : IChannelWriteCooldownGate
    {
        public Task WaitForTurnAsync(string channelId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEcRegisterAccess : IEcRegisterAccess
    {
        private readonly Dictionary<byte, byte> _registers = new();

        public bool FailWrites { get; set; }

        public int WriteCalls { get; private set; }

        public byte LastWriteRegister { get; private set; }

        public byte LastWriteValue { get; private set; }

        public Task<EcWriteResult> WriteRegisterAsync(byte registerAddress, byte value, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            LastWriteRegister = registerAddress;
            LastWriteValue = value;

            if (FailWrites)
            {
                return Task.FromResult(EcWriteResult.Failed("forced failure"));
            }

            _registers[registerAddress] = value;
            return Task.FromResult(EcWriteResult.Ok());
        }

        public Task<EcReadResult> ReadRegisterAsync(byte registerAddress, CancellationToken cancellationToken = default)
        {
            var value = _registers.TryGetValue(registerAddress, out var stored) ? stored : (byte)40;
            return Task.FromResult(EcReadResult.Ok(value));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSuperIoFanControlAccess : ISuperIoFanControlAccess
    {
        public int WriteCalls { get; private set; }

        public SuperIoWriteAttemptResult WriteResult { get; set; } =
            SuperIoWriteAttemptResult.PathUnavailable("not configured");

        public SuperIoReadAttemptResult ReadResult { get; set; } =
            SuperIoReadAttemptResult.PathUnavailable("not configured");

        public Task<SuperIoWriteAttemptResult> TrySetSpeedAsync(
            FanChannel channel,
            int percent,
            CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            return Task.FromResult(WriteResult);
        }

        public Task<SuperIoReadAttemptResult> TryReadSpeedPercentAsync(
            FanChannel channel,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ReadResult);
        }
    }
}
