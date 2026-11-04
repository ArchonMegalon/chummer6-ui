using System;
using System.Linq;
using System.Threading;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Owners;
using Chummer.Avalonia.Creation;
using Chummer.Contracts.Characters;
using Chummer.Presentation;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// WINDOWS_HANDOFF 2026-11-02 item 2: the Finish-with-AI affordance ships gated
/// dark. These are the first headless tests for that surface: default-off, the
/// explicit session opt-in, the projection choke point (no busy/result bypass),
/// and the stable environment-switch contract.
/// </summary>
[TestClass]
public sealed class CharacterCreationAiFinishGateAvaloniaTests
{
    private const string FinishWithAiAutomationId = "creation-wizard-finish-with-ai";

    [TestMethod]
    public void Finish_with_ai_button_is_dark_until_the_feature_gate_is_enabled()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            // Overview projects full availability; the gate still keeps the
            // affordance dark by default.
            control.SetFinishWithAiAvailability(true);
            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsFalse(button.IsVisible, "Finish-with-AI must ship dark by default.");
            Assert.IsFalse(button.IsEnabled);

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;
            RaiseClick(button);
            Assert.AreEqual(
                0, raiseCount, "A gated click must not raise FinishWithAiRequested.");

            host.Close();
        });
    }

    [TestMethod]
    public void Finish_with_ai_button_projects_once_the_feature_gate_is_enabled()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;

            control.SetFinishWithAiAvailability(true);
            control.SetFinishWithAiFeatureEnabled(true);

            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsTrue(button.IsVisible, "Enabled gate + availability must surface the affordance.");
            Assert.IsTrue(button.IsEnabled);
            RaiseClick(button);
            Assert.AreEqual(1, raiseCount);

            host.Close();
        });
    }

    [TestMethod]
    public void Finish_with_ai_busy_and_result_writers_cannot_bypass_the_gate()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            control.SetFinishWithAiAvailability(true);
            control.SetFinishWithAiFeatureEnabled(true);
            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsTrue(button.IsVisible);

            // While busy the button hides; a failure result arriving mid-flight
            // must not resurface it (the pre-gate writers did exactly that).
            control.SetFinishWithAiBusy(true);
            Assert.IsFalse(button.IsVisible);
            control.SetFinishWithAiResult(false, "Build Ghost proposal dismissed.");
            Assert.IsFalse(button.IsVisible, "Result writers must not bypass the gate.");

            // Busy completes: the affordance returns, still under the gate.
            control.SetFinishWithAiBusy(false);
            Assert.IsTrue(button.IsVisible);
            Assert.IsTrue(button.IsEnabled);

            // Disabling the feature re-hides immediately, availability aside.
            control.SetFinishWithAiFeatureEnabled(false);
            Assert.IsFalse(button.IsVisible);
            Assert.IsFalse(button.IsEnabled);

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;
            RaiseClick(button);
            Assert.AreEqual(0, raiseCount, "A disabled gate must swallow clicks.");

            host.Close();
        });
    }

    [TestMethod]
    public void Ai_finish_gate_session_env_contract_is_stable()
    {
        Assert.AreEqual(
            "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH",
            CharacterCreationAiFinishGatePolicy.SessionEnvVariable);
    }

    private static T FindByAutomationId<T>(Control root, string automationId)
        where T : Control
        => root.GetVisualDescendants()
            .Prepend(root)
            .OfType<T>()
            .Single(control => string.Equals(
                control.GetValue(AutomationProperties.AutomationIdProperty),
                automationId,
                StringComparison.Ordinal));

    private static void RaiseClick(Button button)
        => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    internal static void WithHeadlessUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(AiFinishGateHeadlessAppBootstrap));
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
            }
        }
    }

    private sealed class AiFinishGateHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .ConfigureFonts(static manager => manager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
                .WithInterFont();
    }

}

