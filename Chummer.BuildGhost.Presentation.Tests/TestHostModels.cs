namespace Chummer.Presentation.Overview;

internal sealed record DesktopSupportedLanguage(string Code, string Label);

internal static class DesktopLocalizationCatalog
{
    internal const string DefaultLanguage = "en-us";
    internal static IReadOnlyList<DesktopSupportedLanguage> ShippingLanguages { get; } =
    [
        new("en-us", "English"),
        new("de-de", "Deutsch"),
        new("fr-fr", "Français"),
        new("ja-jp", "日本語"),
        new("pt-br", "Português (Brasil)"),
        new("zh-cn", "简体中文")
    ];
}

internal static class DesktopDialogFieldVisualKinds
{
    internal const string Default = "default";
    internal const string Grid = "grid";
    internal const string Detail = "detail";
    internal const string Summary = "summary";
    internal const string Snippet = "snippet";
    internal const string List = "list";
    internal const string Image = "image";
    internal const string Book = "book";
}

internal static class DesktopDialogFieldLayoutSlots
{
    internal const string Full = "full";
    internal const string Left = "left";
    internal const string Right = "right";
    internal const string Hidden = "hidden";
}

internal sealed record DesktopDialogField(
    string Id,
    string Label,
    string Value,
    string Placeholder,
    bool IsMultiline = false,
    bool IsReadOnly = false,
    string InputType = "text",
    string VisualKind = DesktopDialogFieldVisualKinds.Default,
    string LayoutSlot = DesktopDialogFieldLayoutSlots.Full,
    IReadOnlyList<DesktopDialogFieldOption>? Options = null);

internal sealed record DesktopDialogFieldOption(string Value, string Label);

internal sealed record DesktopDialogAction(string Id, string Label, bool IsPrimary = false);

internal sealed record DesktopDialogState(
    string Id,
    string Title,
    string? Message,
    IReadOnlyList<DesktopDialogField> Fields,
    IReadOnlyList<DesktopDialogAction> Actions);

internal sealed record TestWorkspaceId(string Value)
{
    public override string ToString() => Value;
}

internal sealed record CharacterOverviewState(TestWorkspaceId? WorkspaceId, long ContentRevision);

internal static class DesktopDialogFieldValueParser
{
    internal static string? GetValue(DesktopDialogState dialog, string fieldId)
        => dialog.Fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))?.Value;
}
