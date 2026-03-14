using Chummer.Ui.Kit.Tokens;

namespace Chummer.Presentation.UiKit;

public static class DesignTokenThemeBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    private static readonly TokenCanon Canon = TokenCanon.CreateDefault();

    public static readonly string ShellSurfaceToken = Canon["color.background.canvas"];
    public static readonly string ShellBorderToken = Canon["color.border.subtle"];
    public static readonly string PanelSurfaceToken = Canon["color.background.panel"];
    public static readonly string FocusRingToken = Canon["color.accent.primary"];

    public static string CssVariable(string tokenValue)
    {
        return tokenValue;
    }
}
