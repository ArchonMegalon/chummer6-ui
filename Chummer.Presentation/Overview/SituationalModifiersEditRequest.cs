using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record SituationalModifiersEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    int CounterspellingDice,
    int LiftCarryHits);

public sealed record SituationalModifiersEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    int CounterspellingDice,
    int LiftCarryHits);

internal static class SituationalModifiersEditorProjector
{
    private const int MaximumValue = 100;

    public static SituationalModifiersEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing situational modifiers.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        return new SituationalModifiersEditorState(
            workspaceId,
            contentRevision,
            ParseBoundedInt(root.Element("currentcounterspellingdice")?.Value, "Counterspelling dice"),
            ParseBoundedInt(root.Element("currentliftcarryhits")?.Value, "Lift/carry hits"));
    }

    private static int ParseBoundedInt(string? value, string label)
    {
        int parsed = string.IsNullOrWhiteSpace(value)
            ? 0
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : throw new InvalidOperationException($"{label} is not a valid whole number.");
        if (parsed < 0 || parsed > MaximumValue)
        {
            throw new InvalidOperationException($"{label} must be between 0 and {MaximumValue.ToString(CultureInfo.InvariantCulture)}.");
        }
        return parsed;
    }
}
