using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationLifeModuleJourneyInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-journey-overview-authority-required";
    public const string BindingMismatch = "creation-journey-binding-mismatch";
    public const string StaleDraftRevision = "creation-journey-stale-draft-revision";
    public const string PreparedPreviewMismatch = "creation-journey-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-journey-preview-not-confirmable";
    public const string ReceiptMismatch = "creation-journey-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-journey-refresh-authority-required";
    public const string SelectionFinished = "creation-journey-selection-finished";
}

/// <summary>
/// A renderer-neutral, authority-preserving interaction boundary for the
/// Life-Modules journey (stages 2..5). It never derives legal choices or
/// writes a workspace itself; the Core journey service remains the sole read,
/// preview, confirmation, and persistence authority.
/// </summary>
public interface ICharacterCreationLifeModuleJourneyInteractionPresenter
{
    CharacterCreationLifeModuleJourneyInteractionLoadResult Load(
        CharacterOverviewState overview);

    CharacterCreationLifeModuleJourneyInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleSelectionInput input);

    CharacterCreationLifeModuleJourneyInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleConfirmation confirmation);

    CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult PrepareFinish(
        CharacterOverviewState overview);

    CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult ConfirmFinish(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleFinishConfirmation confirmation);
}

public sealed record CharacterCreationLifeModuleSelectionInput(
    string ModuleId,
    string? VersionId,
    IReadOnlyDictionary<string, string>? FollowUpValues = null);

public sealed record CharacterCreationLifeModuleJourneyInteractionState(
    CharacterCreationFoundationBinding Binding,
    long DraftRevision,
    string DraftDigest,
    int CurrentStageOrder,
    string CurrentStageId,
    IReadOnlyList<LifeModuleLegalOptionDto> Options,
    CharacterCreationBudgetState Budget,
    IReadOnlyList<CharacterCreationLifeModuleDraftEntry> AdditionalModules,
    bool SelectionFinished,
    bool CanFinishSelection,
    IReadOnlyList<string> AuthorityBlockers);

public sealed record CharacterCreationLifeModulePreparedPreview(
    string JourneyDigest,
    CharacterCreationFoundationBinding Binding,
    int StageOrder,
    string StageId,
    CharacterCreationFoundationSelection Selection,
    CharacterCreationLifeModuleDraftEntry Entry,
    CharacterCreationBudgetState BudgetBefore,
    CharacterCreationBudgetState BudgetAfter,
    IReadOnlyList<string> AuthorityBlockers,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    string PreviewDigest);

