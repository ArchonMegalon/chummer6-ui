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
            Publish(result.State);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
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
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = State.OpenWorkspaces ?? [];
        IReadOnlyList<AppCommandDefinition> commands = State.Commands ?? [];
        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        string? explicitRulesetId = RulesetDefaults.NormalizeOptional(document.RulesetId);
        if (explicitRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, explicitRulesetId, document.Format);

        string? detectedRulesetId = TryDetectImportRulesetId(document);
        if (detectedRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, detectedRulesetId, document.Format);

        CharacterWorkspaceId? activeWorkspaceId = State.WorkspaceId;
        if (activeWorkspaceId is not null)
        {
            OpenWorkspaceState? activeWorkspace = openWorkspaces.FirstOrDefault(
                workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal));
            string? activeWorkspaceRulesetId = RulesetDefaults.NormalizeOptional(activeWorkspace?.RulesetId);
            if (activeWorkspaceRulesetId is not null)
                return new WorkspaceImportDocument(document.Content, activeWorkspaceRulesetId, document.Format);
        }

        string? commandRulesetId = RulesetDefaults.NormalizeOptional(commands.FirstOrDefault()?.RulesetId);
        if (commandRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, commandRulesetId, document.Format);

        string? tabRulesetId = RulesetDefaults.NormalizeOptional(navigationTabs.FirstOrDefault()?.RulesetId);
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

    private async Task EnsureNavigationContextAsync(CancellationToken ct)
    {
        IReadOnlyList<AppCommandDefinition> commands = State.Commands ?? [];
        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        if (commands.Count > 0 && navigationTabs.Count > 0)
        {
            return;
        }

        string? rulesetId = ResolveCurrentWorkspaceId() is { } currentWorkspace
            ? ResolveWorkspaceRulesetId(currentWorkspace)
            : null;
        ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
            ? shellBootstrap
            : await _bootstrapDataProvider.GetAsync(rulesetId, ct);
        Publish(State with
        {
            Error = null,
            Commands = bootstrap.Commands ?? commands,
            NavigationTabs = bootstrap.NavigationTabs ?? navigationTabs
        });
    }

    private async Task EnsureDefaultWorkspaceSurfaceAsync(CancellationToken ct)
    {
        if (ResolveCurrentWorkspaceId() is null || !string.IsNullOrWhiteSpace(State.ActiveSectionId))
        {
            return;
        }

        await EnsureNavigationContextAsync(ct);

        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        string? defaultTabId = !string.IsNullOrWhiteSpace(State.ActiveTabId)
            ? State.ActiveTabId
            : ResolveDefaultWorkspaceTabId(navigationTabs, State.LastCommandId);
        if (string.IsNullOrWhiteSpace(defaultTabId))
        {
            return;
        }

        await SelectTabAsync(defaultTabId, ct);
    }

    private static string? ResolveDefaultWorkspaceTabId(
        IReadOnlyList<NavigationTabDefinition> navigationTabs,
        string? lastCommandId)
    {
        if (IsNewWorkspaceCommand(lastCommandId))
        {
            string[] visibleNewWorkspaceTabPreference =
            [
                "tab-attributes",
                "tab-skills",
                "tab-info",
                "tab-gear",
                "tab-qualities"
            ];
            foreach (string preferredTabId in visibleNewWorkspaceTabPreference)
            {
                string? matchingTabId = navigationTabs
                    .FirstOrDefault(tab => tab.EnabledByDefault && string.Equals(tab.Id, preferredTabId, StringComparison.Ordinal))
                    ?.Id;
                if (!string.IsNullOrWhiteSpace(matchingTabId))
                {
                    return matchingTabId;
                }
            }

            return navigationTabs
                .FirstOrDefault(tab => tab.EnabledByDefault
                    && !string.Equals(tab.SectionId, "build-lab", StringComparison.Ordinal))?.Id
                ?? navigationTabs.FirstOrDefault(tab => tab.EnabledByDefault)?.Id;
        }

        return navigationTabs
            .FirstOrDefault(tab => tab.EnabledByDefault && string.Equals(tab.Id, "tab-info", StringComparison.Ordinal))
            ?.Id
            ?? navigationTabs.FirstOrDefault(tab => tab.EnabledByDefault)?.Id;
    }

    private static bool IsNewWorkspaceCommand(string? commandId)
        => string.Equals(commandId, "new_character", StringComparison.Ordinal)
            || string.Equals(commandId, "new_critter", StringComparison.Ordinal);

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
            Publish(result.State);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
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
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.SwitchAsync(State, id, ct);
        Publish(result.State);
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
}
