using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

public partial class WorkspaceOverviewLoaderTests
{
    private static readonly OwnerContextStamp RecoveryOwner = new(new OwnerScope("recovery-a"), "recovery-test-host", 1);
    private static readonly CharacterWorkspaceId RecoveryId = new("same-recovery-id");

    [TestMethod]
    public async Task Local_recovery_never_recaptures_an_owner_for_an_unbound_request()
    {
        var client = new OwnerRecoveryClient(RecoveryOwner);
        var loader = (IAuthoritativeWorkspaceOverviewLoader)WorkspaceOverviewLoader.CreateCompositionBound(client);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => loader.LoadRecoverySnapshotAsync(RecoveryId, default));
        Assert.AreEqual(0, client.UnboundReads);
        Assert.AreEqual(0, client.BoundReads);
    }

    [TestMethod]
    [DataRow("b", 0)]
    [DataRow("aba", 0)]
    [DataRow("issuer", 0)]
    [DataRow("b", 1)]
    [DataRow("aba", 1)]
    [DataRow("issuer", 1)]
    [DataRow("b", 2)]
    [DataRow("aba", 2)]
    [DataRow("issuer", 2)]
    public async Task Recovery_rejects_transition_before_or_between_independent_reads(string transition, int afterRead)
    {
        var client = new OwnerRecoveryClient(RecoveryOwner);
        OwnerContextStamp next = NextRecoveryOwner(transition);
        if (afterRead == 0) client.Current = next;
        else client.AfterBoundRead = count => { if (count == afterRead) client.Current = next; };
        var loader = (IAuthoritativeWorkspaceOverviewLoader)WorkspaceOverviewLoader.CreateCompositionBound(client);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => loader.LoadRecoverySnapshotAsync(RecoveryOwner, RecoveryId, default));
        Assert.AreEqual(afterRead, client.BoundReads);
        Assert.AreEqual(0, client.UnboundReads);
    }

    [TestMethod]
    [DataRow("b")]
    [DataRow("aba")]
    [DataRow("issuer")]
    public async Task Recovery_partition_and_validation_capability_keep_the_full_original_owner(string other)
    {
        using var vault = new WorkspaceRecoveryPayloadStore();
        OwnerContextStamp next = NextRecoveryOwner(other);
        IWorkspaceRecoveryPayloadStore a = vault.ForOwner(RecoveryOwner), b = vault.ForOwner(next);
        WorkspaceRecoveryAuthoritySnapshot first = await RecoverySnapshot(RecoveryOwner, "First private runner");
        WorkspaceRecoveryAuthoritySnapshot second = await RecoverySnapshot(next, "Second private runner");
        WorkspaceRecoveryCaptureResult capturedA = CaptureOwnedRecovery(vault, a, first);
        WorkspaceRecoveryCaptureResult capturedB = CaptureOwnedRecovery(vault, b, second);
        Assert.IsTrue(capturedA.Success);
        Assert.IsTrue(capturedB.Success);
        Assert.AreNotEqual(capturedA.LocalGeneration, capturedB.LocalGeneration);
        Assert.IsFalse(vault.GetAvailability(RecoveryId, 1).Available);
        Assert.IsFalse(b.TryAcquireLease(RecoveryId, 1, capturedA.LocalGeneration, out _));
        Assert.IsFalse(b.MarkExported(RecoveryId, 1, capturedA.LocalGeneration));
        Assert.IsTrue(a.TryAcquireLease(RecoveryId, 1, capturedA.LocalGeneration, out var lease));
        using (lease)
        using (var reader = new StreamReader(lease!.Stream))
            StringAssert.Contains(await reader.ReadToEndAsync(), "First private runner");
        Assert.IsTrue(b.TryAcquireLease(RecoveryId, 1, capturedB.LocalGeneration, out var secondLease));
        using (secondLease)
        using (var reader = new StreamReader(secondLease!.Stream))
            StringAssert.Contains(await reader.ReadToEndAsync(), "Second private runner");
        Assert.IsFalse(CaptureOwnedRecovery(vault, b, first).Success, "Another partition cannot reuse an original-owner capability.");
        Assert.IsFalse(CaptureOwnedRecovery(vault, vault, first).Success, "Legacy callers cannot launder an owner-bound capability.");
        Assert.IsTrue(a.GetAvailability(RecoveryId, 1).Available);
        Assert.IsTrue(b.GetAvailability(RecoveryId, 1).Available);
        Assert.AreEqual(0, vault.ActiveCaptureIntentCount);
    }

    [TestMethod]
    public async Task Recovery_close_arbitrates_only_original_partition_but_uses_one_global_capacity()
    {
        using var vault = new WorkspaceRecoveryPayloadStore();
        var snapshots = new List<WorkspaceRecoveryAuthoritySnapshot>();
        var captures = new List<WorkspaceRecoveryCaptureResult>();
        for (int index = 0; index < WorkspaceRecoveryPayloadStore.MaxRetainedEntries + 1; index++)
        {
            var owner = RecoveryOwner with { TransitionRevision = index };
            var snapshot = await RecoverySnapshot(owner, "Retained " + index);
            snapshots.Add(snapshot);
            captures.Add(CaptureOwnedRecovery(vault, vault.ForOwner(owner), snapshot));
        }
        Assert.IsTrue(captures.Take(4).All(value => value.Success));
        Assert.IsFalse(captures[4].Success, "Partitions must not multiply the bounded vault capacity.");
        IWorkspaceRecoveryPayloadStore first = vault.ForOwner(snapshots[0].OriginalOwner!.Value);
        IWorkspaceRecoveryPayloadStore second = vault.ForOwner(snapshots[1].OriginalOwner!.Value);
        Assert.IsTrue(first.MarkExported(RecoveryId, 1, captures[0].LocalGeneration));
        Assert.IsTrue(second.TryBeginCaptureIntent(RecoveryId, 1, out var foreignCapture));
        bool closed = false;
        using (foreignCapture)
            Assert.IsTrue(first.TryCommitExplicitClose(RecoveryId, 1, captures[0].LocalGeneration, () => closed = true));
        Assert.IsTrue(closed);
        Assert.IsFalse(first.GetAvailability(RecoveryId, 1).Available);
        Assert.IsTrue(second.GetAvailability(RecoveryId, 1).Available);
        Assert.IsTrue(CaptureOwnedRecovery(vault, vault.ForOwner(snapshots[4].OriginalOwner!.Value), snapshots[4]).Success);
    }

    [TestMethod]
    public async Task Recovery_partition_cannot_rebind_dispose_parent_or_bypass_same_owner_capture_fence()
    {
        using var vault = new WorkspaceRecoveryPayloadStore();
        var snapshot = await RecoverySnapshot(RecoveryOwner, "Protected runner");
        var owner = vault.ForOwner(RecoveryOwner);
        var capture = CaptureOwnedRecovery(vault, owner, snapshot);
        Assert.ThrowsExactly<ArgumentException>(() => vault.ForOwner(default));
        Assert.ThrowsExactly<InvalidOperationException>(() => owner.ForOwner(NextRecoveryOwner("aba")));
        owner.Dispose();
        Assert.IsTrue(owner.GetAvailability(RecoveryId, 1).Available);
        Assert.IsTrue(owner.MarkExported(RecoveryId, 1, capture.LocalGeneration));
        Assert.IsTrue(owner.TryBeginCaptureIntent(RecoveryId, 1, out var pending));
        using (pending)
            Assert.IsFalse(owner.TryCommitExplicitClose(RecoveryId, 1, capture.LocalGeneration, () => Assert.Fail("Pending original capture must block close.")));
        Assert.IsTrue(owner.CanCloseAfterExport(RecoveryId, 1, capture.LocalGeneration));
    }

    [TestMethod]
    [DataRow("b")]
    [DataRow("aba")]
    [DataRow("issuer")]
    public async Task Account_cleanup_retires_only_original_recovery_epoch_and_all_its_pending_captures(string other)
    {
        using var vault = new WorkspaceRecoveryPayloadStore();
        var a = vault.ForOwner(RecoveryOwner);
        var next = NextRecoveryOwner(other);
        var b = vault.ForOwner(next);
        var original = await RecoverySnapshot(RecoveryOwner, "Deleted private runner");
        var retained = await RecoverySnapshot(next, "Retained private runner");
        var captured = CaptureOwnedRecovery(vault, a, original);
        Assert.IsTrue(CaptureOwnedRecovery(vault, b, retained).Success);
        Assert.IsTrue(a.TryBeginCaptureIntent(RecoveryId, 1, out var pending));
        using (pending)
        {
            Assert.IsFalse(a.RetireOwner(next), "A borrowed facet cannot retire another owner.");
            Assert.IsFalse(vault.RetireOwner(default));
            Assert.IsTrue(vault.RetireOwner(RecoveryOwner));
            Assert.IsTrue(vault.RetireOwner(RecoveryOwner), "Retirement is idempotent.");
            Assert.IsFalse(a.GetAvailability(RecoveryId, 1).Available);
            Assert.IsFalse(a.TryAcquireLease(RecoveryId, 1, captured.LocalGeneration, out _));
            Assert.IsFalse(a.TryBeginCaptureIntent(RecoveryId, 1, out _));
            Assert.IsFalse(vault.Capture(pending!, original.Document, original.Validation).Success);
            Assert.IsTrue(b.GetAvailability(RecoveryId, 1).Available);
            Assert.IsTrue(b.TryBeginCaptureIntent(RecoveryId, 1, out var allowed));
            allowed!.Dispose();
        }
        Assert.AreEqual(0, vault.ActiveCaptureIntentCount);
        Assert.AreEqual(0, vault.RetainedCaptureFailureCount);
    }

    [TestMethod]
    public async Task Account_cleanup_fences_capture_already_committing_outside_vault_lock()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vault = new WorkspaceRecoveryPayloadStore(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Capture was not released.");
        });
        var snapshot = await RecoverySnapshot(RecoveryOwner, "Deleted while capture was running");
        var partition = vault.ForOwner(RecoveryOwner);
        Task<WorkspaceRecoveryCaptureResult> capture = Task.Run(() => CaptureOwnedRecovery(vault, partition, snapshot));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(vault.RetireOwner(RecoveryOwner));
            Assert.AreEqual(1, vault.ActiveCaptureIntentCount,
                "An in-flight capture must occupy capacity until it actually finishes.");
        }
        finally { release.Set(); }
        Assert.IsFalse((await capture.WaitAsync(TimeSpan.FromSeconds(5))).Success);
        Assert.IsFalse(partition.GetAvailability(RecoveryId, 1).Available);
        Assert.AreEqual(0, vault.ActiveCaptureIntentCount);
        Assert.AreEqual(0, vault.RetainedCaptureFailureCount);
    }

    [TestMethod]
    public void Recovery_retirement_capacity_never_evicts_old_revocation_into_renewed_authority()
    {
        using var vault = new WorkspaceRecoveryPayloadStore();
        for (int index = 0; index < WorkspaceRecoveryPayloadStore.MaxActiveCaptureIntents; index++)
            Assert.IsTrue(vault.RetireOwner(RecoveryOwner with { TransitionRevision = index }));
        Assert.IsFalse(vault.RetireOwner(RecoveryOwner with
            { TransitionRevision = WorkspaceRecoveryPayloadStore.MaxActiveCaptureIntents }));
        Assert.IsTrue(vault.RetireOwner(RecoveryOwner));
        Assert.IsFalse(vault.ForOwner(RecoveryOwner).TryBeginCaptureIntent(RecoveryId, 1, out _));
    }

    private static OwnerContextStamp NextRecoveryOwner(string transition) => transition switch
    {
        "b" => RecoveryOwner with { Owner = new OwnerScope("recovery-b"), TransitionRevision = 2 },
        "aba" => RecoveryOwner with { TransitionRevision = 3 },
        _ => RecoveryOwner with { AuthorityInstanceId = "another-host" }
    };

    private static async Task<WorkspaceRecoveryAuthoritySnapshot> RecoverySnapshot(OwnerContextStamp owner, string name)
    {
        var client = new OwnerRecoveryClient(owner, "<character><name>" + name + "</name><alias>RECOVERY</alias>"
            + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
            + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
            + "<karma>9</karma><nuyen>1000</nuyen><created>True</created></character>");
        var loader = (IAuthoritativeWorkspaceOverviewLoader)WorkspaceOverviewLoader.CreateCompositionBound(client);
        var result = await loader.LoadRecoverySnapshotAsync(owner, RecoveryId, default);
        Assert.AreEqual(owner, result.OriginalOwner);
        Assert.AreEqual(2, client.BoundReads);
        Assert.AreEqual(0, client.UnboundReads);
        return result;
    }

    private static WorkspaceRecoveryCaptureResult CaptureOwnedRecovery(WorkspaceRecoveryPayloadStore vault,
        IWorkspaceRecoveryPayloadStore partition, WorkspaceRecoveryAuthoritySnapshot snapshot)
    {
        Assert.IsTrue(partition.TryBeginCaptureIntent(RecoveryId, snapshot.ContentRevision, out var intent));
        using (intent)
            return vault.Capture(intent!, snapshot.Document, snapshot.Validation, protectFromEviction: true);
    }

    private sealed class OwnerRecoveryClient(OwnerContextStamp owner, string? xml = null)
        : LoaderClientStub(xml: xml), IOwnerBoundWorkspaceMutationClient
    {
        public OwnerContextStamp Current { get; set; } = owner;
        public int BoundReads { get; private set; }
        public int UnboundReads { get; private set; }
        public Action<int>? AfterBoundRead { get; set; }
        public OwnerContextStamp CaptureOwnerContext() => Current;
        public override Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            UnboundReads++;
            throw new InvalidOperationException("An owner-bound recovery must not call the legacy reader.");
        }
        public async Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(OwnerContextStamp original,
            CharacterWorkspaceId id, CancellationToken ct)
        {
            if (original != Current) throw new InvalidOperationException("Stale recovery owner.");
            BoundReads++;
            var result = await base.GetWorkspaceAsync(id, ct);
            AfterBoundRead?.Invoke(BoundReads);
            return result;
        }
        public Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(OwnerContextStamp original,
            CharacterWorkspaceId id, long revision, WorkspaceDocument document, Action onDispatch, CancellationToken ct)
            => throw new InvalidOperationException("Recovery is read-only.");
    }
}
