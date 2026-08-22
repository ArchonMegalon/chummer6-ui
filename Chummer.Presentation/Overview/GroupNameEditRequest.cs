using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GroupNameEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    string GroupName);

public sealed record GroupNameEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    string ExpectedGroupName,
    string GroupName);

internal static class GroupNameEditorProjector
{
    public static GroupNameEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing the group name.");
        }

        return new GroupNameEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml));
    }

    internal static string ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] values = root.Elements("groupname").Take(2).ToArray();
        string value = values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value,
            _ => throw new InvalidOperationException("The saved runner has duplicate <groupname> values.")
        };
        if (!CharacterGroupNameRules.TryValidate(value, out string validated))
        {
            throw new InvalidOperationException(
                "The saved runner's group name cannot be represented by the Chummer5 single-line control.");
        }
        return validated;
    }
}
