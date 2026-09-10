using Chummer.Application.Owners;
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
    private readonly Dictionary<BootstrapCacheKey, CachedBootstrapData> _cachedBootstrapsByKey = new();

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
        ct.ThrowIfCancellationRequested();
        string? requestedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId);
        if (_client is not IOwnerBoundShellStateClient boundClient)
        {
            // A remote/unbound client provides no stable local owner epoch. Do not
            // reuse owner-bearing results across calls under a ruleset-only key.
            return CreateBootstrapData(await _client.GetShellBootstrapAsync(requestedRulesetId, ct));
        }

        OwnerContextStamp original = boundClient.CaptureOwnerContext();
        RequireCurrentOwner(boundClient, original);
        BootstrapCacheKey cacheKey = new(original, requestedRulesetId ?? DefaultBootstrapCacheKey);
        await _sync.WaitAsync(ct);
        try
        {
            RequireCurrentOwner(boundClient, original);
            // Every dictionary access is under _sync, including cache hits.
            foreach (BootstrapCacheKey staleKey in _cachedBootstrapsByKey.Keys
                .Where(key => key.OwnerContext != original).ToArray())
                _cachedBootstrapsByKey.Remove(staleKey);
            if (TryGetCachedBootstrap(cacheKey, out ShellBootstrapData? cachedBootstrap))
            {
                return cachedBootstrap;
            }

            ShellBootstrapData bootstrap = CreateBootstrapData(
                await boundClient.GetShellBootstrapAsync(original, requestedRulesetId, ct))
                with { OwnerContext = original };
            // Preserve the request's stamp. A later capture only rejects stale
            // responses; it must never relabel old data as the new owner's data.
            RequireCurrentOwner(boundClient, original);
            CacheBootstrap(cacheKey, requestedRulesetId, bootstrap);
            return bootstrap;
        }
        finally
        {
            _sync.Release();
        }
    }

    private static void RequireCurrentOwner(IOwnerBoundShellStateClient client, OwnerContextStamp original)
    {
        if (!original.IsValid || client.CaptureOwnerContext() != original)
            throw new InvalidOperationException("Shell owner changed; reload the shell before continuing.");
    }

    private bool TryGetCachedBootstrap(BootstrapCacheKey cacheKey, [NotNullWhen(true)] out ShellBootstrapData? cachedBootstrap)
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

    private void CacheBootstrap(BootstrapCacheKey cacheKey, string? requestedRulesetId, ShellBootstrapData bootstrap)
    {
        CachedBootstrapData cached = new(bootstrap, DateTimeOffset.UtcNow);
        _cachedBootstrapsByKey[cacheKey] = cached;

        string? resolvedRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.RulesetId);
        if (!string.IsNullOrWhiteSpace(resolvedRulesetId)
            && !string.Equals(cacheKey.RulesetId, resolvedRulesetId, StringComparison.Ordinal))
        {
            _cachedBootstrapsByKey[new(cacheKey.OwnerContext, resolvedRulesetId)] = cached;
        }

        if (!string.IsNullOrWhiteSpace(requestedRulesetId)
            && !string.Equals(requestedRulesetId, cacheKey.RulesetId, StringComparison.Ordinal)
            && !string.Equals(requestedRulesetId, resolvedRulesetId, StringComparison.Ordinal))
        {
            _cachedBootstrapsByKey[new(cacheKey.OwnerContext, requestedRulesetId)] = cached;
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

    private readonly record struct BootstrapCacheKey(OwnerContextStamp OwnerContext, string RulesetId);

    private sealed record CachedBootstrapData(
        ShellBootstrapData Data,
        DateTimeOffset CachedAtUtc);
}
