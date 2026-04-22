#nullable enable annotations

using System;
using System.Linq;
using Chummer.Avalonia;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
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
    public void Project_projects_sr6_adapted_section_quick_actions_only_for_sr6()
    {
        MainWindowShellFrame sr6Frame = ProjectFrame(
            RulesetDefaults.Sr6,
            activeSectionId: "summary",
            activeTabId: "tab-info");
        MainWindowShellFrame sr5Frame = ProjectFrame(
            RulesetDefaults.Sr5,
            activeSectionId: "summary",
            activeTabId: "tab-info");

        CollectionAssert.AreEqual(
            Sr6AdaptedQuickActionControlIds,
            sr6Frame.SectionHostState.QuickActions.Select(action => action.ControlId).ToArray());
        CollectionAssert.AreEqual(
            Sr6AdaptedQuickActionLabels,
            sr6Frame.SectionHostState.QuickActions.Select(action => action.Label).ToArray());
        Assert.IsTrue(sr6Frame.SectionHostState.QuickActions[0].IsPrimary);
        Assert.IsEmpty(sr5Frame.SectionHostState.QuickActions);
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

    private static MainWindowShellFrame ProjectFrame(
        string rulesetId,
        string activeSectionId,
        string activeTabId,
        WorkspaceSurfaceActionDefinition[]? workspaceActions = null)
    {
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = activeSectionId,
            ActiveSectionJson = $"{{\"section\":\"{activeSectionId}\"}}",
            ActiveSectionRows = [new SectionRowState($"{activeSectionId}.value", "ready")],
            ActiveActionId = workspaceActions?.FirstOrDefault()?.Id
        };

        ShellSurfaceState shellSurface = new(
            Commands: [],
            MenuRoots: [],
            NavigationTabs: [],
            WorkspaceActions: workspaceActions ?? [],
            ActiveWorkflowSurfaceActions: [],
            OpenWorkspaces: [],
            ActiveRulesetId: rulesetId,
            PreferredRulesetId: rulesetId,
            ActiveWorkspaceId: null,
            ActiveTabId: activeTabId,
            LastCommandId: null,
            WorkflowDefinitions: [],
            WorkflowSurfaces: [],
            ActiveRuntime: null);

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
        "gear_add",
        "gear_edit",
        "gear_delete",
        "gear_source"
    ];

    private static readonly string[] StandardInventoryQuickActionLabels =
    [
        "Add Gear",
        "Edit Gear",
        "Remove Gear",
        "Show Source"
    ];

    private static readonly string[] Sr6AdaptedQuickActionControlIds =
    [
        "create_entry",
        "show_source"
    ];

    private static readonly string[] Sr6AdaptedQuickActionLabels =
    [
        "Add Guided Entry",
        "Show Source"
    ];
}
