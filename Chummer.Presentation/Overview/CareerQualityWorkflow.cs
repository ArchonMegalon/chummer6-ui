using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Trusted persistence boundary for the SR5 Career quality workflow. Implementations must
/// derive candidates from the exact enabled source/custom-data/GM profile and must commit a
/// Core plan, its character/effect deltas, expense and receipt in one workspace transaction.
/// Presentation never accepts rule, identity, cost or effect authority from a UI request.
/// </summary>
public interface ICareerQualityAtomicWorkspace
{
    Task<CareerQualityAuthoritySnapshot?> ReadAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<CareerQualityAtomicCommitResult?> CommitAsync(
        CharacterCareerQualityPlan plan,
        CancellationToken ct);

    Task<CareerQualityAtomicCorrectionResult?> CorrectAsync(
        CharacterCareerQualityCorrectionPlan correction,
        CancellationToken ct);
}

public sealed record CareerQualityPersistedReceiptProjection(
    CharacterCareerQualityReceipt Receipt,
    CharacterCareerQualityStateObservation ObservedPostState,
    CharacterCareerQualityExpenseObservation ObservedExpense);

public sealed record CareerQualityPersistedCorrectionProjection(
    CharacterCareerQualityCorrectionPlan Correction,
    CharacterCareerQualityReceipt OriginalReceipt,
    CharacterCareerQualityStateObservation ObservedRestoredState,
    CharacterCareerQualityExpenseObservation ObservedExpense);

public sealed record CareerQualityAuthoritySnapshot(
    string RulesetId,
    CharacterCareerQualityExecutionBinding Binding,
    IReadOnlyList<CharacterCareerQualityInput> Candidates,
    IReadOnlyList<CareerQualityPersistedReceiptProjection> PersistedReceipts,
    IReadOnlyList<CareerQualityPersistedCorrectionProjection> PersistedCorrections,
    IReadOnlyList<Guid> ReservedTransactionIds);

public sealed record CareerQualityEditorState(
    CharacterWorkspaceId WorkspaceId,
    long WorkspaceRevision,
    long SavedRevision,
    string RulesetId,
    string OwnerId,
    string RuntimeFingerprint,
    string ContentDigest,
    IReadOnlyList<CharacterCareerQualityQuote> Quotes,
    int OmittedCandidateCount,
    IReadOnlyList<CharacterCareerQualityReceipt> RecoverableReceipts,
    int OmittedReceiptCount);

/// <summary>
/// Stable, non-label selection captured from one authority projection.
/// </summary>
public sealed record CareerQualityDraft(
    CharacterWorkspaceId WorkspaceId,
    string ExpectedOwnerId,
    long ExpectedWorkspaceRevision,
    long ExpectedSavedRevision,
    string ExpectedRulesetId,
    CharacterCareerQualityOperation Operation,
    CharacterCareerQualityIdentity Identity,
    string ExpectedLogicalRevision,
    string ExpectedSourceRevision,
    string ExpectedRuleDigest,
    string ExpectedRuntimeFingerprint,
    string ExpectedContentDigest);

public sealed record CareerQualityReview(
    CareerQualityDraft Draft,
    CharacterCareerQualityQuote Quote);

public sealed record CareerQualityAtomicCommitResult(
    CharacterCareerQualityPlan Plan,
    CharacterCareerQualityReceipt Receipt,
    CharacterCareerQualityStateObservation ObservedPostState,
    CharacterCareerQualityExpenseObservation ObservedExpense,
    CareerQualityAuthoritySnapshot PersistedSnapshot);

public sealed record CareerQualityAtomicCorrectionResult(
    CharacterCareerQualityCorrectionPlan Correction,
    CharacterCareerQualityStateObservation ObservedRestoredState,
    CharacterCareerQualityExpenseObservation ObservedExpense,
    CareerQualityAuthoritySnapshot PersistedSnapshot);

public sealed record CareerQualityConfirmation(
    CharacterCareerQualityReceipt Receipt,
    CareerQualityEditorState PersistedState);

public sealed record CareerQualityCorrectionConfirmation(
    CharacterCareerQualityCorrectionPlan Correction,
    CareerQualityEditorState PersistedState);

public sealed record CareerQualityCorrectionRequest(
    CharacterWorkspaceId WorkspaceId,
    string ExpectedOwnerId,
    long ExpectedWorkspaceRevision,
    long ExpectedSavedRevision,
    string ExpectedRulesetId,
    CharacterCareerQualityReceipt OriginalReceipt,
    string ExpectedReceiptDigest,
    bool Confirmed,
    Guid CorrectionId,
    string Reason);

public static class CareerQualityWorkflow
{
    private const int MaximumCandidates = 16_384;
    private const int MaximumPersistedTransactions = 16_384;

