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

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // 1. Section-host affordance gating.
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // 2. Dialog: confirmed apply emits a revision-bound request.
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // 3. Dialog: blocked quote keeps Apply disabled and emits nothing.
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // 4. Dialog: undo enabled only with recoverable receipts.
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // Headless bootstrap (mirrors AvaloniaHeadlessSmokeTests).
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static void WithHeadlessUi(Action action)
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

    private static Button? FindButtonByName(Visual root, string name)
        => root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => string.Equals(button.Name, name, StringComparison.Ordinal));

    private static Button? FindButtonByAutomationId(Visual root, string automationId)
        => root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            string.Equals(AutomationProperties.GetAutomationId(button), automationId, StringComparison.Ordinal)
            || string.Equals(button.Name, automationId, StringComparison.Ordinal));

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    // Fixture builders.
    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    // Runs the layout pass synchronously inside the live session: headless
    // RunJobs never executes queued layout, and a layout left pending explodes
    // at ResetForUnitTests when the font collections are already disposed.
    private static void PumpLayout(Layoutable root)
    {
        root.InvalidateMeasure();
        root.Measure(new Size(880, 560));
        root.Arrange(new Rect(0, 0, 880, 560));
        Dispatcher.UIThread.RunJobs();
    }
    private static Window ShowHost(SectionHostControl host)
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

    private static SectionHostState BuildHostState(
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
