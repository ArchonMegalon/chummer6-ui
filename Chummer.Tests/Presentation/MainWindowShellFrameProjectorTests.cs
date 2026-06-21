#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Avalonia;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class MainWindowShellFrameProjectorTests
{
    [TestMethod]
    public void Project_projects_standard_section_quick_actions_into_section_host_state()
    {
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "inventory",
            activeTabId: "tab-gear");

        CollectionAssert.AreEqual(
            StandardInventoryQuickActionControlIds,
            frame.SectionHostState.QuickActions.Select(action => action.ControlId).ToArray());
        CollectionAssert.AreEqual(
            StandardInventoryQuickActionLabels,
            frame.SectionHostState.QuickActions.Select(action => action.Label).ToArray());
        Assert.AreEqual("inventory", frame.SectionHostState.SectionId);
        Assert.IsTrue(frame.SectionHostState.QuickActions[0].IsPrimary);
    }

    [TestMethod]
    public void Project_hides_unbacked_section_quick_actions()
    {
        MainWindowShellFrame sr6Frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "summary",
            activeTabId: "tab-info");
        MainWindowShellFrame sr5Frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "summary",
            activeTabId: "tab-info");

        Assert.IsEmpty(sr6Frame.SectionHostState.QuickActions);
        Assert.IsEmpty(sr5Frame.SectionHostState.QuickActions);
    }

    [TestMethod]
    public void Project_projects_runtime_backed_magic_and_aug_section_quick_actions()
    {
        foreach ((string sectionId, string expectedControlId, string expectedLabel) in RuntimeBackedSectionQuickActions)
        {
            MainWindowShellFrame frame = ProjectFrame(
                RulesetDefaults.Sr6,
                activeSectionId: sectionId,
                activeTabId: "tab-magic");

            Assert.AreEqual(1, frame.SectionHostState.QuickActions.Length, $"Expected one quick action for '{sectionId}'.");
            Assert.AreEqual(expectedControlId, frame.SectionHostState.QuickActions[0].ControlId);
            Assert.AreEqual(expectedLabel, frame.SectionHostState.QuickActions[0].Label);
            Assert.IsTrue(frame.SectionHostState.QuickActions[0].IsPrimary);
        }
    }

    [TestMethod]
    public void Project_projects_progress_section_quick_action_into_section_host_state()
    {
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "progress",
            activeTabId: "tab-info");

        Assert.AreEqual(1, frame.SectionHostState.QuickActions.Length);
        Assert.AreEqual("create_entry", frame.SectionHostState.QuickActions[0].ControlId);
        Assert.AreEqual("Add Entry", frame.SectionHostState.QuickActions[0].Label);
        Assert.IsTrue(frame.SectionHostState.QuickActions[0].IsPrimary);
    }

    [TestMethod]
    public void Project_hides_ai_feature_entry_points_when_global_ai_features_are_disabled()
    {
        AppCommandDefinition[] commands =
        [
            new AppCommandDefinition("tools", "menu.tools", "menu", false, true, RulesetDefaults.Sr5),
            new AppCommandDefinition(DesktopAliceAssistant.CommandId, "command.auto_alice", "tools", false, true, RulesetDefaults.Sr5),
            new AppCommandDefinition("new_character_origin", "command.new_character_origin", "tools", false, true, RulesetDefaults.Sr5),
            new AppCommandDefinition("global_settings", "command.global_settings", "tools", false, true, RulesetDefaults.Sr5)
        ];

        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "summary",
            activeTabId: "tab-info",
            preferences: DesktopPreferenceState.Default with { DisableAiFeatures = true },
            commands: commands,
            menuRoots: [commands[0]],
            openMenuId: "tools",
            activeDialog: new DesktopDialogState(
                DesktopAliceAssistant.DialogId,
                "Alice",
                "Stale assistant dialog from before the preference changed.",
                [new DesktopDialogField("autoAliceConversationMode", "Mode", "Build help", "Build help")],
                [new DesktopDialogAction(DesktopAliceAssistant.PreviewActionId, "Preview", true)]),
            lastCommandId: DesktopAliceAssistant.CommandId);

        string[] commandIds = frame.CommandDialogPaneState.Commands.Select(command => command.Id).ToArray();
        CollectionAssert.DoesNotContain(commandIds, DesktopAliceAssistant.CommandId);
        CollectionAssert.DoesNotContain(commandIds, "new_character_origin");
        CollectionAssert.Contains(commandIds, "global_settings");

        string[] toolsMenuCommandIds = frame.HeaderState.MenuBar.MenuCommandsByMenuId["tools"].Select(command => command.Id).ToArray();
        CollectionAssert.DoesNotContain(toolsMenuCommandIds, DesktopAliceAssistant.CommandId);
        CollectionAssert.DoesNotContain(toolsMenuCommandIds, "new_character_origin");
        CollectionAssert.Contains(toolsMenuCommandIds, "global_settings");

        Assert.AreEqual(false, frame.HeaderState.ToolStrip.ShowAiFeatures);
        Assert.IsFalse(frame.ChromeState.WorkspaceStrip.ShowOriginDossierAction);
        Assert.IsNull(frame.CommandDialogPaneState.SelectedCommandId);
        Assert.IsNull(frame.CommandDialogPaneState.ActiveDialogId);
        Assert.IsEmpty(frame.CommandDialogPaneState.Fields);
        Assert.IsEmpty(frame.CommandDialogPaneState.Actions);
    }

    [TestMethod]
    public void Project_keeps_portable_import_notice_human_and_short()
    {
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "summary",
            activeTabId: "tab-info",
            latestPortabilityActivity: new WorkspacePortabilityActivity(
                "Last portable import",
                new WorkspacePortabilityReceipt(
                    FormatId: WorkspacePortabilityFormatIds.PortableDossierV1,
                    CompatibilityState: WorkspacePortabilityCompatibilityStates.Compatible,
                    ContextSummary: "Imported runner is now governed dossier truth.",
                    ReceiptSummary: "Portable import completed as governed dossier truth and is ready for normal use or portable export.",
                    ProvenanceSummary: "Import receipt import-ws-1-abc123 captured payload hash abc123.",
                    PayloadSha256: "abc123",
                    NextSafeAction: "Use the workspace normally or export it when you need a governed handoff.",
                    SupportedExchangeModes:
                    [
                        WorkspacePortabilityExchangeModes.InspectOnly,
                        WorkspacePortabilityExchangeModes.Merge,
                        WorkspacePortabilityExchangeModes.Replace
                    ],
                    Notes:
                    [
                        new WorkspacePortabilityNote(
                            Code: "format-identity",
                            Severity: WorkspacePortabilityNoteSeverities.Info,
                            Summary: "Imported native workspace XML on the governed dossier rail.")
                    ])));

        string notice = frame.SectionHostState.Notice ?? string.Empty;

        StringAssert.Contains(notice, "Import ready. Review the character, then keep or discard the changes.");
        StringAssert.Contains(notice, "Nothing changes until you accept the import.");

        string[] forbiddenVisibleTerms =
        [
            "receipt",
            "proof",
            "correlation",
            "environment",
            "diagnostics",
            "tuple",
            "payload",
            "handoff",
            "governed",
            "support reuse"
        ];

        foreach (string forbidden in forbiddenVisibleTerms)
        {
            Assert.IsFalse(
                notice.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Portable import notice should not expose '{forbidden}'. Notice: {notice}");
        }
    }

    [TestMethod]
    public void Project_formats_ruleset_conditioned_navigator_section_action_labels()
    {
        foreach ((string rulesetId, WorkspaceSurfaceActionDefinition action, string expectedLabel) in NavigatorLabelExpectations)
        {
            MainWindowShellFrame frame = ProjectFrame(
                rulesetId,
                activeSectionId: action.TargetId,
                activeTabId: action.TabId,
                workspaceActions: [action]);

            Assert.AreEqual(expectedLabel, frame.NavigatorPaneState.SectionActions.Single().Label);
        }
    }

    [TestMethod]
    public void Project_projects_active_tab_section_actions_into_visible_section_host_state()
    {
        WorkspaceSurfaceActionDefinition[] actions =
        [
            new WorkspaceSurfaceActionDefinition(
                Id: "tab-info.summary",
                Label: "Summary",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Summary,
                TargetId: "summary",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: RulesetDefaults.Sr6),
            new WorkspaceSurfaceActionDefinition(
                Id: "tab-info.profile",
                Label: "Profile",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Section,
                TargetId: "profile",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: RulesetDefaults.Sr6),
            new WorkspaceSurfaceActionDefinition(
                Id: "tab-info.attributes",
                Label: "Attributes",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Section,
                TargetId: "attributes",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: RulesetDefaults.Sr6),
        ];

        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "profile",
            activeTabId: "tab-info",
            workspaceActions: actions);

        CollectionAssert.AreEqual(
            actions.Select(action => action.Id).ToArray(),
            frame.SectionHostState.SectionActions.Select(action => action.Id).ToArray());
        Assert.AreEqual("tab-info.summary", frame.SectionHostState.SectionActions[0].Id);
        Assert.AreEqual("tab-info.profile", frame.SectionHostState.ActiveActionId);
    }

    [TestMethod]
    public void Project_omits_restore_choice_chrome_for_unsaved_active_workspace()
    {
        CharacterWorkspaceId workspaceId = new("runner-007");
        DateTimeOffset lastOpenedUtc = new(2026, 5, 7, 4, 15, 0, TimeSpan.Zero);
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "summary",
            activeTabId: "tab-info",
            openWorkspaces:
            [
                new OpenWorkspaceState(
                    workspaceId,
                    "Runner 007",
                    "Ghostwire",
                    lastOpenedUtc,
                    RulesetDefaults.Sr5,
                    HasSavedWorkspace: false)
            ],
            activeWorkspaceId: workspaceId,
            shellNotice: "Restored 1 workspace(s).");

        Assert.IsNull(frame.ChromeState.SummaryHeader.RestoreContinuitySummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.StaleStateSummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.ConflictChoiceSummary);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.HasVisibleContent);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.CanSaveLocalWorkBeforeRestore);
    }

    [TestMethod]
    public void Project_omits_restore_choice_chrome_when_no_active_workspace_is_open()
    {
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "summary",
            activeTabId: "tab-info",
            openWorkspaces:
            [
                new OpenWorkspaceState(
                    new CharacterWorkspaceId("runner-011"),
                    "Runner 011",
                    "Switchback",
                    new DateTimeOffset(2026, 5, 7, 5, 0, 0, TimeSpan.Zero),
                    RulesetDefaults.Sr6,
                    HasSavedWorkspace: true)
            ],
            activeWorkspaceId: null,
            shellNotice: "Restored 1 workspace(s).");

        Assert.IsNull(frame.ChromeState.SummaryHeader.RestoreContinuitySummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.StaleStateSummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.ConflictChoiceSummary);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.HasVisibleContent);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.CanSaveLocalWorkBeforeRestore);
    }

    [TestMethod]
    public void Project_omits_restore_chrome_when_no_workspace_review_context_exists()
    {
        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "summary",
            activeTabId: "tab-info");

        Assert.IsNull(frame.ChromeState.SummaryHeader.RestoreContinuitySummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.StaleStateSummary);
        Assert.IsNull(frame.ChromeState.SummaryHeader.ConflictChoiceSummary);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.HasVisibleContent);
        Assert.IsFalse(frame.ChromeState.SummaryHeader.CanSaveLocalWorkBeforeRestore);
    }

    [TestMethod]
    public void Project_populates_character_roster_pane_from_open_workspaces()
    {
        CharacterWorkspaceId activeWorkspaceId = new("runner-011");
        OpenWorkspaceState[] openWorkspaces =
        [
            new(
                activeWorkspaceId,
                "Runner 011",
                "Switchback",
                new DateTimeOffset(2026, 5, 7, 5, 0, 0, TimeSpan.Zero),
                RulesetDefaults.Sr6,
                HasSavedWorkspace: true),
            new(
                new CharacterWorkspaceId("runner-012"),
                "Runner 012",
                "Glitch",
                new DateTimeOffset(2026, 5, 7, 5, 15, 0, TimeSpan.Zero),
                RulesetDefaults.Sr6,
                HasSavedWorkspace: false)
        ];

        MainWindowShellFrame frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "summary",
            activeTabId: "tab-info",
            openWorkspaces: openWorkspaces,
            activeWorkspaceId: activeWorkspaceId);

        Assert.AreEqual(1, frame.RosterPaneState.Items.Length);
        Assert.AreEqual("runner-011", frame.RosterPaneState.SelectedWorkspaceId);
        Assert.AreEqual("SR6 Roster", frame.RosterPaneState.Items[0].Name);
        Assert.AreEqual("2 characters", frame.RosterPaneState.Items[0].Meta);
        CollectionAssert.AreEqual(new[] { "runner-011", "runner-012" }, frame.RosterPaneState.Items[0].Children.Select(item => item.Id).ToArray());
    }

    [TestMethod]
    public void ResolveRosterWorkspaces_prefers_session_open_workspaces_over_legacy_overview_list()
    {
        OpenWorkspaceState legacyWorkspace = new(
            new CharacterWorkspaceId("ws-legacy"),
            "Legacy Runner",
            "Legacy",
            new DateTimeOffset(2026, 5, 7, 4, 0, 0, TimeSpan.Zero),
            RulesetDefaults.Sr5,
            HasSavedWorkspace: true);
        OpenWorkspaceState sessionWorkspace = new(
            new CharacterWorkspaceId("ws-session"),
            "Session Runner",
            "Session",
            new DateTimeOffset(2026, 5, 7, 5, 0, 0, TimeSpan.Zero),
            RulesetDefaults.Sr5,
            HasSavedWorkspace: false);
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: sessionWorkspace.Id,
                OpenWorkspaces: [sessionWorkspace],
                RecentWorkspaceIds: [sessionWorkspace.Id]),
            OpenWorkspaces = [legacyWorkspace],
            WorkspaceId = sessionWorkspace.Id
        };

        IReadOnlyList<OpenWorkspaceState> resolved = MainWindow.ResolveRosterWorkspaces(state);

        Assert.AreEqual(1, resolved.Count);
        Assert.AreEqual("ws-session", resolved[0].Id.Value);
    }

    private static MainWindowShellFrame ProjectFrame(
        string rulesetId,
        string activeSectionId,
        string activeTabId,
        WorkspaceSurfaceActionDefinition[]? workspaceActions = null,
        OpenWorkspaceState[]? openWorkspaces = null,
        CharacterWorkspaceId? activeWorkspaceId = null,
        string? shellNotice = null,
        DesktopPreferenceState? preferences = null,
        AppCommandDefinition[]? commands = null,
        AppCommandDefinition[]? menuRoots = null,
        string? openMenuId = null,
        DesktopDialogState? activeDialog = null,
        string? lastCommandId = null,
        WorkspacePortabilityActivity? latestPortabilityActivity = null)
    {
        OpenWorkspaceState[] resolvedOpenWorkspaces = openWorkspaces ?? [];
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = activeSectionId,
            ActiveSectionJson = $"{{\"section\":\"{activeSectionId}\"}}",
            ActiveSectionRows = [new SectionRowState($"{activeSectionId}.value", "ready")],
            OpenWorkspaces = resolvedOpenWorkspaces,
            WorkspaceId = activeWorkspaceId,
            Preferences = preferences ?? DesktopPreferenceState.Default,
            ActiveDialog = activeDialog,
            LatestPortabilityActivity = latestPortabilityActivity,
            ActiveActionId = workspaceActions?
                .FirstOrDefault(action => string.Equals(action.TargetId, activeSectionId, StringComparison.Ordinal))
                ?.Id
                ?? workspaceActions?.FirstOrDefault()?.Id
        };

        ShellSurfaceState shellSurface = new(
            Commands: commands ?? [],
            MenuRoots: menuRoots ?? [],
            NavigationTabs: [],
            WorkspaceActions: workspaceActions ?? [],
            ActiveWorkflowSurfaceActions: [],
            OpenWorkspaces: resolvedOpenWorkspaces,
            ActiveRulesetId: rulesetId,
            PreferredRulesetId: rulesetId,
            ActiveWorkspaceId: activeWorkspaceId,
            ActiveTabId: activeTabId,
            LastCommandId: lastCommandId,
            WorkflowDefinitions: [],
            WorkflowSurfaces: [],
            ActiveRuntime: null)
        {
            Notice = shellNotice,
            OpenMenuId = openMenuId
        };

        return MainWindowShellFrameProjector.Project(overviewState, shellSurface, AlwaysAvailableEvaluator.Instance);
    }

    private sealed class AlwaysAvailableEvaluator : ICommandAvailabilityEvaluator
    {
        public static AlwaysAvailableEvaluator Instance { get; } = new();

        public bool IsCommandEnabled(AppCommandDefinition command, CharacterOverviewState state) => true;

        public bool IsNavigationTabEnabled(NavigationTabDefinition tab, CharacterOverviewState state) => true;

        public bool IsWorkspaceActionEnabled(WorkspaceSurfaceActionDefinition action, CharacterOverviewState state) => true;
    }

    private static readonly (string RulesetId, WorkspaceSurfaceActionDefinition Action, string ExpectedLabel)[] NavigatorLabelExpectations =
    [
        (
            RulesetDefaults.Sr4,
            new WorkspaceSurfaceActionDefinition(
                Id: "tab-info.validate",
                Label: "Validate",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Validate,
                TargetId: "validate",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: RulesetDefaults.Sr4),
            "Character Review"
        ),
        (
            RulesetDefaults.Sr6,
            new WorkspaceSurfaceActionDefinition(
                Id: "tab-gear.inventory",
                Label: "Inventory",
                TabId: "tab-gear",
                Kind: WorkspaceSurfaceActionKind.Section,
                TargetId: "inventory",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: RulesetDefaults.Sr6),
            "Gear"
        )
    ];

    private static readonly string[] StandardInventoryQuickActionControlIds =
    [
        "gear_add"
    ];

    private static readonly string[] StandardInventoryQuickActionLabels =
    [
        "Add Gear"
    ];

    private static readonly (string SectionId, string ControlId, string Label)[] RuntimeBackedSectionQuickActions =
    [
        ("cyberwares", "cyberware_add", "Add Cyberware"),
        ("spells", "spell_add", "Add Spell"),
        ("powers", "adept_power_add", "Add Adept Power"),
        ("complexforms", "complex_form_add", "Add Complex Form"),
        ("initiationgrades", "initiation_add", "Add Initiation"),
        ("spirits", "spirit_add", "Add Spirit"),
        ("critterpowers", "critter_power_add", "Add Critter Power"),
        ("aiprograms", "matrix_program_add", "Add Program")
    ];
}
