namespace Chummer.Blazor.Components.Shell;

public readonly record struct DialogFieldInputChange(
    string FieldId,
    string? Value);

public readonly record struct DialogFieldCheckboxChange(
    string FieldId,
    bool Value);

public readonly record struct DialogRosterDropIntent(
    string SourceLine,
    string TargetLine,
    string TargetFolder,
    string ActionId);
