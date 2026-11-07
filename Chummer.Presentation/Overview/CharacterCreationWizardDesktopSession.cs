using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationWizardDesktopSchemas
{
    public const string CheckpointV1 = "chummer.character_creation_wizard.desktop_checkpoint.v1";
    public const string BuildGhostContextV1 = "chummer.character_creation_wizard.build_ghost_context.v1";
}

public static class CharacterCreationWizardCheckpointInvalidationReasons
{
    public const string InvalidCheckpoint = "creation-wizard-checkpoint-invalid";
    public const string WorkspaceChanged = "creation-wizard-checkpoint-workspace-changed";
    public const string WorkspaceRevisionChanged = "creation-wizard-checkpoint-revision-changed";
    public const string SnapshotChanged = "creation-wizard-checkpoint-snapshot-changed";
    public const string StepUnavailable = "creation-wizard-checkpoint-step-unavailable";
}

/// <summary>
/// A desktop checkpoint contains UI navigation only. It never persists rules answers or
/// character mutations; those remain owned by the canonical workspace and rules authorities.
/// </summary>
public sealed record CharacterCreationWizardDesktopCheckpoint(
    string Schema,
    string WorkspaceId,
    long WorkspaceRevision,
    string SnapshotDigest,
    string SelectedStepId);

public sealed record CharacterCreationWizardDesktopResume(
    bool Restored,
    string? InvalidationReason);

public sealed record CharacterCreationWizardDesktopStep(
    string StepId,
    string Label,
    string Status,
    bool IsRequired,
    bool CanEnter,
    bool IsComplete,
    bool IsSelected,
    IReadOnlyList<string> BudgetIds,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> LegalNextStepIds);

public sealed record CharacterCreationWizardDesktopBudget(
    string BudgetId,
    string Label,
    decimal Total,
    decimal Used,
    decimal Remaining,
    bool IsExact,
    IReadOnlyList<string> Blockers,
    string Unit);

public sealed record CharacterCreationWizardDesktopOption(
    string OptionId,
    string? VersionId,
    string Label,
    bool IsEnabled,
    string? DisableReasonKey,
    IReadOnlyDictionary<string, string> DisableReasonArguments,
    IReadOnlyList<CharacterCreationChoiceCost> Costs,
    IReadOnlyList<CharacterCreationChoiceConsequence> Consequences,
    IReadOnlyList<string> SourceAnchorIds,
    string? SourceId,
    int? SourcePage)
{
    public IReadOnlyList<LifeModuleFollowUpPromptDto> FollowUps { get; init; } = [];
}

public sealed record CharacterCreationWizardDesktopContactField(
    string FieldId,
    string Label,
    string ValueKind,
    bool IsEditable,
    string SerializedValue,
    int? Minimum,
    int? Maximum,
    IReadOnlyList<CharacterCreationContactOption> LegalOptions,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds);

public sealed record CharacterCreationWizardDesktopContact(
    Guid ContactId,
    string Name,
    string Role,
    int ContactPointCost,
    bool CountsAgainstContactBudget,
    bool CountsAgainstHighPlacesBudget,
    IReadOnlyList<CharacterCreationWizardDesktopContactField> Fields,
    IReadOnlyList<string> SourceAnchorIds,
    string ContactDigest,
    bool CanDelete = false);

public sealed record CharacterCreationWizardDesktopContactsStep(
    CharacterCreationContactBinding Binding,
    IReadOnlyList<CharacterCreationWizardDesktopContact> Contacts,
    CharacterCreationContactBudget ContactBudget,
    CharacterCreationContactBudget HighPlacesBudget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest)
{
    /// <summary>
    /// Core-owned add-contact defaults (ContactId = Guid.Empty). Null when Core authority is
    /// unavailable. Read-only projection: the desktop session never persists this template.
    /// </summary>
    public CharacterCreationContactProjection? NewContactTemplate { get; init; }
}

