using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Persistence boundary for a Priority creation-quality draft. Implementations must claim
/// TransactionId and IdempotencyKeyDigest, compare both workspace revisions plus content and
/// auxiliary-state digests, persist the draft and receipt atomically, and leave the canonical
/// character document untouched.
/// </summary>
public interface ICharacterCreationQualitiesAtomicWorkspace
{
    Task<CharacterCreationQualitiesAuthoritySnapshot?> ReadAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken cancellationToken);

    Task<CharacterCreationQualitiesAtomicCommitResult?> CommitDraftAsync(
        CharacterCreationQualitiesDraftPlan plan,
        CancellationToken cancellationToken);
}

public sealed record CharacterCreationQualitiesAuthoritySnapshot(
    CharacterCreationQualitiesInput Input,
    IReadOnlyList<CharacterCreationQualitiesDraftReceipt> PersistedReceipts,
    IReadOnlyList<Guid> ReservedTransactionIds);

public sealed record CharacterCreationQualitiesDesktopOption(
    string OptionId,
    Guid SourceId,
    string SelectionKey,
    string Name,
    CharacterCreationQualityType Type,
    int Rating,
    int KarmaCost,
    bool IsMetagenic,
    bool IsFreeOrGranted,
    bool IsSelectable,
    string? DisableReasonKey,
    string? FollowUpChoiceId,
    string? FollowUpChoiceLabel,
    IReadOnlyList<string> SourceAnchorIds);

public sealed record CharacterCreationQualitiesEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    long SavedRevision,
    string AuthorityDigest,
    string RuntimeDigest,
    IReadOnlyList<CharacterCreationQualitiesDesktopOption> Options,
    IReadOnlyList<string> SelectedOptionIds,
    CharacterCreationQualitiesPreview Preview);

/// <summary>
/// UI draft containing stable identities and expected authority only. It deliberately carries
/// no client-supplied price, rating, type, free flag, eligibility answer or source claim.
/// </summary>
public sealed record CharacterCreationQualitiesDesktopDraft(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    long ExpectedSavedRevision,
    string ExpectedRawCharacterXmlDigest,
    string ExpectedAuxiliaryStateDigest,
    string ExpectedAuthorityDigest,
    string ExpectedRuntimeDigest,
    IReadOnlyList<string> SelectedOptionIds);

public sealed record CharacterCreationQualitiesReview(
    CharacterCreationQualitiesDesktopDraft Draft,
    CharacterCreationQualitiesPreview Preview);

public sealed record CharacterCreationQualitiesAtomicCommitResult(
    CharacterCreationQualitiesDraftPlan Plan,
    CharacterCreationQualitiesDraftReceipt Receipt,
    string ObservedDraftDigest,
    CharacterCreationQualitiesAuthoritySnapshot PersistedSnapshot);

public sealed record CharacterCreationQualitiesConfirmation(
    CharacterCreationQualitiesDraftReceipt Receipt,
    CharacterCreationQualitiesEditorState PersistedState);

/// <summary>
/// Renderer-neutral SR5 Priority creation-quality workflow. Presentation projects Core facts,
/// captures stable option identities, and revalidates them immediately before and after an
/// atomic draft commit. All rules and calculations remain in Chummer Core.
/// </summary>
public static class CharacterCreationQualitiesWorkflow
{
    private const int MaximumCatalogOptions = 65_536;
    private const int MaximumPersistedReceipts = 16_384;

