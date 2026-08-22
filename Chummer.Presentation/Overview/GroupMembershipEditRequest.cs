using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GroupMembershipEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterGroupMembershipState Membership);

public sealed record GroupMembershipEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGroupMembershipState ExpectedState,
    bool GroupMember,
    bool Confirmed);

internal static class GroupMembershipEditorProjector
{
    public static GroupMembershipEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing group membership.");
        }

        return new GroupMembershipEditorState(
            workspaceId,
            contentRevision,
            ProjectState(xml, sourceDataResolver));
    }

    internal static CharacterGroupMembershipState ProjectState(
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool groupMember = ReadOptionalBool(root, "groupmember");
        bool created = ReadOptionalBool(root, "created");
        bool magicEnabled = ReadOptionalBool(root, "magenabled");
        bool resonanceEnabled = ReadOptionalBool(root, "resenabled");
        int availableKarma = ReadOptionalInt(root, "karma");

        int? joinCost = null;
        int? leaveCost = null;
        if (created && magicEnabled)
        {
            ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
            if (sourceData is not null
                && sourceData.TryResolveGroupMembershipKarmaCosts(
                    out int resolvedJoinCost,
                    out int resolvedLeaveCost))
            {
                joinCost = resolvedJoinCost;
                leaveCost = resolvedLeaveCost;
            }
        }

        if (!CharacterGroupMembershipRules.TryProject(
                groupMember,
                created,
                magicEnabled,
                resonanceEnabled,
                availableKarma,
                joinCost,
                leaveCost,
                out CharacterGroupMembershipState? state)
            || state is null)
        {
            throw new InvalidOperationException(
                "The saved runner does not prove the exact Chummer5 group-membership rules.");
        }
        return state;
    }

    private static bool ReadOptionalBool(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return false;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static int ReadOptionalInt(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return 0;
        }
        if (values.Length != 1
            || !int.TryParse(values[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }
}
