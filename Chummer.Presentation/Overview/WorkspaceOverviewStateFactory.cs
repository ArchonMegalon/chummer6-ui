using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewStateFactory :
    IWorkspaceOverviewStateFactory,
    IWorkspaceOverviewPreparationFactory
{
    private readonly ICharacterCreationFoundationService? _creationFoundationService;
    private readonly ICharacterCreationContactsService? _creationContactsService;
    private readonly IOwnerBoundCharacterCreationContactsService? _ownerBoundCreationContactsService;
    private readonly ICharacterCreationQualitiesService? _creationQualitiesService;
    private readonly ICharacterCreationMagicResonanceService? _creationMagicResonanceService;
    private readonly ICharacterCreationLifestylesService? _creationLifestylesService;
    private readonly ICharacterCreationFinalizationService? _creationFinalizationService;

    public WorkspaceOverviewStateFactory(
        ICharacterCreationFoundationService? creationFoundationService = null,
        ICharacterCreationContactsService? creationContactsService = null,
        ICharacterCreationQualitiesService? creationQualitiesService = null,
        ICharacterCreationMagicResonanceService? creationMagicResonanceService = null,
        ICharacterCreationLifestylesService? creationLifestylesService = null,
        ICharacterCreationFinalizationService? creationFinalizationService = null,
        IOwnerBoundCharacterCreationContactsService? ownerBoundCreationContactsService = null)
    {
        _creationFoundationService = creationFoundationService;
        _creationContactsService = creationContactsService;
        _ownerBoundCreationContactsService = ownerBoundCreationContactsService;
        _creationQualitiesService = creationQualitiesService;
        _creationMagicResonanceService = creationMagicResonanceService;
        _creationLifestylesService = creationLifestylesService;
        _creationFinalizationService = creationFinalizationService;
    }

    public CharacterOverviewState CreateLoadedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
        => PrepareLoaded(currentState, workspaceId, loadedOverview).Create(session, restoredView);

    PreparedWorkspaceOverviewState IWorkspaceOverviewPreparationFactory.PrepareLoaded(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult overview)
        => PrepareLoaded(currentState, workspaceId, overview);

    private PreparedWorkspaceOverviewState PrepareLoaded(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
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
        CharacterCreationLifestylesState? lifestyles = loadedOverview.Profile.Created
            ? null
            : LoadLifestyles(workspaceId, loadedOverview);
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? finalization =
            loadedOverview.Profile.Created
                ? null
                : LoadFinalization(workspaceId, loadedOverview);
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor =
            CharacterCreationMagicResonanceWorkflow.TryProject(
                magicResonance,
                out CharacterCreationMagicResonanceEditorState? projectedMagicResonance)
                ? projectedMagicResonance
                : null;
        return PrepareState(
            currentState,
            workspaceId,
            loadedOverview,
            foundation,
            contacts,
            qualities,
            magicResonance,
            magicResonanceEditor,
            lifestyles,
            finalization);
    }

    public CharacterOverviewState CreateActivatedState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceSessionState session,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationInitialProjection initialCreation,
        WorkspaceViewState? restoredView,
        bool hasSavedWorkspace)
        => PrepareActivated(currentState, workspaceId, loadedOverview, initialCreation).Create(session, restoredView);

    PreparedWorkspaceOverviewState IWorkspaceOverviewPreparationFactory.PrepareActivated(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult overview,
        CharacterCreationInitialProjection initialCreation)
        => PrepareActivated(currentState, workspaceId, overview, initialCreation);

    private PreparedWorkspaceOverviewState PrepareActivated(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationInitialProjection initialCreation)
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
        CharacterCreationLifestylesState? lifestyles = LoadLifestyles(workspaceId, loadedOverview);
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? finalization =
            LoadFinalization(workspaceId, loadedOverview);
        RequireSupportingInitialProjection(initialCreation);
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor =
            CharacterCreationMagicResonanceWorkflow.TryProject(
                magicResonance,
                out CharacterCreationMagicResonanceEditorState? projectedMagicResonance)
                ? projectedMagicResonance
                : null;
        return PrepareState(
            currentState,
            workspaceId,
            loadedOverview,
            foundation,
            contacts,
            qualities,
            magicResonance,
            magicResonanceEditor,
            lifestyles,
            finalization);
    }

    private static PreparedWorkspaceOverviewState PrepareState(
        CharacterOverviewState currentState,
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationState? foundation,
        CharacterCreationContactsState? contacts,
        CharacterCreationQualitiesState? qualities,
        CharacterCreationMagicResonanceState? magicResonance,
        CharacterCreationMagicResonanceEditorState? magicResonanceEditor,
        CharacterCreationLifestylesState? lifestyles,
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? finalization)
    {
        CharacterCreationWizardSnapshot? wizard = loadedOverview.Profile.Created
            ? null
            : CharacterCreationWizardProjector.Project(
                workspaceId, loadedOverview, foundation, contacts, qualities,
                magicResonance, lifestyles, finalization);
        return new PreparedWorkspaceOverviewState((session, restoredView) => new CharacterOverviewState(
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
            // Cached view payloads have no owner-read provenance. Preserve the
            // navigation preference, but obtain section content from a fresh read.
            ActiveSectionId: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveSectionId : null,
            ActiveSectionJson: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveSectionJson : null,
            ActiveSectionRows: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveSectionRows ?? [] : [],
            ActiveBuildLab: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveBuildLab : null,
            ActiveBrowseWorkspace: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveBrowseWorkspace : null,
            ActiveNpcPersonaStudio: loadedOverview.DisplayOwnerContext is null ? restoredView?.ActiveNpcPersonaStudio : null,
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
            DisplayOwnerContext = loadedOverview.DisplayOwnerContext,
            CreationWizard = wizard,
            CreationFoundation = foundation,
            CreationContacts = contacts,
            CreationQualities = qualities,
            CreationMagicResonance = magicResonance,
            CreationMagicResonanceEditor = magicResonanceEditor,
            CreationLifestyles = lifestyles,
            CreationFinalization = finalization?.Value
        });
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
        if (_creationContactsService is null && _ownerBoundCreationContactsService is null)
            return null;

        var request = new CharacterCreationContactsLoadRequest(workspaceId);
        CharacterCreationContactResult<CharacterCreationContactsState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } original
                ? _ownerBoundCreationContactsService?.Load(original, request)
                : loadedOverview.DisplayOwnerContext is null && _ownerBoundCreationContactsService is null
                    ? _creationContactsService?.Load(request) : null;
        if (result is null)
            return null;
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

    private CharacterCreationLifestylesState? LoadLifestyles(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationLifestylesService is null)
            return null;

        CharacterCreationLifestyleResult<CharacterCreationLifestylesState> result =
            _creationLifestylesService.Load(new CharacterCreationLifestylesLoadRequest(workspaceId));
        return result.Outcome == CharacterCreationLifestyleOutcomes.Available
               && result.Value is CharacterCreationLifestylesState state
               && BlockersMatch(result.Blockers, state.Blockers)
               && CharacterCreationWizardProjector.MatchesLoadedOverview(
                   workspaceId,
                   loadedOverview,
                   state)
            ? state
            : null;
    }

    private CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? LoadFinalization(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationFinalizationService is null)
            return null;

        CharacterCreationFinalizationResult<CharacterCreationFinalizationState> result =
            _creationFinalizationService.Load(new CharacterCreationFinalizationLoadRequest(workspaceId));
        return CharacterCreationWizardProjector.MatchesLoadedOverview(
            workspaceId,
            loadedOverview,
            result)
            ? result
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
