using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceOverviewStateFactory :
    IWorkspaceOverviewStateFactory,
    IWorkspaceOverviewPreparationFactory
{
    private readonly ICharacterCreationFoundationService? _creationFoundationService;
    private readonly IOwnerBoundCharacterCreationLifeModuleFinalizationService? _ownerBoundFoundationReader;
    private readonly ICharacterCreationContactsService? _creationContactsService;
    private readonly IOwnerBoundCharacterCreationContactsService? _ownerBoundCreationContactsService;
    private readonly ICharacterCreationQualitiesService? _creationQualitiesService;
    private readonly IOwnerBoundCharacterCreationQualitiesService? _ownerBoundCreationQualitiesService;
    private readonly ICharacterCreationMagicResonanceService? _creationMagicResonanceService;
    private readonly IOwnerBoundCharacterCreationMagicResonanceService? _ownerBoundCreationMagicResonanceService;
    private readonly ICharacterCreationLifestylesService? _creationLifestylesService;
    private readonly IOwnerBoundCharacterCreationLifestylesReader? _ownerBoundCreationLifestylesReader;
    private readonly ICharacterCreationFinalizationService? _creationFinalizationService;
    private readonly IOwnerBoundCharacterCreationFinalizationService? _ownerBoundCreationFinalizationService;

    public WorkspaceOverviewStateFactory(
        ICharacterCreationFoundationService? creationFoundationService = null,
        ICharacterCreationContactsService? creationContactsService = null,
        ICharacterCreationQualitiesService? creationQualitiesService = null,
        ICharacterCreationMagicResonanceService? creationMagicResonanceService = null,
        ICharacterCreationLifestylesService? creationLifestylesService = null,
        ICharacterCreationFinalizationService? creationFinalizationService = null,
        IOwnerBoundCharacterCreationContactsService? ownerBoundCreationContactsService = null,
        IOwnerBoundCharacterCreationFinalizationService? ownerBoundCreationFinalizationService = null,
        IOwnerBoundCharacterCreationLifestylesReader? ownerBoundCreationLifestylesReader = null,
        IOwnerBoundCharacterCreationLifeModuleFinalizationService? ownerBoundFoundationReader = null,
        IOwnerBoundCharacterCreationQualitiesService? ownerBoundCreationQualitiesService = null,
        IOwnerBoundCharacterCreationMagicResonanceService? ownerBoundCreationMagicResonanceService = null)
    {
        _creationFoundationService = creationFoundationService;
        _ownerBoundFoundationReader = ownerBoundFoundationReader;
        _creationContactsService = creationContactsService;
        _ownerBoundCreationContactsService = ownerBoundCreationContactsService;
        _creationQualitiesService = creationQualitiesService;
        _ownerBoundCreationQualitiesService = ownerBoundCreationQualitiesService;
        _creationMagicResonanceService = creationMagicResonanceService;
        _ownerBoundCreationMagicResonanceService = ownerBoundCreationMagicResonanceService;
        _creationLifestylesService = creationLifestylesService;
        _ownerBoundCreationLifestylesReader = ownerBoundCreationLifestylesReader;
        _creationFinalizationService = creationFinalizationService;
        _ownerBoundCreationFinalizationService = ownerBoundCreationFinalizationService;
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
        bool needsFoundation = !loadedOverview.Profile.Created && !HasUnrelatedCreationMethod(loadedOverview);
        bool unrelatedPriorityDraft = HasUnrelatedPriorityCreationMethod(loadedOverview);
        // Share only a complete owner-bound composition. A denied shared read
        // must not retry through independent readers or an ambient local owner.
        var foundationOverviewReader = needsFoundation
            ? _ownerBoundFoundationReader as IOwnerBoundCharacterCreationOverviewReader : null;
        var overviewReader = foundationOverviewReader
            ?? _ownerBoundCreationFinalizationService as IOwnerBoundCharacterCreationOverviewReader;
        bool useSharedRead = !loadedOverview.Profile.Created
            && loadedOverview.DisplayOwnerContext is { IsValid: true }
            && overviewReader is not null
            && _ownerBoundCreationFinalizationService is not null
            && _ownerBoundCreationContactsService is not null
            && _ownerBoundCreationQualitiesService is not null
            && _ownerBoundCreationMagicResonanceService is not null
            && _ownerBoundCreationLifestylesReader is not null;
        CharacterCreationOverviewRead? shared = useSharedRead
            ? overviewReader!.LoadOverview(loadedOverview.DisplayOwnerContext!.Value,
                workspaceId, includePriorityDrafts: !unrelatedPriorityDraft)
            : null;
        CharacterCreationFoundationState? foundation = !needsFoundation ? null
            : useSharedRead && foundationOverviewReader is not null
                ? SelectFoundation(workspaceId, loadedOverview, shared?.Foundation)
                : LoadFoundation(workspaceId, loadedOverview);
        CharacterCreationContactsState? contacts = loadedOverview.Profile.Created
            ? null
            : useSharedRead ? SelectContacts(workspaceId, loadedOverview, shared?.Contacts)
            : LoadContacts(workspaceId, loadedOverview);
        CharacterCreationQualitiesState? qualities = loadedOverview.Profile.Created || unrelatedPriorityDraft
            ? null
            : useSharedRead ? SelectQualities(workspaceId, loadedOverview, shared?.Qualities)
            : LoadQualities(workspaceId, loadedOverview);
        CharacterCreationMagicResonanceState? magicResonance = loadedOverview.Profile.Created || unrelatedPriorityDraft
            ? null
            : useSharedRead ? SelectMagicResonance(workspaceId, loadedOverview, shared?.MagicResonance)
            : LoadMagicResonance(workspaceId, loadedOverview);
        CharacterCreationLifestylesState? lifestyles = loadedOverview.Profile.Created
            ? null
            : useSharedRead ? SelectLifestyles(workspaceId, loadedOverview, shared?.Lifestyles)
            : LoadLifestyles(workspaceId, loadedOverview);
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? finalization =
            loadedOverview.Profile.Created
                ? null
                : useSharedRead ? SelectFinalization(workspaceId, loadedOverview, shared?.Finalization)
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
        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState>? result)
        => result is not null && result.Outcome == CharacterCreationFoundationOutcomes.Success
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

    private static bool HasUnrelatedCreationMethod(WorkspaceOverviewLoadResult overview)
        // Foundation describes only the Life Modules draft. Do not parse its
        // catalog during every Priority/Sum-to-Ten/Karma restore. Unknown or
        // disagreeing method projections keep the existing fail-closed path.
        => overview.Document?.RulesetId == RulesetDefaults.Sr5
           && overview.Profile.BuildMethod is CharacterCreationBuildMethods.Priority
               or CharacterCreationBuildMethods.SumToTen or CharacterCreationBuildMethods.Karma
           && string.Equals(overview.Profile.BuildMethod, overview.Build.BuildMethod, StringComparison.Ordinal);

    private static bool HasUnrelatedPriorityCreationMethod(WorkspaceOverviewLoadResult overview)
        // These two readers project Priority/Sum-to-Ten drafts, not the separate
        // Karma or Life Modules editors. Their snapshots cannot supply those
        // methods' overview authority. Do not construct their source catalogs on
        // restore; keep the finalizer and method-specific readers unchanged.
        // Unknown rulesets or disagreeing method projections retain every check.
        => overview.Document?.RulesetId == RulesetDefaults.Sr5
           && overview.Profile.BuildMethod is CharacterCreationBuildMethods.Karma
               or CharacterCreationBuildMethods.LifeModules
           && string.Equals(overview.Profile.BuildMethod, overview.Build.BuildMethod, StringComparison.Ordinal);

    private CharacterCreationFoundationState? LoadFoundation(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        // A linked workspace must never fall back to the local-single-user store.
        CharacterCreationFoundationResult<CharacterCreationFoundationState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } owner && _ownerBoundFoundationReader is not null
                ? _ownerBoundFoundationReader.Load(owner, workspaceId)
                : loadedOverview.DisplayOwnerContext is null or { Owner.IsLocalSingleUser: true }
                    ? _creationFoundationService?.Load(new CharacterCreationFoundationLoadRequest(workspaceId)) : null;
        return SelectFoundation(workspaceId, loadedOverview, result);
    }

    private static CharacterCreationFoundationState? SelectFoundation(
        CharacterWorkspaceId workspaceId, WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationResult<CharacterCreationFoundationState>? result)
    {
        if (result is null)
            return null;
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
        return SelectContacts(workspaceId, loadedOverview, result);
    }

    private static CharacterCreationContactsState? SelectContacts(
        CharacterWorkspaceId workspaceId, WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationContactResult<CharacterCreationContactsState>? result)
    {
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
        if (_creationQualitiesService is null && _ownerBoundCreationQualitiesService is null)
            return null;

        var request = new CharacterCreationQualitiesLoadRequest(workspaceId);
        CharacterCreationFoundationResult<CharacterCreationQualitiesState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } original
                ? _ownerBoundCreationQualitiesService?.Load(original, request)
                : loadedOverview.DisplayOwnerContext is null && _ownerBoundCreationQualitiesService is null
                    ? _creationQualitiesService?.Load(request) : null;
        return SelectQualities(workspaceId, loadedOverview, result);
    }

    private static CharacterCreationQualitiesState? SelectQualities(
        CharacterWorkspaceId workspaceId, WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationResult<CharacterCreationQualitiesState>? result)
    {
        if (result is null)
            return null;
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
        if (_creationMagicResonanceService is null && _ownerBoundCreationMagicResonanceService is null)
            return null;

        var request = new CharacterCreationMagicResonanceLoadRequest(workspaceId);
        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } original
                ? _ownerBoundCreationMagicResonanceService?.Load(original, request)
                : loadedOverview.DisplayOwnerContext is null && _ownerBoundCreationMagicResonanceService is null
                    ? _creationMagicResonanceService?.Load(request) : null;
        return SelectMagicResonance(workspaceId, loadedOverview, result);
    }

    private CharacterCreationLifestylesState? LoadLifestyles(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        if (_creationLifestylesService is null && _ownerBoundCreationLifestylesReader is null)
            return null;

        var request = new CharacterCreationLifestylesLoadRequest(workspaceId);
        CharacterCreationLifestyleResult<CharacterCreationLifestylesState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } original
                ? _ownerBoundCreationLifestylesReader?.Load(original, request)
                : loadedOverview.DisplayOwnerContext is null && _ownerBoundCreationLifestylesReader is null
                    ? _creationLifestylesService?.Load(request) : null;
        return SelectLifestyles(workspaceId, loadedOverview, result);
    }

    private static CharacterCreationLifestylesState? SelectLifestyles(
        CharacterWorkspaceId workspaceId, WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationLifestyleResult<CharacterCreationLifestylesState>? result)
    {
        if (result is null)
            return null;
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
        if (_creationFinalizationService is null && _ownerBoundCreationFinalizationService is null)
            return null;

        var request = new CharacterCreationFinalizationLoadRequest(workspaceId);
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? result =
            loadedOverview.DisplayOwnerContext is { IsValid: true } original
                ? _ownerBoundCreationFinalizationService?.Load(original, request)
                : loadedOverview.DisplayOwnerContext is null && _ownerBoundCreationFinalizationService is null
                    ? _creationFinalizationService?.Load(request) : null;
        return SelectFinalization(workspaceId, loadedOverview, result);
    }

    private static CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? SelectFinalization(
        CharacterWorkspaceId workspaceId, WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? result)
    {
        if (result is null)
            return null;
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
