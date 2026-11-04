using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

/// <summary>
/// Presentation-layer tests for <see cref="CharacterCreationSkillsInteractionPresenter"/>.
/// The fake Core service replays owner-bound Load/Preview/Confirm results, so these tests
/// prove the presenter contract only: owner-stamp guarding, catalog projection round-trip,
/// digest guard, and honest failure mapping. Core rule semantics (budget math, group
/// derivation, knowledge overflow) stay Core-owned.
/// </summary>
[TestClass]
public sealed partial class CharacterCreationSkillsInteractionPresenterTests
{
    [TestMethod]
    public void Load_with_valid_owner_stamp_projects_catalog_and_budgets()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeSkillsService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        CharacterCreationSkillsInteractionLoadResult load =
            presenter.Load(overview);

        Assert.AreEqual("ok", load.Outcome, load.Error);
        Assert.IsNotNull(load.State);
        Assert.AreEqual("Pistols", load.State.State.Skills[0].Name);
        Assert.AreEqual("Firearms", load.State.State.SkillGroups[0].Name);
        Assert.AreEqual(46, load.State.State.ActiveSkillPointBudget.Total);
        Assert.AreEqual(10, load.State.State.SkillGroupPointBudget.Total);
        Assert.AreEqual(0, load.State.State.KnowledgeSkillPointBudget.Total);
        Assert.IsTrue(load.State.State.CanEdit);
        Assert.AreEqual(1, service.LoadCalls);
    }

    [TestMethod]
    public void Prepare_round_trips_allocations_and_digest_through_confirm()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeSkillsService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        presenter.Load(overview);
        var allocations = new List<CharacterCreationSkillAllocation>
        {
            new("pistols", "active", 4, SpecializationOptionId: null, IsNativeLanguage: false),
            new("perception", "active", 2, SpecializationOptionId: null, IsNativeLanguage: false),
        };
        var groupAllocations = new List<CharacterCreationSkillGroupAllocation>
        {
            new("firearms", 2),
        };

        CharacterCreationSkillsInteractionPrepareResult prepared =
            presenter.Prepare(overview, allocations, groupAllocations);

        Assert.AreEqual("ok", prepared.Outcome, prepared.Error);
        CharacterCreationSkillsInteractionPreparedPreview preview = RequirePrepared(prepared);
        Assert.AreEqual(fixture.State.Binding, preview.State.State.Binding);
        Assert.IsFalse(string.IsNullOrWhiteSpace(preview.Preview.PreviewDigest));
        Assert.IsTrue(preview.Preview.CanConfirm);
        Assert.IsTrue(preview.Preview.RequiresExplicitConfirmation);
        Assert.AreEqual(1, service.PreviewCalls);
        Assert.AreEqual(1, service.LastPreviewGroupAllocations?.Count ?? 0);

        var confirmation = new CharacterCreationSkillsInteractionConfirmation(
            preview.State,
            allocations,
            groupAllocations,
            preview.Preview.PreviewDigest,
            IdempotencyKey: "skills-confirm-1",
            ExplicitlyConfirmed: true);
        CharacterCreationSkillsInteractionConfirmResult confirmed =
            presenter.Confirm(overview, confirmation);

        Assert.AreEqual("ok", confirmed.Outcome, confirmed.Error);
        Assert.IsNotNull(confirmed.Receipt);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.AreEqual(preview.Preview.PreviewDigest, service.LastConfirmDigest,
            "the presenter must round-trip Core's own digest, never invent one");
        Assert.AreEqual(1, service.LastConfirmExplicitlyConfirmed);
        Assert.AreEqual(46 - (4 + 2), service.LastConfirmReceipt?.ActivePointsRemaining,
            "the receipt must report the budget remaining after applying the confirmed allocations");
    }

    [TestMethod]
    public void Confirm_with_mismatched_digest_is_rejected()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeSkillsService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        presenter.Load(overview);
        CharacterCreationSkillsInteractionPrepareResult prepared =
            presenter.Prepare(
                overview,
                [new CharacterCreationSkillAllocation("pistols", "active", 2, null, false)],
                [new CharacterCreationSkillGroupAllocation("firearms", 1)]);
        CharacterCreationSkillsInteractionPreparedPreview preview = RequirePrepared(prepared);

        var tampered = new CharacterCreationSkillsInteractionConfirmation(
            preview.State,
            [new CharacterCreationSkillAllocation("pistols", "active", 2, null, false)],
            [new CharacterCreationSkillGroupAllocation("firearms", 1)],
            "sha256:" + new string('f', 64),
            IdempotencyKey: "skills-confirm-tampered",
            ExplicitlyConfirmed: true);
        CharacterCreationSkillsInteractionConfirmResult confirmed =
            presenter.Confirm(overview, tampered);

        Assert.AreNotEqual("ok", confirmed.Outcome);
        Assert.IsNull(confirmed.Receipt);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Load_without_owner_stamp_fails_honestly()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeSkillsService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);
        overview = overview with { DisplayOwnerContext = default };

        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        CharacterCreationSkillsInteractionLoadResult load = presenter.Load(overview);

        Assert.AreNotEqual("ok", load.Outcome);
        Assert.IsNull(load.State);
        Assert.AreEqual(0, service.LoadCalls,
            "no Core load may happen without a valid owner stamp");
        Assert.IsFalse(string.IsNullOrWhiteSpace(load.Error));
    }

    [TestMethod]
    public void Confirm_without_explicit_confirmation_is_rejected()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeSkillsService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        presenter.Load(overview);
        CharacterCreationSkillsInteractionPrepareResult prepared =
            presenter.Prepare(
                overview,
                [new CharacterCreationSkillAllocation("pistols", "active", 1, null, false)],
                [new CharacterCreationSkillGroupAllocation("firearms", 1)]);
        CharacterCreationSkillsInteractionPreparedPreview preview = RequirePrepared(prepared);

        var unconfirmed = new CharacterCreationSkillsInteractionConfirmation(
            preview.State,
            [new CharacterCreationSkillAllocation("pistols", "active", 1, null, false)],
            [new CharacterCreationSkillGroupAllocation("firearms", 1)],
            preview.Preview.PreviewDigest,
            IdempotencyKey: "skills-confirm-unconfirmed",
            ExplicitlyConfirmed: false);
        CharacterCreationSkillsInteractionConfirmResult result =
            presenter.Confirm(overview, unconfirmed);

        Assert.AreNotEqual("ok", result.Outcome);
        Assert.IsNull(result.Receipt);
        Assert.AreEqual(0, service.ConfirmCalls,
            "the presentation gate must stop unconfirmed requests before Core is called");
    }

    [TestMethod]
    [DataRow("owner")]
    [DataRow("workspace")]
    [DataRow("revision")]
    public void Changed_context_cannot_prepare_or_confirm_original_selections(string transition)
    {
        var fixture = CreateFixture(); var service = new FakeSkillsService(fixture.State);
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        var prepared = RequirePrepared(presenter.Prepare(overview, [new("pistols", "active", 2, null, false)], []));
        var changed = ChangeContext(overview, fixture.State, service, transition);
        Assert.AreNotEqual("ok", presenter.Confirm(changed, Confirmation(prepared)).Outcome);
        Assert.AreNotEqual("ok", presenter.Prepare(changed, prepared.Allocations, []).Outcome);
        Assert.AreEqual(1, service.PreviewCalls); Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Caller_mutation_cannot_change_reviewed_skills_or_groups()
    {
        var service = new FakeSkillsService(CreateFixture().State); var overview = CreateOverview(service.State, service);
        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        var allocations = new[] { new CharacterCreationSkillAllocation("pistols", "active", 2, "spec", false) };
        var groups = new[] { new CharacterCreationSkillGroupAllocation("firearms", 1) };
        var prepared = RequirePrepared(presenter.Prepare(overview, allocations, groups));
        allocations[0] = allocations[0] with { Rating = 6 }; groups[0] = groups[0] with { Rating = 6 };
        Assert.AreEqual(2, prepared.Allocations[0].Rating); Assert.AreEqual(1, prepared.GroupAllocations[0].Rating);
        Assert.AreNotEqual("ok", presenter.Confirm(overview, Confirmation(prepared) with { Allocations = allocations, GroupAllocations = groups }).Outcome);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Confirmation_is_sent_once_even_if_response_is_lost(bool lost)
    {
        var service = new FakeSkillsService(CreateFixture().State) { LoseConfirmResponse = lost };
        var overview = CreateOverview(service.State, service); var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        var prepared = RequirePrepared(presenter.Prepare(overview, [new("pistols", "active", 2, null, false)], []));
        var result = presenter.Confirm(overview, Confirmation(prepared));
        Assert.AreEqual(lost ? "confirmation-outcome-uncertain" : "ok", result.Outcome);
        Assert.AreNotEqual("ok", presenter.Confirm(overview, Confirmation(prepared) with { IdempotencyKey = "new-key" }).Outcome);
        Assert.AreNotEqual("ok", presenter.Prepare(overview, prepared.Allocations, []).Outcome);
        Assert.AreEqual(1, service.ConfirmCalls);
    }

    [TestMethod]
    public void Changed_snapshot_rejects_confirmation_before_mutation()
    {
        var service = new FakeSkillsService(CreateFixture().State); var overview = CreateOverview(service.State, service);
        var presenter = new CharacterCreationSkillsInteractionPresenter(service);
        var prepared = RequirePrepared(presenter.Prepare(overview, [], []));
        service.State = service.State with { SnapshotDigest = "changed" };
        Assert.AreNotEqual("ok", presenter.Confirm(overview, Confirmation(prepared)).Outcome);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    private static CharacterCreationSkillsInteractionConfirmation Confirmation(CharacterCreationSkillsInteractionPreparedPreview p)
        => new(p.State, p.Allocations, p.GroupAllocations, p.Preview.PreviewDigest, p.IdempotencyKey, true);

    private static CharacterOverviewState ChangeContext(CharacterOverviewState overview, CharacterCreationSkillsState state, FakeSkillsService service, string transition)
        => transition switch
        {
            "owner" => overview with { DisplayOwnerContext = new OwnerContextStamp(OwnerScope.LocalSingleUser, "another-owner", 1) },
            "workspace" => CreateOverview(state with { Binding = state.Binding with { WorkspaceId = new("another-workspace") } }, service),
            "revision" => CreateOverview(state with { Binding = state.Binding with { ContentRevision = state.Binding.ContentRevision + 1 } }, service),
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

    private static CharacterCreationSkillsInteractionPreparedPreview RequirePrepared(
        CharacterCreationSkillsInteractionPrepareResult result)
        => result.Preview
           ?? throw new AssertFailedException("Expected a prepared skills preview.");

    private static CharacterOverviewState CreateOverview(
        CharacterCreationSkillsState state,
        FakeSkillsService service)
    {
        CharacterWorkspaceId workspaceId = state.Binding.WorkspaceId;
        WorkspaceSessionState session = new(
            workspaceId,
            [new OpenWorkspaceState(
                workspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-08-23T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                state.Binding.ContentRevision,
                state.Binding.SavedRevision)],
            [workspaceId]);
        return new WorkspaceOverviewStateFactory(
            ownerBoundCreationSkillsService: service).CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            CreateLoadedOverview(
                FixtureContent,
                state.Binding.ContentRevision,
                state.Binding.SavedRevision),
            restoredView: null,
            hasSavedWorkspace: true)
            with
        {
            // The interaction presenters are owner-bound; the display stamp proves
            // the presenter may speak for this overview (FinalizationOwnerTests pattern).
            DisplayOwnerContext = new OwnerContextStamp(
                OwnerScope.LocalSingleUser, "skills-test-issuer", 0),
        };
    }

    private static WorkspaceOverviewLoadResult CreateLoadedOverview(
        string content,
        long contentRevision,
        long savedRevision)
        => new(
            new CharacterProfileSection(
                "Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
                CharacterCreationBuildMethods.Priority, "Standard", Created: false,
                Adept: false, Magician: false, Technomancer: false, AI: false,
                MainMugshotIndex: 0, MugshotCount: 0),
            new CharacterProgressSection(
                12m, 5000m, 0m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6m,
                0, 0, false, false, false),
            new CharacterSkillsSection(0, 0, []),
            new CharacterRulesSection("SR5", "default.xml", "Standard", 25, 0, 0, 3, []),
            new CharacterBuildSection(
                CharacterCreationBuildMethods.Priority,
                "A", "B", "C", "D", "E", "Mundane", 10,
                0, 0, 0, 12, 3),
            new CharacterMovementSection("0", "0", "0", "0", "0", "0", 0, 0),
            new CharacterAwakeningSection(
                false, false, false, false, false, false, false, 0, 0,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                0, 0, 0, 0, 0),
            contentRevision,
            savedRevision,
            new WorkspaceDocument(content, RulesetDefaults.Sr5));

    private const string FixtureContent =
        "<character><created>False</created><gameedition>SR5</gameedition></character>";

    private static Fixture CreateFixture()
    {
        string contentDigest = RawSha(FixtureContent);
        var workspaceId = new CharacterWorkspaceId("ws-creation-skills");
        var binding = new CharacterCreationSkillsBinding(
            workspaceId,
            ContentRevision: 7,
            SavedRevision: 0,
            RawCharacterXmlDigest: contentDigest,
            AuxiliaryStateDigest: new WorkspaceDocument(FixtureContent, RulesetDefaults.Sr5).AuxiliaryStateDigest,
            PrerequisiteDraftRevision: 0,
            PrerequisiteDraftDigest: Digest('0'),
            PrerequisiteAuthorityDigest: Digest('1'),
            AttributesDraftRevision: 1,
            AttributesDraftDigest: Digest('2'),
            SkillsAuthorityDigest: Digest('3'),
            RuntimeDigest: Digest('4'),
            ContributionInputsDigest: Digest('5'));
        var skillRow = new CharacterCreationSkillProjection(
            "pistols", "active", "Pistols", "Combat", "AGI", "Firearms",
            Rating: 0, EffectiveRating: 0, PointCost: 0,
            SpecializationOptionId: null, SpecializationName: null,
            IsNativeLanguage: false, IsEnabled: true, Blockers: [], SourceAnchorIds: []);
        var knowledgeRow = new CharacterCreationSkillProjection(
            "security-design", "knowledge", "Security Design", "Interests", "LOG", null,
            Rating: 0, EffectiveRating: 0, PointCost: 0,
            SpecializationOptionId: null, SpecializationName: null,
            IsNativeLanguage: false, IsEnabled: true, Blockers: [], SourceAnchorIds: []);
        var groupRow = new CharacterCreationSkillGroupProjection(
            "firearms", "Firearms", Rating: 0, PointCost: 0,
            ["pistols"], IsEnabled: true, Blockers: [], SourceAnchorIds: []);
        var budgets = Budgets(
            totalActive: 46, usedActive: 0,
            totalGroup: 10, usedGroup: 0,
            totalKnowledge: 0, usedKnowledge: 0);
        CharacterCreationSkillsState state = WithSnapshotDigest(new CharacterCreationSkillsState(
            CharacterCreationSkillsSchemas.SnapshotV1,
            binding,
            Authority: new CharacterCreationSkillsAuthority(
                CharacterCreationSkillsSchemas.AuthorityV1,
                SettingsProfileId: "skills-test",
                EffectiveSkillsInputsDigest: Digest('6'),
                RawProfileInputsDigest: Digest('6'),
                KnowledgePointsExpression: "0",
                MaxActiveSkillRatingCreate: 6,
                MaxKnowledgeSkillRatingCreate: 6,
                MaxSkillGroupRatingCreate: 6,
                BaseNativeLanguageLimit: 2,
                UsePointsOnBrokenGroups: false,
                StrictSkillGroupsInCreateMode: true,
                SpecializationsBreakSkillGroups: false,
                ActiveSkills: [],
                KnowledgeSkills: [],
                SkillGroups: [],
                KnowledgePointContributions: [],
                SourceAnchorIds: [],
                Blockers: [],
                IsAuthoritative: true,
                RuntimeDigest: Digest('9'),
                AuthorityDigest: Digest('a')),

            PrerequisiteDraft: null,
            AttributesDraft: null,
            PendingDraft: null,
            Skills: [skillRow, knowledgeRow],
            SkillGroups: [groupRow],
            KnowledgePointContributions: [],
            budgets.Active, budgets.Group, budgets.Knowledge,
            IntuitionUnaugmented: 1,
            LogicUnaugmented: 1,
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty));
        return new Fixture(state);
    }

    private static (CharacterCreationBudgetState Active, CharacterCreationBudgetState Group, CharacterCreationBudgetState Knowledge)
        Budgets(int totalActive, int usedActive, int totalGroup, int usedGroup, int totalKnowledge, int usedKnowledge)
    {
        return (
            new CharacterCreationBudgetState("active", "Active", totalActive, usedActive, totalActive - usedActive, IsExact: true, [], "points"),
            new CharacterCreationBudgetState("group", "Skill groups", totalGroup, usedGroup, totalGroup - usedGroup, IsExact: true, [], "points"),
            new CharacterCreationBudgetState("knowledge", "Knowledge", totalKnowledge, usedKnowledge, totalKnowledge - usedKnowledge, IsExact: true, [], "points"));
    }

    private static CharacterCreationSkillsState WithSnapshotDigest(CharacterCreationSkillsState state)
        => state with { SnapshotDigest = RawSha(state.ToString() ?? string.Empty) };

    private static string RawSha(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Digest(char value) => new(value, 64);

    private sealed record Fixture(CharacterCreationSkillsState State);

    private sealed class FakeSkillsService : IOwnerBoundCharacterCreationSkillsService
    {
        public Action? BeforeLoad { get; set; }
        public bool LoseConfirmResponse { get; set; }
        public IReadOnlyList<CharacterCreationSkillAllocation>? LastPreviewAllocations { get; private set; }
        public int LoadCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public string? LastConfirmDigest { get; private set; }
        public int LastConfirmExplicitlyConfirmed { get; private set; }
        public IReadOnlyList<CharacterCreationSkillGroupAllocation>? LastPreviewGroupAllocations { get; private set; }
        public CharacterCreationSkillsReceipt? LastConfirmReceipt { get; private set; }

        public CharacterCreationFoundationResult<CharacterCreationSkillsState> Load(
            OwnerContextStamp expectedOwner,
            CharacterCreationSkillsLoadRequest request)
        {
            BeforeLoad?.Invoke();
            LoadCalls++;
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationSkillsState>(CharacterCreationFoundationOutcomes.Invalid);
            if (!string.Equals(request.WorkspaceId.Value, State.Binding.WorkspaceId.Value, StringComparison.Ordinal))
                return Denied<CharacterCreationSkillsState>(CharacterCreationFoundationOutcomes.Missing);
            return new CharacterCreationFoundationResult<CharacterCreationSkillsState>(
                CharacterCreationFoundationOutcomes.Success, State, Array.Empty<string>());
        }

        public CharacterCreationFoundationResult<CharacterCreationSkillsPreview> Preview(
            OwnerContextStamp expectedOwner,
            CharacterCreationSkillsPreviewRequest request)
        {
            PreviewCalls++;
            LastPreviewAllocations = request.Allocations.ToArray();
            LastPreviewGroupAllocations = request.GroupAllocations;
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationSkillsPreview>(CharacterCreationFoundationOutcomes.Invalid);
            int activeUsed = request.Allocations
                .Where(a => string.Equals(a.Kind, "active", StringComparison.Ordinal))
                .Sum(a => a.Rating ?? 0);
            int groupUsed = request.GroupAllocations.Sum(g => g.Rating);
            int knowledgeUsed = request.Allocations
                .Where(a => string.Equals(a.Kind, "knowledge", StringComparison.Ordinal))
                .Sum(a => a.Rating ?? 0);
            var skills = State.Skills.Select(row =>
            {
                CharacterCreationSkillAllocation? allocation = request.Allocations
                    .FirstOrDefault(a => string.Equals(a.SourceSkillId, row.SourceSkillId, StringComparison.Ordinal));
                int rating = allocation?.Rating ?? row.Rating ?? 0;
                return row with
                {
                    Rating = rating,
                    EffectiveRating = rating,
                    PointCost = rating,
                    SpecializationOptionId = allocation?.SpecializationOptionId ?? row.SpecializationOptionId,
                };
            }).ToArray();
            var groups = State.SkillGroups.Select(row =>
            {
                CharacterCreationSkillGroupAllocation? allocation = request.GroupAllocations
                    .FirstOrDefault(g => string.Equals(g.GroupId, row.GroupId, StringComparison.Ordinal));
                int rating = allocation?.Rating ?? row.Rating;
                return row with
                {
                    Rating = rating,
                    PointCost = rating,
                };
            }).ToArray();
            var preview = new CharacterCreationSkillsPreview(
                CharacterCreationSkillsSchemas.PreviewV1,
                State.Binding,
                skills,
                groups,
                State.KnowledgePointContributions,
                Budget("active", 46, activeUsed),
                Budget("group", 10, groupUsed),
                Budget("knowledge", 0, knowledgeUsed),
                KnowledgePointOverflowToActive: 0,
                Blockers: [],
                RequiresExplicitConfirmation: true,
                CanConfirm: true,
                PreviewDigest: PreviewDigestFor(request.Allocations, request.GroupAllocations));
            return new CharacterCreationFoundationResult<CharacterCreationSkillsPreview>(
                CharacterCreationFoundationOutcomes.Success, preview, Array.Empty<string>());
        }

        public CharacterCreationFoundationResult<CharacterCreationSkillsReceipt> Confirm(
            OwnerContextStamp expectedOwner,
            CharacterCreationSkillsConfirmRequest request)
        {
            ConfirmCalls++;
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationSkillsReceipt>(CharacterCreationFoundationOutcomes.Invalid);
            if (request.PreviewDigest != PreviewDigestFor(request.Allocations, request.GroupAllocations))
                return Denied<CharacterCreationSkillsReceipt>(CharacterCreationFoundationOutcomes.Conflict);
            if (!request.ExplicitlyConfirmed)
                return Denied<CharacterCreationSkillsReceipt>(CharacterCreationFoundationOutcomes.Invalid);
            LastConfirmDigest = request.PreviewDigest;
            LastConfirmExplicitlyConfirmed = request.ExplicitlyConfirmed ? 1 : 0;
            int activeUsed = request.Allocations
                .Where(a => string.Equals(a.Kind, "active", StringComparison.Ordinal))
                .Sum(a => a.Rating ?? 0);
            var receipt = new CharacterCreationSkillsReceipt(
                CharacterCreationSkillsSchemas.ReceiptV1,
                State.Binding.WorkspaceId,
                PreviousContentRevision: State.Binding.ContentRevision,
                ContentRevision: State.Binding.ContentRevision + 1,
                SavedRevision: State.Binding.SavedRevision,
                DraftRevision: (State.PendingDraft?.DraftRevision ?? 0) + 1,
                DraftDigest: RawSha("confirm"),
                PreviewDigest: request.PreviewDigest,
                IdempotencyKeyDigest: RawSha(request.IdempotencyKey),
                CommandDigest: RawSha("command"),
                PreviousReceiptDigest: Digest('a'),
                SkillsAuthorityDigest: State.Binding.SkillsAuthorityDigest,
                RuntimeDigest: State.Binding.RuntimeDigest,
                ActivePointsRemaining: 46 - activeUsed,
                SkillGroupPointsRemaining: 10 - request.GroupAllocations.Sum(g => g.Rating),
                KnowledgePointsRemaining: 0,
                KnowledgePointOverflowToActive: 0,
                CharacterDocumentChanged: true,
                ReceiptDigest: RawSha("receipt"));
            LastConfirmReceipt = receipt;
            if (LoseConfirmResponse) throw new System.IO.IOException("response lost after commit");
            return new CharacterCreationFoundationResult<CharacterCreationSkillsReceipt>(
                CharacterCreationFoundationOutcomes.Success, receipt, Array.Empty<string>());
        }

        public CharacterCreationSkillsState State { get; set; }

        public FakeSkillsService(CharacterCreationSkillsState state)
        {
            State = state;
        }

        private static CharacterCreationBudgetState Budget(string id, int total, int used)
            => new(id, id, total, used, total - used, IsExact: true, [], "points");

        private static string PreviewDigestFor(
            IReadOnlyList<CharacterCreationSkillAllocation> allocations,
            IReadOnlyList<CharacterCreationSkillGroupAllocation> groupAllocations)
            => RawSha("preview|" + string.Join(
                ";",
                allocations.Select(a => a.SourceSkillId + ":" + (a.Rating ?? 0) + ":" + a.SpecializationOptionId + ":" + a.IsNativeLanguage)
                    .Concat(groupAllocations.Select(g => g.GroupId + ":" + g.Rating))));

        private static CharacterCreationFoundationResult<T> Denied<T>(string outcome)
            where T : class
            => new(outcome, null, Array.Empty<string>());
    }
}
