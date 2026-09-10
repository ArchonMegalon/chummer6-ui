using Chummer.Application.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Shell;

/// <summary>
/// In-process shell read/write capability. Stamps describe the original live owner
/// authority; implementations must acquire that exact authority for synchronous
/// owner-scoped work. This is not a serialized credential or remote authorization.
/// </summary>
public interface IOwnerBoundShellStateClient
{
    OwnerContextStamp CaptureOwnerContext();

    Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(
        OwnerContextStamp ownerContext, string? rulesetId, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(
        OwnerContextStamp ownerContext, CancellationToken ct);

    Task SaveShellSessionAsync(
        OwnerContextStamp ownerContext, ShellSessionState session, CancellationToken ct);

    Task SaveShellPreferencesAsync(
        OwnerContextStamp ownerContext, ShellPreferences preferences, CancellationToken ct);
}
