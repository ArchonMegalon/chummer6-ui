using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceSessionPresenter
{
    WorkspaceSessionState State { get; }

    WorkspaceSessionState Restore(IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null);

    WorkspaceSessionState Open(CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId = null);

    WorkspaceSessionState Switch(CharacterWorkspaceId id);

    WorkspaceSessionState ClearActive();

    WorkspaceSessionState Close(CharacterWorkspaceId id);

    WorkspaceSessionState CloseAll();

    WorkspaceSessionState SetSavedStatus(CharacterWorkspaceId id, bool hasSavedWorkspace);

    bool Contains(CharacterWorkspaceId id);
}
