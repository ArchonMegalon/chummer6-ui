namespace Chummer.Presentation.Overview;

public sealed record DesktopDialogField(
    string Id,
    string Label,
    string Value,
    string Placeholder,
    bool IsMultiline = false,
    bool IsReadOnly = false,
    string InputType = "text",
    string VisualKind = DesktopDialogFieldVisualKinds.Default,
    string LayoutSlot = DesktopDialogFieldLayoutSlots.Full);

public sealed record DesktopDialogAction(
    string Id,
    string Label,
    bool IsPrimary = false);

public sealed record DesktopDialogState(
    string Id,
    string Title,
    string? Message,
    IReadOnlyList<DesktopDialogField> Fields,
    IReadOnlyList<DesktopDialogAction> Actions);

public static class DesktopDialogFieldVisualKinds
{
    public const string Default = "default";
    public const string List = "list";
    public const string Tree = "tree";
    public const string Grid = "grid";
    public const string Detail = "detail";
    public const string Summary = "summary";
    public const string Snippet = "snippet";
    public const string Tabs = "tabs";
    public const string Image = "image";
}

public static class DesktopDialogFieldLayoutSlots
{
    public const string Full = "full";
    public const string Left = "left";
    public const string Right = "right";
}
