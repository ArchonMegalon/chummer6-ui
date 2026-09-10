using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceSessionActivationService
{
    WorkspaceSessionState Activate(
        IWorkspaceSessionPresenter sessionPresenter,
        CharacterWorkspaceId workspaceId,
        CharacterProfileSection? profile,
        WorkspaceSessionState? sessionSeed,
        bool updateSession,
        string? rulesetId = null);

    WorkspaceSessionState Activate(
        OwnerContextStamp originalOwner,
        IWorkspaceSessionPresenter sessionPresenter,
        CharacterWorkspaceId workspaceId,
        CharacterProfileSection? profile,
        WorkspaceSessionState? sessionSeed,
        bool updateSession,
        string? rulesetId = null)
        => throw new NotSupportedException("Owner-bound roster activation is unavailable.");
}
