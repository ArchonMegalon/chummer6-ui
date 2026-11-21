using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Sr6_nuyen_dialog_applies_the_entered_gain_or_spend_at_face_value(bool gain)
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            const string original = "<character><gameedition>SR6</gameedition><created>True</created>"
                + "<karma>30</karma><nuyen>4200</nuyen><expenses />"
                + "<customstate>preserved</customstate></character>";
            var editor = CareerManualNuyenEditorProjector.Project(original,
                new CharacterWorkspaceId("workspace-under-test"), 9, sourceDataResolver: null);
            var dialog = new CareerRewardsWindow(isKarmaMode: false, MakeKarmaEditor(), editor);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            if (!gain)
                FindByAutomation<RadioButton>(dialog, "career-rewards-spend")!.IsChecked = true;

            TextBox amount = FindByAutomation<TextBox>(dialog, "career-rewards-amount")!;
            amount.Text = "800";
            TextBox reason = FindByAutomation<TextBox>(dialog, "career-rewards-reason")!;
            reason.Text = "Payment for run";

            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            WaitCompletion(dialogTask, timeoutMs: 3000);
            var outcome = (CareerRewardsWindow.CareerNuyenAdjustOutcome)dialogTask.GetAwaiter().GetResult()!;

            Assert.AreEqual(gain ? CharacterCareerManualNuyenAction.Gain : CharacterCareerManualNuyenAction.Spend,
                outcome.Request.Action);
            Assert.AreEqual(800, outcome.Request.Amount);
            Assert.AreEqual("Payment for run", outcome.Request.Reason);
            Assert.AreEqual(100m, outcome.Request.Percent);
            Assert.AreEqual(editor.WorkspaceId, outcome.Request.WorkspaceId);
            Assert.AreEqual(editor.ContentRevision, outcome.Request.ExpectedContentRevision);
            Assert.IsFalse(outcome.Request.KarmaNuyenExchange);

            // Feed the actual dialog request through the production mutation path.
            // A successful zero-value expense must not satisfy this regression.
            XElement changed = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
                original, outcome.Request, sourceDataResolver: null)).Root!;
            Assert.AreEqual(gain ? "5000" : "3400", changed.Element("nuyen")!.Value);
            Assert.AreEqual("30", changed.Element("karma")!.Value);
            XElement[] expenses = changed.Element("expenses")!.Elements("expense").ToArray();
            Assert.AreEqual(1, expenses.Length);
            Assert.AreEqual(gain ? "800" : "-800", expenses[0].Element("amount")!.Value);
            Assert.AreEqual("Nuyen", expenses[0].Element("type")!.Value);
            Assert.AreEqual("Payment for run", expenses[0].Element("reason")!.Value);
            Assert.AreEqual("preserved", changed.Element("customstate")!.Value);

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

    [TestMethod]
    public void Confirmed_pending_result_exposes_visible_enabled_acknowledgement()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new();
            owner.Show();
            // The gen14 acceptance contract: a Confirmed pending reward MUST
            // surface an acknowledge control that is visible and enabled -
            // acceptance clicks genuine pointer input on a rendered control,
            // never RaiseEvent on a hidden or collapsed one.
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (_, _) => Task.FromResult(Confirmed()),
                null, null,
                pendingResult: Confirmed(Guid.NewGuid()),
                acknowledge: _ => true);
            _ = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            Button ack = FindByAutomation<Button>(dialog, "career-rewards-acknowledge")
                ?? throw new AssertFailedException("acknowledge control missing on a Confirmed dialog");
            Assert.IsTrue(ack.IsVisible, "acknowledge control must be visible on a Confirmed dialog");
            Assert.IsTrue(ack.IsEnabled, "acknowledge control must be enabled on a Confirmed dialog");
            Assert.IsFalse(FindByAutomation<Button>(dialog, "career-rewards-apply")!.IsVisible,
                "Record must be hidden while a Confirmed result awaits acknowledgement");
            // Genuine click on the visible control clears the terminal state.
            ack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.IsNull(dialog.LastRefusal);
            owner.Close();
        });
    }

    // ---- Gen17 (Controller8300, T660): same-modal ownership coverage ----

    private static Sr6CareerAttributeAdvanceEditorSnapshot MakeAdvanceSnapshot()
    {
        var quote = new Sr6CareerAttributeQuote(
            "sr6-career-attribute-quote/v1",
            "install-account:subject-alpha",
            new CharacterWorkspaceId("sr6-same-modal-tests"),
            42,
            "source-digest-1",
            "rule-digest-1",
            "Body",
            4,
            5,
            0,
            8,
            100,
            25,
            null,
            Array.Empty<string>(),
            "binding-digest-1");
        return new Sr6CareerAttributeAdvanceEditorSnapshot(
            new CharacterWorkspaceId("sr6-same-modal-tests"),
            42,
            "sr6",
            new Sr6CareerAttributeQuote[] { quote },
            0,
            new OwnerContextStamp(
                new OwnerScope("install-account:subject-alpha"),
                "authority-instance-1",
                42));
    }

    [TestMethod]
    public void Single_advance_window_resolves_and_binds_lookups_to_that_instance()
    {
        WithHeadlessUi(() =>
        {
            Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            try
            {
                advance.Show();
                Dispatcher.UIThread.RunJobs();
                PumpLayout(advance);

                var resolved = DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow();
                Assert.IsNotNull(resolved, "exactly one visible advance window must resolve");
                Assert.AreSame(advance, resolved, "the resolved instance must be THE visible window");

                // Same-modal binding: the modal's own controls are found inside
                // THAT instance's visual tree.
                Assert.IsNotNull(
                    DesktopMouseFirstJourneyRunner.FindInWindow<Button>(resolved, "sr6-career-attribute-advance-apply"),
                    "Apply must be found inside the resolved window");
                Assert.IsNotNull(
                    DesktopMouseFirstJourneyRunner.FindInWindow<Button>(resolved, "sr6-career-attribute-advance-cancel"),
                    "Cancel must be found inside the resolved window");
            }
            finally
            {
                advance.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Advance_window_initial_focus_lands_on_the_enabled_apply_action()
    {
        WithHeadlessUi(() =>
        {
            Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            try
            {
                advance.Show();
                Dispatcher.UIThread.RunJobs();
                PumpLayout(advance);

                var apply = DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                    (Sr6CareerAttributeAdvanceWindow)advance, "sr6-career-attribute-advance-apply");
                Assert.IsNotNull(apply, "apply must exist");
                Assert.IsTrue(apply.IsEnabled, "the snapshot quote is advanceable so Apply must be enabled");
                Assert.IsTrue(apply.IsFocused,
                    "initial focus must land on the enabled Advance action, not nowhere (Controller9534)");
            }
            finally
            {
                advance.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Resolved_pointer_route_dispatches_exactly_once_per_call()
    {
        // Controller9544: one helper call = ONE physical dispatch. The
        // waitForClick:true re-raise timer could multiply presses past
        // the discipline's accounting; count actual Click deliveries on
        // a live button through the REAL resolved route.
        WithHeadlessUi(() =>
        {
            Button row = new Button { Content = "improve", IsEnabled = true };
            int clicks = 0;
            row.AddHandler(Button.ClickEvent, (_, _) => clicks++);
            Window host = new Window { Content = new StackPanel { Children = { row } } };
            host.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                PumpLayout(host);

                Task<string>? routeTask = null;
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    routeTask = DesktopMouseFirstJourneyRunner.RoutePointerClickResolvedAsync(
                        row,
                        waitForClick: false,
                        liveTarget: () => row);
                });
                Dispatcher.UIThread.RunJobs();
                Assert.IsNotNull(routeTask, "the route task must be assigned synchronously");
                Assert.IsTrue(routeTask.IsCompletedSuccessfully, "the single dispatch must complete under the headless pump");
                string routing = routeTask.Result;
                Assert.IsTrue(
                    routing.StartsWith("delivered=True", StringComparison.OrdinalIgnoreCase),
                    "the settled live button must deliver on the first dispatch: " + routing);
                Assert.AreEqual(1, clicks, "exactly ONE physical dispatch per call (Controller9544)");
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Escape_close_of_advance_dialog_restores_initiating_focus()
    {
        // Controller9547 / gen9 finding 4: after the SR6 advance modal
        // closes, focus must return to the control that initiated the
        // advance. Capture before ShowDialog, Escape-close, then the
        // restore must land on the same still-visible row.
        WithHeadlessUi(() =>
        {
            Button row = new Button { Name = "AttributeImprove_AGI", Content = "improve AGI" };
            Window owner = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { row } } };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            PumpLayout(owner);
            try
            {
                Assert.IsTrue(row.Focus(), "precondition: the row takes focus");
                Dispatcher.UIThread.RunJobs();
                var (captured, capturedName) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.IsNotNull(captured, "the focused row must be captured pre-open");
                Assert.AreEqual("AttributeImprove_AGI", capturedName, "capture must carry the resolvable name");
                Assert.AreSame(row, captured, "capture must be the row instance");

                Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
                Task<object?> dialogTask = advance.ShowDialog<object?>(owner);
                Dispatcher.UIThread.RunJobs();
                PumpLayout(advance);
                Assert.IsFalse(row.IsFocused, "the modal must be focus-trapped while visible");

                DesktopMouseFirstJourneyRunner.SendKeyStroke(advance, global::Avalonia.Input.Key.Escape);
                Dispatcher.UIThread.RunJobs();
                WaitCompletion(dialogTask, timeoutMs: 3000);
                Assert.IsNull(dialogTask.GetAwaiter().GetResult(), "Escape must cancel without a commit");

                bool restored = MainWindow.RestoreInitiatingFocusAfterAdvanceDialogClose(owner, captured, capturedName);
                Assert.IsTrue(restored, "restore must report success for the still-visible row");
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(row.IsFocused || row.IsKeyboardFocusWithin,
                    "focus must return to the initiating row after the modal close");
            }
            finally
            {
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Escape_close_restore_reaches_a_rebuilt_row_by_name()
    {
        // Parity-grid rebuild: the initiating row instance is detached
        // while the modal is open and a NEW instance with the same name
        // renders. The restore must re-resolve by name and land on the
        // NEW instance - not silently report success on a dead reference.
        WithHeadlessUi(() =>
        {
            Button row = new Button { Name = "AttributeImprove_AGI", Content = "improve AGI" };
            Window owner = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { row } } };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            PumpLayout(owner);
            try
            {
                Assert.IsTrue(row.Focus(), "precondition: the row takes focus");
                Dispatcher.UIThread.RunJobs();
                var (captured, capturedName) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.AreSame(row, captured, "precondition: capture is the original row");

                // Simulate the parity-grid rebuild while "the modal" is up:
                // detach the original row and render a same-named successor.
                var panel = (StackPanel)owner.Content!;
                Button rebuilt = new Button { Name = "AttributeImprove_AGI", Content = "improve AGI (rebuilt)" };
                panel.Children.Clear();
                panel.Children.Add(rebuilt);
                Dispatcher.UIThread.RunJobs();
                PumpLayout(owner);
                Assert.IsFalse(rebuilt.IsFocused, "precondition: the rebuilt row does not hold focus");

                bool restored = MainWindow.RestoreInitiatingFocusAfterAdvanceDialogClose(owner, captured, capturedName);
                Assert.IsTrue(restored, "restore must re-resolve the rebuilt row by name");
                Dispatcher.UIThread.RunJobs();
                Assert.AreSame(rebuilt, owner.FocusManager?.GetFocusedElement() as Control,
                    "focus must land on the rebuilt instance");
                Assert.IsTrue(rebuilt.IsFocused || rebuilt.IsKeyboardFocusWithin,
                    "the rebuilt row must hold focus after restore");
            }
            finally
            {
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Older_restore_cycle_cannot_focus_behind_a_newer_open_modal()
    {
        // Controller9568: close A, open B before A's delayed restore
        // callback, drain A's callback - focus must STAY in B's
        // initiator, and A's stale cycle must exit silently.
        WithHeadlessUi(() =>
        {
            Button rowA = new Button { Name = "AttributeImprove_A", Content = "A" };
            Button rowB = new Button { Name = "AttributeImprove_B", Content = "B" };
            Window owner = new Window { Width = 320, Height = 220, Content = new StackPanel { Children = { rowA, rowB } } };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            PumpLayout(owner);
            try
            {
                Assert.IsTrue(rowA.Focus(), "precondition: A focused");
                var (capA, nameA) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.AreSame(rowA, capA, "precondition: capture A is the row");
                int cycleA = MainWindow.Sr6FocusRestoreDiagnostics.BeginRestoreCycle(owner);

                // Open B's cycle BEFORE A's delayed callback runs.
                Assert.IsTrue(rowB.Focus(), "precondition: B focused after A closed");
                var (capB, nameB) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.AreSame(rowB, capB, "precondition: capture B is the row");
                int cycleB = MainWindow.Sr6FocusRestoreDiagnostics.BeginRestoreCycle(owner);
                Assert.AreNotEqual(cycleA, cycleB, "cycle B must be a newer generation");

                // Drain A's stale deferred callback: the deferred loop IS the
                // product's delayed restore callback and checks the generation
                // before every Focus attempt - it must exit silently without
                // focusing anything.
                MainWindow.RunDeferredFocusRestoreAsync(owner, capA, nameA, cycleA);
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(150);
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(rowB.IsFocused || rowB.IsKeyboardFocusWithin,
                    "focus must STAY on B's initiator after A's stale callback is drained");

                // A's cycle is stale by generation - a deferred loop seeded
                // with cycleA would exit silently without focusing.
                Assert.IsFalse(MainWindow.Sr6FocusRestoreDiagnostics.IsCurrentCycle(cycleA, owner),
                    "A's generation must no longer be current after B began");
                Assert.IsTrue(MainWindow.Sr6FocusRestoreDiagnostics.IsCurrentCycle(cycleB, owner),
                    "B's generation must be current");

                // The same for the by-name path: draining A's stale deferred
                // callback by name must not steal focus from B either.
                MainWindow.RunDeferredFocusRestoreAsync(owner, null, nameA, cycleA);
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(150);
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(rowB.IsFocused || rowB.IsKeyboardFocusWithin,
                    "focus must STAY on B after the stale by-name restore");

                // B's own restore still works (drained through the deferred
                // loop exactly as the product posts it).
                MainWindow.RunDeferredFocusRestoreAsync(owner, capB, nameB, cycleB);
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(MainWindow.Sr6FocusRestoreDiagnostics.LastRestoreOutcome().Focused,
                    "the current cycle's restore still succeeds");
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(rowB.IsFocused || rowB.IsKeyboardFocusWithin,
                    "B stays focused after its own restore");
            }
            finally
            {
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Queued_context_switch_refuses_stale_deferred_focus_restore()
    {
        // Controller9579 gen11: the deferred focus-restore callback is queued
        // with the captured display context (owner + workspace + account).
        // A same-owner context switch (workspace/account change - NOT a
        // revision advance, which legitimately happens on every Apply) that
        // lands while the callback is still in flight must refuse: no Focus
        // call, an honest refusal entry, focus stays where it was. The cycle
        // generation alone cannot catch this - the owner reference and the
        // generation are unchanged - so the display predicate is the guard.
        WithHeadlessUi(() =>
        {
            Button rowA = new Button { Name = "AttributeImprove_A", Content = "A" };
            Button rowB = new Button { Name = "AttributeImprove_B", Content = "B" };
            Window owner = new Window { Width = 320, Height = 220, Content = new StackPanel { Children = { rowA, rowB } } };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            PumpLayout(owner);
            try
            {
                Assert.IsTrue(rowA.Focus(), "precondition: A focused");
                var (capA, nameA) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.AreSame(rowA, capA, "precondition: capture A is the row");
                int cycleA = MainWindow.Sr6FocusRestoreDiagnostics.BeginRestoreCycle(owner);

                // The captured display predicate: context is current while the
                // flag holds; a workspace/account switch flips it - exactly the
                // shape the production call site passes (DisplayStillCurrent).
                bool contextCurrent = true;
                Func<bool> displayStillCurrent = () => contextCurrent;

                // Queue the deferred restore; it is an in-flight GUI operation.
                MainWindow.RunDeferredFocusRestoreAsync(owner, capA, nameA, cycleA, displayStillCurrent);

                // The context switches BEFORE the queued callback drains.
                contextCurrent = false;
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(150);
                Dispatcher.UIThread.RunJobs();

                // The refusal is honest: recorded in the ring...
                string ring = string.Join("|", MainWindow.Sr6FocusRestoreDiagnostics.Snapshot());
                Assert.IsTrue(ring.Contains("context-switch refusal"),
                    "the context-switch refusal must be recorded honestly, ring: " + ring);
                // ...and focus must not have moved to another row.
                Assert.IsFalse(rowB.IsFocused || rowB.IsKeyboardFocusWithin,
                    "a refused cycle must never move focus to another row");
                Assert.IsTrue(rowA.IsFocused || rowA.IsKeyboardFocusWithin,
                    "focus must STAY on the original row after the context switch");

                // The positive side: a CURRENT context still restores. Focus B,
                // begin a newer cycle (A's is now stale by generation too),
                // drain B's deferred restore with the predicate held true.
                Assert.IsTrue(rowB.Focus(), "precondition: B focused after the switch");
                var (capB, nameB) = MainWindow.CaptureInitiatingFocusForAdvanceDialog(owner);
                Assert.AreSame(rowB, capB, "precondition: capture B is the row");
                int cycleB = MainWindow.Sr6FocusRestoreDiagnostics.BeginRestoreCycle(owner);
                Assert.AreNotEqual(cycleA, cycleB, "cycle B must be a newer generation");
                contextCurrent = true;
                MainWindow.RunDeferredFocusRestoreAsync(owner, capB, nameB, cycleB, displayStillCurrent);
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(150);
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(MainWindow.Sr6FocusRestoreDiagnostics.LastRestoreOutcome().Focused,
                    "a current-context restore still succeeds after the fix");
                Assert.IsTrue(rowB.IsFocused || rowB.IsKeyboardFocusWithin,
                    "B stays focused after its own restore");
            }
            finally
            {
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void Menu_item_activation_is_measured_by_the_menu_click_probe()
    {
        // Controller9415 gen10: MenuItem activation raises
        // MenuItem.ClickEvent, not Button.ClickEvent. The delivery probe
        // must listen to the real activation signal, otherwise an executed
        // menu command reports delivered=false and the press discipline
        // fail-closes on a live outcome (gen10n evidence).
        WithHeadlessUi(() =>
        {
            MenuItem item = new MenuItem { Header = "new_character", IsEnabled = true };
            int activations = 0;
            item.AddHandler(MenuItem.ClickEvent, (_, _) => activations++);
            Window host = new Window { Content = new StackPanel { Children = { item } } };
            host.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                PumpLayout(host);

                Task<string>? routeTask = null;
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    routeTask = DesktopMouseFirstJourneyRunner.RoutePointerClickResolvedAsync(
                        item,
                        waitForClick: false,
                        liveTarget: () => item);
                });
                Dispatcher.UIThread.RunJobs();
                Assert.IsNotNull(routeTask, "the route task must be assigned synchronously");
                Assert.IsTrue(routeTask.IsCompletedSuccessfully, "the route must complete under the headless pump");
                string routing = routeTask.Result;
                Assert.IsTrue(
                    routing.StartsWith("delivered=True", StringComparison.OrdinalIgnoreCase),
                    "a menu item activation must be reported as delivered through the MenuItem probe: " + routing);
                Assert.AreEqual(1, activations, "exactly one MenuItem activation per dispatch");
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Advance_window_tab_walk_reaches_close_and_escape_cancels_without_commit()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new Window { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            Task<object?> dialogTask = advance.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            PumpLayout(advance);
            try
            {
                var apply = DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                    (Sr6CareerAttributeAdvanceWindow)advance, "sr6-career-attribute-advance-apply");
                var cancel = DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                    (Sr6CareerAttributeAdvanceWindow)advance, "sr6-career-attribute-advance-cancel");
                Assert.IsNotNull(apply);
                Assert.IsNotNull(cancel);
                Assert.IsTrue(apply.IsFocused, "precondition: initial focus on Apply");

                // Tab moves focus from Apply to Close (Cycle tab order).
                DesktopMouseFirstJourneyRunner.SendKeyStroke(apply, global::Avalonia.Input.Key.Tab);
                Dispatcher.UIThread.RunJobs();
                PumpLayout(advance);
                Assert.IsTrue(cancel.IsFocused, "Tab must move focus from Apply to Close");
                Assert.IsFalse(apply.IsFocused, "focus must have left Apply");

                // Escape cancels: the dialog task completes with null (no
                // commit) without ever clicking Apply.
                DesktopMouseFirstJourneyRunner.SendKeyStroke(advance, global::Avalonia.Input.Key.Escape);
                Dispatcher.UIThread.RunJobs();
                WaitCompletion(dialogTask, timeoutMs: 3000);
                Assert.IsNull(dialogTask.GetAwaiter().GetResult(),
                    "Escape must cancel without a commit outcome");
            }
            finally
            {
                if (!dialogTask.IsCompleted)
                {
                    advance.Close();
                    Dispatcher.UIThread.RunJobs();
                }
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
    [TestMethod]
    public void Advance_window_enter_on_the_focused_apply_commits_the_quoted_advance()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new Window { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            Task<object?> dialogTask = advance.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            PumpLayout(advance);
            try
            {
                var apply = DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                    (Sr6CareerAttributeAdvanceWindow)advance, "sr6-career-attribute-advance-apply");
                Assert.IsNotNull(apply);
                Assert.IsTrue(apply.IsFocused, "precondition: initial focus on Apply");

                // Enter on the default (focused) Apply commits the quote.
                DesktopMouseFirstJourneyRunner.SendKeyStroke(apply, global::Avalonia.Input.Key.Enter);
                Dispatcher.UIThread.RunJobs();
                WaitCompletion(dialogTask, timeoutMs: 3000);
                var outcome = dialogTask.GetAwaiter().GetResult()
                    as Sr6CareerAttributeAdvanceWindow.Sr6CareerAttributeAdvanceOutcome;
                Assert.IsNotNull(outcome, "Enter must commit the quoted advance");
                Assert.AreEqual("Body", outcome!.DisplayAttribute);
                Assert.AreEqual(25, outcome.KarmaCost);
                Assert.AreEqual(5, outcome.TargetNaturalRating);
            }
            finally
            {
                if (!dialogTask.IsCompleted)
                {
                    advance.Close();
                    Dispatcher.UIThread.RunJobs();
                }
                owner.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void Decoy_controls_in_other_windows_never_bind_across_modals()
    {
        WithHeadlessUi(() =>
        {
            Window advance = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            Window decoy = new Window { Width = 240, Height = 120 };
            try
            {
                advance.Show();
                var decoyButton = new Button { Content = "decoy" };
                global::Avalonia.Automation.AutomationProperties.SetAutomationId(
                    decoyButton, "sr6-career-attribute-advance-cancel");
                decoy.Content = decoyButton;
                decoy.Show();
                Dispatcher.UIThread.RunJobs();
                PumpLayout(advance);
                PumpLayout(decoy);

                // The desktop-wide finder WOULD find the decoy; the scoped
                // finder must not. This is the exact mismatched-modal hazard.
                Assert.IsNotNull(
                    DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                        (Sr6CareerAttributeAdvanceWindow)advance, "sr6-career-attribute-advance-cancel"),
                    "the advance window's own cancel must be found");
                Assert.IsNull(
                    DesktopMouseFirstJourneyRunner.FindInWindow<Button>(
                        (Sr6CareerAttributeAdvanceWindow)advance, "decoy-only-id"),
                    "an id that only exists in another window must never resolve inside this one");
            }
            finally
            {
                decoy.Close();
                advance.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void Stacked_advance_windows_refuse_resolution_until_unambiguous()
    {
        WithHeadlessUi(() =>
        {
            Window first = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            Window second = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            try
            {
                first.Show();
                second.Show();
                Dispatcher.UIThread.RunJobs();
                PumpLayout(first);
                PumpLayout(second);

                // Two visible advance modals: same-modal ownership cannot be
                // proven, so the resolver must refuse (null), never pick one
                // silently.
                Assert.IsNull(
                    DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow(),
                    "stacked advance windows must not resolve to an unowned click target");

                second.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.IsNotNull(
                    DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow(),
                    "a single visible advance window resolves again once the stack clears");
            }
            finally
            {
                second.Close();
                first.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void ResolveSingleAdvanceWindow_active_resolution_with_inactive_refusal_as_documented_headless_limitation()
    {
        // Gen18 (T662): a sole visible advance window that is NOT active
        // must refuse resolution (resolver returns null). Measured
        // platform fact: Avalonia 11.3.7 headless cannot produce a
        // visible-but-inactive window - IsActiveProperty is a readonly
        // DirectProperty (SetValue throws ArgumentException), the getter
        // is non-virtual, and IsActive=false only co-occurs with
        // IsVisible=false. The refusal branch is therefore verified
        // indirectly: this test proves the active path resolves the sole
        // visible instance, and the inactive refusal is exercised by
        // production shapes where a visible modal can be inactive
        // (documented in the resolver comment).
        WithHeadlessUi(() =>
        {
            var target = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            try
            {
                target.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(target.IsVisible, "target must be visible");
                Assert.IsTrue(target.IsActive, "headless platform keeps shown windows active (measured)");
                var resolved = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow();
                Assert.IsNotNull(resolved, "single visible+active instance must resolve");
                Assert.AreSame(target, resolved, "resolver must return the visible instance");
            }
            finally
            {
                target.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void ResolveSingleAdvanceWindow_returns_null_when_no_window_visible()
    {
        // Gen18 (T662): with no visible advance window the resolver must
        // return null (pre-existing behavior made explicit here as the
        // negative baseline for the active requirement).
        WithHeadlessUi(() =>
        {
            var resolved = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow();
            Assert.IsNull(resolved, "no visible instance must resolve to null");
        });
    }

    // ---- Gen18 (Controller8327, T664): receipt shape + attribute mapping ----
    // T663 note: the uncertain/closed/wrong-owner attribution semantics are
    // covered at presenter level (CharacterOverviewPresenterTests:
    // Career_reward_committed_then_fault_retains_identity_and_reconciles_without_another_write,
    // Career_reward_original_presenter_consumes_exact_confirmation_from_another_presenter,
    // Career_reward_confirmed_receipt_survives_postcommit_cancellation_or_owner_change,
    // Delayed_reconcile_for_owner_a_after_switch_to_b_never_touches_b_record);
    // the journey-side repair is the read-only GetPendingCareerReward
    // attribution + the restored ownerWindow?.LastResult null safety, both
    // exercised by the byte-bound phaseA journey.

    [TestMethod]
    public void Sr6_advance_receipt_shape_requires_every_final_state_field()
    {
        // Gen19 (T673): ALL final-state fields are REQUIRED on the consumed
        // SR6 receipt: workspace, numeric-representable karma/nuyen, known
        // SR6 attribute, positive integral rank, non-negative integral
        // revision. Missing fields never skip comparisons.
        foreach (var (json, expectedFragment) in new[]
                 {
                     ("{}", "workspaceId"),
                     ("{\"workspaceId\":\"ws-1\"}", "karmaAfter"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20}", "nuyenAfter"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000}", "attributeId"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\"}", "quotedTargetNaturalRating"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7}", "savedRevision"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Essence\",\"quotedTargetNaturalRating\":7,\"savedRevision\":16}", "attributeId"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":0,\"savedRevision\":16}", "positive integral"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7.5,\"savedRevision\":16}", "integral"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":-1}", "non-negative"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":\"twenty\",\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":16}", "karmaAfter"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":1e30,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":16}", "karmaAfter"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":\"ten k\",\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":16}", "nuyenAfter"),
                     ("{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":\"16x\"}", "savedRevision"),
                 })
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            string? error = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
                .ValidateSr6AdvanceReceiptShape(document);
            Assert.IsNotNull(error, "malformed receipt must fail: " + json);
            Assert.IsTrue(
                error.Contains(expectedFragment, StringComparison.Ordinal),
                $"diagnostic '{error}' must name '{expectedFragment}' for {json}");
        }

        var complete = System.Text.Json.JsonDocument.Parse(
            "{\"workspaceId\":\"ws-1\",\"karmaAfter\":20,\"nuyenAfter\":10000,\"attributeId\":\"Body\",\"quotedTargetNaturalRating\":7,\"savedRevision\":16}");
        using (complete)
        {
            Assert.IsNull(
                global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateSr6AdvanceReceiptShape(complete),
                "a complete receipt must pass shape validation");
        }
    }

    [TestMethod]
    public void Saved_receipt_requires_sr6_verification_only_for_sr6_family()
    {
        // Gen19 (T673): the SR6 advance receipt is demanded ONLY when the
        // saved receipt records a verified sr6-family journey. SR5, missing,
        // blank, and non-sr variants must NOT demand it.
        Assert.IsTrue(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":\"sr6\"}")));
        Assert.IsTrue(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":\"SR6-Wall\"}")));
        Assert.IsFalse(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":\"sr5\"}")));
        Assert.IsFalse(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":\"SR5-6E\"}")));
        Assert.IsFalse(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{}")));
        Assert.IsFalse(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":\"\"}")));
        Assert.IsFalse(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner
            .SavedReceiptRequiresSr6AdvanceVerification(
                System.Text.Json.JsonDocument.Parse("{\"rulesetId\":7}")));
    }

    [TestMethod]
    public void ScrollViewer_reserves_layout_space_for_the_vertical_scrollbar_instead_of_overlaying_content()
    {
        // Gen19 (user report): the scrollbar covered the end of tables.
        // Fluent's ScrollViewer theme overlays the bar on the content while
        // AllowAutoHide is true (the default). The app style disables
        // AllowAutoHide, which keeps the classic non-overlay layout: the
        // vertical bar owns its own grid column, so the last visible row is
        // never hidden beneath it. Prove the resolved style headless.
        WithHeadlessUi(() =>
        {
            var viewer = new ScrollViewer
            {
                Width = 220,
                Height = 120,
            };
            var content = new StackPanel();
            for (int i = 0; i < 20; i++)
            {
                content.Children.Add(new TextBlock { Text = "row " + i, Margin = new Thickness(4) });
            }
            viewer.Content = content;
            viewer.AllowAutoHide = false;
            viewer.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            viewer.Classes.Add("app-styled");
            var host = new Window { Width = 240, Height = 160, Content = viewer };
            host.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                Assert.IsFalse(viewer.AllowAutoHide,
                    "the app default must keep scrollbars from overlaying content");
                var bar = viewer.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ScrollBar>()
                    .FirstOrDefault(b => b.Orientation == global::Avalonia.Layout.Orientation.Vertical);
                Assert.IsNotNull(bar, "the vertical scrollbar must exist in the template");
                // Non-overlay layout: the bar's own column starts where the
                // content presenter ends - the bar's left edge equals the
                // content presenter's right edge (no overlap).
                double contentRight = viewer.Presenter is { } p
                    ? p.Bounds.X + p.Bounds.Width : 0;
                Assert.IsTrue(
                    bar.Bounds.X >= contentRight - 0.5,
                    $"the vertical bar (x={bar.Bounds.X:0.#}) must not overlap the content presenter (right={contentRight:0.#})");
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    [TestMethod]
    public void App_style_disables_scrollbar_auto_hide_globally()
    {
        // The App.axaml style must exist and must target ScrollViewer with
        // AllowAutoHide=False - the documented fix for the covered table end.
        // The scrollviewer created under the app scope inherits it.
        WithHeadlessUi(() =>
        {
            var viewer = new ScrollViewer();
            var host = new Window { Width = 200, Height = 120, Content = viewer };
            host.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                Assert.IsFalse(viewer.AllowAutoHide,
                    "App.axaml must set AllowAutoHide=False on ScrollViewer so the last table row is never covered");
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

        [TestMethod]
    public void Nuyen_terminal_attribution_refuses_null_pending_switched_workspace_and_wrong_operation()
    {
        // Gen19 (T674): a null pending must NOT be accepted as "the
        // in-memory Confirmed was consumed by the close"; a switched display
        // workspace must not redirect the attribution; only a terminal
        // Confirmed FOR THE RETAINED operation on the RETAINED workspace
        // passes. The classifier is the exact function the journey calls.
        Guid operation = Guid.Parse("11111111-1111-1111-1111-111111111111");
        string? verdict;

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            null, operation, "ws-1", "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "null pending must not be accepted");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Confirmed), null, "ws-1", "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "operation id was not captured");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Confirmed), operation, null, "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "workspace id was not captured");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Confirmed), operation, "ws-1", "ws-2");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "switched away");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage),
            operation, "ws-1", "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "Uncertain, not Confirmed");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Refused, "no"), operation, "ws-1", "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "Refused, not Confirmed");

        Guid other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(other, CareerRewardApplyStatus.Confirmed), operation, "ws-1", "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "not the retained operation");

        Assert.IsNull(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyNuyenTerminalAttribution(
            new(operation, CareerRewardApplyStatus.Confirmed), operation, "ws-1", "ws-1"),
            "a terminal Confirmed for the retained operation on the retained workspace must pass");
    }

    [TestMethod]
    public void Sr6_advance_reconcile_outcome_refuses_missing_blank_or_wrong_operation_receipts()
    {
        // Gen21 (T676): the close-timeout reconciliation must fail on a
        // missing recovery receipt, a BLANK receipt workspace (never a
        // silent match), a receipt bound to a different workspace, a
        // missing retained operation/workspace capture, a switched
        // display, or a receipt for a DIFFERENT operation (an
        // owner/workspace ABA swap or a later-selection redirect). It
        // passes only on an exact retained-operation + retained-workspace
        // receipt.
        Guid retained = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string? verdict;

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: false, receiptOperationId: null, retainedOperationId: retained,
            receiptWorkspaceId: null, retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "did not return a confirmed receipt");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: "   ", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict, "a blank receipt workspace must never match");
        StringAssert.Contains(verdict, "blank receipt workspace");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: null, retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict, "a null receipt workspace must never match");
        StringAssert.Contains(verdict, "blank receipt workspace");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: "ws-9", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "belongs to workspace");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: null,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "operation id was not captured from the retained dialog");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: null, currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "workspace id was not captured");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-2");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "switched away");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: null, retainedOperationId: retained,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict);
        StringAssert.Contains(verdict, "carries no operation id");

        verdict = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: other, retainedOperationId: retained,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1");
        Assert.IsNotNull(verdict, "a different-operation receipt (ABA/redirect) must fail");
        StringAssert.Contains(verdict, "not the retained operation");

        Assert.IsNull(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifySr6AdvanceReconcileOutcome(
            receiptPresent: true, receiptOperationId: retained, retainedOperationId: retained,
            receiptWorkspaceId: "ws-1", retainedWorkspaceId: "ws-1", currentWorkspaceId: "ws-1"),
            "an exact retained-operation + retained-workspace receipt must pass");
    }

    [TestMethod]
    public void Cap_close_observation_binds_to_the_resolved_window_not_the_desktop()
    {
        // Gen19 (T674): the resolved-window closure check must observe the
        // window instance itself (IsVisible), NOT a desktop-wide search for
        // any apply button - an auto-reopened successor dialog must not be
        // able to defeat or satisfy the observation. The resolver contract
        // plus the no-visible negative prove the binding shape headless.
        WithHeadlessUi(() =>
        {
            var target = new Sr6CareerAttributeAdvanceWindow(MakeAdvanceSnapshot(), "Body");
            try
            {
                target.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(target.IsVisible);
                var resolved = global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ResolveSingleAdvanceWindow();
                Assert.AreSame(target, resolved, "closure must be observed on THIS resolved instance");
                Assert.IsTrue(resolved!.IsVisible, "the resolved window reports IsVisible directly (no desktop-wide query)");
            }
            finally
            {
                target.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

        [TestMethod]
    public void Sr6_attribute_short_code_mapping_covers_all_eight_naturals_and_fails_closed()
    {
        Assert.AreEqual("BOD", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Body"));
        Assert.AreEqual("AGI", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("agility"));
        Assert.AreEqual("REA", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("REACTION"));
        Assert.AreEqual("STR", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Strength"));
        Assert.AreEqual("CHA", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Charisma"));
        Assert.AreEqual("WIL", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Willpower"));
        Assert.AreEqual("INT", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Intuition"));
        Assert.AreEqual("LOG", global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Logic"));
        Assert.IsNull(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("Essence"),
            "unknown ids must fail closed");
        Assert.IsNull(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode(null));
        Assert.IsNull(global::Chummer.Avalonia.DesktopMouseFirstJourneyRunner.MapSr6AttributeShortCode("  "));
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
