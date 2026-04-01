using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using System.Text.Json;

namespace Chummer.Avalonia;

internal static class MainWindowShellFrameProjector
{
    public static MainWindowShellFrame Project(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        string language = DesktopLocalizationCatalog.NormalizeOrDefault(state.Preferences.Language);
        ActiveWorkspaceContext workspaceContext = ResolveActiveWorkspaceContext(shellSurface);
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById = BuildWorkspaceActionLookup(shellSurface.WorkspaceActions);
        CommandPaletteItem[] commands = ProjectCommands(state, shellSurface, commandAvailabilityEvaluator);

        return new MainWindowShellFrame(
            HeaderState: new MainWindowHeaderState(
                ToolStrip: new ToolStripState(
                    BuildToolStripStatusText(state, shellSurface, workspaceContext, language)),
                MenuBar: new MenuBarState(
                    OpenMenuId: shellSurface.OpenMenuId,
                    KnownMenuIds: shellSurface.MenuRoots.Select(menu => menu.Id).ToArray(),
                    IsBusy: state.IsBusy)),
            ChromeState: new MainWindowChromeState(
                WorkspaceStrip: new WorkspaceStripState(
                    BuildWorkspaceStripText(workspaceContext, language)),
                SummaryHeader: new SummaryHeaderState(
                    Name: state.Profile?.Name,
                    Alias: state.Profile?.Alias,
                    Karma: state.Progress?.Karma.ToString(),
                    Skills: state.Skills?.Count.ToString(),
                    RuntimeSummary: ShellStatusTextFormatter.BuildActiveRuntimeSummary(shellSurface.ActiveRuntime, shellSurface.ActiveRulesetId),
                    CanInspectRuntime: shellSurface.ActiveRuntime is not null),
                StatusStrip: new StatusStripState(
                    CharacterState: BuildCharacterStateText(workspaceContext, language),
                    ServiceState: BuildServiceStateText(shellSurface, language),
                    TimeState: DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.shell.status.time", language, DateTimeOffset.UtcNow.ToString("u")),
                    ComplianceState: ShellStatusTextFormatter.BuildComplianceState(shellSurface, state.Preferences))),
            SectionHostState: new SectionHostState(
                Notice: BuildSectionNotice(state, shellSurface),
                PreviewJson: state.ActiveSectionJson ?? string.Empty,
                Rows: state.ActiveSectionRows
                    .Select(row => new SectionRowDisplayItem(row.Path, row.Value))
                    .ToArray(),
                BuildLab: state.ActiveBuildLab,
                BrowseWorkspace: state.ActiveBrowseWorkspace,
                ContactGraph: BuildContactGraph(state),
                DowntimePlanner: BuildDowntimePlanner(state),
                NpcPersonaStudio: state.ActiveNpcPersonaStudio),
            CommandDialogPaneState: ProjectCommandDialogState(state, commands, shellSurface.LastCommandId),
            NavigatorPaneState: new NavigatorPaneState(
                OpenWorkspacesHeading: RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(shellSurface.ActiveRulesetId),
                OpenWorkspaces: ProjectOpenWorkspaces(state, shellSurface),
                SelectedWorkspaceId: shellSurface.ActiveWorkspaceId?.Value,
                NavigationTabsHeading: RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(shellSurface.ActiveRulesetId),
                NavigationTabs: ProjectNavigationTabs(state, shellSurface, commandAvailabilityEvaluator),
                ActiveTabId: shellSurface.ActiveTabId,
                SectionActionsHeading: RulesetUiDirectiveCatalog.BuildSectionActionsHeading(shellSurface.ActiveRulesetId),
                SectionActions: ProjectSectionActions(shellSurface),
                ActiveActionId: state.ActiveActionId,
                WorkflowSurfacesHeading: RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading(shellSurface.ActiveRulesetId),
                WorkflowSurfaces: ProjectWorkflowSurfaces(shellSurface)),
            WorkspaceActionsById: workspaceActionsById);
    }

