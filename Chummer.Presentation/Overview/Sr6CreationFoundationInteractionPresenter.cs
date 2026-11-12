using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public static class Sr6CreationFoundationInteractionBlockers
{
    public const string OverviewAuthorityRequired = "sr6-creation-foundation-overview-authority-required";
    public const string OwnerAuthorityRequired = "sr6-creation-foundation-owner-authority-required";
    public const string CharacterAlreadyCreated = "sr6-creation-foundation-character-already-created";
    public const string BindingMismatch = "sr6-creation-foundation-binding-mismatch";
    public const string PreparedPreviewMismatch = "sr6-creation-foundation-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "sr6-creation-foundation-preview-not-confirmable";
    public const string ReceiptMismatch = "sr6-creation-foundation-receipt-mismatch";
    public const string RefreshAuthorityRequired = "sr6-creation-foundation-refresh-authority-required";
}

/// <summary>
/// A renderer-neutral, authority-preserving interaction boundary for the SR6
/// creation wizard. It never derives legal choices or writes a workspace
/// itself; the Core SR6 foundation service remains the sole read, preview,
/// confirmation, and persistence authority.
/// </summary>
public interface ISr6CreationFoundationInteractionPresenter
{
    Sr6CreationFoundationInteractionLoadResult Load(CharacterOverviewState overview);

    Sr6CreationFoundationInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        Sr6CreationFoundationSelection selection);

    Sr6CreationFoundationInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        Sr6CreationFoundationInteractionConfirmation confirmation);

    /// <summary>Loads the one-way finalization review from the Core store.</summary>
    Sr6CreationFinalizationInteractionReviewResult ReviewFinalization(
        CharacterOverviewState overview,
        Sr6CreationFoundationBinding binding);

    /// <summary>Runs the one-way career transition through the Core store.</summary>
    Sr6CreationFinalizationInteractionConfirmResult ConfirmFinalization(
        CharacterOverviewState overview,
        Sr6CreationFinalizationReview review,
        bool acceptUnspentLoss);
}

public sealed record Sr6CreationFoundationInteractionState(
    Sr6CreationFoundationState Foundation,
    IReadOnlyList<string> AuthorityBlockers);

/// <summary>A preview captured against one exact foundation state revision.</summary>
public sealed record Sr6CreationFoundationPreparedPreview(
    Sr6CreationFoundationState Foundation,
    Sr6CreationFoundationPreview Preview,
    string PreviewDigest);

public sealed record Sr6CreationFoundationInteractionConfirmation(
    Sr6CreationFoundationPreparedPreview PreparedPreview,
    string PreviewDigest,
    Guid OperationId,
    bool ExplicitlyConfirmed);

