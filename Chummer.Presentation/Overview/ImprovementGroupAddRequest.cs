using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ImprovementGroupAddEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterImprovementGroupAddState Collection);

public sealed record ImprovementGroupAddRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterImprovementGroupInsertionIdentity Identity,
    string ExpectedGroupsRevision);

internal static class ImprovementGroupAddEditorProjector
{
    public static ImprovementGroupAddEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before adding an Improvement group.");
        }

        return new ImprovementGroupAddEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml));
    }

    internal static CharacterImprovementGroupAddState ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "Add Improvement Group is exposed by CharacterCareer only.");
        }

        XElement groups = FindContainer(root);
        string[] values = groups.Elements("improvementgroup")
            .Select(group => group.Value)
            .ToArray();
        if (!CharacterImprovementGroupAddRules.TryCreateState(
                created: true,
                groups: values,
                out CharacterImprovementGroupAddState state))
        {
            throw new InvalidOperationException(
                "Improvement group collection state is unavailable.");
        }
        return state;
    }

    internal static XElement FindContainer(XElement root)
    {
        XElement[] values = root.Elements("improvementgroups").Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException(
                "The saved runner requires one <improvementgroups> collection."),
            _ => throw new InvalidOperationException(
                "The saved runner has duplicate <improvementgroups> collections.")
        };
    }

    private static bool ReadRequiredCreated(XElement root)
    {
        XElement[] values = root.Elements("created").Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool created))
        {
            throw new InvalidOperationException(
                "Improvement group creation requires an exact saved creation/career state.");
        }
        return created;
    }
}
