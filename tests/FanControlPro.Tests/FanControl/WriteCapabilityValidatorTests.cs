using FanControlPro.Application.FanControl;
using FanControlPro.Domain.FanControl;
using FanControlPro.Domain.FanControl.Enums;
using FanControlPro.Domain.Hardware.Enums;

namespace FanControlPro.Tests.FanControl;

public sealed class WriteCapabilityValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ShouldTestWriteAndRollback_WhenControllerSupportsControl()
    {
        var controller = new FakeController(allowControl: true, failRollback: false);
        var validator = new WriteCapabilityValidator();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var validation = await validator.ValidateAsync(controller, channel);

        Assert.True(validation.IsValid);
        Assert.True(validation.RollbackApplied);
        Assert.NotNull(validation.PreviousPercent);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenRollbackFails()
    {
        var controller = new FakeController(allowControl: true, failRollback: true);
        var validator = new WriteCapabilityValidator();
        var channel = new FanChannel("cpu_fan", "CPU Fan", "ASUS", SupportLevel.FullControl, IsCpuChannel: true);

        var validation = await validator.ValidateAsync(controller, channel);

        Assert.False(validation.IsValid);
        Assert.False(validation.RollbackApplied);
    }

    private sealed class FakeController : IFanControllerV2
    {
        private readonly bool _allowControl;
        private readonly bool _failRollback;
        private int _current = 40;
        private int? _previousWrite;

        public FakeController(bool allowControl, bool failRollback)
        {
            _allowControl = allowControl;
            _failRollback = failRollback;
        }

        public Task<bool> CanControlAsync(FanChannel channel) => Task.FromResult(_allowControl);

        public Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent)
        {
            if (!_allowControl)
            {
                return Task.FromResult(FanControlResult.Failed(FanControlFailureReason.MonitoringOnly, "Blocked"));
            }

            if (_previousWrite.HasValue && _failRollback && percent == _previousWrite.Value)
            {
                return Task.FromResult(FanControlResult.Failed(FanControlFailureReason.HardwareError, "Rollback failed"));
            }

            _previousWrite ??= _current;
            _current = percent;
            return Task.FromResult(FanControlResult.Succeeded(percent, "Applied"));
        }

        public Task<int> GetCurrentSpeedAsync(FanChannel channel) => Task.FromResult(_current);

        public Task<HealthStatus> GetHealthStatusAsync() => Task.FromResult(HealthStatus.Healthy());
    }
}
