using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

/// <summary>A complete selection reviewed by Core, never an implicit add command.</summary>
public sealed record CharacterCreationQualitiesSelectionReview(
    CharacterCreationQualitiesState Before,
    CharacterCreationQualitiesPreview Preview,
    IReadOnlyList<string> SelectedOptionIds,
    string IdempotencyKey,
    Guid TransactionId);

/// <summary>
/// One owner-bound editor session. Core owns legality and atomic draft/receipt persistence.
/// A dispatched confirmation is never submitted again; uncertain outcomes use read-only Load.
/// </summary>
public sealed class CharacterCreationQualitiesInteraction
{
    private readonly ICharacterCreationQualitiesService _service;
    private readonly IOwnerBoundCharacterCreationQualitiesService? _ownerBound;
    private readonly OwnerContextStamp? _owner;
    private readonly CharacterCreationQualitiesBinding _binding;
    private CharacterCreationQualitiesSelectionReview? _prepared;
    private bool _dispatched;

    public CharacterCreationQualitiesInteraction(
        ICharacterCreationQualitiesService service,
        IOwnerBoundCharacterCreationQualitiesService? ownerBound,
        CharacterOverviewState overview)
    {
        _service = service;
        _ownerBound = ownerBound;
        _owner = overview.DisplayOwnerContext;
        _binding = overview.CreationQualities?.Binding
            ?? throw new InvalidOperationException("Reload this character before editing qualities.");
        RequireDisplay(overview);
        if ((_ownerBound is not null || _owner is not null)
            && (_ownerBound is null || _owner is not { IsValid: true }))
            throw new InvalidOperationException("Reload this character in its original account.");
    }