/// <summary>
/// Read-only lifestyle projection for the Contacts &amp; Lifestyles wizard step.
/// The desktop session never mutates these values; mutations flow through the
/// Core-owned lifestyles interaction presenter.
/// </summary>
public sealed record CharacterCreationWizardDesktopLifestyle(
    CharacterCreationLifestyleConfiguration Configuration,
    Guid SourceId,
    string BaseLifestyleName,
    string SourceBook,
    string Page,
    CharacterCreationLifestyleEconomics Economics,
    IReadOnlyList<string> SourceAnchorIds,
    string LifestyleDigest);

public sealed record CharacterCreationWizardDesktopLifestylesStep(
    CharacterCreationLifestyleBinding Binding,
    CharacterCreationLifestylesAuthority Authority,
    IReadOnlyList<CharacterCreationWizardDesktopLifestyle> Lifestyles,
    CharacterCreationLifestyleBudget Budget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

/// <summary>
/// Revision-bound, read-only context for an interactive Build Ghost turn. It deliberately
/// contains no command, mutation request, payload XML, or confirm/finalize capability.
/// </summary>
public sealed record CharacterCreationWizardBuildGhostContext(
    string Schema,
    string WorkspaceId,
    long WorkspaceRevision,
    string WizardSnapshotDigest,
    string ActiveStepId,
    string RulesetId,
    string RuntimeFingerprint,
    string BuildMethod,
    IReadOnlyList<CharacterCreationWizardDesktopBudget> Budgets,
    IReadOnlyList<CharacterCreationWizardDesktopOption> LegalOptions,
    IReadOnlyList<string> CompletionBlockers,
    IReadOnlyList<string> Warnings,
    CharacterCreationWizardDesktopContactsStep? ContactsStep = null,
    CharacterCreationWizardDesktopLifestylesStep? LifestylesStep = null);

public sealed record CharacterCreationWizardDesktopState(
    string WorkspaceId,
    long WorkspaceRevision,
    string SnapshotDigest,
    string ActiveStepId,
    IReadOnlyList<CharacterCreationWizardDesktopStep> Steps,
    IReadOnlyList<CharacterCreationWizardDesktopBudget> Budgets,
    IReadOnlyList<CharacterCreationWizardDesktopOption> LegalOptions,
    IReadOnlyList<string> CompletionBlockers,
    IReadOnlyList<string> Warnings,
    bool CanContinue,
    bool CanFinalize,
    bool AdvancedEditorUnlocked,
    bool BuildGhostAvailable,
    CharacterCreationWizardDesktopResume Resume,
    CharacterCreationWizardBuildGhostContext BuildGhostContext,
    CharacterCreationWizardDesktopContactsStep? ContactsStep = null,
    CharacterCreationWizardDesktopLifestylesStep? LifestylesStep = null,
    CharacterCreationWizardDesktopJourneyStep? JourneyStep = null);

/// <summary>Authoritative, renderer-neutral projection of the Life-Modules
/// journey (stages 2..5). The stage options ride the regular LegalOptions
/// channel for the life-modules step; this record adds only stage context
/// the renderer cannot derive. No workspace writes, no derived legality.</summary>
public sealed record CharacterCreationWizardDesktopJourneyStep(
    int CurrentStageOrder,
    string CurrentStageId,
    string CurrentStageLabel,
    bool SelectionFinished,
    bool CanFinishSelection);

public static class CharacterCreationWizardBuildGhostPolicy
{
    public static bool IsAuthorized(CharacterCreationWizardSnapshot snapshot)
        => !string.IsNullOrWhiteSpace(snapshot.RuntimeFingerprint)
           && !snapshot.CompletionBlockers.Contains(
               CharacterCreationWizardProjector.RuntimeAuthorityUnavailable,
               StringComparer.Ordinal)
           && !snapshot.CompletionBlockers.Contains(
               CharacterCreationWizardProjector.BuildGhostContextUnavailable,
               StringComparer.Ordinal);

    public static bool CanSend(
        CharacterCreationWizardDesktopState state,
        bool aiPreferenceEnabled)
        => aiPreferenceEnabled && state.BuildGhostAvailable;
}

/// <summary>
/// Feature gate for the creation wizard's Finish-with-AI affordance. The flow is
/// still under active qualification (WINDOWS_HANDOFF 2026-11-02, item 2), so it
/// ships dark by default: neither the wizard control nor the desktop window may
/// surface or actuate it until an explicit operator/journey opt-in enables it
/// for the session. The window and the mouse-first journey honor the same
/// environment switch, so one override drives every entry point.
/// </summary>
public static class CharacterCreationAiFinishGatePolicy
{
    /// <summary>
    /// Environment switch shared by the desktop window gate and the mouse-first
    /// journey runner; set to "1" to opt the current session in.
    /// </summary>
    public const string SessionEnvVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH";

    /// <summary>True only for an explicit operator/journey opt-in.</summary>
    public static bool IsSessionEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable(SessionEnvVariable),
            "1",
            StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Platform-neutral desktop session for the source-driven creation wizard. Navigation consumes
/// the typed route graph projected by Presentation; it never invents legal choices or budgets.
/// </summary>
public sealed class CharacterCreationWizardDesktopSession
{
    private CharacterCreationWizardSnapshot? _snapshot;
    private CharacterCreationContactsState? _contacts;
    private CharacterCreationLifestylesState? _lifestyles;
    private CharacterCreationLifeModuleJourneyInteractionState? _journey;
    private CharacterCreationWizardDesktopState? _state;

    public CharacterCreationWizardDesktopState State
        => _state ?? throw new InvalidOperationException("Bind a wizard snapshot before reading state.");

    public CharacterCreationWizardDesktopState Bind(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationWizardDesktopCheckpoint? checkpoint = null,
        CharacterCreationContactsState? contacts = null,
        CharacterCreationLifestylesState? lifestyles = null,
        CharacterCreationLifeModuleJourneyInteractionState? journey = null)
    {
        ValidateSnapshot(snapshot);

        CharacterCreationWizardDesktopResume resume = ResolveResume(snapshot, checkpoint, out string selectedStepId);
        _snapshot = snapshot;
        _contacts = ContactsMatchSnapshot(snapshot, contacts) ? contacts : null;
        _lifestyles = LifestylesMatchSnapshot(snapshot, lifestyles) ? lifestyles : null;
        _journey = JourneyMatchesSnapshot(snapshot, journey) ? journey : null;
        _state = Project(snapshot, selectedStepId, resume);
        return _state;
    }

    public bool TrySelectStep(string stepId)
    {
        CharacterCreationWizardSnapshot snapshot = _snapshot
            ?? throw new InvalidOperationException("Bind a wizard snapshot before navigating.");
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        CharacterCreationWizardStageState? target = snapshot.Steps.SingleOrDefault(step =>
            string.Equals(step.StepId, stepId, StringComparison.Ordinal));
        if (target is null || !CanEnter(snapshot, target))
            return false;

        _state = Project(
            snapshot,
            target.StepId,
            new CharacterCreationWizardDesktopResume(Restored: false, InvalidationReason: null));
        return true;
    }

    public bool TryContinue()
    {
        CharacterCreationWizardSnapshot snapshot = _snapshot
            ?? throw new InvalidOperationException("Bind a wizard snapshot before navigating.");
        CharacterCreationWizardDesktopState current = State;
        CharacterCreationWizardStageState active = snapshot.Steps.Single(step =>
            string.Equals(step.StepId, current.ActiveStepId, StringComparison.Ordinal));
        CharacterCreationWizardStageState? next = active.LegalNextStepIds
            .Select(nextId => snapshot.Steps.SingleOrDefault(step =>
                string.Equals(step.StepId, nextId, StringComparison.Ordinal)))
            .FirstOrDefault(step => step is not null && CanEnter(snapshot, step));
        return next is not null && TrySelectStep(next.StepId);
    }

    public CharacterCreationWizardDesktopCheckpoint CreateCheckpoint()
    {
        CharacterCreationWizardDesktopState state = State;
        return new CharacterCreationWizardDesktopCheckpoint(
            Schema: CharacterCreationWizardDesktopSchemas.CheckpointV1,
            WorkspaceId: state.WorkspaceId,
            WorkspaceRevision: state.WorkspaceRevision,
            SnapshotDigest: state.SnapshotDigest,
            SelectedStepId: state.ActiveStepId);
    }

    public static byte[] SerializeCheckpoint(CharacterCreationWizardDesktopCheckpoint checkpoint)
    {
        ValidateCheckpointShape(checkpoint);
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint);
    }

    public static bool TryDeserializeCheckpoint(
        ReadOnlySpan<byte> payload,
        out CharacterCreationWizardDesktopCheckpoint? checkpoint)
    {
        checkpoint = null;
        try
        {
            CharacterCreationWizardDesktopCheckpoint? parsed =
                JsonSerializer.Deserialize<CharacterCreationWizardDesktopCheckpoint>(payload);
            if (parsed is null)
                return false;
            ValidateCheckpointShape(parsed);
            checkpoint = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static string SerializeBuildGhostContext(CharacterCreationWizardBuildGhostContext context)
        => JsonSerializer.Serialize(context);

    private CharacterCreationWizardDesktopState Project(
        CharacterCreationWizardSnapshot snapshot,
        string selectedStepId,
        CharacterCreationWizardDesktopResume resume)
    {
        CharacterCreationWizardStageState active = snapshot.Steps.Single(step =>
            string.Equals(step.StepId, selectedStepId, StringComparison.Ordinal));
        CharacterCreationWizardDesktopStep[] steps = snapshot.Steps
            .Select(step => new CharacterCreationWizardDesktopStep(
                StepId: step.StepId,
                Label: step.Label,
                Status: step.Status,
                IsRequired: step.IsRequired,
                CanEnter: CanEnter(snapshot, step),
                IsComplete: step.IsComplete,
                IsSelected: string.Equals(step.StepId, selectedStepId, StringComparison.Ordinal),
                BudgetIds: step.BudgetIds,
                Blockers: step.Blockers,
                Warnings: step.Warnings,
                LegalNextStepIds: step.LegalNextStepIds))
            .ToArray();
        CharacterCreationWizardDesktopBudget[] budgets = snapshot.Budgets
            .Select(static budget => new CharacterCreationWizardDesktopBudget(
                budget.BudgetId,
                budget.Label,
                budget.Total,
                budget.Used,
                budget.Remaining,
                budget.IsExact,
                budget.Blockers,
                budget.Unit))
            .ToArray();
        CharacterCreationWizardDesktopOption[] options = snapshot.LegalOptionsByStep
            .TryGetValue(selectedStepId, out IReadOnlyList<CharacterCreationLegalOption>? legalOptions)
                ? legalOptions.Select(static option => new CharacterCreationWizardDesktopOption(
                    option.OptionId,
                    option.VersionId,
                    option.Label,
                    option.IsEnabled,
                    option.DisableReasonKey,
                    option.DisableReasonArguments,
                    option.Costs,
                    option.Consequences,
                    option.SourceAnchorIds,
                    option.SourceId,
                    option.SourcePage)).ToArray()
                : [];
        if (selectedStepId == CharacterCreationWizardStepIds.LifeModules
            && _journey is { CurrentStageOrder: >= 2 } journeyOptions)
        {
            options = CharacterCreationWizardProjector.BuildLifeModuleOptions(journeyOptions.Options)
                .Select(option =>
                {
                    LifeModuleLegalOptionDto module = journeyOptions.Options.Single(m => m.ModuleId == option.OptionId);
                    LifeModuleVersionProjectionDto? version = module.Versions.SingleOrDefault(v => v.VersionId == option.VersionId);
                    return new CharacterCreationWizardDesktopOption(
                        option.OptionId, option.VersionId, option.Label, option.IsEnabled,
                        option.DisableReasonKey, option.DisableReasonArguments, option.Costs,
                        option.Consequences, option.SourceAnchorIds, option.SourceId, option.SourcePage)
                    {
                        FollowUps = module.FollowUps.Concat(version?.FollowUps ?? [])
                            .DistinctBy(prompt => prompt.PromptId).ToArray()
                    };
                }).ToArray();
        }
        CharacterCreationWizardDesktopContactsStep? contacts = string.Equals(
                selectedStepId,
                CharacterCreationWizardStepIds.ContactsLifestyles,
                StringComparison.Ordinal)
            ? ProjectContacts(_contacts)
            : null;
        CharacterCreationWizardDesktopLifestylesStep? lifestylesStep = string.Equals(
                selectedStepId,
                CharacterCreationWizardStepIds.ContactsLifestyles,
                StringComparison.Ordinal)
            ? ProjectLifestyles(_lifestyles)
            : null;
        bool canContinue = active.LegalNextStepIds.Any(nextId =>
            snapshot.Steps.Any(step =>
                string.Equals(step.StepId, nextId, StringComparison.Ordinal)
                && CanEnter(snapshot, step)));
        CharacterCreationWizardBuildGhostContext ghost = new(
            Schema: CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
            WorkspaceId: snapshot.WorkspaceId,
            WorkspaceRevision: snapshot.WorkspaceRevision,
            WizardSnapshotDigest: snapshot.SnapshotDigest,
            ActiveStepId: selectedStepId,
            RulesetId: snapshot.RulesetId,
            RuntimeFingerprint: snapshot.RuntimeFingerprint,
            BuildMethod: snapshot.BuildMethod,
            Budgets: budgets,
            LegalOptions: options,
            CompletionBlockers: snapshot.CompletionBlockers,
            Warnings: snapshot.Warnings,
            ContactsStep: contacts,
            // Rook's wizard advice must see the same lifestyle projection the
            // player sees (audit win008 finding 4), without extra private data.
            LifestylesStep: lifestylesStep);

        // The pinned projector emits create/delete capability warnings unconditionally.
        // When Core authority for add/remove is actually available (template present and
        // editable), that warning is stale on this display surface, so filter only the
        // displayed warnings; snapshot digests and Core-owned projections are untouched.
        bool contactsAddable = contacts is { } c
            && c.NewContactTemplate is not null
            && c.CanEdit;
        IReadOnlyList<string> displayedWarnings = contactsAddable
            ? snapshot.Warnings
                .Where(warning => !string.Equals(
                    warning,
                    CharacterCreationWizardProjector.ContactCreateDeleteAuthorityUnavailable,
                    StringComparison.Ordinal))
                .ToArray()
            : snapshot.Warnings;

        CharacterCreationWizardDesktopJourneyStep? journeyStep = _journey is { CurrentStageOrder: >= 2 } journeyState
            && string.Equals(
                selectedStepId,
                CharacterCreationWizardStepIds.LifeModules,
                StringComparison.Ordinal)
            ? new CharacterCreationWizardDesktopJourneyStep(
                journeyState.CurrentStageOrder,
                journeyState.CurrentStageId,
                JourneyStageLabel(journeyState.CurrentStageId),
                journeyState.SelectionFinished,
                journeyState.CanFinishSelection)
            : null;
        return new CharacterCreationWizardDesktopState(
            WorkspaceId: snapshot.WorkspaceId,
            WorkspaceRevision: snapshot.WorkspaceRevision,
            SnapshotDigest: snapshot.SnapshotDigest,
            ActiveStepId: selectedStepId,
            Steps: steps,
            Budgets: budgets,
            LegalOptions: options,
            CompletionBlockers: snapshot.CompletionBlockers,
            Warnings: displayedWarnings,
            CanContinue: canContinue,
            // This foundation is navigation/read-only. Canonical finalization is not yet wired;
            // the advanced editor returns only after CreationWizard becomes null upstream.
            CanFinalize: false,
            AdvancedEditorUnlocked: false,
            BuildGhostAvailable: CharacterCreationWizardBuildGhostPolicy.IsAuthorized(snapshot),
            Resume: resume,
            BuildGhostContext: ghost,
            ContactsStep: contacts,
            LifestylesStep: lifestylesStep,
            JourneyStep: journeyStep);
    }

    private static CharacterCreationWizardDesktopContactsStep? ProjectContacts(
        CharacterCreationContactsState? state)
        => state is null
            ? null
            : new CharacterCreationWizardDesktopContactsStep(
                state.Binding,
                state.Contacts.Select(static contact => new CharacterCreationWizardDesktopContact(
                    contact.ContactId,
                    contact.Identity.Name,
                    contact.Identity.Role,
                    contact.ContactPointCost,
                    contact.CountsAgainstContactBudget,
                    contact.CountsAgainstHighPlacesBudget,
                    contact.Fields.Select(static field => new CharacterCreationWizardDesktopContactField(
                        field.FieldId,
                        field.Label,
                        field.ValueKind,
                        field.IsEditable,
                        field.SerializedValue,
                        field.Minimum,
                        field.Maximum,
                        field.LegalOptions,
                        field.Blockers,
                        field.SourceAnchorIds)).ToArray(),
                    contact.SourceAnchorIds,
                    contact.ContactDigest,
                    contact.CanDelete)).ToArray(),
                state.ContactBudget,
                state.HighPlacesBudget,
                state.Blockers,
                state.CanEdit && state.Blockers.Count == 0,
                state.SnapshotDigest)
        {
            NewContactTemplate = state.NewContactTemplate
        };

    private static bool ContactsMatchSnapshot(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationContactsState? contacts)
        => contacts is not null
           && CharacterCreationWizardProjector.MatchesContactSnapshot(snapshot, contacts);

    private static bool JourneyMatchesSnapshot(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationLifeModuleJourneyInteractionState? journey)
        => journey is not null
           && !snapshot.CharacterCreated
           && snapshot.WorkspaceRevision == journey.Binding.ContentRevision
           && string.Equals(snapshot.SourceDigest, journey.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(
               snapshot.WorkspaceId,
               journey.Binding.WorkspaceId.Value,
               StringComparison.Ordinal);

    private static string JourneyStageLabel(string stageId)
        => stageId switch
        {
            CharacterCreationLifeModuleStageIds.Nationality => "Nationality",
            CharacterCreationLifeModuleStageIds.FormativeYears => "Formative Years",
            CharacterCreationLifeModuleStageIds.TeenYears => "Teen Years",
            CharacterCreationLifeModuleStageIds.FurtherEducation => "Further Education",
            CharacterCreationLifeModuleStageIds.RealLife => "Real Life",
            _ => stageId,
        };

    private static bool LifestylesMatchSnapshot(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationLifestylesState? lifestyles)
    {
        if (lifestyles is null
            || snapshot.CharacterCreated
            || lifestyles.CharacterCreated)
        {
            return false;
        }

        return string.Equals(
                   lifestyles.Schema,
                   CharacterCreationLifestylesSchemas.StateV1,
                   StringComparison.Ordinal)
               && string.Equals(
                   lifestyles.StepId,
                   CharacterCreationWizardStepIds.ContactsLifestyles,
                   StringComparison.Ordinal)
               && string.Equals(
                   lifestyles.Binding.WorkspaceId.Value,
                   snapshot.WorkspaceId,
                   StringComparison.Ordinal)
               && lifestyles.Binding.WorkspaceRevision == snapshot.WorkspaceRevision
               && lifestyles.Binding.ContentRevision == snapshot.WorkspaceRevision
               && string.Equals(
                   lifestyles.Binding.ContentDigest,
                   snapshot.ContentDigest,
                   StringComparison.Ordinal);
    }

    private static CharacterCreationWizardDesktopLifestylesStep? ProjectLifestyles(
        CharacterCreationLifestylesState? state)
        => state is null
            ? null
            : new CharacterCreationWizardDesktopLifestylesStep(
                state.Binding,
                state.Authority,
                state.Lifestyles.Select(static lifestyle => new CharacterCreationWizardDesktopLifestyle(
                    lifestyle.Configuration,
                    lifestyle.SourceId,
                    lifestyle.BaseLifestyleName,
                    lifestyle.SourceBook,
                    lifestyle.Page,
                    lifestyle.Economics,
                    lifestyle.SourceAnchorIds,
                    lifestyle.LifestyleDigest)).ToArray(),
                state.Budget,
                state.Blockers,
                state.CanEdit && state.Blockers.Count == 0,
                state.SnapshotDigest);

    private static CharacterCreationWizardDesktopResume ResolveResume(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationWizardDesktopCheckpoint? checkpoint,
        out string selectedStepId)
    {
        selectedStepId = ResolveAuthoritativeActiveStep(snapshot);
        if (checkpoint is null)
            return new CharacterCreationWizardDesktopResume(false, null);
        if (!IsValidCheckpointShape(checkpoint))
            return new CharacterCreationWizardDesktopResume(false, CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        if (!string.Equals(checkpoint.WorkspaceId, snapshot.WorkspaceId, StringComparison.Ordinal))
            return new CharacterCreationWizardDesktopResume(false, CharacterCreationWizardCheckpointInvalidationReasons.WorkspaceChanged);
        if (checkpoint.WorkspaceRevision != snapshot.WorkspaceRevision)
            return new CharacterCreationWizardDesktopResume(false, CharacterCreationWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged);
        if (!string.Equals(checkpoint.SnapshotDigest, snapshot.SnapshotDigest, StringComparison.Ordinal))
            return new CharacterCreationWizardDesktopResume(false, CharacterCreationWizardCheckpointInvalidationReasons.SnapshotChanged);

        CharacterCreationWizardStageState? target = snapshot.Steps.SingleOrDefault(step =>
            string.Equals(step.StepId, checkpoint.SelectedStepId, StringComparison.Ordinal));
        if (target is null || !CanEnter(snapshot, target))
            return new CharacterCreationWizardDesktopResume(false, CharacterCreationWizardCheckpointInvalidationReasons.StepUnavailable);

        selectedStepId = target.StepId;
        return new CharacterCreationWizardDesktopResume(true, null);
    }

    private static string ResolveAuthoritativeActiveStep(CharacterCreationWizardSnapshot snapshot)
    {
        CharacterCreationWizardStageState? active = snapshot.Steps.SingleOrDefault(step =>
            string.Equals(step.StepId, snapshot.ActiveStepId, StringComparison.Ordinal));
        if (active is not null && CanEnter(snapshot, active))
            return active.StepId;

        return snapshot.Steps.First(step => CanEnter(snapshot, step)).StepId;
    }

    private static bool CanEnter(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationWizardStageState step)
        => step.IsAvailable
           || step.IsComplete
           || string.Equals(step.StepId, snapshot.ActiveStepId, StringComparison.Ordinal)
              && !string.Equals(step.Status, CharacterCreationWizardStepStatuses.Blocked, StringComparison.Ordinal);

    private static void ValidateSnapshot(CharacterCreationWizardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.Schema, CharacterCreationWizardSchemas.SnapshotV1, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(snapshot.WorkspaceId)
            || snapshot.WorkspaceRevision <= 0
            || !IsSha256(snapshot.SnapshotDigest)
            || snapshot.CharacterCreated
            || snapshot.Steps.Count == 0
            || snapshot.Steps.Select(static step => step.StepId).Distinct(StringComparer.Ordinal).Count() != snapshot.Steps.Count
            || !snapshot.Steps.Any(static step => step.IsAvailable || step.IsComplete))
        {
            throw new InvalidOperationException("The creation wizard snapshot is not a valid unfinished-character authority.");
        }
    }

    private static void ValidateCheckpointShape(CharacterCreationWizardDesktopCheckpoint checkpoint)
    {
        if (!IsValidCheckpointShape(checkpoint))
            throw new InvalidOperationException("The creation wizard checkpoint is invalid.");
    }

    private static bool IsValidCheckpointShape(CharacterCreationWizardDesktopCheckpoint checkpoint)
        => string.Equals(checkpoint.Schema, CharacterCreationWizardDesktopSchemas.CheckpointV1, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(checkpoint.WorkspaceId)
           && checkpoint.WorkspaceRevision > 0
           && IsSha256(checkpoint.SnapshotDigest)
           && !string.IsNullOrWhiteSpace(checkpoint.SelectedStepId);

    private static bool IsSha256(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
