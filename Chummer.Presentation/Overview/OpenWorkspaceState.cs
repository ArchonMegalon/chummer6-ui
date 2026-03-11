using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record OpenWorkspaceState(
    CharacterWorkspaceId Id,
    string Name,
    string Alias,
    DateTimeOffset LastOpenedUtc,
    string RulesetId,
    bool HasSavedWorkspace = false);
