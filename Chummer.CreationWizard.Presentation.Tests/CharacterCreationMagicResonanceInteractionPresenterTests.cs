using System;
using System.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

/// <summary>
/// Presenter-level regressions for the magic/resonance creation stage: owner-stamp
/// guarded load, prepare through the static presentation workflow, single-dispatch
/// confirm gate, and stale-context refusal. The fake owner-bound service wraps the
/// existing shared-interface StubMagicResonanceService, so Core rule semantics stay
/// exercised by the workflow/fixture tests while these tests pin the presenter
/// contract only.
/// </summary>
[TestClass]
public sealed class CharacterCreationMagicResonanceInteractionPresenterTests
{
    [TestMethod]
    public void Load_with_valid_owner_stamp_projects_state()
    {
        TestContext context = CreateTest();
        CharacterCreationMagicResonanceInteractionLoadResult load = context.Presenter.Load(context.Overview);

        Assert.AreEqual("ok", load.Outcome, load.Error);
        Assert.IsNotNull(load.State);
        Assert.AreEqual(context.WorkspaceId, load.State!.WorkspaceId);
        Assert.IsTrue(load.State.State.CanEdit);
        Assert.AreEqual(1, context.Service.LoadCalls);
    }

    [TestMethod]
    public void Load_without_owner_stamp_fails_honestly_and_never_touches_Core()
    {
        TestContext context = CreateTest();
        var overview = context.Overview with { DisplayOwnerContext = default };

        CharacterCreationMagicResonanceInteractionLoadResult load = context.Presenter.Load(overview);

        Assert.AreNotEqual("ok", load.Outcome);
        Assert.IsNull(load.State);
        Assert.AreEqual(0, context.Service.LoadCalls,
            "no Core load may happen without a valid owner stamp");
        Assert.IsFalse(string.IsNullOrWhiteSpace(load.Error));
    }

    [TestMethod]
    public void Load_with_stale_revision_fails_closed()
    {
        TestContext context = CreateTest();
        context.Service.Shared.StateForTests = context.Service.Shared.StateForTests with
        {
            Binding = context.Service.Shared.StateForTests.Binding with
            {
                ContentRevision = context.Service.Shared.StateForTests.Binding.ContentRevision + 1
            }
        };
        // The overview still reports the ORIGINAL revision while the service state
        // now carries the advanced one, so the presenter binding check fails closed.
        CharacterCreationMagicResonanceInteractionLoadResult load =
            context.Presenter.Load(context.Overview);

        Assert.AreNotEqual("ok", load.Outcome);
        Assert.IsNull(load.State);
    }

    [TestMethod]
    public void Prepare_reviews_through_Core_and_requires_explicit_confirmation()
    {
        TestContext context = CreateTest();
        CharacterCreationMagicResonanceInteractionLoadResult load = context.Presenter.Load(context.Overview);
        Assert.AreEqual("ok", load.Outcome, load.Error);

        CharacterCreationMagicResonanceInteractionPrepareResult prepared = context.Presenter.Prepare(
            context.Overview,
            CharacterCreationMagicResonanceTestFixture.CompleteSelections,
            mysticAdeptPowerPoints: 0);

        Assert.AreEqual("ok", prepared.Outcome, prepared.Error);
        Assert.IsNotNull(prepared.Preview);
        Assert.IsTrue(prepared.Preview!.Preview.CanConfirm);
        Assert.IsTrue(prepared.Preview.Preview.RequiresExplicitConfirmation);
        Assert.IsFalse(string.IsNullOrWhiteSpace(prepared.Preview.Preview.PreviewDigest));
        Assert.AreEqual(1, context.Service.PreviewCalls);
    }

