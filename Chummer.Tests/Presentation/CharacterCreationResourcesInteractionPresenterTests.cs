using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterCreationResourcesInteractionPresenterTests
{
    [TestMethod]
    public void Load_accepts_workspace_document_raw_auxiliary_sha256()
    {
        Fixture fixture = CreateFixture(new string('a', 64));
        var presenter = new CharacterCreationResourcesInteractionPresenter(
            new FakeResourcesService(fixture.State));

        CharacterCreationResourcesInteractionLoadResult result = presenter.Load(fixture.Overview);

        Assert.AreEqual(CharacterCreationResourcesOutcomes.Available, result.Outcome);
        Assert.IsNotNull(result.State);
        Assert.AreEqual(new string('a', 64), result.State.Binding.AuxiliaryStateDigest);
        Assert.IsTrue(result.State.CanEdit);
        Assert.IsEmpty(result.Blockers);
    }

    [TestMethod]
    public void Load_rejects_noncanonical_workspace_document_auxiliary_sha256()
    {
        Fixture valid = CreateFixture(new string('a', 64));
        string[] invalidDigests =
        [
            Digest('a'),
            new string('A', 64),
            new string('a', 63),
            new string('a', 63) + "g",
            string.Empty
        ];

        foreach (string invalidDigest in invalidDigests)
        {
            CharacterCreationResourcesState invalidState = WithAuxiliaryDigest(
                valid.State,
                invalidDigest);
            var presenter = new CharacterCreationResourcesInteractionPresenter(
                new FakeResourcesService(invalidState));

            CharacterCreationResourcesInteractionLoadResult result = presenter.Load(valid.Overview);

            Assert.AreEqual(
                CharacterCreationResourcesOutcomes.Conflict,
                result.Outcome,
                $"Unexpectedly accepted auxiliary digest '{invalidDigest}'.");
            Assert.IsNull(result.State);
            CollectionAssert.Contains(
                result.Blockers.ToArray(),
                CharacterCreationResourcesInteractionBlockers.BindingMismatch);
        }
    }

    private static Fixture CreateFixture(string auxiliaryStateDigest)
    {
        const string content =
            "<character><created>False</created><buildmethod>Priority</buildmethod></character>";
        string rawDigest = RawDigest(content);
        CharacterWorkspaceId workspaceId = new("creation-resources-presentation");
        CharacterCreationResourcesAuthority authority = CreateAuthority();
        var prerequisiteDraft = new CharacterCreationPrerequisiteDraft(
            CharacterCreationPrerequisiteSchemas.DraftV1,
            workspaceId,
            DraftRevision: 1,
            BaseContentRevision: 1,
            BaseRawCharacterXmlDigest: rawDigest,
            AuthorityDigest: Digest('6'),
            BuildMethod: CharacterCreationBuildMethods.Priority,
            SettingsProfileId: "default",
            PriorityTable: "Standard",
            PriorityArray: ["A", "B", "C", "D", "E"],
            SumToTenTarget: null,
            Assignments: [],
            CreationKarmaTotal: 25,
            CreationKarmaUsed: 0,
            SourceAnchorIds: ["priorities.xml#priority-table"],
            DraftDigest: Digest('7'));
        var binding = new CharacterCreationResourcesBinding(
            workspaceId,
            WorkspaceRevision: 2,
            ContentRevision: 2,
            SavedRevision: 2,
            RawCharacterXmlDigest: rawDigest,
            AuxiliaryStateDigest: auxiliaryStateDigest,
            PrerequisiteDraftRevision: prerequisiteDraft.DraftRevision,
            PrerequisiteDraftDigest: prerequisiteDraft.DraftDigest,
            AuthorityDigest: authority.AuthorityDigest,
            SourceDigest: authority.SourceDigest,
            RulesDigest: authority.RulesDigest,
            RuntimeDigest: authority.RuntimeDigest);
        CharacterCreationResourceAllocationOption optionCandidate = new(
            "karma:0",
            KarmaInvestment: 0,
            NuyenFromKarma: 0m,
            TotalStartingNuyen: 50_000m,
            IsEnabled: true,
            Blockers: [],
            SourceAnchorIds: CharacterCreationResourcesSourceAnchors.All,
            OptionDigest: string.Empty);
        CharacterCreationResourceAllocationOption option = optionCandidate with
        {
            OptionDigest = CharacterCreationResourcesRules.ComputeAllocationOptionDigest(
                optionCandidate)
        };
        var budget = new CharacterCreationResourcesBudget(
            PriorityNuyen: 50_000m,
            KarmaInvestment: 0,
            NuyenFromKarma: 0m,
            TotalStartingNuyen: 50_000m,
            KnownPurchaseCost: 0m,
            RemainingNuyen: 50_000m,
            Overspend: 0m,
            CarryoverLimit: 5_000m,
            CarryoverExcess: 45_000m,
            IsExact: true,
            Blockers: [],
            SourceAnchorIds: CharacterCreationResourcesSourceAnchors.All);
        var stateCandidate = new CharacterCreationResourcesState(
            CharacterCreationResourcesSchemas.StateV1,
            CharacterCreationWizardStepIds.Resources,
            binding,
            authority,
            prerequisiteDraft,
            PendingDraft: null,
            Options: [option],
            budget,
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty);
        CharacterCreationResourcesState state = stateCandidate with
        {
            SnapshotDigest = CharacterCreationResourcesRules.ComputeStateDigest(stateCandidate)
        };
        return new Fixture(state, CreateOverview(content, binding));
    }

    private static CharacterCreationResourcesAuthority CreateAuthority()
    {
        var priorityCandidate = new CharacterCreationResourcePriorityOption(
            "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            "D",
            50_000m,
            Digest('1'),
            CharacterCreationResourcesSourceAnchors.All,
            OptionDigest: string.Empty);
        CharacterCreationResourcePriorityOption priority = priorityCandidate with
        {
            OptionDigest = CharacterCreationResourcesRules.ComputePriorityOptionDigest(
                priorityCandidate)
        };
        var authorityCandidate = new CharacterCreationResourcesAuthority(
            CharacterCreationResourcesSchemas.AuthorityV1,
            RulesetDefaults.Sr5,
            "default",
            CharacterCreationBuildMethods.Priority,
            KarmaToNuyenRate: 2_000m,
            MaximumKarmaInvestment: 10,
            NuyenCarryover: 5_000m,
            MaximumAvailability: 12,
            UnrestrictedNuyen: false,
            PriorityOptions: [priority],
            SourceAnchorIds: CharacterCreationResourcesSourceAnchors.All,
            Blockers: [],
            IsAuthoritative: true,
            SourceDigest: Digest('2'),
            ProfileDigest: Digest('3'),
            RulesDigest: Digest('4'),
            RuntimeDigest: Digest('5'),
            AuthorityDigest: string.Empty);
        return authorityCandidate with
        {
            AuthorityDigest = CharacterCreationResourcesRules.ComputeAuthorityDigest(
                authorityCandidate)
        };
    }

    private static CharacterCreationResourcesState WithAuxiliaryDigest(
        CharacterCreationResourcesState state,
        string auxiliaryStateDigest)
    {
        CharacterCreationResourcesState candidate = state with
        {
            Binding = state.Binding with { AuxiliaryStateDigest = auxiliaryStateDigest },
            SnapshotDigest = string.Empty
        };
        return candidate with
        {
            SnapshotDigest = CharacterCreationResourcesRules.ComputeStateDigest(candidate)
        };
    }

    private static CharacterOverviewState CreateOverview(
        string content,
        CharacterCreationResourcesBinding binding)
    {
        WorkspaceSessionState session = new(
            binding.WorkspaceId,
            [new OpenWorkspaceState(
                binding.WorkspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-09-01T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                binding.ContentRevision,
                binding.SavedRevision)],
            [binding.WorkspaceId]);
        WorkspaceOverviewLoadResult loaded = new(
            new CharacterProfileSection(
                "Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
                CharacterCreationBuildMethods.Priority, "Standard", false,
                false, false, false, false, 0, 0),
            new CharacterProgressSection(
                0m, 0m, 0m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m,
                0, 0, false, false, false),
            new CharacterSkillsSection(0, 0, []),
            new CharacterRulesSection("SR5", "default", "Standard", 25, 0, 0, 3, []),
            new CharacterBuildSection(
                CharacterCreationBuildMethods.Priority,
                "A", "B", "C", "D", "E", "Mundane", 10,
                0, 0, 0, 0, 0),
            new CharacterMovementSection("0", "0", "0", "0", "0", "0", 0, 0),
            new CharacterAwakeningSection(
                false, false, false, false, false, false, false, 0, 0,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                0, 0, 0, 0, 0),
            binding.ContentRevision,
            binding.SavedRevision,
            new WorkspaceDocument(content, RulesetDefaults.Sr5));
        return new WorkspaceOverviewStateFactory().CreateLoadedState(
            CharacterOverviewState.Empty,
            binding.WorkspaceId,
            session,
            loaded,
            restoredView: null,
            hasSavedWorkspace: true);
    }

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static string RawDigest(string value) => "sha256:"
        + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record Fixture(
        CharacterCreationResourcesState State,
        CharacterOverviewState Overview);

    private sealed class FakeResourcesService(CharacterCreationResourcesState state)
        : ICharacterCreationResourcesService
    {
        public CharacterCreationResourcesResult<CharacterCreationResourcesState> Load(
            CharacterCreationResourcesLoadRequest request) =>
            new(CharacterCreationResourcesOutcomes.Available, state, []);

        public CharacterCreationResourcesResult<CharacterCreationResourcesPreview> Preview(
            CharacterCreationResourcesPreviewRequest request) =>
            new(CharacterCreationResourcesOutcomes.Invalid, null, []);

        public CharacterCreationResourcesResult<CharacterCreationResourcesReceipt> Confirm(
            CharacterCreationResourcesConfirmRequest request) =>
            new(CharacterCreationResourcesOutcomes.Invalid, null, []);

        public CharacterCreationResourcesResult<CharacterCreationResourcesReceipt> LookupReceipt(
            CharacterCreationResourcesReceiptLookupRequest request) =>
            new(CharacterCreationResourcesOutcomes.NotFound, null, []);
    }
}
