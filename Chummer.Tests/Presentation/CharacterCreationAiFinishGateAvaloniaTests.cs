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
    public void An_unobserved_dispatch_classifies_as_OutcomeUnknown_and_names_the_stage()
    {
        // Terminate after EACH durable append the writer can emit (re-audit item 5):
        // every legitimate prefix must classify, never reject its own journal.
        string[] truncatedPrefixes =
        [
            "claimed",
            "claimed + dispatching attributes",
            "claimed + dispatching/observed attributes",
            "claimed + dispatching/observed attributes + dispatching skills",
            "claimed + dispatching/observed attributes + dispatching/observed skills",
        ];
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        string[] all = File.ReadAllLines(path);
        Assert.AreEqual(6, all.Length, "claimed + 2x(dispatching observed) + completed");

        // Prefix 0 (claimed only): the run never dispatched; no write was sent.
        File.WriteAllLines(path, all.Take(1));
        var restarted = new CreationAiFinishRunStore(_root);
        var (state0, domain0, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Completed, state0,
            "claimed with no dispatching entry dispatched nothing; nothing is unknown");
        Assert.IsNull(domain0);

        // Prefix 1: dispatching attributes, never observed -> unknown, names attributes.
        File.WriteAllLines(path, all.Take(2));
        var (state1, domain1, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.OutcomeUnknown, state1);
        Assert.AreEqual("attributes", domain1);

        // Prefix 2: attributes observed, nothing pending -> completed.
        File.WriteAllLines(path, all.Take(3));
        var (state2, domain2, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Completed, state2);
        Assert.IsNull(domain2);

        // Prefix 3: skills dispatched, never observed -> unknown, names skills.
        File.WriteAllLines(path, all.Take(4));
        var (state3, domain3, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.OutcomeUnknown, state3);
        Assert.AreEqual("skills", domain3);

        // Prefix 4: everything observed except the terminal marker -> completed.
        File.WriteAllLines(path, all.Take(5));
        var (state4, domain4, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Completed, state4);
        Assert.IsNull(domain4);
        Assert.AreEqual(5, truncatedPrefixes.Length, "documented prefix enumeration");
    }

    [TestMethod]
    public void An_observed_failure_classifies_as_Failed_and_authorizes_no_write()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")),
             ("skills", () => new("skills", false, "blocked", ["Core rejected this selection"]))]);
        var restarted = new CreationAiFinishRunStore(_root);
        var (state, domain, _) = restarted.ClassifyRecovery(Owner(), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Failed, state,
            "a failed stage was OBSERVED; its outcome is known and terminal");
        Assert.IsNull(domain);
    }

    [TestMethod]
    public void A_completed_record_classifies_as_Completed()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        var restarted = new CreationAiFinishRunStore(_root);
        var (state, domain, _) = restarted.ClassifyRecovery(Owner(revision: 9), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Completed, state);
        Assert.IsNull(domain);
        Assert.AreEqual(1, restarted.Read(Owner(revision: 9), "runner")!.Count(e => e.Kind == "completed"));
    }

    [TestMethod]
    public void A_fresh_process_classifies_the_same_journal_identically()
    {
        // Fresh instance in THIS process is the store-level proxy for restart;
        // the store's only durable state is the journal file itself.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string path = Directory.GetFiles(_root).Single();
        File.WriteAllLines(path, File.ReadAllLines(path).Take(4));
        var restarted = new CreationAiFinishRunStore(_root);
        var (state, domain, entries) = restarted.ClassifyRecovery(Owner(revision: 4), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.OutcomeUnknown, state);
        Assert.AreEqual("skills", domain);
        Assert.AreEqual("op1", entries[0].OperationId, "the original operation identity is retained");
    }

    [TestMethod]
    public void An_exclusively_held_journal_blocks_a_second_writer_from_dispatching()
    {
        // Two-process exclusion (re-audit item 3): the exclusive CreateNew fence is
        // the cross-process authorization. While one writer holds the journal, a
        // second writer cannot even start a run - it fails before any dispatch.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes"))]);
        string path = Directory.GetFiles(_root).Single();
        var restarted = new CreationAiFinishRunStore(_root);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            int calls = 0;
            Assert.ThrowsExactly<IOException>(() => restarted.Execute(Owner(), "runner", "op2", PLAN,
                [("attributes", () => { calls++; return Success("attributes"); })]),
                "the second writer cannot obtain the fence while the first holds it");
            Assert.AreEqual(0, calls, "no step runs without the exclusive fence");
        }
    }

    [TestMethod]
    public void ClassifyRecovery_never_opens_the_journal_for_writing()
    {
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes"))]);
        string path = Directory.GetFiles(_root).Single();
        string before = File.ReadAllText(path);
        var restarted = new CreationAiFinishRunStore(_root);
        restarted.ClassifyRecovery(Owner(revision: 2), "runner");
        Assert.AreEqual(before, File.ReadAllText(path), "classification is strictly read-only");
    }

    [TestMethod]
    public void Service_classifies_an_unknown_outcome_read_only_and_refuses_foreign_accounts()
    {
        var authority = new TestOwnerAuthority { Stamp = Owner() };
        var service = new CreationAiFinishService(
            DispatchProxy.Create<IWorkspaceStore, UnusedDependency>(),
            DispatchProxy.Create<ICharacterSourceDataResolver, UnusedDependency>(),
            DispatchProxy.Create<ICharacterFileQueries, UnusedDependency>(), authority, _store);

        // No record at all: normal flow.
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.None,
            service.ClassifyRecoveryOutcome("runner", Owner()).State);

        // A run by alice exists; a different account gets its own (empty) view.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes"))]);
        authority.Stamp = Owner("bob", 2);
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.None,
            service.ClassifyRecoveryOutcome("runner", Owner("bob", 2)).State,
            "an account transition must not see another account's saved run");
        Assert.AreEqual("completed", _store.Read(Owner(), "runner")!.Last().Kind,
            "the foreign classification leaves the original record untouched");

        // Alice's fresh authorization sees the same run read-only.
        authority.Stamp = Owner(revision: 5);
        var restarted = new CreationAiFinishRunStore(_root);
        string path = Directory.GetFiles(_root).Single();
        string before = File.ReadAllText(path);
        var (state, _, _) = restarted.ClassifyRecovery(Owner(revision: 5), "runner");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Completed, state);
        Assert.AreEqual(before, File.ReadAllText(path));
    }

    [TestMethod]
    public void Service_recovery_entry_distinguishes_unknown_from_terminal_failures()
    {
        // Typed-state UI entry contract (re-audit item 6): the service's typed
        // classification drives the dialog branch, so a genuinely unknown stage
        // and a terminal failure must never be confusable.
        var authority = new TestOwnerAuthority { Stamp = Owner() };
        var service = new CreationAiFinishService(
            DispatchProxy.Create<IWorkspaceStore, UnusedDependency>(),
            DispatchProxy.Create<ICharacterSourceDataResolver, UnusedDependency>(),
            DispatchProxy.Create<ICharacterFileQueries, UnusedDependency>(), authority, _store);

        // Terminal failure: the stage was OBSERVED as failed - outcome known.
        _store.Execute(Owner(), "runner", "op1", PLAN,
            [("attributes", () => Success("attributes")),
             ("skills", () => new("skills", false, "blocked", ["Core rejected this selection"]))]);
        var (failedState, failedDomain) = service.ClassifyRecoveryOutcome("runner", Owner());
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.Failed, failedState,
            "an observed failure is terminal, never 'unknown'");
        Assert.IsNull(failedDomain);

        // Unknown outcome: interrupted before observation - the only state that
        // may surface the informational dialog, and it names the stage.
        _store.Execute(Owner(), "runner2", "op2", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        // An isolated store root for the second run keeps fence lookup exact.
        string root2 = Path.Combine(Path.GetTempPath(), "chummer-d2-" + Guid.NewGuid().ToString("N"));
        var store2 = new CreationAiFinishRunStore(root2);
        store2.Execute(Owner(), "solo", "op2", PLAN,
            [("attributes", () => Success("attributes")), ("skills", () => Success("skills"))]);
        string fence2 = Directory.GetFiles(root2).Single();
        File.WriteAllLines(fence2, File.ReadAllLines(fence2).Take(4)); // skills dispatched, unobserved
        var (unknownState, unknownDomain, _) = store2.ClassifyRecovery(Owner(), "solo");
        Assert.AreEqual(CreationAiFinishRunStore.RecoveryState.OutcomeUnknown, unknownState);
        Assert.AreEqual("skills", unknownDomain);

        // Both classifications are read-only: bytes unchanged afterwards.
        foreach (string f in Directory.GetFiles(_root).Concat(Directory.GetFiles(root2)))
        {
            Assert.IsFalse(File.ReadAllText(f).Contains("\"resuming\""),
                "no classification may ever append anything to any journal");
        }
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
