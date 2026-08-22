using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewStateFactory : IWorkspaceOverviewStateFactory
{
    private readonly ICharacterCreationFoundationService? _creationFoundationService;

    public WorkspaceOverviewStateFactory(
        ICharacterCreationFoundationService? creationFoundationService = null)
    {
        _creationFoundationService = creationFoundationService;
    }

    public CharacterOverviewState CreateLoadedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
    {
        CharacterCreationFoundationState? foundation = loadedOverview.Profile.Created
            ? null
            : LoadFoundation(workspaceId, loadedOverview);
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
            NavigationTabs: currentState.NavigationTabs)
        {
            CreationWizard = loadedOverview.Profile.Created
                ? null
                : CharacterCreationWizardProjector.Project(
                    workspaceId,
                    loadedOverview,
                    foundation),
            CreationFoundation = foundation
        };
    }

    private CharacterCreationFoundationState? LoadFoundation(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationFoundationService is null)
            return null;

        CharacterCreationFoundationResult<CharacterCreationFoundationState> result =
            _creationFoundationService.Load(new CharacterCreationFoundationLoadRequest(workspaceId));
        return result.Outcome == CharacterCreationFoundationOutcomes.Success
               && result.Value is CharacterCreationFoundationState state
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }
}
