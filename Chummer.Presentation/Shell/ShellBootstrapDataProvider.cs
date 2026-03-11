using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using System.Diagnostics.CodeAnalysis;

namespace Chummer.Presentation.Shell;

public sealed class ShellBootstrapDataProvider : IShellBootstrapDataProvider
{
    private const string DefaultBootstrapCacheKey = "__default__";
    private static readonly TimeSpan BootstrapCacheWindow = TimeSpan.FromSeconds(10);
    private readonly IChummerClient _client;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Dictionary<string, CachedBootstrapData> _cachedBootstrapsByKey = new(StringComparer.Ordinal);

    public ShellBootstrapDataProvider(IChummerClient client)
    {
        _client = client;
    }

    public async Task<ShellBootstrapData> GetAsync(CancellationToken ct)
    {
        return await GetAsync(rulesetId: null, ct);
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> GetWorkspacesAsync(CancellationToken ct)
    {
        ShellBootstrapData bootstrap = await GetAsync(ct);
        return bootstrap.Workspaces;
    }

    public async Task<ShellBootstrapData> GetAsync(string? rulesetId, CancellationToken ct)
    {
        string? requestedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId);
        string cacheKey = requestedRulesetId ?? DefaultBootstrapCacheKey;

        if (TryGetCachedBootstrap(cacheKey, out ShellBootstrapData? cachedBootstrap))
        {
            return cachedBootstrap;
        }

        await _sync.WaitAsync(ct);
        try
        {
            if (TryGetCachedBootstrap(cacheKey, out cachedBootstrap))
            {
                return cachedBootstrap;
            }

            ShellBootstrapData bootstrap = CreateBootstrapData(
                await _client.GetShellBootstrapAsync(requestedRulesetId, ct));
            CacheBootstrap(cacheKey, requestedRulesetId, bootstrap);
            return bootstrap;
        }
        finally
        {
            _sync.Release();
        }
    }

    private bool TryGetCachedBootstrap(string cacheKey, [NotNullWhen(true)] out ShellBootstrapData? cachedBootstrap)
    {
        if (_cachedBootstrapsByKey.TryGetValue(cacheKey, out CachedBootstrapData? cachedEntry)
            && DateTimeOffset.UtcNow - cachedEntry.CachedAtUtc <= BootstrapCacheWindow)
        {
            cachedBootstrap = cachedEntry.Data;
            return true;
        }

        cachedBootstrap = null;
        return false;
    }

    private void CacheBootstrap(string cacheKey, string? requestedRulesetId, ShellBootstrapData bootstrap)
    {
        CachedBootstrapData cached = new(bootstrap, DateTimeOffset.UtcNow);
        _cachedBootstrapsByKey[cacheKey] = cached;

        string? resolvedRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.RulesetId);
        if (!string.IsNullOrWhiteSpace(resolvedRulesetId)
            && !string.Equals(cacheKey, resolvedRulesetId, StringComparison.Ordinal))
        {
            _cachedBootstrapsByKey[resolvedRulesetId] = cached;
        }

        if (!string.IsNullOrWhiteSpace(requestedRulesetId)
            && !string.Equals(requestedRulesetId, cacheKey, StringComparison.Ordinal)
            && !string.Equals(requestedRulesetId, resolvedRulesetId, StringComparison.Ordinal))
        {
            _cachedBootstrapsByKey[requestedRulesetId] = cached;
        }
    }

    private static ShellBootstrapData CreateBootstrapData(ShellBootstrapSnapshot snapshot)
    {
        return new ShellBootstrapData(
            RulesetId: RulesetDefaults.NormalizeOptional(snapshot.RulesetId) ?? string.Empty,
            Commands: snapshot.Commands,
            NavigationTabs: snapshot.NavigationTabs,
            Workspaces: snapshot.Workspaces,
            PreferredRulesetId: RulesetDefaults.NormalizeOptional(snapshot.PreferredRulesetId) ?? string.Empty,
            ActiveRulesetId: RulesetDefaults.NormalizeOptional(snapshot.ActiveRulesetId) ?? string.Empty,
            ActiveWorkspaceId: snapshot.ActiveWorkspaceId,
            ActiveTabId: NormalizeTabId(snapshot.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(snapshot.ActiveTabsByWorkspace),
            WorkflowDefinitions: snapshot.WorkflowDefinitions ?? [],
            WorkflowSurfaces: snapshot.WorkflowSurfaces ?? [],
            ActiveRuntime: snapshot.ActiveRuntime);
    }

    private static string? NormalizeTabId(string? tabId)
    {
        return string.IsNullOrWhiteSpace(tabId)
            ? null
            : tabId.Trim();
    }

    private static IReadOnlyDictionary<string, string>? NormalizeWorkspaceTabMap(IReadOnlyDictionary<string, string>? rawMap)
    {
        if (rawMap is null || rawMap.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in rawMap)
        {
            string? workspaceId = string.IsNullOrWhiteSpace(entry.Key)
                ? null
                : entry.Key.Trim();
            string? tabId = NormalizeTabId(entry.Value);
            if (workspaceId is null || tabId is null)
            {
                continue;
            }

            normalized[workspaceId] = tabId;
        }

        return normalized.Count == 0
            ? null
            : normalized;
    }

    private sealed record CachedBootstrapData(
        ShellBootstrapData Data,
        DateTimeOffset CachedAtUtc);
}
