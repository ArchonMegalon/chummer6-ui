namespace Chummer.Presentation.Overview;

public static class DesktopDialogFieldValueParser
{
    public static string Normalize(DesktopDialogField field, string? value)
    {
        if (string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
        {
            if (bool.TryParse(value, out bool booleanValue))
            {
                return booleanValue ? "true" : "false";
            }

            if (string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return "true";
            }

            return "false";
        }

        return value ?? string.Empty;
    }

    public static string? GetValue(DesktopDialogState dialog, string fieldId)
    {
        DesktopDialogField? field = dialog.Fields.FirstOrDefault(item => string.Equals(item.Id, fieldId, StringComparison.Ordinal));
        return field?.Value;
    }

    public static int ParseInt(DesktopDialogState dialog, string fieldId, int fallback)
    {
        string? raw = GetValue(dialog, fieldId);
        return int.TryParse(raw, out int value) ? value : fallback;
    }

    public static bool ParseBool(DesktopDialogState dialog, string fieldId, bool fallback)
    {
        string? raw = GetValue(dialog, fieldId);
        if (raw is null)
            return fallback;

        if (bool.TryParse(raw, out bool value))
            return value;

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
