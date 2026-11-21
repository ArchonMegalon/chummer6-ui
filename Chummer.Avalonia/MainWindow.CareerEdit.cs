using System;
using Chummer.Application.Owners;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Linq;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private const string CareerEditNoticePrefix = "Career advancement: ";
    private readonly CareerDisplayContext _careerDisplayContext = new();

    // Single-instance guard for the career rewards modal. A trigger event that
    // fires while a rewards dialog is already open must NOT stack a second
    // modal: the open dialog's own loop already serializes follow-up rewards
    // rounds after each acknowledged/recorded outcome, and stacked Confirmed
    // dialogs otherwise strand unacknowledged records behind hidden windows.
    private bool _careerRewardsDialogOpen;

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
        if (entry.WorkspaceId is not { } entryWorkspaceId || entry.DisplayOwnerContext is not { IsValid: true } entryOwner)
            return;
        long entryGeneration = _careerDisplayContext.Capture(entryWorkspaceId, entry.DisplayOwnerContext);
        bool DisplayStillCurrent()
        {
            CharacterOverviewState current = _interactionCoordinator.PeekState();
            return IsVisible && current.WorkspaceId == entryWorkspaceId
                && current.DisplayOwnerContext == entry.DisplayOwnerContext
                && _careerDisplayContext.IsCurrent(entryGeneration, current.WorkspaceId, current.DisplayOwnerContext);
        }

        if (_careerRewardsDialogOpen)
            return;
        _careerRewardsDialogOpen = true;
        try
        {
            await OpenCareerRewardsDialogCoreAsync(isKarmaMode, entryWorkspaceId, entryOwner, DisplayStillCurrent).ConfigureAwait(true);
        }
        finally
        {
            _careerRewardsDialogOpen = false;
        }
    }

    private async Task OpenCareerRewardsDialogCoreAsync(bool isKarmaMode, CharacterWorkspaceId entryWorkspaceId, OwnerContextStamp entryOwner, Func<bool> DisplayStillCurrent)
    {
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
            var dialog = new CareerRewardsWindow(
                isKarmaMode,
                karmaState,
                nuyenState,
                applyKarma: (request, ct) => _interactionCoordinator.ApplyCareerManualKarmaEditAsync(request, entryOwner, ct),
                applyNuyen: (request, ct) => _interactionCoordinator.ApplyCareerManualNuyenEditAsync(request, entryOwner, ct),
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
                reconcile: (operationId, ct) => _interactionCoordinator.ReconcileCareerRewardAsync(entryOwner, entryWorkspaceId, operationId, ct),
                pendingResult: _interactionCoordinator.GetPendingCareerReward(entryOwner, entryWorkspaceId),
                acknowledge: operationId => DisplayStillCurrent()
                    && _interactionCoordinator.AcknowledgeCareerReward(entryOwner, entryWorkspaceId, operationId));
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            if (!DisplayStillCurrent())
                return;
            if (outcome is null)
            {
                if (dialog.CloseNotice is { } closeNotice)
                    PublishCareerEditNotice(closeNotice);

                return;
            }

            switch (outcome)
            {
                case CareerRewardsWindow.CareerRewardReconciledOutcome:
                    PublishCareerEditNotice("The previous reward is confirmed recorded. Review the runner before entering another reward.");
                    break;

                case CareerRewardsWindow.CareerRewardAcknowledgedOutcome:
                    break;

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

    private async void SectionHost_OnCareerAttributeAdvanceRequested(object? sender, string attributeName)
    {
        // Controller9547 / gen10y ring evidence: the initiating focus must be
        // captured SYNCHRONOUSLY at handler entry - the Click dispatch still
        // holds the row focused, but the open route's own awaits can let a
        // parity-grid rebuild detach the row and CLEAR focus before the
        // capture inside the route runs (gen10y: capture type=<null>).
        (IInputElement? initiatingFocus, string? initiatingFocusName) =
            CaptureInitiatingFocusForAdvanceDialog(this);
        // Controller9568: the restore cycle begins HERE - before the modal
        // can open - so any queued restore from a PREVIOUS dialog cycle is
        // invalidated before this cycle's modal opens; an older queued
        // restore can otherwise focus behind the next open modal.
        int restoreGeneration = Sr6FocusRestoreDiagnostics.BeginRestoreCycle(this);
        await RunUiActionAsync(
            () => OpenCareerAttributeAdvanceDialogAsync(
                attributeName, initiatingFocus, initiatingFocusName, restoreGeneration),
            "career-attribute-advance").ConfigureAwait(true);
    }

    private async Task OpenCareerAttributeAdvanceDialogAsync(
        string attributeName,
        IInputElement? initiatingFocus,
        string? initiatingFocusName,
        int restoreGeneration)
    {
        // SR6 runners route through Core's owner-bound advancement service
        // (dedicated dialog); the SR5 XML-mutation editor below stays SR5-only.
        if (await TryOpenSr6CareerAttributeAdvanceDialogAsync(attributeName, initiatingFocus, initiatingFocusName, restoreGeneration).ConfigureAwait(true))
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
    private async Task<bool> TryOpenSr6CareerAttributeAdvanceDialogAsync(
        string attributeName,
        IInputElement? initiatingFocus,
        string? initiatingFocusName,
        int restoreGeneration)
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
            // The initiating focus was captured at HANDLER ENTRY (pre-await,
            // gen10y evidence) and passed in; the in-route capture was too
            // late - the open route's awaits can clear focus via a parity
            // -grid rebuild before it ran.
            var dialog = new Sr6CareerAttributeAdvanceWindow(editor, attributeName);
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            // The modal's close sequence is still unwinding when
            // ShowDialog's task completes; restore on the NEXT dispatcher
            // pass so the window's own focus cleanup cannot clobber the
            // restored focus (gen10v evidence: an immediate restore was
            // observed unfocused by the journey's single-shot probe).
            // The modal's close sequence keeps unwinding after ShowDialog's
            // task completes; the window's own focus cleanup can land after
            // any single restore attempt and clear it (gen10v/gen10w
            // evidence: focusOwner=none across the entire observation). The
            // restore is therefore SELF-VERIFYING and BOUNDED-RETRIED: each
            // attempt re-resolves the live row, focuses it, and confirms
            // through the focus manager before succeeding. Focus management
            // only - never a re-press (Controller9415).
            // Gen10x ring evidence: rapid successive modals (the natural
            // -limit demo) posted ~10 concurrent restore loops whose delayed
            // continuations interleaved and clobbered each other's focus.
            // Each new cycle therefore INVALIDATES the pending older loops
            // via a generation token - a stale loop exits silently instead
            // of fighting the newest capture over keyboard focus.
            Sr6FocusRestoreDiagnostics.Record("capture (handler-entry) type="
                + (initiatingFocus?.GetType().Name ?? "<null>")
                + " name=" + (initiatingFocusName ?? "<null>"));
            // Controller9568: the cycle was begun at HANDLER ENTRY (pre-open)
            // and is owner-scoped; the loop checks the CURRENT generation
            // immediately before every Focus attempt and exits silently when
            // a newer cycle took over.
            _ = RunDeferredFocusRestoreAsync(this, initiatingFocus, initiatingFocusName, restoreGeneration, DisplayStillCurrent);
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

            // Gen9 finding 4: a successful Apply must CLOSE the dialog and
            // return focus to the character. The committed advance is done;
            // reopening the next operation automatically made every ordinary
            // state refresh look like another dialog and forced the player to
            // Cancel repeatedly (six observed auto-reopens in phaseA). An
            // explicit Advance action opens the next operation.
            return true;
        }
    }

    /// <summary>
    /// Controller9568: the deferred focus-restore loop, extracted so the
    /// cross-modal regression can drive cycles directly. Owner-scoped: the
    /// loop exits when a newer cycle takes over (generation token) or when
    /// the cycle no longer belongs to this owner (owner/context change).
    /// Focus management only - never a re-press (Controller9415).
    /// </summary>
    internal static Task RunDeferredFocusRestoreAsync(
        TopLevel owner,
        IInputElement? initiatingFocus,
        string? initiatingFocusName,
        int restoreGeneration,
        Func<bool>? displayStillCurrent = null)
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // Controller9589 correction B (2026-11-21): the helper receives ONE combined
            // live predicate built here - cycle currency + owner-window lifetime + the
            // display context - so each helper focus attempt consults all three facts
            // as they are AT THAT MOMENT, not as they were when the loop started.
            // The revision is deliberately excluded (successful Apply must restore).
            Func<bool> deferredFocusGuard = BuildDeferredFocusGuard(restoreGeneration, owner, displayStillCurrent);
            for (int restoreAttempt = 0; restoreAttempt < 10; restoreAttempt++)
            {
                if (!Sr6FocusRestoreDiagnostics.IsCurrentCycle(restoreGeneration, owner))
                {
                    Sr6FocusRestoreDiagnostics.Record("cycle" + restoreGeneration + " stale-exit attempt" + restoreAttempt);
                    return;
                }
                // Controller9579 gap A: the display context (owner window +
                // workspace + account) must still be current immediately before
                // every Focus attempt - including the by-name re-resolve, which
                // must never land keyboard focus inside a switched-away workspace.
                // Deliberately NOT the revision: the revision advances on every
                // Apply and post-Apply focus restore must keep working.
                if (displayStillCurrent is not null && !displayStillCurrent())
                {
                    Sr6FocusRestoreDiagnostics.Record("cycle" + restoreGeneration + " context-switch refusal attempt" + restoreAttempt);
                    return;
                }
                // Controller9579 gap B: refuse on owner-close. A closed owner
                // window must not receive focus restoration.
                if (owner is Window ownerWindow && !ownerWindow.IsVisible)
                {
                    Sr6FocusRestoreDiagnostics.Record("cycle" + restoreGeneration + " owner-closed refusal attempt" + restoreAttempt);
                    return;
                }
                // Controller9591 gap 2 + Controller9589 correction B (2026-11-21):
                // hand the COMBINED live guard to the helper - cycle currency,
                // owner-window lifetime AND the display predicate are re-evaluated
                // at every Focus attempt inside the helper, so a reentrant failed
                // first Focus (or any switch between the two attempts) cannot let
                // a stale cycle or a closed owner receive focus. The revision is
                // deliberately NOT part of this guard: a successful Apply advances
                // the revision and focus restoration must still work.
                if (RestoreInitiatingFocusAfterAdvanceDialogClose(owner, initiatingFocus, initiatingFocusName, deferredFocusGuard))
                {
                    Sr6FocusRestoreDiagnostics.Record("cycle" + restoreGeneration + " restored attempt" + restoreAttempt);
                    return;
                }
                await Task.Delay(60).ConfigureAwait(true);
            }
            Sr6FocusRestoreDiagnostics.Record("cycle" + restoreGeneration + " exhausted");
        });
    }

    /// <summary>
    /// Controller9589 correction B: builds the combined live focus guard used by
    /// the deferred restore loop - cycle currency + owner-window lifetime + the
    /// display predicate, evaluated at call time. Exposed for the focused
    /// between-attempt regressions so tests exercise exactly the predicate the
    /// production loop passes to the helper. The revision is deliberately NOT a
    /// term: a successful Apply advances the revision and must still restore.
    /// </summary>
    internal static Func<bool> BuildDeferredFocusGuard(
        int restoreGeneration,
        TopLevel owner,
        Func<bool>? displayStillCurrent)
    {
        return () =>
        {
            if (!Sr6FocusRestoreDiagnostics.IsCurrentCycle(restoreGeneration, owner))
            {
                return false;
            }
            if (owner is Window guardOwnerWindow && !guardOwnerWindow.IsVisible)
            {
                return false;
            }
            return displayStillCurrent?.Invoke() ?? true;
        };
    }

    /// <summary>
    /// Captures the initiating focus immediately before an advancement modal
    /// opens (Controller9547): the focused element plus its resolvable name,
    /// so the close path can restore focus even when the parity grid rebuilt
    /// the row while the modal was open.
    /// </summary>
    internal static (IInputElement? Element, string? Name) CaptureInitiatingFocusForAdvanceDialog(TopLevel owner)
    {
        try
        {
            IInputElement? focused = owner.FocusManager?.GetFocusedElement();
            string? name = (focused as Control)?.Name;
            return (focused, name);
        }
        catch (Exception)
        {
            // Focus capture is best-effort; an advancement must never fail
            // because the platform focus manager was unavailable.
            return (null, null);
        }
    }

    /// <summary>
    /// Restores the initiating focus after an advancement modal closes
    /// (gen9 finding 4 / Controller9547): prefer the captured instance while
    /// it is still visible, then re-resolve by name for a parity-grid
    /// rebuild. Returns false when no visible target could be focused; the
    /// completed mutation is never failed on focus restoration.
    /// Controller9591 gap 2 + Controller9589 correction B: the live guard is
    /// re-checked immediately before EVERY Focus attempt - the same-instance
    /// attempt AND the by-name fallback after a failed/reentrant attempt - so
    /// a context that switched between attempts never receives focus. The
    /// guard the deferred loop passes is the COMBINED predicate (restore-cycle
    /// currency + owner-window lifetime + display context); the revision is
    /// deliberately excluded so a successful Apply still restores focus.
    /// </summary>
    internal static bool RestoreInitiatingFocusAfterAdvanceDialogClose(
        TopLevel owner,
        IInputElement? initiatingFocus,
        string? initiatingFocusName,
        Func<bool>? displayStillCurrent = null)
    {
        try
        {
            // A detached (rebuilt-over) initiating instance can still report
            // IsEffectivelyVisible=true, but Focus() on a control with no
            // visual root fails: fall through to the name re-resolve instead
            // of returning that failure (rebuild-phase evidence).
            if (initiatingFocus is Control same)
            {
                // Controller9591 gap 2: the guard is evaluated again at THIS
                // branch entry - including after any prior failed attempt -
                // before the same-instance Focus is allowed.
                if (displayStillCurrent is not null && !displayStillCurrent())
                {
                    Sr6FocusRestoreDiagnostics.Record("guard refusal before same-instance focus");
                    return false;
                }
                if (same.IsEffectivelyVisible
                    && ReferenceEquals(same.GetVisualRoot(), owner)
                    && same.Focus())
                {
                    bool confirmed = ReferenceEquals(owner.FocusManager?.GetFocusedElement(), same);
                    Sr6FocusRestoreDiagnostics.Record("same-instance focused=" + confirmed);
                    Sr6FocusRestoreDiagnostics.RecordLastRestoreOutcome(confirmed, "same-instance");
                    return confirmed;
                }
            }
            if (!string.IsNullOrEmpty(initiatingFocusName))
            {
                // Controller9591 gap 2: the guard is evaluated again before
                // the by-name fallback - including when this attempt follows
                // a FAILED same-instance attempt - so a context that switched
                // between attempts never receives keyboard focus.
                if (displayStillCurrent is not null && !displayStillCurrent())
                {
                    Sr6FocusRestoreDiagnostics.Record("guard refusal before by-name focus");
                    return false;
                }
                Control? rebuilt = owner.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(candidate => string.Equals(candidate.Name, initiatingFocusName, StringComparison.Ordinal)
                        && candidate.IsEffectivelyVisible);
                if (rebuilt is not null && rebuilt.Focus()
                    && ReferenceEquals(owner.FocusManager?.GetFocusedElement(), rebuilt))
                {
                    Sr6FocusRestoreDiagnostics.Record("by-name '" + initiatingFocusName + "' focused=True");
                    return true;
                }
                Sr6FocusRestoreDiagnostics.Record("by-name '" + initiatingFocusName + "' focused=False");
            }
            if (initiatingFocus is not null || !string.IsNullOrEmpty(initiatingFocusName))
            {
                Sr6FocusRestoreDiagnostics.Record("no visible target (name='" + (initiatingFocusName ?? "<null>") + "')");
            }
            return false;
        }
        catch (Exception ex)
        {
            // Focus restoration is best-effort accessibility behavior; never
            // fail a completed mutation on it.
            Sr6FocusRestoreDiagnostics.Record("exception " + ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Bounded diagnostics ring for the advancement-modal focus restoration
    /// (gen10w): the journey dumps the most recent attempts when the
    /// focus-return proof fails, so failures carry measured facts instead of
    /// theory. Never carries user data - only restoration attempt outcomes.
    /// </summary>
    internal static class Sr6FocusRestoreDiagnostics
    {
        private static readonly object Gate = new();
        private static readonly List<string> Entries = new();

        public static void Record(string entry)
        {
            lock (Gate)
            {
                Entries.Add(entry);
                if (Entries.Count > 64)
                {
                    Entries.RemoveRange(0, Entries.Count - 64);
                }
            }
        }

        private static int _cycle;
        private static object? _cycleOwner;

        /// <summary>Begins an owner-scoped restore cycle and invalidates every pending older one.</summary>
        public static int BeginRestoreCycle(object owner)
        {
            lock (Gate)
            {
                _cycle++;
                _cycleOwner = owner;
                Record("cycle" + _cycle + " begin");
                return _cycle;
            }
        }

        /// <summary>True only when the generation is current AND still owned by this owner (owner/context change cancels).</summary>
        public static bool IsCurrentCycle(int generation, object owner)
        {
            lock (Gate)
            {
                return generation == _cycle && ReferenceEquals(_cycleOwner, owner);
            }
        }

        private static bool _lastRestoreOutcome;
        private static string _lastRestoreBranch = string.Empty;

        /// <summary>Records the most recent restore attempt's outcome for regression assertions.</summary>
        public static void RecordLastRestoreOutcome(bool focused, string branch)
        {
            lock (Gate)
            {
                _lastRestoreOutcome = focused;
                _lastRestoreBranch = branch;
            }
        }

        public static (bool Focused, string Branch) LastRestoreOutcome()
        {
            lock (Gate)
            {
                return (_lastRestoreOutcome, _lastRestoreBranch);
            }
        }

        public static IReadOnlyList<string> Snapshot()
        {
            lock (Gate)
            {
                return Entries.ToArray();
            }
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
