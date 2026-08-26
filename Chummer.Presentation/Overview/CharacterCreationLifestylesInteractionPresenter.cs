using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationLifestylesInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-lifestyles-overview-authority-required";
    public const string BindingMismatch = "creation-lifestyles-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-lifestyles-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-lifestyles-preview-not-confirmable";
    public const string IdempotencyKeyMismatch = "creation-lifestyles-idempotency-key-mismatch";
    public const string ReceiptMismatch = "creation-lifestyles-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-lifestyles-refresh-authority-required";
}

/// <summary>
/// Renderer-neutral boundary for the Lifestyle half of the SR5 Contacts/Lifestyles
/// creation stage. Presentation passes stable catalog identities and typed intent to
/// Core; it never prices a Lifestyle or constructs an XML mutation.
/// </summary>
public interface ICharacterCreationLifestylesInteractionPresenter
{
    CharacterCreationLifestylesInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationLifestylesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationLifestyleMutationInput input);

    CharacterCreationLifestylesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationLifestyleConfirmation confirmation);

    CharacterCreationLifestylesInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey);
}

public sealed record CharacterCreationLifestyleMutationInput(
    string MutationKind,
    Guid LifestyleId,
    CharacterCreationLifestyleConfiguration? Configuration)
{
    internal CharacterCreationLifestyleMutation ToCoreMutation()
        => new(MutationKind, LifestyleId, Configuration);
}

