using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ImprovementNotesEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterImprovementNotesState> Improvements);

public sealed record ImprovementNotesEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterImprovementIdentity Identity,
    string ExpectedImprovementRevision,
    string Notes,
    string NotesColor);

internal static class ImprovementNotesEditorProjector
{
    private const string LegacyDefaultNotesColor = "Chocolate";

    public static ImprovementNotesEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Improvement notes.");
        }

        return new ImprovementNotesEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml));
    }

    internal static IReadOnlyList<CharacterImprovementNotesState> ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        IReadOnlyList<CharacterImprovementActiveState> directImprovements =
            ImprovementActiveEditorProjector.ProjectValue(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");

        var states = new List<CharacterImprovementNotesState>(directImprovements.Count);
        foreach (CharacterImprovementActiveState direct in directImprovements)
        {
            XElement improvement = ImprovementActiveEditorProjector.FindNode(root, direct.Identity);
            string notes = ReadOptionalValue(improvement, "notes", string.Empty);
            string notesColor = ReadOptionalValue(
                improvement,
                "notesColor",
                LegacyDefaultNotesColor);
            if (!CharacterImprovementNotesRules.TryCreateState(
                    direct.Identity,
                    true,
                    direct.DisplayName,
                    notes,
                    notesColor,
                    out CharacterImprovementNotesState state))
            {
                throw new InvalidOperationException(
                    "Improvement notes require exact stable identity, note text, and legacy HTML color.");
            }
            states.Add(state);
        }
        return states;
    }

    internal static XElement FindNode(XElement root, CharacterImprovementIdentity identity)
        => ImprovementActiveEditorProjector.FindNode(root, identity);

    private static string ReadOptionalValue(
        XElement parent,
        string name,
        string defaultValue)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => defaultValue,
            1 => values[0].Value,
            _ => throw new InvalidOperationException(
                $"An Improvement has duplicate <{name}> values.")
        };
    }
}