    public static CareerQualityEditorState Project(
        CareerQualityAuthoritySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshotEnvelope(snapshot);

        var quotes = new List<CharacterCareerQualityQuote>(snapshot.Candidates.Count);
        int omittedCandidates = snapshot.Candidates.Count(static input => input is null);
        foreach (IGrouping<CandidateKey, CharacterCareerQualityInput> group in
                 snapshot.Candidates
                     .Where(static input => input is not null)
                     .GroupBy(input => new CandidateKey(input.Operation, input.Identity)))
        {
            if (group.Count() != 1)
            {
                omittedCandidates += group.Count();
                continue;
            }
            CharacterCareerQualityInput input = group.Single();
            if (input.Binding != snapshot.Binding
                || !string.Equals(input.RulesetId, snapshot.RulesetId, StringComparison.Ordinal)
                || !CharacterCareerQualityRules.TryCreateQuote(input, out CharacterCareerQualityQuote quote)
                || !CharacterCareerQualityRules.IsCoherent(quote))
            {
                omittedCandidates++;
                continue;
            }
            quotes.Add(quote);
        }

        ReceiptProjection recovery = Recover(snapshot);
        return new CareerQualityEditorState(
            new CharacterWorkspaceId(snapshot.Binding.WorkspaceId),
            snapshot.Binding.WorkspaceRevision,
            snapshot.Binding.SavedRevision,
            snapshot.RulesetId,
            snapshot.Binding.OwnerId,
            snapshot.Binding.RuntimeFingerprint,
            snapshot.Binding.ContentDigest,
            quotes
                .OrderBy(static quote => quote.Definition.Name, StringComparer.Ordinal)
                .ThenBy(static quote => quote.Definition.SourceId)
                .ThenBy(static quote => quote.Operation)
                .ThenBy(static quote => quote.Identity.InternalId)
                .ToArray(),
            omittedCandidates,
            recovery.Receipts,
            recovery.OmittedCount);
    }

