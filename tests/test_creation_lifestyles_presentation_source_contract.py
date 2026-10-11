from pathlib import Path
import unittest


REPO = Path(__file__).resolve().parents[1]
SOURCE = REPO / "Chummer.Presentation/Overview/CharacterCreationLifestylesInteractionPresenter.cs"


class CreationLifestylesPresentationSourceContractTests(unittest.TestCase):
    def test_presenter_is_typed_fail_closed_and_receipt_recoverable(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        for marker in (
            "ICharacterCreationLifestylesService",
            "CharacterCreationLifestylesInteractionPresenter",
            "CharacterCreationLifestyleMutationInput",
            "CharacterCreationLifestylePreparedPreview",
            "CharacterCreationLifestyleConfirmation",
            "LoadCore(overview.DisplayOwnerContext, new CharacterCreationLifestylesLoadRequest",
            "PreviewCore(overview.DisplayOwnerContext, new CharacterCreationLifestylePreviewRequest",
            "ConfirmCore(prepared.DisplayOwnerContext, new CharacterCreationLifestyleConfirmRequest",
            "LookupCore(overview.DisplayOwnerContext, new CharacterCreationLifestyleReceiptLookupRequest",
            "ExplicitlyConfirmed: true",
            "PreparedStillMatches",
            "ReceiptMatches",
            "RefreshedStateMatches",
            "ReceiptCanBelongToCurrentState",
            "CharacterCreationLifestylesRules.ComputeStateDigest",
            "CharacterCreationLifestylesRules.ComputePreviewDigest",
            "CharacterCreationLifestylesRules.ComputePlanDigest",
            "CharacterCreationLifestylesRules.ComputeReceiptDigest",
            "PreservesUntouchedSiblingState",
            "PreservesNestedState",
        ):
            self.assertIn(marker, source)

        for forbidden in (
            "System.Xml",
            "XDocument",
            "XElement",
            "WorkspaceXmlMutationCatalog",
            "SaveAsync(",
            "ApplyCollectionMutationAsync",
            "CharacterOverviewPresenter(",
            "DialogCoordinator",
        ):
            self.assertNotIn(forbidden, source)

    def test_presenter_binds_every_mutation_to_revision_and_digests(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        for marker in (
            "WorkspaceRevision",
            "ContentRevision",
            "SavedRevision",
            "ContentDigest",
            "AuxiliaryStateDigest",
            "SourceDigest",
            "RulesDigest",
            "RuntimeDigest",
            "LifestylesSnapshotDigest",
            "PreviewDigest",
            "IdempotencyKey",
        ):
            self.assertIn(marker, source)

    def test_pending_plan_and_original_owner_are_preserved(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        for marker in (
            "IOwnerBoundCharacterCreationLifestylesService",
            "OwnerContextMatches(input.DisplayOwnerContext, overview.DisplayOwnerContext)",
            "OwnerContextMatches(prepared.DisplayOwnerContext, overview.DisplayOwnerContext)",
            "_ownerBoundService.Confirm(original, request)",
            "owner is null && _ownerBoundService is null",
            "CharacterCreationLifestylesSchemas.DraftWritePlanV1",
            "CharacterCreationLifestylesDraftRules.IsValidShape",
            "CharacterCreationLifestylesDraftRules.Digest(draft)",
            "CharacterCreationFinalizationDigest.Compute(left.PendingDraft)",
            "CharacterCreationFinalizationDigest.Compute(right.PendingDraft)",
        ):
            self.assertIn(marker, source)


if __name__ == "__main__":
    unittest.main()
