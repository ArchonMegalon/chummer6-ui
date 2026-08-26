using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationResourcesInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-resources-overview-authority-required";
    public const string BindingMismatch = "creation-resources-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-resources-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-resources-preview-not-confirmable";
    public const string IdempotencyKeyMismatch = "creation-resources-idempotency-key-mismatch";
    public const string ReceiptMismatch = "creation-resources-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-resources-refresh-authority-required";
}

/// <summary>
/// Renderer-neutral boundary for the SR5 Priority/Sum-to-Ten Resources step.
/// Presentation passes an exact Core option identity through preview and explicit
/// confirmation; it never calculates nuyen or writes character XML.
/// </summary>
public interface ICharacterCreationResourcesInteractionPresenter
{
    CharacterCreationResourcesInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationResourcesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        string optionId);

    CharacterCreationResourcesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationResourcesConfirmation confirmation);

    CharacterCreationResourcesInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey);
}

public sealed record CharacterCreationResourcesInteractionState(
    CharacterCreationResourcesBinding Binding,
    CharacterCreationResourcesAuthority Authority,
    CharacterCreationPrerequisiteDraft? PrerequisiteDraft,
    CharacterCreationResourcesDraft? PendingDraft,
    IReadOnlyList<CharacterCreationResourceAllocationOption> Options,
    CharacterCreationResourcesBudget Budget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

public sealed record CharacterCreationResourcesPreparedPreview(
    string StateSnapshotDigest,
    CharacterCreationResourcesBinding Binding,
    CharacterCreationResourcesDraft? Before,
    CharacterCreationResourcesDraft After,
    CharacterCreationResourceAllocationOption SelectedOption,
    CharacterCreationResourcesBudget BudgetBefore,
    CharacterCreationResourcesBudget BudgetAfter,
    CharacterCreationResourcesFinalizationContribution FinalizationContribution,
    IReadOnlyList<string> Blockers,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    string IdempotencyKey,
    string PreviewDigest);

public sealed record CharacterCreationResourcesConfirmation(
    CharacterCreationResourcesPreparedPreview PreparedPreview,
    string PreviewDigest,
    string IdempotencyKey,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationResourcesInteractionLoadResult(
    string Outcome,
    CharacterCreationResourcesInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationResourcesInteractionPrepareResult(
    string Outcome,
    CharacterCreationResourcesInteractionState? State,
    CharacterCreationResourcesPreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationResourcesInteractionConfirmResult(
    string Outcome,
    CharacterCreationResourcesPreparedPreview? PreparedPreview,
    CharacterCreationResourcesReceipt? Receipt,
    CharacterCreationResourcesInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationResourcesInteractionReceiptLookupResult(
    string Outcome,
    CharacterCreationResourcesReceipt? Receipt,
    CharacterCreationResourcesInteractionState? CurrentState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationResourcesInteractionPresenter
    : ICharacterCreationResourcesInteractionPresenter
{
    private readonly ICharacterCreationResourcesService _service;

    public CharacterCreationResourcesInteractionPresenter(
        ICharacterCreationResourcesService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public CharacterCreationResourcesInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationResourcesInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public CharacterCreationResourcesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        string optionId)
    {
        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationResourcesState resources)
        {
            return new CharacterCreationResourcesInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }

        CharacterCreationResourcesInteractionState state = Project(resources);
        if (!resources.CanEdit || resources.Blockers.Count != 0)
        {
            return new CharacterCreationResourcesInteractionPrepareResult(
                CharacterCreationResourcesOutcomes.Blocked,
                state,
                null,
                Normalize(load.Blockers.Concat(resources.Blockers)));
        }

        CharacterCreationResourceAllocationOption? option = resources.Options.SingleOrDefault(candidate =>
            string.Equals(candidate.OptionId, optionId, StringComparison.Ordinal));
        if (option is null || !option.IsEnabled || option.Blockers.Count != 0)
        {
            return new CharacterCreationResourcesInteractionPrepareResult(
                CharacterCreationResourcesOutcomes.Invalid,
                state,
                null,
                [CharacterCreationResourcesBlockers.InvalidOption]);
        }

        CharacterCreationResourcesResult<CharacterCreationResourcesPreview> result =
            _service.Preview(new CharacterCreationResourcesPreviewRequest(
                resources.Binding,
                option.OptionId));
        if (result.Value is not CharacterCreationResourcesPreview preview)
        {
            return new CharacterCreationResourcesInteractionPrepareResult(
                result.Outcome,
                state,
                null,
                Normalize(result.Blockers));
        }
        if (!PreviewMatches(resources, option, preview))
        {
            return new CharacterCreationResourcesInteractionPrepareResult(
                CharacterCreationResourcesOutcomes.Conflict,
                state,
                null,
                [CharacterCreationResourcesInteractionBlockers.PreparedPreviewMismatch]);
        }

        return new CharacterCreationResourcesInteractionPrepareResult(
            result.Outcome,
            state,
            Project(resources, preview),
            Normalize(result.Blockers.Concat(preview.Blockers)));
    }

    public CharacterCreationResourcesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationResourcesConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationResourcesPreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationResourcesOutcomes.Invalid,
                prepared,
                CharacterCreationResourcesBlockers.ExplicitConfirmationRequired);
        }
        if (!CharacterCreationResourcesRules.DigestsEqual(
                confirmation.PreviewDigest,
                prepared.PreviewDigest)
            || !CharacterCreationResourcesRules.IsCanonicalDigest(prepared.PreviewDigest))
        {
            return Failure(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                CharacterCreationResourcesBlockers.PreviewDigestMismatch);
        }
        if (!ValidIdempotencyKey(prepared.IdempotencyKey)
            || !string.Equals(
                confirmation.IdempotencyKey,
                prepared.IdempotencyKey,
                StringComparison.Ordinal))
        {
            return Failure(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                CharacterCreationResourcesInteractionBlockers.IdempotencyKeyMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationResourcesState resources)
        {
            return new CharacterCreationResourcesInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }
        if (!PreparedStillMatches(prepared, resources))
        {
            return Failure(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Binding, resources.Binding));
        }
        if (!prepared.RequiresExplicitConfirmation
            || !prepared.CanConfirm
            || prepared.Blockers.Count != 0)
        {
            return new CharacterCreationResourcesInteractionConfirmResult(
                CharacterCreationResourcesOutcomes.Blocked,
                prepared,
                null,
                null,
                prepared.Blockers.Count == 0
                    ? [CharacterCreationResourcesInteractionBlockers.PreviewNotConfirmable]
                    : Normalize(prepared.Blockers));
        }

        // Re-preview against the just-loaded binding. This turns even a structurally
        // plausible stale prepared envelope into a conflict before the commit call.
        CharacterCreationResourcesResult<CharacterCreationResourcesPreview> repreview =
            _service.Preview(new CharacterCreationResourcesPreviewRequest(
                resources.Binding,
                prepared.SelectedOption.OptionId));
        if (repreview.Outcome != CharacterCreationResourcesOutcomes.Available
            || repreview.Value is not CharacterCreationResourcesPreview currentPreview
            || !PreviewMatches(resources, prepared.SelectedOption, currentPreview)
            || !PreparedMatchesPreview(prepared, currentPreview))
        {
            return Failure(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                CharacterCreationResourcesInteractionBlockers.PreparedPreviewMismatch);
        }

        var request = new CharacterCreationResourcesConfirmRequest(
            prepared.Binding,
            prepared.SelectedOption.OptionId,
            prepared.PreviewDigest,
            prepared.IdempotencyKey,
            ExplicitlyConfirmed: true);
        CharacterCreationResourcesResult<CharacterCreationResourcesReceipt> result =
            _service.Confirm(request);
        if (result.Value is not CharacterCreationResourcesReceipt receipt)
        {
            return new CharacterCreationResourcesInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                Normalize(result.Blockers));
        }
        if (result.Outcome is not (CharacterCreationResourcesOutcomes.Applied
                or CharacterCreationResourcesOutcomes.Replayed)
            || !ReceiptMatches(prepared, request, receipt))
        {
            return new CharacterCreationResourcesInteractionConfirmResult(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationResourcesInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationResourcesResult<CharacterCreationResourcesState> refresh =
            _service.Load(new CharacterCreationResourcesLoadRequest(receipt.WorkspaceId));
        if (refresh.Outcome != CharacterCreationResourcesOutcomes.Available
            || refresh.Value is not CharacterCreationResourcesState refreshed
            || !RefreshedStateMatches(prepared, receipt, refreshed))
        {
            return new CharacterCreationResourcesInteractionConfirmResult(
                CharacterCreationResourcesOutcomes.Conflict,
                prepared,
                receipt,
                null,
                Normalize(refresh.Blockers.Append(
                    CharacterCreationResourcesInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationResourcesInteractionConfirmResult(
            result.Outcome,
            prepared,
            receipt,
            Project(refreshed),
            Normalize(result.Blockers.Concat(refresh.Blockers)));
    }

    public CharacterCreationResourcesInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (!ValidIdempotencyKey(idempotencyKey)
            || overview.Profile?.Created != false
            || overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return new CharacterCreationResourcesInteractionReceiptLookupResult(
                CharacterCreationResourcesOutcomes.Invalid,
                null,
                null,
                [CharacterCreationResourcesInteractionBlockers.OverviewAuthorityRequired]);
        }

        CharacterCreationResourcesResult<CharacterCreationResourcesReceipt> lookup =
            _service.LookupReceipt(new CharacterCreationResourcesReceiptLookupRequest(
                workspaceId,
                idempotencyKey));
        if (lookup.Value is not CharacterCreationResourcesReceipt receipt)
        {
            return new CharacterCreationResourcesInteractionReceiptLookupResult(
                lookup.Outcome,
                null,
                null,
                Normalize(lookup.Blockers));
        }

        CharacterCreationResourcesResult<CharacterCreationResourcesState> current =
            _service.Load(new CharacterCreationResourcesLoadRequest(workspaceId));
        if (current.Outcome != CharacterCreationResourcesOutcomes.Available
            || current.Value is not CharacterCreationResourcesState state
            || !ReceiptCanBelongToCurrentState(receipt, state))
        {
            return new CharacterCreationResourcesInteractionReceiptLookupResult(
                CharacterCreationResourcesOutcomes.Conflict,
                receipt,
                null,
                Normalize(current.Blockers.Append(
                    CharacterCreationResourcesInteractionBlockers.ReceiptMismatch)));
        }

        return new CharacterCreationResourcesInteractionReceiptLookupResult(
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
                CharacterCreationResourcesOutcomes.Blocked,
                CharacterCreationResourcesBlockers.CareerModeRejected);
        }
        if (overview.Profile?.Created != false
            || overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || overview.CreationWizard is not { } wizard
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return ExactLoad.Failure(
                CharacterCreationResourcesOutcomes.Invalid,
                CharacterCreationResourcesInteractionBlockers.OverviewAuthorityRequired);
        }

        CharacterCreationResourcesResult<CharacterCreationResourcesState> result =
            _service.Load(new CharacterCreationResourcesLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationResourcesOutcomes.Available
            || result.Value is not CharacterCreationResourcesState resources)
        {
            return new ExactLoad(result.Outcome, null, Normalize(result.Blockers));
        }
        if (!MatchesOverview(activeWorkspace.ContentRevision, activeWorkspace.SavedRevision, wizard, resources))
        {
            return ExactLoad.Failure(
                CharacterCreationResourcesOutcomes.Conflict,
                CharacterCreationResourcesInteractionBlockers.BindingMismatch);
        }
        return new ExactLoad(
            CharacterCreationResourcesOutcomes.Available,
            resources,
            Normalize(result.Blockers.Concat(resources.Blockers)));
    }

    private static bool MatchesOverview(
        long contentRevision,
        long savedRevision,
        CharacterCreationWizardSnapshot wizard,
        CharacterCreationResourcesState state)
        => StateShapeIsValid(state)
           && state.Binding.ContentRevision == contentRevision
           && state.Binding.WorkspaceRevision == contentRevision
           && state.Binding.SavedRevision == savedRevision
           && string.Equals(wizard.WorkspaceId, state.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && wizard.WorkspaceRevision == contentRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               wizard.ContentDigest,
               state.Binding.RawCharacterXmlDigest)
           && !wizard.CharacterCreated;

    private static CharacterCreationResourcesInteractionState Project(
        CharacterCreationResourcesState state)
        => new(
            state.Binding,
            state.Authority,
            state.PrerequisiteDraft,
            state.PendingDraft,
            state.Options,
            state.Budget,
            state.Blockers,
            state.CanEdit,
            state.SnapshotDigest);

    private static CharacterCreationResourcesPreparedPreview Project(
        CharacterCreationResourcesState state,
        CharacterCreationResourcesPreview preview)
        => new(
            state.SnapshotDigest,
            preview.Binding,
            preview.Before,
            preview.After,
            preview.SelectedOption!,
            preview.BudgetBefore,
            preview.BudgetAfter,
            preview.FinalizationContribution,
            preview.Blockers,
            preview.RequiresExplicitConfirmation,
            preview.CanConfirm,
            "creation-resources-" + Guid.NewGuid().ToString("N"),
            preview.PreviewDigest);

    private static bool PreviewMatches(
        CharacterCreationResourcesState state,
        CharacterCreationResourceAllocationOption option,
        CharacterCreationResourcesPreview preview)
        => string.Equals(preview.Schema, CharacterCreationResourcesSchemas.PreviewV1, StringComparison.Ordinal)
           && string.Equals(preview.StepId, CharacterCreationWizardStepIds.Resources, StringComparison.Ordinal)
           && preview.Binding == state.Binding
           && DraftsEqual(preview.Before, state.PendingDraft)
           && OptionEquals(preview.SelectedOption, option)
           && BudgetEquals(preview.BudgetBefore, state.Budget)
           && BudgetShapeIsValid(preview.BudgetAfter)
           && PreviewDraftShapeIsValid(preview.After, preview.Binding)
           && preview.After.SelectedOptionId == option.OptionId
           && preview.After.KarmaInvestment == option.KarmaInvestment
           && BudgetEquals(preview.After.Budget, preview.BudgetAfter)
           && ContributionEquals(preview.After.FinalizationContribution, preview.FinalizationContribution)
           && ContributionShapeIsValid(preview.FinalizationContribution, preview.Binding)
           && preview.RequiresExplicitConfirmation
           && preview.CanConfirm == (preview.Blockers.Count == 0)
           && IsSortedDistinct(preview.Blockers)
           && CharacterCreationResourcesRules.IsCanonicalDigest(preview.PreviewDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               preview.PreviewDigest,
               CharacterCreationResourcesRules.ComputePreviewDigest(preview));

    private static bool PreparedMatchesPreview(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesPreview preview)
        => prepared.Binding == preview.Binding
           && DraftsEqual(prepared.Before, preview.Before)
           && PreviewDraftEquals(prepared.After, preview.After)
           && OptionEquals(prepared.SelectedOption, preview.SelectedOption)
           && BudgetEquals(prepared.BudgetBefore, preview.BudgetBefore)
           && BudgetEquals(prepared.BudgetAfter, preview.BudgetAfter)
           && ContributionEquals(prepared.FinalizationContribution, preview.FinalizationContribution)
           && prepared.Blockers.SequenceEqual(preview.Blockers, StringComparer.Ordinal)
           && prepared.RequiresExplicitConfirmation == preview.RequiresExplicitConfirmation
           && prepared.CanConfirm == preview.CanConfirm
           && CharacterCreationResourcesRules.DigestsEqual(prepared.PreviewDigest, preview.PreviewDigest);

    private static bool PreparedStillMatches(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesState current)
        => CharacterCreationResourcesRules.DigestsEqual(
               prepared.StateSnapshotDigest,
               current.SnapshotDigest)
           && prepared.Binding == current.Binding
           && DraftsEqual(prepared.Before, current.PendingDraft)
           && BudgetEquals(prepared.BudgetBefore, current.Budget)
           && current.Options.Any(option => OptionEquals(option, prepared.SelectedOption)
               && option.IsEnabled
               && option.Blockers.Count == 0);

    private static bool ReceiptMatches(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesConfirmRequest request,
        CharacterCreationResourcesReceipt receipt)
        => string.Equals(receipt.Schema, CharacterCreationResourcesSchemas.ReceiptV1, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(receipt.ReceiptId)
           && receipt.WorkspaceId == prepared.Binding.WorkspaceId
           && receipt.PreviousWorkspaceRevision == prepared.Binding.WorkspaceRevision
           && receipt.WorkspaceRevision == receipt.PreviousWorkspaceRevision + 1
           && receipt.PreviousSavedRevision == prepared.Binding.SavedRevision
           && receipt.SavedRevision == receipt.WorkspaceRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.RawCharacterXmlDigest,
               prepared.Binding.RawCharacterXmlDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.PrerequisiteDraftDigest,
               prepared.Binding.PrerequisiteDraftDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.AuthorityDigest, prepared.Binding.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.SourceDigest, prepared.Binding.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.RulesDigest, prepared.Binding.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.RuntimeDigest, prepared.Binding.RuntimeDigest)
           && string.Equals(receipt.OptionId, prepared.SelectedOption.OptionId, StringComparison.Ordinal)
           && receipt.KarmaInvestment == prepared.SelectedOption.KarmaInvestment
           && receipt.TotalStartingNuyen == prepared.BudgetAfter.TotalStartingNuyen
           && receipt.RemainingNuyen == prepared.BudgetAfter.RemainingNuyen
           && receipt.DraftRevision == prepared.After.DraftRevision
           && CommittedDraftDigestMatches(prepared, receipt)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.PreviewDigest, prepared.PreviewDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.IdempotencyKeyDigest,
               ComputeIdempotencyKeyDigest(prepared.IdempotencyKey))
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.CommandDigest,
               CharacterCreationResourcesRules.ComputeCommandDigest(request))
           && !receipt.CharacterDocumentChanged
           && CharacterCreationResourcesRules.IsCanonicalDigest(receipt.PreviousReceiptDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(receipt.ReceiptDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.ReceiptDigest,
               CharacterCreationResourcesRules.ComputeReceiptDigest(receipt));

    private static bool RefreshedStateMatches(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesReceipt receipt,
        CharacterCreationResourcesState refreshed)
        => StateShapeIsValid(refreshed)
           && refreshed.Binding.WorkspaceId == receipt.WorkspaceId
           && refreshed.Binding.WorkspaceRevision == receipt.WorkspaceRevision
           && refreshed.Binding.ContentRevision == receipt.WorkspaceRevision
           && refreshed.Binding.SavedRevision == receipt.SavedRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               refreshed.Binding.RawCharacterXmlDigest,
               receipt.RawCharacterXmlDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               refreshed.Binding.PrerequisiteDraftDigest,
               receipt.PrerequisiteDraftDigest)
           && CharacterCreationResourcesRules.DigestsEqual(refreshed.Binding.AuthorityDigest, receipt.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(refreshed.Binding.SourceDigest, receipt.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(refreshed.Binding.RulesDigest, receipt.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(refreshed.Binding.RuntimeDigest, receipt.RuntimeDigest)
           && refreshed.PendingDraft is { } draft
           && CommittedDraftMatches(prepared, receipt, draft)
           && BudgetEquals(refreshed.Budget, prepared.BudgetAfter);

    private static bool ReceiptCanBelongToCurrentState(
        CharacterCreationResourcesReceipt receipt,
        CharacterCreationResourcesState current)
        => StateShapeIsValid(current)
           && receipt.WorkspaceId == current.Binding.WorkspaceId
           && receipt.WorkspaceRevision == current.Binding.WorkspaceRevision
           && receipt.SavedRevision == current.Binding.SavedRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.RawCharacterXmlDigest,
               current.Binding.RawCharacterXmlDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.AuthorityDigest, current.Binding.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.SourceDigest, current.Binding.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.RulesDigest, current.Binding.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(receipt.RuntimeDigest, current.Binding.RuntimeDigest)
           && current.PendingDraft is { } draft
           && draft.DraftRevision == receipt.DraftRevision
           && CharacterCreationResourcesRules.DigestsEqual(draft.DraftDigest, receipt.DraftDigest)
           && string.Equals(draft.SelectedOptionId, receipt.OptionId, StringComparison.Ordinal)
           && CharacterCreationResourcesRules.IsCanonicalDigest(receipt.ReceiptDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               receipt.ReceiptDigest,
               CharacterCreationResourcesRules.ComputeReceiptDigest(receipt));

    private static bool StateShapeIsValid(CharacterCreationResourcesState state)
        => string.Equals(state.Schema, CharacterCreationResourcesSchemas.StateV1, StringComparison.Ordinal)
           && string.Equals(state.StepId, CharacterCreationWizardStepIds.Resources, StringComparison.Ordinal)
           && state.Binding.WorkspaceId.Value.Length > 0
           && state.Binding.WorkspaceRevision == state.Binding.ContentRevision
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.RawCharacterXmlDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.AuxiliaryStateDigest)
           && state.Binding.PrerequisiteDraftRevision > 0
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.PrerequisiteDraftDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.AuthorityDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.SourceDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.RulesDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.Binding.RuntimeDigest)
           && CharacterCreationResourcesRules.IsValidAuthority(state.Authority)
           && CharacterCreationResourcesRules.DigestsEqual(
               state.Authority.AuthorityDigest,
               state.Binding.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(state.Authority.SourceDigest, state.Binding.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(state.Authority.RulesDigest, state.Binding.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(state.Authority.RuntimeDigest, state.Binding.RuntimeDigest)
           && state.PrerequisiteDraft is { } prerequisite
           && prerequisite.DraftRevision == state.Binding.PrerequisiteDraftRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               prerequisite.DraftDigest,
               state.Binding.PrerequisiteDraftDigest)
           && state.Options is { Count: > 0 and <= 64 }
           && state.Options.Select(option => option.OptionId).Distinct(StringComparer.Ordinal).Count()
                == state.Options.Count
           && state.Options.All(OptionShapeIsValid)
           && BudgetShapeIsValid(state.Budget)
           && (state.PendingDraft is null || CommittedDraftShapeIsValid(state.PendingDraft, state.Binding))
           && IsSortedDistinct(state.Blockers)
           && state.CanEdit == (state.Blockers.Count == 0)
           && CharacterCreationResourcesRules.IsCanonicalDigest(state.SnapshotDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               state.SnapshotDigest,
               CharacterCreationResourcesRules.ComputeStateDigest(state));

    private static bool OptionShapeIsValid(CharacterCreationResourceAllocationOption option)
        => !string.IsNullOrWhiteSpace(option.OptionId)
           && option.KarmaInvestment >= 0
           && option.NuyenFromKarma >= 0m
           && option.TotalStartingNuyen >= 0m
           && IsSortedDistinct(option.Blockers)
           && option.SourceAnchorIds.Count > 0
           && option.SourceAnchorIds.All(anchor => !string.IsNullOrWhiteSpace(anchor))
           && option.IsEnabled == (option.Blockers.Count == 0)
           && CharacterCreationResourcesRules.IsCanonicalDigest(option.OptionDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               option.OptionDigest,
               CharacterCreationResourcesRules.ComputeAllocationOptionDigest(option));

    private static bool BudgetShapeIsValid(CharacterCreationResourcesBudget budget)
        => budget.PriorityNuyen >= 0m
           && budget.KarmaInvestment >= 0
           && budget.NuyenFromKarma >= 0m
           && budget.TotalStartingNuyen >= 0m
           && budget.KnownPurchaseCost >= 0m
           && budget.RemainingNuyen >= 0m
           && budget.Overspend >= 0m
           && budget.CarryoverLimit >= 0m
           && budget.CarryoverExcess >= 0m
           && IsSortedDistinct(budget.Blockers)
           && budget.SourceAnchorIds.Count > 0
           && budget.SourceAnchorIds.All(anchor => !string.IsNullOrWhiteSpace(anchor))
           && (!budget.IsExact || budget.Blockers.Count == 0);

    private static bool PreviewDraftShapeIsValid(
        CharacterCreationResourcesDraft draft,
        CharacterCreationResourcesBinding binding)
        => string.Equals(draft.Schema, CharacterCreationResourcesSchemas.DraftV1, StringComparison.Ordinal)
           && draft.WorkspaceId == binding.WorkspaceId
           && draft.DraftRevision > 0
           && draft.BaseContentRevision <= binding.ContentRevision
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.BaseRawCharacterXmlDigest)
           && draft.PrerequisiteDraftRevision == binding.PrerequisiteDraftRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               draft.PrerequisiteDraftDigest,
               binding.PrerequisiteDraftDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.AuthorityDigest, binding.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.SourceDigest, binding.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.RulesDigest, binding.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.RuntimeDigest, binding.RuntimeDigest)
           && !string.IsNullOrWhiteSpace(draft.SelectedOptionId)
           && draft.KarmaInvestment >= 0
           && BudgetShapeIsValid(draft.Budget)
           && ContributionShapeIsValid(draft.FinalizationContribution, binding)
           && draft.SourceAnchorIds.Count > 0
           && !draft.CharacterEffectsApplied
           && string.IsNullOrEmpty(draft.LastIdempotencyKeyDigest)
           && string.IsNullOrEmpty(draft.LastPreviewDigest)
           && string.IsNullOrEmpty(draft.LastCommandDigest)
           && string.IsNullOrEmpty(draft.DraftDigest);

    private static bool CommittedDraftShapeIsValid(
        CharacterCreationResourcesDraft draft,
        CharacterCreationResourcesBinding binding)
        => string.Equals(draft.Schema, CharacterCreationResourcesSchemas.DraftV1, StringComparison.Ordinal)
           && draft.WorkspaceId == binding.WorkspaceId
           && draft.DraftRevision > 0
           && draft.BaseContentRevision < binding.ContentRevision
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.BaseRawCharacterXmlDigest)
           && draft.PrerequisiteDraftRevision == binding.PrerequisiteDraftRevision
           && CharacterCreationResourcesRules.DigestsEqual(
               draft.PrerequisiteDraftDigest,
               binding.PrerequisiteDraftDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.AuthorityDigest, binding.AuthorityDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.SourceDigest, binding.SourceDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.RulesDigest, binding.RulesDigest)
           && CharacterCreationResourcesRules.DigestsEqual(draft.RuntimeDigest, binding.RuntimeDigest)
           && !string.IsNullOrWhiteSpace(draft.SelectedOptionId)
           && draft.KarmaInvestment >= 0
           && BudgetShapeIsValid(draft.Budget)
           && ContributionShapeIsValid(draft.FinalizationContribution, binding)
           && draft.SourceAnchorIds.Count > 0
           && !draft.CharacterEffectsApplied
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.LastIdempotencyKeyDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.LastPreviewDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.LastCommandDigest)
           && CharacterCreationResourcesRules.IsCanonicalDigest(draft.DraftDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               draft.DraftDigest,
               CharacterCreationResourcesRules.ComputeDraftDigest(draft));

    private static bool CommittedDraftDigestMatches(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesReceipt receipt)
    {
        CharacterCreationResourcesDraft expected = prepared.After with
        {
            DraftRevision = receipt.DraftRevision,
            BaseContentRevision = receipt.PreviousWorkspaceRevision,
            LastIdempotencyKeyDigest = receipt.IdempotencyKeyDigest,
            LastPreviewDigest = receipt.PreviewDigest,
            LastCommandDigest = receipt.CommandDigest,
            DraftDigest = string.Empty
        };
        return CharacterCreationResourcesRules.DigestsEqual(
            receipt.DraftDigest,
            CharacterCreationResourcesRules.ComputeDraftDigest(expected));
    }

    private static bool CommittedDraftMatches(
        CharacterCreationResourcesPreparedPreview prepared,
        CharacterCreationResourcesReceipt receipt,
        CharacterCreationResourcesDraft draft)
    {
        CharacterCreationResourcesDraft candidate = prepared.After with
        {
            DraftRevision = receipt.DraftRevision,
            BaseContentRevision = receipt.PreviousWorkspaceRevision,
            LastIdempotencyKeyDigest = receipt.IdempotencyKeyDigest,
            LastPreviewDigest = receipt.PreviewDigest,
            LastCommandDigest = receipt.CommandDigest,
            DraftDigest = string.Empty
        };
        CharacterCreationResourcesDraft expected = candidate with
        {
            DraftDigest = CharacterCreationResourcesRules.ComputeDraftDigest(candidate)
        };
        return CharacterCreationResourcesRules.DigestsEqual(draft.DraftDigest, expected.DraftDigest);
    }

    private static bool ContributionShapeIsValid(
        CharacterCreationResourcesFinalizationContribution contribution,
        CharacterCreationResourcesBinding binding)
        => string.Equals(contribution.Schema, CharacterCreationResourcesSchemas.ContributionV1, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(contribution.PriorityRank)
           && !string.IsNullOrWhiteSpace(contribution.PrioritySourceId)
           && contribution.StartingNuyen >= 0m
           && contribution.NuyenKarma >= 0
           && CharacterCreationResourcesRules.DigestsEqual(
               contribution.ExpectedRawCharacterXmlDigest,
               binding.RawCharacterXmlDigest)
           && contribution.SourceAnchorIds.Count > 0
           && contribution.SourceAnchorIds.All(anchor => !string.IsNullOrWhiteSpace(anchor))
           && CharacterCreationResourcesRules.IsCanonicalDigest(contribution.ContributionDigest)
           && CharacterCreationResourcesRules.DigestsEqual(
               contribution.ContributionDigest,
               CharacterCreationResourcesRules.ComputeContributionDigest(contribution));

    private static bool OptionEquals(
        CharacterCreationResourceAllocationOption? left,
        CharacterCreationResourceAllocationOption? right)
        => left is not null
           && right is not null
           && string.Equals(left.OptionId, right.OptionId, StringComparison.Ordinal)
           && left.KarmaInvestment == right.KarmaInvestment
           && left.NuyenFromKarma == right.NuyenFromKarma
           && left.TotalStartingNuyen == right.TotalStartingNuyen
           && left.IsEnabled == right.IsEnabled
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal)
           && CharacterCreationResourcesRules.DigestsEqual(left.OptionDigest, right.OptionDigest);

    private static bool BudgetEquals(
        CharacterCreationResourcesBudget left,
        CharacterCreationResourcesBudget right)
        => left.PriorityNuyen == right.PriorityNuyen
           && left.KarmaInvestment == right.KarmaInvestment
           && left.NuyenFromKarma == right.NuyenFromKarma
           && left.TotalStartingNuyen == right.TotalStartingNuyen
           && left.KnownPurchaseCost == right.KnownPurchaseCost
           && left.RemainingNuyen == right.RemainingNuyen
           && left.Overspend == right.Overspend
           && left.CarryoverLimit == right.CarryoverLimit
           && left.CarryoverExcess == right.CarryoverExcess
           && left.IsExact == right.IsExact
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal);

    private static bool DraftsEqual(
        CharacterCreationResourcesDraft? left,
        CharacterCreationResourcesDraft? right)
        => left is null
            ? right is null
            : right is not null
              && CharacterCreationResourcesRules.DigestsEqual(left.DraftDigest, right.DraftDigest);

    private static bool PreviewDraftEquals(
        CharacterCreationResourcesDraft left,
        CharacterCreationResourcesDraft right)
        => CharacterCreationResourcesRules.DigestsEqual(
            CharacterCreationResourcesRules.ComputeDraftDigest(left),
            CharacterCreationResourcesRules.ComputeDraftDigest(right));

    private static bool ContributionEquals(
        CharacterCreationResourcesFinalizationContribution left,
        CharacterCreationResourcesFinalizationContribution right)
        => CharacterCreationResourcesRules.DigestsEqual(
            left.ContributionDigest,
            right.ContributionDigest);

    private static string BindingConflict(
        CharacterCreationResourcesBinding expected,
        CharacterCreationResourcesBinding actual)
    {
        if (expected.WorkspaceId != actual.WorkspaceId
            || expected.WorkspaceRevision != actual.WorkspaceRevision
            || expected.ContentRevision != actual.ContentRevision
            || expected.SavedRevision != actual.SavedRevision)
            return CharacterCreationResourcesBlockers.StaleWorkspaceRevision;
        if (!CharacterCreationResourcesRules.DigestsEqual(
                expected.RawCharacterXmlDigest,
                actual.RawCharacterXmlDigest))
            return CharacterCreationResourcesBlockers.StaleContentDigest;
        if (!CharacterCreationResourcesRules.DigestsEqual(
                expected.AuxiliaryStateDigest,
                actual.AuxiliaryStateDigest))
            return CharacterCreationResourcesBlockers.StaleAuxiliaryStateDigest;
        if (expected.PrerequisiteDraftRevision != actual.PrerequisiteDraftRevision
            || !CharacterCreationResourcesRules.DigestsEqual(
                expected.PrerequisiteDraftDigest,
                actual.PrerequisiteDraftDigest))
            return CharacterCreationResourcesBlockers.StalePrerequisiteDraft;
        if (!CharacterCreationResourcesRules.DigestsEqual(expected.SourceDigest, actual.SourceDigest))
            return CharacterCreationResourcesBlockers.StaleSourceDigest;
        if (!CharacterCreationResourcesRules.DigestsEqual(expected.RulesDigest, actual.RulesDigest))
            return CharacterCreationResourcesBlockers.StaleRulesDigest;
        if (!CharacterCreationResourcesRules.DigestsEqual(expected.RuntimeDigest, actual.RuntimeDigest))
            return CharacterCreationResourcesBlockers.StaleRuntimeDigest;
        return CharacterCreationResourcesInteractionBlockers.BindingMismatch;
    }

    private static bool ValidIdempotencyKey(string? value)
        => value is { Length: > 0 and <= 200 }
           && value.All(character => char.IsLetterOrDigit(character)
               || character is '-' or '_' or '.' or ':' or '/');

    private static string ComputeIdempotencyKeyDigest(string value)
        => CharacterCreationResourcesRules.ComputeIdempotencyKeyDigest(
            "chummer.sr5.creation-resources.idempotency.v1\0" + value);

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

    private static CharacterCreationResourcesInteractionConfirmResult Failure(
        string outcome,
        CharacterCreationResourcesPreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationResourcesState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, string blocker)
            => new(outcome, null, [blocker]);
    }
}
