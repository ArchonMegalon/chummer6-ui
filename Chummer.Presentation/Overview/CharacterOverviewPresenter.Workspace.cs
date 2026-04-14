using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;
using System.Text.RegularExpressions;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private static readonly Regex GameEditionRegex = new(
        @"<gameedition>\s*([^<]+?)\s*</gameedition>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceImportDocument resolvedDocument = await ResolveImportDocumentAsync(document, ct);
            WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.ImportAsync(State, resolvedDocument, ct);
            Publish(await EnsureDefaultWorkspaceSectionAsync(result.State, ct));
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    private async Task<WorkspaceImportDocument> ResolveImportDocumentAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        string? explicitRulesetId = RulesetDefaults.NormalizeOptional(document.RulesetId);
        if (explicitRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, explicitRulesetId, document.Format);

        string? detectedRulesetId = TryDetectImportRulesetId(document);
        if (detectedRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, detectedRulesetId, document.Format);

        CharacterWorkspaceId? activeWorkspaceId = State.WorkspaceId;
        if (activeWorkspaceId is not null)
        {
            OpenWorkspaceState? activeWorkspace = State.OpenWorkspaces.FirstOrDefault(
                workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal));
            string? activeWorkspaceRulesetId = RulesetDefaults.NormalizeOptional(activeWorkspace?.RulesetId);
            if (activeWorkspaceRulesetId is not null)
                return new WorkspaceImportDocument(document.Content, activeWorkspaceRulesetId, document.Format);
        }

        string? commandRulesetId = RulesetDefaults.NormalizeOptional(State.Commands.FirstOrDefault()?.RulesetId);
        if (commandRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, commandRulesetId, document.Format);

        string? tabRulesetId = RulesetDefaults.NormalizeOptional(State.NavigationTabs.FirstOrDefault()?.RulesetId);
        if (tabRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, tabRulesetId, document.Format);

        ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
            ? shellBootstrap
            : await _bootstrapDataProvider.GetAsync(ct);
        string? bootstrapRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.PreferredRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.ActiveRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.RulesetId);
        if (bootstrapRulesetId is null)
            throw new InvalidOperationException("Workspace ruleset is required.");

        return new WorkspaceImportDocument(document.Content, bootstrapRulesetId, document.Format);
    }

    private static string? TryDetectImportRulesetId(WorkspaceImportDocument document)
    {
        if (document.Format != WorkspaceDocumentFormat.NativeXml)
            return null;

        Match match = GameEditionRegex.Match(document.Content);
        if (!match.Success)
            return null;

        string edition = match.Groups[1].Value.Trim();
        if (edition.Equals("SR5", StringComparison.OrdinalIgnoreCase)
            || edition.Equals("Shadowrun 5", StringComparison.OrdinalIgnoreCase))
        {
            return RulesetDefaults.Sr5;
        }

        if (edition.Equals("SR6", StringComparison.OrdinalIgnoreCase)
            || edition.Equals("Shadowrun 6", StringComparison.OrdinalIgnoreCase))
        {
            return RulesetDefaults.Sr6;
        }

        return RulesetDefaults.NormalizeOptional(edition);
    }

    private CharacterWorkspaceId? ResolveCurrentWorkspaceId()
    {
        return _workspaceOverviewLifecycleCoordinator.CurrentWorkspaceId ?? State.WorkspaceId;
    }

    public async Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.LoadAsync(State, id, ct);
            Publish(await EnsureDefaultWorkspaceSectionAsync(result.State, ct));
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        try
        {
            WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.SwitchAsync(State, id, ct);
            Publish(await EnsureDefaultWorkspaceSectionAsync(result.State, ct));
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAsync(State, id, ct);
        Publish(result.State);
    }

    private async Task CloseAllWorkspacesAsync(CancellationToken ct, string notice)
    {
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAllAsync(State, ct, notice);
        Publish(result.State);
    }

    private CharacterOverviewState CreateWorkspaceResetState(string commandId, string notice)
    {
        return _workspaceOverviewLifecycleCoordinator.CreateResetState(State, commandId, notice).State;
    }

    private async Task<CharacterOverviewState> EnsureDefaultWorkspaceSectionAsync(CharacterOverviewState nextState, CancellationToken ct)
    {
        if (nextState.WorkspaceId is null
            || nextState.ActiveSectionRows.Count > 0
            || !string.IsNullOrWhiteSpace(nextState.ActiveSectionId)
            || !string.IsNullOrWhiteSpace(nextState.ActiveSectionJson))
        {
            return nextState;
        }

        string? rulesetId = ResolveWorkspaceRulesetId(nextState, nextState.WorkspaceId.Value);
        NavigationTabDefinition? defaultTab = ResolveDefaultLoadedTab(nextState.NavigationTabs);
        if (defaultTab is null)
        {
            return nextState;
        }

        WorkspaceSurfaceActionDefinition? defaultAction = rulesetId is null
            ? null
            : _shellCatalogResolver.ResolveWorkspaceActionsForTab(defaultTab.Id, rulesetId)
                .FirstOrDefault(action =>
                    action.Kind == WorkspaceSurfaceActionKind.Section
                    && string.Equals(action.TargetId, defaultTab.SectionId, StringComparison.Ordinal));

        WorkspaceSectionRenderResult renderedSection = await _workspaceSectionRenderer.RenderSectionAsync(
            _client,
            nextState.WorkspaceId.Value,
            defaultAction?.TargetId ?? defaultTab.SectionId,
            defaultAction?.TabId ?? defaultTab.Id,
            defaultAction?.Id ?? $"{defaultTab.Id}.{defaultTab.SectionId}",
            nextState.ActiveTabId,
            nextState.ActiveActionId,
            ct);

        return nextState with
        {
            ActiveTabId = renderedSection.ActiveTabId,
            ActiveActionId = renderedSection.ActiveActionId,
            ActiveSectionId = renderedSection.ActiveSectionId,
            ActiveSectionJson = renderedSection.ActiveSectionJson,
            ActiveSectionRows = renderedSection.ActiveSectionRows,
            ActiveBuildLab = renderedSection.ActiveBuildLab,
            ActiveBrowseWorkspace = renderedSection.ActiveBrowseWorkspace,
            ActiveNpcPersonaStudio = renderedSection.ActiveNpcPersonaStudio
        };
    }

    private static NavigationTabDefinition? ResolveDefaultLoadedTab(IReadOnlyList<NavigationTabDefinition> tabs)
    {
        return tabs.FirstOrDefault(tab => string.Equals(tab.Id, "tab-gear", StringComparison.Ordinal) && tab.EnabledByDefault)
            ?? tabs.FirstOrDefault(tab => string.Equals(tab.Id, "tab-info", StringComparison.Ordinal) && tab.EnabledByDefault)
            ?? tabs.FirstOrDefault(tab => tab.EnabledByDefault)
            ?? tabs.FirstOrDefault();
    }

    private static string? ResolveWorkspaceRulesetId(CharacterOverviewState state, CharacterWorkspaceId workspaceId)
    {
        OpenWorkspaceState? workspace = state.OpenWorkspaces.FirstOrDefault(
            candidate => string.Equals(candidate.Id.Value, workspaceId.Value, StringComparison.Ordinal));
        string? workspaceRulesetId = RulesetDefaults.NormalizeOptional(workspace?.RulesetId);
        if (workspaceRulesetId is not null)
            return workspaceRulesetId;

        string? openWorkspaceRulesetId = state.OpenWorkspaces
            .Select(openWorkspace => RulesetDefaults.NormalizeOptional(openWorkspace.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
        if (openWorkspaceRulesetId is not null)
            return openWorkspaceRulesetId;

        string? commandRulesetId = state.Commands
            .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
        if (commandRulesetId is not null)
            return commandRulesetId;

        return state.NavigationTabs
            .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
            .FirstOrDefault(rulesetId => rulesetId is not null);
    }
}
