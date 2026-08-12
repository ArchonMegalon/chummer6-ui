#nullable enable annotations

using System;
using Chummer.Blazor.Services;
using Chummer.Contracts.Owners;
using Chummer.Workspaces.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildWorkspacePrivacyMaintenanceServiceTests
{
    [TestMethod]
    public void Cycle_replays_deletions_before_purging_expired_receipts()
    {
        var store = new RecordingPrivacyLifecycleStore();
        var service = CreateService(store);

        HostedBuildWorkspacePrivacyMaintenanceStatus status = service.RunCycle();

        Assert.IsTrue(status.Configured);
        Assert.IsTrue(status.Success);
        Assert.AreEqual(3, status.ReplayedDeletionCount);
        Assert.AreEqual(2, status.PurgedReceiptCount);
        CollectionAssert.AreEqual(new[] { "replay", "purge" }, store.Events.ToArray());
    }

    [TestMethod]
    public void Cycle_fails_closed_and_does_not_purge_when_replay_fails()
    {
        var store = new RecordingPrivacyLifecycleStore
        {
            ReplayResult = new WorkspacePrivacyMaintenanceResult(false, 0, "private provider detail")
        };
        var service = CreateService(store);

        HostedBuildWorkspacePrivacyMaintenanceStatus status = service.RunCycle();

        Assert.IsTrue(status.Configured);
        Assert.IsFalse(status.Success);
        Assert.AreEqual(0, status.ReplayedDeletionCount);
        Assert.AreEqual(0, status.PurgedReceiptCount);
        CollectionAssert.AreEqual(new[] { "replay" }, store.Events.ToArray());
        Assert.IsFalse(status.ToString().Contains("private provider detail", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Cycle_without_a_durable_store_is_an_inert_success()
    {
        var service = CreateService(store: null);

        HostedBuildWorkspacePrivacyMaintenanceStatus status = service.RunCycle();

        Assert.IsFalse(status.Configured);
        Assert.IsTrue(status.Success);
    }

    private static HostedBuildWorkspacePrivacyMaintenanceService CreateService(
        IWorkspacePrivacyLifecycleStore? store)
        => new(
            () => store,
            NullLogger<HostedBuildWorkspacePrivacyMaintenanceService>.Instance,
            HostedBuildWorkspacePrivacyMaintenanceOptions.Default);

    private sealed class RecordingPrivacyLifecycleStore : IWorkspacePrivacyLifecycleStore
    {
        public System.Collections.Generic.List<string> Events { get; } = new();

        public WorkspacePrivacyMaintenanceResult ReplayResult { get; set; } =
            new(true, 3);

        public WorkspacePrivacyMaintenanceResult PurgeResult { get; set; } =
            new(true, 2);

        public WorkspaceOwnerErasureResult EraseOwner(OwnerScope owner)
            => throw new NotSupportedException();

        public WorkspacePrivacyMaintenanceResult ApplyDeletionReplay(OwnerScope owner)
            => throw new NotSupportedException();

        public WorkspacePrivacyMaintenanceResult ApplyAllDeletionReplay()
        {
            Events.Add("replay");
            return ReplayResult;
        }

        public WorkspacePrivacyMaintenanceResult PurgeExpiredDeletionAuditReceipts()
        {
            Events.Add("purge");
            return PurgeResult;
        }
    }
}
