using System.Diagnostics;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;

namespace Chummer.Desktop.Runtime.Tests;

[TestClass]
public sealed class GrantBoundRoamingSyncLedgerTests
{
    [TestMethod]
    public async Task Linux_ledger_keeps_the_active_lease_and_recovers_after_holder_exit()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("This test uses the Linux flock utility to hold the ledger lease.");
        }

        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var owner = new OwnerScope("test-owner");
            var workspace = new CharacterWorkspaceId("test-workspace");
            var store = new GrantBoundRoamingSyncLedgerStore(directory, "test-head");
            store.SetPendingRemote(owner, workspace, "{}", 1, null, 1);
            var before = store.TryGet(owner, workspace);
            Assert.IsNotNull(before);
            Assert.IsNotNull(before.PendingRemote);
            string lockPath = Directory.GetFiles(directory, "*.json").Single() + ".lock";

            var start = new ProcessStartInfo("flock")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                ArgumentList = { "--exclusive", "--no-fork", lockPath, "cat" }
            };
            using var holder = Process.Start(start)!;
            try
            {
                // cat starts and echoes our marker only after flock has acquired the lease.
                await holder.StandardInput.WriteLineAsync("ready");
                await holder.StandardInput.FlushAsync();
                Assert.AreEqual("ready", await holder.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));

                Assert.IsFalse(store.ClearPendingRemote(owner, workspace));
                Assert.IsTrue(File.Exists(lockPath));
                Assert.IsFalse(store.ClearPendingRemote(owner, workspace));
                Assert.AreEqual(before.LedgerRevision, store.TryGet(owner, workspace)?.LedgerRevision);
                Assert.IsNotNull(store.TryGet(owner, workspace)?.PendingRemote);
            }
            finally
            {
                if (!holder.HasExited)
                {
                    holder.Kill();
                }
                await holder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }

            Assert.IsTrue(store.ClearPendingRemote(owner, workspace));
            var after = store.TryGet(owner, workspace);
            Assert.IsNotNull(after);
            Assert.IsNull(after.PendingRemote);
            Assert.AreEqual(before.LedgerRevision + 1, after.LedgerRevision);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