    private static string BuildSectionNotice(CharacterOverviewState state, ShellSurfaceState shellSurface)
    {
        List<string> lines =
        [
            $"Notice: {(shellSurface.Notice ?? "Ready.")}",
            RulesetUiDirectiveCatalog.BuildSectionNotice(
                rulesetId: shellSurface.ActiveRulesetId,
                sectionId: state.ActiveSectionId,
                actionId: state.ActiveActionId,
                activeRuntime: shellSurface.ActiveRuntime)
        ];

        WorkspacePortabilityActivity? portability = state.LatestPortabilityActivity;
        if (portability is null)
        {
            return string.Join(
                Environment.NewLine,
                lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
        }

        lines.Add($"{portability.Title}: {portability.Receipt.ReceiptSummary}");
        lines.Add($"Context: {portability.Receipt.ContextSummary}");
        lines.Add($"Next safe action: {portability.Receipt.NextSafeAction}");

        if (portability.Receipt.SupportedExchangeModes.Count > 0)
        {
            lines.Add($"Exchange modes: {string.Join(", ", portability.Receipt.SupportedExchangeModes)}");
        }

        string? watchout = portability.Receipt.Notes
            .FirstOrDefault(note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;
        if (!string.IsNullOrWhiteSpace(watchout))
        {
            lines.Add($"Watchout: {watchout}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildToolStripStatusText(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ActiveWorkspaceContext workspaceContext,
        string language)
    {
        if (shellSurface.Error is not null)
        {
            return DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.shell.state.error", language, shellSurface.Error);
        }

        return DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.state.snapshot",
            language,
            DesktopLocalizationCatalog.GetRequiredString(
                state.IsBusy ? "desktop.shell.state.value.busy" : "desktop.shell.state.value.ready",
                language),
            workspaceContext.ActiveWorkspaceId?.Value ?? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language),
            workspaceContext.OpenWorkspaceCount,
            state.HasSavedWorkspace
                ? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.saved", language)
                : DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.unsaved", language),
            shellSurface.LastCommandId ?? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language));
    }

    private static string BuildWorkspaceStripText(ActiveWorkspaceContext workspaceContext, string language)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.workspace_strip.summary",
            language,
            workspaceContext.ActiveWorkspaceId?.Value ?? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language),
            workspaceContext.OpenWorkspaceCount,
            LocalizeSaveStatus(workspaceContext.ActiveWorkspaceSaveStatus, language));

    private static string BuildCharacterStateText(ActiveWorkspaceContext workspaceContext, string language)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.status.character",
            language,
            DesktopLocalizationCatalog.GetRequiredString(
                workspaceContext.ActiveWorkspaceId is null
                    ? "desktop.shell.value.none"
                    : "desktop.shell.state.value.loaded",
                language));

    private static string BuildServiceStateText(ShellSurfaceState shellSurface, string language)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.status.service",
            language,
            DesktopLocalizationCatalog.GetRequiredString(
                shellSurface.Error is null
                    ? "desktop.shell.state.value.online"
                    : "desktop.shell.state.value.error",
                language));

    private static ActiveWorkspaceContext ResolveActiveWorkspaceContext(ShellSurfaceState shellSurface)
    {
        int openWorkspaceCount = shellSurface.OpenWorkspaces.Count;
        CharacterWorkspaceId? activeWorkspaceId = shellSurface.ActiveWorkspaceId;
        OpenWorkspaceState? activeWorkspace = shellSurface.OpenWorkspaces
            .FirstOrDefault(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId?.Value, StringComparison.Ordinal));
        string activeWorkspaceSaveStatus = activeWorkspace is null
            ? "n/a"
            : activeWorkspace.HasSavedWorkspace ? "saved" : "unsaved";
        return new ActiveWorkspaceContext(activeWorkspaceId, openWorkspaceCount, activeWorkspaceSaveStatus);
    }

    private static string LocalizeSaveStatus(string saveStatus, string language)
        => saveStatus switch
        {
            "saved" => DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.saved", language),
            "unsaved" => DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.unsaved", language),
            _ => DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.na", language)
        };

    private static CommandPaletteItem[] ProjectCommands(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        IEnumerable<AppCommandDefinition> visibleCommands = shellSurface.Commands
            .Where(command => !string.Equals(command.Group, "menu", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(shellSurface.OpenMenuId))
        {
            visibleCommands = visibleCommands.Where(command => string.Equals(command.Group, shellSurface.OpenMenuId, StringComparison.Ordinal));
        }

        return visibleCommands
            .Select(command => new CommandPaletteItem(
                command.Id,
                command.Group,
                commandAvailabilityEvaluator.IsCommandEnabled(command, state)))
            .ToArray();
    }

    private static NavigatorWorkspaceItem[] ProjectOpenWorkspaces(CharacterOverviewState state, ShellSurfaceState shellSurface)
    {
        return shellSurface.OpenWorkspaces
            .Select(workspace => new NavigatorWorkspaceItem(
                workspace.Id.Value,
                workspace.Name,
                workspace.Alias,
                workspace.RulesetId,
                workspace.HasSavedWorkspace,
                Enabled: !state.IsBusy))
            .ToArray();
    }

    private static NavigatorTabItem[] ProjectNavigationTabs(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        return shellSurface.NavigationTabs
            .Select(tab => new NavigatorTabItem(
                tab.Id,
                RulesetUiDirectiveCatalog.FormatNavigationTabLabel(tab.RulesetId, tab.Id, tab.Label),
                tab.SectionId,
                tab.Group,
                commandAvailabilityEvaluator.IsNavigationTabEnabled(tab, state)))
            .ToArray();
    }

    private static NavigatorSectionActionItem[] ProjectSectionActions(ShellSurfaceState shellSurface)
    {
        return shellSurface.WorkspaceActions
            .Select(action => new NavigatorSectionActionItem(
                action.Id,
                RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(action.RulesetId, action.Id, action.TargetId, action.Label),
                action.Kind))
            .ToArray();
    }

    private static NavigatorWorkflowSurfaceItem[] ProjectWorkflowSurfaces(ShellSurfaceState shellSurface)
    {
        return shellSurface.ActiveWorkflowSurfaceActions
            .Select(surface => new NavigatorWorkflowSurfaceItem(
                surface.SurfaceId,
                surface.WorkflowId,
                RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel(shellSurface.ActiveRulesetId, surface.ActionId, surface.Label),
                surface.ActionId))
            .ToArray();
    }

    private static CommandDialogPaneState ProjectCommandDialogState(
        CharacterOverviewState state,
        CommandPaletteItem[] commands,
        string? lastCommandId)
    {
        if (state.ActiveDialog is null)
        {
            return new CommandDialogPaneState(
                Commands: commands,
                SelectedCommandId: lastCommandId,
                DialogTitle: null,
                DialogMessage: null,
                Fields: Array.Empty<DialogFieldDisplayItem>(),
                Actions: Array.Empty<DialogActionDisplayItem>());
        }

        DialogFieldDisplayItem[] fields = state.ActiveDialog.Fields
            .Select(field => new DialogFieldDisplayItem(field.Id, field.Label, field.Value))
            .ToArray();
        DialogActionDisplayItem[] actions = state.ActiveDialog.Actions
            .Select(action => new DialogActionDisplayItem(action.Id, action.Label, action.IsPrimary))
            .ToArray();
        return new CommandDialogPaneState(
            Commands: commands,
            SelectedCommandId: lastCommandId,
            DialogTitle: state.ActiveDialog.Title,
            DialogMessage: state.ActiveDialog.Message,
            Fields: fields,
            Actions: actions);
    }

    private static IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> BuildWorkspaceActionLookup(
        IReadOnlyList<WorkspaceSurfaceActionDefinition> workspaceActions)
    {
        var lookup = new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal);
        foreach (WorkspaceSurfaceActionDefinition action in workspaceActions)
        {
            lookup[action.Id] = action;
        }

        return lookup;
    }

    private static ContactRelationshipGraphState? BuildContactGraph(CharacterOverviewState state)
    {
        if (!string.Equals(state.ActiveSectionId, "contacts", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(state.ActiveSectionJson))
        {
            return null;
        }

        try
        {
            CharacterContactsSection? contacts = JsonSerializer.Deserialize<CharacterContactsSection>(state.ActiveSectionJson);
            return ContactRelationshipGraphProjector.FromContacts(contacts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DowntimePlannerState? BuildDowntimePlanner(CharacterOverviewState state)
    {
        if (string.IsNullOrWhiteSpace(state.ActiveSectionId)
            || state.ActiveSectionId.IndexOf("journal", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(state.ActiveSectionJson))
        {
            return null;
        }

        try
        {
            JournalPanelProjection? journal = JsonSerializer.Deserialize<JournalPanelProjection>(state.ActiveSectionJson);
            return DowntimePlannerProjector.FromJournal(journal);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ActiveWorkspaceContext(
        CharacterWorkspaceId? ActiveWorkspaceId,
        int OpenWorkspaceCount,
        string ActiveWorkspaceSaveStatus);
}

internal sealed record MainWindowShellFrame(
    MainWindowHeaderState HeaderState,
    MainWindowChromeState ChromeState,
    SectionHostState SectionHostState,
    CommandDialogPaneState CommandDialogPaneState,
    NavigatorPaneState NavigatorPaneState,
    IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> WorkspaceActionsById);

internal sealed record MainWindowHeaderState(
    ToolStripState ToolStrip,
    MenuBarState MenuBar);

internal sealed record MainWindowChromeState(
    WorkspaceStripState WorkspaceStrip,
    SummaryHeaderState SummaryHeader,
    StatusStripState StatusStrip);
