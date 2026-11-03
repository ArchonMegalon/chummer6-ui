using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationQualitiesInteractionTests
{
    [TestMethod]
    public void Add_A_then_B_retains_both_and_reopened_editor_loads_the_exact_set()
    {
        var store = new QualityService(CreateState());
        var first = Editor(store);
        var a = first.Prepare(["quality-positive"]);
        Assert.IsTrue(first.Confirm(a, Overview(store.State)), first.Status);
        var second = Editor(store);
        string[] proposed = CharacterCreationQualitiesInteraction.SelectedIds(second.Load()).Append("quality-second").ToArray();
        var ab = second.Prepare(proposed);
        CollectionAssert.AreEquivalent(new[] { "quality-positive", "quality-second" }, ab.SelectedOptionIds.ToArray());
        Assert.AreEqual(10, ab.Preview.KarmaRemaining);
        Assert.IsTrue(second.Confirm(ab, Overview(store.State)), second.Status);
        // A new editor over a newly read persisted state does not retain transient UI selections.
        var reopened = Editor(new QualityService(store.State));
        CollectionAssert.AreEqual(new[] { "quality-positive", "quality-second" }, CharacterCreationQualitiesInteraction.SelectedIds(reopened.Load()));
        Assert.AreEqual(2, store.ConfirmCalls);
    }

    [TestMethod]
    public void Cancel_prepared_B_keeps_A_and_does_not_dispatch()
    {
        var store = SeedA();
        var before = store.State;
        Editor(store).Prepare(["quality-positive", "quality-second"]);
        Assert.AreSame(before, store.State);
        Assert.AreEqual(1, store.ConfirmCalls);
        CollectionAssert.AreEqual(new[] { "quality-positive" }, CharacterCreationQualitiesInteraction.SelectedIds(store.State));
    }

    [TestMethod]
    public void Explicit_removal_and_empty_confirmation_submit_exactly_the_reviewed_set()
    {
        var store = SeedA();
        var edit = Editor(store);
        var remove = edit.Prepare([]);
        CollectionAssert.AreEqual(new[] { "quality-positive" }, CharacterCreationQualitiesInteraction.SelectedIds(remove.Before));
        Assert.IsEmpty(remove.Preview.Selections);
        Assert.AreEqual(25, remove.Preview.KarmaRemaining);
        Assert.IsTrue(edit.Confirm(remove, Overview(store.State)), edit.Status);
        Assert.IsEmpty(store.State.PendingDraft!.SelectedOptionIds);
        Assert.IsEmpty(Editor(new QualityService(store.State)).Load().PendingDraft!.SelectedOptionIds);
    }

    [TestMethod]
    public void Character_or_revision_change_during_review_never_dispatches()
    {
        var store = SeedA();
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive", "quality-second"]);
        var changed = store.State with { Binding = store.State.Binding with { ContentRevision = 99 } };
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, Overview(changed)));
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, Overview(store.State) with { WorkspaceId = new("different") }));
        store.State = changed;
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, Overview(review.Before)));
        Assert.AreEqual(1, store.ConfirmCalls);
    }

    [TestMethod]
    public void Owner_change_during_review_does_not_fall_back_to_ambient_service()
    {
        var store = new QualityService(CreateState());
        var owner = new OwnerContextStamp(OwnerScope.LocalSingleUser, "quality-owner", 0);
        var bound = new OwnerService(store, owner);
        var overview = Overview(store.State) with { DisplayOwnerContext = owner };
        var edit = new CharacterCreationQualitiesInteraction(store, bound, overview);
        var review = edit.Prepare(["quality-positive"]);
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, overview with { DisplayOwnerContext = owner with { TransitionRevision = 1 } }));
        bound.Current = owner with { TransitionRevision = 1 };
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, overview));
        Assert.AreEqual(0, store.ConfirmCalls);
    }

    [TestMethod]
    public void Duplicate_confirmation_is_not_dispatched_and_operation_identity_is_stable()
    {
        var store = new QualityService(CreateState());
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive"]);
        var other = Editor(store).Prepare(["quality-positive"]);
        Assert.AreEqual(review.TransactionId, other.TransactionId);
        Assert.AreEqual(review.IdempotencyKey, other.IdempotencyKey);
        var overview = Overview(store.State);
        Assert.IsTrue(edit.Confirm(review, overview));
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review, overview));
        Assert.AreEqual(1, store.ConfirmCalls);
    }

    [TestMethod]
    public void Lost_response_is_reconciled_by_reading_without_resubmitting()
    {
        var store = new QualityService(CreateState()) { LoseResponse = true };
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive"]);
        Assert.IsFalse(edit.Confirm(review, Overview(store.State)));
        Assert.IsTrue(edit.OutcomeUncertain);
        Assert.AreSame(review, edit.PendingReview);
        Assert.Throws<InvalidOperationException>(() => edit.Prepare(["quality-second"]));
        Assert.IsTrue(edit.Reconcile(Overview(store.State)));
        Assert.IsFalse(edit.OutcomeUncertain);
        Assert.AreEqual(1, store.ConfirmCalls);
    }

    [TestMethod]
    public void Failed_write_keeps_original_state_and_read_check_does_not_retry()
    {
        var store = SeedA();
        var original = store.State;
        store.FailBeforeCommit = true;
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive", "quality-second"]);
        Assert.IsFalse(edit.Confirm(review, Overview(store.State)));
        Assert.IsFalse(edit.Reconcile(Overview(store.State)));
        Assert.IsTrue(edit.OutcomeUncertain);
        Assert.AreSame(original, store.State);
        Assert.AreEqual(2, store.ConfirmCalls);
    }

    [TestMethod]
    public void Changed_review_or_mutated_input_cannot_change_the_confirmed_intent()
    {
        var store = new QualityService(CreateState());
        var edit = Editor(store);
        string[] supplied = ["quality-positive"];
        var review = edit.Prepare(supplied);
        supplied[0] = "quality-second";
        Assert.Throws<InvalidOperationException>(() => edit.Confirm(review with { SelectedOptionIds = supplied }, Overview(store.State)));
        Assert.IsTrue(edit.Confirm(review, Overview(store.State)));
        CollectionAssert.AreEqual(new[] { "quality-positive" }, store.State.PendingDraft!.SelectedOptionIds.ToArray());
    }

    [TestMethod]
    public void Pending_selection_cannot_be_displayed_or_recovered_under_a_different_owner()
    {
        var store = new QualityService(CreateState()) { LoseResponse = true };
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive"]);
        Assert.IsFalse(edit.Confirm(review, Overview(store.State)));
        var changed = Overview(store.State) with { DisplayOwnerContext = new(OwnerScope.LocalSingleUser, "different-owner-authority", 0) };
        Assert.Throws<InvalidOperationException>(() => edit.ValidateContext(changed));
        Assert.Throws<InvalidOperationException>(() => edit.Reconcile(changed));
        Assert.AreEqual(1, store.ConfirmCalls);
        Assert.IsTrue(edit.OutcomeUncertain);
    }


    [TestMethod]
    public void Display_revalidation_accepts_the_current_bound_context()
    {
        var store = SeedA();
        var edit = Editor(store);
        edit.ValidateContext(Overview(store.State));
        CharacterCreationQualitiesState loaded = edit.Load();
        edit.ValidateContext(Overview(store.State));
        edit.RequireDisplayContext(Overview(store.State));
        Assert.AreSame(store.State, loaded);
    }

    [TestMethod]
    public void Revision_bump_during_load_is_caught_by_display_revalidation_alone()
    {
        // Scope item 2: workspace/owner validation alone survives a revision bump in the await
        // gap; the display gate must catch it so stale state is discarded, never presented.
        var store = SeedA();
        var edit = Editor(store);
        edit.ValidateContext(Overview(store.State));
        CharacterCreationQualitiesState loaded = edit.Load();
        CharacterCreationQualitiesState bumped = store.State with { Binding = store.State.Binding with { ContentRevision = store.State.Binding.ContentRevision + 1 } };
        store.State = bumped;
        edit.ValidateContext(Overview(bumped));
        Assert.Throws<InvalidOperationException>(() => edit.RequireDisplayContext(Overview(bumped)));
        edit.RequireDisplayContext(Overview(loaded));
    }

    [TestMethod]
    public void Character_switch_during_load_fails_revalidation_before_display()
    {
        var store = SeedA();
        var edit = Editor(store);
        edit.ValidateContext(Overview(store.State));
        edit.Load();
        CharacterOverviewState elsewhere = Overview(store.State) with { WorkspaceId = new("different") };
        Assert.Throws<InvalidOperationException>(() => edit.ValidateContext(elsewhere));
        Assert.Throws<InvalidOperationException>(() => edit.RequireDisplayContext(elsewhere));
    }

    [TestMethod]
    public void Revision_change_after_preparing_a_review_rejects_its_display()
    {
        var store = SeedA();
        var edit = Editor(store);
        edit.Load();
        var prepared = edit.Prepare(["quality-positive", "quality-second"]);
        Assert.IsTrue(prepared.Preview.CanConfirm);

        var current = store.State with
        {
            Binding = store.State.Binding with
            {
                ContentRevision = store.State.Binding.ContentRevision + 1
            }
        };
        store.State = current;

        edit.ValidateContext(Overview(current));
        Assert.Throws<InvalidOperationException>(
            () => edit.RequireDisplayContext(Overview(current)));
        CollectionAssert.AreEquivalent(
            new[] { "quality-positive" },
            CharacterCreationQualitiesInteraction.SelectedIds(store.State).ToArray());
    }

    [TestMethod]
    public void Uncertain_save_can_show_its_frozen_review_after_reload_without_replaying()
    {
        var store = new QualityService(CreateState()) { LoseResponse = true };
        var edit = Editor(store);
        var review = edit.Prepare(["quality-positive"]);
        Assert.IsFalse(edit.Confirm(review, Overview(store.State)));
        var reloaded = Overview(store.State);

        Assert.Throws<InvalidOperationException>(() => edit.RequireDisplayContext(reloaded));
        edit.RequireReconciliationDisplayContext(reloaded);
        Assert.Throws<InvalidOperationException>(() => edit.Prepare(["quality-second"]));
        Assert.IsTrue(edit.Reconcile(reloaded));
        Assert.AreEqual(1, store.ConfirmCalls);
        Assert.IsFalse(edit.OutcomeUncertain);
    }

    [TestMethod]
    public void Reconciliation_display_requires_a_pending_operation_and_its_original_context()
    {
        var store = new QualityService(CreateState()) { LoseResponse = true };
        var edit = Editor(store);
        Assert.Throws<InvalidOperationException>(
            () => edit.RequireReconciliationDisplayContext(Overview(store.State)));
        var review = edit.Prepare(["quality-positive"]);
        Assert.IsFalse(edit.Confirm(review, Overview(store.State)));
        var current = Overview(store.State);
        var otherCharacter = current with { WorkspaceId = new("different") };
        var otherOwner = current with
        {
            DisplayOwnerContext = new(OwnerScope.LocalSingleUser, "different-owner-authority", 0)
        };
        Assert.Throws<InvalidOperationException>(
            () => edit.RequireReconciliationDisplayContext(otherCharacter));
        Assert.Throws<InvalidOperationException>(
            () => edit.RequireReconciliationDisplayContext(otherOwner));
        Assert.AreEqual(1, store.ConfirmCalls);
        Assert.IsTrue(edit.OutcomeUncertain);
    }

    private static CharacterCreationQualitiesInteraction Editor(QualityService store)
        => new(store, null, Overview(store.State));
    private static QualityService SeedA()
    {
        var store = new QualityService(CreateState());
        var edit = Editor(store);
        Assert.IsTrue(edit.Confirm(edit.Prepare(["quality-positive"]), Overview(store.State)));
        return store;
    }
    private static string Digest(char c) => "sha256:" + new string(c, 64);
    private static CharacterCreationQualityCatalogOption SecondOption(CharacterCreationQualityCatalogOption first)
    {
        string xml = "<quality><id>7da7aede-05b3-4275-a366-ce9cccff1dda</id><name>Second Quality</name><type>Positive</type><karma>5</karma><source>SR5</source></quality>";
        var option = first with { OptionId = "quality-second", SelectionKey = "quality-second", SourceId = Guid.Parse("7da7aede-05b3-4275-a366-ce9cccff1dda"), Name = "Second Quality", KarmaCost = 5,
            SourceNodeXml = xml, SourceNodeDigest = CharacterCreationQualitiesRules.ComputeSourceNodeDigest(xml), SourceAnchorIds = ["qualities.xml#quality:quality-second"], OptionDigest = string.Empty };
        return option with { OptionDigest = CharacterCreationQualitiesRules.ComputeOptionDigest(option) };
    }
    private static CharacterOverviewState Overview(CharacterCreationQualitiesState state)
        => CharacterOverviewState.Empty with
        {
            WorkspaceId = state.Binding.WorkspaceId,
            CreationQualities = state,
            Profile = new("Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
                CharacterCreationBuildMethods.Priority, "Standard", false,
                false, false, false, false, 0, 0),
            Session = new(state.Binding.WorkspaceId,
                [new OpenWorkspaceState(state.Binding.WorkspaceId, "Wizard", "W", DateTimeOffset.UnixEpoch,
                    RulesetDefaults.Sr5, state.Binding.ContentRevision, state.Binding.SavedRevision)], [state.Binding.WorkspaceId])
        };
    private static CharacterCreationQualitiesState CreateState()
    {
        const string sourceNodeXml = "<quality><id>4d8fd70f-cb89-40e8-b93f-c610467bbc11</id>"
            + "<name>Focused Concentration</name><type>Positive</type><karma>10</karma>"
            + "<source>SR5</source></quality>";
        var option = new CharacterCreationQualityCatalogOption(
            "quality-positive",
            Guid.Parse("4d8fd70f-cb89-40e8-b93f-c610467bbc11"),
            "quality-positive",
            "Focused Concentration",
            CharacterCreationQualityType.Positive,
            Rating: 1,
            KarmaCost: 10,
            MaximumSelections: 1,
            IsMetagenic: false,
            CountsAgainstQualityLimit: true,
            CountsAgainstKarma: true,
            IsFreeOrGranted: false,
            IsSelectable: true,
            EligibilityIsExact: true,
            DisableReasonKey: null,
            FollowUpChoiceId: null,
            FollowUpChoiceLabel: null,
            SourceAnchorIds: ["qualities.xml#quality:quality-positive"],
            SourceNodeXml: sourceNodeXml,
            SourceNodeDigest: CharacterCreationQualitiesRules.ComputeSourceNodeDigest(sourceNodeXml),
            OptionDigest: string.Empty);
        option = option with
        {
            OptionDigest = CharacterCreationQualitiesRules.ComputeOptionDigest(option)
        };
        var authority = new CharacterCreationQualitiesAuthority(
            CharacterCreationQualitiesSchemas.AuthorityV1,
            RulesetDefaults.Sr5,
            "settings-profile",
            QualityKarmaLimit: 25,
            MayExceedPositiveQualityLimit: false,
            MayExceedNegativeQualityLimit: false,
            MetagenicLimit: 0,
            Options: [option, SecondOption(option)],
            GrantedQualities: [],
            SourceAnchorIds: ["qualities.xml"],
            Blockers: [],
            IsAuthoritative: true,
            SourceDigest: "sha256:" + new string('a', 64),
            ProfileDigest: "sha256:" + new string('b', 64),
            GmPolicyDigest: "sha256:" + new string('c', 64),
            RuntimeDigest: "sha256:" + new string('d', 64),
            AuthorityDigest: string.Empty);
        authority = authority with
        {
            AuthorityDigest = CharacterCreationQualitiesRules.ComputeAuthorityDigest(authority)
        };
        string rawDigest = Digest('a');
        var binding = new CharacterCreationQualitiesBinding(
            new CharacterWorkspaceId("ws-wizard"),
            4,
            4,
            rawDigest,
            new string('e', 64),
            PrerequisiteDraftRevision: 2,
            PrerequisiteDraftDigest: "sha256:" + new string('f', 64),
            AttributesDraftRevision: 3,
            AttributesDraftDigest: "sha256:" + new string('1', 64),
            RulesetId: RulesetDefaults.Sr5,
            BuildMethod: CharacterCreationBuildMethods.Priority,
            CharacterCreated: false,
            CreationKarmaTotal: 25,
            CreationKarmaUsedBeforeQualities: 0,
            AuthorityDigest: authority.AuthorityDigest,
            RuntimeDigest: authority.RuntimeDigest);
        CharacterCreationQualitiesPreview preview = CharacterCreationQualitiesRules.Evaluate(new(
            binding,
            authority,
            []));
        Assert.IsEmpty(preview.Blockers, "The fixture must carry exact current Core authority.");
        var state = new CharacterCreationQualitiesState(
            CharacterCreationQualitiesSchemas.StateV1,
            binding,
            authority,
            PrerequisiteDraft: null,
            AttributesDraft: null,
            PendingDraft: null,
            Preview: preview,
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty);
        return state with
        {
            SnapshotDigest = CharacterCreationQualitiesRules.ComputeStateDigest(state)
        };
    }


    private sealed class QualityService(CharacterCreationQualitiesState state) : ICharacterCreationQualitiesService
    {
        public CharacterCreationQualitiesState State = state;
        public int ConfirmCalls;
        public bool LoseResponse;
        public bool FailBeforeCommit;
        public CharacterCreationFoundationResult<CharacterCreationQualitiesState> Load(CharacterCreationQualitiesLoadRequest request) => new(CharacterCreationFoundationOutcomes.Success, State, []);
        public CharacterCreationFoundationResult<CharacterCreationQualitiesPreview> Preview(CharacterCreationQualitiesPreviewRequest request)
        {
            Assert.AreEqual(State.Binding, request.Binding);
            var value = CharacterCreationQualitiesRules.Evaluate(new(State.Binding, State.Authority, request.SelectedOptionIds));
            return new(value.CanConfirm ? CharacterCreationFoundationOutcomes.Success : CharacterCreationFoundationOutcomes.Blocked, value, value.Blockers);
        }
        public CharacterCreationFoundationResult<CharacterCreationQualitiesDraftReceipt> Confirm(CharacterCreationQualitiesConfirmRequest request)
        {
            ConfirmCalls++;
            if (FailBeforeCommit) throw new IOException("Synthetic write failure");
            var preview = Preview(new(request.Binding, request.SelectedOptionIds)).Value!;
            Assert.IsTrue(CharacterCreationQualitiesRules.TryPlan(preview, request.PreviewDigest, request.IdempotencyKey, request.ExplicitlyConfirmed, false, request.TransactionId, out var plan));
            var b = State.Binding;
            var draft = new CharacterCreationQualitiesDraft(CharacterCreationQualitiesSchemas.DraftV1,
                b.WorkspaceId, (State.PendingDraft?.DraftRevision ?? 0) + 1, b.ContentRevision, b.RawCharacterXmlDigest,
                b.PrerequisiteDraftRevision, b.PrerequisiteDraftDigest, b.AttributesDraftRevision, b.AttributesDraftDigest,
                b.AuthorityDigest, b.RuntimeDigest, request.SelectedOptionIds.ToArray(), preview.Selections,
                preview.PositiveQualityBudget.Used, preview.NegativeQualityBudget.Used, preview.KarmaRemaining,
                preview.SourceAnchorIds, false, plan.IdempotencyKeyDigest, request.PreviewDigest, plan.CommandDigest, string.Empty);
            draft = draft with { DraftDigest = CharacterCreationQualitiesRules.ComputeDraftDigest(draft) };
            var receipt = new CharacterCreationQualitiesDraftReceipt(CharacterCreationQualitiesSchemas.ReceiptV1,
                request.TransactionId, b.WorkspaceId, b.ContentRevision, plan.TargetContentRevision,
                b.SavedRevision, plan.TargetSavedRevision, b.AuthorityDigest, b.RuntimeDigest,
                request.PreviewDigest, plan.IdempotencyKeyDigest, plan.CommandDigest, plan.PlanDigest,
                draft.DraftDigest, CharacterCreationQualitiesRules.ReceiptLedgerRootDigest, false, string.Empty);
            receipt = receipt with { ReceiptDigest = CharacterCreationQualitiesRules.ComputeReceiptDigest(receipt) };
            var binding = b with { ContentRevision = plan.TargetContentRevision, SavedRevision = plan.TargetSavedRevision, AuxiliaryStateDigest = new string('2', 64) };
            State = State with { Binding = binding, PendingDraft = draft,
                Preview = CharacterCreationQualitiesRules.Evaluate(new(binding, State.Authority, draft.SelectedOptionIds)) };
            State = State with { SnapshotDigest = CharacterCreationQualitiesRules.ComputeStateDigest(State) };
            if (LoseResponse) throw new IOException("Synthetic lost response after atomic commit");
            return new(CharacterCreationFoundationOutcomes.Success, receipt, []);
        }
    }
    private sealed class OwnerService(QualityService inner, OwnerContextStamp owner) : IOwnerBoundCharacterCreationQualitiesService
    {
        public OwnerContextStamp Current = owner;
        private static CharacterCreationFoundationResult<T> Denied<T>() where T : class => new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-changed"]);
        public CharacterCreationFoundationResult<CharacterCreationQualitiesState> Load(OwnerContextStamp expected, CharacterCreationQualitiesLoadRequest request) => expected == Current ? inner.Load(request) : Denied<CharacterCreationQualitiesState>();
        public CharacterCreationFoundationResult<CharacterCreationQualitiesPreview> Preview(OwnerContextStamp expected, CharacterCreationQualitiesPreviewRequest request) => expected == Current ? inner.Preview(request) : Denied<CharacterCreationQualitiesPreview>();
        public CharacterCreationFoundationResult<CharacterCreationQualitiesDraftReceipt> Confirm(OwnerContextStamp expected, CharacterCreationQualitiesConfirmRequest request) => expected == Current ? inner.Confirm(request) : Denied<CharacterCreationQualitiesDraftReceipt>();
    }
}
