using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

public interface IDesktopWorkspaceRoamingSync
{
    Task SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct);

    Task SynchronizeOutboundAsync(OwnerScope owner, CharacterWorkspaceId workspaceId, CancellationToken ct);
}

public sealed class NoOpDesktopWorkspaceRoamingSync : IDesktopWorkspaceRoamingSync
{
    public Task SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task SynchronizeOutboundAsync(OwnerScope owner, CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
