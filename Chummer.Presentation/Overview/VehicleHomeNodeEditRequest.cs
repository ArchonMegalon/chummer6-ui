using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleHomeNodeEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid VehicleId,
    bool HomeNode);
