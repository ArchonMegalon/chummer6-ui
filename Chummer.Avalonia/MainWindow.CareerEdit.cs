using System;
using Chummer.Application.Owners;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private const string CareerEditNoticePrefix = "Career advancement: ";
    private readonly CareerDisplayContext _careerDisplayContext = new();

    private void ObserveCareerDisplayContext(object? sender, EventArgs args)
    {
        CharacterOverviewState state = _adapter.State;
        _careerDisplayContext.Observe(state.WorkspaceId, state.DisplayOwnerContext);
    }

    private async void SectionHost_OnCareerKarmaAwardRequested(object? sender, object? e)
    {
        await RunUiActionAsync(
            () => OpenCareerRewardsDialogAsync(isKarmaMode: true),
            "career-karma-award").ConfigureAwait(true);
    }

    private async void SectionHost_OnCareerNuyenAdjustRequested(object? sender, object? e)
    {
        await RunUiActionAsync(
            () => OpenCareerRewardsDialogAsync(isKarmaMode: false),
            "career-nuyen-adjust").ConfigureAwait(true);
    }

    private async Task OpenCareerRewardsDialogAsync(bool isKarmaMode)
    {
        CharacterOverviewState entry = _interactionCoordinator.PeekState();
        if (entry.WorkspaceId is not { } entryWorkspaceId)
            return;
        long entryGeneration = _careerDisplayContext.Capture(entryWorkspaceId, entry.DisplayOwnerContext);
        bool DisplayStillCurrent()
        {
            CharacterOverviewState current = _interactionCoordinator.PeekState();
            return current.WorkspaceId == entryWorkspaceId
                && current.DisplayOwnerContext == entry.DisplayOwnerContext
                && _careerDisplayContext.IsCurrent(entryGeneration, current.WorkspaceId, current.DisplayOwnerContext);
        }

        CareerManualKarmaEditorState? karmaState = await _interactionCoordinator
            .PrepareCareerManualKarmaEditAsync(CancellationToken.None)
            .ConfigureAwait(true);
        if (karmaState is null || !DisplayStillCurrent())
        {
            return;
        }

        CareerManualNuyenEditorState? nuyenState = isKarmaMode
            ? null
            : await _interactionCoordinator
                .PrepareCareerManualNuyenEditAsync(CancellationToken.None)
                .ConfigureAwait(true);
        if (!DisplayStillCurrent() || (!isKarmaMode && nuyenState is null))
        {
            return;
        }

        while (true)
        {
            if (!DisplayStillCurrent())
                return;
            // Inline-apply mode (UX-03): the dialog applies the mutation itself
            // so an authority refusal keeps the amount, direction and notes on
            // screen, discloses the reason and re-prepares a fresh editor
            // state; the next Record press is the explicit re-confirm.
            string? PeekRefusal() => PeekCareerEditError();
            var dialog = new CareerRewardsWindow(
                isKarmaMode,
                karmaState,
                nuyenState,
                applyKarma: async (request, _) =>
                {
                    await _interactionCoordinator
                        .ApplyCareerManualKarmaEditAsync(request, CancellationToken.None)
                        .ConfigureAwait(true);
                    return PeekRefusal();
                },
                applyNuyen: async (request, _) =>
                {
                    await _interactionCoordinator
                        .ApplyCareerManualNuyenEditAsync(request, CancellationToken.None)
                        .ConfigureAwait(true);
                    return PeekRefusal();
                },
                reprepare: async _ =>
                {
                    if (!DisplayStillCurrent())
                        return (null, null);
                    CareerManualKarmaEditorState? freshKarma = await _interactionCoordinator
                        .PrepareCareerManualKarmaEditAsync(CancellationToken.None)
                        .ConfigureAwait(true);
                    CareerManualNuyenEditorState? freshNuyen = isKarmaMode
                        ? null
                        : await _interactionCoordinator
                            .PrepareCareerManualNuyenEditAsync(CancellationToken.None)
                            .ConfigureAwait(true);
                    return (freshKarma, freshNuyen);
                },
                contextIsCurrent: DisplayStillCurrent,
                peekUncertain: PeekCareerEditUncertain);
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            if (!DisplayStillCurrent())
                return;
            if (outcome is null)
            {
                if (dialog.UncertainOutcome)
                {
                    PublishCareerEditNotice((isKarmaMode ? "karma award" : "nuyen adjustment")
                        + " result uncertain: the award may already be recorded. Review the runner before recording again.");
                }
                else if (dialog.LastRefusal is { } abandonedRefusal)
                {
                    PublishCareerEditNotice((isKarmaMode ? "karma award" : "nuyen adjustment")
                        + " not recorded: " + abandonedRefusal + " The dialog was closed; re-enter the entry when ready.");
                }

                return;
            }

            switch (outcome)
            {
                case CareerRewardsWindow.CareerKarmaAwardOutcome karma:
                    PublishCareerEditNotice("recorded karma award " + karma.DisplayLabel + ".");
                    break;

                case CareerRewardsWindow.CareerNuyenAdjustOutcome nuyen:
                    PublishCareerEditNotice("recorded nuyen adjustment " + nuyen.DisplayLabel + ".");
                    break;

                default:
                    return;
            }

            CareerManualKarmaEditorState? reopenedKarma = await _interactionCoordinator
                .PrepareCareerManualKarmaEditAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (reopenedKarma is null)
            {
                return;
            }

            karmaState = reopenedKarma;
            nuyenState = isKarmaMode
                ? null
                : await _interactionCoordinator
                    .PrepareCareerManualNuyenEditAsync(CancellationToken.None)
                    .ConfigureAwait(true);
            if (!isKarmaMode && nuyenState is null)
            {
                return;
            }
        }
    }

    private async void SectionHost_OnCareerSkillAdvanceRequested(object? sender, object? e)
    {
        await RunUiActionAsync(
            () => OpenCareerSkillAdvanceDialogAsync(),
            "career-skill-advance").ConfigureAwait(true);
    }

    private async Task OpenCareerSkillAdvanceDialogAsync()
    {
        CharacterOverviewState original = _adapter.State;
        if (original.WorkspaceId is not { } workspaceId || original.Profile?.Created != true
            || original.DisplayOwnerContext is not { IsValid: true })
        {
            PublishCareerEditNotice("open a saved Career runner before advancing skills.");
            return;
        }
        if (AttributeWorkbenchProjector.IsSr6Ruleset(original.Rules?.GameEdition))
        {
            // SR6 skill advancement cannot bind the SR6 skill identities the
            // native wizard writes (SR5 settings profiles and SR5 skill sources
            // cannot resolve them). Disclose that instead of opening a dialog
            // that fails to load with a generic error.
            PublishCareerEditNotice(
                "SR6 skill advancement is not available yet. Attribute improvement stays available on the attribute rows.");
            return;
        }
        bool SameOwner() => _adapter.State.WorkspaceId == workspaceId
            && _adapter.State.DisplayOwnerContext == original.DisplayOwnerContext && IsVisible;
        bool Current() => SameOwner() && _adapter.State.ContentRevision == original.ContentRevision
            && _adapter.State.SavedRevision == original.SavedRevision && _adapter.State.Profile?.Created == true;
        var dialog = new CareerSkillAdvanceWindow(
            workspaceId,
            prepareActive: ct => _interactionCoordinator.PrepareCareerActiveSkillAdvanceAsync(ct),
            applyActive: (request, ct) => _interactionCoordinator.TryApplyCareerActiveSkillAdvanceAsync(request, ct),
            prepareKnowledge: ct => _interactionCoordinator.PrepareCareerKnowledgeSkillAdvanceAsync(ct),
            applyKnowledge: (request, ct) => _interactionCoordinator.ApplyCareerKnowledgeSkillAdvanceAsync(request, ct),
            prepareGroup: ct => _interactionCoordinator.PrepareCareerSkillGroupAdvanceAsync(ct),
            applyGroup: (request, ct) => _interactionCoordinator.TryApplyCareerSkillGroupAdvanceAsync(request, ct),
            prepareSpecialization: ct => _interactionCoordinator.PrepareCareerSkillSpecializationAsync(ct),
            prepareSpecializationQuote: (request, ct) => _interactionCoordinator.PrepareCareerSkillSpecializationQuoteAsync(request, ct),
            applySpecialization: (request, ct) => _interactionCoordinator.TryApplyCareerSkillSpecializationAsync(request, ct),
            isCurrentContext: Current, expectedContentRevision: original.ContentRevision, isCurrentOwner: SameOwner);
        void ContextUpdated(object? sender, EventArgs args) => dialog.InvalidateIfContextChanged();
        _adapter.Updated += ContextUpdated;
        try { await dialog.ShowDialog(this).ConfigureAwait(true); }
        finally { _adapter.Updated -= ContextUpdated; }

        if (!SameOwner()) return;
        if (dialog.AdvanceApplied)
        {
            PublishCareerEditNotice("skill advancement recorded; overview refreshed.");
        }

        if (PeekCareerEditError() is { } editError)
        {
            PublishCareerEditNotice("skill advance failed: " + editError);
        }
    }

    private string? PeekCareerEditError()
    {
        string? error = _interactionCoordinator.PeekState().Error;
        return string.IsNullOrWhiteSpace(error) ? null : error;
    }

    private bool PeekCareerEditUncertain()
        => _interactionCoordinator.PeekState().UncertainMutationError == true;

    private async void SectionHost_OnCareerAttributeAdvanceRequested(object? sender, string attributeName)
    {
        await RunUiActionAsync(
            () => OpenCareerAttributeAdvanceDialogAsync(attributeName),
            "career-attribute-advance").ConfigureAwait(true);
    }

    private async Task OpenCareerAttributeAdvanceDialogAsync(string attributeName)
    {
        // SR6 runners route through Core's owner-bound advancement service
        // (dedicated dialog); the SR5 XML-mutation editor below stays SR5-only.
        if (await TryOpenSr6CareerAttributeAdvanceDialogAsync(attributeName).ConfigureAwait(true))
        {
            return;
        }

        CareerAttributeAdvanceEditorState? editor = await _interactionCoordinator
            .PrepareCareerAttributeAdvanceAsync(CancellationToken.None)
            .ConfigureAwait(true);
        if (editor is null)
        {
            return;
        }

        while (true)
        {
            var dialog = new CareerAttributeAdvanceWindow(editor, attributeName);
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            switch (outcome)
            {
                case CareerAttributeAdvanceWindow.CareerAttributeAdvanceOutcome advance:
                    CharacterCareerAttributeAdvanceReceipt? receipt = await _interactionCoordinator
                        .ApplyCareerAttributeAdvanceAsync(advance.Request, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (receipt is null)
                    {
                        return;
                    }

                    PublishCareerEditNotice(
                        "advanced " + advance.DisplayAttribute + " to " + advance.TargetValue
                        + " for " + advance.KarmaCost + " karma.");
                    break;

                case CareerAttributeAdvanceWindow.CareerAttributeCorrectionOutcome correction:
                    CharacterCareerAttributeCorrectionPlan? plan = await _interactionCoordinator
                        .CorrectCareerAttributeAdvanceAsync(correction.Request, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (plan is null)
                    {
                        return;
                    }

                    PublishCareerEditNotice(
                        "undid the last " + correction.DisplayAttribute + " advance (refunded "
                        + correction.KarmaRefund + " karma).");
                    break;

                default:
                    return;
            }

            CareerAttributeAdvanceEditorState? reopened = await _interactionCoordinator
                .PrepareCareerAttributeAdvanceAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (reopened is null)
            {
                return;
            }

            editor = reopened;
        }
    }

    /// <summary>
    /// Opens the SR6 attribute advancement dialog for the currently open SR6
    /// runner. Returns false (without publishing an error) when the dossier is
    /// not SR6 or the client surface is unavailable, so the caller falls back
    /// to the SR5 editor route.
    /// </summary>
    private async Task<bool> TryOpenSr6CareerAttributeAdvanceDialogAsync(string attributeName)
    {
        // Observe transitions synchronously, before queued UI refreshes can
        // coalesce a workspace switch away and back into the same identity.
        CharacterOverviewState entryState = _interactionCoordinator.PeekState();
        if (entryState.WorkspaceId is not { } entryWorkspaceId)
        {
            return false;
        }
        long entryRevision = entryState.ContentRevision;
        OwnerContextStamp? entryOwner = entryState.DisplayOwnerContext;
        long entryGeneration = _careerDisplayContext.Capture(entryWorkspaceId, entryOwner);

        bool DisplayStillCurrent()
        {
            CharacterOverviewState now = _interactionCoordinator.PeekState();
            return now.WorkspaceId == entryWorkspaceId
                && EqualityComparer<OwnerContextStamp?>.Default.Equals(now.DisplayOwnerContext, entryOwner)
                && _careerDisplayContext.IsCurrent(entryGeneration, now.WorkspaceId, now.DisplayOwnerContext);
        }

        bool ContextStillCurrent() => DisplayStillCurrent()
            && _interactionCoordinator.PeekState().ContentRevision == entryRevision;

        Sr6CareerAttributeAdvanceEditorSnapshot? editor = await _interactionCoordinator
            .PrepareSr6CareerAttributeAdvanceAsync(CancellationToken.None)
            .ConfigureAwait(true);
        if (!ContextStillCurrent())
        {
            // The quote was prepared for a runner that is no longer displayed.
            return true;
        }
        if (editor is null)
        {
            return false;
        }

        while (true)
        {
            if (!ContextStillCurrent())
            {
                return true;
            }
            var dialog = new Sr6CareerAttributeAdvanceWindow(editor, attributeName);
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            if (!ContextStillCurrent())
            {
                // Owner/workspace/revision moved while the dialog was open; the
                // quote-borne stamps inside it are stale by construction.
                return true;
            }
            if (outcome is not Sr6CareerAttributeAdvanceWindow.Sr6CareerAttributeAdvanceOutcome advance)
            {
                return true;
            }

            Sr6CareerAttributeReceipt? receipt = await _interactionCoordinator
                .ApplySr6CareerAttributeAdvanceAsync(advance.Owner, advance.Command, CancellationToken.None)
                .ConfigureAwait(true);
            if (receipt is null)
            {
                return true;
            }

            // Only the exact committed receipt may advance the expected view
            // revision. An unrelated write or a display transition still ends
            // this interaction, including a switch away and back.
            if (!DisplayStillCurrent()
                || receipt.Command.OperationId != advance.Command.OperationId
                || receipt.Command.Quote.WorkspaceId != entryWorkspaceId
                || receipt.CommittedRevision <= entryRevision
                || _interactionCoordinator.PeekState().ContentRevision != receipt.CommittedRevision)
            {
                // The commit itself is durable and retained, but this window is
                // no longer showing the runner it was made against: keep the
                // commit, never display it in another context.
                return true;
            }

            entryRevision = receipt.CommittedRevision;
            long noticeRevision = receipt.CommittedRevision;

            PublishCareerEditNotice(
                "advanced " + advance.DisplayAttribute + " to natural rating "
                + advance.TargetNaturalRating.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " for " + advance.Command.Quote.KarmaCost.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " karma (remaining " + receipt.KarmaAfter + ").", () => DisplayStillCurrent()
                    && _interactionCoordinator.PeekState().ContentRevision == noticeRevision);

            Sr6CareerAttributeAdvanceEditorSnapshot? reopened = await _interactionCoordinator
                .PrepareSr6CareerAttributeAdvanceAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (reopened is null)
            {
                return true;
            }
            if (!ContextStillCurrent())
            {
                return true;
            }

            editor = reopened;
        }
    }

    private void PublishCareerEditNotice(string message, Func<bool>? contextStillCurrent = null)
    {
        string notice = CareerEditNoticePrefix + message;
        try
        {
            CareerDisplayContext.PostIfCurrent(callback => Dispatcher.UIThread.Post(callback), contextStillCurrent ?? (() => true), () =>
            {
                try
                {
                    _controls.SectionHost.SetNotice(notice);
                }
                catch (Exception)
                {
                    // Status surface is best-effort; never fail a completed mutation on notice delivery.
                }
            });
        }
        catch (Exception)
        {
            // Same best-effort contract when the dispatcher itself is unavailable.
        }
    }
}

internal sealed class CareerDisplayContext
{
    private readonly object _gate = new();
    private long _generation;
    private CharacterWorkspaceId? _workspaceId;
    private OwnerContextStamp? _owner;

    public void Observe(CharacterWorkspaceId? workspaceId, OwnerContextStamp? owner)
    {
        lock (_gate)
        {
            if (_workspaceId == workspaceId && _owner == owner) return;
            _workspaceId = workspaceId;
            _owner = owner;
            _generation++;
        }
    }

    public long Capture(CharacterWorkspaceId? workspaceId, OwnerContextStamp? owner)
    {
        lock (_gate)
        {
            Observe(workspaceId, owner);
            return _generation;
        }
    }

    public bool IsCurrent(long generation, CharacterWorkspaceId? workspaceId, OwnerContextStamp? owner)
    {
        lock (_gate)
        {
            return generation == _generation && workspaceId == _workspaceId && owner == _owner;
        }
    }

    public void Invalidate()
    {
        lock (_gate) _generation++;
    }

    internal static void PostIfCurrent(Action<Action> post, Func<bool> current, Action publish)
    {
        if (!current()) return;
        post(() => { if (current()) publish(); });
    }
}