[TestClass]
public sealed class CharacterCreationAiFinishRecoveryTests
{
    private string _root = null!;
    private CreationAiFinishRunStore _store = null!;
    private static OwnerContextStamp Owner(string id = "alice", long revision = 1)
        => new(new OwnerScope("install-account:" + id), "authority", revision);
    private const string PLAN = "{\"plan\":1}";
    private static CreationAiFinishService.AiFinishDomainResult Success(string domain)
        => new(domain, true, CharacterCreationFoundationOutcomes.Success, []);

    [TestInitialize] public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "chummer-ai-recovery-" + Guid.NewGuid().ToString("N"));
        _store = new(_root);
    }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    public void A_failed_step_stops_all_later_writes_and_preserves_confirmed_results()
    {
        int later = 0;
        var results = _store.Execute(Owner(), "runner", "op1", "{\"reviewed\":true}",
        [
            ("attributes", () => Success("attributes")),
            ("skills", () => new("skills", false, "blocked", ["Core rejected this selection"])),
            ("qualities", () => { later++; return Success("qualities"); })
        ]);
        Assert.HasCount(2, results); Assert.IsTrue(results[0].Success); Assert.IsFalse(results[1].Success);
        Assert.AreEqual(0, later);
        var recorded = new CreationAiFinishRunStore(_root).Read(Owner(), "runner")!;
        Assert.AreEqual("{\"reviewed\":true}", recorded[0].PlanJson);
        Assert.IsFalse(recorded.Any(e => e.Domain == "qualities"));
    }

    [TestMethod]
    public void Commit_then_lost_response_survives_restart_and_never_dispatches_again()
    {
        int writes = 0;
        _store.Execute(Owner(), "runner", "original", "{}",
            [("skills", () => { writes++; throw new IOException("response lost after commit"); })]);
        var restarted = new CreationAiFinishRunStore(_root);
        var record = restarted.Read(Owner(revision: 3), "runner")!;
        Assert.AreEqual("original", record[0].OperationId);
        Assert.IsFalse(record.Last().Result!.Success);
        Assert.ThrowsExactly<IOException>(() => restarted.Execute(Owner(revision: 3), "runner", "replacement", "{}",
            [("skills", () => { writes++; return Success("skills"); })]));
        Assert.AreEqual(1, writes, "A changed operation id or fresh process cannot bypass the workspace fence.");
    }

    [TestMethod]
    public void Interrupted_and_malformed_records_cannot_authorize_replay()
    {
        _store.Execute(Owner(), "runner", "op1", "{}", [("attributes", () => Success("attributes"))]);
        string path = Directory.GetFiles(_root).Single();
        string[] lines = File.ReadAllLines(path);
        File.WriteAllLines(path, lines.Take(2)); // crash after durable dispatch marker
        var restarted = new CreationAiFinishRunStore(_root);
        Assert.AreEqual("dispatching", restarted.Read(Owner(), "runner")!.Last().Kind);
        int calls = 0;
        Assert.ThrowsExactly<IOException>(() => restarted.Execute(Owner(), "runner", "op2", "{}",
            [("attributes", () => { calls++; return Success("attributes"); })]));
        File.WriteAllText(path, "partial-json");
        Assert.ThrowsExactly<InvalidOperationException>(() => restarted.Read(Owner(), "runner"));
        File.WriteAllText(path, "");
        Assert.ThrowsExactly<InvalidOperationException>(() => restarted.Read(Owner(), "runner"));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Recovery_records_are_partitioned_by_owner_and_workspace()
    {
        _store.Execute(Owner(), "runner", "op1", "{}", [("attributes", () => Success("attributes"))]);
        Assert.IsNull(_store.Read(Owner("bob"), "runner"));
        Assert.IsNull(_store.Read(Owner(), "another"));
        Assert.IsNotNull(_store.Read(Owner(revision: 7), "runner"), "Fresh authorization may inspect the same owner's original record.");
    }

    [TestMethod]
    public void Guest_or_invalid_owner_cannot_start_even_an_operator_qualification_run()
    {
        foreach (var owner in new[] { default(OwnerContextStamp), new(OwnerScope.LocalSingleUser, "authority", 1),
            new(new OwnerScope("local-single-user"), "authority", 1), new(new OwnerScope("install-account:"), "authority", 1) })
        {
            Assert.IsFalse(CreationAiFinishRunStore.IsLinkedOwner(owner));
            Assert.ThrowsExactly<InvalidOperationException>(() => _store.Execute(owner, "runner", "op", "{}", []));
        }
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void Service_rejects_guest_and_stale_owner_before_any_character_read()
    {
        var authority = new TestOwnerAuthority { Stamp = Owner() };
        var service = new CreationAiFinishService(
            DispatchProxy.Create<IWorkspaceStore, UnusedDependency>(),
            DispatchProxy.Create<ICharacterSourceDataResolver, UnusedDependency>(),
            DispatchProxy.Create<ICharacterFileQueries, UnusedDependency>(), authority, _store);
        var original = authority.Stamp;
        Assert.IsTrue(service.IsCurrentOwner(original));
        authority.Stamp = Owner("bob", 2);
        Assert.IsFalse(service.IsCurrentOwner(original));
        authority.Stamp = Owner(revision: 3);
        Assert.IsFalse(service.IsCurrentOwner(original), "Returning to an account does not revive its old authority stamp.");
        Assert.IsTrue(service.IsCurrentOwner(authority.Stamp));
        authority.Stamp = new(OwnerScope.LocalSingleUser, "authority", 4);
        Assert.ThrowsExactly<InvalidOperationException>(() => service.BuildProposal("runner"));
        Assert.ThrowsExactly<InvalidOperationException>(() => service.InspectSavedResult("runner", original));
        Assert.IsFalse(Directory.Exists(_root));
    }

    public class UnusedDependency : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new AssertFailedException("Unauthorized character read: " + targetMethod?.Name);
    }

    private sealed class TestOwnerAuthority : IOwnerContextLeaseAccessor
    {
        public OwnerContextStamp Stamp { get; set; }
        public OwnerScope Current => Stamp.Owner;
        public OwnerContextStamp Capture() => Stamp;
        public bool TryAcquire(OwnerContextStamp expected, out IOwnerContextLease? lease)
        { lease = null; return false; }
    }


    // ------------------------------------------------------------------
    // Qualified durable staged resumption (WINDOWS_AI_RECOVERY.md):
    // resume continues the SAME frozen plan under the SAME operation
    // identity inside the SAME exclusive fence. Exact receipt reconciliation
    // first; only the single uncertain domain is re-dispatched.
    // ------------------------------------------------------------------

    [TestMethod]
    public void Resume_completes_the_uncertain_domain_after_an_interrupted_dispatch()
    {
        // Simulate a crash after the durable dispatch marker for "skills".
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        string[] lines = File.ReadAllLines(path);
        File.WriteAllLines(path, lines.Take(4)); // claimed, dispatching+observed attributes, dispatching skills

        var record = _store.Read(Owner(), "runner")!;
        Assert.AreEqual("dispatching", record.Last().Kind);
        Assert.AreEqual("skills", record.Last().Domain);

        int skillsDispatch = 0;
        var receipt = _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN,
            [("skills", () => { skillsDispatch++; return Success("skills"); })]);

        Assert.AreEqual("skills", receipt.ResumedDomain);
        Assert.AreEqual("op1", receipt.OperationId);
        Assert.AreEqual(1, skillsDispatch, "only the uncertain domain is re-dispatched");
        Assert.IsTrue(receipt.Results.All(r => r.Success));
        var after = _store.Read(Owner(), "runner")!;
        Assert.AreEqual("completed", after.Last().Kind, "resume appends into the SAME fence and completes it");
    }

    [TestMethod]
    public void Resume_refuses_a_plan_that_differs_from_the_claimed_one()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        string[] lines = File.ReadAllLines(path);
        File.WriteAllLines(path, lines.Take(4)); // leave "skills" uncertain

        int dispatch = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN + " ",
                [("skills", () => { dispatch++; return Success("skills"); })]));
        Assert.AreEqual(0, dispatch, "a plan that does not byte-match the claimed receipt can never be resumed");
        // The fence is untouched: the uncertain domain stays pending.
        Assert.AreEqual("dispatching", _store.Read(Owner(), "runner")!.Last().Kind);
    }

    [TestMethod]
    public void Resume_refuses_a_changed_operation_identity()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));

        int dispatch = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _store.ReconcileAndResume(Owner(), "runner", "op2", PLAN,
                [("skills", () => { dispatch++; return Success("skills"); })]));
        Assert.AreEqual(0, dispatch, "a different operation identity is a different run, not a resume");
    }

    [TestMethod]
    public void Resume_refuses_an_already_completed_or_fully_observed_record()
    {
        // Completed record: nothing uncertain left.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        int dispatch = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN,
                [("skills", () => { dispatch++; return Success("skills"); })]));
        Assert.AreEqual(0, dispatch, "a completed run must not be re-dispatched");
    }

    [TestMethod]
    public void Resume_across_processes_and_owner_revisions_appends_to_the_same_fence()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));

        // A fresh store instance (fresh process) with a fresh authorization revision.
        var restarted = new CreationAiFinishRunStore(_root);
        int dispatch = 0;
        var receipt = restarted.ReconcileAndResume(Owner(revision: 9), "runner", "op1", PLAN,
            [("skills", () => { dispatch++; return Success("skills"); })]);

        Assert.AreEqual("skills", receipt.ResumedDomain);
        Assert.AreEqual(1, dispatch);
        var after = restarted.Read(Owner(revision: 9), "runner")!;
        Assert.AreEqual("completed", after.Last().Kind);
        Assert.AreEqual(1, after.Count(e => e.Kind == "observed" && e.Domain == "skills"),
            "exactly one re-dispatch is journaled for the uncertain domain");
    }

    [TestMethod]
    public void Resume_of_a_failed_dispatch_reuses_the_original_operation_id_and_stays_appended()
    {
        // A resume whose re-dispatch fails keeps the record pending and appends
        // its observed receipt; nothing is deleted or replayed.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));

        var receipt = _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN,
            [("skills", () => new("skills", false, "blocked", ["Core rejected this selection"]))]);

        Assert.IsFalse(receipt.Results.Single().Success);
        var after = _store.Read(Owner(), "runner")!;
        Assert.AreEqual("observed", after.Last().Kind);
        Assert.IsFalse(after.Last().Result!.Success);
        Assert.AreNotEqual("completed", after.Last().Kind);
    }

    [TestMethod]
    public void Resume_of_a_throwing_redispatch_reports_an_uncertain_outcome_and_never_completes()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));

        var receipt = _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN,
            [("skills", () => throw new IOException("response lost again"))]);

        Assert.IsFalse(receipt.Results.Single().Success);
        Assert.AreEqual(
            "resumption-outcome-uncertain",
            receipt.Results.Single().Outcome,
            "a lost re-dispatch response stays uncertain, never a clean failure");
        var after = _store.Read(Owner(), "runner")!;
        Assert.AreEqual("observed", after.Last().Kind);
        Assert.AreNotEqual("completed", after.Last().Kind,
            "an uncertain re-dispatch must never mark the run completed");
    }

    [TestMethod]
    public void Resume_refuses_steps_that_do_not_match_the_uncertain_domain()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _store.ReconcileAndResume(Owner(), "runner", "op1", PLAN,
                [("attributes", () => Success("attributes"))]));
    }

    [TestMethod]
    public void Service_resume_refuses_a_missing_record_and_a_different_account()
    {
        var authority = new TestOwnerAuthority { Stamp = Owner() };
        var service = new CreationAiFinishService(
            DispatchProxy.Create<IWorkspaceStore, UnusedDependency>(),
            DispatchProxy.Create<ICharacterSourceDataResolver, UnusedDependency>(),
            DispatchProxy.Create<ICharacterFileQueries, UnusedDependency>(), authority, _store);

        // No record at all: nothing to resume, and no character read is needed
        // to find that out.
        Assert.ThrowsExactly<InvalidOperationException>(() => service.ResumeSavedResult("runner"));

        // A run by alice exists; a different account must not resume it.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes"))]);
        authority.Stamp = Owner("bob", 2);
        Assert.ThrowsExactly<InvalidOperationException>(() => service.ResumeSavedResult("runner"),
            "an account transition must not resume another account's saved run");
        Assert.AreEqual("completed", _store.Read(Owner(), "runner")!.Last().Kind,
            "the refused resume leaves the original record untouched");
    }

    [TestMethod]
    public void Successful_steps_are_recorded_in_order_and_completed_only_after_all_confirm()
    {
        var results = _store.Execute(Owner(), "runner", "op1", "{}",
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        Assert.IsTrue(results.All(r => r.Success));
        var record = new CreationAiFinishRunStore(_root).Read(Owner(), "runner")!;
        CollectionAssert.AreEqual(new[] { "attributes", "skills" }, record.Where(e => e.Kind == "observed").Select(e => e.Domain).ToArray());
        Assert.AreEqual("completed", record.Last().Kind);
    }
}

