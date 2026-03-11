namespace Chummer.Presentation.UiKit;

public static class DesignTokenThemeBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    public const string LocalAdapterMarker = "TODO: replace presentation-local design token/theme mappings with Chummer.Ui.Kit package tokens.";

    public const string ShellSurfaceToken = "--ui-kit-shell-surface";
    public const string ShellBorderToken = "--ui-kit-shell-border";
    public const string PanelSurfaceToken = "--ui-kit-panel-surface";
    public const string FocusRingToken = "--ui-kit-focus-ring";

    public static string CssVariable(string tokenName)
    {
        return $"var({tokenName})";
    }
}
