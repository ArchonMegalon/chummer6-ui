using System.Runtime.CompilerServices;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface ICharacterCreationLoadoutInteractionPresenter
{
    CharacterCreationFoundationResult<CharacterCreationLoadoutState> Load(CharacterOverviewState display, CancellationToken cancellationToken = default);
    CharacterCreationLoadoutPrepareResult Prepare(CharacterOverviewState display, CharacterCreationLoadoutSelections selections, CancellationToken cancellationToken = default);
    CharacterCreationLoadoutConfirmResult Confirm(CharacterOverviewState display, CharacterCreationLoadoutConfirmation confirmation, CancellationToken cancellationToken = default);
    CharacterCreationLoadoutConfirmResult LookupReceipt(CharacterOverviewState display, CharacterCreationLoadoutPreparedPreview prepared);
    void Cancel(CharacterCreationLoadoutPreparedPreview prepared);
}

public sealed record CharacterCreationLoadoutPreparedPreview(CharacterCreationLoadoutPreview Preview, string IdempotencyKey);
public sealed record CharacterCreationLoadoutConfirmation(CharacterCreationLoadoutPreparedPreview Prepared,
    string PreviewDigest, string IdempotencyKey, bool ExplicitlyConfirmed);
public sealed record CharacterCreationLoadoutPrepareResult(string Outcome, CharacterCreationLoadoutState? State,
    CharacterCreationLoadoutPreparedPreview? Prepared, IReadOnlyList<string> Blockers);
public sealed record CharacterCreationLoadoutConfirmResult(string Outcome, CharacterCreationLoadoutReceipt? Receipt,
    IReadOnlyList<string> Blockers);

