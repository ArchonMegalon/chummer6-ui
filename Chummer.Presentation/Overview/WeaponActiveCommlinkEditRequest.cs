using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponActiveCommlinkEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid WeaponId,
    bool ActiveCommlink,
    CharacterWeaponActiveCommlinkSemantics ExpectedSemantics);
