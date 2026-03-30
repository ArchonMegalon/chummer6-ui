using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewStateFactory : IWorkspaceOverviewStateFactory
{
    public CharacterOverviewState CreateLoadedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
    {
        return new CharacterOverviewState(
            IsBusy: false,
            Error: null,
            Session: session,
            WorkspaceId: workspaceId,
            OpenWorkspaces: session.OpenWorkspaces,
            Profile: loadedOverview.Profile,
            Progress: loadedOverview.Progress,
            Skills: loadedOverview.Skills,
            Rules: loadedOverview.Rules,
            Build: loadedOverview.Build,
            Movement: loadedOverview.Movement,
            Awakening: loadedOverview.Awakening,
            ActiveTabId: restoredView?.ActiveTabId,
            ActiveActionId: restoredView?.ActiveActionId,
            ActiveSectionId: restoredView?.ActiveSectionId,
            ActiveSectionJson: restoredView?.ActiveSectionJson,
            ActiveSectionRows: restoredView?.ActiveSectionRows ?? [],
            ActiveBuildLab: restoredView?.ActiveBuildLab,
            ActiveBrowseWorkspace: restoredView?.ActiveBrowseWorkspace,
            ActiveNpcPersonaStudio: restoredView?.ActiveNpcPersonaStudio,
            LastCommandId: currentState.LastCommandId,
            LatestPortabilityActivity: currentState.WorkspaceId is { } currentWorkspaceId
                && string.Equals(currentWorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal)
                ? currentState.LatestPortabilityActivity
                : null,
            Notice: currentState.Notice,
            ActiveDialog: null,
            Preferences: currentState.Preferences,
            Commands: currentState.Commands,
            NavigationTabs: currentState.NavigationTabs,
            HasSavedWorkspace: hasSavedWorkspace);
    }
}
