using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IOverviewCommandDispatcher
{
    Task DispatchAsync(string commandId, OverviewCommandExecutionContext context, CancellationToken ct);
}

public sealed record OverviewCommandExecutionContext(
    CharacterOverviewState State,
    CharacterWorkspaceId? CurrentWorkspace,
    IDesktopDialogFactory DialogFactory,
    Action<CharacterOverviewState> Publish,
    Func<string?, CancellationToken, Task<ShellBootstrapSnapshot>> GetShellBootstrapAsync,
    Func<string, string?, CancellationToken, Task<RuntimeInspectorProjection?>> GetRuntimeInspectorProfileAsync,
    Func<CancellationToken, Task> SaveAsync,
    Func<CancellationToken, Task> DownloadAsync,
    Func<CancellationToken, Task> PrintAsync,
    Func<CharacterWorkspaceId, CancellationToken, Task> LoadAsync,
    Func<string, string, CharacterOverviewState> CreateResetState,
    Func<CancellationToken, string, Task> CloseAllAsync,
    Func<CharacterWorkspaceId, CancellationToken, Task> CloseWorkspaceAsync);
