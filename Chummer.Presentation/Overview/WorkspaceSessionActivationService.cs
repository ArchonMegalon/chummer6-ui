using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSessionActivationService : IWorkspaceSessionActivationService
{
    public WorkspaceSessionState Activate(
        OwnerContextStamp originalOwner,
        IWorkspaceSessionPresenter sessionPresenter,
        CharacterWorkspaceId workspaceId,
        CharacterProfileSection? profile,
        WorkspaceSessionState? sessionSeed,
        bool updateSession,
        string? rulesetId = null)
    {
        if (!originalOwner.IsValid || (sessionSeed is not null && sessionSeed.OwnerContext != originalOwner))
            throw new InvalidOperationException("Roster activation requires the original loaded owner.");
        if (sessionPresenter.State.OwnerContext != originalOwner)
            sessionPresenter.Restore(originalOwner, [], null);
        if (sessionSeed is null && updateSession)
            return sessionPresenter.Open(originalOwner, workspaceId, profile, rulesetId);
        WorkspaceSessionState session = sessionPresenter.Switch(originalOwner, workspaceId);
        return session.ActiveWorkspaceId == workspaceId
            ? session
            : sessionPresenter.Open(originalOwner, workspaceId, profile, rulesetId);
    }

    public WorkspaceSessionState Activate(
        IWorkspaceSessionPresenter sessionPresenter,
        CharacterWorkspaceId workspaceId,
        CharacterProfileSection? profile,
        WorkspaceSessionState? sessionSeed,
        bool updateSession,
        string? rulesetId = null)
    {
        if (sessionSeed is null && updateSession)
        {
            return sessionPresenter.Open(workspaceId, profile, rulesetId);
        }

        WorkspaceSessionState session = sessionPresenter.Switch(workspaceId);
        if (session.ActiveWorkspaceId is null
            || !string.Equals(session.ActiveWorkspaceId.Value.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            session = sessionPresenter.Open(workspaceId, profile, rulesetId);
        }

        return session;
    }
}
