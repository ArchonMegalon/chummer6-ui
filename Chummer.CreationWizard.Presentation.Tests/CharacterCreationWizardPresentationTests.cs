using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationWizardPresentationTests
{
    [TestMethod]
    public void Matching_core_foundation_projects_exact_budget_and_read_only_nationality_options()
    {
        const string content = "<character><name>Nova</name></character>";
        WorkspaceOverviewLoadResult loaded = CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.LifeModules,
            content: content,
            revision: 7);
        CharacterCreationFoundationState foundation = CreateFoundationState(loaded);
        var service = new StubFoundationService(foundation);

        CharacterOverviewState state = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory(service));
        CharacterCreationWizardSnapshot wizard = RequireWizard(state);

        Assert.AreSame(foundation, state.CreationFoundation);
        Assert.AreEqual(foundation.Binding.SourceDigest, wizard.SourceDigest);
        CollectionAssert.DoesNotContain(
            wizard.CompletionBlockers.ToList(),
            CharacterCreationWizardProjector.SourceAuthorityUnavailable);
        CollectionAssert.Contains(
            wizard.CompletionBlockers.ToList(),
            CharacterCreationFoundationBlockers.MetatypeCatalogAuthorityRequired);
        CollectionAssert.Contains(
            wizard.CompletionBlockers.ToList(),
            CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired);

        CharacterCreationBudgetState budget = wizard.Budgets.Single(item =>
            item.BudgetId == CharacterCreationBudgetIds.LifeModules);
        Assert.IsTrue(budget.IsExact);
        Assert.AreEqual(750m, budget.Total);
        Assert.AreEqual(0m, budget.Used);
        Assert.AreEqual(750m, budget.Remaining);

        CharacterCreationLegalOption nationality = AssertExactlyOne(
            wizard.LegalOptionsByStep[CharacterCreationWizardStepIds.LifeModules]);
        Assert.IsTrue(nationality.IsEnabled);
        Assert.AreEqual("nationality-module", nationality.OptionId);
        Assert.AreEqual("RF", nationality.SourceId);
        Assert.AreEqual(66, nationality.SourcePage);
        Assert.AreEqual(15m, AssertExactlyOne(nationality.Costs).Delta);
        CollectionAssert.Contains(
            nationality.SourceAnchorIds.ToList(),
            "lifemodules.xml#module:nationality-module");

        CharacterCreationWizardStageState foundationStep = wizard.Steps.Single(item =>
            item.StepId == CharacterCreationWizardStepIds.Foundation);
        CharacterCreationWizardStageState lifeModulesStep = wizard.Steps.Single(item =>
            item.StepId == CharacterCreationWizardStepIds.LifeModules);
        Assert.IsFalse(foundationStep.IsAvailable);
        Assert.AreEqual(CharacterCreationWizardStepStatuses.Blocked, foundationStep.Status);
        Assert.IsFalse(lifeModulesStep.IsAvailable);
        Assert.AreEqual(CharacterCreationWizardStepStatuses.Blocked, lifeModulesStep.Status);
        CollectionAssert.Contains(
            wizard.Warnings.ToList(),
            "creation-wizard-confirm-authority-unavailable");
        Assert.IsFalse(wizard.CanFinalize);
        Assert.AreEqual(1, service.LoadCalls);
    }

    [TestMethod]
    public void Revision_or_raw_digest_mismatch_rejects_core_foundation_projection()
    {
        const string content = "<character><name>Nova</name></character>";
        WorkspaceOverviewLoadResult loaded = CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.LifeModules,
            content: content,
            revision: 7);
        CharacterCreationFoundationState valid = CreateFoundationState(loaded);
        CharacterCreationFoundationState revisionMismatch = valid with
        {
            Binding = valid.Binding with { ContentRevision = 8 }
        };
        CharacterCreationFoundationState digestMismatch = valid with
        {
            Binding = valid.Binding with
            {
                RawCharacterXmlDigest = "sha256:" + new string('0', 64)
            }
        };

        CharacterOverviewState revisionState = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory(new StubFoundationService(revisionMismatch)));
        CharacterOverviewState digestState = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory(new StubFoundationService(digestMismatch)));

        AssertFoundationRejected(revisionState);
        AssertFoundationRejected(digestState);
    }

    [TestMethod]
    public void Null_service_or_missing_source_authority_never_widens_nationality_options()
    {
        WorkspaceOverviewLoadResult loaded = CreateOverview(
            created: false,
            buildMethod: CharacterCreationBuildMethods.LifeModules,
            content: "<character />",
            revision: 5);
        CharacterOverviewState nullService = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory());
        CharacterCreationFoundationState unavailable = CreateFoundationState(loaded) with
        {
            AuthorityBlockers =
            [
                CharacterCreationFoundationBlockers.EnabledSourceAuthorityRequired,
                CharacterCreationFoundationBlockers.MetatypeCatalogAuthorityRequired,
                CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired
            ]
        };
        CharacterOverviewState missingSource = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory(new StubFoundationService(unavailable)));

        AssertFoundationRejected(nullService);
        Assert.IsNotNull(missingSource.CreationFoundation);
        CharacterCreationWizardSnapshot missingSourceWizard = RequireWizard(missingSource);
        Assert.AreEqual(string.Empty, missingSourceWizard.SourceDigest);
        Assert.IsEmpty(
            missingSourceWizard.LegalOptionsByStep[CharacterCreationWizardStepIds.LifeModules]);
        Assert.IsFalse(missingSourceWizard.Budgets.Single(item =>
            item.BudgetId == CharacterCreationBudgetIds.LifeModules).IsExact);
        CollectionAssert.Contains(
            missingSourceWizard.CompletionBlockers.ToList(),
            CharacterCreationWizardProjector.SourceAuthorityUnavailable);
    }

    [TestMethod]
    public void Completed_character_does_not_call_optional_foundation_service()
    {
        WorkspaceOverviewLoadResult loaded = CreateOverview(
            created: true,
            buildMethod: CharacterCreationBuildMethods.LifeModules,
            content: "<character />",
            revision: 4);
        var service = new StubFoundationService(CreateFoundationState(loaded));

        CharacterOverviewState state = CreateState(
            loaded,
            new WorkspaceOverviewStateFactory(service));

        Assert.IsNull(state.CreationWizard);
        Assert.IsNull(state.CreationFoundation);
        Assert.AreEqual(0, service.LoadCalls);
    }

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
        Assert.AreEqual(CharacterCreationWizardStepIds.Foundation, wizard.ActiveStepId);
        CharacterCreationWizardStageState foundation = wizard.Steps.Single(
            step => string.Equals(step.StepId, CharacterCreationWizardStepIds.Foundation, StringComparison.Ordinal));
        Assert.AreEqual(CharacterCreationWizardStepStatuses.Blocked, foundation.Status);
        Assert.IsFalse(foundation.IsAvailable);
        Assert.IsEmpty(foundation.LegalNextStepIds);
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

    private static CharacterOverviewState CreateState(
        WorkspaceOverviewLoadResult loaded,
        WorkspaceOverviewStateFactory? factory = null)
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
        return (factory ?? new WorkspaceOverviewStateFactory()).CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            loaded,
            restoredView: null,
            hasSavedWorkspace: true);
    }

    private static CharacterCreationFoundationState CreateFoundationState(
        WorkspaceOverviewLoadResult loaded)
    {
        string rawDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(loaded.Document!.Content))).ToLowerInvariant()}";
        var binding = new CharacterCreationFoundationBinding(
            WorkspaceId: new CharacterWorkspaceId("ws-wizard"),
            ContentRevision: loaded.ContentRevision,
            SavedRevision: loaded.SavedRevision,
            RawCharacterXmlDigest: rawDigest,
            CharacterDigestSemantics: CharacterCreationFoundationDigestSemantics.RawCharacterXmlSha256,
            SourceDigest: "sha256:" + new string('a', 64),
            SourceDigestSemantics: CharacterCreationFoundationDigestSemantics.RawSourceInputsSha256,
            SourceFilterApplied: false,
            EnabledSources: ["RF", "SR5"]);
        var nationality = new LifeModuleLegalOptionDto(
            ModuleId: "nationality-module",
            StageOrder: LifeModuleJourneyStageOrders.Nationality,
            Name: "Nation",
            KarmaCost: 15m,
            Source: "RF",
            Page: 66,
            StoryTemplate: "$real was born there.",
            IsEnabled: true,
            Requirements: [],
            Versions: [],
            Effects: [],
            FollowUps: [],
            SourceAnchorIds: ["lifemodules.xml#module:nationality-module"],
            StageId: "Nationality",
            CanRepeat: false,
            KarmaRaw: "15",
            KarmaIsExact: true,
            PageReference: "66",
            AuthorityBlockers: []);
        var budget = new CharacterCreationBudgetState(
            BudgetId: CharacterCreationBudgetIds.LifeModules,
            Label: "Life Modules Karma",
            Total: 750m,
            Used: 0m,
            Remaining: 750m,
            IsExact: true,
            Blockers: [],
            Unit: "karma");
        return new CharacterCreationFoundationState(
            Schema: CharacterCreationFoundationSchemas.SnapshotV1,
            Binding: binding,
            RulesetId: loaded.Document.RulesetId,
            CurrentMetatype: "Human",
            BuildMethod: CharacterCreationBuildMethods.LifeModules,
            CharacterCreated: loaded.Profile.Created,
            MetatypeOptions: [],
            NationalityOptions: [nationality],
            LifeModuleBudget: budget,
            PendingDraft: null,
            ResumeStatus: CharacterCreationFoundationResumeStatuses.AuthorityRequired,
            AuthorityBlockers:
            [
                CharacterCreationFoundationBlockers.MetatypeCatalogAuthorityRequired,
                CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired
            ],
            SnapshotDigest: "sha256:" + new string('b', 64));
    }

    private static void AssertFoundationRejected(CharacterOverviewState state)
    {
        Assert.IsNull(state.CreationFoundation);
        CharacterCreationWizardSnapshot wizard = RequireWizard(state);
        Assert.AreEqual(string.Empty, wizard.SourceDigest);
        Assert.IsEmpty(wizard.LegalOptionsByStep[CharacterCreationWizardStepIds.LifeModules]);
        CollectionAssert.Contains(
            wizard.CompletionBlockers.ToList(),
            CharacterCreationWizardProjector.SourceAuthorityUnavailable);
        CharacterCreationBudgetState budget = wizard.Budgets.Single(item =>
            item.BudgetId == CharacterCreationBudgetIds.LifeModules);
        Assert.IsFalse(budget.IsExact);
    }

    private static T AssertExactlyOne<T>(IReadOnlyList<T> values)
    {
        Assert.HasCount(1, values);
        return values[0];
    }

    private sealed class StubFoundationService : ICharacterCreationFoundationService
    {
        private readonly CharacterCreationFoundationState _state;

        public StubFoundationService(CharacterCreationFoundationState state)
        {
            _state = state;
        }

        public int LoadCalls { get; private set; }

        public CharacterCreationFoundationResult<CharacterCreationFoundationState> Load(
            CharacterCreationFoundationLoadRequest request)
        {
            LoadCalls++;
            return new CharacterCreationFoundationResult<CharacterCreationFoundationState>(
                CharacterCreationFoundationOutcomes.Success,
                _state,
                _state.AuthorityBlockers);
        }

        public CharacterCreationFoundationResult<CharacterCreationFoundationPreview> Preview(
            CharacterCreationFoundationPreviewRequest request) => new(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired]);

        public CharacterCreationFoundationResult<CharacterCreationFoundationApplyReceipt> Confirm(
            CharacterCreationFoundationConfirmRequest request) => new(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired]);
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
