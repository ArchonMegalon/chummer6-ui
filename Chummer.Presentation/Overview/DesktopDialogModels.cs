namespace Chummer.Presentation.Overview;

public sealed record DesktopDialogField(
    string Id,
    string Label,
    string Value,
    string Placeholder,
    bool IsMultiline = false,
    bool IsReadOnly = false,
    string InputType = "text");

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
