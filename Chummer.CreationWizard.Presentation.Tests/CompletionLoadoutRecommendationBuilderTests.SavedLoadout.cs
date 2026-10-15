using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CompletionLoadoutRecommendationBuilderTests
{
    private const string SavedLoadoutKey = "completion-retained-loadout";

    [TestMethod]
    public void Saved_conventional_firearm_offers_missing_loose_ammo_without_replacing_loadout()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([]);
        fixture.PrepareContacts();
        var receipt = SaveLoadout(fixture, "Heavy Pistols");
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var option = inputs.Lifestyles.Gear.State.Authority.Options.Single(row => row.SourceId == AmmoSource);
        Assert.IsFalse(inputs.Lifestyles.Gear.State.PendingDraft!.Lines.Any(row => row.SourceId == AmmoSource));
        // The mage pack normally prefers a taser. Saved source identity, rather
        // than the pack's default weapon, must determine the missing loose stock.
        var intent = Intent(fixture, inputs, CompletionAssistantRules.Mage);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var ammo = proposal.Request.Purchases.Single(row => row.Gear?.OptionId == option.OptionId);
        Assert.AreEqual(100, ammo.Gear!.Quantity);
        Assert.IsNull(ammo.Weapon);
        Assert.IsNull(ammo.Armor);
        Assert.IsNull(ammo.Lifestyle);
        var priced = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines.Single(row => row.SourceId == AmmoSource);
        Assert.AreEqual(option.OptionId, priced.OptionId);
        Assert.AreEqual(option.SourceNodeDigest, priced.SourceNodeDigest);
        Assert.AreEqual(option.PackageQuantity, priced.PackageQuantity);
        Assert.AreEqual(option.PackageCost, priced.PackageCost);
        Assert.AreEqual(100, priced.Quantity);
        Assert.AreEqual(200m, priced.TotalCost, "The fresh Core projection prices the real source's ten-round packages.");
        Assert.IsTrue(proposal.Evaluation.Value.Purchases!.AdditionalSpend <= intent.MaximumAdditionalPurchaseSpend!.Value);
        Assert.IsTrue(proposal.Evaluation.Value.Review.Balances.RemainingNuyen >= intent.Reserves.Nuyen);
        AssertSavedLoadoutRetained(fixture, inputs, proposal, receipt);
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Saved_conventional_firearm_keeps_partial_ammo_stock_without_top_up()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        SaveEquipment(fixture, (AmmoSource, 7));
        var receipt = SaveLoadout(fixture, "Heavy Pistols");
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var saved = inputs.Lifestyles.Gear.State.PendingDraft!.Lines.Single(row => row.SourceId == AmmoSource);
        Assert.AreEqual(7, saved.Quantity);
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai)));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Gear?.OptionId == saved.OptionId));
        var final = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines.Single(row => row.SourceId == AmmoSource);
        CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(saved), JsonSerializer.SerializeToUtf8Bytes(final));
        Assert.AreEqual(new CharacterCreationGearSelection(saved.OptionId, 7),
            proposal.Evaluation.Value.Selections.Gear.Single(row => row.OptionId == saved.OptionId));
        AssertSavedLoadoutRetained(fixture, inputs, proposal, receipt);
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow("Tasers")]
    [DataRow("Blades")]
    public void Unsupported_nonconventional_weapon_cannot_create_a_saved_ammo_baseline(string category)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([]);
        fixture.PrepareContacts();
        var service = new CharacterCreationLoadoutService(fixture.Store, fixture.Resolver);
        var prepared = service.Prepare(fixture.WorkspaceId);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, prepared.Outcome, string.Join(",", prepared.Blockers));
        Assert.IsNotNull(prepared.Value);
        var state = prepared.Value;
        var options = state.Inputs.Authority.Options.Where(row => row.Kind == CharacterCreationLoadoutKinds.Weapon
            && row.Category == category).ToArray();
        Assert.IsTrue(options.Length > 0, "The real source catalogue must contain the unsupported category.");
        Assert.IsTrue(options.All(row => !row.IsSelectable && row.Blockers.Count > 0));
        var files = fixture.CaptureFiles();
        // This v1 purchase lane rejects intrinsic/effect-bearing weapons. Do
        // not fabricate a saved receipt to claim ammunition coverage for them.
        var preview = service.Preview(new(state.Inputs.Binding, state.Inputs.SnapshotDigest,
            new CharacterCreationLoadoutSelections([new(options[0].OptionId, 1)], [])));
        Assert.AreNotEqual(CharacterCreationFoundationOutcomes.Success, preview.Outcome);
        Assert.IsNull(preview.Value);
        Assert.IsTrue(preview.Blockers.Count > 0);
        Assert.IsNull(service.LookupReceipt(fixture.WorkspaceId, SavedLoadoutKey).Value);
        fixture.AssertFilesUnchanged(files);
    }

    private static CharacterCreationLoadoutReceipt SaveLoadout(CompletionRecommendationTestFixture fixture, string weaponCategory)
    {
        // Use the genuine manual service and persisted receipt ledger; a copied
        // completion projection is not an admitted saved draft.
        var service = new CharacterCreationLoadoutService(fixture.Store, fixture.Resolver);
        var prepared = service.Prepare(fixture.WorkspaceId);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, prepared.Outcome, string.Join(",", prepared.Blockers));
        Assert.IsNotNull(prepared.Value);
        var state = prepared.Value;
        CharacterCreationLoadoutCatalogOption Option(string kind, string? category = null)
            => state.Inputs.Authority.Options.Where(row => row.Kind == kind && (category is null || row.Category == category)
                && row.IsSelectable && row.PricingIsExact && row.AvailabilityIsExact && row.Blockers.Count == 0 && row.UnitCost > 0m)
                .OrderBy(row => row.UnitCost).ThenBy(row => row.OptionId, StringComparer.Ordinal).First();
        // Retain two distinct weapon instances and the one armor permitted by
        // the current Core purchase lane; never relax admission for a fixture.
        var selections = new CharacterCreationLoadoutSelections(
            [new(Option(CharacterCreationLoadoutKinds.Weapon, weaponCategory).OptionId, 2)],
            [new(Option(CharacterCreationLoadoutKinds.Armor).OptionId, 1)]);
        var preview = service.Preview(new(state.Inputs.Binding, state.Inputs.SnapshotDigest, selections));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, preview.Outcome, string.Join(",", preview.Blockers));
        Assert.IsNotNull(preview.Value);
        var saved = service.Confirm(new(preview.Value.Request, preview.Value.PreviewDigest, SavedLoadoutKey, true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, saved.Outcome, string.Join(",", saved.Blockers));
        Assert.IsNotNull(saved.Value);
        Assert.AreEqual(3, saved.Value.Draft.Instances.Sum(row => row.InstanceIds.Count));
        CollectionAssert.AreEqual(selections.Weapons.ToArray(), saved.Value.Draft.Selections.Weapons.ToArray());
        CollectionAssert.AreEqual(selections.Armor.ToArray(), saved.Value.Draft.Selections.Armor.ToArray());
        return saved.Value;
    }

    private static void AssertSavedLoadoutRetained(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantProposal proposal, CharacterCreationLoadoutReceipt receipt)
    {
        Assert.IsNotNull(inputs.Loadout);
        Assert.IsNotNull(inputs.Loadout.PendingDraft);
        Assert.AreEqual(receipt.Draft.DraftDigest, inputs.Loadout.PendingDraft.DraftDigest);
        CollectionAssert.AreEqual(receipt.Draft.Selections.Weapons.ToArray(), inputs.Loadout.OriginalSelections.Weapons.ToArray());
        CollectionAssert.AreEqual(receipt.Draft.Selections.Armor.ToArray(), inputs.Loadout.OriginalSelections.Armor.ToArray());
        Assert.IsNotNull(inputs.Loadout.Receipts);
        Assert.IsTrue(inputs.Loadout.Receipts.Any(row => row.ReceiptDigest == receipt.ReceiptDigest));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null || row.Armor is not null));
        Assert.IsNotNull(proposal.Loadout);
        Assert.AreEqual(0, proposal.Loadout.Blockers.Count);
        CollectionAssert.AreEqual(receipt.Draft.Selections.Weapons.ToArray(), proposal.Loadout.Selections.Weapons.ToArray());
        CollectionAssert.AreEqual(receipt.Draft.Selections.Armor.ToArray(), proposal.Loadout.Selections.Armor.ToArray());
        CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(receipt.Draft.Instances),
            JsonSerializer.SerializeToUtf8Bytes(proposal.Loadout.RetainedInstances));
        Assert.AreEqual(receipt.Draft.PurchaseCost, proposal.Loadout.PurchaseCost);
        var lookup = new CharacterCreationLoadoutService(fixture.Store, fixture.Resolver).LookupReceipt(fixture.WorkspaceId, SavedLoadoutKey);
        Assert.IsNotNull(lookup.Value, string.Join(",", lookup.Blockers));
        Assert.AreEqual(receipt.ReceiptDigest, lookup.Value.ReceiptDigest);
        // Re-read through the owner-bound completion reader after building advice
        // to check persisted selections, receipt identities and instance GUIDs.
        CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(inputs),
            JsonSerializer.SerializeToUtf8Bytes(fixture.ReadInputs(includeLoadout: true)));
    }
}
