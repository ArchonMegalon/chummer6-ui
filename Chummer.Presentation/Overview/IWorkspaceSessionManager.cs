using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceSessionManager
{
    IReadOnlyList<OpenWorkspaceState> Restore(IReadOnlyList<WorkspaceListItem> workspaces);

    IReadOnlyList<OpenWorkspaceState> Activate(
        IReadOnlyList<OpenWorkspaceState> existing,
        CharacterWorkspaceId id,
        CharacterProfileSection? profile,
        string? rulesetId = null);

    IReadOnlyList<OpenWorkspaceState> Close(
        IReadOnlyList<OpenWorkspaceState> existing,
        CharacterWorkspaceId id);

    CharacterWorkspaceId? SelectNext(IReadOnlyList<OpenWorkspaceState> workspaces);
}
