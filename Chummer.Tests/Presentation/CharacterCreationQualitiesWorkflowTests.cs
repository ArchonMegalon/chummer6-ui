using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterCreationQualitiesWorkflowTests
{
    [TestMethod]
    public void Project_preserves_core_cost_legality_followup_and_sources()
    {
        CharacterCreationQualitiesAuthoritySnapshot snapshot = Snapshot(
            ["positive"],
            Option("positive", CharacterCreationQualityType.Positive, 10, "choice-agility"),
            DisabledOption("negative"));

        CharacterCreationQualitiesEditorState state = CharacterCreationQualitiesWorkflow.Project(snapshot);

        Assert.AreEqual(10, state.Preview.PositiveQualityBudget.Used);
        Assert.AreEqual(15, state.Preview.KarmaRemaining);
        CharacterCreationQualitiesDesktopOption positive = state.Options.Single(item => item.OptionId == "positive");
        Assert.AreEqual("choice-agility", positive.FollowUpChoiceId);
        CollectionAssert.AreEqual(
            new[] { "qualities.xml#quality:positive" },
            positive.SourceAnchorIds.ToArray());
        CharacterCreationQualitiesDesktopOption disabled = state.Options.Single(item => item.OptionId == "negative");
        Assert.IsFalse(disabled.IsSelectable);
        Assert.AreEqual("quality-requirement-missing", disabled.DisableReasonKey);
    }

    [TestMethod]
    public void Draft_captures_only_stable_ids_and_review_rejects_revision_or_authority_drift()
    {
        CharacterCreationQualitiesAuthoritySnapshot snapshot = Snapshot(
            [],
            Option("positive", CharacterCreationQualityType.Positive, 10));
        CharacterCreationQualitiesEditorState state = CharacterCreationQualitiesWorkflow.Project(snapshot);
        CharacterCreationQualitiesDesktopDraft draft = CharacterCreationQualitiesWorkflow.CreateDraft(
            state,
            ["positive"]);

        CharacterCreationQualitiesReview review = CharacterCreationQualitiesWorkflow.Review(snapshot, draft);
        Assert.IsTrue(review.Preview.CanConfirm, string.Join(",", review.Preview.Blockers));
        Assert.AreEqual(10, review.Preview.PositiveQualityBudget.Used);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationQualitiesWorkflow.CreateDraft(state, ["invented"]));

        CharacterCreationQualitiesAuthoritySnapshot drifted = snapshot with
        {
            Input = snapshot.Input with
            {
                Binding = snapshot.Input.Binding with
                {
                    ContentRevision = snapshot.Input.Binding.ContentRevision + 1
                }
            }
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationQualitiesWorkflow.Review(drifted, draft));
    }

    [TestMethod]
    public void Confirmation_requires_explicit_consent_and_fresh_transaction()
    {
        CharacterCreationQualitiesAuthoritySnapshot snapshot = Snapshot(
            [],
            Option("positive", CharacterCreationQualityType.Positive, 10));
        CharacterCreationQualitiesEditorState state = CharacterCreationQualitiesWorkflow.Project(snapshot);
        CharacterCreationQualitiesReview review = CharacterCreationQualitiesWorkflow.Review(
            snapshot,
            CharacterCreationQualitiesWorkflow.CreateDraft(state, ["positive"]));
        Guid transactionId = Guid.NewGuid();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationQualitiesWorkflow.PlanConfirmation(
                snapshot, review, "quality-command", false, transactionId));
        CharacterCreationQualitiesDraftPlan plan = CharacterCreationQualitiesWorkflow.PlanConfirmation(
            snapshot, review, "quality-command", true, transactionId);
        Assert.IsFalse(plan.CharacterDocumentChanged);

        CharacterCreationQualitiesAuthoritySnapshot reserved = snapshot with
        {
            ReservedTransactionIds = [transactionId]
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationQualitiesWorkflow.PlanConfirmation(
                reserved, review, "quality-command", true, transactionId));
    }

    [TestMethod]
    public void Atomic_commit_validation_requires_receipt_draft_and_persisted_projection_parity()
    {
        CharacterCreationQualitiesAuthoritySnapshot snapshot = Snapshot(
            [],
            Option("positive", CharacterCreationQualityType.Positive, 10));
        CharacterCreationQualitiesEditorState state = CharacterCreationQualitiesWorkflow.Project(snapshot);
        CharacterCreationQualitiesReview review = CharacterCreationQualitiesWorkflow.Review(
            snapshot,
            CharacterCreationQualitiesWorkflow.CreateDraft(state, ["positive"]));
        CharacterCreationQualitiesDraftPlan plan = CharacterCreationQualitiesWorkflow.PlanConfirmation(
            snapshot, review, "quality-command", true, Guid.NewGuid());
        string draftDigest = Digest('d');
        CharacterCreationQualitiesDraftReceipt receipt = Receipt(plan, draftDigest);
        CharacterCreationQualitiesAuthoritySnapshot persisted = snapshot with
        {
            Input = snapshot.Input with
            {
                Binding = snapshot.Input.Binding with
                {
                    ContentRevision = plan.TargetContentRevision,
                    SavedRevision = plan.TargetSavedRevision
                },
                SelectedOptionIds = ["positive"]
            },
            PersistedReceipts = [receipt]
        };
        var committed = new CharacterCreationQualitiesAtomicCommitResult(
            plan,
            receipt,
            draftDigest,
            persisted);

        CharacterCreationQualitiesEditorState persistedProjection =
            CharacterCreationQualitiesWorkflow.Project(persisted);
        Assert.AreEqual(plan.TargetContentRevision, persistedProjection.ContentRevision);
        Assert.AreEqual(plan.TargetSavedRevision, persistedProjection.SavedRevision);
        Assert.AreEqual(plan.AuthorityDigest, persistedProjection.AuthorityDigest);
        CollectionAssert.AreEqual(
            plan.Selections.Select(static item => item.OptionId).OrderBy(static item => item).ToArray(),
            persistedProjection.SelectedOptionIds.OrderBy(static item => item).ToArray());
        Assert.IsTrue(persisted.PersistedReceipts.Any(candidate =>
            candidate.TransactionId == plan.TransactionId
            && CharacterCreationQualitiesRules.DigestsEqual(
                candidate.ReceiptDigest,
                receipt.ReceiptDigest)));

        CharacterCreationQualitiesConfirmation confirmation =
            CharacterCreationQualitiesWorkflow.ValidateAtomicCommit(review, plan, committed);
        Assert.AreEqual(receipt.ReceiptDigest, confirmation.Receipt.ReceiptDigest);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationQualitiesWorkflow.ValidateAtomicCommit(
                review,
                plan,
                committed with { ObservedDraftDigest = Digest('e') }));
    }

    private static CharacterCreationQualitiesAuthoritySnapshot Snapshot(
        IReadOnlyList<string> selected,
        params CharacterCreationQualityCatalogOption[] options)
    {
        CharacterCreationQualitiesAuthority authority = Authority(options);
        var binding = new CharacterCreationQualitiesBinding(
            new CharacterWorkspaceId("quality-workspace"),
            ContentRevision: 12,
            SavedRevision: 12,
            RawCharacterXmlDigest: Digest('5'),
            AuxiliaryStateDigest: Digest('6'),
            PrerequisiteDraftRevision: 2,
            PrerequisiteDraftDigest: Digest('7'),
            AttributesDraftRevision: 3,
            AttributesDraftDigest: Digest('8'),
            RulesetId: "sr5",
            BuildMethod: CharacterCreationBuildMethods.Priority,
            CharacterCreated: false,
            CreationKarmaTotal: 25,
            CreationKarmaUsedBeforeQualities: 0,
            AuthorityDigest: authority.AuthorityDigest,
            RuntimeDigest: authority.RuntimeDigest);
        return new CharacterCreationQualitiesAuthoritySnapshot(
            new CharacterCreationQualitiesInput(binding, authority, selected),
            [],
            []);
    }

    private static CharacterCreationQualityCatalogOption Option(
        string id,
        CharacterCreationQualityType type,
        int karma,
        string? followUp = null)
    {
        var option = new CharacterCreationQualityCatalogOption(
            id,
            Guid.NewGuid(),
            id,
            id,
            type,
            Rating: 1,
            KarmaCost: karma,
            MaximumSelections: 1,
            IsMetagenic: false,
            CountsAgainstQualityLimit: true,
            CountsAgainstKarma: true,
            IsFreeOrGranted: false,
            IsSelectable: true,
            EligibilityIsExact: true,
            DisableReasonKey: null,
            FollowUpChoiceId: followUp,
            FollowUpChoiceLabel: followUp is null ? null : "Agility",
            SourceAnchorIds: [$"qualities.xml#quality:{id}"],
            OptionDigest: string.Empty);
        return option with
        {
            OptionDigest = CharacterCreationQualitiesRules.ComputeOptionDigest(option)
        };
    }

    private static CharacterCreationQualityCatalogOption DisabledOption(string id)
    {
        CharacterCreationQualityCatalogOption option = Option(
            id, CharacterCreationQualityType.Negative, -5) with
        {
            IsSelectable = false,
            EligibilityIsExact = false,
            DisableReasonKey = "quality-requirement-missing",
            OptionDigest = string.Empty
        };
        return option with
        {
            OptionDigest = CharacterCreationQualitiesRules.ComputeOptionDigest(option)
        };
    }

    private static CharacterCreationQualitiesAuthority Authority(
        IReadOnlyList<CharacterCreationQualityCatalogOption> options)
    {
        var authority = new CharacterCreationQualitiesAuthority(
            CharacterCreationQualitiesSchemas.AuthorityV1,
            "sr5",
            "settings-profile",
            QualityKarmaLimit: 25,
            MayExceedPositiveQualityLimit: false,
            MayExceedNegativeQualityLimit: false,
            MetagenicLimit: 0,
            Options: options,
            GrantedQualities: [],
            SourceAnchorIds: ["qualities.xml"],
            Blockers: [],
            IsAuthoritative: true,
            SourceDigest: Digest('1'),
            ProfileDigest: Digest('2'),
            GmPolicyDigest: Digest('3'),
            RuntimeDigest: Digest('4'),
            AuthorityDigest: string.Empty);
        return authority with
        {
            AuthorityDigest = CharacterCreationQualitiesRules.ComputeAuthorityDigest(authority)
        };
    }

    private static CharacterCreationQualitiesDraftReceipt Receipt(
        CharacterCreationQualitiesDraftPlan plan,
        string draftDigest)
    {
        var receipt = new CharacterCreationQualitiesDraftReceipt(
            CharacterCreationQualitiesSchemas.ReceiptV1,
            plan.TransactionId,
            plan.WorkspaceId,
            plan.ExpectedContentRevision,
            plan.TargetContentRevision,
            plan.ExpectedSavedRevision,
            plan.TargetSavedRevision,
            plan.AuthorityDigest,
            plan.RuntimeDigest,
            plan.PreviewDigest,
            plan.IdempotencyKeyDigest,
            plan.CommandDigest,
            plan.PlanDigest,
            draftDigest,
            CharacterCreationQualitiesRules.ReceiptLedgerRootDigest,
            CharacterDocumentChanged: false,
            ReceiptDigest: string.Empty);
        return receipt with
        {
            ReceiptDigest = CharacterCreationQualitiesRules.ComputeReceiptDigest(receipt)
        };
    }

    private static string Digest(char value) => "sha256:" + new string(value, 64);
}
