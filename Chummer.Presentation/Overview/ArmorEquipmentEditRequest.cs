using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ArmorEquipmentEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid ArmorId,
    bool ExpectedEquipped,
    int ExpectedArmorCount,
    int ExpectedEquippedCount,
    CharacterArmorEquipmentAction Action);
