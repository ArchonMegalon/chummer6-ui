using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponHomeNodeEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid WeaponId,
    bool HomeNode,
    CharacterWeaponHomeNodeSemantics ExpectedSemantics);
