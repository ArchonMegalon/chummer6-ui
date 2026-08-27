using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerQualityWorkflowTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("workspace-1");
    private static readonly Guid SourceId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProposedId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TransactionId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CorrectionId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime ExpenseDate =
        new(2081, 5, 12, 14, 30, 0, DateTimeKind.Unspecified);

    [TestMethod]
    public async Task Projection_draft_review_commit_and_restart_recovery_are_exact()
    {
        var workspace = new FakeAtomicWorkspace();
        var presenter = new CareerQualityInteractionPresenter(workspace);

        CareerQualityEditorState projected = await presenter.ProjectAsync(
            WorkspaceId,
            CancellationToken.None);
        Assert.AreEqual("owner-1", projected.OwnerId);
        Assert.AreEqual(41L, projected.WorkspaceRevision);
        Assert.AreEqual(19L, projected.SavedRevision);
        Assert.AreEqual(H('a'), projected.RuntimeFingerprint);
        Assert.AreEqual(H('b'), projected.ContentDigest);
        Assert.AreEqual(0, projected.OmittedCandidateCount);
        Assert.HasCount(1, projected.Quotes);
        Assert.HasCount(0, projected.RecoverableReceipts);

        CharacterCareerQualityQuote quote = projected.Quotes.Single();
        Assert.AreEqual(CharacterCareerQualityOperation.AcquireLevel, quote.Operation);
        Assert.AreEqual(ProposedId, quote.Identity.InternalId);
        Assert.AreEqual(SourceId, quote.Identity.SourceId);
        Assert.AreEqual(10, quote.RuleKarmaCost);
        Assert.AreEqual(-10, quote.CharacterKarmaDelta);
        Assert.IsTrue(quote.CanApply);

        CareerQualityDraft draft = CareerQualityWorkflow.CreateDraft(projected, quote);
        CareerQualityReview review = await presenter.ReviewAsync(
            draft,
            CancellationToken.None);
        CareerQualityConfirmation committed = await presenter.ConfirmAsync(
            review,
            confirmed: true,
            TransactionId,
            ExpenseDate,
            CancellationToken.None);

        Assert.IsTrue(CharacterCareerQualityRules.IsCoherent(committed.Receipt));
        Assert.AreEqual(42L, committed.Receipt.WorkspaceRevisionAfter);
        Assert.AreEqual(20L, committed.Receipt.SavedRevisionAfter);
        Assert.AreEqual(90, committed.Receipt.CharacterKarmaAfter);
        Assert.AreEqual(TransactionId, committed.Receipt.ExpenseId);
        Assert.AreEqual(42L, committed.PersistedState.WorkspaceRevision);
        Assert.AreEqual(20L, committed.PersistedState.SavedRevision);
        Assert.HasCount(1, committed.PersistedState.RecoverableReceipts);
        Assert.AreEqual(
            committed.Receipt.ReceiptDigest,
            committed.PersistedState.RecoverableReceipts.Single().ReceiptDigest);

        CareerQualityEditorState reopened = await presenter.ProjectAsync(
            WorkspaceId,
            CancellationToken.None);
        CareerQualityEditorState restarted = CareerQualityWorkflow.Project(
            workspace.Snapshot);
        Assert.HasCount(1, reopened.RecoverableReceipts);
        Assert.AreEqual(
            reopened.RecoverableReceipts.Single().ReceiptDigest,
            restarted.RecoverableReceipts.Single().ReceiptDigest);
        Assert.AreEqual(CharacterCareerQualityOperation.RemoveAllLevels,
            reopened.Quotes.Single().Operation);
    }

    [TestMethod]
    public async Task Correction_is_compensating_durable_and_cannot_replay()
    {
        var workspace = new FakeAtomicWorkspace();
        var presenter = new CareerQualityInteractionPresenter(workspace);
        CareerQualityEditorState initial = await presenter.ProjectAsync(
            WorkspaceId,
            CancellationToken.None);
        CareerQualityReview review = await presenter.ReviewAsync(
            CareerQualityWorkflow.CreateDraft(initial, initial.Quotes.Single()),
            CancellationToken.None);
        CareerQualityConfirmation committed = await presenter.ConfirmAsync(
            review,
            true,
            TransactionId,
            ExpenseDate,
            CancellationToken.None);
        CharacterCareerQualityReceipt receipt = committed.Receipt;
        CareerQualityCorrectionRequest request = new(
            WorkspaceId,
            committed.PersistedState.OwnerId,
            committed.PersistedState.WorkspaceRevision,
            committed.PersistedState.SavedRevision,
            committed.PersistedState.RulesetId,
            receipt,
            receipt.ReceiptDigest,
            Confirmed: true,
            CorrectionId,
            "Undo mistaken quality purchase");

        CareerQualityCorrectionConfirmation corrected = await presenter.CorrectAsync(
            request,
            CancellationToken.None);
        Assert.IsTrue(CharacterCareerQualityRules.IsCoherent(corrected.Correction));
        Assert.AreEqual(43L, corrected.PersistedState.WorkspaceRevision);
        Assert.AreEqual(21L, corrected.PersistedState.SavedRevision);
        Assert.HasCount(0, corrected.PersistedState.RecoverableReceipts);
        Assert.HasCount(1, corrected.PersistedState.Quotes);
        Assert.AreEqual(CharacterCareerQualityOperation.AcquireLevel,
            corrected.PersistedState.Quotes.Single().Operation);

        CharacterCareerQualityExecutionBinding nextBinding = workspace.Snapshot.Binding with
        {
            WorkspaceRevision = workspace.Snapshot.Binding.WorkspaceRevision + 1,
            SavedRevision = workspace.Snapshot.Binding.SavedRevision + 1
        };
        CareerQualityAuthoritySnapshot laterSnapshot = workspace.Snapshot with
        {
            Binding = nextBinding,
            Candidates =
            [
                workspace.Snapshot.Candidates.Single() with { Binding = nextBinding }
            ]
        };
        CareerQualityEditorState historical = CareerQualityWorkflow.Project(laterSnapshot);
        Assert.AreEqual(0, historical.OmittedReceiptCount);
        Assert.HasCount(0, historical.RecoverableReceipts);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            presenter.CorrectAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public void Every_review_and_execution_binding_fails_closed()
    {
        var workspace = new FakeAtomicWorkspace();
        CareerQualityEditorState state = CareerQualityWorkflow.Project(workspace.Snapshot);
        CareerQualityDraft draft = CareerQualityWorkflow.CreateDraft(
            state,
            state.Quotes.Single());

        foreach (Func<CareerQualityDraft, CareerQualityDraft> mutate in new Func<CareerQualityDraft, CareerQualityDraft>[]
        {
            value => value with { ExpectedOwnerId = "owner-2" },
            value => value with { ExpectedWorkspaceRevision = value.ExpectedWorkspaceRevision + 1 },
            value => value with { ExpectedSavedRevision = value.ExpectedSavedRevision + 1 },
            value => value with { ExpectedRulesetId = "sr6" },
            value => value with { ExpectedLogicalRevision = H('0') },
            value => value with { ExpectedSourceRevision = H('0') },
            value => value with { ExpectedRuleDigest = H('0') },
            value => value with { ExpectedRuntimeFingerprint = H('0') },
            value => value with { ExpectedContentDigest = H('0') },
            value => value with { Identity = value.Identity with { InternalId = Guid.NewGuid() } }
        })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                CareerQualityWorkflow.Review(workspace.Snapshot, mutate(draft)));
        }

        CareerQualityReview review = CareerQualityWorkflow.Review(workspace.Snapshot, draft);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.PlanConfirmation(
                workspace.Snapshot,
                review,
                confirmed: false,
                TransactionId,
                ExpenseDate));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.PlanConfirmation(
                workspace.Snapshot,
                review,
                confirmed: true,
                Guid.Empty,
                ExpenseDate));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.PlanConfirmation(
                workspace.Snapshot with
                {
                    ReservedTransactionIds = [TransactionId]
                },
                review,
                confirmed: true,
                TransactionId,
                ExpenseDate));
    }

    [TestMethod]
    public void Candidate_selection_never_uses_labels_and_ambiguity_is_omitted()
    {
        var workspace = new FakeAtomicWorkspace();
        CharacterCareerQualityInput first = workspace.Snapshot.Candidates.Single();
        Guid otherSource = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid otherInternal = Guid.Parse("44444444-4444-4444-4444-444444444444");
        CharacterCareerQualityInput sameLabel = first with
        {
            Identity = new CharacterCareerQualityIdentity(otherInternal, otherSource),
            Definition = first.Definition with { SourceId = otherSource }
        };
        CareerQualityAuthoritySnapshot twoLabels = workspace.Snapshot with
        {
            Candidates = [first, sameLabel]
        };
        CareerQualityEditorState state = CareerQualityWorkflow.Project(twoLabels);
        Assert.HasCount(2, state.Quotes);
        Assert.AreEqual(2, state.Quotes.Select(value => value.Identity.SourceId).Distinct().Count());
        CareerQualityDraft selected = CareerQualityWorkflow.CreateDraft(
            state,
            state.Quotes.Single(value => value.Identity.SourceId == otherSource));
        Assert.AreEqual(otherSource, selected.Identity.SourceId);

        CareerQualityEditorState duplicate = CareerQualityWorkflow.Project(
            workspace.Snapshot with { Candidates = [first, first] });
        Assert.HasCount(0, duplicate.Quotes);
        Assert.AreEqual(2, duplicate.OmittedCandidateCount);
    }

    [TestMethod]
    public void Core_blockers_surface_but_unprovable_candidates_are_omitted()
    {
        var workspace = new FakeAtomicWorkspace();
        CharacterCareerQualityInput input = workspace.Snapshot.Candidates.Single();
        CharacterCareerQualityInput unsupported = input with
        {
            Effects = input.Effects with
            {
                UnsupportedFamilies = [CharacterCareerQualityEffectFamily.ChoiceSelection]
            }
        };
        CareerQualityEditorState blocked = CareerQualityWorkflow.Project(
            workspace.Snapshot with { Candidates = [unsupported] });
        Assert.HasCount(1, blocked.Quotes);
        Assert.IsFalse(blocked.Quotes.Single().CanApply);
        Assert.AreEqual(
            CharacterCareerQualityBlocker.UnsupportedEffectFamily,
            blocked.Quotes.Single().Blocker);

        CharacterCareerQualityInput unprovable = input with
        {
            Identity = input.Identity with { SourceId = Guid.NewGuid() }
        };
        CareerQualityEditorState omitted = CareerQualityWorkflow.Project(
            workspace.Snapshot with { Candidates = [unprovable] });
        Assert.HasCount(0, omitted.Quotes);
        Assert.AreEqual(1, omitted.OmittedCandidateCount);
    }

    [TestMethod]
    public async Task Foreign_workspace_and_unavailable_authority_fail_closed()
    {
        var workspace = new FakeAtomicWorkspace();
        var presenter = new CareerQualityInteractionPresenter(workspace);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            presenter.ProjectAsync(new CharacterWorkspaceId("other"), CancellationToken.None));

        workspace.ReturnUnavailable = true;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            presenter.ProjectAsync(WorkspaceId, CancellationToken.None));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.Project(workspace.Snapshot with
            {
                Binding = workspace.Snapshot.Binding with
                {
                    RuntimeFingerprint = "not-a-digest"
                }
            }));
    }

    [TestMethod]
    public void Tampered_commit_receipt_and_persisted_recovery_are_rejected()
    {
        var workspace = new FakeAtomicWorkspace();
        CareerQualityEditorState state = CareerQualityWorkflow.Project(workspace.Snapshot);
        CareerQualityReview review = CareerQualityWorkflow.Review(
            workspace.Snapshot,
            CareerQualityWorkflow.CreateDraft(state, state.Quotes.Single()));
        CharacterCareerQualityPlan plan = CareerQualityWorkflow.PlanConfirmation(
            workspace.Snapshot,
            review,
            true,
            TransactionId,
            ExpenseDate);
        CareerQualityAtomicCommitResult committed = workspace.Commit(plan);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.ValidateAtomicCommit(
                review,
                plan,
                committed with
                {
                    Receipt = committed.Receipt with { ReceiptDigest = H('0') }
                }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.ValidateAtomicCommit(
                review,
                plan,
                committed with
                {
                    Plan = committed.Plan with
                    {
                        SavedCharacterKarma = committed.Plan.SavedCharacterKarma + 1
                    }
                }));

        CareerQualityAuthoritySnapshot tamperedRecovery = committed.PersistedSnapshot with
        {
            PersistedReceipts =
            [
                committed.PersistedSnapshot.PersistedReceipts.Single() with
                {
                    ObservedExpense = committed.ObservedExpense with
                    {
                        MatchingEntryCount = 2
                    }
                }
            ]
        };
        CareerQualityEditorState recovered = CareerQualityWorkflow.Project(tamperedRecovery);
        Assert.HasCount(0, recovered.RecoverableReceipts);
        Assert.AreEqual(1, recovered.OmittedReceiptCount);
    }

    [TestMethod]
    public void Replayed_transaction_and_invalid_correction_marker_fail_closed()
    {
        var workspace = new FakeAtomicWorkspace();
        CareerQualityEditorState initial = CareerQualityWorkflow.Project(workspace.Snapshot);
        CareerQualityReview review = CareerQualityWorkflow.Review(
            workspace.Snapshot,
            CareerQualityWorkflow.CreateDraft(initial, initial.Quotes.Single()));
        CharacterCareerQualityPlan plan = CareerQualityWorkflow.PlanConfirmation(
            workspace.Snapshot,
            review,
            true,
            TransactionId,
            ExpenseDate);
        CareerQualityAtomicCommitResult committed = workspace.Commit(plan);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerQualityWorkflow.PlanConfirmation(
                committed.PersistedSnapshot,
                review,
                true,
                TransactionId,
                ExpenseDate));

        CharacterCareerQualityReceipt receipt = committed.Receipt;
        CareerQualityCorrectionRequest request = new(
            WorkspaceId,
            "owner-1",
            42,
            20,
            CharacterCareerQualityRules.RulesetId,
            receipt,
            receipt.ReceiptDigest,
            true,
            CorrectionId,
            "correct");
        CharacterCareerQualityCorrectionPlan correction =
            CareerQualityWorkflow.PlanCorrection(committed.PersistedSnapshot, request);
        CareerQualityAtomicCorrectionResult corrected = workspace.Correct(correction);
        CareerQualityAuthoritySnapshot tampered = corrected.PersistedSnapshot with
        {
            PersistedCorrections =
            [
                corrected.PersistedSnapshot.PersistedCorrections.Single() with
                {
                    ObservedExpense = new CharacterCareerQualityExpenseObservation(
                        1,
                        Guid.NewGuid(),
                        ExpenseDate,
                        0,
                        "tampered",
                        "Karma",
                        false,
                        false,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        0m,
                        string.Empty)
                }
            ]
        };
        CareerQualityEditorState state = CareerQualityWorkflow.Project(tampered);
        Assert.HasCount(0, state.RecoverableReceipts);
        Assert.IsGreaterThan(0, state.OmittedReceiptCount);

        CareerQualityAuthoritySnapshot zeroCountButNotEmpty =
            corrected.PersistedSnapshot with
            {
                PersistedCorrections =
                [
                    corrected.PersistedSnapshot.PersistedCorrections.Single() with
                    {
                        ObservedExpense = corrected.ObservedExpense with
                        {
                            Reason = "hidden expense residue"
                        }
                    }
                ]
            };
        CareerQualityEditorState residue = CareerQualityWorkflow.Project(
            zeroCountButNotEmpty);
        Assert.HasCount(0, residue.RecoverableReceipts);
        Assert.IsGreaterThan(0, residue.OmittedReceiptCount);
    }

    private static string H(char value) => new(value, 64);

    private sealed class FakeAtomicWorkspace : ICareerQualityAtomicWorkspace
    {
        private CharacterCareerQualityInput _candidate;
        private readonly List<CareerQualityPersistedReceiptProjection> _receipts = [];
        private readonly List<CareerQualityPersistedCorrectionProjection> _corrections = [];

        public FakeAtomicWorkspace()
        {
            _candidate = CreateInput();
        }

        public bool ReturnUnavailable { get; set; }

        public CareerQualityAuthoritySnapshot Snapshot => new(
            CharacterCareerQualityRules.RulesetId,
            _candidate.Binding,
            [_candidate],
            _receipts.ToArray(),
            _corrections.ToArray(),
            []);

        public Task<CareerQualityAuthoritySnapshot?> ReadAsync(
            CharacterWorkspaceId workspaceId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (ReturnUnavailable)
                return Task.FromResult<CareerQualityAuthoritySnapshot?>(null);
            return Task.FromResult<CareerQualityAuthoritySnapshot?>(Snapshot);
        }

        public Task<CareerQualityAtomicCommitResult?> CommitAsync(
            CharacterCareerQualityPlan plan,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<CareerQualityAtomicCommitResult?>(Commit(plan));
        }

        public CareerQualityAtomicCommitResult Commit(
            CharacterCareerQualityPlan plan)
        {
            if (!CharacterCareerQualityRules.TryCreateQuote(
                    _candidate,
                    out CharacterCareerQualityQuote reviewed)
                || reviewed.LogicalRevision != plan.ExpectedLogicalRevision
                || _receipts.Any(value => value.Receipt.TransactionId == plan.TransactionId))
            {
                throw new InvalidOperationException("Fake atomic CAS rejected the plan.");
            }

            CharacterCareerQualityExecutionBinding targetBinding = _candidate.Binding with
            {
                WorkspaceRevision = plan.TargetWorkspaceRevision,
                SavedRevision = plan.TargetSavedRevision
            };
            Assert.IsTrue(CharacterCareerQualityRules.TryCreateStateObservation(
                plan.Identity,
                plan.Definition,
                plan.Extra,
                plan.SourceName,
                plan.InstancesAfter,
                plan.SavedCharacterKarma,
                targetBinding,
                _candidate.RawSourceState,
                reviewed.RuleDigest,
                out CharacterCareerQualityStateObservation observed));
            CharacterCareerQualityExpenseObservation expense = Expense(plan);
            Assert.IsTrue(CharacterCareerQualityRules.TryCreateReceipt(
                plan.TransactionId,
                reviewed,
                plan,
                observed,
                expense,
                out CharacterCareerQualityReceipt receipt));

            _candidate = _candidate with
            {
                Operation = CharacterCareerQualityOperation.RemoveAllLevels,
                Identity = plan.Identity,
                ProposedInternalIdUnused = false,
                TargetOwnedByCharacter = true,
                AvailableKarma = plan.SavedCharacterKarma,
                MatchingInstances = plan.InstancesAfter,
                Binding = targetBinding
            };
            var persisted = new CareerQualityPersistedReceiptProjection(
                receipt,
                observed,
                expense);
            _receipts.Add(persisted);
            return new CareerQualityAtomicCommitResult(
                plan,
                receipt,
                observed,
                expense,
                Snapshot);
        }

        public Task<CareerQualityAtomicCorrectionResult?> CorrectAsync(
            CharacterCareerQualityCorrectionPlan correction,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<CareerQualityAtomicCorrectionResult?>(Correct(correction));
        }

        public CareerQualityAtomicCorrectionResult Correct(
            CharacterCareerQualityCorrectionPlan correction)
        {
            CareerQualityPersistedReceiptProjection persisted = _receipts.Single(
                value => value.Receipt.TransactionId == correction.OriginalTransactionId);
            CharacterCareerQualityReceipt receipt = persisted.Receipt;
            CharacterCareerQualityExecutionBinding targetBinding = _candidate.Binding with
            {
                WorkspaceRevision = correction.TargetWorkspaceRevision,
                SavedRevision = correction.TargetSavedRevision
            };
            Assert.IsTrue(CharacterCareerQualityRules.TryCreateStateObservation(
                receipt.Identity,
                receipt.Definition,
                receipt.Extra,
                receipt.SourceName,
                receipt.InstancesBefore,
                receipt.CharacterKarmaBefore,
                targetBinding,
                _candidate.RawSourceState,
                correction.ExpectedRuleDigest,
                out CharacterCareerQualityStateObservation restored));
            CharacterCareerQualityExpenseObservation noExpense = NoExpense();

            _candidate = _candidate with
            {
                Operation = CharacterCareerQualityOperation.AcquireLevel,
                Identity = receipt.Identity,
                ProposedInternalIdUnused = true,
                TargetOwnedByCharacter = false,
                AvailableKarma = receipt.CharacterKarmaBefore,
                MatchingInstances = receipt.InstancesBefore,
                Binding = targetBinding
            };
            _corrections.Add(new CareerQualityPersistedCorrectionProjection(
                correction,
                receipt,
                restored,
                noExpense));
            return new CareerQualityAtomicCorrectionResult(
                correction,
                restored,
                noExpense,
                Snapshot);
        }

        private static CharacterCareerQualityInput CreateInput()
            => new(
                CharacterCareerQualityOperation.AcquireLevel,
                new CharacterCareerQualityIdentity(ProposedId, SourceId),
                Created: true,
                RulesetId: CharacterCareerQualityRules.RulesetId,
                DefinitionProjectionIsExact: true,
                IdentityProjectionIsExact: true,
                ProposedInternalIdUnused: true,
                TargetOwnedByCharacter: false,
                GmAllows: true,
                GmFreeCostApproved: false,
                HasMentorSpiritWay: false,
                MetagenicLimit: 0,
                AvailableKarma: 100,
                Extra: string.Empty,
                SourceName: string.Empty,
                Definition: new CharacterCareerQualityDefinition(
                    SourceId,
                    "Test Quality",
                    CharacterCareerQualityType.Positive,
                    BaseKarma: 5,
                    Implemented: true,
                    SourceEnabled: true,
                    CareerOnly: false,
                    ChargenOnly: false,
                    OnlyPriorityGiven: false,
                    DoubleCostCareer: true,
                    StagedPurchase: false,
                    RefundKarmaOnRemove: false,
                    NoLevels: false,
                    LimitIsUnlimited: false,
                    LevelLimit: 3,
                    Metagenic: false,
                    ContributeToBp: true,
                    CostDiscountDefined: false,
                    CostDiscountProjectionIsExact: true,
                    CostDiscountRequirementsMet: false,
                    CostDiscountValue: 0),
                Settings: new CharacterCareerQualitySettings(1, false, false),
                Eligibility: new CharacterCareerQualityEligibilityProjection(
                    IsExact: true,
                    GeneralRequirementsMet: true,
                    RequiredOneOfQualityMet: true,
                    RequiredOneOfMetatypeMet: true,
                    RequiredAllQualitiesMet: true,
                    ForbiddenQualitiesClear: true,
                    ConflictingQualityInternalIds: [],
                    MissingRequirementIds: [],
                    ProjectionDigest: H('e')),
                Effects: new CharacterCareerQualityEffectProjection(
                    IsExact: true,
                    AppliedFamilies: [],
                    UnsupportedFamilies: [],
                    MutationCount: 0,
                    DeltaDigest: H('f')),
                MatchingInstances: [],
                Binding: new CharacterCareerQualityExecutionBinding(
                    "owner-1",
                    WorkspaceId.Value,
                    WorkspaceRevision: 41,
                    SavedRevision: 19,
                    RuntimeFingerprint: H('a'),
                    ContentDigest: H('b')),
                RawSourceState: "source-state",
                RawRuleState: "rule-state");

        private static CharacterCareerQualityExpenseObservation Expense(
            CharacterCareerQualityPlan plan)
            => plan.CreatesExpense
                ? new CharacterCareerQualityExpenseObservation(
                    MatchingEntryCount: 1,
                    plan.ExpenseId,
                    plan.ExpenseDateLocal,
                    plan.ExpenseAmount,
                    plan.ExpenseReason,
                    plan.ExpenseType,
                    plan.ExpenseRefund,
                    plan.ForceCareerVisible,
                    plan.KarmaUndoType,
                    plan.NuyenUndoType,
                    plan.UndoObjectId,
                    plan.UndoQuantity,
                    plan.UndoExtra)
                : NoExpense();

        private static CharacterCareerQualityExpenseObservation NoExpense()
            => new(
                MatchingEntryCount: 0,
                ExpenseId: Guid.Empty,
                ExpenseDateLocal: DateTime.MinValue,
                Amount: 0,
                Reason: string.Empty,
                ExpenseType: string.Empty,
                Refund: false,
                ForceCareerVisible: false,
                KarmaUndoType: string.Empty,
                NuyenUndoType: string.Empty,
                UndoObjectId: string.Empty,
                UndoQuantity: 0m,
                UndoExtra: string.Empty);
    }
}
