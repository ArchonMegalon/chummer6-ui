using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponAccessoryIncludedEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid WeaponId,
    Guid AccessoryId,
    bool IncludedInWeapon);
