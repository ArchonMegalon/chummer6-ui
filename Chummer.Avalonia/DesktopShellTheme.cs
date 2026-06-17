using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Chummer.Avalonia;

internal static class DesktopShellTheme
{
    public static IBrush ResolveThemeBrush(string resourceKey, string fallbackHex)
        => App.Current?.TryFindResource(resourceKey, out object? resource) == true && resource is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallbackHex));

    public static Border CreateWindowSurface(Control child, double padding = 16)
        => new()
        {
            Background = ResolveThemeBrush("ChummerShellWindowBackgroundBrush", "#E3EAF3"),
            Padding = new Thickness(padding),
            Child = child
        };

    public static Border CreateUtilityPanel(Control child, double padding = 10, double cornerRadius = 8)
        => new()
        {
            Background = ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F2F5FA"),
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cornerRadius),
            Padding = new Thickness(padding),
            Child = child
        };

    public static void ApplyPrimaryButton(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.Classes.Add("shell-action");
        button.Classes.Add("primary");
    }
}
