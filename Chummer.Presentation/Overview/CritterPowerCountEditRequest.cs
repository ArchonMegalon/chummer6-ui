using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CritterPowerCountEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid CritterPowerId,
    bool CountsTowardsLimit);
