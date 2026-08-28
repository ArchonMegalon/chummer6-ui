using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewStateFactory : IWorkspaceOverviewStateFactory
{
    private readonly ICharacterCreationFoundationService? _creationFoundationService;
    private readonly ICharacterCreationContactsService? _creationContactsService;
    private readonly ICharacterCreationQualitiesService? _creationQualitiesService;
    private readonly ICharacterCreationMagicResonanceService? _creationMagicResonanceService;

    public WorkspaceOverviewStateFactory(
        ICharacterCreationFoundationService? creationFoundationService = null,
        ICharacterCreationContactsService? creationContactsService = null,
        ICharacterCreationQualitiesService? creationQualitiesService = null,
        ICharacterCreationMagicResonanceService? creationMagicResonanceService = null)
    {
        _creationFoundationService = creationFoundationService;
        _creationContactsService = creationContactsService;
        _creationQualitiesService = creationQualitiesService;
        _creationMagicResonanceService = creationMagicResonanceService;
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
        CharacterCreationContactsState? contacts = loadedOverview.Profile.Created
            ? null
            : LoadContacts(workspaceId, loadedOverview);
        CharacterCreationQualitiesState? qualities = loadedOverview.Profile.Created
            ? null
            : LoadQualities(workspaceId, loadedOverview);
        CharacterCreationMagicResonanceState? magicResonance = loadedOverview.Profile.Created
            ? null
            : LoadMagicResonance(workspaceId, loadedOverview);
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor =
            CharacterCreationMagicResonanceWorkflow.TryProject(
                magicResonance,
                out CharacterCreationMagicResonanceEditorState? projectedMagicResonance)
                ? projectedMagicResonance
                : null;
        return CreateState(
            currentState,
            workspaceId,
            session,
            loadedOverview,
            restoredView,
            foundation,
            contacts,
            qualities,
            magicResonance,
            magicResonanceEditor);
    }

    public CharacterOverviewState CreateActivatedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationInitialProjection initialCreation,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
    {
        ArgumentNullException.ThrowIfNull(initialCreation);
        CharacterCreationFoundationState foundation = RequireFoundation(
            workspaceId,
            loadedOverview,
            initialCreation.Foundation);
        CharacterCreationContactsState contacts = RequireContacts(
            workspaceId,
            loadedOverview,
            initialCreation.Contacts);
        CharacterCreationQualitiesState qualities = RequireQualities(
            workspaceId,
            loadedOverview,
            initialCreation.Qualities);
        CharacterCreationMagicResonanceState? magicResonance = SelectMagicResonance(
            workspaceId,
            loadedOverview,
            initialCreation.MagicResonance);
        RequireSupportingInitialProjection(initialCreation);
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor =
            CharacterCreationMagicResonanceWorkflow.TryProject(
                magicResonance,
                out CharacterCreationMagicResonanceEditorState? projectedMagicResonance)
                ? projectedMagicResonance
                : null;
        return CreateState(
            currentState,
            workspaceId,
            session,
            loadedOverview,
            restoredView,
            foundation,
            contacts,
            qualities,
            magicResonance,
            magicResonanceEditor);
    }

    private static CharacterOverviewState CreateState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        WorkspaceViewState? restoredView,
        CharacterCreationFoundationState? foundation,
        CharacterCreationContactsState? contacts,
        CharacterCreationQualitiesState? qualities,
        CharacterCreationMagicResonanceState? magicResonance,
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor)
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
            NavigationTabs: currentState.NavigationTabs)
        {
            CreationWizard = loadedOverview.Profile.Created
                ? null
                : CharacterCreationWizardProjector.Project(
                    workspaceId,
                    loadedOverview,
                    foundation,
                    contacts,
                    qualities,
                    magicResonance),
            CreationFoundation = foundation,
            CreationContacts = contacts,
            CreationQualities = qualities,
            CreationMagicResonance = magicResonance,
            CreationMagicResonanceEditor = magicResonanceEditor
        };
    }

    private static CharacterCreationFoundationState RequireFoundation(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationResult<CharacterCreationFoundationState> result)
        => result.Outcome == CharacterCreationFoundationOutcomes.Success
           && result.Value is CharacterCreationFoundationState state
           && BlockersMatch(result.Blockers, state.AuthorityBlockers)
           && CharacterCreationWizardProjector.MatchesLoadedOverview(
               workspaceId,
               loadedOverview,
               state)
            ? state
            : throw new InvalidDataException(
                "Creation activation foundation projection is not bound to the opened dossier.");

    private static CharacterCreationContactsState RequireContacts(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationContactResult<CharacterCreationContactsState> result)
        => result.Outcome == CharacterCreationContactOutcomes.Available
           && result.Value is CharacterCreationContactsState state
           && BlockersMatch(result.Blockers, state.Blockers)
           && CharacterCreationWizardProjector.MatchesLoadedOverview(
               workspaceId,
               loadedOverview,
               state)
            ? state
            : throw new InvalidDataException(
                "Creation activation contacts projection is not bound to the opened dossier.");

    private static CharacterCreationQualitiesState RequireQualities(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationResult<CharacterCreationQualitiesState> result)
        => result.Outcome == CharacterCreationFoundationOutcomes.Success
           && result.Value is CharacterCreationQualitiesState state
           && BlockersMatch(result.Blockers, state.Blockers)
           && CharacterCreationWizardProjector.MatchesLoadedOverview(
               workspaceId,
               loadedOverview,
               state)
            ? state
            : throw new InvalidDataException(
                "Creation activation qualities projection is not bound to the opened dossier.");

    private static CharacterCreationMagicResonanceState? SelectMagicResonance(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> result)
        => result.Outcome == CharacterCreationFoundationOutcomes.Success
           && result.Value is CharacterCreationMagicResonanceState state
           && BlockersMatch(result.Blockers, state.Blockers)
           && CharacterCreationWizardProjector.MatchesLoadedOverview(
               workspaceId,
               loadedOverview,
               state)
            ? state
            : null;

    private static void RequireSupportingInitialProjection(
        CharacterCreationInitialProjection initialCreation)
    {
        bool prerequisiteIsValid = initialCreation.Prerequisite.Outcome
                                   == CharacterCreationFoundationOutcomes.Success
                                   && initialCreation.Prerequisite.Value
                                       is CharacterCreationPrerequisiteState prerequisite
                                   && BlockersMatch(
                                       initialCreation.Prerequisite.Blockers,
                                       prerequisite.Blockers);
        bool attributesAreValid = initialCreation.Attributes.Outcome
                                  == CharacterCreationFoundationOutcomes.Success
                                  && initialCreation.Attributes.Value
                                      is CharacterCreationAttributesState attributes
                                  && BlockersMatch(
                                      initialCreation.Attributes.Blockers,
                                      attributes.Blockers);
        if (!prerequisiteIsValid || !attributesAreValid)
        {
            throw new InvalidDataException(
                "Creation activation supporting projections are incomplete.");
        }
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
               && BlockersMatch(result.Blockers, state.AuthorityBlockers)
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }

    private CharacterCreationContactsState? LoadContacts(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationContactsService is null)
            return null;

        CharacterCreationContactResult<CharacterCreationContactsState> result =
            _creationContactsService.Load(new CharacterCreationContactsLoadRequest(workspaceId));
        return result.Outcome == CharacterCreationContactOutcomes.Available
               && result.Value is CharacterCreationContactsState state
               && BlockersMatch(result.Blockers, state.Blockers)
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }

    private CharacterCreationQualitiesState? LoadQualities(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationQualitiesService is null)
            return null;

        CharacterCreationFoundationResult<CharacterCreationQualitiesState> result =
            _creationQualitiesService.Load(new CharacterCreationQualitiesLoadRequest(workspaceId));
        return result.Outcome == CharacterCreationFoundationOutcomes.Success
               && result.Value is CharacterCreationQualitiesState state
               && BlockersMatch(result.Blockers, state.Blockers)
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }

    private CharacterCreationMagicResonanceState? LoadMagicResonance(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationMagicResonanceService is null)
            return null;

        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> result =
            _creationMagicResonanceService.Load(
                new CharacterCreationMagicResonanceLoadRequest(workspaceId));
        return result.Outcome == CharacterCreationFoundationOutcomes.Success
               && result.Value is CharacterCreationMagicResonanceState state
               && BlockersMatch(result.Blockers, state.Blockers)
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }

    private static bool BlockersMatch(
        IReadOnlyList<string> resultBlockers,
        IReadOnlyList<string> stateBlockers)
        => resultBlockers
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(stateBlockers.Where(static blocker =>
                !string.IsNullOrWhiteSpace(blocker)));
}
