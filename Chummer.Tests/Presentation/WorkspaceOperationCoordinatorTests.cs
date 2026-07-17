#nullable enable annotations

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceOperationCoordinatorTests
{
    [TestMethod]
    public async Task New_activation_invalidates_older_completion()
    {
        using WorkspaceOperationCoordinator coordinator = new();
        CharacterWorkspaceId slowWorkspace = new("slow");
        CharacterWorkspaceId fastWorkspace = new("fast");
        TaskCompletionSource<bool> slowStarted = NewSignal();
        TaskCompletionSource<bool> releaseSlow = NewSignal();

        Task<WorkspaceOperationExecution<string>> slow = coordinator.RunActivationAsync(
            slowWorkspace,
            async _ =>
            {
                slowStarted.TrySetResult(true);
                await releaseSlow.Task;
                return "slow";
            },
            CancellationToken.None);
        await slowStarted.Task;

        WorkspaceOperationExecution<string> fast = await coordinator.RunActivationAsync(
            fastWorkspace,
            _ => Task.FromResult("fast"),
            CancellationToken.None);
        releaseSlow.TrySetResult(true);
        WorkspaceOperationExecution<string> stale = await slow;

        Assert.IsTrue(fast.CanPublish);
        Assert.AreEqual("fast", fast.Value);
        Assert.IsFalse(stale.CanPublish);
        Assert.IsTrue(coordinator.IsCurrent(fastWorkspace));
    }

    [TestMethod]
    public async Task Current_operations_are_serialized_per_workspace()
    {
        using WorkspaceOperationCoordinator coordinator = new();
        CharacterWorkspaceId workspaceId = new("serial");
        coordinator.SetActiveWorkspace(workspaceId);
        TaskCompletionSource<bool> firstStarted = NewSignal();
        TaskCompletionSource<bool> releaseFirst = NewSignal();
        int concurrent = 0;
        int maxConcurrent = 0;

        Task<WorkspaceOperationExecution<int>> first = coordinator.RunCurrentAsync(
            workspaceId,
            async _ =>
            {
                int active = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, active);
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
                Interlocked.Decrement(ref concurrent);
                return 1;
            },
            CancellationToken.None);
        await firstStarted.Task;

        Task<WorkspaceOperationExecution<int>> second = coordinator.RunCurrentAsync(
            workspaceId,
            _ =>
            {
                int active = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, active);
                Interlocked.Decrement(ref concurrent);
                return Task.FromResult(2);
            },
            CancellationToken.None);
        await Task.Yield();
        Assert.IsFalse(second.IsCompleted);

        releaseFirst.TrySetResult(true);
        WorkspaceOperationExecution<int>[] results = await Task.WhenAll(first, second);

        Assert.AreEqual(1, maxConcurrent);
        Assert.IsTrue(results.All(result => result.CanPublish));
    }

    [TestMethod]
    public async Task Nested_current_operation_fails_promptly_without_leaking_gate_or_admission()
    {
        using var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("nested-current");
        coordinator.SetActiveWorkspace(workspaceId);

        WorkspaceOperationExecution<string> outer = await coordinator.RunCurrentAsync(
            workspaceId,
            _ =>
            {
                InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    coordinator.RunCurrentAsync(
                        workspaceId,
                        _ => Task.FromResult("must-not-run"),
                        CancellationToken.None));
                return Task.FromResult(failure.Message);
            },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(outer.CanPublish);
        StringAssert.Contains(outer.Value, "Nested workspace operation admission");
        WorkspaceOperationExecution<string> followup = await coordinator.RunCurrentAsync(
            workspaceId,
            _ => Task.FromResult("followup"),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(followup.CanPublish);
        Assert.AreEqual("followup", followup.Value);
    }

    [TestMethod]
    public async Task Nested_activation_fails_before_mutating_current_activation()
    {
        using var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("nested-activation-owner");
        var nestedWorkspaceId = new CharacterWorkspaceId("nested-activation-target");
        coordinator.SetActiveWorkspace(workspaceId);

        WorkspaceOperationExecution<string> outer = await coordinator.RunCurrentAsync(
            workspaceId,
            _ =>
            {
                InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    coordinator.RunActivationAsync(
                        nestedWorkspaceId,
                        _ => Task.FromResult("must-not-run"),
                        CancellationToken.None));
                Assert.IsTrue(coordinator.IsCurrent(workspaceId));
                return Task.FromResult(failure.Message);
            },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(outer.CanPublish);
        StringAssert.Contains(outer.Value, "Nested workspace operation admission");
        Assert.IsTrue(coordinator.IsCurrent(workspaceId));
    }

    [TestMethod]
    public async Task Switching_workspace_prevents_inflight_save_from_publishing()
    {
        using WorkspaceOperationCoordinator coordinator = new();
        CharacterWorkspaceId original = new("original");
        coordinator.SetActiveWorkspace(original);
        TaskCompletionSource<bool> saveStarted = NewSignal();
        TaskCompletionSource<bool> releaseSave = NewSignal();

        Task<WorkspaceOperationExecution<string>> save = coordinator.RunCurrentAsync(
            original,
            async _ =>
            {
                saveStarted.TrySetResult(true);
                await releaseSave.Task;
                return "saved";
            },
            CancellationToken.None);
        await saveStarted.Task;

        coordinator.SetActiveWorkspace(new CharacterWorkspaceId("new"));
        releaseSave.TrySetResult(true);
        WorkspaceOperationExecution<string> result = await save;

        Assert.IsFalse(result.CanPublish);
        Assert.IsTrue(result.HasValue);
        Assert.AreEqual("saved", result.Value);
    }

    [TestMethod]
    public async Task Switching_workspace_suppresses_late_failure_from_superseded_operation()
    {
        using WorkspaceOperationCoordinator coordinator = new();
        CharacterWorkspaceId original = new("original");
        coordinator.SetActiveWorkspace(original);
        TaskCompletionSource<bool> operationStarted = NewSignal();
        TaskCompletionSource<bool> releaseOperation = NewSignal();

        Task<WorkspaceOperationExecution<string>> operation = coordinator.RunCurrentAsync<string>(
            original,
            async _ =>
            {
                operationStarted.TrySetResult(true);
                await releaseOperation.Task;
                throw new InvalidOperationException("late stale failure");
            },
            CancellationToken.None);
        await operationStarted.Task;

        coordinator.SetActiveWorkspace(new CharacterWorkspaceId("new"));
        releaseOperation.TrySetResult(true);
        WorkspaceOperationExecution<string> result = await operation;

        Assert.IsFalse(result.CanPublish);
        Assert.IsFalse(result.HasValue);
    }

    [TestMethod]
    public async Task Dispose_stops_admission_cancels_blocked_work_and_drains_noncooperative_owner()
    {
        var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("dispose-drain");
        coordinator.SetActiveWorkspace(workspaceId);
        TaskCompletionSource<bool> ownerStarted = NewSignal();
        TaskCompletionSource<bool> releaseOwner = NewSignal();
        Task<WorkspaceOperationExecution<string>> owner = coordinator.RunCurrentAsync(
            workspaceId,
            async _ =>
            {
                ownerStarted.TrySetResult(true);
                await releaseOwner.Task;
                return "committed-recovery";
            },
            CancellationToken.None);
        await ownerStarted.Task;
        Task<WorkspaceOperationExecution<string>> blocked = coordinator.RunCurrentAsync(
            workspaceId,
            _ => Task.FromResult("must-not-run"),
            CancellationToken.None);

        Task disposal = coordinator.DisposeAsync().AsTask();
        await Task.Yield();
        Assert.IsFalse(disposal.IsCompleted);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => coordinator.RunCurrentAsync(
            workspaceId,
            _ => Task.FromResult("late"),
            CancellationToken.None));
        Assert.ThrowsExactly<ObjectDisposedException>(() => coordinator.SetActiveWorkspace(workspaceId));
        Assert.ThrowsExactly<ObjectDisposedException>(() => coordinator.Invalidate(workspaceId));
        Assert.ThrowsExactly<ObjectDisposedException>(() => coordinator.IsCurrent(workspaceId));

        WorkspaceOperationExecution<string> blockedResult = await blocked;
        Assert.IsFalse(blockedResult.CanPublish);
        releaseOwner.TrySetResult(true);
        WorkspaceOperationExecution<string> committed = await owner;
        Assert.IsTrue(committed.HasValue);
        Assert.AreEqual("committed-recovery", committed.Value);
        Assert.IsFalse(committed.CanPublish);
        await disposal;
    }

    [TestMethod]
    public async Task Concurrent_async_disposal_has_one_drain_and_never_disposes_a_held_gate()
    {
        var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("dispose-concurrent");
        coordinator.SetActiveWorkspace(workspaceId);
        TaskCompletionSource<bool> started = NewSignal();
        TaskCompletionSource<bool> release = NewSignal();
        Task<WorkspaceOperationExecution<int>> operation = coordinator.RunCurrentAsync(
            workspaceId,
            async _ =>
            {
                started.TrySetResult(true);
                await release.Task;
                return 42;
            },
            CancellationToken.None);
        await started.Task;

        Task[] disposals = Enumerable.Range(0, 16)
            .Select(_ => coordinator.DisposeAsync().AsTask())
            .ToArray();
        Task synchronousDisposal = Task.Run(coordinator.Dispose);
        await Task.Yield();
        Assert.IsTrue(disposals.All(task => !task.IsCompleted));
        Assert.IsFalse(synchronousDisposal.IsCompleted);
        release.TrySetResult(true);

        WorkspaceOperationExecution<int> result = await operation;
        Assert.IsTrue(result.HasValue);
        Assert.AreEqual(42, result.Value);
        await Task.WhenAll(disposals);
        await synchronousDisposal;
        coordinator.Dispose();
    }

    [TestMethod]
    public async Task Synchronous_dispose_inside_admitted_delegate_fails_promptly_and_async_close_drains()
    {
        var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("dispose-reentrant");
        coordinator.SetActiveWorkspace(workspaceId);
        TaskCompletionSource<bool> disposeReturned = NewSignal();

        Task<WorkspaceOperationExecution<string>> operation = coordinator.RunCurrentAsync(
            workspaceId,
            _ =>
            {
                InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
                    coordinator.Dispose);
                StringAssert.Contains(failure.Message, "inside an admitted workspace operation");
                disposeReturned.TrySetResult(true);
                return Task.FromResult("completed-after-close-started");
            },
            CancellationToken.None);

        await disposeReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        WorkspaceOperationExecution<string> result = await operation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsFalse(result.CanPublish);
        Assert.IsTrue(result.HasValue);
        Assert.AreEqual("completed-after-close-started", result.Value);
        await coordinator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Asynchronous_dispose_inside_admitted_delegate_fails_promptly_and_external_close_drains()
    {
        var coordinator = new WorkspaceOperationCoordinator();
        var workspaceId = new CharacterWorkspaceId("dispose-reentrant-async");
        coordinator.SetActiveWorkspace(workspaceId);

        Task<WorkspaceOperationExecution<string>> operation = coordinator.RunCurrentAsync(
            workspaceId,
            async _ =>
            {
                InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => coordinator.DisposeAsync().AsTask());
                return failure.Message;
            },
            CancellationToken.None);

        WorkspaceOperationExecution<string> result = await operation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsFalse(result.CanPublish);
        Assert.IsTrue(result.HasValue);
        StringAssert.Contains(result.Value, "inside an admitted workspace operation");
        await coordinator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Activation_cancellation_callback_can_reenter_without_running_under_coordinator_lock()
    {
        using var coordinator = new WorkspaceOperationCoordinator();
        var original = new CharacterWorkspaceId("activation-callback-original");
        var requested = new CharacterWorkspaceId("activation-callback-requested");
        var callbackWorkspace = new CharacterWorkspaceId("activation-callback-reentrant");
        coordinator.SetActiveWorkspace(original);
        TaskCompletionSource<bool> registered = NewSignal();
        TaskCompletionSource<bool> release = NewSignal();

        Task<WorkspaceOperationExecution<string>> operation = coordinator.RunCurrentAsync(
            original,
            async token =>
            {
                using CancellationTokenRegistration registration = token.Register(
                    () => coordinator.SetActiveWorkspace(callbackWorkspace));
                registered.TrySetResult(true);
                await release.Task;
                return "stale";
            },
            CancellationToken.None);
        await registered.Task;

        await Task.Run(() => coordinator.SetActiveWorkspace(requested))
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(coordinator.IsCurrent(callbackWorkspace));

        release.TrySetResult(true);
        WorkspaceOperationExecution<string> result = await operation;
        Assert.IsFalse(result.CanPublish);
    }

    [TestMethod]
    public async Task Reentrant_activation_callback_supersedes_request_before_admission_without_leaking_drain()
    {
        using var coordinator = new WorkspaceOperationCoordinator();
        var original = new CharacterWorkspaceId("activation-request-original");
        var requested = new CharacterWorkspaceId("activation-request-superseded");
        var callbackWorkspace = new CharacterWorkspaceId("activation-request-callback");
        coordinator.SetActiveWorkspace(original);
        TaskCompletionSource<bool> registered = NewSignal();
        TaskCompletionSource<bool> release = NewSignal();
        bool supersededOperationInvoked = false;

        Task<WorkspaceOperationExecution<string>> originalOperation = coordinator.RunCurrentAsync(
            original,
            async token =>
            {
                using CancellationTokenRegistration registration = token.Register(
                    () => coordinator.SetActiveWorkspace(callbackWorkspace));
                registered.TrySetResult(true);
                await release.Task;
                return "original";
            },
            CancellationToken.None);
        await registered.Task;

        WorkspaceOperationExecution<string> superseded = await coordinator.RunActivationAsync(
            requested,
            _ =>
            {
                supersededOperationInvoked = true;
                return Task.FromResult("must-not-run");
            },
            CancellationToken.None);
        Assert.IsFalse(superseded.CanPublish);
        Assert.IsFalse(supersededOperationInvoked);
        Assert.IsTrue(coordinator.IsCurrent(callbackWorkspace));

        release.TrySetResult(true);
        Assert.IsFalse((await originalOperation).CanPublish);
        await coordinator.DisposeAsync();
    }

    [TestMethod]
    public async Task Throwing_activation_cancellation_callback_still_disposes_replaced_source()
    {
        using var coordinator = new WorkspaceOperationCoordinator();
        var original = new CharacterWorkspaceId("activation-throw-original");
        coordinator.SetActiveWorkspace(original);
        var replacedSource = (CancellationTokenSource)typeof(WorkspaceOperationCoordinator)
            .GetField(
                "_activationCancellation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(coordinator)!;
        TaskCompletionSource<bool> registered = NewSignal();
        TaskCompletionSource<bool> release = NewSignal();

        Task<WorkspaceOperationExecution<string>> operation = coordinator.RunCurrentAsync(
            original,
            async token =>
            {
                using CancellationTokenRegistration registration = token.Register(
                    () => throw new InvalidOperationException("activation callback failed"));
                registered.TrySetResult(true);
                await release.Task;
                return "stale";
            },
            CancellationToken.None);
        await registered.Task;

        Assert.ThrowsExactly<AggregateException>(() =>
            coordinator.SetActiveWorkspace(new CharacterWorkspaceId("activation-throw-next")));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = replacedSource.Token);

        release.TrySetResult(true);
        WorkspaceOperationExecution<string> result = await operation;
        Assert.IsFalse(result.CanPublish);
    }

    [TestMethod]
    public async Task Standalone_lifecycle_disposes_only_its_internally_created_operation_coordinator()
    {
        WorkspaceOverviewLifecycleCoordinator ownedLifecycle = CreateLifecycleCoordinator();
        var ownedCoordinator = (WorkspaceOperationCoordinator)typeof(WorkspaceOverviewLifecycleCoordinator)
            .GetField(
                "_workspaceOperationCoordinator",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(ownedLifecycle)!;

        await ownedLifecycle.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            ownedCoordinator.SetActiveWorkspace(new CharacterWorkspaceId("owned-closed")));
        ownedLifecycle.Dispose();

        using var externalCoordinator = new WorkspaceOperationCoordinator();
        WorkspaceOverviewLifecycleCoordinator injectedLifecycle = CreateLifecycleCoordinator(externalCoordinator);
        await injectedLifecycle.DisposeAsync();

        var stillOpen = new CharacterWorkspaceId("injected-still-open");
        externalCoordinator.SetActiveWorkspace(stillOpen);
        Assert.IsTrue(externalCoordinator.IsCurrent(stillOpen));
        injectedLifecycle.Dispose();
    }

    [TestMethod]
    public async Task Lifecycle_disposal_drains_admitted_notification_before_disposing_lane_semaphore()
    {
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator(
            deletionNotificationBudget: TimeSpan.FromSeconds(5));
        TaskCompletionSource<bool> callbackStarted = NewSignal();
        TaskCompletionSource<bool> releaseCallback = NewSignal();
        int callbackCount = 0;
        ((IWorkspaceDeletionCommitSource)lifecycle).WorkspaceDeletionCommitted += async (_, _) =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackStarted.TrySetResult(true);
            await releaseCallback.Task;
        };

        Task notification = InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-drain"), 1));
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task disposal = lifecycle.DisposeAsync().AsTask();
        Task synchronousDisposal = Task.Run(lifecycle.Dispose);
        await Task.Yield();
        Assert.IsFalse(disposal.IsCompleted);
        Assert.IsFalse(synchronousDisposal.IsCompleted);

        releaseCallback.TrySetResult(true);
        await notification.WaitAsync(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));
        await synchronousDisposal.WaitAsync(TimeSpan.FromSeconds(2));

        await InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-drain"), 2));
        Assert.AreEqual(1, Volatile.Read(ref callbackCount));
    }

    [TestMethod]
    public async Task Lifecycle_disposal_detaches_callback_lane_that_ignores_notification_budget()
    {
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator(
            deletionNotificationBudget: TimeSpan.FromMilliseconds(25));
        TaskCompletionSource<bool> callbackStarted = NewSignal();
        TaskCompletionSource<bool> releaseCallback = NewSignal();
        TaskCompletionSource<bool> callbackFinished = NewSignal();
        int callbackCount = 0;
        ((IWorkspaceDeletionCommitSource)lifecycle).WorkspaceDeletionCommitted += async (_, _) =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackStarted.TrySetResult(true);
            await releaseCallback.Task;
            callbackFinished.TrySetResult(true);
        };

        Task notification = InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-late-lane"), 1));
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await notification.WaitAsync(TimeSpan.FromSeconds(2));

        Task disposal = lifecycle.DisposeAsync().AsTask();
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, Volatile.Read(ref callbackCount));

        // Completion after bounded detachment may still run its wrapper, but it
        // cannot resurrect a lane or touch the disposed ordering semaphore.
        releaseCallback.TrySetResult(true);
        await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-late-lane"), 2));
        Assert.AreEqual(1, Volatile.Read(ref callbackCount));
        await lifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Synchronous_lifecycle_dispose_inside_deletion_callback_fails_promptly_then_drains()
    {
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator(
            deletionNotificationBudget: TimeSpan.FromSeconds(5));
        TaskCompletionSource<bool> disposeReturned = NewSignal();
        ((IWorkspaceDeletionCommitSource)lifecycle).WorkspaceDeletionCommitted += (_, _) =>
        {
            InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
                lifecycle.Dispose);
            StringAssert.Contains(failure.Message, "inside a deletion callback");
            disposeReturned.TrySetResult(true);
            return Task.CompletedTask;
        };

        Task notification = InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-reentrant"), 1));
        await disposeReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await notification.WaitAsync(TimeSpan.FromSeconds(2));
        await lifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Asynchronous_lifecycle_dispose_inside_deletion_callback_fails_promptly_then_drains()
    {
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator(
            deletionNotificationBudget: TimeSpan.FromSeconds(5));
        TaskCompletionSource<InvalidOperationException> disposeFailure = NewSignal<InvalidOperationException>();
        ((IWorkspaceDeletionCommitSource)lifecycle).WorkspaceDeletionCommitted += async (_, _) =>
        {
            InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => lifecycle.DisposeAsync().AsTask());
            disposeFailure.TrySetResult(failure);
        };

        Task notification = InvokeDeletionNotificationAsync(
            lifecycle,
            new WorkspaceDeletionCommit(new CharacterWorkspaceId("lifecycle-reentrant-async"), 1));
        InvalidOperationException failure = await disposeFailure.Task.WaitAsync(TimeSpan.FromSeconds(2));
        StringAssert.Contains(failure.Message, "inside a deletion callback");
        await notification.WaitAsync(TimeSpan.FromSeconds(2));
        await lifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Asynchronous_lifecycle_dispose_inside_owned_workspace_operation_fails_promptly_then_drains()
    {
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator();
        var coordinator = (WorkspaceOperationCoordinator)typeof(WorkspaceOverviewLifecycleCoordinator)
            .GetField(
                "_workspaceOperationCoordinator",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(lifecycle)!;
        var workspaceId = new CharacterWorkspaceId("lifecycle-owned-operation-reentrant");
        coordinator.SetActiveWorkspace(workspaceId);

        WorkspaceOperationExecution<string> result = await coordinator.RunCurrentAsync(
            workspaceId,
            async _ =>
            {
                InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => lifecycle.DisposeAsync().AsTask());
                return failure.Message;
            },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsFalse(result.CanPublish);
        Assert.IsTrue(result.HasValue);
        StringAssert.Contains(result.Value, "inside an admitted workspace operation");
        await lifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Lifecycle_import_reentrant_dispose_fails_promptly_and_external_close_drains_import()
    {
        (IChummerClient client, ImportClientProxy proxy) = CreateImportClient();
        WorkspaceOverviewLifecycleCoordinator lifecycle = CreateLifecycleCoordinator(client: client);
        TaskCompletionSource<InvalidOperationException> disposeFailure = NewSignal<InvalidOperationException>();
        TaskCompletionSource<bool> releaseImport = NewSignal();
        proxy.ImportHandler = async () =>
        {
            InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => lifecycle.DisposeAsync().AsTask());
            disposeFailure.TrySetResult(failure);
            await releaseImport.Task;
            throw new OperationCanceledException("test import released after disposal began");
        };

        Task<WorkspaceOverviewLifecycleResult> import = lifecycle.ImportAsync(
            CharacterOverviewState.Empty,
            new WorkspaceImportDocument("<chummer />", "sr5"),
            CancellationToken.None);
        InvalidOperationException failure = await disposeFailure.Task.WaitAsync(TimeSpan.FromSeconds(2));
        StringAssert.Contains(failure.Message, "inside an admitted import");

        Task disposal = lifecycle.DisposeAsync().AsTask();
        await Task.Yield();
        Assert.IsFalse(disposal.IsCompleted);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => lifecycle.ImportAsync(
            CharacterOverviewState.Empty,
            new WorkspaceImportDocument("<chummer />", "sr5"),
            CancellationToken.None));

        releaseImport.TrySetResult(true);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => import);
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));
        await lifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static WorkspaceOverviewLifecycleCoordinator CreateLifecycleCoordinator(
        IWorkspaceOperationCoordinator? operationCoordinator = null,
        TimeSpan? deletionNotificationBudget = null,
        IChummerClient? client = null)
        => new(
            client: client!,
            workspaceSessionPresenter: null!,
            workspaceOverviewLoader: null!,
            workspaceViewStateStore: null!,
            workspaceShellStateFactory: null!,
            workspaceRemoteCloseService: null!,
            workspaceSessionActivationService: null!,
            workspaceOverviewStateFactory: null!,
            workspaceOperationCoordinator: operationCoordinator,
            deletionNotificationBudget: deletionNotificationBudget);

    private static Task InvokeDeletionNotificationAsync(
        WorkspaceOverviewLifecycleCoordinator lifecycle,
        WorkspaceDeletionCommit commit)
        => (Task)typeof(WorkspaceOverviewLifecycleCoordinator)
            .GetMethod(
                "NotifyDeletionCommittedBestEffortAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(lifecycle, [commit])!;

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<T> NewSignal<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static (IChummerClient Client, ImportClientProxy Proxy) CreateImportClient()
    {
        IChummerClient client = DispatchProxy.Create<IChummerClient, ImportClientProxy>();
        return (client, (ImportClientProxy)(object)client);
    }

    public class ImportClientProxy : DispatchProxy
    {
        public Func<Task<WorkspaceImportResult>>? ImportHandler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (string.Equals(targetMethod?.Name, nameof(IChummerClient.ImportAsync), StringComparison.Ordinal))
            {
                return (ImportHandler
                    ?? throw new InvalidOperationException("Import handler is not configured."))();
            }

            throw new NotSupportedException(
                $"Unexpected test client call: {targetMethod?.Name ?? "<unknown>"}.");
        }
    }
}
