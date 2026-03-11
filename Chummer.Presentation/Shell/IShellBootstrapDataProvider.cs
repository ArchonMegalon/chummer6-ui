using Chummer.Contracts.Presentation;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Shell;

public sealed record ShellBootstrapData(
    string RulesetId,
    IReadOnlyList<AppCommandDefinition> Commands,
    IReadOnlyList<NavigationTabDefinition> NavigationTabs,
    IReadOnlyList<WorkspaceListItem> Workspaces,
    string PreferredRulesetId,
    string ActiveRulesetId,
    CharacterWorkspaceId? ActiveWorkspaceId = null,
    string? ActiveTabId = null,
    IReadOnlyDictionary<string, string>? ActiveTabsByWorkspace = null,
    IReadOnlyList<WorkflowDefinition>? WorkflowDefinitions = null,
    IReadOnlyList<WorkflowSurfaceDefinition>? WorkflowSurfaces = null,
    ActiveRuntimeStatusProjection? ActiveRuntime = null);

public interface IShellBootstrapDataProvider
{
    async Task<IReadOnlyList<WorkspaceListItem>> GetWorkspacesAsync(CancellationToken ct)
    {
        ShellBootstrapData bootstrap = await GetAsync(ct);
        return bootstrap.Workspaces;
    }

    Task<ShellBootstrapData> GetAsync(CancellationToken ct);

    Task<ShellBootstrapData> GetAsync(string? rulesetId, CancellationToken ct)
    {
        return GetAsync(ct);
    }
}