    public static CharacterCreationQualitiesEditorState Project(
        CharacterCreationQualitiesAuthoritySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshotShape(snapshot);
        CharacterCreationQualitiesPreview preview =
            CharacterCreationQualitiesRules.Evaluate(snapshot.Input);
        CharacterCreationQualitiesDesktopOption[] options = snapshot.Input.Authority.Options
            .OrderBy(static option => option.Name, StringComparer.Ordinal)
            .ThenBy(static option => option.Rating)
            .ThenBy(static option => option.OptionId, StringComparer.Ordinal)
            .Select(static option => new CharacterCreationQualitiesDesktopOption(
                option.OptionId,
                option.SourceId,
                option.SelectionKey,
                option.Name,
                option.Type,
                option.Rating,
                option.KarmaCost,
                option.IsMetagenic,
                option.IsFreeOrGranted,
                option.IsSelectable,
                option.DisableReasonKey,
                option.FollowUpChoiceId,
                option.FollowUpChoiceLabel,
                option.SourceAnchorIds))
            .ToArray();
        return new CharacterCreationQualitiesEditorState(
            snapshot.Input.Binding.WorkspaceId,
            snapshot.Input.Binding.ContentRevision,
            snapshot.Input.Binding.SavedRevision,
            snapshot.Input.Authority.AuthorityDigest,
            snapshot.Input.Authority.RuntimeDigest,
            options,
            preview.Selections.Select(static item => item.OptionId).ToArray(),
            preview);
    }

    public static CharacterCreationQualitiesDesktopDraft CreateDraft(
        CharacterCreationQualitiesEditorState state,
        IReadOnlyList<string> selectedOptionIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        selectedOptionIds ??= [];
        if (selectedOptionIds.Count > state.Options.Count
            || selectedOptionIds.Any(string.IsNullOrWhiteSpace)
            || selectedOptionIds.Distinct(StringComparer.Ordinal).Count() != selectedOptionIds.Count)
        {
            throw new InvalidOperationException("Quality selection identities are invalid or duplicated.");
        }
        HashSet<string> projectedIds = state.Options.Select(static option => option.OptionId)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedOptionIds.Any(optionId => !projectedIds.Contains(optionId)))
            throw new InvalidOperationException("A selected quality is not part of the current Core projection.");