    public static CareerQualityDraft CreateDraft(
        CareerQualityEditorState state,
        CharacterCareerQualityQuote selected)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selected);
        CharacterCareerQualityQuote current = ResolveQuote(
            state.Quotes,
            selected.Operation,
            selected.Identity,
            "The selected quality is not part of this authority projection.");
        if (!QuoteMatches(current, selected))
        {
            throw new InvalidOperationException(
                "The selected quality does not match the current Core projection.");
        }

        return new CareerQualityDraft(
            state.WorkspaceId,
            state.OwnerId,
            state.WorkspaceRevision,
            state.SavedRevision,
            state.RulesetId,
            current.Operation,
            current.Identity,
            current.LogicalRevision,
            current.SourceRevision,
            current.RuleDigest,
            current.Binding.RuntimeFingerprint,
            current.Binding.ContentDigest);
    }

    public static CareerQualityReview Review(
        CareerQualityAuthoritySnapshot currentSnapshot,
        CareerQualityDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CareerQualityEditorState state = Project(currentSnapshot);
        ValidateDraftBinding(state, draft);
        CharacterCareerQualityQuote current = ResolveQuote(
            state.Quotes,
            draft.Operation,
            draft.Identity,
            "The selected quality changed or disappeared while review was open.");
        if (!DraftMatchesQuote(draft, current))
        {
            throw new InvalidOperationException(
                "The quality source, rules, runtime or character state changed before review.");
        }
        return new CareerQualityReview(draft, current);
    }

    public static CharacterCareerQualityPlan PlanConfirmation(
        CareerQualityAuthoritySnapshot currentSnapshot,
        CareerQualityReview review,
        bool confirmed,
        Guid transactionId,
        DateTime expenseDateLocal)
    {
        ArgumentNullException.ThrowIfNull(review);
        CareerQualityReview refreshed = Review(currentSnapshot, review.Draft);
        if (!QuoteMatches(refreshed.Quote, review.Quote))
        {
            throw new InvalidOperationException(
                "The reviewed quality changed before confirmation.");
        }

        bool transactionExists = TransactionIdExists(currentSnapshot, transactionId);
        CharacterCareerQualityQuote quote = refreshed.Quote;
        if (!CharacterCareerQualityRules.TryPlan(
                quote,
                review.Draft.ExpectedLogicalRevision,
                review.Draft.ExpectedSourceRevision,
                review.Draft.ExpectedRuleDigest,
                review.Draft.ExpectedRuntimeFingerprint,
                review.Draft.ExpectedContentDigest,
                review.Draft.ExpectedWorkspaceRevision,
                review.Draft.ExpectedSavedRevision,
                confirmed,
                transactionExists,
                transactionId,
                expenseDateLocal,
                out CharacterCareerQualityPlan plan)
            || !CharacterCareerQualityRules.IsCoherent(plan))
        {
            throw new InvalidOperationException(
                "Quality confirmation requires explicit consent, an unchanged exact Core review, and a fresh transaction identity.");
        }
        return plan;
    }

    public static CareerQualityConfirmation ValidateAtomicCommit(
        CareerQualityReview review,
        CharacterCareerQualityPlan requestedPlan,
        CareerQualityAtomicCommitResult committed)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(requestedPlan);
        ArgumentNullException.ThrowIfNull(committed);
        if (!PlanMatches(requestedPlan, committed.Plan)
            || !CharacterCareerQualityRules.TryCreateReceipt(
                requestedPlan.TransactionId,
                review.Quote,
                requestedPlan,
                committed.ObservedPostState,
                committed.ObservedExpense,
                out CharacterCareerQualityReceipt expectedReceipt)
            || !ReceiptMatches(expectedReceipt, committed.Receipt)
            || !CharacterCareerQualityRules.TryRecoverReceipt(
                committed.Receipt,
                committed.Receipt.TransactionId,
                committed.ObservedPostState,
                committed.ObservedExpense,
                committed.Receipt.ReceiptDigest,
                out _))
        {
            throw new InvalidOperationException(
                "The atomic quality commit did not return the exact Core plan, state, expense and receipt.");
        }

        CareerQualityEditorState persisted = Project(committed.PersistedSnapshot);
        CharacterCareerQualityReceipt[] matches = persisted.RecoverableReceipts
            .Where(receipt => receipt.TransactionId == expectedReceipt.TransactionId)
            .Take(2)
            .ToArray();
        if (persisted.OmittedReceiptCount != 0
            || matches.Length != 1
            || !ReceiptMatches(expectedReceipt, matches[0])
            || !string.Equals(persisted.OwnerId, requestedPlan.OwnerId, StringComparison.Ordinal)
            || !string.Equals(
                persisted.WorkspaceId.Value,
                requestedPlan.WorkspaceId,
                StringComparison.Ordinal)
            || persisted.WorkspaceRevision != requestedPlan.TargetWorkspaceRevision
            || persisted.SavedRevision != requestedPlan.TargetSavedRevision
            || !string.Equals(
                persisted.RuntimeFingerprint,
                requestedPlan.ExpectedRuntimeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                persisted.ContentDigest,
                requestedPlan.ExpectedContentDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The quality receipt is not durably recoverable from the exact target revision.");
        }
        return new CareerQualityConfirmation(expectedReceipt, persisted);
    }

    public static CharacterCareerQualityCorrectionPlan PlanCorrection(
        CareerQualityAuthoritySnapshot currentSnapshot,
        CareerQualityCorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CareerQualityEditorState state = Project(currentSnapshot);
        ValidateCorrectionBinding(state, request);
        if (!request.Confirmed)
        {
            throw new InvalidOperationException(
                "A compensating quality correction requires explicit confirmation.");
        }

        CareerQualityPersistedReceiptProjection persisted = ResolvePersistedReceipt(
            currentSnapshot,
            request.OriginalReceipt.TransactionId);
        if (!ReceiptMatches(persisted.Receipt, request.OriginalReceipt)
            || !CharacterCareerQualityRules.TryRecoverReceipt(
                persisted.Receipt,
                request.OriginalReceipt.TransactionId,
                persisted.ObservedPostState,
                persisted.ObservedExpense,
                request.ExpectedReceiptDigest,
                out CharacterCareerQualityReceipt recovered))
        {
            throw new InvalidOperationException(
                "The quality correction no longer matches its exact persisted receipt and state.");
        }

        bool correctionIdAlreadyExists = TransactionIdExists(
            currentSnapshot,
            request.CorrectionId);
        bool originalAlreadyCorrected = currentSnapshot.PersistedCorrections.Any(
            candidate => candidate.Correction.OriginalTransactionId
                == recovered.TransactionId);
        if (!CharacterCareerQualityRules.TryPlanCorrection(
                recovered,
                persisted.ObservedPostState,
                persisted.ObservedExpense,
                request.CorrectionId,
                request.Reason,
                correctionIdAlreadyExists,
                originalAlreadyCorrected,
                request.ExpectedReceiptDigest,
                out CharacterCareerQualityCorrectionPlan correction)
            || !CharacterCareerQualityRules.IsCoherent(correction))
        {
            throw new InvalidOperationException(
                "The quality correction is stale, foreign, duplicated or no longer matches its exact Core receipt.");
        }
        return correction;
    }

    public static CareerQualityCorrectionConfirmation ValidateAtomicCorrection(
        CharacterCareerQualityReceipt originalReceipt,
        CharacterCareerQualityCorrectionPlan requestedCorrection,
        CareerQualityAtomicCorrectionResult committed)
    {
        ArgumentNullException.ThrowIfNull(originalReceipt);
        ArgumentNullException.ThrowIfNull(requestedCorrection);
        ArgumentNullException.ThrowIfNull(committed);
        if (!CorrectionMatches(requestedCorrection, committed.Correction)
            || !RestoredStateMatches(
                originalReceipt,
                requestedCorrection,
                committed.ObservedRestoredState,
                committed.ObservedExpense))
        {
            throw new InvalidOperationException(
                "The atomic quality correction did not exactly restore its bound state and expense.");
        }

        CareerQualityEditorState persisted = Project(committed.PersistedSnapshot);
        CareerQualityPersistedCorrectionProjection[] matches =
            committed.PersistedSnapshot.PersistedCorrections
                .Where(candidate => candidate.Correction.CorrectionId
                    == requestedCorrection.CorrectionId)
                .Take(2)
                .ToArray();
        if (persisted.OmittedReceiptCount != 0
            || persisted.RecoverableReceipts.Any(receipt =>
                receipt.TransactionId == originalReceipt.TransactionId)
            || matches.Length != 1
            || !CorrectionMatches(matches[0].Correction, requestedCorrection)
            || !ReceiptMatches(matches[0].OriginalReceipt, originalReceipt)
            || !RestoredStateMatches(
                originalReceipt,
                requestedCorrection,
                matches[0].ObservedRestoredState,
                matches[0].ObservedExpense)
            || !string.Equals(
                persisted.OwnerId,
                requestedCorrection.OwnerId,
                StringComparison.Ordinal)
            || !string.Equals(
                persisted.WorkspaceId.Value,
                requestedCorrection.WorkspaceId,
                StringComparison.Ordinal)
            || persisted.WorkspaceRevision != requestedCorrection.TargetWorkspaceRevision
            || persisted.SavedRevision != requestedCorrection.TargetSavedRevision
            || !string.Equals(
                persisted.RuntimeFingerprint,
                requestedCorrection.ExpectedRuntimeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                persisted.ContentDigest,
                requestedCorrection.ExpectedContentDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The compensating quality correction is not durably recoverable from the exact target revision.");
        }
        return new CareerQualityCorrectionConfirmation(requestedCorrection, persisted);
    }

    private static ReceiptProjection Recover(CareerQualityAuthoritySnapshot snapshot)
    {
        var recovered = new List<CharacterCareerQualityReceipt>();
        int omitted = 0;
        Dictionary<Guid, CareerQualityPersistedCorrectionProjection[]> corrections =
            snapshot.PersistedCorrections
                .Where(static value => value is not null)
                .GroupBy(value => value.Correction.OriginalTransactionId)
                .ToDictionary(group => group.Key, group => group.Take(2).ToArray());

        foreach (CareerQualityPersistedReceiptProjection? persisted in
                 snapshot.PersistedReceipts)
        {
            if (persisted is null
                || !CharacterCareerQualityRules.IsCoherent(persisted.Receipt))
            {
                omitted++;
                continue;
            }

            if (corrections.TryGetValue(
                    persisted.Receipt.TransactionId,
                    out CareerQualityPersistedCorrectionProjection[]? matches))
            {
                if (matches.Length != 1
                    || !CorrectionProjectionMatches(matches[0], persisted.Receipt)
                    || !TransactionBelongsToSnapshot(
                        snapshot.Binding,
                        matches[0].Correction.OwnerId,
                        matches[0].Correction.WorkspaceId,
                        matches[0].Correction.TargetWorkspaceRevision,
                        matches[0].Correction.TargetSavedRevision,
                        matches[0].Correction.ExpectedRuntimeFingerprint,
                        matches[0].Correction.ExpectedContentDigest))
                {
                    omitted++;
                }
                continue;
            }

            if (!CharacterCareerQualityRules.TryRecoverReceipt(
                    persisted.Receipt,
                    persisted.Receipt.TransactionId,
                    persisted.ObservedPostState,
                    persisted.ObservedExpense,
                    persisted.Receipt.ReceiptDigest,
                    out CharacterCareerQualityReceipt receipt))
            {
                omitted++;
                continue;
            }
            if (!TransactionBelongsToSnapshot(
                    snapshot.Binding,
                    receipt.OwnerId,
                    receipt.WorkspaceId,
                    receipt.WorkspaceRevisionAfter,
                    receipt.SavedRevisionAfter,
                    receipt.RuntimeFingerprint,
                    receipt.ContentDigest))
            {
                omitted++;
                continue;
            }
            if (receipt.WorkspaceRevisionAfter == snapshot.Binding.WorkspaceRevision
                && receipt.SavedRevisionAfter == snapshot.Binding.SavedRevision)
            {
                recovered.Add(receipt);
            }
        }

        foreach (CareerQualityPersistedCorrectionProjection? correction in
                 snapshot.PersistedCorrections)
        {
            if (correction is null
                || !snapshot.PersistedReceipts.Any(receipt => receipt is not null
                    && receipt.Receipt.TransactionId
                        == correction.Correction.OriginalTransactionId))
            {
                omitted++;
            }
        }

        return new ReceiptProjection(recovered, omitted);
    }

    private static bool CorrectionProjectionMatches(
        CareerQualityPersistedCorrectionProjection projection,
        CharacterCareerQualityReceipt receipt)
        => CharacterCareerQualityRules.IsCoherent(projection.Correction)
           && ReceiptMatches(projection.OriginalReceipt, receipt)
           && projection.Correction.OriginalTransactionId == receipt.TransactionId
           && string.Equals(
               projection.Correction.OriginalReceiptDigest,
               receipt.ReceiptDigest,
               StringComparison.Ordinal)
           && RestoredStateMatches(
               receipt,
               projection.Correction,
               projection.ObservedRestoredState,
               projection.ObservedExpense);

    private static bool RestoredStateMatches(
        CharacterCareerQualityReceipt receipt,
        CharacterCareerQualityCorrectionPlan correction,
        CharacterCareerQualityStateObservation state,
        CharacterCareerQualityExpenseObservation expense)
        => CharacterCareerQualityRules.IsCoherent(state)
           && state.Identity == receipt.Identity
           && state.Definition == receipt.Definition
           && string.Equals(state.Extra, receipt.Extra, StringComparison.Ordinal)
           && string.Equals(state.SourceName, receipt.SourceName, StringComparison.Ordinal)
           && state.Instances.SequenceEqual(receipt.InstancesBefore)
           && state.AvailableKarma == receipt.CharacterKarmaBefore
           && state.Binding.WorkspaceRevision == correction.TargetWorkspaceRevision
           && state.Binding.SavedRevision == correction.TargetSavedRevision
           && string.Equals(state.Binding.OwnerId, correction.OwnerId, StringComparison.Ordinal)
           && string.Equals(state.Binding.WorkspaceId, correction.WorkspaceId, StringComparison.Ordinal)
           && string.Equals(
               state.Binding.RuntimeFingerprint,
               correction.ExpectedRuntimeFingerprint,
               StringComparison.Ordinal)
           && string.Equals(
               state.Binding.ContentDigest,
               correction.ExpectedContentDigest,
               StringComparison.Ordinal)
           && string.Equals(
               state.SourceRevision,
               correction.ExpectedSourceRevision,
               StringComparison.Ordinal)
           && string.Equals(
               state.RuleDigest,
               correction.ExpectedRuleDigest,
               StringComparison.Ordinal)
           && IsNoExpense(expense);

    private static void ValidateSnapshotEnvelope(CareerQualityAuthoritySnapshot snapshot)
    {
        CharacterCareerQualityExecutionBinding? binding = snapshot.Binding;
        if (!string.Equals(
                snapshot.RulesetId,
                CharacterCareerQualityRules.RulesetId,
                StringComparison.Ordinal)
            || binding is null
            || string.IsNullOrWhiteSpace(binding.OwnerId)
            || string.IsNullOrWhiteSpace(binding.WorkspaceId)
            || binding.WorkspaceRevision <= 0
            || binding.SavedRevision < 0
            || !IsRevision(binding.RuntimeFingerprint)
            || !IsRevision(binding.ContentDigest)
            || snapshot.Candidates is null
            || snapshot.PersistedReceipts is null
            || snapshot.PersistedCorrections is null
            || snapshot.ReservedTransactionIds is null
            || snapshot.Candidates.Count > MaximumCandidates
            || snapshot.PersistedReceipts.Count > MaximumPersistedTransactions
            || snapshot.PersistedCorrections.Count > MaximumPersistedTransactions
            || snapshot.ReservedTransactionIds.Count > MaximumPersistedTransactions
            || snapshot.PersistedReceipts.Any(static value => value is null
                || value.Receipt is null
                || value.ObservedPostState is null
                || value.ObservedExpense is null)
            || snapshot.PersistedCorrections.Any(static value => value is null
                || value.Correction is null
                || value.OriginalReceipt is null
                || value.ObservedRestoredState is null
                || value.ObservedExpense is null))
        {
            throw new InvalidOperationException(
                "The SR5 quality authority snapshot is absent, unbound or outside safe limits.");
        }

        Guid[] transactionIds = snapshot.PersistedReceipts
            .Where(static value => value is not null)
            .Select(value => value.Receipt.TransactionId)
            .ToArray();
        Guid[] correctionIds = snapshot.PersistedCorrections
            .Where(static value => value is not null)
            .Select(value => value.Correction.CorrectionId)
            .ToArray();
        Guid[] reservedIds = snapshot.ReservedTransactionIds.ToArray();
        if (transactionIds.Any(id => id == Guid.Empty)
            || correctionIds.Any(id => id == Guid.Empty)
            || reservedIds.Any(id => id == Guid.Empty)
            || transactionIds.Distinct().Count() != transactionIds.Length
            || correctionIds.Distinct().Count() != correctionIds.Length
            || reservedIds.Distinct().Count() != reservedIds.Length
            || transactionIds.Intersect(correctionIds).Any()
            || transactionIds.Intersect(reservedIds).Any()
            || correctionIds.Intersect(reservedIds).Any())
        {
            throw new InvalidOperationException(
                "The quality authority snapshot contains ambiguous transaction identities.");
        }
    }

    private static void ValidateDraftBinding(
        CareerQualityEditorState state,
        CareerQualityDraft draft)
    {
        if (state.WorkspaceId != draft.WorkspaceId
            || !string.Equals(state.OwnerId, draft.ExpectedOwnerId, StringComparison.Ordinal)
            || state.WorkspaceRevision != draft.ExpectedWorkspaceRevision
            || state.SavedRevision != draft.ExpectedSavedRevision
            || !string.Equals(state.RulesetId, draft.ExpectedRulesetId, StringComparison.Ordinal)
            || !string.Equals(
                state.RuntimeFingerprint,
                draft.ExpectedRuntimeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                state.ContentDigest,
                draft.ExpectedContentDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The quality draft belongs to a different owner, workspace, revision, runtime or content profile.");
        }
    }

    private static void ValidateCorrectionBinding(
        CareerQualityEditorState state,
        CareerQualityCorrectionRequest request)
    {
        if (state.WorkspaceId != request.WorkspaceId
            || !string.Equals(state.OwnerId, request.ExpectedOwnerId, StringComparison.Ordinal)
            || state.WorkspaceRevision != request.ExpectedWorkspaceRevision
            || state.SavedRevision != request.ExpectedSavedRevision
            || !string.Equals(state.RulesetId, request.ExpectedRulesetId, StringComparison.Ordinal)
            || !state.RecoverableReceipts.Any(receipt =>
                ReceiptMatches(receipt, request.OriginalReceipt)))
        {
            throw new InvalidOperationException(
                "The quality correction belongs to a stale or foreign workspace authority.");
        }
    }

    private static CharacterCareerQualityQuote ResolveQuote(
        IReadOnlyList<CharacterCareerQualityQuote> values,
        CharacterCareerQualityOperation operation,
        CharacterCareerQualityIdentity identity,
        string error)
    {
        CharacterCareerQualityQuote[] matches = values
            .Where(candidate => candidate.Operation == operation
                && candidate.Identity == identity)
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(error);
        return matches[0];
    }

    private static CareerQualityPersistedReceiptProjection ResolvePersistedReceipt(
        CareerQualityAuthoritySnapshot snapshot,
        Guid transactionId)
    {
        CareerQualityPersistedReceiptProjection[] matches = snapshot.PersistedReceipts
            .Where(candidate => candidate.Receipt.TransactionId == transactionId)
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "The quality receipt is absent or ambiguous in durable authority.");
        }
        return matches[0];
    }

    private static bool DraftMatchesQuote(
        CareerQualityDraft draft,
        CharacterCareerQualityQuote quote)
        => draft.Operation == quote.Operation
           && draft.Identity == quote.Identity
           && string.Equals(draft.ExpectedLogicalRevision, quote.LogicalRevision, StringComparison.Ordinal)
           && string.Equals(draft.ExpectedSourceRevision, quote.SourceRevision, StringComparison.Ordinal)
           && string.Equals(draft.ExpectedRuleDigest, quote.RuleDigest, StringComparison.Ordinal)
           && string.Equals(draft.ExpectedRuntimeFingerprint, quote.Binding.RuntimeFingerprint, StringComparison.Ordinal)
           && string.Equals(draft.ExpectedContentDigest, quote.Binding.ContentDigest, StringComparison.Ordinal)
           && draft.ExpectedWorkspaceRevision == quote.Binding.WorkspaceRevision
           && draft.ExpectedSavedRevision == quote.Binding.SavedRevision;

    private static bool QuoteMatches(
        CharacterCareerQualityQuote left,
        CharacterCareerQualityQuote right)
        => CharacterCareerQualityRules.IsCoherent(left)
           && CharacterCareerQualityRules.IsCoherent(right)
           && left.Operation == right.Operation
           && left.Identity == right.Identity
           && string.Equals(left.LogicalRevision, right.LogicalRevision, StringComparison.Ordinal)
           && string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal)
           && string.Equals(left.RuleDigest, right.RuleDigest, StringComparison.Ordinal)
           && left.Binding == right.Binding;

    private static bool PlanMatches(
        CharacterCareerQualityPlan left,
        CharacterCareerQualityPlan right)
        => CharacterCareerQualityRules.IsCoherent(left)
           && CharacterCareerQualityRules.IsCoherent(right)
           && left.TransactionId == right.TransactionId
           && left.Operation == right.Operation
           && left.Identity == right.Identity
           && left.Definition == right.Definition
           && string.Equals(left.Extra, right.Extra, StringComparison.Ordinal)
           && string.Equals(left.SourceName, right.SourceName, StringComparison.Ordinal)
           && left.InstancesBefore.SequenceEqual(right.InstancesBefore)
           && left.InstancesAfter.SequenceEqual(right.InstancesAfter)
           && left.AffectedInternalIds.SequenceEqual(right.AffectedInternalIds)
           && left.SavedCharacterKarma == right.SavedCharacterKarma
           && left.CreatesExpense == right.CreatesExpense
           && left.ExpenseId == right.ExpenseId
           && left.ExpenseDateLocal == right.ExpenseDateLocal
           && left.ExpenseAmount == right.ExpenseAmount
           && string.Equals(left.ExpenseReason, right.ExpenseReason, StringComparison.Ordinal)
           && left.ExpenseRefund == right.ExpenseRefund
           && string.Equals(left.ExpenseType, right.ExpenseType, StringComparison.Ordinal)
           && left.ForceCareerVisible == right.ForceCareerVisible
           && string.Equals(left.KarmaUndoType, right.KarmaUndoType, StringComparison.Ordinal)
           && string.Equals(left.NuyenUndoType, right.NuyenUndoType, StringComparison.Ordinal)
           && string.Equals(left.UndoObjectId, right.UndoObjectId, StringComparison.Ordinal)
           && left.UndoQuantity == right.UndoQuantity
           && string.Equals(left.UndoExtra, right.UndoExtra, StringComparison.Ordinal)
           && string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal)
           && string.Equals(left.WorkspaceId, right.WorkspaceId, StringComparison.Ordinal)
           && left.ExpectedWorkspaceRevision == right.ExpectedWorkspaceRevision
           && left.TargetWorkspaceRevision == right.TargetWorkspaceRevision
           && left.ExpectedSavedRevision == right.ExpectedSavedRevision
           && left.TargetSavedRevision == right.TargetSavedRevision
           && string.Equals(
               left.ExpectedRuntimeFingerprint,
               right.ExpectedRuntimeFingerprint,
               StringComparison.Ordinal)
           && string.Equals(
               left.ExpectedContentDigest,
               right.ExpectedContentDigest,
               StringComparison.Ordinal)
           && string.Equals(
               left.ExpectedSourceRevision,
               right.ExpectedSourceRevision,
               StringComparison.Ordinal)
           && string.Equals(
               left.ExpectedRuleDigest,
               right.ExpectedRuleDigest,
               StringComparison.Ordinal)
           && string.Equals(
               left.ExpectedLogicalRevision,
               right.ExpectedLogicalRevision,
               StringComparison.Ordinal);

    private static bool ReceiptMatches(
        CharacterCareerQualityReceipt left,
        CharacterCareerQualityReceipt right)
        => CharacterCareerQualityRules.IsCoherent(left)
           && CharacterCareerQualityRules.IsCoherent(right)
           && left.TransactionId == right.TransactionId
           && string.Equals(left.ReceiptDigest, right.ReceiptDigest, StringComparison.Ordinal);

    private static bool CorrectionMatches(
        CharacterCareerQualityCorrectionPlan left,
        CharacterCareerQualityCorrectionPlan right)
        => CharacterCareerQualityRules.IsCoherent(left)
           && CharacterCareerQualityRules.IsCoherent(right)
           && left.CorrectionId == right.CorrectionId
           && left.OriginalTransactionId == right.OriginalTransactionId
           && string.Equals(left.CorrectionDigest, right.CorrectionDigest, StringComparison.Ordinal);

    private static bool TransactionIdExists(
        CareerQualityAuthoritySnapshot snapshot,
        Guid transactionId)
        => transactionId == Guid.Empty
           || snapshot.PersistedReceipts.Any(candidate =>
               candidate.Receipt.TransactionId == transactionId)
           || snapshot.PersistedCorrections.Any(candidate =>
               candidate.Correction.CorrectionId == transactionId
               || candidate.Correction.OriginalTransactionId == transactionId)
           || snapshot.ReservedTransactionIds.Contains(transactionId);

    private static bool TransactionBelongsToSnapshot(
        CharacterCareerQualityExecutionBinding snapshot,
        string ownerId,
        string workspaceId,
        long workspaceRevision,
        long savedRevision,
        string runtimeFingerprint,
        string contentDigest)
        => string.Equals(snapshot.OwnerId, ownerId, StringComparison.Ordinal)
           && string.Equals(snapshot.WorkspaceId, workspaceId, StringComparison.Ordinal)
           && string.Equals(
               snapshot.RuntimeFingerprint,
               runtimeFingerprint,
               StringComparison.Ordinal)
           && string.Equals(snapshot.ContentDigest, contentDigest, StringComparison.Ordinal)
           && workspaceRevision > 0
           && savedRevision >= 0
           && workspaceRevision <= snapshot.WorkspaceRevision
           && savedRevision <= snapshot.SavedRevision;

    private static bool IsNoExpense(CharacterCareerQualityExpenseObservation value)
        => value is not null
           && value.MatchingEntryCount == 0
           && value.ExpenseId == Guid.Empty
           && value.ExpenseDateLocal == DateTime.MinValue
           && value.Amount == 0
           && string.IsNullOrEmpty(value.Reason)
           && string.IsNullOrEmpty(value.ExpenseType)
           && !value.Refund
           && !value.ForceCareerVisible
           && string.IsNullOrEmpty(value.KarmaUndoType)
           && string.IsNullOrEmpty(value.NuyenUndoType)
           && string.IsNullOrEmpty(value.UndoObjectId)
           && value.UndoQuantity == 0m
           && string.IsNullOrEmpty(value.UndoExtra);

    private static bool IsRevision(string? value)
        => value is { Length: CharacterCareerQualityRules.RevisionHexLength }
           && value.All(static character => character is >= '0' and <= '9'
               or >= 'a' and <= 'f');

    private readonly record struct CandidateKey(
        CharacterCareerQualityOperation Operation,
        CharacterCareerQualityIdentity Identity);

    private sealed record ReceiptProjection(
        IReadOnlyList<CharacterCareerQualityReceipt> Receipts,
        int OmittedCount);
}

