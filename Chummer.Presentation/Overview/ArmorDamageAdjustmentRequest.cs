using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ArmorDamageAdjustmentRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid ArmorId,
    int ExpectedArmorDamage,
    int ArmorDamageMaximum,
    CharacterArmorDamageAdjustment Adjustment);
