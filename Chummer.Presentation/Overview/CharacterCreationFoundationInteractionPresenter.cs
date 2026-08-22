using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationFoundationInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-foundation-overview-authority-required";
    public const string BindingMismatch = "creation-foundation-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-foundation-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-foundation-preview-not-confirmable";
    public const string ReceiptMismatch = "creation-foundation-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-foundation-refresh-authority-required";
}

/// <summary>
/// A renderer-neutral, authority-preserving interaction boundary for the first
/// Foundation/Life Module wizard selection.  It never derives legal choices or
/// writes a workspace itself; the Core service remains the sole read, preview,
/// confirmation, and persistence authority.
/// </summary>
public interface ICharacterCreationFoundationInteractionPresenter
{
    CharacterCreationFoundationInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationFoundationInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationFoundationSelectionInput input);

    CharacterCreationFoundationInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationFoundationConfirmation confirmation);
}

public sealed record CharacterCreationFoundationSelectionInput(
    string RequestedMetatype,
    string NationalityModuleId,
    string? NationalityVersionId,
    IReadOnlyDictionary<string, string>? FollowUpValues = null);

public sealed record CharacterCreationFoundationInteractionState(
    CharacterCreationFoundationBinding Binding,
    string RulesetId,
    string CurrentMetatype,
    string BuildMethod,
    IReadOnlyList<CharacterCreationLegalOption> MetatypeOptions,
    IReadOnlyList<LifeModuleLegalOptionDto> NationalityOptions,
    CharacterCreationBudgetState LifeModuleBudget,
    CharacterCreationFoundationDraftLedger? PendingDraft,
    string ResumeStatus,
    IReadOnlyList<string> AuthorityBlockers,
    string FoundationSnapshotDigest);

public sealed record CharacterCreationFoundationPreparedPreview(
    string FoundationSnapshotDigest,
    CharacterCreationFoundationBinding Binding,
    string RequestedMetatype,
    CharacterCreationFoundationSelection Selection,
    LifeModuleLegalOptionDto? Nationality,
    LifeModuleVersionProjectionDto? NationalityVersion,
    IReadOnlyList<LifeModuleRequirementProjectionDto> RequirementEvaluations,
    IReadOnlyDictionary<string, string> FollowUpValues,
    CharacterCreationBudgetState LifeModuleBudgetBefore,
    CharacterCreationChoiceCost SelectionCost,
    CharacterCreationBudgetState LifeModuleBudgetAfter,
    IReadOnlyList<CharacterCreationFoundationDiffEntry> Diff,
    IReadOnlyList<string> AuthorityBlockers,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    bool CanApply,
    bool CharacterEffectsApplied,
    string PreviewDigest);

