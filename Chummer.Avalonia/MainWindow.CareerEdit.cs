using System;
using Chummer.Application.Owners;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private const string CareerEditNoticePrefix = "Career advancement: ";

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
        CareerManualKarmaEditorState? karmaState = await _interactionCoordinator
            .PrepareCareerManualKarmaEditAsync(CancellationToken.None)
            .ConfigureAwait(true);
        if (karmaState is null)
        {
            return;
        }

        CareerManualNuyenEditorState? nuyenState = isKarmaMode
            ? null
            : await _interactionCoordinator
                .PrepareCareerManualNuyenEditAsync(CancellationToken.None)
                .ConfigureAwait(true);
        if (!isKarmaMode && nuyenState is null)
        {
            return;
        }

        while (true)
        {
            var dialog = new CareerRewardsWindow(isKarmaMode, karmaState, nuyenState);
            object? outcome = await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
            switch (outcome)
            {
                case CareerRewardsWindow.CareerKarmaAwardOutcome karma:
                    await _interactionCoordinator
                        .ApplyCareerManualKarmaEditAsync(karma.Request, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (PeekCareerEditError() is { } karmaError)
                    {
                        PublishCareerEditNotice("karma award failed: " + karmaError);
                        return;
                    }

                    PublishCareerEditNotice("recorded karma award " + karma.DisplayLabel + ".");
                    break;

                case CareerRewardsWindow.CareerNuyenAdjustOutcome nuyen:
                    await _interactionCoordinator
                        .ApplyCareerManualNuyenEditAsync(nuyen.Request, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (PeekCareerEditError() is { } nuyenError)
                    {
                        PublishCareerEditNotice("nuyen adjustment failed: " + nuyenError);
                        return;
                    }

                    PublishCareerEditNotice("recorded nuyen adjustment " + nuyen.DisplayLabel + ".");
                    break;

                default:
                    return;
            }

            // Reopen with a fresh prepare so consecutive awards chain cleanly.
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
        // R109 fix 4: the entire dialog loop is bound to the owner/workspace/
        // revision context captured BEFORE the first await. After every await
        // (quote, dialog, apply, requote) the window rechecks that context and
        // silently abandons the loop when the displayed runner moved on, so a
        // committed receipt is never published into a different runner or a
        // stale owner display (A-to-B, A-to-B-to-A).
        CharacterOverviewState entryState = _interactionCoordinator.PeekState();
        if (entryState.WorkspaceId is not { } entryWorkspaceId)
        {
            return false;
        }
        long entryRevision = entryState.ContentRevision;
        OwnerContextStamp? entryOwner = entryState.DisplayOwnerContext;

        bool ContextStillCurrent()
        {
            CharacterOverviewState now = _interactionCoordinator.PeekState();
            return now.WorkspaceId == entryWorkspaceId
                && now.ContentRevision == entryRevision
                && EqualityComparer<OwnerContextStamp?>.Default.Equals(now.DisplayOwnerContext, entryOwner);
        }

        Sr6CareerAttributeAdvanceEditorSnapshot? editor = await _interactionCoordinator
            .PrepareSr6CareerAttributeAdvanceAsync(CancellationToken.None)
            .ConfigureAwait(true);
        if (editor is null)
        {
            return false;
        }
        if (!ContextStillCurrent())
        {
            // The quote was prepared for a runner that is no longer displayed.
            return true;
        }

        while (true)
        {
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
            if (!ContextStillCurrent())
            {
                // The commit itself is durable and retained, but this window is
                // no longer showing the runner it was made against: keep the
                // commit, never display it in another context.
                return true;
            }

            PublishCareerEditNotice(
                "advanced " + advance.DisplayAttribute + " to natural rating "
                + advance.TargetNaturalRating.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " for " + receipt.ExpenseAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " karma (remaining " + receipt.KarmaAfter + ").");

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

    private void PublishCareerEditNotice(string message)
    {
        string notice = CareerEditNoticePrefix + message;
        try
        {
            Dispatcher.UIThread.Post(() =>
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
