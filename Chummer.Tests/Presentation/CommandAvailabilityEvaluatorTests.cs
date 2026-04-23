using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class CommandAvailabilityEvaluatorTests
{
    [TestMethod]
    public void IsCommandEnabled_requires_open_workspace_when_flagged()
    {
        DefaultCommandAvailabilityEvaluator evaluator = new();
        AppCommandDefinition command = new("save_character", "Save", "file", true, true, RulesetDefaults.Sr5);

        bool withoutWorkspace = evaluator.IsCommandEnabled(command, CharacterOverviewState.Empty);
        bool withWorkspace = evaluator.IsCommandEnabled(
            command,
            CharacterOverviewState.Empty with { WorkspaceId = new CharacterWorkspaceId("ws-1") });

        Assert.IsFalse(withoutWorkspace);
        Assert.IsTrue(withWorkspace);
    }

    [TestMethod]
    public void IsNavigationTabEnabled_honors_enabled_flag()
    {
        DefaultCommandAvailabilityEvaluator evaluator = new();
        NavigationTabDefinition tab = new("tab-skills", "Skills", "skills", "character", true, false, RulesetDefaults.Sr5);

        bool enabled = evaluator.IsNavigationTabEnabled(
            tab,
            CharacterOverviewState.Empty with { WorkspaceId = new CharacterWorkspaceId("ws-1") });

        Assert.IsFalse(enabled);
    }

    [TestMethod]
    public void IsCommandEnabled_keeps_startup_shell_commands_available_without_workspace()
    {
        DefaultCommandAvailabilityEvaluator evaluator = new();
        CharacterOverviewState state = CharacterOverviewState.Empty;

        AppCommandDefinition newCharacter = new("new_character", "New", "file", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition openCharacter = new("open_character", "Open", "file", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition globalSettings = new("global_settings", "Options", "tools", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition reportBug = new("report_bug", "Report Issue", "tools", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition saveCharacter = new("save_character", "Save", "file", true, true, RulesetDefaults.Sr5);

        Assert.IsTrue(evaluator.IsCommandEnabled(newCharacter, state));
        Assert.IsTrue(evaluator.IsCommandEnabled(openCharacter, state));
        Assert.IsTrue(evaluator.IsCommandEnabled(globalSettings, state));
        Assert.IsTrue(evaluator.IsCommandEnabled(reportBug, state));
        Assert.IsFalse(evaluator.IsCommandEnabled(saveCharacter, state));
    }

    [TestMethod]
    public void IsWorkspaceActionEnabled_requires_open_workspace_when_flagged()
    {
        DefaultCommandAvailabilityEvaluator evaluator = new();
        WorkspaceSurfaceActionDefinition action = new(
            Id: "tab-info.summary",
            Label: "Summary",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Summary,
            TargetId: "summary",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        bool withoutWorkspace = evaluator.IsWorkspaceActionEnabled(action, CharacterOverviewState.Empty);
        bool withWorkspace = evaluator.IsWorkspaceActionEnabled(
            action,
            CharacterOverviewState.Empty with { WorkspaceId = new CharacterWorkspaceId("ws-1") });

        Assert.IsFalse(withoutWorkspace);
        Assert.IsTrue(withWorkspace);
    }
}