public sealed record CharacterCreationFoundationConfirmation(
    CharacterCreationFoundationPreparedPreview PreparedPreview,
    string PreviewDigest,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationFoundationInteractionLoadResult(
    string Outcome,
    CharacterCreationFoundationInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationFoundationInteractionPrepareResult(
    string Outcome,
    CharacterCreationFoundationInteractionState? State,
    CharacterCreationFoundationPreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationFoundationInteractionConfirmResult(
    string Outcome,
    CharacterCreationFoundationPreparedPreview? PreparedPreview,
    CharacterCreationFoundationApplyReceipt? Receipt,
    CharacterCreationFoundationInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationFoundationInteractionPresenter
    : ICharacterCreationFoundationInteractionPresenter
{
    private readonly ICharacterCreationFoundationService _service;

    public CharacterCreationFoundationInteractionPresenter(
        ICharacterCreationFoundationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public CharacterCreationFoundationInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationFoundationInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public CharacterCreationFoundationInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationFoundationSelectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationFoundationState foundation)
        {
            return new CharacterCreationFoundationInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }

        var selection = new CharacterCreationFoundationSelection(
            input.NationalityModuleId,
            input.NationalityVersionId);
        CharacterCreationFoundationResult<CharacterCreationFoundationPreview> result =
            _service.Preview(new CharacterCreationFoundationPreviewRequest(
                foundation.Binding,
                input.RequestedMetatype,
                selection,
                input.FollowUpValues));
        if (result.Value is not CharacterCreationFoundationPreview preview)
        {
            return new CharacterCreationFoundationInteractionPrepareResult(
                result.Outcome,
                Project(foundation),
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!BindingEquals(preview.Binding, foundation.Binding)
            || !string.Equals(preview.Selection.ModuleId, selection.ModuleId, StringComparison.Ordinal)
            || !string.Equals(preview.Selection.VersionId, selection.VersionId, StringComparison.Ordinal)
            || !string.Equals(
                preview.RequestedMetatype,
                (input.RequestedMetatype ?? string.Empty).Trim(),
                StringComparison.Ordinal)
            || !IsDigest(preview.PreviewDigest))
        {
            return new CharacterCreationFoundationInteractionPrepareResult(
                CharacterCreationFoundationOutcomes.Conflict,
                Project(foundation),
                null,
                [CharacterCreationFoundationInteractionBlockers.PreparedPreviewMismatch]);
        }

        CharacterCreationFoundationPreparedPreview prepared = Project(
            foundation.SnapshotDigest,
            preview);
        return new CharacterCreationFoundationInteractionPrepareResult(
            result.Outcome,
            Project(foundation),
            prepared,
            NormalizeBlockers(result.Blockers.Concat(preview.AuthorityBlockers)));
    }

    public CharacterCreationFoundationInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationFoundationConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationFoundationPreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Invalid,
                prepared,
                CharacterCreationFoundationBlockers.ExplicitConfirmationRequired);
        }

        if (!string.Equals(
                confirmation.PreviewDigest,
                prepared.PreviewDigest,
                StringComparison.Ordinal)
            || !IsDigest(prepared.PreviewDigest))
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                CharacterCreationFoundationBlockers.PreviewDigestMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationFoundationState foundation)
        {
            return new CharacterCreationFoundationInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }

        if (!string.Equals(
                prepared.FoundationSnapshotDigest,
                foundation.SnapshotDigest,
                StringComparison.Ordinal)
            || !BindingEquals(prepared.Binding, foundation.Binding))
        {
            return Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Binding, foundation.Binding));
        }

        if (!prepared.RequiresExplicitConfirmation
            || !prepared.CanConfirm
            || !prepared.CanApply
            || prepared.AuthorityBlockers.Count > 0)
        {
            IReadOnlyList<string> blockers = prepared.AuthorityBlockers.Count > 0
                ? prepared.AuthorityBlockers
                : [CharacterCreationFoundationInteractionBlockers.PreviewNotConfirmable];
            return new CharacterCreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Blocked,
                prepared,
                null,
                null,
                NormalizeBlockers(blockers));
        }

        CharacterCreationFoundationResult<CharacterCreationFoundationApplyReceipt> result =
            _service.Confirm(new CharacterCreationFoundationConfirmRequest(
                prepared.Binding,
                prepared.RequestedMetatype,
                prepared.Selection,
                prepared.PreviewDigest,
                ExplicitlyConfirmed: true,
                prepared.FollowUpValues));
        if (result.Value is not CharacterCreationFoundationApplyReceipt receipt)
        {
            return new CharacterCreationFoundationInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!ReceiptMatches(prepared, receipt))
        {
            return new CharacterCreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationFoundationInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationFoundationResult<CharacterCreationFoundationState> refresh =
            _service.Load(new CharacterCreationFoundationLoadRequest(receipt.WorkspaceId));
        if (refresh.Outcome != CharacterCreationFoundationOutcomes.Success
            || refresh.Value is not CharacterCreationFoundationState refreshed
            || !RefreshedStateMatches(receipt, foundation, refreshed))
        {
            return new CharacterCreationFoundationInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                receipt,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    CharacterCreationFoundationInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationFoundationInteractionConfirmResult(
            CharacterCreationFoundationOutcomes.Success,
            prepared,
            receipt,
            Project(refreshed),
            NormalizeBlockers(refresh.Blockers));
    }

    private ExactLoad LoadExact(CharacterOverviewState overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created == true
            || overview.CreationWizard?.CharacterCreated == true
            || overview.CreationFoundation?.CharacterCreated == true)
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Blocked,
                CharacterCreationFoundationBlockers.CharacterAlreadyCreated);
        }

        if (overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || overview.Profile is null
            || overview.CreationWizard is not { } wizard
            || overview.CreationFoundation is not { } projectedFoundation
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Invalid,
                CharacterCreationFoundationInteractionBlockers.OverviewAuthorityRequired);
        }

        CharacterCreationFoundationResult<CharacterCreationFoundationState> result =
            _service.Load(new CharacterCreationFoundationLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.Value is not CharacterCreationFoundationState foundation)
        {
            return new ExactLoad(result.Outcome, null, NormalizeBlockers(result.Blockers));
        }

        if (foundation.CharacterCreated)
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Blocked,
                CharacterCreationFoundationBlockers.CharacterAlreadyCreated);
        }

        if (!MatchesOverview(
                workspaceId,
                activeWorkspace.ContentRevision,
                activeWorkspace.SavedRevision,
                wizard,
                projectedFoundation,
                foundation))
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                CharacterCreationFoundationInteractionBlockers.BindingMismatch);
        }

        return new ExactLoad(
            CharacterCreationFoundationOutcomes.Success,
            foundation,
            NormalizeBlockers(result.Blockers.Concat(foundation.AuthorityBlockers)));
    }

    private static bool MatchesOverview(
        Chummer.Contracts.Workspaces.CharacterWorkspaceId workspaceId,
        long contentRevision,
        long savedRevision,
        CharacterCreationWizardSnapshot wizard,
        CharacterCreationFoundationState projected,
        CharacterCreationFoundationState loaded)
        => string.Equals(workspaceId.Value, loaded.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && contentRevision == loaded.Binding.ContentRevision
           && savedRevision == loaded.Binding.SavedRevision
           && string.Equals(wizard.WorkspaceId, workspaceId.Value, StringComparison.Ordinal)
           && wizard.WorkspaceRevision == loaded.Binding.ContentRevision
           && string.Equals(wizard.ContentDigest, loaded.Binding.RawCharacterXmlDigest, StringComparison.Ordinal)
           && string.Equals(wizard.SourceDigest, loaded.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(wizard.RulesetId, loaded.RulesetId, StringComparison.Ordinal)
           && string.Equals(wizard.BuildMethod, loaded.BuildMethod, StringComparison.Ordinal)
           && !wizard.CharacterCreated
           && BindingEquals(projected.Binding, loaded.Binding)
           && string.Equals(projected.SnapshotDigest, loaded.SnapshotDigest, StringComparison.Ordinal)
           && string.Equals(projected.RulesetId, loaded.RulesetId, StringComparison.Ordinal)
           && string.Equals(projected.BuildMethod, loaded.BuildMethod, StringComparison.Ordinal)
           && !projected.CharacterCreated
           && string.Equals(
               loaded.Binding.CharacterDigestSemantics,
               CharacterCreationFoundationDigestSemantics.RawCharacterXmlSha256,
               StringComparison.Ordinal)
           && string.Equals(
               loaded.Binding.SourceDigestSemantics,
               CharacterCreationFoundationDigestSemantics.RawSourceInputsSha256,
               StringComparison.Ordinal)
           && IsDigest(loaded.Binding.RawCharacterXmlDigest)
           && IsDigest(loaded.Binding.SourceDigest)
           && IsDigest(loaded.SnapshotDigest);

    private static CharacterCreationFoundationInteractionState Project(
        CharacterCreationFoundationState state)
        => new(
            state.Binding,
            state.RulesetId,
            state.CurrentMetatype,
            state.BuildMethod,
            state.MetatypeOptions,
            state.NationalityOptions,
            state.LifeModuleBudget,
            state.PendingDraft,
            state.ResumeStatus,
            state.AuthorityBlockers,
            state.SnapshotDigest);

    private static CharacterCreationFoundationPreparedPreview Project(
        string foundationSnapshotDigest,
        CharacterCreationFoundationPreview preview)
        => new(
            foundationSnapshotDigest,
            preview.Binding,
            preview.RequestedMetatype,
            preview.Selection,
            preview.Nationality,
            preview.NationalityVersion,
            preview.RequirementEvaluations,
            preview.FollowUpValues,
            preview.LifeModuleBudgetBefore,
            preview.SelectionCost,
            preview.LifeModuleBudgetAfter,
            preview.Diff,
            preview.AuthorityBlockers,
            preview.RequiresExplicitConfirmation,
            preview.CanConfirm,
            preview.CanApply,
            preview.CharacterEffectsApplied,
            preview.PreviewDigest);

    private static bool BindingEquals(
        CharacterCreationFoundationBinding left,
        CharacterCreationFoundationBinding right)
        => string.Equals(left.WorkspaceId.Value, right.WorkspaceId.Value, StringComparison.Ordinal)
           && left.ContentRevision == right.ContentRevision
           && left.SavedRevision == right.SavedRevision
           && string.Equals(left.RawCharacterXmlDigest, right.RawCharacterXmlDigest, StringComparison.Ordinal)
           && string.Equals(left.CharacterDigestSemantics, right.CharacterDigestSemantics, StringComparison.Ordinal)
           && string.Equals(left.SourceDigest, right.SourceDigest, StringComparison.Ordinal)
           && string.Equals(left.SourceDigestSemantics, right.SourceDigestSemantics, StringComparison.Ordinal)
           && left.SourceFilterApplied == right.SourceFilterApplied
           && left.EnabledSources.SequenceEqual(right.EnabledSources, StringComparer.Ordinal);

    private static string BindingConflict(
        CharacterCreationFoundationBinding prepared,
        CharacterCreationFoundationBinding current)
    {
        if (prepared.ContentRevision != current.ContentRevision
            || prepared.SavedRevision != current.SavedRevision)
        {
            return CharacterCreationFoundationBlockers.StaleWorkspaceRevision;
        }

        if (!string.Equals(
                prepared.RawCharacterXmlDigest,
                current.RawCharacterXmlDigest,
                StringComparison.Ordinal))
        {
            return CharacterCreationFoundationBlockers.StaleRawCharacterXmlDigest;
        }

        if (!string.Equals(prepared.SourceDigest, current.SourceDigest, StringComparison.Ordinal))
            return CharacterCreationFoundationBlockers.SourceDigestConflict;
        return CharacterCreationFoundationInteractionBlockers.PreparedPreviewMismatch;
    }

    private static bool ReceiptMatches(
        CharacterCreationFoundationPreparedPreview prepared,
        CharacterCreationFoundationApplyReceipt receipt)
        => string.Equals(receipt.WorkspaceId.Value, prepared.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && receipt.PreviousContentRevision == prepared.Binding.ContentRevision
           && receipt.ContentRevision > receipt.PreviousContentRevision
           && receipt.SavedRevision == receipt.ContentRevision
           && string.Equals(receipt.RawCharacterXmlDigest, prepared.Binding.RawCharacterXmlDigest, StringComparison.Ordinal)
           && string.Equals(receipt.SourceDigest, prepared.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(receipt.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal)
           && string.Equals(receipt.Selection.ModuleId, prepared.Selection.ModuleId, StringComparison.Ordinal)
           && string.Equals(receipt.Selection.VersionId, prepared.Selection.VersionId, StringComparison.Ordinal)
           && string.Equals(receipt.Metatype, prepared.RequestedMetatype, StringComparison.Ordinal)
           && receipt.DraftRevision > 0
           && IsDigest(receipt.DraftDigest)
           && !receipt.CharacterEffectsApplied;

    private static bool RefreshedStateMatches(
        CharacterCreationFoundationApplyReceipt receipt,
        CharacterCreationFoundationState before,
        CharacterCreationFoundationState refreshed)
        => !refreshed.CharacterCreated
           && string.Equals(refreshed.Binding.WorkspaceId.Value, receipt.WorkspaceId.Value, StringComparison.Ordinal)
           && refreshed.Binding.ContentRevision == receipt.ContentRevision
           && refreshed.Binding.SavedRevision == receipt.SavedRevision
           && string.Equals(refreshed.Binding.RawCharacterXmlDigest, receipt.RawCharacterXmlDigest, StringComparison.Ordinal)
           && string.Equals(refreshed.Binding.SourceDigest, receipt.SourceDigest, StringComparison.Ordinal)
           && string.Equals(
               refreshed.Binding.CharacterDigestSemantics,
               CharacterCreationFoundationDigestSemantics.RawCharacterXmlSha256,
               StringComparison.Ordinal)
           && string.Equals(
               refreshed.Binding.SourceDigestSemantics,
               CharacterCreationFoundationDigestSemantics.RawSourceInputsSha256,
               StringComparison.Ordinal)
           && refreshed.Binding.SourceFilterApplied == before.Binding.SourceFilterApplied
           && refreshed.Binding.EnabledSources.SequenceEqual(
               before.Binding.EnabledSources,
               StringComparer.Ordinal)
           && string.Equals(refreshed.RulesetId, before.RulesetId, StringComparison.Ordinal)
           && string.Equals(refreshed.BuildMethod, before.BuildMethod, StringComparison.Ordinal)
           && refreshed.PendingDraft is { } draft
           && string.Equals(draft.WorkspaceId.Value, receipt.WorkspaceId.Value, StringComparison.Ordinal)
           && draft.DraftRevision == receipt.DraftRevision
           && draft.BaseContentRevision == receipt.PreviousContentRevision
           && string.Equals(draft.BaseRawCharacterXmlDigest, receipt.RawCharacterXmlDigest, StringComparison.Ordinal)
           && string.Equals(draft.SourceDigest, receipt.SourceDigest, StringComparison.Ordinal)
           && string.Equals(draft.RequestedMetatype, receipt.Metatype, StringComparison.Ordinal)
           && string.Equals(draft.Selection.ModuleId, receipt.Selection.ModuleId, StringComparison.Ordinal)
           && string.Equals(draft.Selection.VersionId, receipt.Selection.VersionId, StringComparison.Ordinal)
           && string.Equals(draft.DraftDigest, receipt.DraftDigest, StringComparison.Ordinal)
           && !draft.CharacterEffectsApplied
           && IsDigest(refreshed.SnapshotDigest);

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

    private static CharacterCreationFoundationInteractionConfirmResult Failure(
        string outcome,
        CharacterCreationFoundationPreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationFoundationState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, params string[] blockers)
            => new(outcome, null, blockers);
    }
}
