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
/// Presentation-layer tests for <see cref="CharacterCreationAttributesInteractionPresenter"/>.
/// The fake Core service replays owner-bound Load/Preview/Confirm results, so these tests
/// prove the presenter contract only: owner-stamp guarding, digest round-trip, and honest
/// failure mapping. Core rule semantics (budget math, karma levels) stay Core-owned.
/// </summary>
[TestClass]
public sealed partial class CharacterCreationAttributesInteractionPresenterTests
{
    [TestMethod]
    public void Load_with_valid_owner_stamp_projects_budgets_and_rows()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        CharacterCreationAttributesInteractionLoadResult load =
            presenter.Load(overview);

        Assert.AreEqual("ok", load.Outcome, load.Error);
        Assert.IsNotNull(load.State);
        Assert.AreEqual("BOD", load.State.Attributes[0].AttributeId);
        Assert.AreEqual(24, load.State.NormalPointBudget.Total);
        Assert.AreEqual(6, load.State.SpecialPointBudget.Total);
        Assert.IsTrue(load.State.CanEdit);
        Assert.AreEqual(1, service.LoadCalls);
    }

    [TestMethod]
    public void Prepare_round_trips_allocations_and_digest_through_confirm()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        presenter.Load(overview);
        var allocations = new List<CharacterCreationAttributeAllocation>
        {
            new("BOD", 3, 0),
            new("AGI", 0, 1),
        };

        CharacterCreationAttributesInteractionPrepareResult prepared =
            presenter.Prepare(overview, allocations);

        Assert.AreEqual("ok", prepared.Outcome);
        CharacterCreationAttributesPreparedPreview preview = RequirePrepared(prepared);
        Assert.AreEqual(fixture.State.Binding, preview.Binding);
        Assert.IsFalse(string.IsNullOrWhiteSpace(preview.PreviewDigest));
        Assert.IsTrue(preview.CanConfirm);
        Assert.IsTrue(preview.RequiresExplicitConfirmation);
        Assert.AreEqual(1, service.PreviewCalls);

        var confirmation = new CharacterCreationAttributesConfirmation(
            allocations, preview.PreviewDigest, ExplicitlyConfirmed: true);
        CharacterCreationAttributesInteractionConfirmResult confirmed =
            presenter.Confirm(overview, confirmation);

        Assert.AreEqual("ok", confirmed.Outcome);
        Assert.IsNotNull(confirmed.State);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.AreEqual(preview.PreviewDigest, service.LastConfirmDigest,
            "the presenter must round-trip Core's own digest, never invent one");
        Assert.AreEqual(1, service.LastConfirmExplicitlyConfirmed);
    }

    [TestMethod]
    public void Confirm_with_mismatched_digest_is_rejected()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);

        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        presenter.Load(overview);
        CharacterCreationAttributesInteractionPrepareResult prepared =
            presenter.Prepare(overview, [new CharacterCreationAttributeAllocation("BOD", 2, 0)]);
        CharacterCreationAttributesPreparedPreview preview = RequirePrepared(prepared);

        var tampered = new CharacterCreationAttributesConfirmation(
            [new CharacterCreationAttributeAllocation("BOD", 2, 0)],
            "sha256:" + new string('f', 64),
            ExplicitlyConfirmed: true);
        CharacterCreationAttributesInteractionConfirmResult confirmed =
            presenter.Confirm(overview, tampered);

        Assert.AreNotEqual("ok", confirmed.Outcome);
        Assert.IsNull(confirmed.State);
        Assert.AreEqual(0, service.ConfirmCalls, "a substituted preview must be rejected before dispatch");
    }

    [TestMethod]
    public void Load_without_owner_stamp_fails_honestly()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        CharacterOverviewState overview = CreateOverview(fixture.State, service);
        overview = overview with { DisplayOwnerContext = default };

        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        CharacterCreationAttributesInteractionLoadResult load = presenter.Load(overview);

        Assert.AreNotEqual("ok", load.Outcome);
        Assert.IsNull(load.State);
        Assert.AreEqual(0, service.LoadCalls,
            "no Core load may happen without a valid owner stamp");
        Assert.IsFalse(string.IsNullOrWhiteSpace(load.Error));
    }

    [TestMethod]
    public void Confirm_without_explicit_confirmation_is_rejected()
    {
        Fixture implied =
            CreateFixture();
        var service = new FakeAttributesService(implied.State);
        CharacterOverviewState overview = CreateOverview(implied.State, service);

        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        presenter.Load(overview);
        CharacterCreationAttributesInteractionPrepareResult prepared =
            presenter.Prepare(overview, [new CharacterCreationAttributeAllocation("BOD", 1, 0)]);
        CharacterCreationAttributesPreparedPreview preview = RequirePrepared(prepared);

        var unconfirmed = new CharacterCreationAttributesConfirmation(
            [new CharacterCreationAttributeAllocation("BOD", 1, 0)],
            preview.PreviewDigest,
            ExplicitlyConfirmed: false);
        CharacterCreationAttributesInteractionConfirmResult result =
            presenter.Confirm(overview, unconfirmed);

        Assert.AreNotEqual("ok", result.Outcome);
        Assert.IsNull(result.State);
        Assert.AreEqual(0, service.ConfirmCalls,
            "the presentation gate must stop unconfirmed requests before Core is called");
    }

    private static CharacterCreationAttributesPreparedPreview RequirePrepared(
        CharacterCreationAttributesInteractionPrepareResult result)
        => result.PreparedPreview
           ?? throw new AssertFailedException("Expected a prepared attributes preview.");

    [TestMethod]
    [DataRow("workspace")]
    [DataRow("owner")]
    [DataRow("revision")]
    public void Editor_cannot_prepare_or_confirm_after_context_transition(string transition)
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        var original = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var allocation = new[] { new CharacterCreationAttributeAllocation("BOD", 2, 0) };
        var review = RequirePrepared(presenter.Prepare(original, allocation));
        var changed = ChangeContext(original, fixture.State, service, transition);
        Assert.IsFalse(presenter.IsCurrentContext(changed));
        Assert.AreNotEqual("ok", presenter.Confirm(changed, new(allocation, review.PreviewDigest, true)).Outcome);
        Assert.AreNotEqual("ok", presenter.Prepare(changed, allocation).Outcome);
        Assert.AreEqual(1, service.PreviewCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Confirm_rejects_changed_snapshot_without_dispatch()
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var allocation = new[] { new CharacterCreationAttributeAllocation("BOD", 2, 0) };
        var review = RequirePrepared(presenter.Prepare(overview, allocation));
        service.State = service.State with { SnapshotDigest = Digest('f') };
        Assert.AreNotEqual("ok", presenter.Confirm(overview, new(allocation, review.PreviewDigest, true)).Outcome);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Prepared_allocations_are_copied_and_changed_input_cannot_confirm()
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var allocation = new List<CharacterCreationAttributeAllocation> { new("BOD", 2, 0) };
        var review = RequirePrepared(presenter.Prepare(overview, allocation));
        allocation[0] = new("BOD", 5, 0);
        Assert.AreNotEqual("ok", presenter.Confirm(overview, new(allocation, review.PreviewDigest, true)).Outcome);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Successful_confirmation_cannot_be_dispatched_twice()
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State);
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var allocation = new[] { new CharacterCreationAttributeAllocation("BOD", 2, 0) };
        var review = RequirePrepared(presenter.Prepare(overview, allocation));
        var command = new CharacterCreationAttributesConfirmation(allocation, review.PreviewDigest, true);
        Assert.AreEqual("ok", presenter.Confirm(overview, command).Outcome);
        Assert.AreNotEqual("ok", presenter.Confirm(overview, command).Outcome);
        Assert.AreEqual(1, service.ConfirmCalls);
    }

    [TestMethod]
    public void Lost_confirmation_response_cannot_reprepare_or_repeat_the_write()
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State) { LoseConfirmResponse = true };
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var allocation = new[] { new CharacterCreationAttributeAllocation("BOD", 2, 0) };
        var review = RequirePrepared(presenter.Prepare(overview, allocation));
        var command = new CharacterCreationAttributesConfirmation(allocation, review.PreviewDigest, true);
        Assert.AreEqual("confirmation-outcome-uncertain", presenter.Confirm(overview, command).Outcome);
        Assert.AreNotEqual("ok", presenter.Prepare(overview, allocation).Outcome);
        Assert.AreNotEqual("ok", presenter.Confirm(overview, command).Outcome);
        Assert.AreEqual(1, service.ConfirmCalls);
    }

    [TestMethod]
    public void Blocked_preview_retains_costs_and_budgets_without_enabling_confirmation()
    {
        var fixture = CreateFixture();
        var service = new FakeAttributesService(fixture.State) { PreviewCanConfirm = false };
        var overview = CreateOverview(fixture.State, service);
        var presenter = new CharacterCreationAttributesInteractionPresenter(service);
        var review = RequirePrepared(presenter.Prepare(overview, [new("BOD", 2, 0)]));
        Assert.IsFalse(review.CanConfirm);
        Assert.AreEqual(2, review.NormalPointBudget.Used);
        Assert.AreEqual(3, review.Attributes[0].Current);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    private static CharacterOverviewState ChangeContext(CharacterOverviewState original,
        CharacterCreationAttributesState state, FakeAttributesService service, string transition)
        => transition switch
        {
            "owner" => original with { DisplayOwnerContext = new OwnerContextStamp(OwnerScope.LocalSingleUser, "attributes-test-issuer", 1) },
            "workspace" => CreateOverview(state with { Binding = state.Binding with { WorkspaceId = new CharacterWorkspaceId("other-runner") } }, service),
            "revision" => CreateOverview(state with { Binding = state.Binding with { ContentRevision = state.Binding.ContentRevision + 1 } }, service),
            _ => throw new ArgumentException(transition),
        };

    private static CharacterOverviewState CreateOverview(
        CharacterCreationAttributesState state,
        FakeAttributesService service)
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
            ownerBoundCreationAttributesService: service).CreateLoadedState(
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
                OwnerScope.LocalSingleUser, "attributes-test-issuer", 0),
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
        var workspaceId = new CharacterWorkspaceId("ws-creation-attributes");
        var binding = new CharacterCreationAttributesBinding(
            workspaceId,
            ContentRevision: 7,
            SavedRevision: 0,
            RawCharacterXmlDigest: contentDigest,
            AuxiliaryStateDigest: new WorkspaceDocument(FixtureContent, RulesetDefaults.Sr5).AuxiliaryStateDigest,
            PrerequisiteDraftRevision: 0,
            PrerequisiteDraftDigest: Digest('0'),
            PrerequisiteAuthorityDigest: Digest('1'));
        var projection = new CharacterCreationAttributeProjection(
            "BOD", "Physical", 1, 6, 8, Current: 1,
            PriorityPointsSpent: 0, KarmaLevels: 0, PriorityPointCost: 0, KarmaCost: 0,
            IsEnabled: true, DisableReasons: [], SourceAnchorIds: []);
        var budgets = Budgets(
            totalNormal: 24, usedNormal: 0,
            totalSpecial: 6, usedSpecial: 0,
            totalKarma: 50, usedKarma: 0);
        CharacterCreationAttributesState state = WithSnapshotDigest(new CharacterCreationAttributesState(
            CharacterCreationAttributesSchemas.SnapshotV1,
            binding,
            PrerequisiteDraft: null,
            PendingDraft: null,
            Attributes: [projection],
            budgets.Normal, budgets.Special, budgets.Karma,
            MaxNumberMaxAttributesCreate: 0,
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty));
        return new Fixture(state);
    }

    private static (CharacterCreationBudgetState Normal, CharacterCreationBudgetState Special, CharacterCreationBudgetState Karma)
        Budgets(int totalNormal, int usedNormal, int totalSpecial, int usedSpecial, int totalKarma, int usedKarma)
    {
        return (
            new CharacterCreationBudgetState("normal", "Normal", totalNormal, usedNormal, totalNormal - usedNormal, IsExact: true, [], "points"),
            new CharacterCreationBudgetState("special", "Special", totalSpecial, usedSpecial, totalSpecial - usedSpecial, IsExact: true, [], "points"),
            new CharacterCreationBudgetState("karma", "Karma", totalKarma, usedKarma, totalKarma - usedKarma, IsExact: true, [], "karma"));
    }

    private static CharacterCreationAttributesState WithSnapshotDigest(CharacterCreationAttributesState state)
        => state with { SnapshotDigest = RawSha(state.ToString() ?? string.Empty) };

    private static string RawSha(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Digest(char value) => new(value, 64);

    private sealed record Fixture(CharacterCreationAttributesState State);

    private sealed class FakeAttributesService : IOwnerBoundCharacterCreationAttributesService
    {
        public bool LoseConfirmResponse { get; init; }
        public bool PreviewCanConfirm { get; init; } = true;
        public Action? BeforeLoad { get; set; }
        public int LoadCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public string? LastConfirmDigest { get; private set; }
        public int LastConfirmExplicitlyConfirmed { get; private set; }

        public CharacterCreationFoundationResult<CharacterCreationAttributesState> Load(
            OwnerContextStamp expectedOwner,
            CharacterCreationAttributesLoadRequest request)
        {
            LoadCalls++;
            BeforeLoad?.Invoke();
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationAttributesState>(CharacterCreationFoundationOutcomes.Invalid);
            if (!string.Equals(request.WorkspaceId.Value, State.Binding.WorkspaceId.Value, StringComparison.Ordinal))
                return Denied<CharacterCreationAttributesState>(CharacterCreationFoundationOutcomes.Missing);
            return new CharacterCreationFoundationResult<CharacterCreationAttributesState>(
                CharacterCreationFoundationOutcomes.Success, State, Array.Empty<string>());
        }

        public CharacterCreationFoundationResult<CharacterCreationAttributesPreview> Preview(
            OwnerContextStamp expectedOwner,
            CharacterCreationAttributesPreviewRequest request)
        {
            PreviewCalls++;
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationAttributesPreview>(CharacterCreationFoundationOutcomes.Invalid);
            int normalUsed = request.Allocations.Sum(a => a.PriorityPoints);
            int specialUsed = request.Allocations.Sum(a => a.KarmaLevels);
            var rows = State.Attributes.Select(row =>
            {
                CharacterCreationAttributeAllocation? allocation = request.Allocations
                    .FirstOrDefault(a => string.Equals(a.AttributeId, row.AttributeId, StringComparison.Ordinal));
                return row with
                {
                    PriorityPointsSpent = allocation?.PriorityPoints ?? row.PriorityPointsSpent,
                    KarmaLevels = allocation?.KarmaLevels ?? row.KarmaLevels,
                    Current = row.Minimum + (allocation?.PriorityPoints ?? 0) + (allocation?.KarmaLevels ?? 0),
                    PriorityPointCost = allocation?.PriorityPoints ?? 0,
                    KarmaCost = 2 * (allocation?.KarmaLevels ?? 0),
                };
            }).ToArray();
            var preview = new CharacterCreationAttributesPreview(
                CharacterCreationAttributesSchemas.PreviewV1,
                State.Binding,
                rows,
                Budget("normal", 24, normalUsed),
                Budget("special", 6, specialUsed),
                Budget("karma", 50, 0),
                Blockers: [],
                RequiresExplicitConfirmation: true,
                CanConfirm: PreviewCanConfirm,
                PreviewDigest: PreviewDigestFor(request.Allocations));
            return new CharacterCreationFoundationResult<CharacterCreationAttributesPreview>(
                CharacterCreationFoundationOutcomes.Success, preview, Array.Empty<string>());
        }

        public CharacterCreationFoundationResult<CharacterCreationAttributesReceipt> Confirm(
            OwnerContextStamp expectedOwner,
            CharacterCreationAttributesConfirmRequest request)
        {
            ConfirmCalls++;
            if (LoseConfirmResponse)
            {
                State = State with { Binding = State.Binding with { ContentRevision = State.Binding.ContentRevision + 1 } };
                throw new System.IO.IOException("simulated committed write with lost response");
            }
            if (!expectedOwner.IsValid)
                return Denied<CharacterCreationAttributesReceipt>(CharacterCreationFoundationOutcomes.Invalid);
            if (request.PreviewDigest != PreviewDigestFor(request.Allocations))
                return Denied<CharacterCreationAttributesReceipt>(CharacterCreationFoundationOutcomes.Conflict);
            if (!request.ExplicitlyConfirmed)
                return Denied<CharacterCreationAttributesReceipt>(CharacterCreationFoundationOutcomes.Invalid);
            LastConfirmDigest = request.PreviewDigest;
            LastConfirmExplicitlyConfirmed = request.ExplicitlyConfirmed ? 1 : 0;
            return new CharacterCreationFoundationResult<CharacterCreationAttributesReceipt>(
                CharacterCreationFoundationOutcomes.Success,
                new CharacterCreationAttributesReceipt(
                    State.Binding.WorkspaceId,
                    PreviousContentRevision: State.Binding.ContentRevision,
                    ContentRevision: State.Binding.ContentRevision + 1,
                    SavedRevision: State.Binding.SavedRevision,
                    DraftRevision: (State.PendingDraft?.DraftRevision ?? 0) + 1,
                    DraftDigest: RawSha("confirm"),
                    NormalPointsRemaining: 24 - request.Allocations.Sum(a => a.PriorityPoints),
                    SpecialPointsRemaining: 6 - request.Allocations.Sum(a => a.KarmaLevels),
                    CreationKarmaRemaining: 50,
                    CharacterDocumentChanged: true),
                Array.Empty<string>());
        }

        public CharacterCreationAttributesState State { get; set; }

        public FakeAttributesService(CharacterCreationAttributesState state)
        {
            State = state;
        }

        private static CharacterCreationBudgetState Budget(string id, int total, int used)
            => new(id, id, total, used, total - used, IsExact: true, [], "points");

        private static string PreviewDigestFor(IReadOnlyList<CharacterCreationAttributeAllocation> allocations)
            => RawSha("preview|" + string.Join(
                ";",
                allocations.Select(a => a.AttributeId + ":" + a.PriorityPoints + ":" + a.KarmaLevels)));

        private static CharacterCreationFoundationResult<T> Denied<T>(string outcome)
            where T : class
            => new(outcome, null, Array.Empty<string>());
    }
}
