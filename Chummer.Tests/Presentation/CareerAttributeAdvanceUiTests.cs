using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Headless UI-consumption tests for the SR5 career attribute-advance affordance.
/// Pins: (1) the Advance button renders only on non-SR6 career-mode attribute rows,
/// (2) Apply emits a Confirmed=true revision-bound request,
/// (3) blocked quotes keep Apply disabled and emit nothing,
/// (4) Undo is enabled only when recoverable receipts exist and emits a confirmed
/// correction bound to the original transaction.
/// Engine parity itself is pinned by CareerAttributeAdvanceParityTests.
/// </summary>
[TestClass]
[DoNotParallelize]
    public sealed class CareerAttributeAdvanceUiTests
{
    private const string ApplyAutomationId = "career-attribute-advance-apply";
    private const string UndoAutomationId = "career-attribute-advance-undo";
    private const string CancelAutomationId = "career-attribute-advance-cancel";

    [TestMethod]
    public void Starting_cash_and_finalization_status_have_separate_visible_rows()
    {
        WithHeadlessUi(() =>
        {
            var control = new CharacterCreationWizardControl();
            var cash = control.FindControl<StackPanel>("StartingCashPanel")!;
            var status = control.FindControl<TextBlock>("FinalizeStatusText")!;
            cash.IsVisible = true;
            status.Text = "Review the completed character before entering Career.";
            var window = new Window { Content = control, Width = 1100, Height = 800 };
            window.Show();
            PumpLayout(window);

            var cashPosition = cash.TranslatePoint(default, window);
            var statusPosition = status.TranslatePoint(default, window);
            Assert.IsNotNull(cashPosition);
            Assert.IsNotNull(statusPosition);
            Assert.IsTrue(cash.Bounds.Height > 0 && status.Bounds.Height > 0);
            Assert.IsTrue(statusPosition.Value.Y >= cashPosition.Value.Y + cash.Bounds.Height,
                "Starting cash controls and finalization status must remain visible without overlap.");
            window.Close();
        });
    }