public sealed record CharacterCreationLifestylesInteractionState(
    CharacterCreationLifestyleBinding Binding,
    CharacterCreationLifestylesAuthority Authority,
    IReadOnlyList<CharacterCreationLifestyleProjection> Lifestyles,
    CharacterCreationLifestyleBudget Budget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

public sealed record CharacterCreationLifestylePreparedPreview(
    string LifestylesSnapshotDigest,
    CharacterCreationLifestyleBinding Binding,
    CharacterCreationLifestyleMutation Mutation,
    IReadOnlyList<CharacterCreationLifestyleProjection> LifestylesBefore,
    CharacterCreationLifestyleProjection? Before,
    CharacterCreationLifestyleProjection? After,
    CharacterCreationLifestyleBudget BudgetBefore,
    CharacterCreationLifestyleBudget BudgetAfter,
    CharacterCreationLifestyleAtomicWritePlan WritePlan,
    IReadOnlyList<string> Blockers,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    string IdempotencyKey,
    string PreviewDigest);

public sealed record CharacterCreationLifestyleConfirmation(
    CharacterCreationLifestylePreparedPreview PreparedPreview,
    string PreviewDigest,
    string IdempotencyKey,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationLifestylesInteractionLoadResult(
    string Outcome,
    CharacterCreationLifestylesInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifestylesInteractionPrepareResult(
    string Outcome,
    CharacterCreationLifestylesInteractionState? State,
    CharacterCreationLifestylePreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifestylesInteractionConfirmResult(
    string Outcome,
    CharacterCreationLifestylePreparedPreview? PreparedPreview,
    CharacterCreationLifestyleReceipt? Receipt,
    CharacterCreationLifestylesInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifestylesInteractionReceiptLookupResult(
    string Outcome,
    CharacterCreationLifestyleReceipt? Receipt,
    CharacterCreationLifestylesInteractionState? CurrentState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationLifestylesInteractionPresenter
    : ICharacterCreationLifestylesInteractionPresenter
{
    private readonly ICharacterCreationLifestylesService _service;

    public CharacterCreationLifestylesInteractionPresenter(
        ICharacterCreationLifestylesService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public CharacterCreationLifestylesInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationLifestylesInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public CharacterCreationLifestylesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationLifestyleMutationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationLifestylesState lifestyles)
        {
            return new CharacterCreationLifestylesInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }

        CharacterCreationLifestylesInteractionState state = Project(lifestyles);
        if (!lifestyles.CanEdit || lifestyles.Blockers.Count != 0)
        {
            return new CharacterCreationLifestylesInteractionPrepareResult(
                CharacterCreationLifestyleOutcomes.Blocked,
                state,
                null,
                Normalize(load.Blockers.Concat(lifestyles.Blockers)));
        }

        CharacterCreationLifestyleMutation mutation = input.ToCoreMutation();
        if (!MutationEnvelopeIsValid(mutation))
        {
            return new CharacterCreationLifestylesInteractionPrepareResult(
                CharacterCreationLifestyleOutcomes.Invalid,
                state,
                null,
                [CharacterCreationLifestylesBlockers.InvalidMutation]);
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestylePreview> result =
            _service.Preview(new CharacterCreationLifestylePreviewRequest(
                lifestyles.Binding,
                mutation));
        if (result.Value is not CharacterCreationLifestylePreview preview)
        {
            return new CharacterCreationLifestylesInteractionPrepareResult(
                result.Outcome,
                state,
                null,
                Normalize(result.Blockers));
        }
        if (!PreviewMatches(lifestyles, mutation, preview))
        {
            return new CharacterCreationLifestylesInteractionPrepareResult(
                CharacterCreationLifestyleOutcomes.Conflict,
                state,
                null,
                [CharacterCreationLifestylesInteractionBlockers.PreparedPreviewMismatch]);
        }

        CharacterCreationLifestylePreparedPreview prepared = Project(lifestyles, mutation, preview);
        return new CharacterCreationLifestylesInteractionPrepareResult(
            result.Outcome,
            state,
            prepared,
            Normalize(result.Blockers.Concat(preview.Blockers)));
    }

    public CharacterCreationLifestylesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationLifestyleConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationLifestylePreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationLifestyleOutcomes.Invalid,
                prepared,
                CharacterCreationLifestylesBlockers.ExplicitConfirmationRequired);
        }
        if (!string.Equals(confirmation.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal)
            || !IsDigest(prepared.PreviewDigest))
        {
            return Failure(
                CharacterCreationLifestyleOutcomes.Conflict,
                prepared,
                CharacterCreationLifestylesBlockers.PreviewDigestMismatch);
        }
        if (!ValidIdempotencyKey(prepared.IdempotencyKey)
            || !string.Equals(
                confirmation.IdempotencyKey,
                prepared.IdempotencyKey,
                StringComparison.Ordinal))
        {
            return Failure(
                CharacterCreationLifestyleOutcomes.Conflict,
                prepared,
                CharacterCreationLifestylesInteractionBlockers.IdempotencyKeyMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationLifestylesState lifestyles)
        {
            return new CharacterCreationLifestylesInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }
        if (!PreparedStillMatches(prepared, lifestyles))
        {
            return Failure(
                CharacterCreationLifestyleOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Binding, lifestyles.Binding));
        }
        if (!prepared.RequiresExplicitConfirmation
            || !prepared.CanConfirm
            || prepared.Blockers.Count != 0)
        {
            return new CharacterCreationLifestylesInteractionConfirmResult(
                CharacterCreationLifestyleOutcomes.Blocked,
                prepared,
                null,
                null,
                prepared.Blockers.Count == 0
                    ? [CharacterCreationLifestylesInteractionBlockers.PreviewNotConfirmable]
                    : Normalize(prepared.Blockers));
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> result =
            _service.Confirm(new CharacterCreationLifestyleConfirmRequest(
                prepared.Binding,
                prepared.Mutation,
                prepared.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));
        if (result.Value is not CharacterCreationLifestyleReceipt receipt)
        {
            return new CharacterCreationLifestylesInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                Normalize(result.Blockers));
        }
        if (result.Outcome is not (CharacterCreationLifestyleOutcomes.Applied
                or CharacterCreationLifestyleOutcomes.Replayed)
            || !ReceiptMatches(prepared, receipt))
        {
            return new CharacterCreationLifestylesInteractionConfirmResult(
                CharacterCreationLifestyleOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationLifestylesInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestylesState> refresh =
            _service.Load(new CharacterCreationLifestylesLoadRequest(receipt.WorkspaceId));
        if (refresh.Outcome != CharacterCreationLifestyleOutcomes.Available
            || refresh.Value is not CharacterCreationLifestylesState refreshed
            || !RefreshedStateMatches(prepared, receipt, refreshed))
        {
            return new CharacterCreationLifestylesInteractionConfirmResult(
                CharacterCreationLifestyleOutcomes.Conflict,
                prepared,
                receipt,
                null,
                Normalize(refresh.Blockers.Append(
                    CharacterCreationLifestylesInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationLifestylesInteractionConfirmResult(
            result.Outcome,
            prepared,
            receipt,
            Project(refreshed),
            Normalize(result.Blockers.Concat(refresh.Blockers)));
    }

    public CharacterCreationLifestylesInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created != false
            || overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return new CharacterCreationLifestylesInteractionReceiptLookupResult(
                CharacterCreationLifestyleOutcomes.Invalid,
                null,
                null,
                [CharacterCreationLifestylesInteractionBlockers.OverviewAuthorityRequired]);
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> lookup =
            _service.LookupReceipt(new CharacterCreationLifestyleReceiptLookupRequest(
                workspaceId,
                idempotencyKey));
        if (lookup.Value is not CharacterCreationLifestyleReceipt receipt)
        {
            return new CharacterCreationLifestylesInteractionReceiptLookupResult(
                lookup.Outcome,
                null,
                null,
                Normalize(lookup.Blockers));
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestylesState> current =
            _service.Load(new CharacterCreationLifestylesLoadRequest(workspaceId));
        if (current.Outcome != CharacterCreationLifestyleOutcomes.Available
            || current.Value is not CharacterCreationLifestylesState state
            || !ReceiptCanBelongToCurrentState(receipt, state))
        {
            return new CharacterCreationLifestylesInteractionReceiptLookupResult(
                CharacterCreationLifestyleOutcomes.Conflict,
                receipt,
                null,
                Normalize(current.Blockers.Append(
                    CharacterCreationLifestylesInteractionBlockers.ReceiptMismatch)));
        }

        return new CharacterCreationLifestylesInteractionReceiptLookupResult(
            lookup.Outcome,
            receipt,
            Project(state),
            Normalize(lookup.Blockers.Concat(current.Blockers)));
    }

    private ExactLoad LoadExact(CharacterOverviewState overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created == true || overview.CreationWizard?.CharacterCreated == true)
        {
            return ExactLoad.Failure(
                CharacterCreationLifestyleOutcomes.Blocked,
                CharacterCreationLifestylesBlockers.CareerModeRejected);
        }
        if (overview.Profile?.Created != false
            || overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || overview.CreationWizard is not { } wizard
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return ExactLoad.Failure(
                CharacterCreationLifestyleOutcomes.Invalid,
                CharacterCreationLifestylesInteractionBlockers.OverviewAuthorityRequired);
        }

        CharacterCreationLifestyleResult<CharacterCreationLifestylesState> result =
            _service.Load(new CharacterCreationLifestylesLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationLifestyleOutcomes.Available
            || result.Value is not CharacterCreationLifestylesState lifestyles)
        {
            return new ExactLoad(result.Outcome, null, Normalize(result.Blockers));
        }
        if (!MatchesOverview(activeWorkspace.ContentRevision, activeWorkspace.SavedRevision, wizard, lifestyles))
        {
            return ExactLoad.Failure(
                CharacterCreationLifestyleOutcomes.Conflict,
                CharacterCreationLifestylesInteractionBlockers.BindingMismatch);
        }
        return new ExactLoad(
            CharacterCreationLifestyleOutcomes.Available,
            lifestyles,
            Normalize(result.Blockers.Concat(lifestyles.Blockers)));
    }

    private static bool MatchesOverview(
        long contentRevision,
        long savedRevision,
        CharacterCreationWizardSnapshot wizard,
        CharacterCreationLifestylesState state)
        => StateShapeIsValid(state)
           && !state.CharacterCreated
           && state.Binding.ContentRevision == contentRevision
           && state.Binding.WorkspaceRevision == contentRevision
           && state.Binding.SavedRevision == savedRevision
           && string.Equals(wizard.WorkspaceId, state.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && wizard.WorkspaceRevision == contentRevision
           && string.Equals(wizard.ContentDigest, state.Binding.ContentDigest, StringComparison.Ordinal)
           && !wizard.CharacterCreated;

    private static CharacterCreationLifestylesInteractionState Project(
        CharacterCreationLifestylesState state)
        => new(
            state.Binding,
            state.Authority,
            state.Lifestyles,
            state.Budget,
            state.Blockers,
            state.CanEdit,
            state.SnapshotDigest);

    private static CharacterCreationLifestylePreparedPreview Project(
        CharacterCreationLifestylesState state,
        CharacterCreationLifestyleMutation mutation,
        CharacterCreationLifestylePreview preview)
        => new(
            state.SnapshotDigest,
            preview.Binding,
            mutation,
            state.Lifestyles.ToArray(),
            preview.Before,
            preview.After,
            preview.BudgetBefore,
            preview.BudgetAfter,
            preview.WritePlan,
            preview.Blockers,
            preview.RequiresExplicitConfirmation,
            preview.CanConfirm,
            "creation-lifestyle-" + Guid.NewGuid().ToString("N"),
            preview.PreviewDigest);

    private static bool PreviewMatches(
        CharacterCreationLifestylesState state,
        CharacterCreationLifestyleMutation mutation,
        CharacterCreationLifestylePreview preview)
        => string.Equals(preview.Schema, CharacterCreationLifestylesSchemas.PreviewV1, StringComparison.Ordinal)
           && string.Equals(preview.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && BindingEquals(preview.Binding, state.Binding)
           && string.Equals(preview.MutationKind, mutation.MutationKind, StringComparison.Ordinal)
           && PreviewTargetMatches(mutation, preview)
           && BudgetEquals(preview.BudgetBefore, state.Budget)
           && BudgetShapeIsValid(preview.BudgetAfter)
           && PlanMatchesPreview(preview)
           && preview.RequiresExplicitConfirmation
           && preview.CanConfirm == (preview.Blockers.Count == 0)
           && IsSortedDistinct(preview.Blockers)
           && IsDigest(preview.PreviewDigest)
           && string.Equals(
               preview.PreviewDigest,
               CharacterCreationLifestylesRules.ComputePreviewDigest(preview),
               StringComparison.Ordinal);

    private static bool PreviewTargetMatches(
        CharacterCreationLifestyleMutation mutation,
        CharacterCreationLifestylePreview preview)
        => mutation.MutationKind switch
        {
            CharacterCreationLifestyleMutationKinds.Create => preview.Before is null
                && preview.After is { } created
                && ProjectionShapeIsValid(created)
                && ConfigurationMatchesMutation(created.Configuration, mutation.Configuration!),
            CharacterCreationLifestyleMutationKinds.Edit => preview.Before is { } before
                && preview.After is { } after
                && ProjectionShapeIsValid(before)
                && ProjectionShapeIsValid(after)
                && before.Configuration.LifestyleId == mutation.LifestyleId
                && ConfigurationMatchesMutation(after.Configuration, mutation.Configuration!),
            CharacterCreationLifestyleMutationKinds.Delete => preview.Before is { } deleted
                && ProjectionShapeIsValid(deleted)
                && deleted.Configuration.LifestyleId == mutation.LifestyleId
                && preview.After is null,
            _ => false
        };

    private static bool PlanMatchesPreview(CharacterCreationLifestylePreview preview)
        => string.Equals(preview.WritePlan.Schema, CharacterCreationLifestylesSchemas.WritePlanV1, StringComparison.Ordinal)
           && string.Equals(preview.WritePlan.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && string.Equals(preview.WritePlan.MutationKind, preview.MutationKind, StringComparison.Ordinal)
           && preview.WritePlan.LifestyleId == (preview.Before ?? preview.After)!.Configuration.LifestyleId
           && string.Equals(preview.WritePlan.ContentDigestBefore, preview.Binding.ContentDigest, StringComparison.Ordinal)
           && IsDigest(preview.WritePlan.ContentDigestAfter)
           && IsDigest(preview.WritePlan.UntouchedSiblingDigestBefore)
           && IsDigest(preview.WritePlan.UntouchedSiblingDigestAfter)
           && IsDigest(preview.WritePlan.NestedStateDigestBefore)
           && IsDigest(preview.WritePlan.NestedStateDigestAfter)
           && (!preview.CanConfirm || preview.WritePlan.PreservesUntouchedSiblingState)
           && (!preview.CanConfirm || preview.WritePlan.PreservesNestedState)
           && preview.WritePlan.Operations.Select(static operation => operation.Order)
               .SequenceEqual(Enumerable.Range(1, preview.WritePlan.Operations.Count))
           && preview.WritePlan.Operations.All(operation =>
               operation.LifestyleId == preview.WritePlan.LifestyleId
               && string.Equals(operation.MutationKind, preview.MutationKind, StringComparison.Ordinal)
               && IsDigest(operation.BeforeDigest)
               && IsDigest(operation.AfterDigest)
               && operation.SourceAnchorIds.Count > 0)
           && string.Equals(
               preview.WritePlan.PlanDigest,
               CharacterCreationLifestylesRules.ComputePlanDigest(preview.WritePlan),
               StringComparison.Ordinal);

    private static bool PreparedStillMatches(
        CharacterCreationLifestylePreparedPreview prepared,
        CharacterCreationLifestylesState current)
        => string.Equals(prepared.LifestylesSnapshotDigest, current.SnapshotDigest, StringComparison.Ordinal)
           && BindingEquals(prepared.Binding, current.Binding)
           && BudgetEquals(prepared.BudgetBefore, current.Budget)
           && ProjectionSetsEqual(prepared.LifestylesBefore, current.Lifestyles)
           && (prepared.Before is null
               ? current.Lifestyles.All(item => item.Configuration.LifestyleId != prepared.Mutation.LifestyleId)
               : current.Lifestyles.Any(item => ProjectionEquals(item, prepared.Before)));

    private static bool ReceiptMatches(
        CharacterCreationLifestylePreparedPreview prepared,
        CharacterCreationLifestyleReceipt receipt)
        => string.Equals(receipt.Schema, CharacterCreationLifestylesSchemas.ReceiptV1, StringComparison.Ordinal)
           && string.Equals(receipt.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(receipt.ReceiptId)
           && receipt.WorkspaceId == prepared.Binding.WorkspaceId
           && string.Equals(receipt.MutationKind, prepared.Mutation.MutationKind, StringComparison.Ordinal)
           && receipt.LifestyleId == prepared.Mutation.LifestyleId
           && receipt.PreviousWorkspaceRevision == prepared.Binding.WorkspaceRevision
           && receipt.WorkspaceRevision == receipt.PreviousWorkspaceRevision + 1
           && receipt.PreviousContentRevision == prepared.Binding.ContentRevision
           && receipt.ContentRevision == receipt.PreviousContentRevision + 1
           && receipt.PreviousSavedRevision == prepared.Binding.SavedRevision
           && receipt.SavedRevision == receipt.ContentRevision
           && string.Equals(receipt.ContentDigestBefore, prepared.Binding.ContentDigest, StringComparison.Ordinal)
           && string.Equals(receipt.ContentDigestAfter, prepared.WritePlan.ContentDigestAfter, StringComparison.Ordinal)
           && string.Equals(receipt.SourceDigest, prepared.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RulesDigest, prepared.Binding.RulesDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RuntimeDigest, prepared.Binding.RuntimeDigest, StringComparison.Ordinal)
           && receipt.LifestyleCostBefore == prepared.BudgetBefore.Used
           && receipt.LifestyleCostAfter == prepared.BudgetAfter.Used
           && receipt.LifestyleBudgetRemaining == prepared.BudgetAfter.Remaining
           && WritePlanEquals(receipt.WritePlan, prepared.WritePlan)
           && IsDigest(receipt.IdempotencyKeyDigest)
           && IsDigest(receipt.CommandDigest)
           && IsDigest(receipt.ReceiptDigest)
           && string.Equals(
               receipt.ReceiptDigest,
               CharacterCreationLifestylesRules.ComputeReceiptDigest(receipt),
               StringComparison.Ordinal);

    private static bool RefreshedStateMatches(
        CharacterCreationLifestylePreparedPreview prepared,
        CharacterCreationLifestyleReceipt receipt,
        CharacterCreationLifestylesState refreshed)
    {
        if (!StateShapeIsValid(refreshed)
            || refreshed.CharacterCreated
            || refreshed.Binding.WorkspaceId != receipt.WorkspaceId
            || refreshed.Binding.WorkspaceRevision != receipt.WorkspaceRevision
            || refreshed.Binding.ContentRevision != receipt.ContentRevision
            || refreshed.Binding.SavedRevision != receipt.SavedRevision
            || !string.Equals(refreshed.Binding.ContentDigest, receipt.ContentDigestAfter, StringComparison.Ordinal)
            || !string.Equals(refreshed.Binding.SourceDigest, receipt.SourceDigest, StringComparison.Ordinal)
            || !string.Equals(refreshed.Binding.RulesDigest, receipt.RulesDigest, StringComparison.Ordinal)
            || !string.Equals(refreshed.Binding.RuntimeDigest, receipt.RuntimeDigest, StringComparison.Ordinal)
            || !BudgetEquals(refreshed.Budget, prepared.BudgetAfter))
        {
            return false;
        }

        IReadOnlyList<CharacterCreationLifestyleProjection> expected = prepared.LifestylesBefore
            .Where(item => item.Configuration.LifestyleId != prepared.Mutation.LifestyleId)
            .Concat(prepared.After is { } after
                ? [after]
                : Array.Empty<CharacterCreationLifestyleProjection>())
            .ToArray();
        return ProjectionSetsEqual(expected, refreshed.Lifestyles);
    }

    private static bool ReceiptCanBelongToCurrentState(
        CharacterCreationLifestyleReceipt receipt,
        CharacterCreationLifestylesState current)
        => StateShapeIsValid(current)
           && receipt.WorkspaceId == current.Binding.WorkspaceId
           && receipt.WorkspaceRevision == current.Binding.WorkspaceRevision
           && receipt.ContentRevision == current.Binding.ContentRevision
           && receipt.SavedRevision == current.Binding.SavedRevision
           && string.Equals(receipt.ContentDigestAfter, current.Binding.ContentDigest, StringComparison.Ordinal)
           && string.Equals(receipt.SourceDigest, current.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RulesDigest, current.Binding.RulesDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RuntimeDigest, current.Binding.RuntimeDigest, StringComparison.Ordinal)
           && IsDigest(receipt.ReceiptDigest)
           && string.Equals(
               receipt.ReceiptDigest,
               CharacterCreationLifestylesRules.ComputeReceiptDigest(receipt),
               StringComparison.Ordinal);

    private static bool StateShapeIsValid(CharacterCreationLifestylesState state)
        => string.Equals(state.Schema, CharacterCreationLifestylesSchemas.StateV1, StringComparison.Ordinal)
           && string.Equals(state.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && state.Binding.WorkspaceId.Value.Length > 0
           && state.Binding.WorkspaceRevision == state.Binding.ContentRevision
           && IsDigest(state.Binding.ContentDigest)
           && !string.IsNullOrWhiteSpace(state.Binding.AuxiliaryStateDigest)
           && IsDigest(state.Binding.SourceDigest)
           && IsDigest(state.Binding.RulesDigest)
           && IsDigest(state.Binding.RuntimeDigest)
           && AuthorityShapeIsValid(state.Authority, state.Binding)
           && state.Lifestyles.All(ProjectionShapeIsValid)
           && state.Lifestyles.Select(item => item.Configuration.LifestyleId).Distinct().Count()
                == state.Lifestyles.Count
           && BudgetShapeIsValid(state.Budget)
           && IsSortedDistinct(state.Blockers)
           && state.CanEdit == (state.Blockers.Count == 0)
           && IsDigest(state.SnapshotDigest)
           && string.Equals(
               state.SnapshotDigest,
               CharacterCreationLifestylesRules.ComputeStateDigest(state),
               StringComparison.Ordinal);

    private static bool AuthorityShapeIsValid(
        CharacterCreationLifestylesAuthority authority,
        CharacterCreationLifestyleBinding binding)
        => string.Equals(authority.Schema, CharacterCreationLifestylesSchemas.AuthorityV1, StringComparison.Ordinal)
           && string.Equals(authority.RulesetId, "sr5", StringComparison.Ordinal)
           && authority.IsAuthoritative
           && authority.Blockers.Count == 0
           && authority.LifestyleOptions.Select(option => option.OptionId).Distinct(StringComparer.Ordinal).Count()
                == authority.LifestyleOptions.Count
           && authority.QualityOptions.Select(option => option.OptionId).Distinct(StringComparer.Ordinal).Count()
                == authority.QualityOptions.Count
           && authority.LifestyleOptions.All(option => IsDigest(option.OptionDigest)
               && string.Equals(option.OptionDigest,
                   CharacterCreationLifestylesRules.ComputeOptionDigest(option), StringComparison.Ordinal))
           && authority.QualityOptions.All(option => IsDigest(option.OptionDigest)
               && string.Equals(option.OptionDigest,
                   CharacterCreationLifestylesRules.ComputeQualityOptionDigest(option), StringComparison.Ordinal))
           && string.Equals(authority.SourceDigest, binding.SourceDigest, StringComparison.Ordinal)
           && IsDigest(authority.ProfileDigest)
           && IsDigest(authority.GmPolicyDigest)
           && string.Equals(authority.RuntimeDigest, binding.RuntimeDigest, StringComparison.Ordinal)
           && IsDigest(authority.AuthorityDigest)
           && string.Equals(authority.AuthorityDigest,
               CharacterCreationLifestylesRules.ComputeAuthorityDigest(authority), StringComparison.Ordinal);

    private static bool ProjectionShapeIsValid(CharacterCreationLifestyleProjection projection)
        => projection.Configuration.LifestyleId != Guid.Empty
           && projection.SourceId != Guid.Empty
           && !string.IsNullOrWhiteSpace(projection.BaseLifestyleName)
           && IsSortedDistinct(projection.Economics.Blockers)
           && projection.SourceAnchorIds.Count > 0
           && IsDigest(projection.LifestyleDigest)
           && string.Equals(projection.LifestyleDigest,
               CharacterCreationLifestylesRules.ComputeProjectionDigest(projection), StringComparison.Ordinal);

    private static bool BudgetShapeIsValid(CharacterCreationLifestyleBudget budget)
        => budget.Total >= 0m
           && budget.Used >= 0m
           && budget.Remaining >= 0m
           && budget.Overspend >= 0m
           && budget.SourceAnchorIds.Count > 0
           && IsSortedDistinct(budget.Blockers)
           && (!budget.IsExact || budget.Blockers.Count == 0);

    private static bool MutationEnvelopeIsValid(CharacterCreationLifestyleMutation mutation)
        => mutation.LifestyleId != Guid.Empty
           && (mutation.MutationKind switch
           {
               CharacterCreationLifestyleMutationKinds.Create
                   or CharacterCreationLifestyleMutationKinds.Edit =>
                       mutation.Configuration is { } configuration
                       && configuration.LifestyleId == mutation.LifestyleId,
               CharacterCreationLifestyleMutationKinds.Delete => mutation.Configuration is null,
               _ => false
           });

    private static bool BindingEquals(
        CharacterCreationLifestyleBinding left,
        CharacterCreationLifestyleBinding right)
        => left == right;

    private static string BindingConflict(
        CharacterCreationLifestyleBinding expected,
        CharacterCreationLifestyleBinding actual)
    {
        if (expected.WorkspaceId != actual.WorkspaceId
            || expected.WorkspaceRevision != actual.WorkspaceRevision
            || expected.ContentRevision != actual.ContentRevision
            || expected.SavedRevision != actual.SavedRevision)
        {
            return CharacterCreationLifestylesBlockers.StaleWorkspaceRevision;
        }
        if (!string.Equals(expected.ContentDigest, actual.ContentDigest, StringComparison.Ordinal))
            return CharacterCreationLifestylesBlockers.StaleContentDigest;
        if (!string.Equals(expected.AuxiliaryStateDigest, actual.AuxiliaryStateDigest, StringComparison.Ordinal))
            return CharacterCreationLifestylesBlockers.StaleAuxiliaryStateDigest;
        if (!string.Equals(expected.SourceDigest, actual.SourceDigest, StringComparison.Ordinal))
            return CharacterCreationLifestylesBlockers.StaleSourceDigest;
        if (!string.Equals(expected.RulesDigest, actual.RulesDigest, StringComparison.Ordinal))
            return CharacterCreationLifestylesBlockers.StaleRulesDigest;
        if (!string.Equals(expected.RuntimeDigest, actual.RuntimeDigest, StringComparison.Ordinal))
            return CharacterCreationLifestylesBlockers.StaleRuntimeDigest;
        return CharacterCreationLifestylesInteractionBlockers.BindingMismatch;
    }

    private static bool ProjectionSetsEqual(
        IReadOnlyList<CharacterCreationLifestyleProjection> left,
        IReadOnlyList<CharacterCreationLifestyleProjection> right)
        => left.Count == right.Count
           && left.OrderBy(item => item.Configuration.LifestyleId)
               .Select(item => (item.Configuration.LifestyleId, item.LifestyleDigest))
               .SequenceEqual(right.OrderBy(item => item.Configuration.LifestyleId)
                   .Select(item => (item.Configuration.LifestyleId, item.LifestyleDigest)));

    private static bool ProjectionEquals(
        CharacterCreationLifestyleProjection left,
        CharacterCreationLifestyleProjection right)
        => left.Configuration.LifestyleId == right.Configuration.LifestyleId
           && string.Equals(left.LifestyleDigest, right.LifestyleDigest, StringComparison.Ordinal);

    private static bool BudgetEquals(
        CharacterCreationLifestyleBudget left,
        CharacterCreationLifestyleBudget right)
        => left.Total == right.Total
           && left.Used == right.Used
           && left.Remaining == right.Remaining
           && left.Overspend == right.Overspend
           && left.IsExact == right.IsExact
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal);

    private static bool ConfigurationMatchesMutation(
        CharacterCreationLifestyleConfiguration projected,
        CharacterCreationLifestyleConfiguration requested)
        => projected.LifestyleId == requested.LifestyleId
           && string.Equals(projected.BaseLifestyleOptionId, requested.BaseLifestyleOptionId, StringComparison.Ordinal)
           && string.Equals(projected.Name, requested.Name, StringComparison.Ordinal)
           && string.Equals(projected.StyleId, requested.StyleId, StringComparison.Ordinal)
           && string.Equals(projected.IncrementId, requested.IncrementId, StringComparison.Ordinal)
           && projected.Increments == requested.Increments
           && projected.Percentage == requested.Percentage
           && projected.Roommates == requested.Roommates
           && projected.SplitCostWithRoommates == requested.SplitCostWithRoommates
           && projected.TrustFund == requested.TrustFund
           && projected.Area == requested.Area
           && projected.Comforts == requested.Comforts
           && projected.Security == requested.Security
           && projected.BonusLifestylePoints == requested.BonusLifestylePoints
           && string.Equals(projected.City, requested.City, StringComparison.Ordinal)
           && string.Equals(projected.District, requested.District, StringComparison.Ordinal)
           && string.Equals(projected.Borough, requested.Borough, StringComparison.Ordinal)
           && projected.Qualities.Where(item => !item.IsBuiltIn)
               .OrderBy(item => item.InstanceId)
               .SequenceEqual(requested.Qualities.Where(item => !item.IsBuiltIn)
                   .OrderBy(item => item.InstanceId));

    private static bool WritePlanEquals(
        CharacterCreationLifestyleAtomicWritePlan left,
        CharacterCreationLifestyleAtomicWritePlan right)
        => string.Equals(left.Schema, right.Schema, StringComparison.Ordinal)
           && string.Equals(left.StepId, right.StepId, StringComparison.Ordinal)
           && string.Equals(left.MutationKind, right.MutationKind, StringComparison.Ordinal)
           && left.LifestyleId == right.LifestyleId
           && string.Equals(left.ContentDigestBefore, right.ContentDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.ContentDigestAfter, right.ContentDigestAfter, StringComparison.Ordinal)
           && string.Equals(left.UntouchedSiblingDigestBefore, right.UntouchedSiblingDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.UntouchedSiblingDigestAfter, right.UntouchedSiblingDigestAfter, StringComparison.Ordinal)
           && string.Equals(left.NestedStateDigestBefore, right.NestedStateDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.NestedStateDigestAfter, right.NestedStateDigestAfter, StringComparison.Ordinal)
           && left.PreservesUntouchedSiblingState == right.PreservesUntouchedSiblingState
           && left.PreservesNestedState == right.PreservesNestedState
           && string.Equals(left.PlanDigest, right.PlanDigest, StringComparison.Ordinal)
           && left.Operations.Count == right.Operations.Count
           && left.Operations.Zip(right.Operations).All(pair =>
               pair.First.Order == pair.Second.Order
               && string.Equals(pair.First.MutationKind, pair.Second.MutationKind, StringComparison.Ordinal)
               && pair.First.LifestyleId == pair.Second.LifestyleId
               && string.Equals(pair.First.BeforeDigest, pair.Second.BeforeDigest, StringComparison.Ordinal)
               && string.Equals(pair.First.AfterDigest, pair.Second.AfterDigest, StringComparison.Ordinal)
               && pair.First.SourceAnchorIds.SequenceEqual(
                   pair.Second.SourceAnchorIds,
                   StringComparer.Ordinal));

    private static bool IsDigest(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool ValidIdempotencyKey(string? value)
        => value is { Length: > 0 and <= 200 }
           && value.All(character => char.IsLetterOrDigit(character)
               || character is '-' or '_' or '.' or ':' or '/');

    private static bool IsSortedDistinct(IReadOnlyList<string> values)
        => values.All(value => !string.IsNullOrWhiteSpace(value))
           && values.SequenceEqual(
               values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal),
               StringComparer.Ordinal);

    private static string[] Normalize(IEnumerable<string> blockers)
        => blockers.Where(blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(blocker => blocker, StringComparer.Ordinal)
            .ToArray();

    private static CharacterCreationLifestylesInteractionConfirmResult Failure(
        string outcome,
        CharacterCreationLifestylePreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationLifestylesState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, string blocker)
            => new(outcome, null, [blocker]);
    }
}