public sealed record CharacterCreationLifeModuleConfirmation(
    CharacterCreationLifeModulePreparedPreview PreparedPreview,
    string PreviewDigest,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationLifeModuleFinishConfirmation(
    string JourneyDigest,
    string PreviewDigest,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationLifeModuleJourneyInteractionLoadResult(
    string Outcome,
    CharacterCreationLifeModuleJourneyInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifeModuleJourneyInteractionPrepareResult(
    string Outcome,
    CharacterCreationLifeModuleJourneyInteractionState? State,
    CharacterCreationLifeModulePreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifeModuleJourneyInteractionConfirmResult(
    string Outcome,
    CharacterCreationLifeModulePreparedPreview? PreparedPreview,
    CharacterCreationLifeModuleApplyReceipt? Receipt,
    CharacterCreationLifeModuleJourneyInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
    string Outcome,
    CharacterCreationLifeModuleFinishPreview? FinishPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
    string Outcome,
    CharacterCreationLifeModuleFinishPreview? FinishPreview,
    CharacterCreationLifeModuleFinishReceipt? Receipt,
    CharacterCreationLifeModuleJourneyInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationLifeModuleJourneyInteractionPresenter
    : ICharacterCreationLifeModuleJourneyInteractionPresenter
{
    private readonly ICharacterCreationLifeModuleJourneyService _service;

    public CharacterCreationLifeModuleJourneyInteractionPresenter(
        ICharacterCreationLifeModuleJourneyService journeyService)
    {
        _service = journeyService ?? throw new ArgumentNullException(nameof(journeyService));
    }

    public CharacterCreationLifeModuleJourneyInteractionLoadResult Load(
        CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationLifeModuleJourneyInteractionLoadResult(
            load.Outcome,
            load.Journey is null ? null : Project(load.Journey),
            load.Blockers);
    }

    public CharacterCreationLifeModuleJourneyInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleSelectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ExactLoad load = LoadExact(overview);
        if (load.Journey is not CharacterCreationLifeModuleJourneyState journey)
        {
            return new CharacterCreationLifeModuleJourneyInteractionPrepareResult(
                load.Outcome,
                load.Journey is null ? null : Project(load.Journey),
                null,
                load.Blockers);
        }

        if (journey.SelectionFinished)
        {
            return new CharacterCreationLifeModuleJourneyInteractionPrepareResult(
                CharacterCreationFoundationOutcomes.Blocked,
                Project(journey),
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.SelectionFinished]);
        }

        var selection = new CharacterCreationFoundationSelection(
            input.ModuleId,
            input.VersionId);
        var request = new CharacterCreationLifeModulePreviewRequest(
            journey.Binding,
            journey.DraftRevision,
            journey.DraftDigest,
            selection,
            input.FollowUpValues);
        CharacterCreationFoundationResult<CharacterCreationLifeModulePreview> result =
            _service.PreviewModule(request);
        if (result.Value is not CharacterCreationLifeModulePreview preview)
        {
            return new CharacterCreationLifeModuleJourneyInteractionPrepareResult(
                result.Outcome,
                Project(journey),
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!BindingEquals(preview.Request.Binding, journey.Binding)
            || preview.Request.DraftRevision != journey.DraftRevision
            || !string.Equals(preview.Request.DraftDigest, journey.DraftDigest, StringComparison.Ordinal)
            || !string.Equals(preview.Request.Selection.ModuleId, selection.ModuleId, StringComparison.Ordinal)
            || !string.Equals(preview.Request.Selection.VersionId, selection.VersionId, StringComparison.Ordinal)
            || !IsDigest(preview.PreviewDigest))
        {
            return new CharacterCreationLifeModuleJourneyInteractionPrepareResult(
                CharacterCreationFoundationOutcomes.Conflict,
                Project(journey),
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.PreparedPreviewMismatch]);
        }

        CharacterCreationLifeModulePreparedPreview prepared = Project(journey.DraftDigest, preview);
        return new CharacterCreationLifeModuleJourneyInteractionPrepareResult(
            result.Outcome,
            Project(journey),
            prepared,
            NormalizeBlockers(result.Blockers));
    }

    public CharacterCreationLifeModuleJourneyInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationLifeModulePreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return ConfirmFailure(
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
            return ConfirmFailure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                CharacterCreationFoundationBlockers.PreviewDigestMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.Journey is not CharacterCreationLifeModuleJourneyState journey)
        {
            return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }

        if (!string.Equals(prepared.JourneyDigest, journey.DraftDigest, StringComparison.Ordinal)
            || !BindingEquals(prepared.Binding, journey.Binding)
            || prepared.StageOrder != journey.CurrentStageOrder)
        {
            return ConfirmFailure(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Binding, journey.Binding));
        }

        if (!prepared.CanConfirm
            || prepared.AuthorityBlockers.Count > 0)
        {
            IReadOnlyList<string> blockers = prepared.AuthorityBlockers.Count > 0
                ? prepared.AuthorityBlockers
                : [CharacterCreationLifeModuleJourneyInteractionBlockers.PreviewNotConfirmable];
            return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Blocked,
                prepared,
                null,
                null,
                NormalizeBlockers(blockers));
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleApplyReceipt> result =
            _service.ConfirmModule(new CharacterCreationLifeModuleConfirmRequest(
                new CharacterCreationLifeModulePreviewRequest(
                    prepared.Binding,
                    journey.DraftRevision,
                    journey.DraftDigest,
                    prepared.Selection,
                    prepared.Entry.FollowUpValues),
                prepared.PreviewDigest,
                ExplicitlyConfirmed: true));
        if (result.Value is not CharacterCreationLifeModuleApplyReceipt receipt)
        {
            return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!ReceiptMatches(prepared, receipt))
        {
            return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleJourneyState> refresh =
            _service.LoadJourney(new CharacterCreationFoundationLoadRequest(
                receipt.Binding.WorkspaceId));
        if (refresh.Outcome != CharacterCreationFoundationOutcomes.Success
            || refresh.Value is not CharacterCreationLifeModuleJourneyState refreshed
            || !RefreshedStateMatches(receipt, journey, refreshed))
        {
            return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                prepared,
                receipt,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    CharacterCreationLifeModuleJourneyInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationLifeModuleJourneyInteractionConfirmResult(
            CharacterCreationFoundationOutcomes.Success,
            prepared,
            receipt,
            Project(refreshed),
            NormalizeBlockers(refresh.Blockers));
    }

    public CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult PrepareFinish(
        CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        if (load.Journey is not CharacterCreationLifeModuleJourneyState journey)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
                load.Outcome,
                null,
                load.Blockers);
        }

        if (journey.SelectionFinished)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.SelectionFinished]);
        }

        if (!journey.CanFinishSelection)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationFoundationBlockers.FinalizationRequiredStagesIncomplete]);
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleFinishPreview> result =
            _service.PreviewFinishSelection(new CharacterCreationLifeModuleFinishRequest(
                journey.Binding,
                journey.DraftRevision,
                journey.DraftDigest));
        if (result.Value is not CharacterCreationLifeModuleFinishPreview finishPreview
            || !IsDigest(finishPreview.PreviewDigest)
            || !string.Equals(
                finishPreview.Request.Binding.WorkspaceId.Value,
                journey.Binding.WorkspaceId.Value,
                StringComparison.Ordinal)
            || finishPreview.Request.DraftRevision != journey.DraftRevision)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
                result.Value is null ? result.Outcome : CharacterCreationFoundationOutcomes.Conflict,
                result.Value is null ? null : result.Value,
                result.Value is null
                    ? NormalizeBlockers(result.Blockers)
                    : [CharacterCreationLifeModuleJourneyInteractionBlockers.PreparedPreviewMismatch]);
        }

        return new CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult(
            result.Outcome,
            result.Value,
            NormalizeBlockers(result.Blockers));
    }

    public CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult ConfirmFinish(
        CharacterOverviewState overview,
        CharacterCreationLifeModuleFinishConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        if (!confirmation.ExplicitlyConfirmed)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                CharacterCreationFoundationOutcomes.Invalid,
                null,
                null,
                null,
                [CharacterCreationFoundationBlockers.ExplicitConfirmationRequired]);
        }

        if (!IsDigest(confirmation.PreviewDigest))
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                null,
                null,
                [CharacterCreationFoundationBlockers.PreviewDigestMismatch]);
        }

        ExactLoad load = LoadExact(overview);
        if (load.Journey is not CharacterCreationLifeModuleJourneyState journey)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                load.Outcome,
                null,
                null,
                null,
                load.Blockers);
        }

        if (!string.Equals(confirmation.JourneyDigest, journey.DraftDigest, StringComparison.Ordinal))
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                null,
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.BindingMismatch]);
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleFinishReceipt> result =
            _service.ConfirmFinishSelection(new CharacterCreationLifeModuleFinishConfirmRequest(
                new CharacterCreationLifeModuleFinishRequest(
                    journey.Binding,
                    journey.DraftRevision,
                    journey.DraftDigest),
                confirmation.PreviewDigest,
                ExplicitlyConfirmed: true));
        if (result.Value is not CharacterCreationLifeModuleFinishReceipt receipt)
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                result.Outcome,
                null,
                null,
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!FinishReceiptMatches(journey, receipt))
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                receipt,
                null,
                [CharacterCreationLifeModuleJourneyInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleJourneyState> refresh =
            _service.LoadJourney(new CharacterCreationFoundationLoadRequest(
                receipt.Binding.WorkspaceId));
        if (refresh.Outcome != CharacterCreationFoundationOutcomes.Success
            || refresh.Value is not CharacterCreationLifeModuleJourneyState refreshed
            || !RefreshedFinishStateMatches(receipt, journey, refreshed))
        {
            return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
                CharacterCreationFoundationOutcomes.Conflict,
                null,
                receipt,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    CharacterCreationLifeModuleJourneyInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult(
            CharacterCreationFoundationOutcomes.Success,
            null,
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
                CharacterCreationLifeModuleJourneyInteractionBlockers.OverviewAuthorityRequired);
        }

        CharacterCreationFoundationResult<CharacterCreationLifeModuleJourneyState> result =
            _service.LoadJourney(new CharacterCreationFoundationLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.Value is not CharacterCreationLifeModuleJourneyState journey)
        {
            return new ExactLoad(result.Outcome, null, NormalizeBlockers(result.Blockers));
        }

        if (journey.SelectionFinished)
        {
            return new ExactLoad(
                CharacterCreationFoundationOutcomes.Success,
                journey,
                NormalizeBlockers([CharacterCreationLifeModuleJourneyInteractionBlockers.SelectionFinished]));
        }

        if (overview.CreationFoundation is not { } foundation
            || !MatchesFoundationBinding(foundation, journey))
        {
            return ExactLoad.Failure(
                CharacterCreationFoundationOutcomes.Conflict,
                CharacterCreationLifeModuleJourneyInteractionBlockers.BindingMismatch);
        }

        return new ExactLoad(
            CharacterCreationFoundationOutcomes.Success,
            journey,
            NormalizeBlockers(result.Blockers));
    }

    private static bool MatchesFoundationBinding(
        CharacterCreationFoundationState projected,
        CharacterCreationLifeModuleJourneyState journey)
        => BindingEquals(projected.Binding, journey.Binding);

    private static CharacterCreationLifeModuleJourneyInteractionState Project(
        CharacterCreationLifeModuleJourneyState journey)
        => new(
            journey.Binding,
            journey.DraftRevision,
            journey.DraftDigest,
            journey.CurrentStageOrder,
            StageIdFor(journey.CurrentStageOrder),
            journey.Options,
            journey.Budget,
            journey.AdditionalModules,
            journey.SelectionFinished,
            journey.CanFinishSelection,
            []);

    private static string StageIdFor(int stageOrder)
        => stageOrder switch
        {
            LifeModuleJourneyStageOrders.Nationality => CharacterCreationLifeModuleStageIds.Nationality,
            LifeModuleJourneyStageOrders.FormativeYears => CharacterCreationLifeModuleStageIds.FormativeYears,
            LifeModuleJourneyStageOrders.TeenYears => CharacterCreationLifeModuleStageIds.TeenYears,
            LifeModuleJourneyStageOrders.FurtherEducation => CharacterCreationLifeModuleStageIds.FurtherEducation,
            LifeModuleJourneyStageOrders.RealLife => CharacterCreationLifeModuleStageIds.RealLife,
            _ => $"life-module-stage-{stageOrder}",
        };

    private static CharacterCreationLifeModulePreparedPreview Project(
        string journeyDigest,
        CharacterCreationLifeModulePreview preview)
        => new(
            journeyDigest,
            preview.Request.Binding,
            preview.Entry.StageOrder,
            preview.Entry.StageId,
            preview.Request.Selection,
            preview.Entry,
            preview.BudgetBefore,
            preview.BudgetAfter,
            [],
            preview.CanConfirm,
            preview.CanConfirm,
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
        return CharacterCreationLifeModuleJourneyInteractionBlockers.BindingMismatch;
    }

    private static bool ReceiptMatches(
        CharacterCreationLifeModulePreparedPreview prepared,
        CharacterCreationLifeModuleApplyReceipt receipt)
        => string.Equals(
               receipt.Binding.WorkspaceId.Value,
               prepared.Binding.WorkspaceId.Value,
               StringComparison.Ordinal)
           && receipt.PreviousContentRevision == prepared.Binding.ContentRevision
           && receipt.ContentRevision > receipt.PreviousContentRevision
           && receipt.SavedRevision == receipt.ContentRevision
           && string.Equals(
               receipt.Binding.RawCharacterXmlDigest,
               prepared.Binding.RawCharacterXmlDigest,
               StringComparison.Ordinal)
           && string.Equals(receipt.Binding.SourceDigest, prepared.Binding.SourceDigest, StringComparison.Ordinal)
           && receipt.DraftRevision > 0
           && IsDigest(receipt.DraftDigest)
           && string.Equals(receipt.Entry.Selection.ModuleId, prepared.Selection.ModuleId, StringComparison.Ordinal)
           && string.Equals(receipt.Entry.Selection.VersionId, prepared.Selection.VersionId, StringComparison.Ordinal)
           && receipt.Entry.StageOrder == prepared.StageOrder
           && string.Equals(receipt.Entry.StageId, prepared.StageId, StringComparison.Ordinal)
           && !receipt.CharacterEffectsApplied;

    private static bool RefreshedStateMatches(
        CharacterCreationLifeModuleApplyReceipt receipt,
        CharacterCreationLifeModuleJourneyState before,
        CharacterCreationLifeModuleJourneyState refreshed)
        => string.Equals(
               refreshed.Binding.WorkspaceId.Value,
               receipt.Binding.WorkspaceId.Value,
               StringComparison.Ordinal)
           && refreshed.Binding.ContentRevision == receipt.ContentRevision
           && refreshed.Binding.SavedRevision == receipt.SavedRevision
           && refreshed.DraftRevision == receipt.DraftRevision
           && string.Equals(refreshed.DraftDigest, receipt.DraftDigest, StringComparison.Ordinal)
           && refreshed.AdditionalModules.Count == before.AdditionalModules.Count + 1
           && IsDigest(refreshed.DraftDigest);

    private static bool FinishReceiptMatches(
        CharacterCreationLifeModuleJourneyState journey,
        CharacterCreationLifeModuleFinishReceipt receipt)
        => string.Equals(
               receipt.Binding.WorkspaceId.Value,
               journey.Binding.WorkspaceId.Value,
               StringComparison.Ordinal)
           && receipt.PreviousContentRevision == journey.Binding.ContentRevision
           && receipt.ContentRevision > receipt.PreviousContentRevision
           && receipt.SavedRevision == receipt.ContentRevision
           && string.Equals(
               receipt.Binding.RawCharacterXmlDigest,
               journey.Binding.RawCharacterXmlDigest,
               StringComparison.Ordinal)
           && string.Equals(receipt.Binding.SourceDigest, journey.Binding.SourceDigest, StringComparison.Ordinal)
           && receipt.DraftRevision > 0
           && IsDigest(receipt.DraftDigest)
           && !receipt.CharacterEffectsApplied;

    private static bool RefreshedFinishStateMatches(
        CharacterCreationLifeModuleFinishReceipt receipt,
        CharacterCreationLifeModuleJourneyState before,
        CharacterCreationLifeModuleJourneyState refreshed)
        => string.Equals(
               refreshed.Binding.WorkspaceId.Value,
               receipt.Binding.WorkspaceId.Value,
               StringComparison.Ordinal)
           && refreshed.Binding.ContentRevision == receipt.ContentRevision
           && refreshed.Binding.SavedRevision == receipt.SavedRevision
           && refreshed.DraftRevision == receipt.DraftRevision
           && string.Equals(refreshed.DraftDigest, receipt.DraftDigest, StringComparison.Ordinal)
           && refreshed.SelectionFinished
           && refreshed.AdditionalModules.Count == before.AdditionalModules.Count
           && refreshed.Options.Count == 0
           && IsDigest(refreshed.DraftDigest);

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

    private static CharacterCreationLifeModuleJourneyInteractionConfirmResult ConfirmFailure(
        string outcome,
        CharacterCreationLifeModulePreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationLifeModuleJourneyState? Journey,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, params string[] blockers)
            => new(outcome, null, blockers);
    }
}