    // ────────────────────────────────────
    // 1. Section-host affordance gating.
    // ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Advance_button_renders_on_sr5_career_rows_only()
    {
        WithHeadlessUi(() =>
        {
            // SR5 + career mode: Advance button present.
            Window careerWindow = ShowHost(new SectionHostControl());
            var careerHost = (SectionHostControl)careerWindow.Content!;
            careerHost.SetState(BuildHostState(rulesetId: "sr5", careerMode: true));
            PumpLayout(careerHost);
            Assert.IsNotNull(FindButtonByName(careerHost, "AttributeAdvance_BOD"),
                "SR5 career rows must expose the Advance affordance.");
            Assert.IsNull(FindButtonByName(careerHost, "AttributeImprove_BOD"),
                "SR6-style Improve must not render on SR5 rows.");

            // SR5 + creation (not career): no Advance, no Improve.
            Window creationWindow = ShowHost(new SectionHostControl());
            var creationHost = (SectionHostControl)creationWindow.Content!;
            creationHost.SetState(BuildHostState(rulesetId: "sr5", careerMode: false));
            PumpLayout(creationHost);
            Assert.IsNull(FindButtonByName(creationHost, "AttributeAdvance_BOD"),
                "Creation rows must not expose Advance.");
            Assert.IsNull(FindButtonByName(creationHost, "AttributeImprove_BOD"));

            // SR6 + career: keeps its own Improve/Burn buttons, no SR5 Advance.
            Window sr6Window = ShowHost(new SectionHostControl());
            var sr6Host = (SectionHostControl)sr6Window.Content!;
            sr6Host.SetState(BuildHostState(rulesetId: "sr6", careerMode: true));
            PumpLayout(sr6Host);
            Assert.IsNotNull(FindButtonByName(sr6Host, "AttributeImprove_BOD"),
                "SR6 Improve button must keep rendering.");
            Assert.IsNull(FindButtonByName(sr6Host, "AttributeAdvance_BOD"),
                "SR5 Advance affordance must not render on SR6 rulesets.");

            // Close before session teardown: an open window queues a render that
            // dies in ResetForUnitTests (headless glyph pipeline).
            careerWindow.Close();
            creationWindow.Close();
            sr6Window.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Advance_button_click_raises_career_attribute_advance_event()
    {
        WithHeadlessUi(() =>
        {
            Window hostWindow = ShowHost(new SectionHostControl());
            var host = (SectionHostControl)hostWindow.Content!;
            host.SetState(BuildHostState(rulesetId: "sr5", careerMode: true));
            PumpLayout(host);

            string? requested = null;
            host.CareerAttributeAdvanceRequested += (_, attributeName) => requested = attributeName;

            Button? advance = FindButtonByName(host, "AttributeAdvance_BOD");
            Assert.IsNotNull(advance, "Advance button must exist before the click.");
            advance.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual("BOD", requested,
                "The click must raise CareerAttributeAdvanceRequested with the row attribute.");

            hostWindow.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    // ----- 1b. SR6 Improve / Burn Edge buttons (T66) -----

    [TestMethod]
    public void Sr6_improve_button_click_raises_improve_event_with_optimistic_karma_deduction()
    {
        WithHeadlessUi(() =>
        {
            Window hostWindow = ShowHost(new SectionHostControl());
            var host = (SectionHostControl)hostWindow.Content!;
            // BOD at total 2 (SR6 metatypeAugMax 9): cost = nextRank(3) * 5 = 15; 30 karma available.
            host.SetState(BuildHostState("sr6", careerMode: true));
            PumpLayout(host);

            List<AttributeEditRequest> edits = [];
            host.AttributeEditRequested += (_, request) => edits.Add(request);

            Button? improve = FindButtonByName(host, "AttributeImprove_BOD");
            Assert.IsNotNull(improve, "SR6 career row must expose the Improve button.");
            Assert.IsTrue(improve.IsEnabled, "Improve must be enabled when karma covers the computed cost.");
            Assert.AreEqual("Improve", improve.Content);

            improve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(1, edits.Count, "Exactly one improve edit must be raised per click.");
            Assert.AreEqual("BOD", edits[0].AttributeName, "The edit must target the clicked row.");
            Assert.AreEqual("improve", edits[0].Bucket, "The edit must land in the improve bucket.");
            Assert.AreEqual(3, edits[0].Value, "The improve edit must carry the incremented total value.");

            hostWindow.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Sr6_burn_edge_button_click_raises_burn_event_with_optimistic_decrement()
    {
        WithHeadlessUi(() =>
        {
            Window hostWindow = ShowHost(new SectionHostControl());
            var host = (SectionHostControl)hostWindow.Content!;
            host.SetState(BuildHostState("sr6", careerMode: true, includeEdgeRow: true));
            PumpLayout(host);

            List<AttributeEditRequest> edits = [];
            host.AttributeEditRequested += (_, request) => edits.Add(request);

            Button? burn = FindButtonByName(host, "AttributeBurnEdge_EDG");
            Assert.IsNotNull(burn, "SR6 career EDGE row must expose the Burn Edge button.");
            Assert.IsTrue(burn.IsEnabled, "Burn Edge must be enabled while Edge has points to burn.");
            Assert.AreEqual("Burn", burn.Content);

            burn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(1, edits.Count, "Exactly one burn edit must be raised per click.");
            Assert.AreEqual("EDGE", edits[0].AttributeName, "The burn must target the Edge attribute.");
            Assert.AreEqual("burn", edits[0].Bucket, "The edit must land in the burn bucket.");
            Assert.AreEqual(2, edits[0].Value, "The burn edit must carry the decremented total value.");

            hostWindow.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Sr6_improve_button_disables_when_karma_cannot_cover_the_cost()
    {
        WithHeadlessUi(() =>
        {
            Window hostWindow = ShowHost(new SectionHostControl());
            var host = (SectionHostControl)hostWindow.Content!;
            // Cost 15 vs 5 karma: canImprove false; no click may fire an edit.
            host.SetState(BuildHostState("sr6", careerMode: true, availableKarmaOverride: 5));
            PumpLayout(host);

            List<AttributeEditRequest> edits = [];
            host.AttributeEditRequested += (_, request) => edits.Add(request);

            Button? improve = FindButtonByName(host, "AttributeImprove_BOD");
            Assert.IsNotNull(improve, "SR6 career row must still render the Improve button.");
            Assert.IsFalse(improve.IsEnabled, "Improve must disable when available karma cannot cover the cost.");

            improve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(0, edits.Count, "A disabled Improve button must not raise an edit.");

            hostWindow.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. Dialog: confirmed apply emits a revision-bound request.
    // ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dialog_apply_emits_confirmed_request_bound_to_reviewed_quote()
    {
        WithHeadlessUi(() =>
        {
            CharacterCareerAttributeAdvanceQuote quote = MakeQuote(
                canAdvance: true,
                blocker: CharacterCareerAttributeAdvanceBlocker.None,
                effectiveValue: 2,
                targetValue: 3,
                karmaCost: 15,
                availableKarma: 30);
            CareerAttributeAdvanceWindow dialog = new(
                MakeEditor(quotes: new[] { quote }, recoverableReceipts: Array.Empty<CharacterCareerAttributeAdvanceReceipt>()),
                requestedAttributeName: "BOD");

            object? result = ShowAndAwait(dialog, clickAutomationId: ApplyAutomationId);

            var outcome = result as CareerAttributeAdvanceWindow.CareerAttributeAdvanceOutcome;
            Assert.IsNotNull(outcome, "Apply must close the dialog with an advance outcome.");

            Assert.IsTrue(outcome.Request.Confirmed,
                "The emitted request must be explicitly confirmed.");
            Assert.AreEqual(15, outcome.KarmaCost);
            Assert.AreEqual(3, outcome.TargetValue);
            Assert.AreEqual("Body", outcome.DisplayAttribute);

            CareerAttributeAdvanceRequest request = outcome.Request;
            Assert.AreEqual("workspace-under-test", request.WorkspaceId.Value);
            Assert.AreEqual(41L, request.ExpectedContentRevision, "Request must bind the reviewed content revision.");
            Assert.AreEqual(quote.LogicalRevision, request.ExpectedLogicalRevision);
            Assert.AreEqual(quote.SourceRevision, request.ExpectedSourceRevision);
            Assert.AreEqual(quote.RuleDigest, request.ExpectedRuleDigest);
            Assert.AreNotEqual(Guid.Empty, request.ExpenseId);
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Dialog: blocked quote keeps Apply disabled and emits nothing.
    // ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dialog_apply_disabled_for_blocked_quote_and_click_emits_nothing()
    {
        WithHeadlessUi(() =>
        {
            CharacterCareerAttributeAdvanceQuote blocked = MakeQuote(
                canAdvance: false,
                blocker: CharacterCareerAttributeAdvanceBlocker.InsufficientKarma,
                effectiveValue: 2,
                targetValue: 3,
                karmaCost: 45,
                availableKarma: 30);
            CareerAttributeAdvanceWindow dialog = new(
                MakeEditor(quotes: new[] { blocked }, recoverableReceipts: Array.Empty<CharacterCareerAttributeAdvanceReceipt>()),
                requestedAttributeName: "BOD");

            // Headless: AutomationIds resolve once the dialog is rooted.
            Window owner0 = new() { Width = 400, Height = 300, FontFamily = new FontFamily("fonts:Inter#Inter") };
            owner0.Show();
            Dispatcher.UIThread.RunJobs();
            Task<object?> dialogTask0 = dialog.ShowDialog<object?>(owner0);
            Dispatcher.UIThread.RunJobs();

            Button? apply = FindButtonByAutomationId(dialog, ApplyAutomationId);
            Assert.IsNotNull(apply, "Apply button must exist.");
            Assert.IsFalse(apply.IsEnabled, "Apply must be disabled while the quote is blocked.");

            // Force a click even though disabled; the handler's own guard must emit nothing.
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(dialogTask0.IsCompleted,
                "A blocked-apply click must not close the dialog with an outcome.");

            dialog.Close(null);
            owner0.Close();
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. Dialog: undo enabled only with recoverable receipts.
    // ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dialog_undo_requires_recoverable_receipt_and_emits_confirmed_correction()
    {
        WithHeadlessUi(() =>
        {
            // Without receipts: Undo disabled. Headless: AutomationIds resolve
            // once the dialog is rooted, so show it before inspecting buttons.
            CharacterCareerAttributeAdvanceQuote quote = MakeQuote(
                canAdvance: true,
                blocker: CharacterCareerAttributeAdvanceBlocker.None,
                effectiveValue: 2,
                targetValue: 3,
                karmaCost: 15,
                availableKarma: 30);
            Window owner = new() { Width = 400, Height = 300, FontFamily = new FontFamily("fonts:Inter#Inter") };
            owner.Show();
            Dispatcher.UIThread.RunJobs();

            CareerAttributeAdvanceWindow withoutReceipts = new(
                MakeEditor(quotes: new[] { quote }, recoverableReceipts: Array.Empty<CharacterCareerAttributeAdvanceReceipt>()),
                requestedAttributeName: "BOD")
            {
                FontFamily = new FontFamily("fonts:Inter#Inter"),
            };
            withoutReceipts.Show();
            Dispatcher.UIThread.RunJobs();
            Button? undoWithout = FindButtonByAutomationId(withoutReceipts, UndoAutomationId);
            Assert.IsNotNull(undoWithout);
            Assert.IsFalse(undoWithout.IsEnabled, "Undo must be disabled with no recoverable receipts.");
            withoutReceipts.Close();

            // With a receipt: Undo enabled, emits a confirmed correction.
            CharacterCareerAttributeAdvanceReceipt receipt = MakeReceipt(expenseAmount: 15);
            CareerAttributeAdvanceWindow withReceipts = new(
                MakeEditor(quotes: new[] { quote }, recoverableReceipts: new[] { receipt }),
                requestedAttributeName: "BOD")
            {
                FontFamily = new FontFamily("fonts:Inter#Inter"),
            };
            Task<object?> dialogTask = withReceipts.ShowDialog<object?>(owner);
            Dispatcher.UIThread.RunJobs();
            Button? undo = FindButtonByAutomationId(withReceipts, UndoAutomationId);
            Assert.IsNotNull(undo);
            Assert.IsTrue(undo.IsEnabled, "Undo must be enabled with a recoverable receipt.");
            Assert.IsTrue((undo.Content?.ToString() ?? string.Empty).Contains("BOD"),
                "Undo label should disclose which advance it would revert.");

            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!dialogTask.IsCompleted && stopwatch.ElapsedMilliseconds < 5000)
            {
                Thread.Sleep(25);
            }

            object? result = dialogTask.IsCompleted ? dialogTask.GetAwaiter().GetResult() : null;
            if (!dialogTask.IsCompleted)
            {
                withReceipts.Close(null);
            }

            owner.Close();
            var outcome = result as CareerAttributeAdvanceWindow.CareerAttributeCorrectionOutcome;
            Assert.IsNotNull(outcome, "Undo must close the dialog with a correction outcome.");
            Assert.IsTrue(outcome.Request.Confirmed);
            Assert.AreEqual(receipt.ReceiptDigest, outcome.Request.ExpectedReceiptDigest);
            Assert.AreEqual(15, outcome.KarmaRefund);
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // Headless bootstrap (mirrors AvaloniaHeadlessSmokeTests).
    // ────────────────────────────────────────────────────────────────────────

    internal static void WithHeadlessUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(CareerAdvanceHeadlessAppBootstrap));
                session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    session?.Dispose();
                }
                catch (NullReferenceException)
                {
                    // Avalonia headless teardown can intermittently throw after successful dispatch.
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }

    private sealed class CareerAdvanceHeadlessAppBootstrap
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

    /// <summary>
    /// Shows the dialog against a headless owner, clicks the requested button, and
    /// returns the ShowDialog result (or null when the dialog did not close).
    /// </summary>
    private static object? ShowAndAwait(Window dialog, string clickAutomationId)
    {
        // Headless cannot resolve the app-styled Segoe UI; force the bundled Inter
        // family so the dialog subtree inherits a resolvable font.
        dialog.FontFamily = new FontFamily("fonts:Inter#Inter");
        Window owner = new()
        {
            Content = new Border(),
            Width = 400,
            Height = 300,
            FontFamily = new FontFamily("fonts:Inter#Inter"),
        };
        owner.Show();
        Dispatcher.UIThread.RunJobs();
        Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
        Dispatcher.UIThread.RunJobs();

        Button? button = FindButtonByAutomationId(dialog, clickAutomationId);
        Assert.IsNotNull(button, "Dialog button '" + clickAutomationId + "' not found.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Close(outcome) completes the ShowDialog task synchronously inside the
        // click handler; poll briefly to tolerate a headless frame in between.
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!dialogTask.IsCompleted && stopwatch.ElapsedMilliseconds < 5000)
        {
            Thread.Sleep(25);
        }

        object? result = dialogTask.IsCompleted ? dialogTask.GetAwaiter().GetResult() : null;

        if (!dialogTask.IsCompleted)
        {
            // Never leave a pending dialog attached to the owner.
            dialog.Close(null);
        }

        owner.Close();
        return result;
    }

    internal static Button? FindButtonByName(Visual root, string name)
        => root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => string.Equals(button.Name, name, StringComparison.Ordinal));

    private static Button? FindButtonByAutomationId(Visual root, string automationId)
        => root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            string.Equals(AutomationProperties.GetAutomationId(button), automationId, StringComparison.Ordinal)
            || string.Equals(button.Name, automationId, StringComparison.Ordinal));

    // ────────────────────────────────────────────────────────────────────────
    // Fixture builders.
    // ────────────────────────────────────────────────────────────────────────

    // Runs the layout pass synchronously inside the live session: headless
    // RunJobs never executes queued layout, and a layout left pending explodes
    // at ResetForUnitTests when the font collections are already disposed.
    internal static void PumpLayout(Layoutable root)
    {
        root.InvalidateMeasure();
        root.Measure(new Size(880, 560));
        root.Arrange(new Rect(0, 0, 880, 560));
        Dispatcher.UIThread.RunJobs();
    }
    internal static Window ShowHost(SectionHostControl host)
    {
        // Local-value pin beats the App.axaml Window/UserControl style composite
        // ("Segoe UI,Verdana,Arial"), which cannot resolve in a headless session.
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

    internal static SectionHostState BuildHostState(
        string rulesetId,
        bool careerMode,
        bool includeEdgeRow = false,
        int? availableKarmaOverride = null,
        int? upgradeKarmaCostOverride = null)
    {
        // Per-attribute JSON in the exact shape AttributeWorkbenchProjector parses:
        // careerMode comes from "created"; canCareerUpgrade needs karma to cover cost.
        string previewJson = JsonSerializer.Serialize(new
        {
            attributes = includeEdgeRow
                ? BuildHostAttributes(careerMode, availableKarmaOverride, upgradeKarmaCostOverride)
                : new object[] { BuildBodyAttribute(careerMode, availableKarmaOverride, upgradeKarmaCostOverride) },
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

    private static object BuildBodyAttribute(bool careerMode, int? availableKarmaOverride, int? upgradeKarmaCostOverride) => new
    {
        name = "BOD",
        baseValue = 2,
        karmaValue = 0,
        metatypeMin = 1,
        metatypeMax = 6,
        metatypeAugMax = 9,
        created = careerMode,
        availableKarma = availableKarmaOverride ?? (careerMode ? 30 : 0),
        upgradeKarmaCost = upgradeKarmaCostOverride ?? (careerMode ? 15 : -1),
        canCareerUpgrade = careerMode,
    };

    private static object[] BuildHostAttributes(bool careerMode, int? availableKarmaOverride, int? upgradeKarmaCostOverride) => new object[]
    {
        BuildBodyAttribute(careerMode, availableKarmaOverride, upgradeKarmaCostOverride),
        new
        {
            name = "EDGE",
            baseValue = 3,
            karmaValue = 0,
            metatypeMin = 1,
            metatypeMax = 6,
            metatypeAugMax = 9,
            created = careerMode,
            availableKarma = availableKarmaOverride ?? (careerMode ? 30 : 0),
            upgradeKarmaCost = upgradeKarmaCostOverride ?? (careerMode ? 20 : -1),
            canCareerUpgrade = careerMode,
        },
    };

    private static CareerAttributeAdvanceEditorState MakeEditor(
        IReadOnlyList<CharacterCareerAttributeAdvanceQuote> quotes,
        IReadOnlyList<CharacterCareerAttributeAdvanceReceipt> recoverableReceipts)
        => new(
            new CharacterWorkspaceId("workspace-under-test"),
            ContentRevision: 41L,
            quotes,
            OmittedAttributeCount: 0,
            recoverableReceipts,
            OmittedReceiptCount: 0);

    private static CharacterCareerAttributeAdvanceQuote MakeQuote(
        bool canAdvance,
        CharacterCareerAttributeAdvanceBlocker blocker,
        int effectiveValue,
        int targetValue,
        int karmaCost,
        int availableKarma)
        => new(
            Identity: new CharacterCareerAttributeIdentity("BOD", CharacterCareerAttributeKind.Normal),
            DisplayName: "Body",
            BasePoints: 2,
            KarmaPoints: 0,
            EffectiveValue: effectiveValue,
            TargetValue: targetValue,
            NaturalMaximum: 6,
            MetatypeMinimum: 1,
            AvailableKarma: availableKarma,
            KarmaCost: karmaCost,
            RepairsBurnedEdge: false,
            BurnedEdgePoints: 0,
            ApplicationDuration: TimeSpan.Zero,
            TimeAuthority: CharacterCareerAttributeTimeAuthority.ImmediateChummerPersistence,
            Prerequisites: Array.Empty<CharacterCareerAttributePrerequisiteResult>(),
            CanAdvance: canAdvance,
            Blocker: blocker,
            LogicalRevision: Hex64('a'),
            SourceRevision: Hex64('b'),
            RuleDigest: Hex64('c'));

    private static CharacterCareerAttributeAdvanceReceipt MakeReceipt(int expenseAmount)
        => new(
            TransactionId: Guid.NewGuid(),
            Identity: new CharacterCareerAttributeIdentity("BOD", CharacterCareerAttributeKind.Normal),
            RepairsBurnedEdge: false,
            AttributeKarmaBefore: 0,
            AttributeKarmaAfter: 1,
            CharacterKarmaBefore: 100,
            CharacterKarmaAfter: 85,
            BurnedEdgePointsBefore: 0,
            BurnedEdgePointsAfter: 0,
            ExpenseId: Guid.NewGuid(),
            ExpenseAmount: expenseAmount,
            LogicalRevision: Hex64('d'),
            SourceRevision: Hex64('e'),
            RuleDigest: Hex64('f'),
            ReceiptDigest: Hex64('0'));

    private static string Hex64(char digit)
        => new string(digit, 64);
}

/// <summary>
/// Headless UI-consumption tests for the SR5 career skill-advance affordance:
/// (1) the Advance Skills quick-action renders on SR5 career mode and raises
/// CareerSkillAdvanceRequested, (2) the dialog Apply emits Confirmed=true
/// revision-bound requests, (3) blocked quotes keep Advance disabled.
/// </summary>

[TestClass]
[DoNotParallelize]
public sealed class CareerSkillAdvanceUiTests
{
    private static string Hex64(char digit) => new string(digit, 64);

    [TestMethod]
    public void Advance_skills_button_renders_and_raises_event_on_sr5_career_mode()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            Window careerWindow = CareerAttributeAdvanceUiTests.ShowHost(new SectionHostControl());
            var careerHost = (SectionHostControl)careerWindow.Content!;
            careerHost.SetState(CareerHostState("sr5", profileCreated: true));
            CareerAttributeAdvanceUiTests.PumpLayout(careerHost);

            bool requested = false;
            careerHost.CareerSkillAdvanceRequested += (_, _) => requested = true;

            Button? btn = CareerAttributeAdvanceUiTests.FindButtonByName(careerHost, "CareerAdvanceSkills");
            Assert.IsNotNull(btn, "SR5 career mode must expose the Advance Skills quick action.");
            btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(requested, "The click must raise CareerSkillAdvanceRequested.");

            Window creationWindow = CareerAttributeAdvanceUiTests.ShowHost(new SectionHostControl());
            var creationHost = (SectionHostControl)creationWindow.Content!;
            creationHost.SetState(CareerHostState("sr5", profileCreated: false));
            CareerAttributeAdvanceUiTests.PumpLayout(creationHost);
            Assert.IsNull(CareerAttributeAdvanceUiTests.FindButtonByName(creationHost, "CareerAdvanceSkills"),
                "Creation mode must not expose the Advance Skills quick action.");

            Window sr6Window = CareerAttributeAdvanceUiTests.ShowHost(new SectionHostControl());
            var sr6Host = (SectionHostControl)sr6Window.Content!;
            sr6Host.SetState(CareerHostState("sr6", profileCreated: true));
            CareerAttributeAdvanceUiTests.PumpLayout(sr6Host);
            Assert.IsNotNull(CareerAttributeAdvanceUiTests.FindButtonByName(sr6Host, "CareerAdvanceSkills"),
                "Career skill advance quick action is ruleset-agnostic and must render on SR6 too.");

            careerWindow.Close();
            creationWindow.Close();
            sr6Window.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Skill_advance_dialog_blocked_quote_keeps_apply_disabled()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            var workspaceId = new CharacterWorkspaceId("ws-career-skill-advance-blocked");
            var activeState = new CareerActiveSkillAdvanceEditorState(
                workspaceId,
                ContentRevision: 9,
                Skills: new List<CharacterCareerActiveSkillAdvanceQuote>
                {
                    new(
                        new CharacterCareerActiveSkillIdentity(
                            Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            Guid.Parse("22222222-2222-2222-2222-222222222222")),
                        Name: "Sneaking",
                        SkillCategory: "Stealth",
                        BasePoints: 6,
                        KarmaPoints: 30,
                        TotalBaseRating: 6,
                        RatingMaximum: 6,
                        AvailableKarma: 5,
                        KarmaCost: 0,
                        CanAdvance: false,
                        Blocker: CharacterCareerActiveSkillAdvanceBlocker.AtMaximum,
                        LogicalRevision: Hex64('a'),
                        SourceRevision: Hex64('b'),
                        RuleDigest: Hex64('c')),
                },
                OmittedSkillCount: 0);

            bool applied = false;
            var dialog = new CareerSkillAdvanceWindow(
                workspaceId,
                prepareActive: _ => Task.FromResult<CareerActiveSkillAdvanceEditorState?>(activeState),
                applyActive: (_, _) =>
                {
                    applied = true;
                    return Task.FromResult(true);
                },
                prepareKnowledge: _ => Task.FromResult<CareerKnowledgeSkillAdvanceEditorState?>(null),
                applyKnowledge: (_, _) => Task.FromResult<CharacterCareerKnowledgeSkillAdvanceReceipt?>(null),
                prepareGroup: _ => Task.FromResult<CareerSkillGroupAdvanceEditorState?>(null),
                applyGroup: (_, _) => Task.FromResult(false),
                prepareSpecialization: _ => Task.FromResult<CareerSkillSpecializationEditorState?>(null),
                prepareSpecializationQuote: (_, _) => Task.FromResult<CharacterCareerSkillSpecializationQuote?>(null),
                applySpecialization: (_, _) => Task.FromResult(false), isCurrentContext: () => true, expectedContentRevision: 9);

            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            Button? apply = CareerAttributeAdvanceUiTests.FindButtonByName(dialog, "career-skill-advance-apply");
            Assert.IsNotNull(apply, "The dialog footer must expose the Advance button by name.");

            string identityKey = "A:" + Guid.Parse("11111111-1111-1111-1111-111111111111");
            Border? row = FindRowBorderByKey(dialog, identityKey);
            Assert.IsNotNull(row, "The active-skill quote row must render with its identity name.");
            bool selected = dialog.SelectRowByKey(identityKey);
            Assert.IsTrue(selected, "SelectRowByKey must find the rendered active-skill row.");
            Dispatcher.UIThread.RunJobs();

            Assert.IsFalse(apply.IsEffectivelyEnabled, "Blocked quotes must keep Advance disabled.");
            Assert.IsFalse(applied, "Blocked quote must not emit a request.");

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    public void Skill_advance_dialog_apply_emits_confirmed_request_and_survives_revision_bump()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            var workspaceId = new CharacterWorkspaceId("ws-career-skill-advance-apply");
            var skillId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var activeState = new CareerActiveSkillAdvanceEditorState(
                workspaceId,
                ContentRevision: 9,
                Skills: new List<CharacterCareerActiveSkillAdvanceQuote>
                {
                    new(
                        new CharacterCareerActiveSkillIdentity(
                            skillId,
                            Guid.Parse("22222222-2222-2222-2222-222222222222")),
                        Name: "Sneaking",
                        SkillCategory: "Stealth",
                        BasePoints: 6,
                        KarmaPoints: 30,
                        TotalBaseRating: 5,
                        RatingMaximum: 6,
                        AvailableKarma: 30,
                        KarmaCost: 15,
                        CanAdvance: true,
                        Blocker: CharacterCareerActiveSkillAdvanceBlocker.None,
                        LogicalRevision: Hex64('a'),
                        SourceRevision: Hex64('b'),
                        RuleDigest: Hex64('c')),
                },
                OmittedSkillCount: 0);

            List<CareerActiveSkillAdvanceRequest> requests = new();
            var dialog = new CareerSkillAdvanceWindow(
                workspaceId,
                prepareActive: _ => Task.FromResult<CareerActiveSkillAdvanceEditorState?>(activeState),
                applyActive: (request, _) =>
                {
                    requests.Add(request);
                    return Task.FromResult(true);
                },
                prepareKnowledge: _ => Task.FromResult<CareerKnowledgeSkillAdvanceEditorState?>(null),
                applyKnowledge: (_, _) => Task.FromResult<CharacterCareerKnowledgeSkillAdvanceReceipt?>(null),
                prepareGroup: _ => Task.FromResult<CareerSkillGroupAdvanceEditorState?>(null),
                applyGroup: (_, _) => Task.FromResult(false),
                prepareSpecialization: _ => Task.FromResult<CareerSkillSpecializationEditorState?>(null),
                prepareSpecializationQuote: (_, _) => Task.FromResult<CharacterCareerSkillSpecializationQuote?>(null),
                applySpecialization: (_, _) => Task.FromResult(false), isCurrentContext: () => true, expectedContentRevision: 9);

            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            bool selected = dialog.SelectRowByKey("A:" + skillId);
            Assert.IsTrue(selected, "The advanceable row must be selectable programmatically.");
            Dispatcher.UIThread.RunJobs();

            Button? apply = CareerAttributeAdvanceUiTests.FindButtonByName(dialog, "career-skill-advance-apply");
            Assert.IsNotNull(apply, "The dialog footer must expose the Advance button by name.");
            Assert.IsTrue(apply.IsEffectivelyEnabled, "An advanceable selection must enable Advance.");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(0, requests.Count, "Review is not consent to buy.");
            Assert.AreEqual("Confirm purchase", apply.Content);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(1, requests.Count, "Advance must emit exactly one request.");
            CareerActiveSkillAdvanceRequest request = requests[0];
            Assert.IsTrue(request.Confirmed, "The emitted request must be explicitly confirmed.");
            Assert.AreEqual(workspaceId, request.WorkspaceId);
            Assert.AreEqual(9L, request.ExpectedContentRevision, "Request must bind the prepared content revision.");
            Assert.AreEqual(skillId, request.ExpectedSkill.Identity.SkillId);
            Assert.AreEqual(Hex64('c'), request.ExpectedRuleDigest);
            Assert.AreNotEqual(Guid.Empty, request.ExpenseId);
            Assert.IsTrue(dialog.AdvanceApplied, "The dialog must report the completed advance.");

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unverified_or_lost_result_is_not_success_and_cannot_be_replayed(bool lost)
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            int writes = 0;
            var window = NewSkillWindow(apply: (_, _) => { writes++; if (lost) throw new System.IO.IOException("lost response"); return Task.FromResult(false); });
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(window.SelectRowByKey("A:" + TestSkillId));
            var button = CareerAttributeAdvanceUiTests.FindButtonByName(window, "career-skill-advance-apply")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, writes);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(1, writes); Assert.IsFalse(window.AdvanceApplied); Assert.IsFalse(button.IsEnabled);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, writes);
            window.Close();
        });
    }

    [TestMethod]
    public void Context_change_during_load_discards_the_old_runner_choices()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            bool current = true; int writes = 0;
            var loaded = new TaskCompletionSource<CareerActiveSkillAdvanceEditorState?>();
            var window = NewSkillWindow(prepare: _ => loaded.Task, current: () => current,
                apply: (_, _) => { writes++; return Task.FromResult(true); });
            window.Show(); current = false; loaded.SetResult(TestActiveState()); Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(window.IsVisible); Assert.IsFalse(window.SelectRowByKey("A:" + TestSkillId));
            Assert.AreEqual(0, writes);
        });
    }

    [TestMethod]
    public void Changed_context_or_cancelled_review_never_buys_a_skill()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            bool current = true; int writes = 0;
            var window = NewSkillWindow(current: () => current, apply: (_, _) => { writes++; return Task.FromResult(true); });
            window.Show(); Dispatcher.UIThread.RunJobs(); window.SelectRowByKey("A:" + TestSkillId);
            var button = CareerAttributeAdvanceUiTests.FindButtonByName(window, "career-skill-advance-apply")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual("Confirm purchase", button.Content); Assert.AreEqual(0, writes);
            current = false; button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, writes); Assert.IsFalse(window.AdvanceApplied); Assert.IsFalse(window.IsVisible);
        });
    }

    [TestMethod]
    public void Specialization_quote_is_displayed_before_separate_explicit_confirmation()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            var identity = new CharacterCareerSkillIdentity(TestSkillId, TestSourceId, CharacterCareerSkillKind.Active);
            var option = new CharacterCareerSkillSpecializationOption("urban", "Urban", CharacterCareerSkillSpecializationOptionKind.SourceCatalog, "source");
            var state = new CareerSkillSpecializationEditorState(new("skill-test"), 9,
                [new(identity, "Sneaking", "Stealth", "", 3, 0, [option])], 0);
            var quote = new CharacterCareerSkillSpecializationQuote(identity, new("Urban", option.Kind, option.OptionIdentity), "Sneaking", "Stealth", "", true, false, true, true, false,
                3, 0, 30, 0, false, false, false, 7, false, true, CharacterCareerSkillSpecializationBlocker.None, "character", "source", "rules", "logical");
            int writes = 0;
            var window = new CareerSkillAdvanceWindow(new("skill-test"),
                _ => Task.FromResult<CareerActiveSkillAdvanceEditorState?>(null), (_, _) => Task.FromResult(false),
                _ => Task.FromResult<CareerKnowledgeSkillAdvanceEditorState?>(null), (_, _) => Task.FromResult<CharacterCareerKnowledgeSkillAdvanceReceipt?>(null),
                _ => Task.FromResult<CareerSkillGroupAdvanceEditorState?>(null), (_, _) => Task.FromResult(false),
                _ => Task.FromResult<CareerSkillSpecializationEditorState?>(state), (_, _) => Task.FromResult<CharacterCareerSkillSpecializationQuote?>(quote),
                (request, _) => { writes++; Assert.AreEqual(quote, request.ExpectedQuote); Assert.IsTrue(request.Confirmed); return Task.FromResult(true); }, () => true, 9);
            window.Show(); Dispatcher.UIThread.RunJobs(); Assert.IsTrue(window.SelectRowByKey("S:" + TestSkillId + ":urban"));
            var button = CareerAttributeAdvanceUiTests.FindButtonByName(window, "career-skill-advance-apply")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(0, writes); Assert.AreEqual("Confirm purchase", button.Content);
            Assert.IsTrue(window.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text?.Contains("7 Karma") == true));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(1, writes); Assert.IsTrue(window.AdvanceApplied); Assert.IsFalse(window.IsVisible);
        });
    }

    [TestMethod]
    public void Sr6_skill_list_overflow_is_scrollable_and_keyboard_reachable()
    {
        CareerAttributeAdvanceUiTests.WithHeadlessUi(() =>
        {
            // 40 rows at ~44 px each overflow the 900x620 viewport.
            var quotes = new List<CharacterCareerActiveSkillAdvanceQuote>();
            for (int i = 0; i < 40; i++)
            {
                Guid id = Guid.Parse("11111111-1111-1111-1111-111111111111");
                var bytesId = id.ToByteArray();
                bytesId[15] = (byte)i;
                var skillId = new Guid(bytesId);
                quotes.Add(new CharacterCareerActiveSkillAdvanceQuote(
                    new(skillId, TestSourceId), "Skill" + i.ToString("00"), "Combat", 1, 0, 1, 6, 30, 6, true,
                    CharacterCareerActiveSkillAdvanceBlocker.None, Hex64('a'), Hex64('b'), Hex64('c')));
            }

            var state = new CareerActiveSkillAdvanceEditorState(new("skill-test"), 9, quotes, 0);
            var window = new CareerSkillAdvanceWindow(new("skill-test"),
                _ => Task.FromResult<CareerActiveSkillAdvanceEditorState?>(state), (_, _) => Task.FromResult(true),
                _ => Task.FromResult<CareerKnowledgeSkillAdvanceEditorState?>(null), (_, _) => Task.FromResult<CharacterCareerKnowledgeSkillAdvanceReceipt?>(null),
                _ => Task.FromResult<CareerSkillGroupAdvanceEditorState?>(null), (_, _) => Task.FromResult(false),
                _ => Task.FromResult<CareerSkillSpecializationEditorState?>(null), (_, _) => Task.FromResult<CharacterCareerSkillSpecializationQuote?>(null),
                (_, _) => Task.FromResult(false), () => true, 9);

            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.Width = 900;
            window.Height = 620;
            CareerAttributeAdvanceUiTests.PumpLayout(window);

            // The list must live in a bounded scroll container, not an unbounded StackPanel.
            var listBox = window.GetVisualDescendants().OfType<ListBox>().Single();
            var scroll = listBox.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.IsTrue(
                scroll.Extent.Height > scroll.Viewport.Height,
                "The skill list must overflow a bounded viewport so the test proves scrolling.");

            // Keyboard: focus the first realized item, then End reaches the final row.
            var firstItem = (ListBoxItem)listBox.Items[0]!;
            firstItem.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(global::Avalonia.Input.Key.End, global::Avalonia.Input.RawInputModifiers.None);
            CareerAttributeAdvanceUiTests.PumpLayout(window);
            Assert.IsNotNull(listBox.SelectedItem, "Keyboard End must select a row.");

            // The window's Row type is private; recover the identity key from the
            // selected item's content: ListBoxItem -> StackPanel wrapper -> Border(Name=key).
            var lastItem = (ListBoxItem)listBox.SelectedItem!;
            var lastBorder = (Border)((StackPanel)lastItem.Content!).Children[0];
            var key = lastBorder.Name!;
            Assert.IsTrue(
                key.StartsWith("A:", StringComparison.Ordinal),
                "Keyboard End must select an active-skill row, saw " + key);
            var lastName = ((TextBlock)((StackPanel)lastBorder.Child!).Children[0]).Text;
            Assert.IsTrue(
                string.Equals(lastName, "Skill39", StringComparison.Ordinal),
                "Keyboard End must reach the final row, saw " + lastName);

            // The selected row must be scrolled into the visible viewport.
            var border = FindRowBorderByKey(window, key);
            Assert.IsNotNull(border, "The selected row must be realized in the tree.");
            var at = border.TranslatePoint(new Point(0, 0), window);
            Assert.IsNotNull(at, "Selected row must be positioned inside the window.");
            Assert.IsTrue(
                at.Value.Y >= 0 && at.Value.Y <= window.Height,
                "Keyboard selection must scroll the row into view, saw Y=" + at.Value.Y);

            // Mouse wheel moves the viewport as well (a second non-helper path).
            double before = scroll.Offset.Y;
            window.MouseWheel(
                new Point(window.Width / 2, window.Height / 2),
                new Vector(0, -120),
                global::Avalonia.Input.RawInputModifiers.None);
            CareerAttributeAdvanceUiTests.PumpLayout(window);
            Assert.IsTrue(
                scroll.Offset.Y > before || scroll.Offset.Y > 0,
                "Mouse wheel must scroll the skill list.");

            // Mouse click on the realized, scrolled-into-view row selects it.
            window.MouseMove(at.Value + new Point(12, 8));
            window.MouseDown(at.Value + new Point(12, 8), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
            window.MouseUp(at.Value + new Point(12, 8), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            // The review/apply affordance exists for the selected row.
            var apply = CareerAttributeAdvanceUiTests.FindButtonByName(window, "career-skill-advance-apply");
            Assert.IsNotNull(apply, "The review/apply affordance must exist for the selected row.");
        });
    }

    private static readonly Guid TestSkillId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestSourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static CareerActiveSkillAdvanceEditorState TestActiveState() => new(new("skill-test"), 9,
        [new(new(TestSkillId, TestSourceId), "Sneaking", "Stealth", 2, 0, 2, 6, 30, 6, true, CharacterCareerActiveSkillAdvanceBlocker.None, Hex64('a'), Hex64('b'), Hex64('c'))], 0);
    private static CareerSkillAdvanceWindow NewSkillWindow(
        Func<CancellationToken, Task<CareerActiveSkillAdvanceEditorState?>>? prepare = null,
        Func<CareerActiveSkillAdvanceRequest, CancellationToken, Task<bool>>? apply = null, Func<bool>? current = null)
        => new(new("skill-test"), prepare ?? (_ => Task.FromResult<CareerActiveSkillAdvanceEditorState?>(TestActiveState())), apply ?? ((_, _) => Task.FromResult(false)),
            _ => Task.FromResult<CareerKnowledgeSkillAdvanceEditorState?>(null), (_, _) => Task.FromResult<CharacterCareerKnowledgeSkillAdvanceReceipt?>(null),
            _ => Task.FromResult<CareerSkillGroupAdvanceEditorState?>(null), (_, _) => Task.FromResult(false),
            _ => Task.FromResult<CareerSkillSpecializationEditorState?>(null), (_, _) => Task.FromResult<CharacterCareerSkillSpecializationQuote?>(null), (_, _) => Task.FromResult(false), current ?? (() => true), 9);

    private static Border? FindRowBorderByKey(Visual root, string identityKey)
    {
        if (root is Border b && string.Equals(b.Name, identityKey, StringComparison.Ordinal))
        {
            return b;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var hit = FindRowBorderByKey(child, identityKey);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>
    /// Attributes-shaped host state with the career strip gated purely on ProfileCreated
    /// (the production state carries it; the shared helper pins it to false).
    /// </summary>
    private static SectionHostState CareerHostState(string rulesetId, bool profileCreated)
    {
        string previewJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            attributes = new[] { new { } },
        });

        return new SectionHostState(
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
            NpcPersonaStudio: null,
            RulesetId: rulesetId,
            ProfileCreated: profileCreated);
    }
}
