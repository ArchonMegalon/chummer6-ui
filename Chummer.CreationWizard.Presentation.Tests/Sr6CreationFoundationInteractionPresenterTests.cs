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
/// Lane tests for the SR6 creation wizard interaction boundary: the Core SR6
/// foundation service remains the sole authority; the presenter must enforce
/// owner/binding integrity before any Core call and never write on its own.
/// </summary>
[TestClass]
public sealed class Sr6CreationFoundationInteractionPresenterTests
{
    [TestMethod]
    public void Load_projects_foundation_state_and_blocks_after_character_created()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        var service = new FakeSr6FoundationService(foundation);
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);

        Sr6CreationFoundationInteractionLoadResult ok = presenter.Load(CreateOverview(foundation));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, ok.Outcome);
        Assert.IsNotNull(ok.State);
        CollectionAssert.AreEqual(
            new[] { "Human", "Elf" },
            ok.State.Foundation.Metatypes.Select(m => m.Id).ToArray());
        Assert.AreEqual(1, service.LoadCalls);

        CharacterOverviewState created = CreateOverview(foundation, created: true);
        Sr6CreationFoundationInteractionLoadResult blocked = presenter.Load(created);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, blocked.Outcome);
        CollectionAssert.Contains(
            blocked.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.CharacterAlreadyCreated);
        Assert.AreEqual(1, service.LoadCalls);
    }

    [TestMethod]
    public void Prepare_requires_owner_and_current_binding_before_any_core_preview()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        Sr6CreationFoundationPreview preview = CreatePreview(foundation);
        var service = new FakeSr6FoundationService(foundation) { PreviewResult = Success(preview) };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);

        CharacterOverviewState noOwner = CreateOverview(foundation) with { DisplayOwnerContext = null };
        Sr6CreationFoundationInteractionPrepareResult noOwnerResult = presenter.Prepare(
            noOwner,
            Selection());
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, noOwnerResult.Outcome);
        CollectionAssert.Contains(
            noOwnerResult.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.OwnerAuthorityRequired);
        Assert.AreEqual(0, service.LoadCalls);
        Assert.AreEqual(0, service.PreviewCalls);

        Sr6CreationFoundationInteractionPrepareResult ok = presenter.Prepare(
            CreateOverview(foundation),
            Selection());
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, ok.Outcome);
        Assert.IsNotNull(ok.PreparedPreview);
        Assert.AreEqual(preview.PreviewDigest, ok.PreparedPreview.PreviewDigest);
        Assert.AreEqual(1, service.LoadCalls);
        Assert.AreEqual(1, service.PreviewCalls);
    }

    [TestMethod]
    public void Confirm_gates_explicit_confirmation_and_rejects_digest_mismatch()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        Sr6CreationFoundationPreview preview = CreatePreview(foundation);
        var service = new FakeSr6FoundationService(foundation) { PreviewResult = Success(preview) };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);
        Sr6CreationFoundationPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            CreateOverview(foundation),
            Selection()));

        Sr6CreationFoundationInteractionConfirmResult unconfirmed = presenter.Confirm(
            CreateOverview(foundation),
            new Sr6CreationFoundationInteractionConfirmation(
                prepared,
                prepared.PreviewDigest,
                Guid.NewGuid(),
                ExplicitlyConfirmed: false));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, unconfirmed.Outcome);
        CollectionAssert.Contains(
            unconfirmed.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.PreviewNotConfirmable);
        Assert.AreEqual(0, service.ConfirmCalls);

        Sr6CreationFoundationInteractionConfirmResult digestMismatch = presenter.Confirm(
            CreateOverview(foundation),
            new Sr6CreationFoundationInteractionConfirmation(
                prepared,
                Digest('0'),
                Guid.NewGuid(),
                ExplicitlyConfirmed: true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, digestMismatch.Outcome);
        CollectionAssert.Contains(
            digestMismatch.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.PreparedPreviewMismatch);
        Assert.AreEqual(0, service.ConfirmCalls);

        Sr6CreationFoundationInteractionConfirmResult ok = presenter.Confirm(
            CreateOverview(foundation),
            new Sr6CreationFoundationInteractionConfirmation(
                prepared,
                prepared.PreviewDigest,
                Guid.NewGuid(),
                ExplicitlyConfirmed: true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, ok.Outcome);
        Assert.IsNotNull(ok.Commit);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.IsTrue(service.LastConfirmRequest!.ExplicitlyConfirmed);
        Assert.AreEqual(prepared.Preview.PreviewDigest, service.LastConfirmRequest.PreviewDigest);
    }

    [TestMethod]
    public void Confirm_rejects_a_valid_receipt_for_a_different_operation()
    {
        var foundation = CreateFoundation();
        var service = new FakeSr6FoundationService(foundation)
        {
            PreviewResult = Success(CreatePreview(foundation)), WrongReceiptOperation = true
        };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);
        var prepared = RequirePrepared(presenter.Prepare(CreateOverview(foundation), Selection()));
        var result = presenter.Confirm(CreateOverview(foundation),
            new(prepared, prepared.PreviewDigest, Guid.NewGuid(), true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        CollectionAssert.Contains(result.Blockers.ToArray(), Sr6CreationFoundationInteractionBlockers.ReceiptMismatch);
        Assert.AreEqual(1, service.ConfirmCalls);
    }

    [TestMethod]
    public void ReviewFinalization_rejects_owner_and_binding_mismatch_before_core()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        Sr6CreationFinalizationReview review = CreateReview(foundation, blockers: []);
        var service = new FakeSr6FoundationService(foundation) { FinalizationReview = review };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);

        CharacterOverviewState noOwner = CreateOverview(foundation) with { DisplayOwnerContext = null };
        Sr6CreationFinalizationInteractionReviewResult noOwnerResult = presenter.ReviewFinalization(
            noOwner,
            foundation.Binding);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, noOwnerResult.Outcome);
        CollectionAssert.Contains(
            noOwnerResult.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.OwnerAuthorityRequired);
        Assert.AreEqual(0, service.ReviewFinalizationCalls);

        Sr6CreationFinalizationInteractionReviewResult ok = presenter.ReviewFinalization(
            CreateOverview(foundation),
            foundation.Binding);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, ok.Outcome);
        Assert.IsNotNull(ok.Review);
        Assert.AreEqual(1, service.ReviewFinalizationCalls);
    }

    [TestMethod]
    public void ConfirmFinalization_requires_can_finalize_and_forwards_explicit_confirmation()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        Sr6CreationFinalizationReview review = CreateReview(foundation, blockers: []);
        var service = new FakeSr6FoundationService(foundation) { FinalizationReview = review };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);

        Sr6CreationFinalizationReview blockedReview = CreateReview(
            foundation,
            blockers: [Sr6CreationFinalizationBlockers.NotFinalized]);
        Sr6CreationFinalizationInteractionConfirmResult blocked = presenter.ConfirmFinalization(
            CreateOverview(foundation),
            blockedReview,
            acceptUnspentLoss: true);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, blocked.Outcome);
        CollectionAssert.Contains(
            blocked.Blockers.ToArray(),
            Sr6CreationFinalizationBlockers.NotFinalized);
        Assert.AreEqual(0, service.ConfirmFinalizationCalls);

        Sr6CreationFinalizationInteractionConfirmResult ok = presenter.ConfirmFinalization(
            CreateOverview(foundation),
            review,
            acceptUnspentLoss: true);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, ok.Outcome);
        Assert.IsNotNull(ok.Commit);
        Assert.AreEqual(1, service.ConfirmFinalizationCalls);
        Assert.IsTrue(service.LastFinalizationRequest!.ExplicitlyConfirmed);
        Assert.IsTrue(service.LastFinalizationRequest.AcceptUnspentLoss);
        Assert.AreEqual(foundation.Binding, service.LastFinalizationRequest.Binding);
        Assert.AreEqual(review.ReviewDigest, service.LastFinalizationRequest.ReviewDigest);
    }

    [TestMethod]
    public void ConfirmFinalization_binding_mismatch_fails_closed()
    {
        Sr6CreationFoundationState foundation = CreateFoundation();
        Sr6CreationFinalizationReview review = CreateReview(
            foundation,
            blockers: Array.Empty<string>());
        var service = new FakeSr6FoundationService(foundation) { FinalizationReview = review };
        var presenter = new Sr6CreationFoundationInteractionPresenter(service);

        Sr6CreationFinalizationReview foreignReview = CreateReview(
            foundation with
            {
                Binding = foundation.Binding with
                {
                    WorkspaceId = new CharacterWorkspaceId("ws-foreign"),
                },
            },
            blockers: Array.Empty<string>());
        Sr6CreationFinalizationInteractionConfirmResult result = presenter.ConfirmFinalization(
            CreateOverview(foundation),
            foreignReview,
            acceptUnspentLoss: true);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        CollectionAssert.Contains(
            result.Blockers.ToArray(),
            Sr6CreationFoundationInteractionBlockers.BindingMismatch);
        Assert.AreEqual(0, service.ConfirmFinalizationCalls);
    }

    // -------------------------------------------------------------------
    // Fixtures
    // -------------------------------------------------------------------

    private static Sr6CreationFoundationState CreateFoundation(
        long contentRevision = 7,
        long savedRevision = 3)
    {
        var binding = new Sr6CreationFoundationBinding(
            new CharacterWorkspaceId("ws-sr6"),
            contentRevision,
            savedRevision,
            Digest('a'),
            Digest('b'),
            Digest('c'));
        return new Sr6CreationFoundationState(
            binding,
            Sr6CharacterCreationBuildMethods.Priority,
            new[]
            {
                new Sr6CreationFoundationOption("Human", ["A", "B", "C", "D", "E"]),
                new Sr6CreationFoundationOption("Elf", ["A", "B", "C", "D", "E"]),
            },
            new[]
            {
                new Sr6CreationFoundationOption("adept", ["A", "B", "C"]),
                new Sr6CreationFoundationOption("magician", ["A", "B", "C"]),
            },
            Selection: null)
        {
            AttributeOptions = new[]
            {
                new Sr6CreationAttributeOption("Body", 1, 6, true, true),
            },
        };
    }

    private static Sr6CreationFoundationPreview CreatePreview(Sr6CreationFoundationState foundation)
    {
        Sr6CreationFoundationPreview preview = new(
            foundation.Binding,
            Selection(),
            new Sr6CreationPriorityBudget(
                AttributePoints: 24,
                SkillPoints: 46,
                ResourcesNuyen: 50000,
                MetatypeAdjustmentPoints: 6,
                MagicResonanceRank: "A"),
            BaseMagic: 1,
            BaseResonance: 0,
            ["core-rules"],
            Digest('p'));
        // The presenter recomputes the canonical digest with
        // Sr6CreationFoundationIntegrity — the fake must serve a preview whose
        // stored digest matches that recomputation.
        return preview with { PreviewDigest = Sr6CreationFoundationIntegrity.PreviewDigest(preview) };
    }

    private static Sr6CreationFoundationSelection Selection()
        => new(
            "Human",
            "adept",
            new[]
            {
                new Sr6CreationPriorityChoice("heritage", "B"),
                new Sr6CreationPriorityChoice("talent", "A"),
                new Sr6CreationPriorityChoice("attributes", "C"),
                new Sr6CreationPriorityChoice("skills", "D"),
                new Sr6CreationPriorityChoice("resources", "E"),
            });

    private static Sr6CreationFinalizationReview CreateReview(
        Sr6CreationFoundationState foundation,
        IReadOnlyList<string> blockers)
        => new(
            foundation.Binding,
            Document: null!,
            Digest('d'),
            new Sr6CreationDraftBalances(0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 0),
            Losses: [],
            blockers,
            ["core-rules"],
            Digest('r'));

    private static CharacterOverviewState CreateOverview(
        Sr6CreationFoundationState foundation,
        bool created = false)
    {
        WorkspaceSessionState session = CreateSession(foundation.Binding.ContentRevision);
        return CharacterOverviewState.Empty with
        {
            WorkspaceId = foundation.Binding.WorkspaceId,
            Session = session,
            DisplayOwnerContext = CreateOwner(),
            Profile = CreateProfile(created),
        };
    }

    private static WorkspaceSessionState CreateSession(long contentRevision)
    {
        var workspaceId = new CharacterWorkspaceId("ws-sr6");
        return new WorkspaceSessionState(
            workspaceId,
            [
                new OpenWorkspaceState(
                    workspaceId,
                    "Sr6",
                    "S",
                    DateTimeOffset.Parse("2026-11-06T00:00:00+00:00"),
                    RulesetDefaults.Sr6,
                    contentRevision,
                    SavedRevision: 3)
            ],
            [workspaceId]);
    }

    private static CharacterProfileSection CreateProfile(bool created)
        => new(
            "Sr6", "S", string.Empty, "Human", string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
            Sr6CharacterCreationBuildMethods.Priority, "Standard", created,
            false, false, false, false, 0, 0);

    private static OwnerContextStamp CreateOwner()
        => new(new OwnerScope("owner-a"), "authority-a", 1);

    private static Sr6CreationFoundationPreparedPreview RequirePrepared(
        Sr6CreationFoundationInteractionPrepareResult result)
        => result.PreparedPreview
           ?? throw new AssertFailedException("Expected a prepared preview.");

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static CharacterCreationFoundationResult<T> Success<T>(T value)
        where T : class
        => new(CharacterCreationFoundationOutcomes.Success, value, []);

    private sealed class FakeSr6FoundationService : ISr6CreationFoundationService
    {
        public FakeSr6FoundationService(Sr6CreationFoundationState foundation)
        {
            _foundation = foundation;
            PreviewResult = new CharacterCreationFoundationResult<Sr6CreationFoundationPreview>(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                ["fake-preview-blocked"]);
        }

        private readonly Sr6CreationFoundationState _foundation;
        private Sr6CreationFoundationState? _committed;

        public bool WrongReceiptOperation;
        public int LoadCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public int ReviewFinalizationCalls { get; private set; }
        public int ConfirmFinalizationCalls { get; private set; }
        public CharacterCreationFoundationResult<Sr6CreationFoundationPreview> PreviewResult { get; set; }
        public Sr6CreationFinalizationReview? FinalizationReview { get; set; }
        public Sr6CreationFoundationConfirmRequest? LastConfirmRequest { get; private set; }
        public Sr6CreationFinalizationRequest? LastFinalizationRequest { get; private set; }

        public CharacterCreationFoundationResult<Sr6CreationFoundationState> Load(
            OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
        {
            LoadCalls++;
            if (!string.Equals(_foundation.Binding.WorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal))
            {
                return new CharacterCreationFoundationResult<Sr6CreationFoundationState>(
                    CharacterCreationFoundationOutcomes.Blocked, null, ["fake-workspace-unavailable"]);
            }

            return Success(_committed ?? _foundation);
        }

        public CharacterCreationFoundationResult<Sr6CreationFoundationPreview> Preview(
            OwnerContextStamp owner,
            Sr6CreationFoundationBinding binding,
            Sr6CreationFoundationSelection selection)
        {
            PreviewCalls++;
            if (!BindingMatchesDraft(binding))
            {
                return new CharacterCreationFoundationResult<Sr6CreationFoundationPreview>(
                    CharacterCreationFoundationOutcomes.Conflict, null, ["fake-stale-binding"]);
            }

            return PreviewResult;
        }

        public CharacterCreationFoundationResult<Sr6CreationCharacterProjection> ProjectCharacter(
            OwnerContextStamp owner, Sr6CreationFoundationBinding binding)
            => new(CharacterCreationFoundationOutcomes.Blocked, null, ["fake-not-used"]);

        public CharacterCreationFoundationResult<Sr6CreationFinalizationReview> ReviewFinalization(
            OwnerContextStamp owner, Sr6CreationFoundationBinding binding)
        {
            ReviewFinalizationCalls++;
            return FinalizationReview is null
                ? new CharacterCreationFoundationResult<Sr6CreationFinalizationReview>(
                    CharacterCreationFoundationOutcomes.Blocked, null, ["fake-review-blocked"])
                : Success(FinalizationReview);
        }

        public CharacterCreationFoundationResult<Sr6CreationFinalizationCommit> ConfirmFinalization(
            OwnerContextStamp owner, Sr6CreationFinalizationRequest request)
        {
            ConfirmFinalizationCalls++;
            LastFinalizationRequest = request;
            return Success(new Sr6CreationFinalizationCommit(
                new Sr6CreationFinalizationReceipt(
                    request,
                    ContentRevision: 8,
                    SavedRevision: 4,
                    Digest('n'),
                    Digest('e')),
                Replayed: false));
        }

        public CharacterCreationFoundationResult<Sr6CreationFoundationCommit> Confirm(
            OwnerContextStamp owner, Sr6CreationFoundationConfirmRequest request)
        {
            ConfirmCalls++;
            LastConfirmRequest = request;
            Sr6CreationFoundationPreview preview = (PreviewResult.Value ?? CreatePreview(_foundation));
            Sr6CreationFoundationDecision seed = new(
                Sr6CreationFoundationDecision.SchemaV1,
                WrongReceiptOperation ? request with { OperationId = Guid.NewGuid() } : request,
                preview,
                CommittedContentRevision: 8,
                Digest('a'));
            Sr6CreationFoundationDecision decision = seed with
            {
                DecisionDigest = Sr6CreationFoundationIntegrity.DecisionDigest(seed),
            };
            _committed = _foundation with
            {
                Binding = _foundation.Binding with { ContentRevision = 8 },
            };
            return Success(new Sr6CreationFoundationCommit(decision, Replayed: false));
        }

        public CharacterCreationFoundationResult<Sr6CreationFinalizationReceipt> LoadFinalization(
            OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
            => new(CharacterCreationFoundationOutcomes.Missing, null, ["fake-not-used"]);

        private bool BindingMatchesDraft(Sr6CreationFoundationBinding binding)
            => string.Equals(binding.WorkspaceId.Value, _foundation.Binding.WorkspaceId.Value, StringComparison.Ordinal)
               && binding.ContentRevision == _foundation.Binding.ContentRevision
               && binding.SavedRevision == _foundation.Binding.SavedRevision;
    }
}
