using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearSleazeSwapEditorState(
    CharacterWorkspaceId WorkspaceId, long ContentRevision, Guid RootGearId,
    IReadOnlyList<CharacterGearMatrixSwapState> Nodes);

public sealed record GearSleazeSwapEditRequest(
    CharacterWorkspaceId WorkspaceId, long ExpectedContentRevision,
    CharacterGearMatrixSwapIdentity Identity, string ExpectedNodeRevision,
    CharacterGearMatrixStat ChangedAttribute, CharacterGearMatrixStat TargetAttribute);

internal static class GearSleazeSwapEditorProjector
{
    public static GearSleazeSwapEditorState Project(string xml, CharacterWorkspaceId workspaceId,
        long contentRevision, Guid rootGearId)
    {
        GearAttackSwapEditorState shared = GearAttackSwapEditorProjector.Project(
            xml, workspaceId, contentRevision, rootGearId);
        return new(workspaceId, contentRevision, rootGearId, shared.Nodes.Select(ToShared).ToArray());
    }

    internal static IReadOnlyList<CharacterGearMatrixSwapState> ProjectValue(string xml, Guid rootGearId)
        => GearAttackSwapEditorProjector.ProjectValue(xml, rootGearId).Select(ToShared).ToArray();

    internal static XElement FindNode(XElement root, CharacterGearMatrixSwapIdentity identity)
        => GearAttackSwapEditorProjector.FindNode(root, new CharacterGearAttackSwapIdentity(identity.GearPath));

    private static CharacterGearMatrixSwapState ToShared(CharacterGearAttackSwapState state)
        => new(new CharacterGearMatrixSwapIdentity(state.Identity.GearPath), state.DisplayPath,
            state.Phase == CharacterGearAttackSwapPhase.Career
                ? CharacterGearMatrixSwapPhase.Career : CharacterGearMatrixSwapPhase.Creation,
            state.Attack, state.Sleaze, state.DataProcessing, state.Firewall,
            new CharacterGearMatrixSwapEconomics(state.Economics.NuyenDelta, state.Economics.KarmaDelta),
            state.Revision);
}
