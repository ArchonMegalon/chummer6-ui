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

        if (!CommitMatches(prepared, commit))
        {
            return new Sr6CreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                commit,
                null,
                [Sr6CreationFoundationInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationFoundationResult<Sr6CreationFoundationState> refresh =
            _service.Load(load.Owner, foundation.Binding.WorkspaceId);
        if (refresh.Outcome != CharacterCreationFoundationOutcomes.Success
            || refresh.Value is not Sr6CreationFoundationState refreshed
            || !RefreshedStateMatches(commit, refreshed))
        {
            return new Sr6CreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                commit,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    Sr6CreationFoundationInteractionBlockers.RefreshAuthorityRequired)));
        }

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
        Sr6CreationFoundationCommit commit)
    {
        Sr6CreationFoundationDecision decision = commit.Decision;
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
               && decision.Command.Selection.Equals(prepared.Preview.Selection)
               && decision.Command.ExplicitlyConfirmed
               && decision.Preview.Equals(prepared.Preview)
               && decision.CommittedContentRevision > prepared.Foundation.Binding.ContentRevision
               && IsDigest(decision.DecisionDigest);
    }

    private static bool RefreshedStateMatches(
        Sr6CreationFoundationCommit commit,
        Sr6CreationFoundationState refreshed)
        => BindingWorkspaceMatches(commit, refreshed)
           && refreshed.Binding.ContentRevision == commit.Decision.CommittedContentRevision
           && refreshed.Binding.ContentRevision > refreshed.Binding.SavedRevision
           && IsDigest(refreshed.Binding.AuxiliaryStateDigest)
           && IsDigest(refreshed.Binding.AuthorityDigest);

    private static bool BindingWorkspaceMatches(
        Sr6CreationFoundationCommit commit,
        Sr6CreationFoundationState refreshed)
        => string.Equals(
            refreshed.Binding.WorkspaceId.Value,
            commit.Decision.Command.Binding.WorkspaceId.Value,
            StringComparison.Ordinal);

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
