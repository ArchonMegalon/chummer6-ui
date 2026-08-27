using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationGearInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-gear-overview-authority-required";
    public const string BindingMismatch = "creation-gear-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-gear-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-gear-preview-not-confirmable";
    public const string IdempotencyKeyMismatch = "creation-gear-idempotency-key-mismatch";
    public const string ReceiptMismatch = "creation-gear-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-gear-refresh-authority-required";
}

/// <summary>
/// Renderer-neutral SR5 creation Gear basket boundary. It passes stable Core option
/// identities and quantities through preview and explicit confirmation; it never
/// parses source data, calculates prices, or writes character XML.
/// </summary>
public interface ICharacterCreationGearInteractionPresenter
{
    CharacterCreationGearInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationGearInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        IReadOnlyList<CharacterCreationGearSelection> basket);

    CharacterCreationGearInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationGearConfirmation confirmation);

    CharacterCreationGearInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey);
}

public sealed record CharacterCreationGearInteractionState(
    CharacterCreationGearBinding Binding,
    CharacterCreationGearAuthority Authority,
    CharacterCreationResourcesDraft? ResourcesDraft,
    CharacterCreationGearDraft? PendingDraft,
    CharacterCreationGearBudget Budget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

public sealed record CharacterCreationGearPreparedPreview(
    string StateSnapshotDigest,
    IReadOnlyList<CharacterCreationGearSelection> Basket,
    CharacterCreationGearPreview Preview,
    string IdempotencyKey);

public sealed record CharacterCreationGearConfirmation(
    CharacterCreationGearPreparedPreview PreparedPreview,
    string PreviewDigest,
    string IdempotencyKey,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationGearInteractionLoadResult(
    string Outcome,
    CharacterCreationGearInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationGearInteractionPrepareResult(
    string Outcome,
    CharacterCreationGearInteractionState? State,
    CharacterCreationGearPreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationGearInteractionConfirmResult(
    string Outcome,
    CharacterCreationGearPreparedPreview? PreparedPreview,
    CharacterCreationGearReceipt? Receipt,
    CharacterCreationGearInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationGearInteractionReceiptLookupResult(
    string Outcome,
    CharacterCreationGearReceipt? Receipt,
    CharacterCreationGearInteractionState? CurrentState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationGearInteractionPresenter
    : ICharacterCreationGearInteractionPresenter
{
    private const int MaximumIdempotencyKeyLength = 200;
    private readonly ICharacterCreationGearService _service;

    public CharacterCreationGearInteractionPresenter(ICharacterCreationGearService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public CharacterCreationGearInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationGearInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public CharacterCreationGearInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        IReadOnlyList<CharacterCreationGearSelection> basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationGearState gear)
        {
            return new CharacterCreationGearInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }
        CharacterCreationGearInteractionState state = Project(gear);
        if (!gear.CanEdit || gear.Blockers.Count != 0)
        {
            return new CharacterCreationGearInteractionPrepareResult(
                CharacterCreationGearOutcomes.Blocked,
                state,
                null,
                Normalize(load.Blockers.Concat(gear.Blockers)));
        }
        if (!BasketUsesSelectableAuthority(basket, gear.Authority))
        {
            return new CharacterCreationGearInteractionPrepareResult(
                CharacterCreationGearOutcomes.Invalid,
                state,
                null,
                [CharacterCreationGearBlockers.InvalidBasket]);
        }

        CharacterCreationGearResult<CharacterCreationGearPreview> result = _service.Preview(
            new CharacterCreationGearPreviewRequest(gear.Binding, basket));
        if (result.Value is not CharacterCreationGearPreview preview)
        {
            return new CharacterCreationGearInteractionPrepareResult(
                result.Outcome,
                state,
                null,
                Normalize(result.Blockers));
        }
        if (!PreviewMatches(gear, basket, preview))
        {
            return new CharacterCreationGearInteractionPrepareResult(
                CharacterCreationGearOutcomes.Conflict,
                state,
                null,
                [CharacterCreationGearInteractionBlockers.PreparedPreviewMismatch]);
        }

        CharacterCreationGearSelection[] canonicalBasket = basket
            .OrderBy(item => item.OptionId, StringComparer.Ordinal)
            .ToArray();
        var prepared = new CharacterCreationGearPreparedPreview(
            gear.SnapshotDigest,
            canonicalBasket,
            preview,
            "creation-gear-" + Guid.NewGuid().ToString("N"));
        return new CharacterCreationGearInteractionPrepareResult(
            result.Outcome,
            state,
            prepared,
            Normalize(result.Blockers.Concat(preview.Blockers)));
    }

    public CharacterCreationGearInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationGearConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationGearPreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationGearOutcomes.Invalid,
                prepared,
                CharacterCreationGearBlockers.ExplicitConfirmationRequired);
        }
        if (!CharacterCreationGearRules.IsCanonicalDigest(prepared.Preview.PreviewDigest)
            || !CharacterCreationGearRules.DigestsEqual(
                confirmation.PreviewDigest,
                prepared.Preview.PreviewDigest))
        {
            return Failure(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                CharacterCreationGearBlockers.PreviewDigestMismatch);
        }
        if (!ValidIdempotencyKey(prepared.IdempotencyKey)
            || !string.Equals(
                confirmation.IdempotencyKey,
                prepared.IdempotencyKey,
                StringComparison.Ordinal))
        {
            return Failure(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                CharacterCreationGearInteractionBlockers.IdempotencyKeyMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationGearState current)
        {
            return new CharacterCreationGearInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }
        if (!CharacterCreationGearRules.DigestsEqual(
                prepared.StateSnapshotDigest,
                current.SnapshotDigest)
            || prepared.Preview.Binding != current.Binding
            || !DraftsEqual(prepared.Preview.Before, current.PendingDraft))
        {
            return Failure(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Preview.Binding, current.Binding));
        }
        if (!prepared.Preview.RequiresExplicitConfirmation
            || !prepared.Preview.CanConfirm
            || prepared.Preview.Blockers.Count != 0)
        {
            return Failure(
                CharacterCreationGearOutcomes.Blocked,
                prepared,
                CharacterCreationGearInteractionBlockers.PreviewNotConfirmable);
        }

        CharacterCreationGearResult<CharacterCreationGearPreview> repreview = _service.Preview(
            new CharacterCreationGearPreviewRequest(current.Binding, prepared.Basket));
        if (repreview.Outcome != CharacterCreationGearOutcomes.Available
            || repreview.Value is not CharacterCreationGearPreview currentPreview
            || !PreviewMatches(current, prepared.Basket, currentPreview)
            || !CharacterCreationGearRules.DigestsEqual(
                currentPreview.PreviewDigest,
                prepared.Preview.PreviewDigest))
        {
            return Failure(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                CharacterCreationGearInteractionBlockers.PreparedPreviewMismatch);
        }

        var request = new CharacterCreationGearConfirmRequest(
            current.Binding,
            prepared.Basket,
            prepared.Preview.PreviewDigest,
            prepared.IdempotencyKey,
            ExplicitlyConfirmed: true);
        CharacterCreationGearResult<CharacterCreationGearReceipt> result = _service.Confirm(request);
        if (result.Value is not CharacterCreationGearReceipt receipt)
        {
            return new CharacterCreationGearInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                Normalize(result.Blockers));
        }
        if (result.Outcome is not (CharacterCreationGearOutcomes.Applied
                or CharacterCreationGearOutcomes.Replayed)
            || !ReceiptMatches(prepared, request, receipt))
        {
            return new CharacterCreationGearInteractionConfirmResult(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationGearInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationGearResult<CharacterCreationGearState> refresh = _service.Load(
            new CharacterCreationGearLoadRequest(receipt.WorkspaceId));
        if (refresh.Outcome != CharacterCreationGearOutcomes.Available
            || refresh.Value is not CharacterCreationGearState refreshed
            || refreshed.PendingDraft is not CharacterCreationGearDraft draft
            || draft.DraftRevision != receipt.DraftRevision
            || !CharacterCreationGearRules.DigestsEqual(draft.DraftDigest, receipt.DraftDigest)
            || refreshed.Binding.WorkspaceRevision != receipt.WorkspaceRevision
            || refreshed.Binding.ContentRevision != receipt.WorkspaceRevision
            || refreshed.Binding.SavedRevision != receipt.WorkspaceRevision)
        {
            return new CharacterCreationGearInteractionConfirmResult(
                CharacterCreationGearOutcomes.Conflict,
                prepared,
                receipt,
                null,
                Normalize(refresh.Blockers.Append(
                    CharacterCreationGearInteractionBlockers.RefreshAuthorityRequired)));
        }
        return new CharacterCreationGearInteractionConfirmResult(
            result.Outcome,
            prepared,
            receipt,
            Project(refreshed),
            Normalize(result.Blockers.Concat(refresh.Blockers)));
    }

    public CharacterCreationGearInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (!ValidIdempotencyKey(idempotencyKey)
            || !TryGetWorkspaceAuthority(overview, out CharacterWorkspaceId workspaceId))
        {
            return new CharacterCreationGearInteractionReceiptLookupResult(
                CharacterCreationGearOutcomes.Invalid,
                null,
                null,
                [CharacterCreationGearInteractionBlockers.OverviewAuthorityRequired]);
        }
        CharacterCreationGearResult<CharacterCreationGearReceipt> lookup = _service.LookupReceipt(
            new CharacterCreationGearReceiptLookupRequest(workspaceId, idempotencyKey));
        if (lookup.Value is not CharacterCreationGearReceipt receipt)
        {
            return new CharacterCreationGearInteractionReceiptLookupResult(
                lookup.Outcome,
                null,
                null,
                Normalize(lookup.Blockers));
        }
        CharacterCreationGearResult<CharacterCreationGearState> current = _service.Load(
            new CharacterCreationGearLoadRequest(workspaceId));
        if (current.Outcome != CharacterCreationGearOutcomes.Available
            || current.Value is not CharacterCreationGearState state
            || state.PendingDraft is not CharacterCreationGearDraft draft
            || receipt.WorkspaceRevision > state.Binding.WorkspaceRevision
            || receipt.DraftRevision > draft.DraftRevision
            || !CharacterCreationGearRules.DigestsEqual(
                receipt.ResourcesDraftDigest,
                draft.ResourcesDraftDigest))
        {
            return new CharacterCreationGearInteractionReceiptLookupResult(
                CharacterCreationGearOutcomes.Conflict,
                receipt,
                null,
                Normalize(current.Blockers.Append(
                    CharacterCreationGearInteractionBlockers.ReceiptMismatch)));
        }
        return new CharacterCreationGearInteractionReceiptLookupResult(
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
                CharacterCreationGearOutcomes.Blocked,
                CharacterCreationGearBlockers.CareerModeRejected);
        }
        if (!TryGetWorkspaceAuthority(overview, out CharacterWorkspaceId workspaceId)
            || overview.ActiveWorkspace is not { } activeWorkspace
            || overview.CreationWizard is not { } wizard)
        {
            return ExactLoad.Failure(
                CharacterCreationGearOutcomes.Invalid,
                CharacterCreationGearInteractionBlockers.OverviewAuthorityRequired);
        }
        CharacterCreationGearResult<CharacterCreationGearState> result = _service.Load(
            new CharacterCreationGearLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationGearOutcomes.Available
            || result.Value is not CharacterCreationGearState gear)
            return new ExactLoad(result.Outcome, null, Normalize(result.Blockers));
        if (!StateShapeIsValid(gear)
            || gear.Binding.ContentRevision != activeWorkspace.ContentRevision
            || gear.Binding.WorkspaceRevision != activeWorkspace.ContentRevision
            || gear.Binding.SavedRevision != activeWorkspace.SavedRevision
            || wizard.WorkspaceRevision != activeWorkspace.ContentRevision
            || !string.Equals(wizard.WorkspaceId, workspaceId.Value, StringComparison.Ordinal)
            || !CharacterCreationGearRules.DigestsEqual(
                wizard.ContentDigest,
                gear.Binding.RawCharacterXmlDigest))
        {
            return ExactLoad.Failure(
                CharacterCreationGearOutcomes.Conflict,
                CharacterCreationGearInteractionBlockers.BindingMismatch);
        }
        return new ExactLoad(
            CharacterCreationGearOutcomes.Available,
            gear,
            Normalize(result.Blockers.Concat(gear.Blockers)));
    }

    private static bool TryGetWorkspaceAuthority(
        CharacterOverviewState overview,
        out CharacterWorkspaceId workspaceId)
    {
        workspaceId = default;
        if (overview.Profile?.Created != false
            || overview.WorkspaceId is not { } resolved
            || overview.ActiveWorkspace is not { } active
            || !string.Equals(active.Id.Value, resolved.Value, StringComparison.Ordinal))
            return false;
        workspaceId = resolved;
        return true;
    }

    private static CharacterCreationGearInteractionState Project(CharacterCreationGearState state) => new(
        state.Binding,
        state.Authority,
        state.ResourcesDraft,
        state.PendingDraft,
        state.Budget,
        state.Blockers,
        state.CanEdit,
        state.SnapshotDigest);

    private static bool StateShapeIsValid(CharacterCreationGearState state) =>
        string.Equals(state.Schema, CharacterCreationGearSchemas.StateV1, StringComparison.Ordinal)
        && string.Equals(state.StepId, CharacterCreationWizardStepIds.Resources, StringComparison.Ordinal)
        && CharacterCreationGearRules.IsValidAuthority(state.Authority)
        && state.ResourcesDraft is not null
        && state.Budget is not null
        && state.CanEdit == (state.Blockers.Count == 0)
        && IsSortedDistinct(state.Blockers)
        && CharacterCreationGearRules.IsCanonicalDigest(state.SnapshotDigest)
        && CharacterCreationGearRules.DigestsEqual(
            state.SnapshotDigest,
            CharacterCreationGearRules.ComputeStateDigest(state));

    private static bool PreviewMatches(
        CharacterCreationGearState state,
        IReadOnlyList<CharacterCreationGearSelection> basket,
        CharacterCreationGearPreview preview) =>
        string.Equals(preview.Schema, CharacterCreationGearSchemas.PreviewV1, StringComparison.Ordinal)
        && string.Equals(preview.StepId, CharacterCreationWizardStepIds.Resources, StringComparison.Ordinal)
        && preview.Binding == state.Binding
        && DraftsEqual(preview.Before, state.PendingDraft)
        && preview.After is not null
        && preview.After.Lines.Select(item => new CharacterCreationGearSelection(
                item.OptionId,
                item.Quantity))
            .SequenceEqual(
                basket.OrderBy(item => item.OptionId, StringComparer.Ordinal),
                EqualityComparer<CharacterCreationGearSelection>.Default)
        && preview.After.FinalizationContribution == preview.FinalizationContribution
        && preview.After.Budget == preview.BudgetAfter
        && preview.RequiresExplicitConfirmation
        && preview.CanConfirm == (preview.Blockers.Count == 0)
        && IsSortedDistinct(preview.Blockers)
        && CharacterCreationGearRules.IsCanonicalDigest(preview.PreviewDigest)
        && CharacterCreationGearRules.DigestsEqual(
            preview.PreviewDigest,
            CharacterCreationGearRules.ComputePreviewDigest(preview));

    private static bool BasketUsesSelectableAuthority(
        IReadOnlyList<CharacterCreationGearSelection> basket,
        CharacterCreationGearAuthority authority)
    {
        if (basket.Count > authority.MaximumBasketLines)
            return false;
        Dictionary<string, CharacterCreationGearCatalogOption> catalog = authority.Options
            .ToDictionary(item => item.OptionId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return basket.All(selection => selection is not null
            && selection.Quantity is >= 1
            && selection.Quantity <= authority.MaximumQuantityPerLine
            && seen.Add(selection.OptionId)
            && catalog.TryGetValue(selection.OptionId, out CharacterCreationGearCatalogOption? option)
            && option.IsSelectable
            && option.Blockers.Count == 0);
    }

    private static bool DraftsEqual(
        CharacterCreationGearDraft? left,
        CharacterCreationGearDraft? right) => left is null && right is null
        || left is not null
           && right is not null
           && CharacterCreationGearRules.DigestsEqual(left.DraftDigest, right.DraftDigest);

    private static bool ReceiptMatches(
        CharacterCreationGearPreparedPreview prepared,
        CharacterCreationGearConfirmRequest request,
        CharacterCreationGearReceipt receipt) =>
        string.Equals(receipt.Schema, CharacterCreationGearSchemas.ReceiptV1, StringComparison.Ordinal)
        && receipt.WorkspaceId == prepared.Preview.Binding.WorkspaceId
        && receipt.PreviousWorkspaceRevision == prepared.Preview.Binding.WorkspaceRevision
        && receipt.WorkspaceRevision == receipt.PreviousWorkspaceRevision + 1
        && receipt.PreviousSavedRevision == prepared.Preview.Binding.SavedRevision
        && receipt.SavedRevision == receipt.WorkspaceRevision
        && receipt.ResourcesDraftRevision == prepared.Preview.Binding.ResourcesDraftRevision
        && CharacterCreationGearRules.DigestsEqual(
            receipt.ResourcesDraftDigest,
            prepared.Preview.Binding.ResourcesDraftDigest)
        && CharacterCreationGearRules.DigestsEqual(
            receipt.RawCharacterXmlDigest,
            prepared.Preview.Binding.RawCharacterXmlDigest)
        && receipt.LineCount == prepared.Preview.After.Lines.Count
        && receipt.BasketCost == prepared.Preview.BudgetAfter.BasketCost
        && receipt.RemainingNuyen == prepared.Preview.BudgetAfter.RemainingNuyen
        && CharacterCreationGearRules.DigestsEqual(
            receipt.PreviewDigest,
            prepared.Preview.PreviewDigest)
        && CharacterCreationGearRules.DigestsEqual(
            receipt.CommandDigest,
            CharacterCreationGearRules.ComputeCommandDigest(request))
        && !receipt.CharacterDocumentChanged
        && CharacterCreationGearRules.IsCanonicalDigest(receipt.ReceiptDigest)
        && CharacterCreationGearRules.DigestsEqual(
            receipt.ReceiptDigest,
            CharacterCreationGearRules.ComputeReceiptDigest(receipt));

    private static string BindingConflict(
        CharacterCreationGearBinding prepared,
        CharacterCreationGearBinding current)
    {
        if (prepared.WorkspaceRevision != current.WorkspaceRevision)
            return CharacterCreationGearBlockers.StaleWorkspaceRevision;
        if (!CharacterCreationGearRules.DigestsEqual(
                prepared.AuxiliaryStateDigest,
                current.AuxiliaryStateDigest))
            return CharacterCreationGearBlockers.StaleAuxiliaryStateDigest;
        if (!CharacterCreationGearRules.DigestsEqual(
                prepared.ResourcesDraftDigest,
                current.ResourcesDraftDigest))
            return CharacterCreationGearBlockers.StaleResourcesDraft;
        return CharacterCreationGearInteractionBlockers.BindingMismatch;
    }

    private static bool ValidIdempotencyKey(string value) =>
        value is { Length: > 0 and <= MaximumIdempotencyKeyLength }
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsSortedDistinct(IReadOnlyList<string> values) =>
        values.All(item => !string.IsNullOrWhiteSpace(item))
        && values.SequenceEqual(
            values.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static string[] Normalize(IEnumerable<string> blockers) => blockers
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(item => item, StringComparer.Ordinal)
        .ToArray();

    private static CharacterCreationGearInteractionConfirmResult Failure(
        string outcome,
        CharacterCreationGearPreparedPreview prepared,
        string blocker) => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationGearState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, params string[] blockers) =>
            new(outcome, null, Normalize(blockers));
    }
}
