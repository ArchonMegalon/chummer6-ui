using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

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
}
