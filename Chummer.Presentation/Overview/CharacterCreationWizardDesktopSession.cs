using System.Text.Json;
using Chummer.Contracts.Characters;

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
    int? SourcePage);

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
    string ContactDigest);

public sealed record CharacterCreationWizardDesktopContactsStep(
    CharacterCreationContactBinding Binding,
    IReadOnlyList<CharacterCreationWizardDesktopContact> Contacts,
    CharacterCreationContactBudget ContactBudget,
    CharacterCreationContactBudget HighPlacesBudget,
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
    CharacterCreationWizardDesktopContactsStep? ContactsStep = null);

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
    CharacterCreationWizardDesktopContactsStep? ContactsStep = null);

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
/// Platform-neutral desktop session for the source-driven creation wizard. Navigation consumes
/// the typed route graph projected by Presentation; it never invents legal choices or budgets.
/// </summary>
public sealed class CharacterCreationWizardDesktopSession
{
    private CharacterCreationWizardSnapshot? _snapshot;
    private CharacterCreationContactsState? _contacts;
    private CharacterCreationWizardDesktopState? _state;

    public CharacterCreationWizardDesktopState State
        => _state ?? throw new InvalidOperationException("Bind a wizard snapshot before reading state.");

    public CharacterCreationWizardDesktopState Bind(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationWizardDesktopCheckpoint? checkpoint = null,
        CharacterCreationContactsState? contacts = null)
    {
        ValidateSnapshot(snapshot);

        CharacterCreationWizardDesktopResume resume = ResolveResume(snapshot, checkpoint, out string selectedStepId);
        _snapshot = snapshot;
        _contacts = ContactsMatchSnapshot(snapshot, contacts) ? contacts : null;
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
        CharacterCreationWizardDesktopContactsStep? contacts = string.Equals(
                selectedStepId,
                CharacterCreationWizardStepIds.ContactsLifestyles,
                StringComparison.Ordinal)
            ? ProjectContacts(_contacts)
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
            ContactsStep: contacts);

        return new CharacterCreationWizardDesktopState(
            WorkspaceId: snapshot.WorkspaceId,
            WorkspaceRevision: snapshot.WorkspaceRevision,
            SnapshotDigest: snapshot.SnapshotDigest,
            ActiveStepId: selectedStepId,
            Steps: steps,
            Budgets: budgets,
            LegalOptions: options,
            CompletionBlockers: snapshot.CompletionBlockers,
            Warnings: snapshot.Warnings,
            CanContinue: canContinue,
            // This foundation is navigation/read-only. Canonical finalization is not yet wired;
            // the advanced editor returns only after CreationWizard becomes null upstream.
            CanFinalize: false,
            AdvancedEditorUnlocked: false,
            BuildGhostAvailable: CharacterCreationWizardBuildGhostPolicy.IsAuthorized(snapshot),
            Resume: resume,
            BuildGhostContext: ghost,
            ContactsStep: contacts);
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
                    contact.ContactDigest)).ToArray(),
                state.ContactBudget,
                state.HighPlacesBudget,
                state.Blockers,
                state.CanEdit && state.Blockers.Count == 0,
                state.SnapshotDigest);

    private static bool ContactsMatchSnapshot(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationContactsState? contacts)
        => contacts is not null
           && CharacterCreationWizardProjector.MatchesContactSnapshot(snapshot, contacts);

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
