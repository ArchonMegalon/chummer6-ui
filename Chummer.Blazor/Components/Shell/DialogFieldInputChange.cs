namespace Chummer.Blazor.Components.Shell;

public readonly record struct DialogFieldInputChange(
    string FieldId,
    string? Value);

public readonly record struct DialogFieldCheckboxChange(
    string FieldId,
    bool Value);
