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
