using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record PrototypeTranshumanEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid CyberwareId,
    bool PrototypeTranshuman,
    CharacterPrototypeTranshumanSemantics ExpectedSemantics);