/// <summary>Owner/display-bound manual purchases. Core owns all prices, validation and persistence.</summary>
public sealed class CharacterCreationLoadoutInteractionPresenter(IOwnerBoundCharacterCreationLoadoutService service)
    : ICharacterCreationLoadoutInteractionPresenter
{
    public const string OutcomeUnknown = "creation-loadout-outcome-unknown";
    public const string DisplayMismatch = "creation-loadout-display-mismatch";
    public const string ReceiptMismatch = "creation-loadout-receipt-mismatch";
    private readonly ConditionalWeakTable<CharacterCreationLoadoutPreparedPreview, Admission> _prepared = new();
    private sealed class Admission(CharacterOverviewState display, OwnerContextStamp owner,
        CharacterCreationLoadoutState state, string previewDigest)
    {
        public readonly CharacterOverviewState Display = display;
        public readonly OwnerContextStamp Owner = owner;
        public readonly CharacterCreationLoadoutState State = state;
        public readonly string PreviewDigest = previewDigest;
        public readonly string StateDigest = CharacterCreationFinalizationDigest.Compute(state);
        public int Phase; // 0: review; 1: dispatched (lookup only); 2: canceled.
    }

    public CharacterCreationFoundationResult<CharacterCreationLoadoutState> Load(CharacterOverviewState display,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryAuthority(display, out var owner, out var workspace)) return Denied();
        var result = service.Prepare(owner, workspace, cancellationToken);
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Blockers.Count != 0 || result.Value is not { } state)
            return result with { Value = null };
        var binding = state.Inputs.Binding;
        if (binding.WorkspaceId != workspace || binding.ContentRevision != display.ContentRevision
            || binding.SavedRevision != display.SavedRevision
            || display.CreationWizard is not { CharacterCreated: false } wizard
            || wizard.WorkspaceId != workspace.Value || wizard.WorkspaceRevision != binding.ContentRevision
            || wizard.ContentDigest != binding.RawCharacterXmlDigest
            || state.Inputs.Schema != CharacterCreationLoadoutCompletionSchemas.InputsV1
            || state.Inputs.SnapshotDigest != CharacterCreationLoadoutCompletionRules.ComputeInputsDigest(state.Inputs)
            || !CharacterCreationLoadoutRules.IsValidAuthority(state.Inputs.Authority)
            || state.Resources.PendingDraft is null || !state.Resources.PurchaseAuthorityExact
            || state.Budget is not { IsExact: true, Blockers.Count: 0 }
            || state.Budget.KnownPurchaseCost != state.Resources.KnownPurchaseCost
            || state.Budget.TotalStartingNuyen != state.Resources.PendingDraft.Budget.TotalStartingNuyen
            || state.Resources.SnapshotDigest != CharacterCreationResourcesCompletionRules.ComputeInputsDigest(state.Resources)
            || state.Resources.Binding.WorkspaceId != workspace
            || state.Resources.Binding.ContentRevision != binding.ContentRevision
            || state.Resources.Binding.SavedRevision != binding.SavedRevision
            || state.Resources.Binding.RawCharacterXmlDigest != binding.RawCharacterXmlDigest
            || state.Resources.Binding.AuxiliaryStateDigest != binding.AuxiliaryStateDigest)
            return Denied();
        return result;
    }

    public CharacterCreationLoadoutPrepareResult Prepare(CharacterOverviewState display,
        CharacterCreationLoadoutSelections selections, CancellationToken cancellationToken = default)
    {
        var loaded = Load(display, cancellationToken);
        if (loaded.Value is not { } state) return new(loaded.Outcome, null, null, loaded.Blockers);
        // Copy UI collections; never reconstruct trusted originals or price locally.
        var request = new CharacterCreationLoadoutPreviewRequest(state.Inputs.Binding, state.Inputs.SnapshotDigest,
            new(selections.Weapons.ToArray(), selections.Armor.ToArray()));
        var result = service.Preview(display.DisplayOwnerContext!.Value, request, cancellationToken);
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Blockers.Count != 0 || result.Value is not { } preview)
            return new(result.Outcome, state, null, result.Blockers);
        if (!PreviewMatches(request, preview)) return new(CharacterCreationFoundationOutcomes.Blocked, state, null, [DisplayMismatch]);
        var prepared = new CharacterCreationLoadoutPreparedPreview(preview, "creation-loadout-" + Guid.NewGuid().ToString("N"));
        _prepared.Add(prepared, new(display, display.DisplayOwnerContext!.Value, state, preview.PreviewDigest));
        return new(result.Outcome, state, prepared, []);
    }

    public void Cancel(CharacterCreationLoadoutPreparedPreview prepared)
    {
        if (_prepared.TryGetValue(prepared, out var admission)) Interlocked.CompareExchange(ref admission.Phase, 2, 0);
    }

    public CharacterCreationLoadoutConfirmResult Confirm(CharacterOverviewState display,
        CharacterCreationLoadoutConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        var prepared = confirmation.Prepared;
        if (!Admitted(display, prepared, out var admission) || Volatile.Read(ref admission.Phase) == 2) return Failure(DisplayMismatch);
        if (!confirmation.ExplicitlyConfirmed) return Failure(CharacterCreationFinalizationBlockers.ExplicitConfirmationRequired);
        if (confirmation.PreviewDigest != admission.PreviewDigest || confirmation.IdempotencyKey != prepared.IdempotencyKey
            || prepared.Preview.PreviewDigest != admission.PreviewDigest
            || !PreviewMatches(prepared.Preview.Request, prepared.Preview)) return Failure(DisplayMismatch);
        if (Volatile.Read(ref admission.Phase) != 0) return Unknown();
        var loaded = Load(display, cancellationToken);
        if (loaded.Value is not { } current || current.Inputs.Binding != prepared.Preview.Request.Binding
            || current.Inputs.SnapshotDigest != prepared.Preview.Request.SnapshotDigest
            || current.Resources.SnapshotDigest != admission.State.Resources.SnapshotDigest) return Failure(DisplayMismatch);
        var review = service.Preview(admission.Owner, prepared.Preview.Request, cancellationToken);
        if (review.Outcome != CharacterCreationFoundationOutcomes.Success || review.Blockers.Count != 0 || review.Value is not { } preview
            || !PreviewMatches(prepared.Preview.Request, preview) || preview.PreviewDigest != admission.PreviewDigest) return Failure(DisplayMismatch);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref admission.Phase, 1, 0) != 0) return Failure(DisplayMismatch);
        try
        {
            var result = service.Confirm(admission.Owner, Request(prepared), cancellationToken);
            if (result.Value is not { } receipt) return result.Outcome == CharacterCreationFoundationOutcomes.Success
                ? Unknown() : new(result.Outcome, null, result.Blockers);
            return result.Outcome == CharacterCreationFoundationOutcomes.Success && ReceiptMatches(prepared, admission, receipt)
                ? new(result.Outcome, receipt, []) : new(OutcomeUnknown, null, [ReceiptMismatch]);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A lost/canceled reply after dispatch may already be durable. Never send Confirm again.
            return LookupReceipt(display, prepared);
        }
    }

    public CharacterCreationLoadoutConfirmResult LookupReceipt(CharacterOverviewState display, CharacterCreationLoadoutPreparedPreview prepared)
    {
        if (!Admitted(display, prepared, out var admission) || Volatile.Read(ref admission.Phase) != 1) return Failure(DisplayMismatch);
        try
        {
            var result = service.LookupReceipt(admission.Owner, prepared.Preview.Request.Binding.WorkspaceId, prepared.IdempotencyKey);
            if (result.Value is not { } receipt) return Unknown();
            return result.Outcome == CharacterCreationFoundationOutcomes.Success && ReceiptMatches(prepared, admission, receipt)
                ? new(result.Outcome, receipt, []) : new(OutcomeUnknown, null, [ReceiptMismatch]);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return Unknown(); }
    }

    private bool Admitted(CharacterOverviewState display, CharacterCreationLoadoutPreparedPreview prepared, out Admission admission)
        => _prepared.TryGetValue(prepared, out admission!) && ReferenceEquals(display, admission.Display)
            && TryAuthority(display, out var owner, out _) && owner == admission.Owner
            && admission.StateDigest == CharacterCreationFinalizationDigest.Compute(admission.State)
            && prepared.Preview.PreviewDigest == admission.PreviewDigest
            && CharacterCreationLoadoutDraftRules.PreviewDigest(prepared.Preview) == admission.PreviewDigest;

    private static bool TryAuthority(CharacterOverviewState display, out OwnerContextStamp owner, out CharacterWorkspaceId workspace)
    {
        owner = display.DisplayOwnerContext.GetValueOrDefault();
        workspace = display.WorkspaceId.GetValueOrDefault();
        return owner.IsValid && display.Session.OwnerContext == owner && display.Profile?.Created == false
            && display.WorkspaceId is not null && display.ActiveWorkspace?.Id == workspace;
    }

    private static CharacterCreationLoadoutConfirmRequest Request(CharacterCreationLoadoutPreparedPreview prepared)
        => new(prepared.Preview.Request, prepared.Preview.PreviewDigest, prepared.IdempotencyKey, true);

    private static bool PreviewMatches(CharacterCreationLoadoutPreviewRequest request, CharacterCreationLoadoutPreview preview)
        => preview.Request.Binding == request.Binding && preview.Request.SnapshotDigest == request.SnapshotDigest
            && Same(preview.Request.Selections, request.Selections) && Same(preview.Projection.Selections, request.Selections)
            && preview.Projection.Blockers.Count == 0
            && preview.Projection.ProjectionDigest == CharacterCreationLoadoutCompletionRules.ComputeProjectionDigest(preview.Projection)
            && preview.PreviewDigest == CharacterCreationLoadoutDraftRules.PreviewDigest(preview);

    private static bool ReceiptMatches(CharacterCreationLoadoutPreparedPreview prepared, Admission admission, CharacterCreationLoadoutReceipt receipt)
    {
        var binding = prepared.Preview.Request.Binding;
        var draft = receipt.Draft;
        var history = admission.State.Inputs.Receipts ?? [];
        return CharacterCreationLoadoutReceiptLedgerIntegrity.IsValidLedger(binding.WorkspaceId,
                receipt.ContentRevision, draft, [.. history, receipt])
            && receipt.Schema == CharacterCreationLoadoutReceipt.SchemaV1 && receipt.WorkspaceId == binding.WorkspaceId
            && receipt.PreviousContentRevision == binding.ContentRevision && receipt.ContentRevision == binding.ContentRevision + 1
            && receipt.PreviousSavedRevision == binding.SavedRevision && receipt.SavedRevision == receipt.ContentRevision
            && receipt.PreviousAuxiliaryStateDigest == binding.AuxiliaryStateDigest
            && receipt.IdempotencyKeyDigest == CharacterCreationLoadoutDraftRules.KeyDigest(prepared.IdempotencyKey)
            && receipt.CommandDigest == CharacterCreationLoadoutRules.Compute(Request(prepared))
            && receipt.PreviewDigest == admission.PreviewDigest
            && receipt.PreviousReceiptDigest == (admission.State.Inputs.Receipts?.LastOrDefault()?.ReceiptDigest
                ?? CharacterCreationLoadoutDraftRules.RootReceiptDigest)
            && receipt.ReceiptDigest == CharacterCreationLoadoutDraftRules.ReceiptDigest(receipt)
            && draft.Schema == CharacterCreationLoadoutDraft.SchemaV1 && draft.WorkspaceId == binding.WorkspaceId
            && draft.BaseContentRevision == binding.ContentRevision && draft.RawCharacterXmlDigest == binding.RawCharacterXmlDigest
            && draft.DraftRevision == (admission.State.Inputs.PendingDraft?.DraftRevision ?? 0) + 1
            && draft.ResourcesDraftRevision == admission.State.Resources.PendingDraft!.DraftRevision
            && draft.ResourcesDraftDigest == admission.State.Resources.PendingDraft.DraftDigest
            && draft.Authority.AuthorityDigest == admission.State.Inputs.Authority.AuthorityDigest
            && Same(draft.Authority.OriginalSelections, admission.State.Inputs.OriginalSelections)
            && Same(draft.Authority.ProposedSelections, prepared.Preview.Projection.Selections)
            && Same(draft.Selections, prepared.Preview.Projection.Selections)
            && draft.PurchaseCost == prepared.Preview.Projection.PurchaseCost
            && draft.Lines.Select(x => x.LineDigest).SequenceEqual(prepared.Preview.Projection.Lines.Select(x => x.LineDigest))
            && draft.DraftDigest == CharacterCreationLoadoutDraftRules.DraftDigest(draft);
    }

    private static bool Same(CharacterCreationLoadoutSelections left, CharacterCreationLoadoutSelections right)
        => left.Weapons.SequenceEqual(right.Weapons) && left.Armor.SequenceEqual(right.Armor);
    private static CharacterCreationFoundationResult<CharacterCreationLoadoutState> Denied()
        => new(CharacterCreationFoundationOutcomes.Blocked, null, [DisplayMismatch]);
    private static CharacterCreationLoadoutConfirmResult Failure(string blocker)
        => new(CharacterCreationFoundationOutcomes.Blocked, null, [blocker]);
    private static CharacterCreationLoadoutConfirmResult Unknown() => new(OutcomeUnknown, null, [OutcomeUnknown]);
}
