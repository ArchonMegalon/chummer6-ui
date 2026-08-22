using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record QualityLevelEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid QualityId,
    int ExpectedLevel,
    int MaximumLevel,
    int NewLevel,
    bool IncreaseConfirmed);
