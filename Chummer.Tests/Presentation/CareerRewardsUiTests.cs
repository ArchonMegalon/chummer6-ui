using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Headless UI tests for the T65 career rewards surface: strip visibility
/// gating and the request shapes the rewards dialog emits (Gain/Spend action,
/// revision-bound editor state, positive-amount guard).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CareerRewardsUiTests
{
    [TestMethod]
    public void Rewards_strip_renders_on_career_sections_only()
    {
        WithHeadlessUi(() =>
        {
            Window careerWindow = ShowHost(new SectionHostControl());
            var careerHost = (SectionHostControl)careerWindow.Content!;
            careerHost.SetState(BuildHostState("sr5", created: true, karma: 12, nuyen: 15000m));
            PumpLayout(careerHost);

            Border? strip = FindByName<Border>(careerHost, "CareerRewardsBorder");
            Assert.IsNotNull(strip, "CareerRewardsBorder must exist in the context area.");
            Assert.IsTrue(strip.IsVisible, "Strip must be visible in career mode.");
            Assert.IsNotNull(FindByName<Button>(careerHost, "CareerAwardKarma"), "Award Karma button must exist.");
            Assert.IsNotNull(FindByName<Button>(careerHost, "CareerAdjustNuyen"), "Adjust Nuyen button must exist.");

            careerWindow.Close();
            Dispatcher.UIThread.RunJobs();

            Window creationWindow = ShowHost(new SectionHostControl());
            var creationHost = (SectionHostControl)creationWindow.Content!;
            creationHost.SetState(BuildHostState("sr5", created: false, karma: 0, nuyen: 0m));
            PumpLayout(creationHost);

            Border? hiddenStrip = FindByName<Border>(creationHost, "CareerRewardsBorder");
            Assert.IsNotNull(hiddenStrip, "CareerRewardsBorder element must exist even when hidden.");
            Assert.IsFalse(hiddenStrip.IsVisible, "Strip must be hidden outside career mode.");

            creationWindow.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Karma_award_outcome_carries_gain_and_revision_binding()
    {
        CareerRewardsWindow.CareerKarmaAwardOutcome? outcome = DriveKarmaDialog(gain: true, amount: "10", reason: "Extraction run");

        Assert.IsNotNull(outcome);
        Assert.AreEqual("+10 karma (Extraction run)", outcome.DisplayLabel);
        Assert.AreEqual(CharacterCareerManualKarmaAction.Gain, outcome.Request.Action);
        Assert.AreEqual(10, outcome.Request.Amount);
        Assert.AreEqual("Extraction run", outcome.Request.Reason);
        Assert.IsFalse(outcome.Request.Refund);
        Assert.IsFalse(outcome.Request.KarmaNuyenExchange);
    }

    [TestMethod]
    public void Karma_spend_outcome_flips_action_to_spend()
    {
        CareerRewardsWindow.CareerKarmaAwardOutcome? outcome = DriveKarmaDialog(gain: false, amount: "5", reason: "Training");

        Assert.IsNotNull(outcome);
        Assert.AreEqual(CharacterCareerManualKarmaAction.Spend, outcome.Request.Action);
        Assert.AreEqual(5, outcome.Request.Amount);
        Assert.IsTrue(outcome.DisplayLabel.StartsWith('-'), "Spend labels open with '-'.");
    }

    [TestMethod]
    public void Nuyen_adjust_outcome_carries_amount_and_action()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            var dialog = new CareerRewardsWindow(isKarmaMode: false, MakeKarmaEditor(), MakeNuyenEditor(4200m));
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "800";
            TextBox reason = FindByAutomation<TextBox>(dialog, "career-rewards-reason")!;
            reason.Text = "Payment for run";

            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            WaitCompletion(dialogTask, timeoutMs: 3000);
            var outcome = (CareerRewardsWindow.CareerNuyenAdjustOutcome)dialogTask.GetAwaiter().GetResult()!;

            Assert.AreEqual(CharacterCareerManualNuyenAction.Gain, outcome.Request.Action);
            Assert.AreEqual(800, outcome.Request.Amount);
            Assert.AreEqual("Payment for run", outcome.Request.Reason);
            Assert.AreEqual(0m, outcome.Request.Percent);

            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Dialog_rejects_nonpositive_amounts()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            var dialog = new CareerRewardsWindow(isKarmaMode: true, MakeKarmaEditor(), null);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "0";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            TextBlock balance = FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!;
            Assert.IsTrue((balance.Text ?? string.Empty).Contains("positive"), "Expected the validation hint.");

            dialog.Close(null);
            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Inline_apply_refusal_keeps_dialog_open_with_entry_intact_and_requires_reconfirm()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            CareerManualKarmaEditorState state = MakeKarmaEditor();
            int applyCount = 0;
            int reprepareCount = 0;
            var dialog = new CareerRewardsWindow(
                isKarmaMode: true,
                state,
                null,
                applyKarma: (request, _) =>
                {
                    applyCount++;
                    if (applyCount == 1)
                    {
                        return Task.FromResult(Refused("expected content revision 9 but the workspace is at 10"));
                    }

                    return Task.FromResult(Confirmed(request.OperationId));
                },
                applyNuyen: null,
                reprepare: _ =>
                {
                    reprepareCount++;
                    CareerManualKarmaEditorState? freshKarma = MakeKarmaEditor(revision: 10L);
                    return Task.FromResult((freshKarma, (CareerManualNuyenEditorState?)null));
                });
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "7";
            TextBox reason = FindByAutomation<TextBox>(dialog, "career-rewards-reason")!;
            reason.Text = "Refused run";
            RadioButton spend = FindByAutomation<RadioButton>(dialog, "career-rewards-spend")!;
            spend.IsChecked = true;

            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // First press: refused. The dialog must stay open, keep the entry,
            // disclose the reason and swap in the freshly prepared state.
            Assert.AreEqual(1, applyCount);
            Assert.AreEqual(1, reprepareCount, "a refusal must re-prepare a fresh editor state.");
            Assert.IsFalse(dialogTask.IsCompleted, "a refused apply must not close the dialog.");
            Assert.AreEqual("7", amount.Text, "the refused amount must be retained.");
            Assert.AreEqual("Refused run", reason.Text, "the refused notes must be retained.");
            Assert.IsTrue(spend.IsChecked == true, "the refused direction must be retained.");
            TextBlock balance = FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!;
            string refusalText = balance.Text ?? string.Empty;
            Assert.IsTrue(refusalText.Contains("Not recorded:"), $"status line must show the refusal, got: {refusalText}");
            Assert.IsTrue(refusalText.Contains("expected content revision 9"), $"status line must show the reason, got: {refusalText}");
            Assert.IsTrue(refusalText.Contains("press Record again"), $"status line must require an explicit re-confirm, got: {refusalText}");
            Assert.AreEqual("expected content revision 9 but the workspace is at 10", dialog.LastRefusal);

            // Second press: explicit re-confirm against the fresh state.
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(2, applyCount);
            Assert.AreEqual(1, reprepareCount, "a successful re-confirm must not re-prepare again.");
            WaitCompletion(dialogTask, timeoutMs: 3000);
            var outcome = (CareerRewardsWindow.CareerKarmaAwardOutcome?)dialogTask.GetAwaiter().GetResult();
            Assert.IsNotNull(outcome, "the re-confirmed apply must close with the outcome.");
            Assert.AreEqual(CharacterCareerManualKarmaAction.Spend, outcome!.Request.Action);
            Assert.AreEqual(7, outcome.Request.Amount);
            Assert.AreEqual("Refused run", outcome.Request.Reason);
            Assert.AreEqual(10L, outcome.Request.ExpectedContentRevision, "the re-confirm must bind to the freshly prepared revision.");
            Assert.IsNull(dialog.LastRefusal, "a successful apply clears the refusal.");

            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Inline_apply_success_closes_with_outcome_on_first_press()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            CareerManualKarmaEditRequest? captured = null;
            var dialog = new CareerRewardsWindow(
                isKarmaMode: true,
                MakeKarmaEditor(),
                null,
                applyKarma: (request, _) =>
                {
                    captured = request;
                    return Task.FromResult(Confirmed(request.OperationId));
                },
                applyNuyen: null,
                reprepare: null);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "3";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            WaitCompletion(dialogTask, timeoutMs: 3000);
            var outcome = (CareerRewardsWindow.CareerKarmaAwardOutcome?)dialogTask.GetAwaiter().GetResult();
            Assert.IsNotNull(outcome, "an accepted inline apply must close with the outcome.");
            Assert.AreEqual("+3 karma", outcome!.DisplayLabel);
            Assert.IsNotNull(captured);
            Assert.AreEqual(3, captured!.Amount);

            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Inline_apply_is_single_flight_until_refusal_reprepare_finishes()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            var applyGate = new TaskCompletionSource<CareerRewardApplyResult>();
            var prepareGate = new TaskCompletionSource<(CareerManualKarmaEditorState?, CareerManualNuyenEditorState?)>();
            int applies = 0, prepares = 0;
            long appliedRevision = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (request, _) =>
                {
                    appliedRevision = request.ExpectedContentRevision;
                    return ++applies == 1 ? applyGate.Task : Task.FromResult(Confirmed(request.OperationId));
                }, null,
                _ => { prepares++; return prepareGate.Task; });
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "7";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies);
            Assert.IsFalse(apply.IsEnabled);
            applyGate.SetResult(Refused("stale revision"));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(1, prepares);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies, "reprepare is part of the same flight");
            prepareGate.SetResult((MakeKarmaEditor(10), null));
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(apply.IsEnabled);
            Assert.IsFalse(result.IsCompleted);
            Assert.AreEqual(1, applies, "refresh must never replay the entry");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitCompletion(result, 3000);
            Assert.AreEqual(2, applies);
            Assert.AreEqual(10L, appliedRevision);
            owner.Close();
        });
    }

    [TestMethod]
    public void Closed_rewards_dialog_does_not_reprepare_or_dispatch_again()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            var pending = new TaskCompletionSource<CareerRewardApplyResult>();
            int applies = 0, prepares = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (_, _) => { applies++; return pending.Task; }, null,
                _ =>
                {
                    prepares++;
                    return Task.FromResult(((CareerManualKarmaEditorState?)MakeKarmaEditor(10), (CareerManualNuyenEditorState?)null));
                });
            _ = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "7";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dialog.Close();
            pending.SetResult(Refused("stale revision"));
            Dispatcher.UIThread.RunJobs();
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies);
            Assert.AreEqual(0, prepares);
            owner.Close();
        });
    }

    [TestMethod]
    public void Refusal_cannot_rebind_retained_input_to_another_runner_or_owner()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            bool current = true;
            int applies = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (_, _) => { applies++; return Task.FromResult(Refused("stale runner")); }, null,
                _ => Task.FromResult(((CareerManualKarmaEditorState?)(MakeKarmaEditor(10) with
                    { WorkspaceId = new CharacterWorkspaceId("another-runner") }), (CareerManualNuyenEditorState?)null)),
                () => current);
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "7";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(result.IsCompleted);
            Assert.AreEqual("7", amount.Text);
            StringAssert.Contains(FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text!, "original runner");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies, "A mismatched refreshed runner invalidates the editor.");
            current = false;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies, "an owner transition requires a new editor gesture");
            dialog.Close();
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(false, true)]
    public void Committed_then_fault_or_cancel_keeps_entry_uncertain_and_blocks_retries(bool karma, bool cancelled)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            int commits = 0, prepares = 0, inspections = 0;
            Guid operationId = Guid.Empty;
            Task<CareerRewardApplyResult> Commit(Guid id)
            {
                operationId = id;
                commits++; // The replacement was stored, then its response was lost.
                return cancelled
                    ? Task.FromCanceled<CareerRewardApplyResult>(new CancellationToken(true))
                    : Task.FromException<CareerRewardApplyResult>(new InvalidOperationException("Lost reply"));
            }
            var dialog = new CareerRewardsWindow(karma, MakeKarmaEditor(), MakeNuyenEditor(15000m),
                (request, _) => Commit(request.OperationId), (request, _) => Commit(request.OperationId),
                _ => { prepares++; return Task.FromResult(((CareerManualKarmaEditorState?)MakeKarmaEditor(10), (CareerManualNuyenEditorState?)MakeNuyenEditor(15003m))); },
                reconcile: (id, _) =>
                {
                    inspections++;
                    Assert.AreEqual(operationId, id);
                    return Task.FromResult(new CareerRewardApplyResult(id, CareerRewardApplyStatus.Uncertain));
                });
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            TextBox reason = FindByAutomation<TextBox>(dialog, "career-rewards-reason")!;
            amount.Text = "3";
            reason.Text = "Lost reply reward";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(result.IsCompleted);
            Assert.IsFalse(apply.IsEnabled);
            Assert.IsNull(dialog.LastRefusal);
            Assert.AreEqual(CareerRewardApplyStatus.Uncertain, dialog.LastResult!.Status);
            Assert.AreEqual(operationId, dialog.LastResult.OperationId);
            Assert.AreEqual("3", amount.Text);
            Assert.AreEqual("Lost reply reward", reason.Text);
            StringAssert.Contains(FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text!, "Do not enter it again");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FindByAutomation<Button>(dialog, "career-rewards-check")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, inspections);
            Assert.AreEqual(1, commits);
            Assert.AreEqual(0, prepares, "Uncertainty cannot rebind the input to a new revision.");
            Assert.IsFalse(apply.IsEnabled);
            dialog.Close();
            StringAssert.Contains(dialog.CloseNotice!, "may already be recorded");
            Assert.IsFalse(dialog.CloseNotice!.Contains("Not recorded"));
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Reopened_uncertain_reward_only_offers_read_only_check_of_original_operation(bool karma)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            Guid operationId = Guid.NewGuid();
            int applies = 0, checks = 0;
            var dialog = new CareerRewardsWindow(karma, MakeKarmaEditor(10), MakeNuyenEditor(15003m),
                (_, _) => { applies++; return Task.FromResult(Confirmed()); },
                (_, _) => { applies++; return Task.FromResult(Confirmed()); }, null,
                reconcile: (id, _) =>
                {
                    checks++;
                    Assert.AreEqual(operationId, id);
                    return Task.FromResult(new CareerRewardApplyResult(id, CareerRewardApplyStatus.Confirmed));
                }, pendingResult: new(operationId, CareerRewardApplyStatus.Uncertain));
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            Assert.IsFalse(apply.IsEnabled);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FindByAutomation<Button>(dialog, "career-rewards-check")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(0, applies);
            Assert.AreEqual(1, checks);
            Assert.IsInstanceOfType<CareerRewardsWindow.CareerRewardReconciledOutcome>(result.GetAwaiter().GetResult());
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Reopened_pending_reward_definite_refusal_requires_fresh_review_and_explicit_record(bool karma)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            Guid operationId = Guid.NewGuid();
            int applies = 0, checks = 0, prepares = 0;
            Guid? nextId = null;
            long? nextRevision = null;
            var prepareGate = new TaskCompletionSource<(CareerManualKarmaEditorState?, CareerManualNuyenEditorState?)>();
            var dialog = new CareerRewardsWindow(karma, MakeKarmaEditor(), MakeNuyenEditor(15000m),
                (request, _) =>
                {
                    applies++;
                    nextId = request.OperationId;
                    nextRevision = request.ExpectedContentRevision;
                    return Task.FromResult(Confirmed(request.OperationId));
                },
                (request, _) =>
                {
                    applies++;
                    nextId = request.OperationId;
                    nextRevision = request.ExpectedContentRevision;
                    return Task.FromResult(Confirmed(request.OperationId));
                },
                _ => { prepares++; return prepareGate.Task; },
                reconcile: (id, _) =>
                {
                    checks++;
                    Assert.AreEqual(operationId, id);
                    return Task.FromResult(new CareerRewardApplyResult(id, CareerRewardApplyStatus.Refused, "A newer revision won."));
                },
                pendingResult: new(operationId, CareerRewardApplyStatus.Uncertain));
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            Button check = FindByAutomation<Button>(dialog, "career-rewards-check")!;
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(1, checks);
            Assert.AreEqual(1, prepares);
            Assert.IsFalse(apply.IsEnabled, "A definite refusal still needs a fresh editor before recording.");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, applies);
            Assert.AreEqual(1, checks);
            prepareGate.SetResult((MakeKarmaEditor(10), MakeNuyenEditor(15000m) with { ContentRevision = 10 }));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(result.IsCompleted);
            Assert.IsTrue(apply.IsEnabled);
            Assert.IsFalse(check.IsVisible);
            Assert.AreEqual(0, applies, "Checking and refreshing must never replay the refused command.");
            StringAssert.Contains(FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text!, "press Record again");
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "3";
            FindByAutomation<TextBox>(dialog, "career-rewards-reason")!.Text = "Reviewed after refusal";
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            WaitCompletion(result, 3000);
            Assert.AreEqual(1, applies);
            Assert.IsTrue(nextId.HasValue && nextId.Value != operationId);
            Assert.AreEqual(10L, nextRevision!.Value);
            Assert.AreEqual(CareerRewardApplyStatus.Confirmed, dialog.LastResult!.Status);
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Close_during_reward_dispatch_preserves_uncertainty_and_ignores_late_fault(bool karma)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            var pending = new TaskCompletionSource<CareerRewardApplyResult>();
            int applies = 0;
            var dialog = new CareerRewardsWindow(karma, MakeKarmaEditor(), MakeNuyenEditor(15000m),
                (_, _) => { applies++; return pending.Task; }, (_, _) => { applies++; return pending.Task; }, null);
            _ = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "3";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dialog.Close();
            StringAssert.Contains(dialog.CloseNotice!, "may already be recorded");
            pending.SetException(new InvalidOperationException("Commit response lost"));
            Dispatcher.UIThread.RunJobs();
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies);
            Assert.IsNull(dialog.LastRefusal);
            Assert.AreEqual(CareerRewardApplyStatus.Uncertain, dialog.LastResult!.Status);
            owner.Close();
        });
    }

    [TestMethod]
    public void A_lost_reply_after_a_refused_attempt_clears_the_old_not_recorded_notice()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            int applies = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (_, _) => ++applies == 1 ? Task.FromResult(Refused("stale revision"))
                    : Task.FromException<CareerRewardApplyResult>(new InvalidOperationException("Lost reply")),
                null, _ => Task.FromResult(((CareerManualKarmaEditorState?)MakeKarmaEditor(10), (CareerManualNuyenEditorState?)null)));
            _ = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "3";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("stale revision", dialog.LastRefusal);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.IsNull(dialog.LastRefusal);
            Assert.IsFalse(apply.IsEnabled);
            dialog.Close();
            StringAssert.Contains(dialog.CloseNotice!, "may already be recorded");
            Assert.IsFalse(dialog.CloseNotice!.Contains("Not recorded"));
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Close_before_confirmation_reopens_terminal_and_requires_visible_explicit_acknowledgment(bool karma)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            var pending = new TaskCompletionSource<CareerRewardApplyResult>();
            Guid operationId = Guid.Empty;
            int applies = 0, acknowledgments = 0;
            var dialog = new CareerRewardsWindow(karma, MakeKarmaEditor(), MakeNuyenEditor(15000m),
                (request, _) => { applies++; operationId = request.OperationId; return pending.Task; },
                (request, _) => { applies++; operationId = request.OperationId; return pending.Task; }, null,
                acknowledge: _ => { acknowledgments++; return true; });
            _ = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "3";
            Button originalApply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            Button originalAck = FindByAutomation<Button>(dialog, "career-rewards-acknowledge")!;
            originalApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dialog.Close();
            pending.SetResult(Confirmed(operationId));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(CareerRewardApplyStatus.Confirmed, dialog.LastResult!.Status);
            originalApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            originalAck.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies);
            Assert.AreEqual(0, acknowledgments, "A late closed-dialog callback must not acknowledge.");
            StringAssert.Contains(dialog.CloseNotice!, "review and acknowledge");

            var reopened = new CareerRewardsWindow(karma, MakeKarmaEditor(10), MakeNuyenEditor(15003m),
                (_, _) => { applies++; return Task.FromResult(Confirmed()); },
                (_, _) => { applies++; return Task.FromResult(Confirmed()); }, null,
                pendingResult: dialog.LastResult,
                acknowledge: id => { Assert.AreEqual(operationId, id); acknowledgments++; return true; });
            Assert.AreEqual(0, acknowledgments, "Constructing the new dialog does not display or acknowledge completion.");
            Task<object?> result = reopened.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            Button apply = FindByAutomation<Button>(reopened, "career-rewards-apply")!;
            Button acknowledge = FindByAutomation<Button>(reopened, "career-rewards-acknowledge")!;
            Assert.IsFalse(apply.IsEnabled);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies);
            Assert.AreEqual(0, acknowledgments, "Displaying the result still requires an explicit user acknowledgment.");
            StringAssert.Contains(FindByAutomation<TextBlock>(reopened, "career-rewards-balance")!.Text!, "confirmed recorded");
            acknowledge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            acknowledge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, acknowledgments);
            Assert.IsInstanceOfType<CareerRewardsWindow.CareerRewardAcknowledgedOutcome>(result.GetAwaiter().GetResult());
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void Confirmed_reward_close_failure_stays_terminal_and_never_redispatches(bool reconciled, bool cancelClose)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            Guid operationId = Guid.NewGuid();
            int applies = 0, checks = 0, acknowledgments = 0;
            bool failClose = true;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (request, _) => { applies++; return Task.FromResult(Confirmed(request.OperationId)); }, null, null,
                reconcile: (id, _) => { checks++; return Task.FromResult(Confirmed(id)); },
                pendingResult: reconciled ? new(operationId, CareerRewardApplyStatus.Uncertain) : null,
                acknowledge: _ => { acknowledgments++; return true; });
            dialog.Closing += (_, e) =>
            {
                if (!failClose) return;
                if (cancelClose) e.Cancel = true;
                else throw new InvalidOperationException("Injected window close failure");
            };
            Task<object?> result = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "3";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            Button check = FindByAutomation<Button>(dialog, "career-rewards-check")!;
            (reconciled ? check : apply).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(CareerRewardApplyStatus.Confirmed, dialog.LastResult!.Status);
            if (!cancelClose)
            {
                // Avalonia closes the window even when a Closing handler throws.
                // The confirmed operation must survive that teardown without an implicit
                // acknowledgment or a second dispatch, then remain reviewable on reopen.
                Assert.IsTrue(result.IsCompletedSuccessfully);
                Assert.IsFalse(dialog.IsVisible);
                Assert.AreEqual(0, acknowledgments);
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(reconciled ? 0 : 1, applies);
                Assert.AreEqual(reconciled ? 1 : 0, checks);
                dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                    (request, _) => { applies++; return Task.FromResult(Confirmed(request.OperationId)); }, null, null,
                    reconcile: (id, _) => { checks++; return Task.FromResult(Confirmed(id)); },
                    pendingResult: dialog.LastResult,
                    acknowledge: _ => { acknowledgments++; return true; });
                result = dialog.ShowDialog<object?>(owner);
                Dispatcher.UIThread.RunJobs();
                apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
                check = FindByAutomation<Button>(dialog, "career-rewards-check")!;
            }
            Assert.IsFalse(result.IsCompleted);
            Assert.IsFalse(apply.IsEnabled);
            string message = FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text!;
            StringAssert.Contains(message, "confirmed recorded");
            if (cancelClose) StringAssert.Contains(message, "dialog could not close");
            Assert.IsFalse(message.Contains("could not be confirmed", StringComparison.Ordinal));
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(reconciled ? 0 : 1, applies);
            Assert.AreEqual(reconciled ? 1 : 0, checks);

            // Even a failed close after explicit acknowledgment cannot acknowledge twice.
            Button acknowledge = FindByAutomation<Button>(dialog, "career-rewards-acknowledge")!;
            acknowledge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            acknowledge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, acknowledgments);
            Assert.IsFalse(apply.IsEnabled);
            failClose = false;
            dialog.Close();
            owner.Close();
        });
    }

    [TestMethod]
    [DataRow("owner-aba")]
    [DataRow("workspace-aba")]
    [DataRow("hidden")]
    [DataRow("never-shown")]
    public void Confirmed_reward_cannot_be_acknowledged_from_a_stale_or_hidden_dialog(string transition)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            var workspace = MakeKarmaEditor().WorkspaceId;
            OwnerContextStamp stamp = new(new OwnerScope("reward-owner"), "reward-session", 0);
            CareerDisplayContext context = new();
            long generation = context.Capture(workspace, stamp);
            int acknowledgments = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (_, _) => Task.FromResult(Confirmed()), null, null,
                contextIsCurrent: () => context.IsCurrent(generation, workspace, stamp),
                pendingResult: Confirmed(), acknowledge: _ => { acknowledgments++; return true; });
            if (transition != "never-shown")
            {
                _ = dialog.ShowDialog<object?>(owner);
                Dispatcher.UIThread.RunJobs();
            }
            Button acknowledge = FindByAutomation<Button>((Visual)dialog.Content!, "career-rewards-acknowledge")!;
            if (transition == "hidden") dialog.Hide();
            else if (transition != "never-shown")
            {
                context.Observe(transition == "workspace-aba" ? new CharacterWorkspaceId("other") : workspace,
                    transition == "owner-aba" ? stamp with { TransitionRevision = 1 } : stamp);
                context.Observe(workspace, stamp);
            }
            acknowledge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, acknowledgments);
            Assert.AreEqual(CareerRewardApplyStatus.Confirmed, dialog.LastResult!.Status);
            dialog.Close();
            owner.Close();
        });
    }

    private static CareerRewardApplyResult Confirmed(Guid? operationId = null) => new(operationId ?? Guid.NewGuid(), CareerRewardApplyStatus.Confirmed);
    private static CareerRewardApplyResult Refused(string message) => new(Guid.NewGuid(), CareerRewardApplyStatus.Refused, message);

    // ----- helpers -----

    private static CareerRewardsWindow.CareerKarmaAwardOutcome? _capturedKarmaOutcome;

    private static CareerRewardsWindow.CareerKarmaAwardOutcome? DriveKarmaDialog(bool gain, string amount, string reason)
    {
        _capturedKarmaOutcome = null;
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            var dialog = new CareerRewardsWindow(isKarmaMode: true, MakeKarmaEditor(), null);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            if (!gain)
            {
                RadioButton spend = FindByAutomation<RadioButton>(dialog, "career-rewards-spend")!;
                spend.IsChecked = true;
            }

            TextBox amountBox = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amountBox.Text = amount;
            TextBox reasonBox = FindByAutomation<TextBox>(dialog, "career-rewards-reason")!;
            reasonBox.Text = reason;

            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            WaitCompletion(dialogTask, timeoutMs: 3000);
            _capturedKarmaOutcome = (CareerRewardsWindow.CareerKarmaAwardOutcome?)dialogTask.GetAwaiter().GetResult();

            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });

        return _capturedKarmaOutcome;
    }

    private static CareerManualKarmaEditorState MakeKarmaEditor(long revision = 9L)
        => new(
            new CharacterWorkspaceId("workspace-under-test"),
            ContentRevision: revision,
            Karma: new CharacterCareerManualKarmaState(
                AvailableKarma: 30,
                AvailableNuyen: 15000m,
                NuyenPerKarmaWorkingForPeople: 2500m,
                NuyenPerKarmaWorkingForMan: 2500m));

    private static CareerManualNuyenEditorState MakeNuyenEditor(decimal nuyen)
        => new(
            new CharacterWorkspaceId("workspace-under-test"),
            ContentRevision: 9L,
            Nuyen: new CharacterCareerManualNuyenState(
                AvailableKarma: 30,
                AvailableNuyen: nuyen,
                NuyenPerKarmaWorkingForPeople: 2500m,
                NuyenPerKarmaWorkingForMan: 2500m));

    private static SectionHostState BuildHostState(string rulesetId, bool created, int karma, decimal nuyen)
    {
        // Root-level keys feed the rewards strip parser; the per-attribute array
        // activates the parity editor so the classic-sheet surface stays hidden
        // (SetSectionContext requires that to render the context area).
        string previewJson = JsonSerializer.Serialize(new
        {
            created,
            karma,
            nuyen,
            summary = "Test runner",
            attributes = new object[]
            {
                new
                {
                    name = "BOD",
                    baseValue = 2,
                    karmaValue = 0,
                    metatypeMin = 1,
                    metatypeMax = 6,
                    metatypeAugMax = 9,
                    created,
                    availableKarma = karma,
                    upgradeKarmaCost = created ? 15 : -1,
                    canCareerUpgrade = created,
                },
            },
        });

        return new SectionHostState(
            RulesetId: rulesetId,
            SectionId: "attributes",
            NavigationTabs: Array.Empty<NavigatorTabItem>(),
            ActiveTabId: null,
            SectionActions: Array.Empty<NavigatorSectionActionItem>(),
            ActiveActionId: null,
            Notice: string.Empty,
            PreviewJson: previewJson,
            Rows: Array.Empty<SectionRowDisplayItem>(),
            QuickActions: Array.Empty<SectionQuickActionDisplayItem>(),
            BuildLab: null,
            BrowseWorkspace: null,
            ContactGraph: null,
            DowntimePlanner: null,
            NpcPersonaStudio: null);
    }
    private static Window ShowHost(SectionHostControl host)
    {
        host.FontFamily = new FontFamily("fonts:Inter#Inter");
        Window window = new()
        {
            Content = host,
            Width = 900,
            Height = 600,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void PumpLayout(Layoutable root)
    {
        root.InvalidateMeasure();
        root.Measure(new Size(880, 560));
        root.Arrange(new Rect(0, 0, 880, 560));
        Dispatcher.UIThread.RunJobs();
    }

    private static T? FindByName<T>(Visual root, string name) where T : Visual
        => root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    private static T? FindByAutomation<T>(Visual root, string automationId) where T : Visual
        => root.GetVisualDescendants().OfType<T>().FirstOrDefault(control =>
            string.Equals(global::Avalonia.Automation.AutomationProperties.GetAutomationId(control), automationId, StringComparison.Ordinal));

    private static void WaitCompletion(Task task, int timeoutMs)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!task.IsCompleted && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(25);
        }

        if (!task.IsCompleted)
        {
            throw new TimeoutException("Dialog did not complete in time.");
        }
    }

    private static void WithHeadlessUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(CareerRewardsHeadlessAppBootstrap));
                session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                session?.Dispose();
            }
        }
    }

    private sealed class CareerRewardsHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont();
        }
    }
}
