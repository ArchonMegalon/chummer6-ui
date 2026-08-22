using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearDataProcessingFirewallSwapEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearMatrixSwapState> Nodes);

public sealed record GearDataProcessingFirewallSwapEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearMatrixSwapIdentity Identity,
    string ExpectedNodeRevision,
    CharacterGearMatrixStat ChangedAttribute,
    CharacterGearMatrixStat TargetAttribute);

internal static class GearDataProcessingFirewallSwapEditorProjector
{
    public static GearDataProcessingFirewallSwapEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before swapping Gear Data Processing or Firewall.");
        }

        return new GearDataProcessingFirewallSwapEditorState(
            workspaceId,
            contentRevision,
            rootGearId,
            ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearMatrixSwapState> ProjectValue(string xml, Guid rootGearId)
        => GearSleazeSwapEditorProjector.ProjectValue(xml, rootGearId);

    internal static XElement FindNode(XElement root, CharacterGearMatrixSwapIdentity identity)
        => GearSleazeSwapEditorProjector.FindNode(root, identity);
}
