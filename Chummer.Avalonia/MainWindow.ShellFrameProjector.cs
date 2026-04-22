using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Presentation.UiKit;
using System.Text.Json;

namespace Chummer.Avalonia;

internal static class MainWindowShellFrameProjector
{
    private static readonly CatalogOnlyRulesetShellCatalogResolver CompatibilityShellCatalogResolver = new();

    public static MainWindowShellFrame Project(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        string language = DesktopLocalizationCatalog.NormalizeOrDefault(state.Preferences.Language);
        OpenWorkspaceState[] resolvedOpenWorkspaces = ResolveOpenWorkspaces(state, shellSurface);
        ActiveWorkspaceContext workspaceContext = ResolveActiveWorkspaceContext(state, shellSurface, resolvedOpenWorkspaces);
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById = BuildWorkspaceActionLookup(shellSurface.WorkspaceActions);
        CommandPaletteItem[] commands = ProjectCommands(state, shellSurface, commandAvailabilityEvaluator);
        NavigatorTabItem[] navigationTabs = ProjectNavigationTabs(state, shellSurface, commandAvailabilityEvaluator);

        return new MainWindowShellFrame(
            HeaderState: new MainWindowHeaderState(
                ToolStrip: new ToolStripState(
                    BuildToolStripStatusText(state, shellSurface, workspaceContext, language)),
                MenuBar: new MenuBarState(
                    OpenMenuId: shellSurface.OpenMenuId,
                    KnownMenuIds: shellSurface.MenuRoots.Select(menu => menu.Id).ToArray(),
                    OpenMenuCommands: ProjectMenuCommands(state, shellSurface, commandAvailabilityEvaluator),
                    MenuCommandsByMenuId: ProjectMenuCommandGroups(state, shellSurface, commandAvailabilityEvaluator),
                    IsBusy: state.IsBusy)),
            ChromeState: new MainWindowChromeState(
                WorkspaceStrip: new WorkspaceStripState(
                    BuildWorkspaceStripText(workspaceContext, language),
                    ShowQuickStartAction: false),
                SummaryHeader: new SummaryHeaderState(
                    NavigationTabsHeading: RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(shellSurface.ActiveRulesetId),
                    NavigationTabs: navigationTabs,
                    ActiveTabId: shellSurface.ActiveTabId,
                    RuntimeSummary: ShellStatusTextFormatter.BuildActiveRuntimeSummary(shellSurface.ActiveRuntime, shellSurface.ActiveRulesetId),
                    RestoreContinuitySummary: BuildRestoreContinuitySummary(workspaceContext, language),
                    StaleStateSummary: BuildStaleStateSummary(shellSurface, workspaceContext, language),
                    ConflictChoiceSummary: BuildConflictChoiceSummary(workspaceContext, language),
                    CanSaveLocalWorkBeforeRestore: CanSaveLocalWorkBeforeRestore(workspaceContext)),
                StatusStrip: new StatusStripState(
                    CharacterState: BuildCharacterStateText(workspaceContext, language),
                    ServiceState: BuildServiceStateText(shellSurface, language),
                    TimeState: DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.shell.status.time", language, DateTimeOffset.UtcNow.ToString("u")),
                    ComplianceState: ShellStatusTextFormatter.BuildComplianceState(shellSurface, state.Preferences))),
            SectionHostState: new SectionHostState(
                SectionId: state.ActiveSectionId,
                Notice: BuildSectionNotice(state, shellSurface),
                PreviewJson: state.ActiveSectionJson ?? string.Empty,
                Rows: state.ActiveSectionRows
                    .Select(row => new SectionRowDisplayItem(row.Path, row.Value))
                    .ToArray(),
                QuickActions: ProjectSectionQuickActions(shellSurface.ActiveRulesetId, state.ActiveSectionId),
                BuildLab: state.ActiveBuildLab,
                BrowseWorkspace: state.ActiveBrowseWorkspace,
                ContactGraph: BuildContactGraph(state),
                DowntimePlanner: BuildDowntimePlanner(state),
                NpcPersonaStudio: state.ActiveNpcPersonaStudio),
            CommandDialogPaneState: ProjectCommandDialogState(state, commands, shellSurface.LastCommandId),
            ShowNavigatorPane: resolvedOpenWorkspaces.Length > 1,
            NavigatorPaneState: new NavigatorPaneState(
                OpenWorkspacesHeading: RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(shellSurface.ActiveRulesetId),
                OpenWorkspaces: ProjectOpenWorkspaces(state, shellSurface),
                SelectedWorkspaceId: shellSurface.ActiveWorkspaceId?.Value,
                NavigationTabsHeading: RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(shellSurface.ActiveRulesetId),
                NavigationTabs: navigationTabs,
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
        List<string> lines = [];

        string? shellNotice = string.IsNullOrWhiteSpace(shellSurface.Notice)
            ? null
            : shellSurface.Notice.Trim();
        if (!string.IsNullOrWhiteSpace(shellNotice)
            && !IsRoutineShellNotice(shellNotice))
        {
            lines.Add($"Notice: {shellNotice}");
        }

        WorkspacePortabilityActivity? portability = state.LatestPortabilityActivity;
        if (portability is null)
        {
            return string.Join(Environment.NewLine, lines);
        }

        string? watchout = portability.Receipt.Notes
            .FirstOrDefault(note => !string.Equals(note.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase))
            ?.Summary;
        if (!string.IsNullOrWhiteSpace(watchout))
        {
            lines.Add($"Import watchout: {watchout}");
        }

        lines.Add($"Import rule environment: {DesktopTrustReceiptText.BuildImportRuleEnvironment(portability.Receipt)}");
        lines.Add($"Import environment before: {DesktopTrustReceiptText.BuildImportDiffBefore(portability.Receipt)}");
        lines.Add($"Import environment after: {DesktopTrustReceiptText.BuildImportDiffAfter(portability.Receipt)}");
        lines.Add($"Import explain receipt: {DesktopTrustReceiptText.BuildImportExplainReceipt(portability.Receipt)}");
        lines.Add($"Support reuse: {DesktopTrustReceiptText.BuildImportSupportReuse(portability.Receipt)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsRoutineShellNotice(string shellNotice)
    {
        if (string.Equals(shellNotice, "Ready.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return shellNotice.StartsWith("Command '", StringComparison.OrdinalIgnoreCase)
            && shellNotice.EndsWith("dispatched.", StringComparison.OrdinalIgnoreCase);
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

    private static string? BuildRestoreContinuitySummary(ActiveWorkspaceContext workspaceContext, string language)
    {
        return null;
    }

    private static string? BuildStaleStateSummary(
        ShellSurfaceState shellSurface,
        ActiveWorkspaceContext workspaceContext,
        string language)
    {
        return null;
    }

    private static string? BuildConflictChoiceSummary(ActiveWorkspaceContext workspaceContext, string language) => null;

    private static bool CanSaveLocalWorkBeforeRestore(ActiveWorkspaceContext workspaceContext)
        => string.Equals(workspaceContext.ActiveWorkspaceSaveStatus, "unsaved", StringComparison.Ordinal);

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

    private static ActiveWorkspaceContext ResolveActiveWorkspaceContext(
        CharacterOverviewState overviewState,
        ShellSurfaceState shellSurface,
        IReadOnlyList<OpenWorkspaceState> openWorkspaces)
    {
        int openWorkspaceCount = openWorkspaces.Count;
        CharacterWorkspaceId? activeWorkspaceId = shellSurface.ActiveWorkspaceId;
        OpenWorkspaceState? activeWorkspace = openWorkspaces
            .FirstOrDefault(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId?.Value, StringComparison.Ordinal));
        string activeWorkspaceSaveStatus = activeWorkspace is null
            ? "n/a"
            : activeWorkspace.HasSavedWorkspace ? "saved" : "unsaved";
        return new ActiveWorkspaceContext(
            activeWorkspaceId,
            openWorkspaceCount,
            activeWorkspaceSaveStatus,
            activeWorkspace?.LastOpenedUtc);
    }

    private static string BuildWorkspacePresenceReceipt(ActiveWorkspaceContext workspaceContext)
    {
        if (workspaceContext.ActiveWorkspaceId is null)
        {
            return workspaceContext.OpenWorkspaceCount > 0
                ? $"Primary desktop head still has {workspaceContext.OpenWorkspaceCount} open workspace tab(s) available for review."
                : "Primary desktop head has no active workspace yet, so restore review stays on the current local shell until you pick one.";
        }

        return $"{workspaceContext.ActiveWorkspaceId.Value} stays visible on the current desktop head until you choose review or support.";
    }

    private static string BuildWorkspaceTimestampReceipt(ActiveWorkspaceContext workspaceContext)
    {
        string workspaceLabel = workspaceContext.ActiveWorkspaceId?.Value ?? "no active workspace";
        return workspaceContext.ActiveWorkspaceLastSeenUtc is DateTimeOffset lastSeenUtc
            ? $"{workspaceLabel} was last touched locally at {lastSeenUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC and stays visible before any replacement;"
            : $"{workspaceLabel} stays visible on the current desktop head before any replacement;";
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
                ShellChromeBoundary.FormatCommandLabel(command.Id),
                command.Group,
                commandAvailabilityEvaluator.IsCommandEnabled(command, state)))
            .ToArray();
    }

    private static MenuCommandItem[] ProjectMenuCommands(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        if (string.IsNullOrWhiteSpace(shellSurface.OpenMenuId))
        {
            return Array.Empty<MenuCommandItem>();
        }

        return ResolveMenuCommandsForGroup(shellSurface, shellSurface.OpenMenuId)
            .Select(command => new MenuCommandItem(
                command.Id,
                ShellChromeBoundary.FormatCommandLabel(command.Id),
                commandAvailabilityEvaluator.IsCommandEnabled(command, state),
                IsPrimary: string.Equals(command.Id, "open_character", StringComparison.Ordinal)
                    || string.Equals(command.Id, "save_character", StringComparison.Ordinal)))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MenuCommandItem>> ProjectMenuCommandGroups(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        return shellSurface.MenuRoots
            .Select(menu => menu.Id)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                menuId => menuId,
                menuId => (IReadOnlyList<MenuCommandItem>)ResolveMenuCommandsForGroup(shellSurface, menuId)
                    .Select(command => new MenuCommandItem(
                        command.Id,
                        ShellChromeBoundary.FormatCommandLabel(command.Id),
                        commandAvailabilityEvaluator.IsCommandEnabled(command, state),
                        IsPrimary: string.Equals(command.Id, "open_character", StringComparison.Ordinal)
                            || string.Equals(command.Id, "save_character", StringComparison.Ordinal)))
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<AppCommandDefinition> ResolveMenuCommandsForGroup(
        ShellSurfaceState shellSurface,
        string menuId)
    {
        AppCommandDefinition[] runtimeCommands = shellSurface.Commands
            .Where(command => string.Equals(command.Group, menuId, StringComparison.Ordinal))
            .ToArray();
        if (runtimeCommands.Length > 0)
        {
            return runtimeCommands;
        }

        return CompatibilityShellCatalogResolver.ResolveCommands(shellSurface.ActiveRulesetId)
            .Where(command => string.Equals(command.Group, menuId, StringComparison.Ordinal))
            .ToArray();
    }

    private static NavigatorWorkspaceItem[] ProjectOpenWorkspaces(CharacterOverviewState state, ShellSurfaceState shellSurface)
    {
        return ResolveOpenWorkspaces(state, shellSurface)
            .Select(workspace => new NavigatorWorkspaceItem(
                workspace.Id.Value,
                workspace.Name,
                workspace.Alias,
                workspace.RulesetId,
                workspace.HasSavedWorkspace,
                Enabled: !state.IsBusy))
            .ToArray();
    }

    private static SectionQuickActionDisplayItem[] ProjectSectionQuickActions(string? rulesetId, string? sectionId)
    {
        return SectionQuickActionCatalog.ForSection(rulesetId, sectionId)
            .Select(action => new SectionQuickActionDisplayItem(action.ControlId, action.Label, action.IsPrimary))
            .ToArray();
    }

    private static OpenWorkspaceState[] ResolveOpenWorkspaces(CharacterOverviewState overviewState, ShellSurfaceState shellSurface)
    {
        if (shellSurface.OpenWorkspaces.Count > 0)
        {
            return shellSurface.OpenWorkspaces.ToArray();
        }

        IReadOnlyList<OpenWorkspaceState> overviewOpenWorkspaces = overviewState.Session.OpenWorkspaces.Count > 0
            ? overviewState.Session.OpenWorkspaces
            : overviewState.OpenWorkspaces;
        return overviewOpenWorkspaces.ToArray();
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
            .Select(field => new DialogFieldDisplayItem(
                field.Id,
                field.Label,
                field.Value,
                field.Placeholder,
                field.IsMultiline,
                field.IsReadOnly,
                field.InputType,
                field.VisualKind,
                field.LayoutSlot))
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
        string ActiveWorkspaceSaveStatus,
        DateTimeOffset? ActiveWorkspaceLastSeenUtc);
}

internal sealed record MainWindowShellFrame(
    MainWindowHeaderState HeaderState,
    MainWindowChromeState ChromeState,
    SectionHostState SectionHostState,
    CommandDialogPaneState CommandDialogPaneState,
    bool ShowNavigatorPane,
    NavigatorPaneState NavigatorPaneState,
    IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> WorkspaceActionsById);

internal sealed record MainWindowHeaderState(
    ToolStripState ToolStrip,
    MenuBarState MenuBar);

internal sealed record MainWindowChromeState(
    WorkspaceStripState WorkspaceStrip,
    SummaryHeaderState SummaryHeader,
    StatusStripState StatusStrip);