[TestClass]
public sealed class CharacterCreationWizardFinalizeAvailabilityAvaloniaTests
{
    private static void WithHeadlessUi(Action action)
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(action);

    [TestMethod]
    public void Starting_a_mutation_hides_and_disables_an_available_finalize_action()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();
            control.SetFinalizeAvailability(true, Array.Empty<string>());
            Assert.IsTrue(control.FinalizeButton.IsVisible);
            Assert.IsTrue(control.FinalizeButton.IsEnabled);

            control.SetFinalizeMutationBusy(true);
            Assert.IsFalse(control.FinalizeButton.IsVisible);
            Assert.IsFalse(control.FinalizeButton.IsEnabled);

            control.SetFinalizeMutationBusy(false);
            Assert.IsTrue(control.FinalizeButton.IsVisible);
            Assert.IsTrue(control.FinalizeButton.IsEnabled);
            host.Close();
        });
    }

    [TestMethod]
    public void Busy_clear_does_not_resurrect_a_finalization_the_overview_denies()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            // Authoritative overview says: not available (blockers present).
            control.SetFinalizeAvailability(false, new[] { "review not complete" });
            Assert.IsFalse(control.FinalizeButton.IsVisible,
                "unavailable finalization must stay hidden");

            // A busy window opens and closes again.
            control.SetFinalizeMutationBusy(true);
            control.SetFinalizeMutationBusy(false);

            Assert.IsFalse(control.FinalizeButton.IsVisible,
                "busy->idle must re-derive from stored availability, not re-enable");

            host.Close();
        });
    }

    [TestMethod]
    public void Rejected_finalize_stays_disabled_after_busy_clears()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            control.SetFinalizeAvailability(true, Array.Empty<string>());
            Assert.IsTrue(control.FinalizeButton.IsVisible,
                "available finalization must be visible");
            Assert.IsTrue(control.FinalizeButton.IsEnabled);

            // The authoritative reject arrives, then the busy flag clears.
            control.SetFinalizeResult("rejected", Array.Empty<string>());
            control.SetFinalizeMutationBusy(false);

            Assert.IsFalse(control.FinalizeButton.IsVisible,
                "a rejected finalize must not come back after busy clears");

            host.Close();
        });
    }

    [TestMethod]
    public void Availability_is_restored_when_overview_turns_positive_again()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            control.SetFinalizeAvailability(false, new[] { "review not complete" });
            control.SetFinalizeMutationBusy(true);
            control.SetFinalizeMutationBusy(false);
            Assert.IsFalse(control.FinalizeButton.IsVisible);

            // Overview turns positive: the button must come back.
            control.SetFinalizeAvailability(true, Array.Empty<string>());
            Assert.IsTrue(control.FinalizeButton.IsVisible,
                "a fresh authoritative availability must show the button again");

            host.Close();
        });
    }
}
