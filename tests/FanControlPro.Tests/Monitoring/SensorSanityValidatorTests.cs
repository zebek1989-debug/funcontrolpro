using FanControlPro.Application.Monitoring;

namespace FanControlPro.Tests.Monitoring;

public sealed class SensorSanityValidatorTests
{
    private readonly SensorSanityValidator _validator = new();

    [Fact]
    public void ValidateTemperature_ShouldRejectValuesAbove150()
    {
        var issue = _validator.ValidateTemperature("cpu-temp", 151);

        Assert.NotNull(issue);
        Assert.Contains("150", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTemperature_ShouldAcceptSafeValues()
    {
        var issue = _validator.ValidateTemperature("cpu-temp", 72.5);

        Assert.Null(issue);
    }

    [Fact]
    public void ValidateFanSpeed_ShouldRejectValuesAbove10000()
    {
        var issue = _validator.ValidateFanSpeed("cpu-fan", 10001);

        Assert.NotNull(issue);
        Assert.Contains("10000", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSystemLoad_ShouldReturnIssueForOutOfRangeValues()
    {
        var issues = _validator.ValidateSystemLoad(new SystemLoadSnapshot(50, 101, -4));

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, x => x.SensorId == "system-load/gpu");
        Assert.Contains(issues, x => x.SensorId == "system-load/memory");
    }
}
