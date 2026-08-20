using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record PrimaryArmEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    string Value,
    bool Ambidextrous);

public sealed record PrimaryArmEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    string Value);

internal static class PrimaryArmEditorProjector
{
    public static PrimaryArmEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing primary arm.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        string value = NormalizeValue(root.Element("primaryarm")?.Value);
        return new PrimaryArmEditorState(
            workspaceId,
            contentRevision,
            value,
            IsAmbidextrous(root));
    }

    internal static string NormalizeValue(string? value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "Right" : value.Trim();
        if (!string.Equals(normalized, "Left", StringComparison.Ordinal)
            && !string.Equals(normalized, "Right", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Primary arm must be Left or Right.");
        }
        return normalized;
    }

    internal static bool IsAmbidextrous(XElement root)
        => root
            .Element("improvements")?
            .Elements("improvement")
            .Any(improvement =>
                string.Equals(
                    improvement.Element("improvementttype")?.Value.Trim(),
                    "Ambidextrous",
                    StringComparison.OrdinalIgnoreCase)
                && IsEnabled(improvement.Element("enabled")?.Value))
            ?? false;

    private static bool IsEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
        {
            return integer > 0;
        }
        return !bool.TryParse(value, out bool boolean) || boolean;
    }
}
