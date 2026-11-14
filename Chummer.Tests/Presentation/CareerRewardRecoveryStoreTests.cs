using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerRewardRecoveryStoreTests
{
    [TestMethod]
    public void A_new_store_instance_reads_the_exact_intent_and_stale_clear_cannot_remove_its_successor()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var firstStore = directory.OpenStore();
        var original = CareerRecoveryTestData.Pending();
        Assert.IsTrue(firstStore.TryAdd(original));
        var restarted = directory.OpenStore();
        Assert.IsTrue(restarted.TryRead(new OwnerScope(original.OriginalOwnerId), original.WorkspaceId, out var loaded));
        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.EntryId, loaded.EntryId);
        Assert.AreEqual(original.OperationId, loaded.OperationId);
        Assert.AreEqual(original.ExpectedRevision, loaded.ExpectedRevision);
        Assert.IsFalse(restarted.TryAdd(original with { EntryId = Guid.NewGuid() }), "Unresolved intent cannot be replaced.");
        Assert.IsTrue(restarted.TryClear(loaded));
        // Even an accidental reuse of the operation id has a different intent
        // incarnation. A delayed clear must not erase that later entry.
        var newer = original with { EntryId = Guid.NewGuid(), OperationId = Guid.NewGuid() };
        Assert.IsTrue(restarted.TryAdd(newer));
        Assert.IsFalse(firstStore.TryClear(original));
        Assert.IsTrue(restarted.TryRead(new OwnerScope(original.OriginalOwnerId), original.WorkspaceId, out loaded));
        Assert.AreEqual(newer.EntryId, loaded!.EntryId);
    }

    [TestMethod]
    public void Replacement_backfill_fills_only_the_matching_intent_incarnation()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = CareerRecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        Assert.IsFalse(store.TryRecordReplacement(Guid.NewGuid(), WorkspaceDocumentFormat.NativeXml, "sr5", "{}"),
            "An unknown entry id must not mutate any ledger.");
        Assert.IsTrue(store.TryRecordReplacement(pending.EntryId, WorkspaceDocumentFormat.NativeXml, "sr5", "{}"));
        Assert.IsTrue(store.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out var loaded));
        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.HasReplacement);
        Assert.AreEqual(WorkspaceDocumentFormat.NativeXml, loaded.ReplacementFormat);
        Assert.AreEqual("sr5", loaded.ReplacementRulesetId);
        Assert.AreEqual("{}", loaded.ReplacementContent);
        // A second backfill of the same entry id replaces only its own intent;
        // a new incarnation must be a fresh dispatch.
        Assert.IsFalse(store.TryRecordReplacement(Guid.NewGuid(), WorkspaceDocumentFormat.Json, "sr6", "[]"));
    }

    [TestMethod]
    public void Resolution_is_idempotent_only_for_the_exact_valid_replacement_bearing_entry()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = CareerRecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        Assert.IsFalse(store.TryMarkResolved(pending.EntryId, pending.OperationId), "Intent alone is not confirmation.");
        Assert.IsTrue(store.TryRecordReplacement(pending.EntryId, WorkspaceDocumentFormat.NativeXml, "sr5", "<character/>"));
        Assert.IsFalse(store.TryMarkResolved(pending.EntryId, Guid.NewGuid()));
        Assert.IsTrue(store.TryMarkResolved(pending.EntryId, pending.OperationId));
        string path = Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories).Single();
        string resolvedBytes = File.ReadAllText(path);

        var other = directory.OpenStore();
        Assert.IsTrue(other.TryMarkResolved(pending.EntryId, pending.OperationId));
        Assert.AreEqual(resolvedBytes, File.ReadAllText(path), "Idempotent resolution does not rewrite the ledger.");
        Assert.IsFalse(other.TryMarkResolved(pending.EntryId, Guid.NewGuid()));
        Assert.IsFalse(other.TryMarkResolved(Guid.NewGuid(), pending.OperationId));
        Assert.IsTrue(other.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out var proof));
        Assert.IsNotNull(proof);
        Assert.IsTrue(proof.IsResolved);

        File.WriteAllText(path, resolvedBytes.Replace("42", "43", StringComparison.Ordinal));
        Assert.IsFalse(other.TryMarkResolved(pending.EntryId, pending.OperationId), "Idempotency must still validate the digest.");
        File.WriteAllText(path, resolvedBytes);
        Assert.IsTrue(other.TryClear(proof));
        var successor = pending with { EntryId = Guid.NewGuid() };
        Assert.IsTrue(other.TryAdd(successor));
        Assert.IsFalse(store.TryMarkResolved(pending.EntryId, pending.OperationId), "A reused operation id cannot resolve a different incarnation.");
        Assert.IsTrue(other.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out var held));
        Assert.AreEqual(successor.EntryId, held!.EntryId);
        Assert.IsFalse(held.IsResolved);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{truncated")]
    public void Corrupt_ledger_is_not_an_empty_map(string corrupt)
    {
        using var directory = new CareerRecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = CareerRecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        string path = Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(path, corrupt);
        Assert.IsFalse(store.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out _));
        Assert.IsFalse(store.TryAdd(pending));
        Assert.IsFalse(store.TryClear(pending));
        Assert.AreEqual(corrupt, File.ReadAllText(path));
    }

    [TestMethod]
    public void Tampered_intent_bytes_are_detected_without_overwriting_the_ledger()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = CareerRecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        string path = Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories).Single();
        string modified = File.ReadAllText(path).Replace("42", "43", StringComparison.Ordinal);
        File.WriteAllText(path, modified);
        Assert.IsFalse(store.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out _));
        Assert.IsFalse(store.TryAdd(pending));
        Assert.AreEqual(modified, File.ReadAllText(path));
    }

    [TestMethod]
    public void Failed_directory_and_contended_lock_refuse_dependent_writes_without_deleting_the_lock()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var pending = CareerRecoveryTestData.Pending();
        string blocked = Path.Combine(directory.Path, "blocked");
        File.WriteAllText(blocked, "not a directory");
        Assert.IsFalse(new FileCareerRewardRecoveryStore(blocked).TryAdd(pending));
        var store = directory.OpenStore();
        Assert.IsTrue(store.TryAdd(pending));
        string lockPath = Directory.GetFiles(directory.Path, "*.lock", SearchOption.AllDirectories).Single();
        using (var held = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.IsFalse(store.TryClear(pending));
            Assert.IsTrue(File.Exists(lockPath));
        }
        Assert.IsTrue(directory.OpenStore().TryClear(pending), "A leftover lock file is not a stale lock after restart.");
    }

    [TestMethod]
    public async Task Two_store_instances_cannot_overwrite_each_others_unresolved_intents()
    {
        using var directory = new CareerRecoveryTestDirectory();
        var first = CareerRecoveryTestData.Pending();
        var second = CareerRecoveryTestData.Pending();
        bool[] added = await Task.WhenAll(
            Task.Run(() => directory.OpenStore().TryAdd(first)),
            Task.Run(() => directory.OpenStore().TryAdd(second)));
        Assert.AreEqual(1, added.Count(success => success));
        Assert.IsTrue(directory.OpenStore().TryRead(new OwnerScope(first.OriginalOwnerId), first.WorkspaceId, out var found));
        Assert.AreEqual(added[0] ? first.EntryId : second.EntryId, found!.EntryId);
    }
}

internal static class CareerRecoveryTestData
{
    public static CareerRewardPendingRecovery Pending(
        string workspace = "ws-career-recovery", long revision = 42, string owner = "owner-a")
    {
        return new CareerRewardPendingRecovery(
            Guid.NewGuid(), owner, false, new CharacterWorkspaceId(workspace), Guid.NewGuid(), revision,
            null, null, null);
    }
}

internal sealed class CareerRecoveryTestDirectory : IDisposable
{
    public CareerRecoveryTestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chummer-career-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public FileCareerRewardRecoveryStore OpenStore() => new(System.IO.Path.Combine(Path, "ledger"));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
