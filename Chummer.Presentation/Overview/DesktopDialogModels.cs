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
    string LayoutSlot = DesktopDialogFieldLayoutSlots.Full,
    IReadOnlyList<DesktopDialogFieldOption>? Options = null)
{
    public string AccessibleName => DesktopDialogAccessibility.BuildFieldAccessibleName(Label);
    public string ToolTip => DesktopDialogAccessibility.BuildFieldToolTip(Label, Placeholder, Value);
    public string HelpText => DesktopDialogAccessibility.BuildFieldHelpText(
        Label,
        Placeholder,
        Value,
        InputType,
        IsReadOnly,
        IsMultiline,
        VisualKind);
}

public sealed record DesktopDialogAction(
    string Id,
    string Label,
    bool IsPrimary = false)
{
    public string AccessibleName => DesktopDialogAccessibility.BuildActionAccessibleName(Label);
    public string ToolTip => DesktopDialogAccessibility.BuildActionToolTip(Label);
    public string HelpText => DesktopDialogAccessibility.BuildActionHelpText(Label, IsPrimary);
}

public sealed record DesktopDialogState(
    string Id,
    string Title,
    string? Message,
    IReadOnlyList<DesktopDialogField> Fields,
    IReadOnlyList<DesktopDialogAction> Actions);

public sealed record DesktopDialogFieldOption(
    string Value,
    string Label);

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
    public const string Hidden = "hidden";
}
