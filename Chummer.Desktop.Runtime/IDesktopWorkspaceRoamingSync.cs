using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

public enum DesktopWorkspaceRoamingOutcome
{
    AlreadyCurrent = 0,
    Applied = 1,
    Conflict = 2,
    Unavailable = 3,
    Unauthorized = 4
}

public sealed record DesktopWorkspaceRoamingResult(
    DesktopWorkspaceRoamingOutcome Outcome,
    CharacterWorkspaceId? WorkspaceId = null,
    long? RemoteRevision = null,
    string? ServerToken = null)
{
    public bool Success => Outcome is DesktopWorkspaceRoamingOutcome.Applied
        or DesktopWorkspaceRoamingOutcome.AlreadyCurrent;

    public static DesktopWorkspaceRoamingResult AlreadyCurrent(CharacterWorkspaceId? workspaceId = null)
        => new(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, workspaceId);
}

public interface IDesktopWorkspaceRoamingSync
{
    Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct);

    Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

public sealed class NoOpDesktopWorkspaceRoamingSync : IDesktopWorkspaceRoamingSync
{
    public Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(DesktopWorkspaceRoamingResult.AlreadyCurrent());
    }

    public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
        OwnerScope owner,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(DesktopWorkspaceRoamingResult.AlreadyCurrent(workspaceId));
    }
}