        return new CharacterCreationQualitiesDesktopDraft(
            state.WorkspaceId,
            state.ContentRevision,
            state.SavedRevision,
            state.Preview.Binding.RawCharacterXmlDigest,
            state.Preview.Binding.AuxiliaryStateDigest,
            state.AuthorityDigest,
            state.RuntimeDigest,
            selectedOptionIds.OrderBy(static item => item, StringComparer.Ordinal).ToArray());
    }

    public static CharacterCreationQualitiesReview Review(
        CharacterCreationQualitiesAuthoritySnapshot currentSnapshot,
        CharacterCreationQualitiesDesktopDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ValidateSnapshotShape(currentSnapshot);
        CharacterCreationQualitiesBinding binding = currentSnapshot.Input.Binding;
        if (binding.WorkspaceId != draft.WorkspaceId
            || binding.ContentRevision != draft.ExpectedContentRevision
            || binding.SavedRevision != draft.ExpectedSavedRevision
            || !FixedEquals(binding.RawCharacterXmlDigest, draft.ExpectedRawCharacterXmlDigest)
            || !FixedEquals(binding.AuxiliaryStateDigest, draft.ExpectedAuxiliaryStateDigest)
            || !FixedEquals(currentSnapshot.Input.Authority.AuthorityDigest, draft.ExpectedAuthorityDigest)
            || !FixedEquals(currentSnapshot.Input.Authority.RuntimeDigest, draft.ExpectedRuntimeDigest))
        {
            throw new InvalidOperationException(
                "Workspace, source, runtime or creation-quality authority changed before review.");
        }

        CharacterCreationQualitiesPreview preview = CharacterCreationQualitiesRules.Evaluate(
            currentSnapshot.Input with { SelectedOptionIds = draft.SelectedOptionIds });
        return new CharacterCreationQualitiesReview(draft, preview);
    }

    public static CharacterCreationQualitiesDraftPlan PlanConfirmation(
        CharacterCreationQualitiesAuthoritySnapshot currentSnapshot,
        CharacterCreationQualitiesReview review,
        string idempotencyKey,
        bool explicitlyConfirmed,
        Guid transactionId)
    {
        ArgumentNullException.ThrowIfNull(review);
        CharacterCreationQualitiesReview refreshed = Review(currentSnapshot, review.Draft);
        if (!FixedEquals(refreshed.Preview.PreviewDigest, review.Preview.PreviewDigest))
            throw new InvalidOperationException("The reviewed quality choices changed before confirmation.");
        bool transactionExists = currentSnapshot.ReservedTransactionIds.Contains(transactionId)
            || currentSnapshot.PersistedReceipts.Any(receipt => receipt.TransactionId == transactionId);
        if (!CharacterCreationQualitiesRules.TryPlan(
                refreshed.Preview,
                review.Preview.PreviewDigest,
                idempotencyKey,
                explicitlyConfirmed,
                transactionExists,
                transactionId,
                out CharacterCreationQualitiesDraftPlan plan))
        {
            throw new InvalidOperationException(
                "Quality confirmation requires an unchanged, legal Core review, explicit consent and a fresh transaction identity.");
        }
        return plan;
    }

    public static CharacterCreationQualitiesConfirmation ValidateAtomicCommit(
        CharacterCreationQualitiesReview review,
        CharacterCreationQualitiesDraftPlan expectedPlan,
        CharacterCreationQualitiesAtomicCommitResult committed)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(expectedPlan);
        ArgumentNullException.ThrowIfNull(committed);
        string[] reviewedIds = review.Preview.Selections.Select(static item => item.OptionId)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        string[] plannedIds = expectedPlan.Selections.Select(static item => item.OptionId)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        if (!FixedEquals(review.Preview.PreviewDigest, expectedPlan.PreviewDigest)
            || !reviewedIds.SequenceEqual(plannedIds, StringComparer.Ordinal)
            || !FixedEquals(committed.Plan.PlanDigest, expectedPlan.PlanDigest)
            || committed.Plan.TransactionId != expectedPlan.TransactionId
            || !CharacterCreationQualitiesRules.IsValidReceipt(
                committed.Receipt,
                expectedPlan,
                committed.ObservedDraftDigest))
        {
            throw new InvalidOperationException("The creation-quality commit receipt is invalid.");
        }

        CharacterCreationQualitiesEditorState persisted = Project(committed.PersistedSnapshot);
        string[] expectedIds = expectedPlan.Selections.Select(static item => item.OptionId)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        string[] observedIds = persisted.SelectedOptionIds.OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        if (persisted.ContentRevision != expectedPlan.TargetContentRevision
            || persisted.SavedRevision != expectedPlan.TargetSavedRevision
            || !FixedEquals(persisted.AuthorityDigest, expectedPlan.AuthorityDigest)
            || !FixedEquals(persisted.RuntimeDigest, expectedPlan.RuntimeDigest)
            || !expectedIds.SequenceEqual(observedIds, StringComparer.Ordinal)
            || !committed.PersistedSnapshot.PersistedReceipts.Any(receipt =>
                receipt.TransactionId == expectedPlan.TransactionId
                && FixedEquals(receipt.ReceiptDigest, committed.Receipt.ReceiptDigest)))
        {
            throw new InvalidOperationException(
                "The persisted creation-quality draft does not match the confirmed Core plan.");
        }
        return new CharacterCreationQualitiesConfirmation(committed.Receipt, persisted);
    }

    private static void ValidateSnapshotShape(CharacterCreationQualitiesAuthoritySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Input);
        ArgumentNullException.ThrowIfNull(snapshot.Input.Binding);
        ArgumentNullException.ThrowIfNull(snapshot.Input.Authority);
        if (snapshot.Input.Authority.Options.Count > MaximumCatalogOptions
            || snapshot.PersistedReceipts.Count > MaximumPersistedReceipts
            || snapshot.ReservedTransactionIds.Count > MaximumPersistedReceipts
            || snapshot.ReservedTransactionIds.Contains(Guid.Empty)
            || snapshot.ReservedTransactionIds.Distinct().Count() != snapshot.ReservedTransactionIds.Count
            || snapshot.PersistedReceipts.Any(static receipt => receipt is null))
        {
            throw new InvalidOperationException("Creation-quality authority snapshot is malformed.");
        }
    }

    private static bool FixedEquals(string? left, string? right)
        => CharacterCreationQualitiesRules.DigestsEqual(left, right);
}
