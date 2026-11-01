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

    private static CareerManualKarmaEditorState MakeKarmaEditor()
        => new(
            new CharacterWorkspaceId("workspace-under-test"),
            ContentRevision: 9L,
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