    [TestMethod]
    public void Confirm_without_explicit_confirmation_is_refused_before_Core()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview preview = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        CharacterCreationMagicResonanceInteractionConfirmResult confirmed =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: false);

        Assert.AreNotEqual("ok", confirmed.Outcome);
        Assert.IsNull(confirmed.Receipt);
        Assert.AreEqual(0, context.Service.ConfirmCalls,
            "the presentation gate must stop unconfirmed requests before Core is called");
    }

    [TestMethod]
    public void Confirm_with_explicit_confirmation_round_trips_Core_digest()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview preview = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        CharacterCreationMagicResonanceInteractionConfirmResult confirmed =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: true);

        Assert.AreEqual("ok", confirmed.Outcome, confirmed.Error);
        Assert.IsNotNull(confirmed.Receipt);
        Assert.AreEqual(1, context.Service.ConfirmCalls);
        Assert.AreEqual(
            preview.Preview.PreviewDigest,
            context.Service.LastConfirmDigest,
            "the presenter must round-trip Core's own preview digest, never invent one");
        Assert.AreEqual(1, context.Service.LastConfirmExplicitlyConfirmed);
    }

    [TestMethod]
    public void Confirm_dispatches_exactly_once_even_across_lost_responses()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview preview = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        CharacterCreationMagicResonanceInteractionConfirmResult first =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: true);
        Assert.AreEqual("ok", first.Outcome, first.Error);

        // A second confirm attempt with the SAME prepared preview must be refused by
        // the presenter's one-dispatch gate (the preview object is stale by design).
        CharacterCreationMagicResonanceInteractionConfirmResult second =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: true);

        Assert.AreNotEqual("ok", second.Outcome);
        Assert.AreEqual(1, context.Service.ConfirmCalls);
    }

    [TestMethod]
    public void Confirm_with_throwing_preread_fails_recoverably_and_never_dispatches()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview preview = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        // Inject a read fault so the Chain-B pre-read cannot complete. Nothing has
        // been dispatched yet: the outcome must be recoverable, the one-dispatch
        // fence stays open, and Core's Confirm is never reached.
        context.Service.Shared.LoadThrows = true;
        CharacterCreationMagicResonanceInteractionConfirmResult result =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: true);

        Assert.AreEqual("creation-magic-confirm-preread-outcome-uncertain", result.Outcome, result.Error);
        Assert.IsNull(result.Receipt);
        Assert.AreEqual(0, context.Service.ConfirmCalls,
            "a pre-dispatch read fault must never reach Core's Confirm");

        // Recoverable: with the fault cleared, the SAME presenter can still confirm
        // through the ordinary path (the fence was never latched).
        context.Service.Shared.LoadThrows = false;
        CharacterCreationMagicResonanceInteractionConfirmResult recovered =
            context.Presenter.Confirm(context.Overview, preview, explicitlyConfirmed: true);
        Assert.AreEqual("ok", recovered.Outcome, recovered.Error);
    }

    [TestMethod]
    public void Confirm_after_lost_response_latches_fence_and_reconciles_by_read()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview prepared = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        // Genuine lost response: the stub commits the confirmation, THEN throws, so
        // the caller cannot know whether the operation landed.
        context.Service.Shared.LoseResponse = true;
        CharacterCreationMagicResonanceInteractionConfirmResult first =
            context.Presenter.Confirm(context.Overview, prepared, explicitlyConfirmed: true);
        Assert.AreEqual("creation-magic-confirm-outcome-uncertain", first.Outcome, first.Error);
        Assert.IsNull(first.Receipt);

        // The fence must stay latched: no automatic retry through this presenter.
        context.Service.Shared.LoseResponse = false;
        CharacterCreationMagicResonanceInteractionConfirmResult retry =
            context.Presenter.Confirm(context.Overview, prepared, explicitlyConfirmed: true);
        Assert.AreEqual("creation-magic-confirmation-already-dispatched", retry.Outcome);
        Assert.AreEqual(1, context.Service.ConfirmCalls,
            "a lost response must never trigger an automatic resubmit");

        // Reconciliation is by READ, not resubmit: the overview is refreshed from
        // the store's post-commit revision (the confirm advanced ContentRevision),
        // and a fresh presenter then sees the committed state by ordinary read.
        CharacterOverviewState refreshed = CreateOverview(context.Service);
        CharacterCreationMagicResonanceInteractionLoadResult fresh =
            new CharacterCreationMagicResonanceInteractionPresenter(context.Service).Load(refreshed);
        Assert.AreEqual("ok", fresh.Outcome, fresh.Error);
        Assert.IsNotNull(fresh.State);
        Assert.IsTrue(fresh.State!.State.PendingDraft is not null,
            "the lost confirm's committed state must be visible to a fresh read");
        // NOTE: CanEdit legitimately stays true on a plain re-read (the fixture
        // carries no blockers); the edit-refusal after a committed draft is
        // enforced by Core at Prepare time (DraftDuplicate), not at Load time.
    }

    [TestMethod]
    public void Changed_context_refuses_prepare_and_confirm()
    {
        TestContext context = CreateTest();
        context.Presenter.Load(context.Overview);
        CharacterCreationMagicResonanceInteractionPreparedPreview preview = RequirePrepared(
            context.Presenter.Prepare(
                context.Overview,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0));

        // Advance the service state revision and rebuild the overview from it: the
        // presenter must refuse both operations against the changed context.
        context.Service.State = context.Service.State with
        {
            Binding = context.Service.State.Binding with
            {
                ContentRevision = context.Service.State.Binding.ContentRevision + 1
            }
        };
        var changed = CreateOverview(context.Service);

        Assert.AreNotEqual(
            "ok",
            context.Presenter.Prepare(
                changed,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections,
                mysticAdeptPowerPoints: 0).Outcome);
        Assert.AreNotEqual(
            "ok",
            context.Presenter.Confirm(changed, preview, explicitlyConfirmed: true).Outcome);
        Assert.AreEqual(0, context.Service.ConfirmCalls,
            "the confirm against the changed context must not reach Core");
    }

    private static TestContext CreateTest()
    {
        var shared = new StubMagicResonanceService(
            CharacterCreationMagicResonanceTestFixture.CreateState(
                Digest(FixtureContent)));
        var service = new OwnerBoundStub(shared);
        var presenter = new CharacterCreationMagicResonanceInteractionPresenter(service);
        var overview = CreateOverview(service);
        return new TestContext(service, presenter, overview, FixtureWorkspaceId);
    }

    private static readonly CharacterWorkspaceId FixtureWorkspaceId =
        CharacterCreationMagicResonanceTestFixture.WorkspaceId;

    private const string FixtureContent =
        "<character><created>False</created><gameedition>SR5</gameedition></character>";

    private static string Digest(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static CharacterOverviewState CreateOverview(OwnerBoundStub service)
    {
        CharacterCreationMagicResonanceBinding binding = service.State.Binding;
        long contentRevision = binding.ContentRevision;
        CharacterWorkspaceId workspaceId = binding.WorkspaceId;
        WorkspaceSessionState session = new(
            workspaceId,
            [new OpenWorkspaceState(
                workspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-08-23T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                contentRevision,
                binding.SavedRevision)],
            [workspaceId]);
        return new WorkspaceOverviewStateFactory(
            creationMagicResonanceService: service.Shared,
            ownerBoundCreationMagicResonanceService: service).CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            CreateLoadedOverview(
                contentRevision,
                binding.SavedRevision),
            restoredView: null,
            hasSavedWorkspace: true)
            with
        {
            // The interaction presenters are owner-bound; the display stamp proves
            // the presenter may speak for this overview (FinalizationOwnerTests pattern).
            DisplayOwnerContext = new OwnerContextStamp(
                OwnerScope.LocalSingleUser, "magic-test-issuer", 0),
        };
    }

    private static WorkspaceOverviewLoadResult CreateLoadedOverview(
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
            new WorkspaceDocument(FixtureContent, RulesetDefaults.Sr5));

    private static CharacterCreationMagicResonanceInteractionPreparedPreview RequirePrepared(
        CharacterCreationMagicResonanceInteractionPrepareResult result)
        => result.Preview
           ?? throw new AssertFailedException("Expected a prepared magic/resonance preview.");

    private sealed record TestContext(
        OwnerBoundStub Service,
        CharacterCreationMagicResonanceInteractionPresenter Presenter,
        CharacterOverviewState Overview,
        CharacterWorkspaceId WorkspaceId);

    /// <summary>
    /// Owner-bound fake delegating to the shared-interface stub. Owner stamps are
    /// validated structurally (any valid stamp is accepted) so presenter-level
    /// ownership rules stay pinned without re-testing Core's stamp semantics.
    /// </summary>
    private sealed class OwnerBoundStub : IOwnerBoundCharacterCreationMagicResonanceService
    {
        private readonly StubMagicResonanceService _shared;

        public OwnerBoundStub(StubMagicResonanceService shared) => _shared = shared;

        public StubMagicResonanceService Shared => _shared;

        public CharacterCreationMagicResonanceState State
        {
            get => _shared.StateForTests;
            set => _shared.StateForTests = value;
        }

        public int LoadCalls => _shared.LoadCalls;
        public int PreviewCalls => _shared.PreviewCalls;
        public int ConfirmCalls => _shared.ConfirmCalls;
        public string? LastConfirmDigest => _shared.LastConfirmDigest;
        public int LastConfirmExplicitlyConfirmed => _shared.LastConfirmExplicitlyConfirmed ? 1 : 0;

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> Load(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonanceLoadRequest request)
            => expectedOwner.IsValid
                ? _shared.Load(request)
                : new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);

        public CharacterCreationFoundationResult<CharacterCreationMagicResonancePreview> Preview(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonancePreviewRequest request)
            => expectedOwner.IsValid
                ? _shared.Preview(request)
                : new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceReceipt> Confirm(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonanceConfirmRequest request)
            => expectedOwner.IsValid
                ? _shared.Confirm(request)
                : new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);
    }
}
