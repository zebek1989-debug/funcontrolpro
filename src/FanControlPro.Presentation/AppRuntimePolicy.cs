using Avalonia.Styling;
using FanControlPro.Application.Configuration;

namespace FanControlPro.Presentation;

public static class AppRuntimePolicy
{
    public static bool ShouldStartMinimized(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return false;
        }

        if (HasArgument(args, "--force-visible", "--show-main-window"))
        {
            return false;
        }

        return HasArgument(args, "--start-minimized", "--start-to-tray");
    }

    public static bool HasArgument(IReadOnlyList<string>? args, params string[] acceptedValues)
    {
        if (args is null || args.Count == 0 || acceptedValues.Length == 0)
        {
            return false;
        }

        foreach (var arg in args)
        {
            foreach (var acceptedValue in acceptedValues)
            {
                if (string.Equals(arg, acceptedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static ThemeVariant ResolveThemeVariant(ApplicationTheme theme)
    {
        return theme switch
        {
            ApplicationTheme.Light => ThemeVariant.Light,
            ApplicationTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
