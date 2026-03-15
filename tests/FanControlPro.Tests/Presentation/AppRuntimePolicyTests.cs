using Avalonia.Styling;
using FanControlPro.Application.Configuration;
using FanControlPro.Presentation;

namespace FanControlPro.Tests.Presentation;

public sealed class AppRuntimePolicyTests
{
    [Fact]
    public void ShouldStartMinimized_WhenArgsContainStartMinimized_ShouldReturnTrue()
    {
        var args = new[] { "--start-minimized" };

        var result = AppRuntimePolicy.ShouldStartMinimized(args);

        Assert.True(result);
    }

    [Fact]
    public void ShouldStartMinimized_WhenForceVisibleIsPresent_ShouldReturnFalse()
    {
        var args = new[] { "--start-minimized", "--force-visible" };

        var result = AppRuntimePolicy.ShouldStartMinimized(args);

        Assert.False(result);
    }

    [Fact]
    public void HasArgument_ShouldMatchCaseInsensitive()
    {
        var args = new[] { "--STARTUP-LITE" };

        var result = AppRuntimePolicy.HasArgument(args, "--startup-lite");

        Assert.True(result);
    }

    [Fact]
    public void ResolveThemeVariant_System_ShouldMapToDefault()
    {
        var result = AppRuntimePolicy.ResolveThemeVariant(ApplicationTheme.System);

        Assert.Equal(ThemeVariant.Default, result);
    }

    [Theory]
    [InlineData(ApplicationTheme.Light)]
    [InlineData(ApplicationTheme.Dark)]
    public void ResolveThemeVariant_LightOrDark_ShouldMapOneToOne(ApplicationTheme theme)
    {
        var result = AppRuntimePolicy.ResolveThemeVariant(theme);

        Assert.Equal(
            theme == ApplicationTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark,
            result);
    }
}
