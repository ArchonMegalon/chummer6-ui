using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Presentation.UiKit;
using System.Reflection;
using System.Text.Json;

namespace Chummer.Avalonia;

internal static class MainWindowShellFrameProjector
{
    private const string ReleaseChannelEnvironmentVariable = "CHUMMER_DESKTOP_RELEASE_CHANNEL";
    private const string SampleControlsEnvironmentVariable = "CHUMMER_DESKTOP_ENABLE_SAMPLES";

    public static MainWindowShellFrame Project(
        CharacterOverviewState state,
        ShellSurfaceState shellSurface,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator)
    {
        string language = DesktopLocalizationCatalog.NormalizeOrDefault(state.Preferences.Language);
        OpenWorkspaceState[] resolvedOpenWorkspaces = ResolveOpenWorkspaces(state, shellSurface);
        ActiveWorkspaceContext workspaceContext = ResolveActiveWorkspaceContext(state, shellSurface, resolvedOpenWorkspaces);
        bool hasOpenWorkspace = workspaceContext.ActiveWorkspaceId is not null || workspaceContext.OpenWorkspaceCount > 0;
        bool hasProjectedSectionSurface =
            hasOpenWorkspace
            || !string.IsNullOrWhiteSpace(state.ActiveSectionId)
            || !string.IsNullOrWhiteSpace(state.ActiveActionId)
            || !string.IsNullOrWhiteSpace(state.ActiveSectionJson)
            || state.ActiveSectionRows.Count > 0
            || state.ActiveBuildLab is not null
            || state.ActiveBrowseWorkspace is not null
            || state.ActiveNpcPersonaStudio is not null;
        bool showSampleControls = ShouldShowSampleControls();
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById = BuildWorkspaceActionLookup(shellSurface.WorkspaceActions);
        bool showAiFeatures = !state.Preferences.DisableAiFeatures;
        CommandPaletteItem[] commands = ProjectCommands(state, shellSurface, commandAvailabilityEvaluator);
        NavigatorTabItem[] navigationTabs = ProjectNavigationTabs(state, shellSurface, commandAvailabilityEvaluator);

        return new MainWindowShellFrame(
            HeaderState: new MainWindowHeaderState(
                ToolStrip: new ToolStripState(
                    BuildToolStripStatusText(state, shellSurface, workspaceContext, language),
                    ShowOpenForExport: true,
                    ShowGmPrep: showSampleControls,
                    ShowRosterMovement: showSampleControls,
                    ShowCampaignWorkspace: false,
                    ShowLoadDemoRunner: !hasOpenWorkspace && showSampleControls,
                    ShowAiFeatures: showAiFeatures),
                MenuBar: new MenuBarState(
                    OpenMenuId: shellSurface.OpenMenuId,
                    KnownMenuIds: shellSurface.MenuRoots.Select(menu => menu.Id).ToArray(),
                    OpenMenuCommands: ProjectMenuCommands(state, shellSurface, commandAvailabilityEvaluator),
                    MenuCommandsByMenuId: ProjectMenuCommandGroups(state, shellSurface, commandAvailabilityEvaluator),
                    IsBusy: state.IsBusy)),
            ChromeState: new MainWindowChromeState(
                WorkspaceStrip: new WorkspaceStripState(
                    BuildWorkspaceStripText(workspaceContext, language),
                    ShowQuickStartAction: !hasOpenWorkspace && showSampleControls,
                    ShowOriginDossierAction: !hasOpenWorkspace && showAiFeatures),
                SummaryHeader: new SummaryHeaderState(
                    NavigationTabsHeading: RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(shellSurface.ActiveRulesetId),
                    NavigationTabs: navigationTabs,
                    ActiveTabId: shellSurface.ActiveTabId,
                    HasVisibleContent: false,
                    RuntimeSummary: ShellStatusTextFormatter.BuildActiveRuntimeSummary(shellSurface.ActiveRuntime, shellSurface.ActiveRulesetId),
                    RestoreDecisionWorkspaceId: workspaceContext.ActiveWorkspaceId?.Value),
                StatusStrip: new StatusStripState(
                    CharacterState: BuildCharacterStateText(workspaceContext, language),
                    ServiceState: BuildServiceStateText(shellSurface, language),
                    TimeState: DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.shell.status.time", language, DateTimeOffset.UtcNow.ToString("u")),
                    ComplianceState: ShellStatusTextFormatter.BuildComplianceState(shellSurface, state.Preferences))
                {
                    IsBusy = state.IsBusy || shellSurface.IsBusy
                }),
            SectionHostState: new SectionHostState(
                SectionId: hasProjectedSectionSurface ? state.ActiveSectionId : null,
                NavigationTabs: navigationTabs,
                ActiveTabId: shellSurface.ActiveTabId,
                SectionActions: ProjectSectionActions(shellSurface),
                ActiveActionId: state.ActiveActionId,
                Notice: BuildSectionNotice(state, shellSurface),
                PreviewJson: hasProjectedSectionSurface ? state.ActiveSectionJson ?? string.Empty : string.Empty,
                Rows: hasProjectedSectionSurface
                    ? state.ActiveSectionRows
                        .Select(row => new SectionRowDisplayItem(row.Path, row.Value))
                        .ToArray()
                    : Array.Empty<SectionRowDisplayItem>(),
                QuickActions: hasProjectedSectionSurface
                    ? ProjectSectionQuickActions(shellSurface.ActiveRulesetId, state.ActiveSectionId)
                    : Array.Empty<SectionQuickActionDisplayItem>(),
                BuildLab: state.ActiveBuildLab,
                BrowseWorkspace: state.ActiveBrowseWorkspace,
                ContactGraph: BuildContactGraph(state),
                DowntimePlanner: BuildDowntimePlanner(state),
                NpcPersonaStudio: state.ActiveNpcPersonaStudio,
                RulesetId: shellSurface.ActiveRulesetId),
            RosterPaneState: new RosterPaneState(
                Items: CharacterRosterDataBinder.CreateRosterNodes(resolvedOpenWorkspaces).ToArray(),
                SelectedWorkspaceId: workspaceContext.ActiveWorkspaceId?.Value),
            CommandDialogPaneState: ProjectCommandDialogState(
                state,
                commands,
                SanitizeLastCommandIdForPreferences(shellSurface.LastCommandId, state.Preferences)),
            ShowNavigatorPane: false,
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

    private static bool ShouldShowSampleControls()
    {
        if (IsEnabledEnvironmentFlag(SampleControlsEnvironmentVariable))
        {
            return true;
        }

        if (string.Equals(ResolveReleaseChannel(), "public_stable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ClassicModePolicy.ShouldShowSampleControls())
        {
            return true;
        }

        if (ClassicModePolicy.IsClassicDefault())
        {
            return false;
        }

        return !string.Equals(ResolveReleaseChannel(), "public_stable", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReleaseChannel()
    {
        string? overrideChannel = Environment.GetEnvironmentVariable(ReleaseChannelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideChannel))
        {
            return overrideChannel.Trim();
        }

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "ChummerDesktopReleaseChannel", StringComparison.Ordinal))?
            .Value?
            .Trim()
            ?? "local";
    }

    private static bool IsEnabledEnvironmentFlag(string variableName)
    {
        string? raw = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = raw.Trim();
        return normalized switch
        {
            "1" => true,
            _ when string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
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
            lines.Add(shellNotice);
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
            lines.Add($"Import needs review: {watchout.Trim()}");
        }
        else if (string.Equals(portability.Receipt.CompatibilityState, WorkspacePortabilityCompatibilityStates.Compatible, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Import ready. Review the character, then keep or discard the changes.");
        }
        else
        {
            lines.Add("Import needs review before it can change this character.");
        }

        lines.Add("Nothing changes until you accept the import.");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsRoutineShellNotice(string shellNotice)
    {
        if (string.Equals(shellNotice, "Ready.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (shellNotice.StartsWith("Restored ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (shellNotice.StartsWith("Menu ", StringComparison.OrdinalIgnoreCase)
            && (shellNotice.EndsWith(" opened.", StringComparison.OrdinalIgnoreCase)
                || shellNotice.EndsWith(" opened", StringComparison.OrdinalIgnoreCase)))
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
            DescribeWorkspaceForChrome(state, workspaceContext, language),
            workspaceContext.OpenWorkspaceCount,
            state.HasSavedWorkspace
                ? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.saved", language)
                : DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.unsaved", language),
            FormatCommandLabel(SanitizeLastCommandIdForStatus(shellSurface), language));
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

    private static string DescribeWorkspaceForChrome(
        CharacterOverviewState state,
        ActiveWorkspaceContext workspaceContext,
        string language)
    {
        if (!string.IsNullOrWhiteSpace(state.Profile?.Name))
        {
            return state.Profile!.Name!;
        }

        return workspaceContext.ActiveWorkspaceId?.Value is { Length: > 8 } workspaceId
            ? workspaceId[..8]
            : workspaceContext.ActiveWorkspaceId?.Value ?? DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language);
    }

    private static string FormatCommandLabel(string? commandId, string language)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language);
        }

        return string.Join(
            ' ',
            commandId
                .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }

    private static string? SanitizeLastCommandIdForStatus(ShellSurfaceState shellSurface)
    {
        if (string.IsNullOrWhiteSpace(shellSurface.LastCommandId))
        {
            return null;
        }

        if (shellSurface.MenuRoots.Any(root => string.Equals(root.Id, shellSurface.LastCommandId, StringComparison.Ordinal)))
        {
            return null;
        }

        foreach (AppCommandDefinition command in shellSurface.Commands)
        {
            if (string.Equals(command.Id, shellSurface.LastCommandId, StringComparison.Ordinal)
                && string.Equals(command.Group, "menu", StringComparison.Ordinal))
            {
                return null;
            }
        }

        return shellSurface.LastCommandId;
    }

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
                ? $"{workspaceContext.OpenWorkspaceCount} workspace tab(s) are open for review."
                : "No workspace is active yet. The current local view stays in place until you pick one.";
        }

        return $"{workspaceContext.ActiveWorkspaceId.Value} stays visible until you choose review or support.";
    }

    private static string BuildWorkspaceTimestampReceipt(ActiveWorkspaceContext workspaceContext)
    {
        string workspaceLabel = workspaceContext.ActiveWorkspaceId?.Value ?? "no active workspace";
        return workspaceContext.ActiveWorkspaceLastSeenUtc is DateTimeOffset lastSeenUtc
            ? $"{workspaceLabel} was last touched locally at {lastSeenUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC and stays visible before any replacement;"
            : $"{workspaceLabel} stays visible before any replacement;";
    }

    private static bool HasRestoreReviewContext(
        ShellSurfaceState shellSurface,
        ActiveWorkspaceContext workspaceContext)
        => workspaceContext.ActiveWorkspaceId is not null
            || workspaceContext.OpenWorkspaceCount > 0
            || (!string.IsNullOrWhiteSpace(shellSurface.Notice)
                && shellSurface.Notice.StartsWith("Restored ", StringComparison.OrdinalIgnoreCase));

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
        IEnumerable<AppCommandDefinition> visibleCommands;
        if (!string.IsNullOrWhiteSpace(shellSurface.OpenMenuId))
        {
            visibleCommands = ResolveMenuCommandsForGroup(shellSurface, shellSurface.OpenMenuId)
                .Where(command => IsStateVisibleMenuCommand(state, shellSurface.OpenMenuId, command.Id));
        }
        else
        {
            visibleCommands = shellSurface.Commands
                .Where(command => !string.Equals(command.Group, "menu", StringComparison.Ordinal));
        }

        if (state.Preferences.DisableAiFeatures)
        {
            visibleCommands = visibleCommands.Where(command => !OverviewCommandPolicy.IsAiFeatureCommand(command.Id));
        }

        return visibleCommands
            .Select(command => new CommandPaletteItem(
                command.Id,
                ShellChromeBoundary.FormatCommandLabel(command.Id),
                GetProjectedMenuGroupId(command),
                commandAvailabilityEvaluator.IsCommandEnabled(command, state)))
            .ToArray();
    }

    private static string? SanitizeLastCommandIdForPreferences(string? commandId, DesktopPreferenceState preferences)
        => !string.IsNullOrWhiteSpace(commandId)
        && OverviewCommandPolicy.IsBlockedByAiFeaturePreference(commandId, preferences)
            ? null
            : commandId;

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
            .Where(command => IsStateVisibleMenuCommand(state, shellSurface.OpenMenuId, command.Id))
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
                    .Where(command => IsStateVisibleMenuCommand(state, menuId, command.Id))
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
        => DesktopMenuProjectionCatalog.ResolveVisibleMenuCommands(
            shellSurface.ActiveRulesetId,
            shellSurface.Commands,
            menuId);

    private static bool IsVisibleMenuCommand(string menuId, string commandId)
        => DesktopMenuProjectionCatalog.IsVisibleMenuCommand(menuId, commandId);

    private static bool IsStateVisibleMenuCommand(CharacterOverviewState state, string menuId, string commandId)
    {
        if (!IsVisibleMenuCommand(menuId, commandId))
        {
            return false;
        }

        if (OverviewCommandPolicy.IsBlockedByAiFeaturePreference(commandId, state.Preferences))
        {
            return false;
        }

        if (ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic
            && string.Equals(commandId, "xml_editor", StringComparison.Ordinal))
        {
            return false;
        }

        return !(string.Equals(commandId, "master_index", StringComparison.Ordinal)
            && state.Preferences.HideMasterIndex);
    }

    private static string GetProjectedMenuGroupId(AppCommandDefinition command)
        => DesktopMenuProjectionCatalog.ResolveProjectedMenuGroupId(command);

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
        return shellSurface.OpenWorkspaces.ToArray();
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
        if (string.Equals(shellSurface.ActiveRulesetId, "sr5", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

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
        if (state.ActiveDialog is null || IsActiveDialogBlockedByPreferences(state.ActiveDialog, state.Preferences))
        {
            return new CommandDialogPaneState(
                Commands: commands,
                SelectedCommandId: lastCommandId,
                ActiveDialogId: null,
                DialogTitle: null,
                DialogMessage: null,
                DialogTrustReceipt: null,
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
                field.Options?.Select(option => new DialogFieldOptionDisplayItem(option.Value, option.Label)).ToArray(),
                field.VisualKind,
                field.LayoutSlot))
            .ToArray();
        DialogActionDisplayItem[] actions = state.ActiveDialog.Actions
            .Select(action => new DialogActionDisplayItem(action.Id, action.Label, action.IsPrimary))
            .ToArray();
        return new CommandDialogPaneState(
            Commands: commands,
            SelectedCommandId: lastCommandId,
            ActiveDialogId: state.ActiveDialog.Id,
            DialogTitle: state.ActiveDialog.Title,
            DialogMessage: state.ActiveDialog.Message,
            DialogTrustReceipt: DesktopTrustReceiptText.BuildDialogReceipt(state.ActiveDialog),
            Fields: fields,
            Actions: actions);
    }

    private static bool IsActiveDialogBlockedByPreferences(DesktopDialogState dialog, DesktopPreferenceState preferences)
        => preferences.DisableAiFeatures
            && (string.Equals(dialog.Id, DesktopAliceAssistant.DialogId, StringComparison.Ordinal)
                || dialog.Id.StartsWith("dialog.new_character.origin_", StringComparison.Ordinal));

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
        if (!string.Equals(state.ActiveSectionId, "relationships", StringComparison.OrdinalIgnoreCase))
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
    RosterPaneState RosterPaneState,
    CommandDialogPaneState CommandDialogPaneState,
    bool ShowNavigatorPane,
    NavigatorPaneState NavigatorPaneState,
    IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> WorkspaceActionsById)
{
    internal MainWindowShellFrame(
        MainWindowHeaderState headerState,
        MainWindowChromeState chromeState,
        SectionHostState sectionHostState,
        CommandDialogPaneState commandDialogPaneState,
        NavigatorPaneState navigatorPaneState,
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById)
        : this(
            headerState,
            chromeState,
            sectionHostState,
            new RosterPaneState(Array.Empty<CharacterRosterNode>(), null),
            commandDialogPaneState,
            true,
            navigatorPaneState,
            workspaceActionsById)
    {
    }

    internal MainWindowShellFrame(
        MainWindowHeaderState headerState,
        MainWindowChromeState chromeState,
        SectionHostState sectionHostState,
        RosterPaneState rosterPaneState,
        CommandDialogPaneState commandDialogPaneState,
        NavigatorPaneState navigatorPaneState,
        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> workspaceActionsById)
        : this(
            headerState,
            chromeState,
            sectionHostState,
            rosterPaneState,
            commandDialogPaneState,
            true,
            navigatorPaneState,
            workspaceActionsById)
    {
    }

    internal MainWindowShellFrame(
        MainWindowHeaderState headerState,
        MainWindowChromeState chromeState,
        SectionHostState sectionHostState,
        RosterPaneState rosterPaneState,
        CommandDialogPaneState commandDialogPaneState,
        bool showNavigatorPane,
        NavigatorPaneState navigatorPaneState)
        : this(
            headerState,
            chromeState,
            sectionHostState,
            rosterPaneState,
            commandDialogPaneState,
            showNavigatorPane,
            navigatorPaneState,
            new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal))
    {
    }
}

internal sealed record RosterPaneState(
    CharacterRosterNode[] Items,
    string? SelectedWorkspaceId);

internal sealed record MainWindowHeaderState(
    ToolStripState ToolStrip,
    MenuBarState MenuBar);

internal sealed record MainWindowChromeState(
    WorkspaceStripState WorkspaceStrip,
    SummaryHeaderState SummaryHeader,
    StatusStripState StatusStrip);
