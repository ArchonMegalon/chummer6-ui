using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearActiveCommlinkEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid GearId,
    bool ActiveCommlink,
    CharacterGearActiveCommlinkSemantics ExpectedSemantics);
