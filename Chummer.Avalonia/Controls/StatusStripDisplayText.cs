namespace Chummer.Avalonia.Controls;

/// <summary>Keeps implementation inventory out of normal desktop chrome.</summary>
internal static class StatusStripDisplayText
{
    internal static string Summary(string? diagnosticText)
    {
        if (string.IsNullOrWhiteSpace(diagnosticText))
            return string.Empty;

        // The formatter's leading ruleset segment remains the identity used by
        // visible-shell readers. The complete original belongs in Details.
        int separator = diagnosticText.IndexOf('|');
        return (separator >= 0 ? diagnosticText[..separator] : diagnosticText).Trim();
    }
}
