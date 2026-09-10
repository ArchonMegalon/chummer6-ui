using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

/// <summary>Synchronizes a retained projection, never a freshly re-authorized workspace ID.</summary>
public static class ShellWorkspaceContextSynchronization
{
    public static async Task SynchronizeAsync(
        IShellPresenter shell, IChummerClient client, CharacterOverviewState requested, CancellationToken ct)
    {
        CharacterWorkspaceId? active = requested.Session.ActiveWorkspaceId ?? requested.WorkspaceId;
        if (client is not IOwnerBoundShellStateClient bound)
        {
            await shell.SyncWorkspaceContextAsync(active, ct).ConfigureAwait(false);
            return;
        }

        OwnerContextStamp? origin = requested.Session.ActiveWorkspaceId is not null || active is null
            ? requested.Session.OwnerContext : requested.DisplayOwnerContext;
        // An unopened empty presenter has no owner-bearing session to synchronize.
        // In particular, opening a dialog is not permission to clear another session.
        if (active is null && origin is null) return;
        if (origin is not { IsValid: true } original || bound.CaptureOwnerContext() != original)
            throw new InvalidOperationException("The original workspace owner changed; reload before navigating.");
        if (shell.State.OwnerContext is null)
            await shell.InitializeAsync(ct).ConfigureAwait(false);
        if (bound.CaptureOwnerContext() != original || shell.State.OwnerContext != original)
            throw new InvalidOperationException("The shell does not belong to the original workspace owner.");
        await shell.SyncWorkspaceContextAsync(original, active, ct).ConfigureAwait(false);
    }
}