    public bool OutcomeUncertain { get; private set; }
    public bool Confirmed { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public CharacterCreationQualitiesSelectionReview? PendingReview => _prepared;

    public void ValidateContext(CharacterOverviewState overview) => RequireOwnerAndWorkspace(overview);

    public CharacterCreationQualitiesState Load()
    {
        CharacterCreationQualitiesState state = Read();
        if (state.Binding != _binding || !state.CanEdit || state.Blockers.Count != 0)
            throw new InvalidOperationException("The character changed or its prerequisites are incomplete. Reload it before editing qualities.");
        return state;
    }

    public static string[] SelectedIds(CharacterCreationQualitiesState state)
        => (state.PendingDraft?.SelectedOptionIds
            ?? state.Preview.Selections.Select(item => item.OptionId).ToArray())
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public CharacterCreationQualitiesSelectionReview Prepare(IReadOnlyList<string> selectedOptionIds)
    {
        if (_dispatched)
            throw new InvalidOperationException("Check the saved quality selection before making another change.");
        CharacterCreationQualitiesState before = Load();
        string[] ids = selectedOptionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidOperationException("Select each quality only once.");
        var result = Preview(new(_binding, ids));
        if (result.Outcome is not (CharacterCreationFoundationOutcomes.Success or CharacterCreationFoundationOutcomes.Blocked)
            || result.Value is not { } preview
            || preview.Binding != _binding
            || !SameIds(ids, preview.Selections.Select(item => item.OptionId)))
            throw new InvalidOperationException("These qualities cannot be confirmed together. Check their prerequisites and the available Karma.");

        // The same exact command has one identity, including after a lost response.
        string key = $"desktop-qualities:{_binding.WorkspaceId.Value}:{preview.PreviewDigest}";
        Guid transaction = new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
        _prepared = new(before, preview, Array.AsReadOnly(ids), key, transaction);
        return _prepared;
    }

    public bool Confirm(CharacterCreationQualitiesSelectionReview review, CharacterOverviewState overview)
    {
        RequireDisplay(overview);
        if (_dispatched || !ReferenceEquals(review, _prepared))
            throw new InvalidOperationException("Review this selection again before confirming it.");
        CharacterCreationQualitiesState fresh = Load();
        var checkedPreview = Preview(new(_binding, review.SelectedOptionIds));
        if (fresh.SnapshotDigest != review.Before.SnapshotDigest
            || checkedPreview.Outcome != CharacterCreationFoundationOutcomes.Success
            || checkedPreview.Blockers.Count != 0 || checkedPreview.Value is not { CanConfirm: true } current
            || current.Binding != _binding || current.PreviewDigest != review.Preview.PreviewDigest)
            throw new InvalidOperationException("The character changed during review. Reload it and review the selection again.");

        if (!CharacterCreationQualitiesRules.TryPlan(review.Preview, review.Preview.PreviewDigest,
                review.IdempotencyKey, true, false, review.TransactionId, out var plan))
            throw new InvalidOperationException("Review the legal selection again before confirming.");
        _dispatched = true;
        OutcomeUncertain = true;
        var request = new CharacterCreationQualitiesConfirmRequest(_binding, review.SelectedOptionIds,
            review.Preview.PreviewDigest, review.IdempotencyKey, review.TransactionId, true);
        try
        {
            var result = _owner is { } owner
                ? _ownerBound!.Confirm(owner, request) : _service.Confirm(request);
            if (result.Value is not { } receipt)
            {
                OutcomeUncertain = false; // Core returned an explicit refusal, not a lost response.
                Status = "The selection was not applied. Reload the character and review it again.";
                return false;
            }
            if (result.Outcome != CharacterCreationFoundationOutcomes.Success
                || receipt.WorkspaceId != _binding.WorkspaceId
                || receipt.TransactionId != review.TransactionId
                || receipt.PreviewDigest != review.Preview.PreviewDigest
                || receipt.IdempotencyKeyDigest != CharacterCreationQualitiesRules.ComputeIdempotencyKeyDigest(review.IdempotencyKey)
                || receipt.CommandDigest != CommandDigest(review)
                || !CharacterCreationQualitiesRules.IsValidReceipt(receipt, plan, receipt.DraftDigest))
                return Unknown();
            return Reconcile(overview);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Unknown();
        }
    }

    /// <summary>Read the original command's persisted draft; never call Confirm as a retry.</summary>
    public bool Reconcile(CharacterOverviewState overview)
    {
        RequireOwnerAndWorkspace(overview);
        if (!_dispatched || _prepared is not { } review)
            throw new InvalidOperationException("There is no pending quality confirmation to check.");
        try
        {
            CharacterCreationQualitiesState state = Read();
            if (state.PendingDraft is { } draft
                && state.Binding.RawCharacterXmlDigest == _binding.RawCharacterXmlDigest
                && state.Binding.ContentRevision > _binding.ContentRevision
                && draft.LastIdempotencyKeyDigest == CharacterCreationQualitiesRules.ComputeIdempotencyKeyDigest(review.IdempotencyKey)
                && draft.LastCommandDigest == CommandDigest(review)
                && draft.LastPreviewDigest == review.Preview.PreviewDigest
                && SameIds(review.SelectedOptionIds, draft.SelectedOptionIds))
            {
                Confirmed = true;
                OutcomeUncertain = false;
                Status = "Quality selection saved.";
                return true;
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Preserve the original operation identity and offer another read, never another write.
        }
        return Unknown();
    }

    private bool Unknown()
    {
        OutcomeUncertain = true;
        Status = "The saved result could not be verified. Check the saved quality selection; do not submit it again.";
        return false;
    }

    private CharacterCreationQualitiesState Read()
    {
        var request = new CharacterCreationQualitiesLoadRequest(_binding.WorkspaceId);
        var result = _owner is { } owner ? _ownerBound!.Load(owner, request) : _service.Load(request);
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Value is not { } state
            || state.Binding.WorkspaceId != _binding.WorkspaceId)
            throw new InvalidOperationException("Qualities could not be loaded. Reload this character and try again.");
        return state;
    }

    private CharacterCreationFoundationResult<CharacterCreationQualitiesPreview> Preview(CharacterCreationQualitiesPreviewRequest request)
        => _owner is { } owner ? _ownerBound!.Preview(owner, request) : _service.Preview(request);

    private void RequireOwnerAndWorkspace(CharacterOverviewState overview)
    {
        if (overview.WorkspaceId != _binding.WorkspaceId || overview.DisplayOwnerContext != _owner
            || overview.Profile?.Created != false)
            throw new InvalidOperationException("The character or account changed. Return to the original character to check its qualities.");
    }

    private void RequireDisplay(CharacterOverviewState overview)
    {
        RequireOwnerAndWorkspace(overview);
        if (overview.CreationQualities?.Binding != _binding
            || overview.ActiveWorkspace?.ContentRevision != _binding.ContentRevision
            || overview.ActiveWorkspace?.SavedRevision != _binding.SavedRevision)
            throw new InvalidOperationException("The character changed. Reload it before editing qualities.");
    }

    private string CommandDigest(CharacterCreationQualitiesSelectionReview review)
        => CharacterCreationQualitiesRules.ComputeCommandDigest(_binding, review.SelectedOptionIds, review.Preview.PreviewDigest);

    private static bool SameIds(IEnumerable<string> left, IEnumerable<string> right)
        => left.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(right.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal);
}
