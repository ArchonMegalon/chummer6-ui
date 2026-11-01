using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// T33 parity gates: with owner-bound skills/resources states bound to the loaded overview,
/// the creation wizard projector must surface real authority state instead of the historical
/// hard-coded blocked placeholders, and resources must expose real legal options. Every fixture
/// digest is computed with Core's canonical digest helpers so the projector's integrity gates
/// hold for genuinely valid Core data.
/// </summary>
[TestClass]
public partial class CharacterCreationWizardProjectorAuthorityTests
{
    private static readonly Guid PrioritySourceIdA = new("1f2e3d4c-5b6a-4798-8a9b-0c1d2e3f4a5b");
    private static readonly Guid PrioritySourceIdB = new("2a3b4c5d-6e7f-4a90-9b8c-1d2e3f4a5b6c");

    private const string DocumentContent = "<character><id>fixture</id></character>";

    [TestMethod]
    public void Resources_stage_and_options_are_projected_from_loaded_authority_state()
    {
        CharacterWorkspaceId workspaceId = new("ws-authority");
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview();
        CharacterCreationResourcesState resources = CreateResourcesState(workspaceId, loaded);

        CharacterCreationWizardSnapshot snapshot = CharacterCreationWizardProjector.Project(
            workspaceId,
            loaded,
            skills: null,
            resources: resources);

        AssertStage(snapshot, CharacterCreationWizardStepIds.Resources, CharacterCreationWizardStepStatuses.Available);

        IReadOnlyList<CharacterCreationLegalOption> options =
            snapshot.LegalOptionsByStep[CharacterCreationWizardStepIds.Resources];
        Assert.AreEqual(2, options.Count);
        Assert.AreEqual("priority:a", options[0].OptionId);
        Assert.AreEqual("priority:b", options[1].OptionId);
        Assert.IsTrue(options[0].IsEnabled);
        Assert.IsNull(options[0].DisableReasonKey);
        StringAssert.Contains(options[0].Label, "45000");
        StringAssert.Contains(options[1].Label, "karma invested");
    }

    [TestMethod]
    public void Resources_stage_falls_back_to_blocked_without_a_bound_state()
    {
        CharacterWorkspaceId workspaceId = new("ws-authority");
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview();

        CharacterCreationWizardSnapshot snapshot = CharacterCreationWizardProjector.Project(
            workspaceId,
            loaded,
            skills: null,
            resources: null);

        AssertStage(snapshot, CharacterCreationWizardStepIds.Resources, CharacterCreationWizardStepStatuses.Blocked);
        Assert.IsTrue(snapshot.Steps.Single(stage =>
                string.Equals(stage.StepId, CharacterCreationWizardStepIds.Resources, StringComparison.Ordinal))
            .Blockers.Contains("creation-wizard-legal-options-authority-unavailable"));
        Assert.AreEqual(0, snapshot.LegalOptionsByStep[CharacterCreationWizardStepIds.Resources].Count);
    }

    [TestMethod]
    public void Skills_stage_is_available_and_budgets_are_real_from_bound_state()
    {
        CharacterWorkspaceId workspaceId = new("ws-authority");
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview();
        CharacterCreationSkillsState skills = CreateSkillsState(workspaceId, loaded);

        CharacterCreationWizardSnapshot assertions = CharacterCreationWizardProjector.Project(
            workspaceId,
            loaded,
            skills: skills,
            resources: null);

        AssertStage(assertions, CharacterCreationWizardStepIds.Skills, CharacterCreationWizardStepStatuses.Available);
        CharacterCreationBudgetState activeSkills = assertions.Budgets.Single(
            budget => string.Equals(budget.BudgetId, CharacterCreationBudgetIds.ActiveSkills, StringComparison.Ordinal));
        Assert.AreEqual(46m, activeSkills.Total);
        Assert.AreEqual(46m, activeSkills.Remaining);
        Assert.IsTrue(activeSkills.IsExact);
        Assert.AreEqual(0, activeSkills.Blockers.Count);

        CharacterCreationBudgetState skillGroups = assertions.Budgets.Single(
            budget => string.Equals(budget.BudgetId, CharacterCreationBudgetIds.SkillGroups, StringComparison.Ordinal));
        Assert.AreEqual(10m, skillGroups.Total);
        CharacterCreationBudgetState knowledgeSkills = assertions.Budgets.Single(
            budget => string.Equals(budget.BudgetId, CharacterCreationBudgetIds.KnowledgeSkills, StringComparison.Ordinal));
        Assert.AreEqual(0, knowledgeSkills.Blockers.Count);
    }

    [TestMethod]
    public void Skills_state_with_blockers_projects_blocked_stage()
    {
        CharacterWorkspaceId workspaceId = new("ws-authority");
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview();
        CharacterCreationSkillsState skills = CreateSkillsState(
            workspaceId,
            loaded,
            authorityBlockers: ["creation-skills-authority-blocked-test"]);

        CharacterCreationWizardSnapshot snapshot = CharacterCreationWizardProjector.Project(
            workspaceId,
            loaded,
            skills: skills,
            resources: null);

        AssertStage(snapshot, CharacterCreationWizardStepIds.Skills, CharacterCreationWizardStepStatuses.Blocked);
        Assert.IsTrue(snapshot.Steps.Single(stage =>
                string.Equals(stage.StepId, CharacterCreationWizardStepIds.Skills, StringComparison.Ordinal))
            .Blockers.Contains("creation-skills-authority-blocked-test"));
    }

    [TestMethod]
    public void Skills_state_bound_to_a_foreign_workspace_is_dropped()
    {
        CharacterWorkspaceId workspaceId = new("ws-authority");
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview();
        CharacterWorkspaceId foreignWorkspaceId = new(Guid.NewGuid().ToString("D"));
        CharacterCreationSkillsState skills = CreateSkillsState(foreignWorkspaceId, loaded);

        CharacterCreationWizardSnapshot snapshot = CharacterCreationWizardProjector.Project(
            workspaceId,
            loaded,
            skills: skills,
            resources: null);

        AssertStage(snapshot, CharacterCreationWizardStepIds.Skills, CharacterCreationWizardStepStatuses.Blocked);
        Assert.IsFalse(snapshot.Budgets.Any(budget =>
            string.Equals(budget.BudgetId, CharacterCreationBudgetIds.ActiveSkills, StringComparison.Ordinal)
            && budget.IsExact));
    }
}