public sealed record Sr6CreationFoundationInteractionLoadResult(
    string Outcome,
    Sr6CreationFoundationInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record Sr6CreationFoundationInteractionPrepareResult(
    string Outcome,
    Sr6CreationFoundationInteractionState? State,
    Sr6CreationFoundationPreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record Sr6CreationFoundationInteractionConfirmResult(
    string Outcome,
    Sr6CreationFoundationPreparedPreview? PreparedPreview,
    Sr6CreationFoundationCommit? Commit,
    Sr6CreationFoundationInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record Sr6CreationFinalizationInteractionReviewResult(
    string Outcome,
    Sr6CreationFinalizationReview? Review,
    IReadOnlyList<string> Blockers)
{
    public bool Success => string.Equals(Outcome, CharacterCreationFoundationOutcomes.Success, StringComparison.Ordinal);
}

public sealed record Sr6CreationFinalizationInteractionConfirmResult(
    string Outcome,
    Sr6CreationFinalizationCommit? Commit,
    IReadOnlyList<string> Blockers)
{
    public bool Success => string.Equals(Outcome, CharacterCreationFoundationOutcomes.Success, StringComparison.Ordinal);
}

public sealed class Sr6CreationFoundationInteractionPresenter
    : ISr6CreationFoundationInteractionPresenter
{
    private readonly ISr6CreationFoundationService _service;

    public Sr6CreationFoundationInteractionPresenter(
        ISr6CreationFoundationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public Sr6CreationFoundationInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new Sr6CreationFoundationInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public Sr6CreationFoundationInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        Sr6CreationFoundationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ExactLoad load = LoadExact(overview);
        if (load.State is not Sr6CreationFoundationState foundation)
        {
            return new Sr6CreationFoundationInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }

        CharacterCreationFoundationResult<Sr6CreationFoundationPreview> result =
            _service.Preview(load.Owner, foundation.Binding, selection);
        if (result.Value is not Sr6CreationFoundationPreview preview)
        {
            return new Sr6CreationFoundationInteractionPrepareResult(
                result.Outcome,
                Project(foundation),
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!BindingEquals(preview.Binding, foundation.Binding)
            || !string.Equals(
                Sr6CreationFoundationIntegrity.PreviewDigest(preview),
                preview.PreviewDigest,
                StringComparison.Ordinal)
            || !IsDigest(preview.PreviewDigest))
        {
            return new Sr6CreationFoundationInteractionPrepareResult(
                CharacterCreationFoundationOutcomes.Conflict,
                Project(foundation),
                null,
                [Sr6CreationFoundationInteractionBlockers.PreparedPreviewMismatch]);
        }

        Sr6CreationFoundationPreparedPreview prepared = new(foundation, preview, preview.PreviewDigest);
        return new Sr6CreationFoundationInteractionPrepareResult(
            result.Outcome,
            Project(foundation),
            prepared,
            NormalizeBlockers(result.Blockers));
    }

    public Sr6CreationFoundationInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        Sr6CreationFoundationInteractionConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        Sr6CreationFoundationPreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Invalid,
                prepared,
                Sr6CreationFoundationInteractionBlockers.PreviewNotConfirmable);
        }

        if (confirmation.OperationId == Guid.Empty
            || !string.Equals(
                confirmation.PreviewDigest,
                prepared.PreviewDigest,
                StringComparison.Ordinal)
            || !IsDigest(prepared.PreviewDigest))
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                Sr6CreationFoundationInteractionBlockers.PreparedPreviewMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not Sr6CreationFoundationState foundation)
        {
            return new Sr6CreationFoundationInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }

        if (!BindingEquals(prepared.Foundation.Binding, foundation.Binding))
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                Sr6CreationFoundationBlockers.StaleBinding);
        }

        CharacterCreationFoundationResult<Sr6CreationFoundationCommit> result =
            _service.Confirm(load.Owner, new Sr6CreationFoundationConfirmRequest(
                foundation.Binding,
                prepared.Preview.Selection,
                prepared.PreviewDigest,
                confirmation.OperationId,
                ExplicitlyConfirmed: true));
        if (result.Value is not Sr6CreationFoundationCommit commit)
        {
            return new Sr6CreationFoundationInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!CommitMatches(prepared, commit, confirmation.OperationId))
        {
            return new Sr6CreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                commit,
                null,
                [Sr6CreationFoundationInteractionBlockers.ReceiptMismatch]);
        }

        // A verified commit must survive a failing post-commit reload: the save
        // itself is durable at this point, so an unexpected reload failure is
        // reported as saved-but-not-refreshed instead of discarding the commit.
        CharacterCreationFoundationResult<Sr6CreationFoundationState> refresh;
        try
        {
            refresh = _service.Load(load.Owner, foundation.Binding.WorkspaceId);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // T492DIAG3: capture the refresh exception detail.
            try
            {
                System.IO.File.AppendAllText(
                    @"D:\sources\chummer\_scratch\t492_cm_diag.txt",
                    "T492DIAG3 refresh threw " + error.GetType().FullName
                        + ": " + error.Message
                        + " | stack=" + error.StackTrace
                        + Environment.NewLine);
            }
            catch { }
            return new Sr6CreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                commit,
                null,
                [Sr6CreationFoundationInteractionBlockers.RefreshAuthorityRequired]);
        }
        if (refresh.Outcome != CharacterCreationFoundationOutcomes.Success
            || refresh.Value is not Sr6CreationFoundationState refreshed
            || !RefreshedStateMatches(commit, refreshed))
        {
            // T492DIAG4: capture the refresh mismatch detail.
            try
            {
                System.IO.File.AppendAllText(
                    @"D:\sources\chummer\_scratch\t492_cm_diag.txt",
                    "T492DIAG4 refresh outcome=" + refresh.Outcome
                    + " blockers=" + string.Join(",", refresh.Blockers ?? new string[0])
                    + " refreshed=" + (refresh.Value as Sr6CreationFoundationState)?.Binding
                    + " commitRev=" + commit.Decision.CommittedContentRevision
                    + Environment.NewLine);
            }
            catch { }
            return new Sr6CreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                commit,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    Sr6CreationFoundationInteractionBlockers.RefreshAuthorityRequired)));
        }

            // T492DIAG2: capture the refresh failure detail.
            try
            {
                System.IO.File.AppendAllText(
                    @"D:\sources\chummer\_scratch\t492_cm_diag.txt",
                    "T492DIAG2 refresh outcome=" + refresh.Outcome
                    + " blockers=" + string.Join(",", refresh.Blockers ?? new string[0])
                    + " refreshedBinding=" + (refresh.Value as Sr6CreationFoundationState)?.Binding
                    + " commitRev=" + commit.Decision.CommittedContentRevision
                    + Environment.NewLine);
            }
            catch { }
        return new Sr6CreationFoundationInteractionConfirmResult(
            CharacterCreationFoundationOutcomes.Success,
            prepared,
            commit,
            Project(refreshed),
            NormalizeBlockers(refresh.Blockers));
    }

    /// <inheritdoc/>
    public Sr6CreationFinalizationInteractionReviewResult ReviewFinalization(
        CharacterOverviewState overview,
        Sr6CreationFoundationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(binding);
        if (overview.Profile?.Created == true)
        {
            return new Sr6CreationFinalizationInteractionReviewResult(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [Sr6CreationFoundationInteractionBlockers.CharacterAlreadyCreated]);
        }

        if (overview.DisplayOwnerContext is not { } owner || !owner.IsValid)
        {
            return new Sr6CreationFinalizationInteractionReviewResult(
                CharacterCreationFoundationOutcomes.Invalid,
                null,
                [Sr6CreationFoundationInteractionBlockers.OwnerAuthorityRequired]);
        }

        if (!string.Equals(binding.WorkspaceId.Value, WorkspaceIdOf(overview), StringComparison.Ordinal))
        {
            return new Sr6CreationFinalizationInteractionReviewResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                [Sr6CreationFoundationInteractionBlockers.BindingMismatch]);
        }

        CharacterCreationFoundationResult<Sr6CreationFinalizationReview> result =
            _service.ReviewFinalization(owner, binding);
        return new Sr6CreationFinalizationInteractionReviewResult(
            result.Outcome,
            result.Value,
            NormalizeBlockers(result.Blockers));
    }

    /// <inheritdoc/>
    public Sr6CreationFinalizationInteractionConfirmResult ConfirmFinalization(
        CharacterOverviewState overview,
        Sr6CreationFinalizationReview review,
        bool acceptUnspentLoss)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(review);
        if (overview.Profile?.Created == true)
        {
            return new Sr6CreationFinalizationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [Sr6CreationFoundationInteractionBlockers.CharacterAlreadyCreated]);
        }

        if (overview.DisplayOwnerContext is not { } owner || !owner.IsValid)
        {
            return new Sr6CreationFinalizationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Invalid,
                null,
                [Sr6CreationFoundationInteractionBlockers.OwnerAuthorityRequired]);
        }

        if (!string.Equals(WorkspaceIdOf(overview), review.Binding.WorkspaceId.Value, StringComparison.Ordinal))
        {
            return new Sr6CreationFinalizationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                [Sr6CreationFoundationInteractionBlockers.BindingMismatch]);
        }

        if (!review.CanFinalize)
        {
            return new Sr6CreationFinalizationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                review.Blockers);
        }

        CharacterCreationFoundationResult<Sr6CreationFinalizationCommit> result =
            _service.ConfirmFinalization(owner, new Sr6CreationFinalizationRequest(
                review.Binding,
                review.ReviewDigest,
                Guid.NewGuid(),
                ExplicitlyConfirmed: true,
                acceptUnspentLoss));
        return new Sr6CreationFinalizationInteractionConfirmResult(
            result.Outcome,
            result.Value,
            NormalizeBlockers(result.Blockers));
    }

    private static string WorkspaceIdOf(CharacterOverviewState overview)
        => overview.WorkspaceId?.Value ?? string.Empty;

    private ExactLoad LoadExact(CharacterOverviewState overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created == true)
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Blocked,
                Sr6CreationFoundationInteractionBlockers.CharacterAlreadyCreated);
        }

        if (overview.WorkspaceId is not { } workspaceId)
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Invalid,
                Sr6CreationFoundationInteractionBlockers.OverviewAuthorityRequired);
        }

        if (overview.DisplayOwnerContext is not { } owner || !owner.IsValid)
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Invalid,
                Sr6CreationFoundationInteractionBlockers.OwnerAuthorityRequired);
        }

        CharacterCreationFoundationResult<Sr6CreationFoundationState> result =
            _service.Load(owner, workspaceId);
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.Value is not Sr6CreationFoundationState foundation)
        {
            return new ExactLoad(result.Outcome, owner, null, NormalizeBlockers(result.Blockers));
        }

        if (!string.Equals(
                foundation.Binding.WorkspaceId.Value,
                workspaceId.Value,
                StringComparison.Ordinal))
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                Sr6CreationFoundationInteractionBlockers.BindingMismatch);
        }

        return new ExactLoad(
            CharacterCreationFoundationOutcomes.Success,
            owner,
            foundation,
            NormalizeBlockers(result.Blockers));
    }

    private static Sr6CreationFoundationInteractionState Project(
        Sr6CreationFoundationState state)
        => new(state, []);

    private static bool BindingEquals(
        Sr6CreationFoundationBinding left,
        Sr6CreationFoundationBinding right)
        => string.Equals(left.WorkspaceId.Value, right.WorkspaceId.Value, StringComparison.Ordinal)
           && left.ContentRevision == right.ContentRevision
           && left.SavedRevision == right.SavedRevision
           && string.Equals(left.AuxiliaryStateDigest, right.AuxiliaryStateDigest, StringComparison.Ordinal)
           && string.Equals(left.BootstrapBindingDigest, right.BootstrapBindingDigest, StringComparison.Ordinal)
           && string.Equals(left.AuthorityDigest, right.AuthorityDigest, StringComparison.Ordinal);

    private static bool CommitMatches(
        Sr6CreationFoundationPreparedPreview prepared,
        Sr6CreationFoundationCommit commit, Guid operationId)
    {
        Sr6CreationFoundationDecision decision = commit.Decision;
        // T492 fix: Sr6CreationFoundationSelection.Assignments is
        // IReadOnlyList<T>, so record equality compares it by reference - a
        // List<T> built by the wizard never equals the frozen T[] the
        // decision carries, even with identical content. Compare the
        // command selection and the preview by content instead of record
        // equality so a legitimate commit is not rejected as a
        // receipt mismatch.
        return string.Equals(decision.Schema, Sr6CreationFoundationDecision.SchemaV1, StringComparison.Ordinal)
               && string.Equals(
                   Sr6CreationFoundationIntegrity.DecisionDigest(decision),
                   decision.DecisionDigest,
                   StringComparison.Ordinal)
               && BindingEquals(decision.Command.Binding, prepared.Foundation.Binding)
               && string.Equals(
                   decision.Command.PreviewDigest,
                   prepared.PreviewDigest,
                   StringComparison.Ordinal)
               && SelectionEquals(decision.Command.Selection, prepared.Preview.Selection)
               && decision.Command.OperationId == operationId
               && decision.Command.ExplicitlyConfirmed
               && PreviewEquals(decision.Preview, prepared.Preview)
               && decision.CommittedContentRevision > prepared.Foundation.Binding.ContentRevision
               && IsDigest(decision.DecisionDigest);
    }

    // T492 fix: content-based selection comparison. The assignments sequence
    // is compared element-wise (order-sensitive; Core freezes it into the
    // canonical category order, and the wizard builds it in the same order),
    // and every sub-selection record uses reference-safe comparison.
    private static bool SelectionEquals(
        Sr6CreationFoundationSelection? left, Sr6CreationFoundationSelection? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(left.MetatypeId, right.MetatypeId, StringComparison.Ordinal)
               && string.Equals(left.TalentId, right.TalentId, StringComparison.Ordinal)
               && SequenceEquals(left.Assignments, right.Assignments)
               && NullableEquals(left.Attributes, right.Attributes)
               && NullableEquals(left.Skills, right.Skills)
               && NullableEquals(left.Knowledge, right.Knowledge)
               && NullableEquals(left.PointBuy, right.PointBuy)
               && NullableEquals(left.TalentAllocation, right.TalentAllocation)
               && NullableEquals(left.ComplexForms, right.ComplexForms)
               && NullableEquals(left.Spells, right.Spells)
               && NullableEquals(left.AdeptPowers, right.AdeptPowers)
               && NullableEquals(left.Karma, right.Karma)
               && NullableEquals(left.Qualities, right.Qualities)
               && NullableEquals(left.Contacts, right.Contacts)
               && NullableEquals(left.Gear, right.Gear)
               && NullableEquals(left.Lifestyle, right.Lifestyle);
    }

    private static bool SequenceEquals(
        IReadOnlyList<Sr6CreationPriorityChoice>? left, IReadOnlyList<Sr6CreationPriorityChoice>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PreviewEquals(
        Sr6CreationFoundationPreview? left, Sr6CreationFoundationPreview? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return BindingEquals(left.Binding, right.Binding)
               && SelectionEquals(left.Selection, right.Selection)
               && string.Equals(left.PreviewDigest, right.PreviewDigest, StringComparison.Ordinal)
               && NullableEquals(left.Attributes, right.Attributes)
               && NullableEquals(left.Skills, right.Skills)
               && NullableEquals(left.Knowledge, right.Knowledge)
               && NullableEquals(left.PointBuy, right.PointBuy)
               && NullableEquals(left.TalentAllocation, right.TalentAllocation)
               && NullableEquals(left.ComplexForms, right.ComplexForms)
               && NullableEquals(left.Spells, right.Spells)
               && NullableEquals(left.AdeptPowers, right.AdeptPowers)
               && NullableEquals(left.Karma, right.Karma)
               && NullableEquals(left.Qualities, right.Qualities)
               && NullableEquals(left.Contacts, right.Contacts)
               && NullableEquals(left.Gear, right.Gear)
               && NullableEquals(left.Lifestyle, right.Lifestyle);
    }

    // T492 fix: content-based comparison for the optional sub-selection
    // records. Several carry IReadOnlyList<T> members, whose record
    // equality compares by reference - a List<T> built by the wizard
    // never equals the frozen T[] the decision carries, even with
    // identical content. JSON round-trips both shapes to the same
    // token stream, so serialize-and-compare is shape-independent.
    private static bool NullableEquals<T>(T? left, T? right) where T : class
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(
            System.Text.Json.JsonSerializer.Serialize(left, JsonOptions),
            System.Text.Json.JsonSerializer.Serialize(right, JsonOptions),
            StringComparison.Ordinal);
    }

    // T492 fix: shared serializer options so the JSON comparison is
    // deterministic across calls (camelCase would also work, but the
    // default PascalCase keeps the payload compact and stable).
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new();

    private static bool RefreshedStateMatches(
        Sr6CreationFoundationCommit commit,
        Sr6CreationFoundationState refreshed)
        => BindingWorkspaceMatches(commit, refreshed)
           && refreshed.Binding.ContentRevision == commit.Decision.CommittedContentRevision
           // T492 fix: Core's FileWorkspaceStore persists a foundation commit with
           // SavedRevision equal to CommittedContentRevision, so the post-commit
           // refresh binding satisfies ContentRevision >= SavedRevision (equality,
           // not a strict inequality).
           && refreshed.Binding.ContentRevision >= refreshed.Binding.SavedRevision
           // T492 fix: Core's Load emits the AuxiliaryStateDigest as bare 64-hex
           // (only AuthorityDigest carries the sha256: prefix), so accept both.
           && (IsDigest(refreshed.Binding.AuxiliaryStateDigest) || IsBareDigest(refreshed.Binding.AuxiliaryStateDigest))
           && IsDigest(refreshed.Binding.AuthorityDigest);

    private static bool BindingWorkspaceMatches(
        Sr6CreationFoundationCommit commit,
        Sr6CreationFoundationState refreshed)
        => string.Equals(
            refreshed.Binding.WorkspaceId.Value,
            commit.Decision.Command.Binding.WorkspaceId.Value,
            StringComparison.Ordinal);

    // T492 fix: bare 64-hex digest (Core's Load emits AuxiliaryStateDigest without the sha256: prefix).
    private static bool IsBareDigest(string? value)
        => value is { Length: 64 }
           && value.All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static bool IsDigest(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string[] NormalizeBlockers(IEnumerable<string> blockers)
        => blockers
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();

    private static Sr6CreationFoundationInteractionConfirmResult Failure(
        string outcome,
        Sr6CreationFoundationPreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        OwnerContextStamp Owner,
        Sr6CreationFoundationState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, params string[] blockers)
            => new(outcome, default, null, blockers);
    }
}
