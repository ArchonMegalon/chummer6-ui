using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Sr6CareerAttributeRecoveryStoreTests
{
    [TestMethod]
    public void A_new_store_instance_reads_the_exact_intent_and_stale_clear_cannot_remove_its_successor()
    {
        using var directory = new Sr6RecoveryTestDirectory();
        var firstStore = directory.OpenStore();
        var original = Sr6RecoveryTestData.Pending();
        Assert.IsTrue(firstStore.TryAdd(original));
        var restarted = directory.OpenStore();
        Assert.IsTrue(restarted.TryRead(new OwnerScope(original.OriginalOwnerId), original.WorkspaceId, out var loaded));
        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.EntryId, loaded.EntryId);
        Assert.AreEqual(original.CommandDigest, Sr6CareerAttributeIntegrity.Digest(loaded.Command));
        Assert.IsFalse(restarted.TryAdd(original with { EntryId = Guid.NewGuid() }), "Unresolved intent cannot be replaced.");
        Assert.IsTrue(restarted.TryClear(loaded));
        // Even an accidental reuse of the operation id has a different intent
        // incarnation. A delayed clear must not erase that later entry.
        var newer = original with { EntryId = Guid.NewGuid() };
        Assert.IsTrue(restarted.TryAdd(newer));
        Assert.IsFalse(firstStore.TryClear(original));
        Assert.IsTrue(restarted.TryRead(new OwnerScope(original.OriginalOwnerId), original.WorkspaceId, out loaded));
        Assert.AreEqual(newer.EntryId, loaded!.EntryId);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{truncated")]
    public void Corrupt_ledger_is_not_an_empty_map(string corrupt)
    {
        using var directory = new Sr6RecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = Sr6RecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        string path = Directory.GetFiles(directory.Path, "*.json").Single();
        File.WriteAllText(path, corrupt);
        Assert.IsFalse(store.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out _));
        Assert.IsFalse(store.TryAdd(pending));
        Assert.IsFalse(store.TryClear(pending));
        Assert.AreEqual(corrupt, File.ReadAllText(path));
    }

    [TestMethod]
    public void Modified_command_bytes_are_detected_without_overwriting_the_ledger()
    {
        using var directory = new Sr6RecoveryTestDirectory();
        var store = directory.OpenStore();
        var pending = Sr6RecoveryTestData.Pending();
        Assert.IsTrue(store.TryAdd(pending));
        string path = Directory.GetFiles(directory.Path, "*.json").Single();
        string modified = File.ReadAllText(path).Replace("sr6:body", "sr6:other", StringComparison.Ordinal);
        File.WriteAllText(path, modified);
        Assert.IsFalse(store.TryRead(new OwnerScope(pending.OriginalOwnerId), pending.WorkspaceId, out _));
        Assert.IsFalse(store.TryAdd(pending));
        Assert.AreEqual(modified, File.ReadAllText(path));
    }

    [TestMethod]
    public void Failed_directory_and_contended_lock_refuse_dependent_writes_without_deleting_the_lock()
    {
        using var directory = new Sr6RecoveryTestDirectory();
        var pending = Sr6RecoveryTestData.Pending();
        string blocked = System.IO.Path.Combine(directory.Path, "blocked");
        File.WriteAllText(blocked, "not a directory");
        Assert.IsFalse(new FileSr6CareerAttributeRecoveryStore(blocked).TryAdd(pending));
        var store = directory.OpenStore();
        Assert.IsTrue(store.TryAdd(pending));
        string lockPath = Directory.GetFiles(directory.Path, "*.lock").Single();
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
        using var directory = new Sr6RecoveryTestDirectory();
        var first = Sr6RecoveryTestData.Pending();
        var second = Sr6RecoveryTestData.Pending();
        bool[] added = await Task.WhenAll(
            Task.Run(() => directory.OpenStore().TryAdd(first)),
            Task.Run(() => directory.OpenStore().TryAdd(second)));
        Assert.AreEqual(1, added.Count(success => success));
        Assert.IsTrue(directory.OpenStore().TryRead(new OwnerScope(first.OriginalOwnerId), first.WorkspaceId, out var found));
        Assert.AreEqual(added[0] ? first.EntryId : second.EntryId, found!.EntryId);
    }
}
