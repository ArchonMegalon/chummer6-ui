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
                        return Task.FromResult<string?>("expected content revision 9 but the workspace is at 10");
                    }

                    return Task.FromResult<string?>(null);
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
    public void Inline_apply_uncertain_outcome_disables_record_and_never_invites_reconfirm()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            int applies = 0;
            int reprepares = 0;
            bool uncertain = true;
            var dialog = new CareerRewardsWindow(
                isKarmaMode: true,
                MakeKarmaEditor(),
                null,
                applyKarma: (_, _) => { applies++; return Task.FromResult<string?>("commit reply lost"); },
                applyNuyen: null,
                reprepare: _ => { reprepares++; return Task.FromResult(((CareerManualKarmaEditorState?)null, (CareerManualNuyenEditorState?)null)); },
                contextIsCurrent: () => true,
                peekUncertain: () => uncertain);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "7";
            FindByAutomation<TextBox>(dialog, "career-rewards-reason")!.Text = "Uncertain run";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(1, applies);
            Assert.IsFalse(dialogTask.IsCompleted, "an uncertain outcome must keep the dialog open.");
            Assert.IsFalse(apply.IsEnabled, "Record must stay disabled after an uncertain outcome.");
            Assert.IsTrue(dialog.UncertainOutcome);
            Assert.AreEqual("commit reply lost", dialog.LastRefusal);
            Assert.AreEqual(0, reprepares, "an uncertain outcome must not re-prepare a fresh editor state.");
            string status = FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text ?? string.Empty;
            Assert.IsTrue(status.Contains("Uncertain result:"), $"status must disclose uncertainty, got: {status}");
            Assert.IsTrue(status.Contains("may already be recorded"), $"status must warn about a possible commit, got: {status}");
            Assert.IsFalse(status.Contains("press Record again"), "an uncertain outcome must never invite a re-confirm.");

            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(1, applies, "a disabled Record must not dispatch again.");

            dialog.Close();
            owner.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Inline_apply_uncertain_nuyen_outcome_keeps_reconciliation_read_only()
    {
        WithHeadlessUi(() =>
        {
            Window owner = new() { Width = 300, Height = 200 };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            int applies = 0;
            bool uncertain = true;
            var dialog = new CareerRewardsWindow(
                isKarmaMode: false,
                MakeKarmaEditor(),
                MakeNuyenEditor(4200m),
                applyKarma: null,
                applyNuyen: (_, _) => { applies++; return Task.FromResult<string?>("commit reply lost"); },
                reprepare: null,
                contextIsCurrent: () => true,
                peekUncertain: () => uncertain);
            Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();

            FindByAutomation<TextBox>(dialog, "career-rewards-amount")!.Text = "800";
            Button apply = FindByAutomation<Button>(dialog, "career-rewards-apply")!;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(1, applies);
            Assert.IsFalse(dialogTask.IsCompleted, "an uncertain outcome must keep the dialog open.");
            Assert.IsFalse(apply.IsEnabled, "Record must stay disabled after an uncertain outcome.");
            Assert.IsTrue(dialog.UncertainOutcome);
            string status = FindByAutomation<TextBlock>(dialog, "career-rewards-balance")!.Text ?? string.Empty;
            Assert.IsTrue(status.Contains("Uncertain result:"), $"status must disclose uncertainty, got: {status}");
            Assert.IsFalse(status.Contains("press Record again"), "an uncertain outcome must never invite a re-confirm.");

            dialog.Close();
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
                    return Task.FromResult<string?>(null);
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
            var applyGate = new TaskCompletionSource<string?>();
            var prepareGate = new TaskCompletionSource<(CareerManualKarmaEditorState?, CareerManualNuyenEditorState?)>();
            int applies = 0, prepares = 0;
            long appliedRevision = 0;
            var dialog = new CareerRewardsWindow(true, MakeKarmaEditor(), null,
                (request, _) =>
                {
                    appliedRevision = request.ExpectedContentRevision;
                    return ++applies == 1 ? applyGate.Task : Task.FromResult<string?>(null);
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
            applyGate.SetResult("stale revision");
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
            var pending = new TaskCompletionSource<string?>();
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
            pending.SetResult("stale revision");
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
                (_, _) => { applies++; return Task.FromResult<string?>("stale runner"); }, null,
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
            current = false;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, applies, "an owner transition requires a new editor gesture");
            dialog.Close();
            owner.Close();
        });
    }

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
