using System;

namespace Chummer.Presentation.Overview;

public static class DesktopDialogAccessibility
{
    public static string BuildFieldContainerName(string fieldId)
        => BuildControlName("DialogField", fieldId);

    public static string BuildFieldLabelName(string fieldId)
        => BuildControlName("DialogFieldLabel", fieldId);

    public static string BuildFieldInputName(string fieldId)
        => BuildControlName("DialogFieldInput", fieldId);

    public static string BuildActionName(string actionId)
        => BuildControlName("DialogAction", actionId);

    public static string BuildFieldAccessibleName(string label)
        => string.IsNullOrWhiteSpace(label) ? "Dialog field" : label;

    public static string BuildFieldToolTip(
        string label,
        string placeholder,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(placeholder)
            && !string.Equals(placeholder, value, StringComparison.Ordinal))
        {
            return $"{BuildFieldAccessibleName(label)}: {placeholder}";
        }

        return BuildFieldAccessibleName(label);
    }

    public static string BuildFieldHelpText(
        string label,
        string placeholder,
        string value,
        string inputType,
        bool isReadOnly,
        bool isMultiline,
        string visualKind)
    {
        string accessibleName = BuildFieldAccessibleName(label);
        if (string.Equals(inputType, "checkbox", StringComparison.Ordinal))
        {
            string checkboxState = NormalizeCheckboxValue(value);
            string checkboxMode = isReadOnly ? "Read-only checkbox." : "Editable checkbox.";
            return $"{accessibleName}. {checkboxMode} Current value {checkboxState}.";
        }

        string descriptor = DescribeFieldSurface(inputType, isMultiline, visualKind);
        string mode = isReadOnly ? "Read-only" : "Editable";
        string placeholderSuffix = !string.IsNullOrWhiteSpace(placeholder)
            && !string.Equals(placeholder, value, StringComparison.Ordinal)
            ? $" Placeholder {placeholder}."
            : string.Empty;
        return $"{accessibleName}. {mode} {descriptor}.{placeholderSuffix}";
    }

    public static string BuildActionAccessibleName(string label)
        => string.IsNullOrWhiteSpace(label) ? "Dialog action" : label;

    public static string BuildActionToolTip(string label)
        => BuildActionAccessibleName(label);

    public static string BuildActionHelpText(string label, bool isPrimary)
    {
        string accessibleName = BuildActionAccessibleName(label);
        return isPrimary
            ? $"{accessibleName}. Primary dialog action."
            : $"{accessibleName}. Dialog action.";
    }

    private static string DescribeFieldSurface(string inputType, bool isMultiline, string visualKind)
    {
        if (!string.Equals(visualKind, DesktopDialogFieldVisualKinds.Default, StringComparison.Ordinal))
        {
            return visualKind switch
            {
                DesktopDialogFieldVisualKinds.Tabs => "tab-strip view",
                DesktopDialogFieldVisualKinds.Image => "image preview",
                DesktopDialogFieldVisualKinds.Tree => "tree view",
                DesktopDialogFieldVisualKinds.Grid => "grid summary",
                DesktopDialogFieldVisualKinds.Snippet => "snippet preview",
                DesktopDialogFieldVisualKinds.List => "list view",
                DesktopDialogFieldVisualKinds.Detail => "detail pane",
                DesktopDialogFieldVisualKinds.Summary => "summary pane",
                _ => "dialog field"
            };
        }

        if (string.Equals(inputType, "number", StringComparison.Ordinal))
        {
            return "number field";
        }

        if (isMultiline)
        {
            return "multi-line text field";
        }

        return "text field";
    }

    private static string NormalizeCheckboxValue(string value)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed ? "checked" : "unchecked";
        }

        if (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return "checked";
        }

        return "unchecked";
    }

    private static string BuildControlName(string prefix, string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return prefix;
        }

        char[] characters = suffix.Trim().ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]) && characters[index] != '_')
            {
                characters[index] = '_';
            }
        }

        return $"{prefix}_{new string(characters)}";
    }
}
