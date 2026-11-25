using Avalonia;
using Avalonia.Styling;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

/// <summary>Windows appearance projection; never changes game or account state.</summary>
internal static class DesktopAppearance
{
    internal static ThemeVariant ResolveVariant(string? preference)
        => preference?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            "system" => ThemeVariant.Default,
            // Preserve the previous dark rendering of legacy classic/modern preferences.
            _ => ThemeVariant.Dark
        };

    internal static void Apply(string? preference)
    {
        if (global::Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = ResolveVariant(preference);
    }

    internal static void LoadBeforeFirstWindow()
    {
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState("avalonia");
        DesktopPreferenceStateRuntime.SetCurrent(preferences);
        Apply(preferences.Theme);
    }

    internal static IReadOnlyList<(string Value, string Label)> Options(string? savedValue)
    {
        List<(string Value, string Label)> options = new()
        {
            ("light", "Light"), ("dark", "Dark"), ("system", "System (Windows)")
        };
        if (!string.IsNullOrWhiteSpace(savedValue)
            && !options.Any(option => string.Equals(option.Value, savedValue, StringComparison.OrdinalIgnoreCase)))
            options.Add((savedValue, $"{savedValue} (saved appearance)"));
        return options;
    }
}
