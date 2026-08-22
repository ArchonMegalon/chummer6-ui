using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearWirelessEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearWirelessState> Nodes);

public sealed record GearWirelessEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearEquipmentIdentity Identity,
    string ExpectedNodeRevision,
    bool WirelessOn);

internal static class GearWirelessEditorProjector
{
    public static GearWirelessEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Gear Wireless state.");
        }

        return new GearWirelessEditorState(
            workspaceId,
            contentRevision,
            rootGearId,
            ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearWirelessState> ProjectValue(
        string xml,
        Guid rootGearId)
    {
        IReadOnlyList<CharacterGearEquipmentState> equipmentNodes =
            GearEquipmentEditorProjector.ProjectValue(xml, rootGearId);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");

        var states = new List<CharacterGearWirelessState>(equipmentNodes.Count);
        foreach (CharacterGearEquipmentState equipment in equipmentNodes)
        {
            XElement gear = GearEquipmentEditorProjector.FindNode(root, equipment.Identity);
            XElement[] values = gear.Elements("wirelesson").Take(2).ToArray();
            if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool wirelessOn))
            {
                throw new InvalidOperationException(
                    "Gear Wireless editing requires one exact saved wirelesson Boolean per Gear node.");
            }

            if (!CharacterGearWirelessRules.TryCreateState(
                    equipment.Identity,
                    equipment.Phase == CharacterGearEquipmentPhase.Career,
                    equipment.DisplayPath,
                    wirelessOn,
                    out CharacterGearWirelessState state))
            {
                throw new InvalidOperationException(
                    "Gear Wireless editing requires exact saved hierarchical identity and state.");
            }
            states.Add(state);
        }
        return states.ToArray();
    }
}
