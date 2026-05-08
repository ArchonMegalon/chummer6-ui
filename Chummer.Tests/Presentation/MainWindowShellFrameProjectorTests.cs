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
    public void Project_surfaces_restore_continuity_and_conflict_guidance_for_unsaved_active_workspace()
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

        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.RestoreContinuitySummary ?? string.Empty,
            "Restore choice: keep runner-007 open before accepting a newer continuity packet.");
        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.RestoreContinuitySummary ?? string.Empty,
            "runner-007 stays visible on the current desktop head until you choose review or support.");
        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.ConflictChoiceSummary ?? string.Empty,
            "Conflict choices: keep local work visible, save local work when available, review Campaign Workspace, or open workspace support before accepting restore replacement.");
        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.ConflictChoiceSummary ?? string.Empty,
            "runner-007 was last touched locally at 2026-05-07 04:15 UTC and stays visible before any replacement;");
        Assert.IsTrue(frame.ChromeState.SummaryHeader.CanSaveLocalWorkBeforeRestore);
    }

    [TestMethod]
    public void Project_surfaces_stale_state_warning_when_no_active_workspace_is_open()
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

        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.RestoreContinuitySummary ?? string.Empty,
            "keep the current desktop workspace review visible before accepting a newer continuity packet.");
        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.StaleStateSummary ?? string.Empty,
            "Stale state: service continuity is unavailable until a local workspace is opened for review.");
        StringAssert.Contains(
            frame.ChromeState.SummaryHeader.StaleStateSummary ?? string.Empty,
            "no active workspace stays visible on the current desktop head before any replacement;");
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

        Assert.AreEqual(2, frame.RosterPaneState.Items.Length);
        Assert.AreEqual("runner-011", frame.RosterPaneState.SelectedWorkspaceId);
        Assert.AreEqual("Runner 011", frame.RosterPaneState.Items[0].Name);
        Assert.AreEqual("Switchback", frame.RosterPaneState.Items[0].Meta);
        CollectionAssert.AreEqual(new[] { "runner-011", "runner-012" }, frame.RosterPaneState.Items.Select(item => item.Id).ToArray());
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
        string? shellNotice = null)
    {
        OpenWorkspaceState[] resolvedOpenWorkspaces = openWorkspaces ?? [];
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = activeSectionId,
            ActiveSectionJson = $"{{\"section\":\"{activeSectionId}\"}}",
            ActiveSectionRows = [new SectionRowState($"{activeSectionId}.value", "ready")],
            OpenWorkspaces = resolvedOpenWorkspaces,
            WorkspaceId = activeWorkspaceId,
            ActiveActionId = workspaceActions?
                .FirstOrDefault(action => string.Equals(action.TargetId, activeSectionId, StringComparison.Ordinal))
                ?.Id
                ?? workspaceActions?.FirstOrDefault()?.Id
        };

        ShellSurfaceState shellSurface = new(
            Commands: [],
            MenuRoots: [],
            NavigationTabs: [],
            WorkspaceActions: workspaceActions ?? [],
            ActiveWorkflowSurfaceActions: [],
            OpenWorkspaces: resolvedOpenWorkspaces,
            ActiveRulesetId: rulesetId,
            PreferredRulesetId: rulesetId,
            ActiveWorkspaceId: activeWorkspaceId,
            ActiveTabId: activeTabId,
            LastCommandId: null,
            WorkflowDefinitions: [],
            WorkflowSurfaces: [],
            ActiveRuntime: null)
        {
            Notice = shellNotice
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
            "Parity Check"
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
