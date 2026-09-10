#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Application.Tools;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

// Actual client/executor admission tests with test-owned synchronous service
// doubles. These are not canonical workspace persistence or network receipts.
[TestClass]
public sealed class InProcessShellOwnerContextTests
{
    [TestMethod]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Legacy_shell_read_uses_real_owner_lease(string operation)
    {
        var fixture = new Fixture();
        await InvokeLegacyRead(fixture.Client, operation);
        Assert.AreEqual(1, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Legacy_shell_read_captures_owner_before_queue_and_rejects_ABA(string operation)
    {
        var fixture = new Fixture();
        fixture.Roaming.Block = true;
        Task predecessor = fixture.Client.ListWorkspacesAsync(fixture.Owner.Capture(), CancellationToken.None);
        Task? queued = null;
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = InvokeLegacyRead(fixture.Client, operation);
            Assert.IsFalse(queued.IsCompleted);
            fixture.Owner.TransitionAwayAndBack();
            fixture.Roaming.Release.TrySetResult();
            await AssertRejected(predecessor);
            await AssertRejected(queued);
            Assert.AreEqual(0, fixture.ServiceCalls);
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            await ObserveOriginalAsync(predecessor);
            if (queued is not null) await ObserveOriginalAsync(queued);
        }
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    private static async Task InvokeLegacyRead(InProcessChummerClient client, string operation)
    {
        if (operation == "session") await client.GetShellSessionAsync(CancellationToken.None);
        else await client.GetShellPreferencesAsync(CancellationToken.None);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Bound_shell_rejects_original_stamp_after_owner_ABA_before_entry(string operation)
    {
        var fixture = new Fixture();
        OwnerContextStamp original = fixture.Owner.Capture();
        fixture.Owner.TransitionAwayAndBack();
        await AssertRejected(Invoke(fixture.Client, operation, original));
        Assert.AreEqual(0, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Roaming.Calls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Bound_shell_rechecks_original_stamp_after_executor_queue_admission(string operation)
    {
        var fixture = new Fixture();
        OwnerContextStamp original = fixture.Owner.Capture();
        fixture.Roaming.Block = true;
        Task predecessor = fixture.Client.ListWorkspacesAsync(original, CancellationToken.None);
        Task? queued = null;
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = Invoke(fixture.Client, operation, original);
            Assert.IsFalse(queued.IsCompleted);
            fixture.Owner.TransitionAwayAndBack();
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            try
            {
                await AssertRejected(predecessor);
                if (queued is not null) await AssertRejected(queued);
            }
            finally
            {
                try { await ObserveOriginalAsync(predecessor); }
                finally { if (queued is not null) await ObserveOriginalAsync(queued); }
            }
        }
        Assert.AreEqual(1, fixture.Roaming.Calls, "The stale queued action must not start another roaming operation.");
        Assert.AreEqual(0, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Bound_shell_steady_owner_services_run_under_real_lease_and_release_it(string operation)
    {
        var fixture = new Fixture();
        Task pending = Invoke(fixture.Client, operation, fixture.Owner.Capture());
        try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await ObserveOriginalAsync(pending); }
        Assert.AreEqual(operation == "bootstrap" ? 3 : 1, fixture.ServiceCalls);
        Assert.AreEqual(operation is "bootstrap" or "list" ? 1 : 0, fixture.Roaming.Calls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    public async Task Bound_shell_owner_ABA_during_roaming_cannot_publish_local_materialization(string operation)
    {
        var fixture = new Fixture();
        fixture.Roaming.Block = true;
        Task pending = Invoke(fixture.Client, operation, fixture.Owner.Capture());
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, fixture.Owner.ActiveLeases, "No lease may cross roaming's await.");
            Assert.IsFalse(pending.IsCompleted);
            fixture.Owner.TransitionAwayAndBack();
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            await AssertRejected(pending);
        }
        Assert.AreEqual(0, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Bound_shell_current_only_accessor_cannot_be_upgraded_to_lease_authority(string operation)
    {
        var fixture = new Fixture(unsupportedOwner: true);
        await AssertRejected(Invoke(fixture.Client, operation, fixture.Owner.Capture()));
        Assert.AreEqual(0, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Roaming.Calls);
    }

    [TestMethod]
    [DataRow("bootstrap")]
    [DataRow("list")]
    [DataRow("session")]
    [DataRow("preferences")]
    public async Task Bound_shell_service_failure_releases_the_actual_owner_lease(string operation)
    {
        var fixture = new Fixture { FailService = true };
        await AssertRejected(Invoke(fixture.Client, operation, fixture.Owner.Capture()));
        Assert.AreEqual(1, fixture.ServiceCalls);
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
        OwnerContextStamp stillCurrent = fixture.Owner.Capture();
        Assert.IsTrue(fixture.Owner.TryAcquire(stillCurrent, out IOwnerContextLease? lease));
        lease.Dispose();
    }

    private static async Task ObserveOriginalAsync(Task pending)
    {
        try { await pending.ConfigureAwait(false); }
        catch { /* Join without replacing the original test observation. */ }
    }

    private static Task Invoke(IOwnerBoundShellStateClient client, string operation, OwnerContextStamp original)
        => operation switch
        {
            "bootstrap" => client.GetShellBootstrapAsync(original, "sr5", CancellationToken.None),
            "list" => client.ListWorkspacesAsync(original, CancellationToken.None),
            "session" => client.SaveShellSessionAsync(original, ShellSessionState.Default, CancellationToken.None),
            "preferences" => client.SaveShellPreferencesAsync(original, new ShellPreferences("sr5"), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static async Task AssertRejected(Task task)
    {
        try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (InvalidOperationException) { return; }
        Assert.Fail("The original authority must reject this operation; no fresh capture can authorize it.");
    }

    private sealed class Fixture
    {
        public LeaseOwner Owner { get; } = new();
        public ProbeRoaming Roaming { get; }
        public InProcessChummerClient Client { get; }
        public int ServiceCalls;
        public bool FailService;

        public Fixture(bool unsupportedOwner = false)
        {
            Roaming = new ProbeRoaming(Owner);
            IWorkspaceService workspace = CreateService<IWorkspaceService>((method, args) =>
            {
                ObserveService(args);
                if (method.Name != nameof(IWorkspaceService.List)) throw new NotSupportedException(method.Name);
                return Array.Empty<WorkspaceListItem>();
            });
            IShellPreferencesService preferences = CreateService<IShellPreferencesService>((method, args) =>
            {
                ObserveService(args);
                return method.Name == nameof(IShellPreferencesService.Load) ? ShellPreferences.Default : null;
            });
            IShellSessionService sessions = CreateService<IShellSessionService>((method, args) =>
            {
                ObserveService(args);
                return method.Name == nameof(IShellSessionService.Load) ? ShellSessionState.Default : null;
            });
            var registry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            Client = new InProcessChummerClient(workspace, new RulesetShellCatalogResolverService(registry),
                rulesetSelectionPolicy: new DefaultRulesetSelectionPolicy(registry),
                shellPreferencesService: preferences, shellSessionService: sessions,
                ownerContextAccessor: unsupportedOwner ? new CurrentOnlyOwner() : Owner,
                workspaceRoamingSync: Roaming);
        }

        private void ObserveService(object?[]? arguments)
        {
            Assert.AreEqual(1, Owner.ActiveLeases);
            Assert.IsNotNull(arguments);
            Assert.IsInstanceOfType<OwnerScope>(arguments[0]);
            Assert.AreEqual(Owner.ExpectedOwner, (OwnerScope)arguments[0]!);
            Interlocked.Increment(ref ServiceCalls);
            if (FailService) throw new InvalidOperationException("Synthetic service failure after admitted lease.");
        }
    }

    public class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Handler(targetMethod ?? throw new InvalidOperationException("Method unavailable."), args);
    }

    private static T CreateService<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        T service = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)service).Handler = handler;
        return service;
    }

    private sealed class CurrentOnlyOwner : IOwnerContextAccessor
    {
        public OwnerScope Current => new("shell-owner-a");
    }

    private sealed class LeaseOwner : IOwnerContextLeaseAccessor
    {
        private readonly object _gate = new();
        private OwnerContextStamp _stamp = new(new OwnerScope("shell-owner-a"), Guid.NewGuid().ToString("N"), 0);
        public OwnerScope ExpectedOwner => new("shell-owner-a");
        public int ActiveLeases;
        public OwnerScope Current => Capture().Owner;
        public OwnerContextStamp Capture() { lock (_gate) { Assert.AreEqual(0, ActiveLeases); return _stamp; } }
        public void TransitionAwayAndBack()
        {
            lock (_gate)
            {
                Assert.AreEqual(0, ActiveLeases);
                _stamp = _stamp with { Owner = new("shell-owner-b"), TransitionRevision = checked(_stamp.TransitionRevision + 1) };
                _stamp = _stamp with { Owner = ExpectedOwner, TransitionRevision = checked(_stamp.TransitionRevision + 1) };
            }
        }
        public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
        {
            Monitor.Enter(_gate);
            if (!expected.IsValid || expected != _stamp) { Monitor.Exit(_gate); lease = null; return false; }
            ActiveLeases++;
            lease = new Lease(this, _stamp);
            return true;
        }
        private sealed class Lease(LeaseOwner owner, OwnerContextStamp stamp) : IOwnerContextLease
        {
            private bool _disposed;
            public OwnerContextStamp Stamp => !_disposed ? stamp : throw new ObjectDisposedException(nameof(Lease));
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                owner.ActiveLeases--;
                Monitor.Exit(owner._gate);
            }
        }
    }

    private sealed class ProbeRoaming(LeaseOwner owner) : IDesktopWorkspaceRoamingSync
    {
        public bool Block;
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope scope, CancellationToken ct)
        {
            Assert.AreEqual(0, owner.ActiveLeases);
            Assert.AreEqual(owner.ExpectedOwner, scope);
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(ct);
            Assert.AreEqual(0, owner.ActiveLeases);
            return DesktopWorkspaceRoamingResult.AlreadyCurrent();
        }
        public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(OwnerScope scope, CharacterWorkspaceId id, CancellationToken ct)
            => throw new NotSupportedException("Shell fixture must not perform outbound roaming.");
    }
}
