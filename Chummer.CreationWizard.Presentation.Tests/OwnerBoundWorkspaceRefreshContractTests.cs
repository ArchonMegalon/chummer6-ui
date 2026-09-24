using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class OwnerBoundWorkspaceRefreshContractTests
{
    [TestMethod]
    public async Task Host_shell_sync_request_preserves_legacy_owner_workspace_and_token()
    {
        var legacy = new LegacyRefresh();
        IOwnerBoundWorkspaceRefreshPresenter refresh = legacy;
        var owner = new OwnerContextStamp(new("account-a"), "original-issuer", 7);
        var id = new CharacterWorkspaceId("refresh-contract");
        using var cancellation = new CancellationTokenSource();

        await refresh.LoadBeforeShellSyncAsync(owner, id, cancellation.Token);

        Assert.AreEqual(1, legacy.Calls);
        Assert.AreEqual(owner, legacy.Owner);
        Assert.AreEqual(id, legacy.Workspace);
        Assert.AreEqual(cancellation.Token, legacy.Token);
    }

    [TestMethod]
    public async Task Host_shell_sync_request_does_not_hide_legacy_load_failure()
    {
        var legacy = new LegacyRefresh { Fail = true };
        IOwnerBoundWorkspaceRefreshPresenter refresh = legacy;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => refresh.LoadBeforeShellSyncAsync(
            new(new("account-a"), "original-issuer", 7), new("refresh-contract"), default));
        Assert.AreEqual(1, legacy.Calls);
    }

    private sealed class LegacyRefresh : IOwnerBoundWorkspaceRefreshPresenter
    {
        public int Calls;
        public bool Fail;
        public OwnerContextStamp Owner;
        public CharacterWorkspaceId Workspace;
        public CancellationToken Token;

        public Task LoadAsync(OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct)
        {
            Calls++;
            Owner = expectedOwner;
            Workspace = id;
            Token = ct;
            return Fail ? Task.FromException(new InvalidOperationException("Synthetic reload failure.")) : Task.CompletedTask;
        }
    }
}
