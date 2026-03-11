using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public async Task ExecuteCommandAsync(string commandId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            Publish(State with { Error = "Command id is required." });
            return;
        }

        Publish(State with
        {
            LastCommandId = commandId,
            Error = null
        });

        OverviewCommandExecutionContext context = new(
            State: State,
            CurrentWorkspace: ResolveCurrentWorkspaceId(),
            DialogFactory: _dialogFactory,
            Publish: Publish,
            GetShellBootstrapAsync: (rulesetId, token) => _client.GetShellBootstrapAsync(rulesetId, token),
            GetRuntimeInspectorProfileAsync: (profileId, rulesetId, token) => _client.GetRuntimeInspectorProfileAsync(profileId, rulesetId, token),
            SaveAsync: SaveAsync,
            DownloadAsync: DownloadAsync,
            PrintAsync: PrintAsync,
            LoadAsync: LoadAsync,
            CreateResetState: CreateWorkspaceResetState,
            CloseAllAsync: CloseAllWorkspacesAsync,
            CloseWorkspaceAsync: CloseWorkspaceAsync);

        await _commandDispatcher.DispatchAsync(commandId, context, ct);
    }

    public async Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (action is null)
        {
            Publish(State with { Error = "Workspace action is required." });
            return;
        }

        if (action.RequiresOpenCharacter && currentWorkspace is null)
        {
            Publish(State with { Error = "No workspace loaded." });
            return;
        }

        switch (action.Kind)
        {
            case WorkspaceSurfaceActionKind.Section:
                await LoadSectionAsync(action.TargetId, action.TabId, action.Id, ct);
                return;
            case WorkspaceSurfaceActionKind.Summary:
                await RenderSummaryAction(action, ct);
                return;
            case WorkspaceSurfaceActionKind.Validate:
                await RenderValidateAction(action, ct);
                return;
            case WorkspaceSurfaceActionKind.Metadata:
                Publish(State with
                {
                    ActiveTabId = action.TabId,
                    ActiveActionId = action.Id,
                    Error = null,
                    ActiveDialog = _dialogFactory.CreateMetadataDialog(State.Profile, State.Preferences)
                });
                return;
            case WorkspaceSurfaceActionKind.Command:
                await ExecuteCommandAsync(action.TargetId, ct);
                Publish(State with
                {
                    ActiveTabId = action.TabId,
                    ActiveActionId = action.Id
                });
                return;
            default:
                Publish(State with { Error = $"Unsupported workspace action kind '{action.Kind}'." });
                return;
        }
    }

    public async Task SelectTabAsync(string tabId, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (string.IsNullOrWhiteSpace(tabId))
        {
            Publish(State with { Error = "Tab id is required." });
            return;
        }

        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No workspace loaded." });
            return;
        }

        string? rulesetId = ResolveWorkspaceRulesetId(currentWorkspace.Value);
        NavigationTabDefinition? tab = State.NavigationTabs.FirstOrDefault(item => string.Equals(item.Id, tabId, StringComparison.Ordinal));
        if (tab is null && !string.IsNullOrWhiteSpace(rulesetId))
        {
            tab = _shellCatalogResolver.ResolveNavigationTabs(rulesetId)
                .FirstOrDefault(item => string.Equals(item.Id, tabId, StringComparison.Ordinal));
        }
        if (tab is null)
        {
            Publish(State with { Error = $"Unknown tab '{tabId}'." });
            return;
        }

        WorkspaceSurfaceActionDefinition? defaultAction = null;
        if (!string.IsNullOrWhiteSpace(rulesetId))
        {
            defaultAction = _shellCatalogResolver.ResolveWorkspaceActionsForTab(
                    tab.Id,
                    rulesetId)
                .FirstOrDefault(action =>
                    action.Kind == WorkspaceSurfaceActionKind.Section
                    && string.Equals(action.TargetId, tab.SectionId, StringComparison.Ordinal));
        }
        if (defaultAction is not null)
        {
            await ExecuteWorkspaceActionAsync(defaultAction, ct);
            return;
        }

        await LoadSectionAsync(tab.SectionId, tab.Id, $"{tab.Id}.{tab.SectionId}", ct);
    }

    private string? ResolveWorkspaceRulesetId(CharacterWorkspaceId workspaceId)
    {
        OpenWorkspaceState? workspace = State.OpenWorkspaces.FirstOrDefault(
            candidate => string.Equals(candidate.Id.Value, workspaceId.Value, StringComparison.Ordinal));
        string? workspaceRulesetId = RulesetDefaults.NormalizeOptional(workspace?.RulesetId);
        if (workspaceRulesetId is not null)
            return workspaceRulesetId;

        string? openWorkspaceRulesetId = State.OpenWorkspaces
            .Select(openWorkspace => RulesetDefaults.NormalizeOptional(openWorkspace.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
        if (openWorkspaceRulesetId is not null)
            return openWorkspaceRulesetId;

        string? commandRulesetId = State.Commands
            .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
        if (commandRulesetId is not null)
            return commandRulesetId;

        return State.NavigationTabs
            .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
    }
}
