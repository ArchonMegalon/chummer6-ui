using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CyberwareActiveCommlinkEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid CyberwareId,
    bool ActiveCommlink,
    CharacterCyberwareActiveCommlinkSemantics ExpectedSemantics);
