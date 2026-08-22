using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record LifestyleIncrementEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid LifestyleId,
    CharacterLifestyleIncrementAction Action,
    int? RequestedIncrements,
    CharacterLifestyleIncrementState ExpectedState);
