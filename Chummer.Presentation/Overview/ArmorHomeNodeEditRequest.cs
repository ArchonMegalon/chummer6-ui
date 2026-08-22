using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ArmorHomeNodeEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid ArmorId,
    bool HomeNode);
