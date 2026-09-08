using System.Reflection;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class WorkspaceOverviewPreparationTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(5);
    private static readonly CharacterWorkspaceId Slow = new("slow-creation");
    private static readonly CharacterWorkspaceId Fast = new("fast-creation");

    [TestMethod]
    public async Task Immediate_overview_read_does_not_run_creation_checks_on_the_ui_thread()
    {
        using var fixture = new Fixture();
        var returned = new TaskCompletionSource<Task<WorkspaceOverviewLifecycleResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiContext = new SynchronizationContext();
        int uiThreadId = 0;
        var thread = new Thread(() =>
        {
            uiThreadId = Environment.CurrentManagedThreadId;
            SynchronizationContext.SetSynchronizationContext(uiContext);
            try { returned.SetResult(fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, CancellationToken.None)); }
            catch (Exception error) { returned.SetException(error); }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        }) { IsBackground = true };
        thread.Start();
        try
        {
            await fixture.Finalizer.Entered.Task.WaitAsync(Watchdog);
            Task<WorkspaceOverviewLifecycleResult> pending = await returned.Task.WaitAsync(Watchdog);
            Assert.IsFalse(pending.IsCompleted);
            Assert.AreNotEqual(uiThreadId, fixture.Finalizer.LoadThreadId);
            Assert.IsNull(fixture.Finalizer.LoadContext);
            Assert.IsNull(fixture.Sessions.State.ActiveWorkspaceId);
            fixture.Finalizer.Release.Set();
            WorkspaceOverviewLifecycleResult completed = await pending.WaitAsync(Watchdog);
            Assert.IsTrue(completed.CanPublish);
            Assert.AreEqual(Slow, completed.State.WorkspaceId);
            Assert.AreSame(fixture.Overview.Document, completed.RecoveryDocument);
            Assert.AreEqual("restored-tab", completed.State.ActiveTabId);
            Assert.AreEqual("restored-action", completed.State.ActiveActionId);
            Assert.AreEqual(1, fixture.Finalizer.LoadCount, "Publishing must not repeat synchronous Core reads.");
        }
        finally
        {
            fixture.Finalizer.Release.Set();
            thread.Join(Watchdog);
            if (returned.Task.IsCompletedSuccessfully)
                await returned.Task.Result.WaitAsync(Watchdog);
        }
    }

    [TestMethod]
    public async Task New_workspace_activation_rejects_a_late_creation_projection()
    {
        using var fixture = new Fixture();
        Task<WorkspaceOverviewLifecycleResult> slow = fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, CancellationToken.None);
        try
        {
            await fixture.Finalizer.Entered.Task.WaitAsync(Watchdog);
            WorkspaceOverviewLifecycleResult fast = await fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Fast, CancellationToken.None).WaitAsync(Watchdog);
            Assert.IsTrue(fast.CanPublish);
            fixture.Finalizer.Release.Set();
            WorkspaceOverviewLifecycleResult stale = await slow.WaitAsync(Watchdog);
            Assert.IsFalse(stale.CanPublish);
            Assert.AreEqual(Fast, fixture.Coordinator.CurrentWorkspaceId);
            Assert.AreEqual(Fast, fixture.Sessions.State.ActiveWorkspaceId);
            Assert.AreEqual(Fast, stale.CurrentWorkspaceId);
        }
        finally { fixture.Finalizer.Release.Set(); await slow.WaitAsync(Watchdog); }
    }

    [TestMethod]
    public async Task Cancellation_after_read_entry_never_activates_or_publishes_the_workspace()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        Task<WorkspaceOverviewLifecycleResult> pending = fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, cancellation.Token);
        try
        {
            await fixture.Finalizer.Entered.Task.WaitAsync(Watchdog);
            cancellation.Cancel();
            fixture.Finalizer.Release.Set();
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(Watchdog));
            Assert.IsNull(fixture.Coordinator.CurrentWorkspaceId);
            Assert.IsNull(fixture.Sessions.State.ActiveWorkspaceId);
            Assert.IsEmpty(fixture.Sessions.State.OpenWorkspaces);
        }
        finally { fixture.Finalizer.Release.Set(); }
    }

    [TestMethod]
    public async Task Same_workspace_reload_invalidates_the_previous_preparation_generation()
    {
        using var fixture = new Fixture();
        Task<WorkspaceOverviewLifecycleResult> first = fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, CancellationToken.None);
        try
        {
            await fixture.Finalizer.Entered.Task.WaitAsync(Watchdog);
            Task<WorkspaceOverviewLifecycleResult> second = fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, CancellationToken.None);
            Assert.IsFalse(second.IsCompleted);
            Assert.AreEqual(1, fixture.Finalizer.LoadCount, "Same-workspace reads must remain serialized.");
            fixture.Finalizer.Release.Set();
            Assert.IsFalse((await first.WaitAsync(Watchdog)).CanPublish);
            Assert.IsTrue((await second.WaitAsync(Watchdog)).CanPublish);
            Assert.AreEqual(2, fixture.Finalizer.LoadCount);
            Assert.AreEqual(Slow, fixture.Sessions.State.ActiveWorkspaceId);
        }
        finally { fixture.Finalizer.Release.Set(); await first.WaitAsync(Watchdog); }
    }

    [TestMethod]
    public async Task Current_creation_read_failure_does_not_partially_activate_the_session()
    {
        using var fixture = new Fixture();
        fixture.Finalizer.ThrowAfterRelease = true;
        fixture.Finalizer.Release.Set();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Coordinator.LoadAsync(
            CharacterOverviewState.Empty, Slow, CancellationToken.None).WaitAsync(Watchdog));
        Assert.IsNull(fixture.Coordinator.CurrentWorkspaceId);
        Assert.IsNull(fixture.Sessions.State.ActiveWorkspaceId);
        Assert.IsEmpty(fixture.Sessions.State.OpenWorkspaces);
    }

    [TestMethod]
    public async Task Canceled_before_entry_does_not_load_creation_domains()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Coordinator.LoadAsync(
            CharacterOverviewState.Empty, Slow, new CancellationToken(canceled: true)));
        Assert.AreEqual(0, fixture.Finalizer.LoadCount);
        Assert.IsNull(fixture.Sessions.State.ActiveWorkspaceId);
    }

    [TestMethod]
    public async Task Late_creation_failure_cannot_replace_a_newly_active_workspace()
    {
        using var fixture = new Fixture();
        fixture.Finalizer.ThrowAfterRelease = true;
        Task<WorkspaceOverviewLifecycleResult> slow = fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Slow, CancellationToken.None);
        try
        {
            await fixture.Finalizer.Entered.Task.WaitAsync(Watchdog);
            WorkspaceOverviewLifecycleResult fast = await fixture.Coordinator.LoadAsync(CharacterOverviewState.Empty, Fast, CancellationToken.None).WaitAsync(Watchdog);
            Assert.IsTrue(fast.CanPublish);
            fixture.Finalizer.Release.Set();
            WorkspaceOverviewLifecycleResult stale = await slow.WaitAsync(Watchdog);
            Assert.IsFalse(stale.CanPublish);
            Assert.AreEqual(Fast, fixture.Sessions.State.ActiveWorkspaceId);
            Assert.AreEqual(Fast, fixture.Coordinator.CurrentWorkspaceId);
        }
        finally { fixture.Finalizer.Release.Set(); await slow.WaitAsync(Watchdog); }
    }

    private sealed class Fixture : IDisposable
    {
        internal WorkspaceOverviewLoadResult Overview { get; } = CharacterCreationWizardPresentationTests.CreateOverview(
            created: false, CharacterCreationBuildMethods.Priority, "<character><created>False</created></character>", revision: 3);
        internal BlockingFinalizer Finalizer { get; } = new();
        internal WorkspaceSessionPresenter Sessions { get; } = new();
        internal WorkspaceOverviewLifecycleCoordinator Coordinator { get; }

        internal Fixture()
        {
            var views = new WorkspaceViewStateStore();
            views.Capture(Slow, CharacterOverviewState.Empty with { ActiveTabId = "restored-tab", ActiveActionId = "restored-action" });
            Coordinator = new WorkspaceOverviewLifecycleCoordinator(
                DispatchProxy.Create<IChummerClient, ForbiddenClient>(), Sessions, new ImmediateLoader(Overview), views,
                new WorkspaceShellStateFactory(), new WorkspaceRemoteCloseService(), new WorkspaceSessionActivationService(),
                new WorkspaceOverviewStateFactory(creationFinalizationService: Finalizer));
        }

        public void Dispose()
        {
            Finalizer.Release.Set();
            Coordinator.Dispose();
            Finalizer.Release.Dispose();
        }
    }

    // These are scheduling fixtures, not rule or recovery authority. Domain
    // readiness is deliberately unavailable; real Core readiness has its own test.
    private sealed class ImmediateLoader(WorkspaceOverviewLoadResult overview) : IWorkspaceOverviewLoader
    {
        public Task<WorkspaceOverviewLoadResult> LoadAsync(IChummerClient client, CharacterWorkspaceId id, CancellationToken ct)
            => Task.FromResult(overview);
    }

    public class ForbiddenClient : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("Scheduling proof must not call a client or mutation route.");
    }

    private sealed class BlockingFinalizer : ICharacterCreationFinalizationService
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        internal int LoadThreadId;
        internal SynchronizationContext? LoadContext;
        internal int LoadCount;
        internal bool ThrowAfterRelease;

        public CharacterCreationFinalizationResult<CharacterCreationFinalizationState> Load(CharacterCreationFinalizationLoadRequest request)
        {
            Interlocked.Increment(ref LoadCount);
            if (request.WorkspaceId == Slow)
            {
                LoadThreadId = Environment.CurrentManagedThreadId;
                LoadContext = SynchronizationContext.Current;
                Entered.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Test release signal missing.");
                if (ThrowAfterRelease) throw new InvalidOperationException("Late read failure.");
            }
            return new(CharacterCreationFinalizationOutcomes.Unavailable, null, [CharacterCreationWizardProjector.FinalizationAuthorityUnavailable]);
        }

        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReview> Review(CharacterCreationFinalizationReviewRequest request)
            => throw new InvalidOperationException("Read-only scheduling proof.");
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> Confirm(CharacterCreationFinalizationConfirmRequest request)
            => throw new InvalidOperationException("Read-only scheduling proof.");
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> LookupReceipt(CharacterCreationFinalizationReceiptLookupRequest request)
            => throw new InvalidOperationException("Read-only scheduling proof.");
    }
}
