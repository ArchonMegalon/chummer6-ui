using System;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class UnavailableDesktopWorkspaceRoamingSyncTests
{
    [TestMethod]
    public async Task Disabled_roaming_never_claims_a_successful_transfer()
    {
        var sync = new UnavailableDesktopWorkspaceRoamingSync();
        OwnerScope linkedOwner = new("install-account:subject-alpha");
        CharacterWorkspaceId workspace = new("unsynced-local-work");
        DesktopWorkspaceRoamingResult inbound = await sync.SynchronizeInboundAsync(linkedOwner, CancellationToken.None);
        DesktopWorkspaceRoamingResult outbound = await sync.SynchronizeOutboundAsync(linkedOwner, workspace, CancellationToken.None);
        Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unavailable, inbound.Outcome);
        Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unavailable, outbound.Outcome);
        Assert.IsFalse(inbound.Success);
        Assert.IsFalse(outbound.Success);
        Assert.AreEqual(workspace, outbound.WorkspaceId);
        Assert.IsNull(inbound.RemoteRevision);
        Assert.IsNull(outbound.ServerToken);
    }

    [TestMethod]
    public async Task Disabled_roaming_honors_cancellation()
    {
        var sync = new UnavailableDesktopWorkspaceRoamingSync();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => sync.SynchronizeInboundAsync(OwnerScope.LocalSingleUser, cancel.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => sync.SynchronizeOutboundAsync(OwnerScope.LocalSingleUser, new("local-work"), cancel.Token));
    }
}