/// <summary>
/// Stateful orchestration facade used by native/desktop surfaces. It re-reads authority at
/// every transition and never retries a failed atomic commit or correction.
/// </summary>
public sealed class CareerQualityInteractionPresenter
{
    private readonly ICareerQualityAtomicWorkspace _workspace;

    public CareerQualityInteractionPresenter(ICareerQualityAtomicWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task<CareerQualityEditorState> ProjectAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        CareerQualityAuthoritySnapshot snapshot = await ReadRequiredAsync(workspaceId, ct)
            .ConfigureAwait(false);
        return CareerQualityWorkflow.Project(snapshot);
    }

    public async Task<CareerQualityReview> ReviewAsync(
        CareerQualityDraft draft,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CareerQualityAuthoritySnapshot snapshot = await ReadRequiredAsync(draft.WorkspaceId, ct)
            .ConfigureAwait(false);
        return CareerQualityWorkflow.Review(snapshot, draft);
    }

    public async Task<CareerQualityConfirmation> ConfirmAsync(
        CareerQualityReview review,
        bool confirmed,
        Guid transactionId,
        DateTime expenseDateLocal,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(review);
        CareerQualityAuthoritySnapshot snapshot = await ReadRequiredAsync(
                review.Draft.WorkspaceId,
                ct)
            .ConfigureAwait(false);
        CharacterCareerQualityPlan plan = CareerQualityWorkflow.PlanConfirmation(
            snapshot,
            review,
            confirmed,
            transactionId,
            expenseDateLocal);
        CareerQualityAtomicCommitResult committed = await _workspace.CommitAsync(plan, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The quality transaction was not atomically committed.");
        CareerQualityAuthoritySnapshot durable = await ReadRequiredAsync(
                review.Draft.WorkspaceId,
                ct)
            .ConfigureAwait(false);
        return CareerQualityWorkflow.ValidateAtomicCommit(
            review,
            plan,
            committed with { PersistedSnapshot = durable });
    }

    public async Task<CareerQualityCorrectionConfirmation> CorrectAsync(
        CareerQualityCorrectionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        CareerQualityAuthoritySnapshot snapshot = await ReadRequiredAsync(
                request.WorkspaceId,
                ct)
            .ConfigureAwait(false);
        CharacterCareerQualityCorrectionPlan correction =
            CareerQualityWorkflow.PlanCorrection(snapshot, request);
        CareerQualityAtomicCorrectionResult committed = await _workspace
            .CorrectAsync(correction, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The quality correction was not atomically committed.");
        CareerQualityAuthoritySnapshot durable = await ReadRequiredAsync(
                request.WorkspaceId,
                ct)
            .ConfigureAwait(false);
        return CareerQualityWorkflow.ValidateAtomicCorrection(
            request.OriginalReceipt,
            correction,
            committed with { PersistedSnapshot = durable });
    }

    private async Task<CareerQualityAuthoritySnapshot> ReadRequiredAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
            throw new InvalidOperationException("A nonblank dossier identity is required.");
        CareerQualityAuthoritySnapshot snapshot = await _workspace
            .ReadAsync(workspaceId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Exact SR5 quality authority is unavailable for this dossier.");
        if (!string.Equals(
                snapshot.Binding.WorkspaceId,
                workspaceId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The quality authority returned a different dossier identity.");
        }
        return snapshot;
    }
}
