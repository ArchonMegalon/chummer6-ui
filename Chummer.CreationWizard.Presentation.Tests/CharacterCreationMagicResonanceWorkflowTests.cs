using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationMagicResonanceWorkflowTests
{
    [TestMethod]
    public void Source_contract_keeps_rules_and_mutation_authority_in_core()
    {
        Assert.AreEqual(
            "chummer.presentation.sr5_priority_magic_resonance.v1",
            CharacterCreationMagicResonancePresentationContract.Schema);
        Assert.AreEqual("chummer-core", CharacterCreationMagicResonancePresentationContract.AuthorityOwner);
        Assert.AreEqual(
            "core-atomic-auxiliary-state-only",
            CharacterCreationMagicResonancePresentationContract.PersistenceMode);
        Assert.IsFalse(CharacterCreationMagicResonancePresentationContract.AllowsCharacterDocumentMutation);
        Assert.IsFalse(CharacterCreationMagicResonancePresentationContract.AllowsPresentationPersistence);
        Assert.IsTrue(CharacterCreationMagicResonancePresentationContract.RequiresCorePreview);
        Assert.IsTrue(CharacterCreationMagicResonancePresentationContract.RequiresExplicitConfirmation);
        Assert.IsFalse(CharacterCreationMagicResonancePresentationContract.IsSupportedTalentKind(
            CharacterCreationMagicResonanceKinds.ArtificialIntelligence));
        Assert.IsFalse(CharacterCreationMagicResonancePresentationContract.IsSupportedTalentKind(
            CharacterCreationMagicResonanceKinds.Unsupported));
    }

    [TestMethod]
    public void Project_preserves_exact_talent_catalog_budgets_and_source_anchors()
    {
        CharacterCreationMagicResonanceState core =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                CharacterCreationMagicResonanceTestFixture.Digest('0'));

        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(core);

        Assert.AreEqual(CharacterCreationMagicResonanceKinds.Magician, state.Talent.Kind);
        Assert.AreEqual(6, state.Talent.Magic);
        Assert.IsTrue(state.Talent.RequiresTradition);
        Assert.IsTrue(state.Talent.AllowsSpells);
        Assert.HasCount(1, state.Traditions);
        Assert.HasCount(2, state.Spells);
        Assert.AreEqual(1m, state.Budgets.Single(budget =>
            budget.Kind == CharacterCreationMagicResonanceKinds.Tradition).Remaining);
        Assert.AreEqual(2m, state.Budgets.Single(budget =>
            budget.Kind == CharacterCreationMagicResonanceKinds.Spell).Remaining);
        CollectionAssert.Contains(
            state.Talent.SourceAnchorIds.ToArray(),
            "priorities.xml#priority:magic-a:talent:0");
        Assert.IsTrue(state.CanEdit);
        Assert.IsFalse(state.HasPendingDraft);
    }

    [TestMethod]
    public void Draft_accepts_only_current_typed_identities_and_levels()
    {
        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(
                CharacterCreationMagicResonanceTestFixture.CreateState(
                    CharacterCreationMagicResonanceTestFixture.Digest('0')));

        CharacterCreationMagicResonanceDesktopDraft draft =
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                CharacterCreationMagicResonanceTestFixture.TraditionId,
                null,
                [],
                [
                    CharacterCreationMagicResonanceTestFixture.SpellTwoId,
                    CharacterCreationMagicResonanceTestFixture.SpellOneId
                ],
                []);

        CollectionAssert.AreEqual(
            new[]
            {
                CharacterCreationMagicResonanceTestFixture.SpellOneId,
                CharacterCreationMagicResonanceTestFixture.SpellTwoId
            },
            draft.Selections.Spells.ToArray());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                new CharacterCreationMagicResonanceOptionIdentity(
                    CharacterCreationMagicResonanceKinds.Tradition,
                    "invented"),
                null,
                [],
                [],
                []));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                CharacterCreationMagicResonanceTestFixture.TraditionId,
                null,
                [],
                [
                    CharacterCreationMagicResonanceTestFixture.SpellOneId,
                    CharacterCreationMagicResonanceTestFixture.SpellOneId
                ],
                []));
    }

    [TestMethod]
    public void Review_uses_core_preview_and_preserves_core_blockers()
    {
        CharacterCreationMagicResonanceState core =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                CharacterCreationMagicResonanceTestFixture.Digest('0'));
        var service = new StubMagicResonanceService(core);
        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(core);
        CharacterCreationMagicResonanceDesktopDraft incomplete =
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                tradition: null,
                stream: null,
                adeptPowers: [],
                spells: [],
                complexForms: []);

        CharacterCreationMagicResonanceReview review =
            CharacterCreationMagicResonanceWorkflow.Review(service, state, incomplete);

        Assert.AreEqual(1, service.PreviewCalls);
        Assert.IsFalse(review.Preview.CanConfirm);
        CollectionAssert.Contains(
            review.Preview.Blockers.ToArray(),
            CharacterCreationMagicResonanceBlockers.TraditionRequired);
        CollectionAssert.Contains(
            review.Preview.Blockers.ToArray(),
            CharacterCreationMagicResonanceBlockers.SpellBudgetIncomplete);
    }

    [TestMethod]
    public void Review_rejects_digest_binding_drift_before_core_preview()
    {
        CharacterCreationMagicResonanceState core =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                CharacterCreationMagicResonanceTestFixture.Digest('0'));
        var service = new StubMagicResonanceService(core);
        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(core);
        CharacterCreationMagicResonanceDesktopDraft draft =
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                CharacterCreationMagicResonanceTestFixture.TraditionId,
                null,
                [],
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Spells,
                []);

        CharacterCreationMagicResonanceDesktopDraft drifted = draft with
        {
            ExpectedBinding = draft.ExpectedBinding with
            {
                CustomDataInputsDigest = CharacterCreationMagicResonanceTestFixture.Digest('4')
            }
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationMagicResonanceWorkflow.Review(service, state, drifted));
        Assert.AreEqual(0, service.PreviewCalls);
    }

    [TestMethod]
    public void Confirm_uses_core_atomic_authority_and_validates_exact_persisted_receipt_and_replay()
    {
        string rawDigest = CharacterCreationMagicResonanceTestFixture.Digest('0');
        CharacterCreationMagicResonanceState core =
            CharacterCreationMagicResonanceTestFixture.CreateState(rawDigest);
        var service = new StubMagicResonanceService(core);
        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(core);
        CharacterCreationMagicResonanceDesktopDraft draft =
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Tradition,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Stream,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.AdeptPowers,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Spells,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.ComplexForms);
        CharacterCreationMagicResonanceReview review =
            CharacterCreationMagicResonanceWorkflow.Review(service, state, draft);

        CharacterCreationMagicResonanceConfirmation committed =
            CharacterCreationMagicResonanceWorkflow.Confirm(
                service,
                review,
                "magic-command",
                explicitlyConfirmed: true);
        CharacterCreationMagicResonanceConfirmation replayed =
            CharacterCreationMagicResonanceWorkflow.Confirm(
                service,
                review,
                "magic-command",
                explicitlyConfirmed: true);

        Assert.IsFalse(committed.IsIdempotentReplay);
        Assert.IsTrue(replayed.IsIdempotentReplay);
        Assert.IsTrue(committed.IsCurrentDraft);
        Assert.IsTrue(replayed.IsCurrentDraft);
        Assert.IsFalse(committed.Receipt.CharacterDocumentChanged);
        Assert.AreEqual(core.Binding.ContentRevision + 1, committed.Receipt.ContentRevision);
        Assert.AreEqual(rawDigest, committed.PersistedState.Binding.RawCharacterXmlDigest);
        Assert.AreEqual(
            committed.Receipt.ReceiptDigest,
            replayed.Receipt.ReceiptDigest);
        Assert.AreEqual(2, service.ConfirmCalls);
    }

    [TestMethod]
    public void Confirm_requires_explicit_consent_at_the_core_boundary()
    {
        CharacterCreationMagicResonanceState core =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                CharacterCreationMagicResonanceTestFixture.Digest('0'));
        var service = new StubMagicResonanceService(core);
        CharacterCreationMagicResonanceEditorState state =
            CharacterCreationMagicResonanceWorkflow.Project(core);
        CharacterCreationMagicResonanceDesktopDraft draft =
            CharacterCreationMagicResonanceWorkflow.CreateDraft(
                state,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Tradition,
                null,
                [],
                CharacterCreationMagicResonanceTestFixture.CompleteSelections.Spells,
                []);
        CharacterCreationMagicResonanceReview review =
            CharacterCreationMagicResonanceWorkflow.Review(service, state, draft);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationMagicResonanceWorkflow.Confirm(
                service,
                review,
                "magic-command",
                explicitlyConfirmed: false));

        Assert.AreEqual(
            CharacterCreationMagicResonanceBlockers.ExplicitConfirmationRequired,
            error.Message);
        Assert.AreEqual(1, service.ConfirmCalls);
    }

    [TestMethod]
    public void Unsupported_ai_identity_fails_closed_even_if_a_provider_marks_it_enabled()
    {
        CharacterCreationMagicResonanceState ai =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                CharacterCreationMagicResonanceTestFixture.Digest('0'),
                talentKind: CharacterCreationMagicResonanceKinds.ArtificialIntelligence);

        Assert.IsFalse(CharacterCreationMagicResonanceWorkflow.TryProject(ai, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CharacterCreationMagicResonanceWorkflow.Project(ai));
    }
}
