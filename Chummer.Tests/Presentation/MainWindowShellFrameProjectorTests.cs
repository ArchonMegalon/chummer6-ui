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
