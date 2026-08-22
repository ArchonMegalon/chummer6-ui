using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleActiveCommlinkEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid VehicleId,
    bool ActiveCommlink,
    CharacterVehicleActiveCommlinkSemantics ExpectedSemantics);
