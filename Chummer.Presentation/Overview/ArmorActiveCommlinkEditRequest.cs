using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ArmorActiveCommlinkEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid ArmorId,
    bool ActiveCommlink);
