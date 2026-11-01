using System;
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
