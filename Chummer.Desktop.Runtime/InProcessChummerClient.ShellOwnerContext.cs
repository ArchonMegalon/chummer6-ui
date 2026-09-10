using Chummer.Application.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;

namespace Chummer.Desktop.Runtime;

public sealed partial class InProcessChummerClient : IOwnerBoundShellStateClient
{
    public Task SaveShellPreferencesAsync(
        OwnerContextStamp ownerContext, ShellPreferences preferences, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preferences);
        return _workspaceOperations.Execute(() => WithOwnerLease(ownerContext, owner =>
        {
            _shellPreferencesService.Save(owner, preferences);
            return true;
        }), ct);
    }

    public Task SaveShellSessionAsync(
        OwnerContextStamp ownerContext, ShellSessionState session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);
        ShellSessionState normalized = new(
            ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
            ActiveTabId: NormalizeTabId(session.ActiveTabId),
            ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
        return _workspaceOperations.Execute(() => WithOwnerLease(ownerContext, owner =>
        {
            _shellSessionService.Save(owner, normalized);
            return true;
        }), ct);
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(
        OwnerContextStamp ownerContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
            await SynchronizeShellInboundAsync(ownerContext, ct).ConfigureAwait(false);
            return WithOwnerLease(ownerContext, owner => _workspaceService.List(owner));
        }, ct).ConfigureAwait(false);
    }

    public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(
        OwnerContextStamp ownerContext, string? rulesetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await _workspaceOperations.ExecuteAsync(async () =>
        {
            await SynchronizeShellInboundAsync(ownerContext, ct).ConfigureAwait(false);
            // The complete synchronous projection shares one admitted authority.
            // Never stamp a mixture of independently ownerless reads afterward.
            return WithOwnerLease(ownerContext, owner =>
            {
                IReadOnlyList<WorkspaceListItem> workspaces = _workspaceService.List(owner, ShellBootstrapDefaults.MaxWorkspaces);
                ShellPreferences preferences = _shellPreferencesService.Load(owner);
                ShellSessionState session = _shellSessionService.Load(owner);
                string fallbackRulesetId = _rulesetSelectionPolicy.GetDefaultRulesetId();
                string preferredRulesetId = ResolvePreferredRulesetId(preferences.PreferredRulesetId, workspaces, fallbackRulesetId);
                CharacterWorkspaceId? activeWorkspaceId = ResolveActiveWorkspaceId(workspaces, session.ActiveWorkspaceId);
                string activeRulesetId = ResolveRulesetForWorkspace(activeWorkspaceId, workspaces, preferredRulesetId, fallbackRulesetId);
                string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
                    ?? activeRulesetId
                    ?? fallbackRulesetId;
                string effectiveActiveRulesetId = string.IsNullOrWhiteSpace(activeRulesetId)
                    ? effectiveRulesetId
                    : activeRulesetId;

                return new ShellBootstrapSnapshot(
                    RulesetId: effectiveRulesetId,
                    Commands: _shellCatalogResolver.ResolveCommands(effectiveRulesetId),
                    NavigationTabs: _shellCatalogResolver.ResolveNavigationTabs(effectiveRulesetId),
                    Workspaces: workspaces,
                    PreferredRulesetId: preferredRulesetId,
                    ActiveRulesetId: effectiveActiveRulesetId,
                    ActiveWorkspaceId: activeWorkspaceId,
                    ActiveTabId: NormalizeTabId(session.ActiveTabId),
                    ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace),
                    WorkflowDefinitions: _shellCatalogResolver.ResolveWorkflowDefinitions(effectiveRulesetId),
                    WorkflowSurfaces: _shellCatalogResolver.ResolveWorkflowSurfaces(effectiveRulesetId),
                    ActiveRuntime: _activeRuntimeStatusService?.GetActiveProfileStatus(owner, effectiveRulesetId));
            });
        }, ct).ConfigureAwait(false);
    }

    private async Task SynchronizeShellInboundAsync(OwnerContextStamp ownerContext, CancellationToken ct)
    {
        // Called only inside the admitted executor. Reject an already stale
        // action before starting roaming, but never hold the local lease over
        // network/async work. Roaming has its own owner fence, not an atomic
        // bootstrap transaction or an original-stamp rollback guarantee.
        WithOwnerLease(ownerContext, static _ => true);
        LastWorkspaceRoamingResult = await _workspaceRoamingSync
            .SynchronizeInboundAsync(ownerContext.Owner, ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        // The caller reacquires the ORIGINAL stamp for local materialization.
        // A transition during roaming, including A-to-B-to-A, cannot authorize
        // a shell snapshot or list under a freshly captured authority.
    }
}
