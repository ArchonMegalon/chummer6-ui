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
            Publish(result.State);
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
