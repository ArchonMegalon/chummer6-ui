using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationWizardPresentationTests
{
    [TestMethod]
    public void Draft_state_uses_canonical_document_digest_and_fail_closed_authority()
    {
        const string content = "<character><name>Nova</name></character>";
        CharacterOverviewState state = CreateState(CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.Priority,
            content: content,
            revision: 7,
            contactPoints: 8,
            contactPointsUsed: 3));

        CharacterCreationWizardSnapshot wizard = RequireWizard(state);
        string expectedDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()}";
        Assert.AreEqual(expectedDigest, wizard.ContentDigest);
        Assert.AreEqual(7L, wizard.WorkspaceRevision);
        Assert.AreEqual(RulesetDefaults.Sr5, wizard.RulesetId);
        Assert.AreEqual(CharacterCreationWizardStepIds.Foundation, wizard.ActiveStepId);
        Assert.AreEqual(string.Empty, wizard.SourceDigest);
        Assert.AreEqual(string.Empty, wizard.RuntimeFingerprint);
        Assert.IsFalse(wizard.CanFinalize);
        Assert.AreEqual(71, wizard.SnapshotDigest.Length);
        CollectionAssert.Contains(wizard.CompletionBlockers.ToArray(), CharacterCreationWizardProjector.SourceAuthorityUnavailable);
        CollectionAssert.Contains(wizard.CompletionBlockers.ToArray(), CharacterCreationWizardProjector.RuntimeAuthorityUnavailable);
        CollectionAssert.Contains(wizard.CompletionBlockers.ToArray(), CharacterCreationWizardProjector.BuildGhostContextUnavailable);
        Assert.IsTrue(wizard.LegalOptionsByStep.Values.All(static options => options.Count == 0));

        CharacterCreationBudgetState contacts = wizard.Budgets.Single(
            budget => string.Equals(budget.BudgetId, CharacterCreationBudgetIds.Contacts, StringComparison.Ordinal));
        Assert.IsTrue(contacts.IsExact);
        Assert.AreEqual(8m, contacts.Total);
        Assert.AreEqual(3m, contacts.Used);
        Assert.AreEqual(5m, contacts.Remaining);
        Assert.IsTrue(wizard.Budgets
            .Where(budget => !string.Equals(budget.BudgetId, CharacterCreationBudgetIds.Contacts, StringComparison.Ordinal))
            .All(static budget => !budget.IsExact && budget.Blockers.Count > 0));
    }

    [TestMethod]
    public void Completed_character_has_no_creation_wizard()
    {
        CharacterOverviewState state = CreateState(CreateOverview(
            created: true,
            buildMethod: CharacterCreationBuildMethods.Priority,
            content: "<character />",
            revision: 4));

        Assert.IsNull(state.CreationWizard);
    }

    [TestMethod]
    public void Life_modules_branch_is_required_and_blocked_without_karma_fallback()
    {
        CharacterCreationWizardSnapshot wizard = RequireWizard(CreateState(CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.LifeModules,
            content: "<character />",
            revision: 9)));

        Assert.AreEqual(CharacterCreationBuildMethods.LifeModules, wizard.BuildMethod);
        Assert.AreEqual(CharacterCreationWizardStepIds.LifeModules, wizard.ActiveStepId);
        CharacterCreationWizardStageState foundation = wizard.Steps.Single(
            step => string.Equals(step.StepId, CharacterCreationWizardStepIds.Foundation, StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            new[] { CharacterCreationWizardStepIds.LifeModules },
            foundation.LegalNextStepIds.ToArray());
        CharacterCreationWizardStageState lifeModules = wizard.Steps.Single(
            step => string.Equals(step.StepId, CharacterCreationWizardStepIds.LifeModules, StringComparison.Ordinal));
        Assert.IsTrue(lifeModules.IsRequired);
        Assert.IsFalse(lifeModules.IsAvailable);
        Assert.AreEqual(CharacterCreationWizardStepStatuses.Blocked, lifeModules.Status);
        CollectionAssert.Contains(lifeModules.Blockers.ToArray(), CharacterCreationWizardProjector.LifeModuleAuthorityUnavailable);
        Assert.IsEmpty(lifeModules.LegalNextStepIds);
    }

    [TestMethod]
    public void Build_method_drift_blocks_at_method_step()
    {
        WorkspaceOverviewLoadResult loaded = CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.Priority,
            content: "<character />",
            revision: 3);
        loaded = loaded with
        {
            Build = loaded.Build with { BuildMethod = CharacterCreationBuildMethods.Karma }
        };

        CharacterCreationWizardSnapshot wizard = RequireWizard(CreateState(loaded));
        Assert.AreEqual(CharacterCreationWizardStepIds.Method, wizard.ActiveStepId);
        CollectionAssert.Contains(wizard.CompletionBlockers.ToArray(), CharacterCreationWizardProjector.BuildMethodMismatch);
    }

    [TestMethod]
    public void Setup_life_modules_route_is_a_wizard_blocker_not_karma_dialog()
    {
        DesktopDialogState dialog = BuildNewCharacterContinuationDialog(CharacterCreationBuildMethods.LifeModules);

        Assert.AreEqual("dialog.new_character.life_modules_wizard_blocked", dialog.Id);
        Assert.AreNotEqual("dialog.new_character.karma_workflow", dialog.Id);
        Assert.AreEqual(
            CharacterCreationWizardProjector.LifeModuleAuthorityUnavailable,
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterLifeModulesWizardBlocker"));
        Assert.IsFalse(dialog.Fields.Any(field => field.Id.Contains("Karma", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Existing_setup_routes_remain_priority_sum_to_ten_and_karma()
    {
        Assert.AreEqual(
            "dialog.new_character.priority_workflow",
            BuildNewCharacterContinuationDialog(CharacterCreationBuildMethods.Priority).Id);
        Assert.AreEqual(
            "dialog.new_character.priority_workflow",
            BuildNewCharacterContinuationDialog(CharacterCreationBuildMethods.SumToTen).Id);
        Assert.AreEqual(
            "dialog.new_character.karma_workflow",
            BuildNewCharacterContinuationDialog(CharacterCreationBuildMethods.Karma).Id);
    }

    private static CharacterOverviewState CreateState(WorkspaceOverviewLoadResult loaded)
    {
        CharacterWorkspaceId workspaceId = new("ws-wizard");
        WorkspaceSessionState session = new(
            ActiveWorkspaceId: workspaceId,
            OpenWorkspaces:
            [
                new OpenWorkspaceState(
                    workspaceId,
                    "Wizard",
                    "W",
                    DateTimeOffset.Parse("2026-08-22T00:00:00+00:00"),
                    RulesetDefaults.Sr5,
                    ContentRevision: loaded.ContentRevision,
                    SavedRevision: loaded.SavedRevision)
            ],
            RecentWorkspaceIds: [workspaceId]);
        return new WorkspaceOverviewStateFactory().CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            loaded,
            restoredView: null,
            hasSavedWorkspace: true);
    }

    private static WorkspaceOverviewLoadResult CreateOverview(
        bool created,
        string buildMethod,
        string content,
        long revision,
        int contactPoints = 0,
        int contactPointsUsed = 0)
        => new(
            Profile: new CharacterProfileSection(
                Name: "Wizard",
                Alias: "W",
                PlayerName: string.Empty,
                Metatype: "Human",
                Metavariant: string.Empty,
                Sex: string.Empty,
                Age: string.Empty,
                Height: string.Empty,
                Weight: string.Empty,
                Hair: string.Empty,
                Eyes: string.Empty,
                Skin: string.Empty,
                Concept: string.Empty,
                Description: string.Empty,
                Background: string.Empty,
                CreatedVersion: "1.0",
                AppVersion: "1.0",
                BuildMethod: buildMethod,
                GameplayOption: "Standard",
                Created: created,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                MainMugshotIndex: 0,
                MugshotCount: 0),
            Progress: new CharacterProgressSection(
                Karma: 12m,
                Nuyen: 5000m,
                StartingNuyen: 0m,
                StreetCred: 0,
                Notoriety: 0,
                PublicAwareness: 0,
                BurntStreetCred: 0,
                BuildKarma: 0,
                TotalAttributes: 0,
                TotalSpecial: 0,
                PhysicalCmFilled: 0,
                StunCmFilled: 0,
                TotalEssence: 6m,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false),
            Skills: new CharacterSkillsSection(0, 0, []),
            Rules: new CharacterRulesSection("SR5", "default.xml", "Standard", 25, 0, 0, 3, []),
            Build: new CharacterBuildSection(
                BuildMethod: buildMethod,
                PriorityMetatype: "A",
                PriorityAttributes: "B",
                PrioritySpecial: "C",
                PrioritySkills: "D",
                PriorityResources: "E",
                PriorityTalent: "Mundane",
                SumToTen: 10,
                Special: 0,
                TotalSpecial: 0,
                TotalAttributes: 0,
                ContactPoints: contactPoints,
                ContactPointsUsed: contactPointsUsed),
            Movement: new CharacterMovementSection("0", "0", "0", "0", "0", "0", 0, 0),
            Awakening: new CharacterAwakeningSection(
                false, false, false, false, false, false, false, 0, 0,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                0, 0, 0, 0, 0),
            ContentRevision: revision,
            SavedRevision: revision,
            Document: new WorkspaceDocument(content, RulesetDefaults.Sr5));

    private static CharacterCreationWizardSnapshot RequireWizard(CharacterOverviewState state)
        => state.CreationWizard
           ?? throw new AssertFailedException("Expected an unfinished-character wizard projection.");

    private static DesktopDialogState BuildNewCharacterContinuationDialog(string buildMethod)
    {
        MethodInfo method = typeof(DesktopDialogFactory)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate =>
                string.Equals(candidate.Name, "BuildNewCharacterContinuationDialog", StringComparison.Ordinal)
                && candidate.GetParameters().Length == 5);
        return (DesktopDialogState)(method.Invoke(
            null,
            [RulesetDefaults.Sr5, buildMethod, false, "Wizard", "W"])
            ?? throw new AssertFailedException("Continuation dialog returned null."));
    }
}
