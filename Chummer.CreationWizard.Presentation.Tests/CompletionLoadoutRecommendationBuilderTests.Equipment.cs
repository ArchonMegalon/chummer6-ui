using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CompletionLoadoutRecommendationBuilderTests
{
    private static readonly Guid AmmoSource = Guid.Parse("b2a0b340-c793-4322-8422-8b03d18a6fae");
    private static readonly Guid TransceiverSource = Guid.Parse("d1fca845-ae01-4b39-a176-1506da699e0e");
    private static readonly Guid MicSource = Guid.Parse("7bdc5866-88ae-45a6-a525-ec03f161f086");
    private static readonly Guid EraserSource = Guid.Parse("d0eb5971-03d6-4822-9fb5-c524bf16bf62");
    private static readonly Guid PicksSource = Guid.Parse("0fdeb240-743c-4a9d-9cf1-fef0990575bb");
    private static readonly Guid LightSource = Guid.Parse("c49a893a-d445-4aac-bec0-c8501cba4c2c");

    [TestMethod]
    public void New_firearm_stocks_loose_regular_ammo_at_Core_package_price()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai)));
        var option = inputs.Lifestyles.Gear.State.Authority.Options.Single(row => row.SourceId == AmmoSource);
        var ammo = proposal.Request.Purchases.Single(row => row.Gear?.OptionId == option.OptionId);
        Assert.AreEqual(100, ammo.Gear!.Quantity);
        Assert.IsNull(ammo.Weapon, "Loose stock has no weapon-category or loaded-clip selection.");
        Assert.IsNull(ammo.Armor);
        Assert.IsNull(ammo.Lifestyle);
        Assert.IsTrue(option.IsSelectable && option.PricingIsExact && option.AvailabilityIsExact);
        Assert.AreEqual(0, option.Blockers.Count);
        var lines = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        var priced = lines.Single(row => row.SourceId == AmmoSource);
        Assert.AreEqual(option.OptionId, priced.OptionId);
        Assert.AreEqual(option.SourceNodeDigest, priced.SourceNodeDigest);
        // Real gear.xml sells this source in tens. Quantity means rounds, not
        // packages; all these amounts come from the fresh Core projection.
        Assert.AreEqual(10, priced.PackageQuantity);
        Assert.AreEqual(20m, priced.PackageCost);
        Assert.AreEqual(100, priced.Quantity);
        Assert.AreEqual(200m, priced.TotalCost);
        var targets = proposal.Request.Purchases.ToList();
        int weaponIndex = targets.FindIndex(row => row.Weapon is not null);
        int ammoIndex = targets.IndexOf(ammo);
        int utilityIndex = targets.FindIndex(row => row.Gear?.OptionId ==
            inputs.Lifestyles.Gear.State.Authority.Options.Single(option => option.SourceId == TransceiverSource).OptionId);
        Assert.IsTrue(weaponIndex >= 0 && weaponIndex < ammoIndex && ammoIndex < utilityIndex);
        Assert.AreEqual(proposal.Request.Purchases.Count,
            proposal.Evaluation.Value.Purchases!.Targets.Count);
        Assert.IsTrue(proposal.Evaluation.Value.Purchases.Targets.All(row =>
            row.Outcome == CharacterCreationPurchaseCompletionTargetOutcomes.Added));
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.ammo-binding-loading-modifications"));
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.commlinks-rated-gear-child-graphs"));
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Ammo_falls_back_to_fifty_rounds_within_cumulative_spend_limit()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var intent = Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai);
        var funded = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.IsNotNull(funded.Loadout);
        decimal cap = funded.Loadout.PurchaseCost + 100m;
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { MaximumAdditionalPurchaseSpend = cap }));
        var lines = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        var ammo = lines.Single(row => row.SourceId == AmmoSource);
        Assert.AreEqual(50, ammo.Quantity);
        Assert.AreEqual(100m, ammo.TotalCost);
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(cap, proposal.Evaluation.Value.Purchases!.AdditionalSpend);
        CollectionAssert.AreEqual(funded.Loadout.Selections.Weapons.ToArray(), proposal.Loadout!.Selections.Weapons.ToArray());
        CollectionAssert.AreEqual(funded.Loadout.Selections.Armor.ToArray(), proposal.Loadout.Selections.Armor.ToArray());
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(CompletionAssistantRules.Face, true)]
    [DataRow(CompletionAssistantRules.GeneralSurvivor, true)]
    [DataRow(CompletionAssistantRules.StreetSamurai, false)]
    [DataRow(CompletionAssistantRules.Decker, false)]
    [DataRow(CompletionAssistantRules.Rigger, false)]
    [DataRow(CompletionAssistantRules.Mage, false)]
    public void Utility_kit_uses_admitted_source_identities_and_role_preferences(string archetype, bool quietEntry)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs, archetype)));
        var lines = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        foreach (Guid sourceId in new[] { TransceiverSource, MicSource, quietEntry ? PicksSource : EraserSource, LightSource })
        {
            var source = inputs.Lifestyles.Gear.State.Authority.Options.Single(row => row.SourceId == sourceId);
            Assert.IsTrue(source.IsSelectable && source.PricingIsExact && source.AvailabilityIsExact);
            Assert.AreEqual(0, source.Blockers.Count);
            var selection = proposal.Request.Purchases.Single(row => row.Gear?.OptionId == source.OptionId).Gear!;
            Assert.AreEqual(1, selection.Quantity);
            Assert.AreEqual(source.SourceNodeDigest, lines.Single(row => row.SourceId == sourceId).SourceNodeDigest);
        }
        Assert.IsFalse(lines.Any(row => row.SourceId == (quietEntry ? EraserSource : PicksSource)));
        Assert.AreEqual(lines.Count, lines.Select(row => row.SourceId).Distinct().Count());
        Assert.IsTrue(lines.All(line => inputs.Lifestyles.Gear.State.Authority.Options.Single(
            option => option.OptionId == line.OptionId).IsSelectable));
        Assert.AreEqual(CompletionCoverageState.Unsupported,
            proposal.Coverage.Single(row => row.Category == "advanced-gear").State);
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Saved_gear_identity_and_partial_package_quantity_remain_exact()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        SaveEquipment(fixture, (AmmoSource, 7), (TransceiverSource, 3), (PicksSource, 2));
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var original = inputs.Lifestyles.Gear.State.PendingDraft!.Lines;
        Assert.AreEqual(7, original.Single(row => row.SourceId == AmmoSource).Quantity);
        Assert.AreEqual(14m, original.Single(row => row.SourceId == AmmoSource).TotalCost);
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var files = fixture.CaptureFiles();
        var intent = Intent(fixture, inputs, CompletionAssistantRules.Face);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var final = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        foreach (var saved in original)
        {
            Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Gear?.OptionId == saved.OptionId));
            CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(saved),
                JsonSerializer.SerializeToUtf8Bytes(final.Single(row => row.SourceId == saved.SourceId)));
            Assert.AreEqual(new CharacterCreationGearSelection(saved.OptionId, saved.Quantity),
                proposal.Evaluation.Value.Selections.Gear.Single(row => row.OptionId == saved.OptionId));
        }
        Assert.AreEqual(final.Count, final.Select(row => row.SourceId).Distinct().Count());
        var repeated = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.AreEqual(proposal.ProposalDigest, repeated.ProposalDigest);
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Disabled_equipment_sources_are_omitted_without_losing_loadout()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = DisableEquipment(fixture.ReadInputs(includeLoadout: true));
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai)));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Gear is not null));
        Assert.IsNotNull(proposal.Loadout);
        Assert.AreEqual(1, proposal.Loadout.Selections.Weapons.Count);
        Assert.AreEqual(1, proposal.Loadout.Selections.Armor.Count);
        Assert.AreEqual(0, proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines.Count);
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Unaffordable_ammo_preserves_existing_choices_and_reserve(bool constrainReserve)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        SaveEquipment(fixture, (TransceiverSource, 2));
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var intent = Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai);
        var funded = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.IsNotNull(funded.Loadout);
        var noSpend = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { MaximumAdditionalPurchaseSpend = 0m }));
        decimal available = noSpend.Evaluation.Value!.Review.Balances.RemainingNuyen!.Value;
        // Both authored ammo quantities exceed this remaining allowance. Core
        // must keep accepted weapon/armor and saved gear while rejecting ammo.
        decimal allowance = funded.Loadout.PurchaseCost + 99m;
        var constrained = constrainReserve
            ? intent with { Reserves = new(Nuyen: available - allowance) }
            : intent with { MaximumAdditionalPurchaseSpend = allowance };
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, constrained));
        var lines = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        Assert.IsFalse(lines.Any(row => row.SourceId == AmmoSource));
        var saved = inputs.Lifestyles.Gear.State.PendingDraft!.Lines.Single();
        CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(saved),
            JsonSerializer.SerializeToUtf8Bytes(lines.Single(row => row.SourceId == saved.SourceId)));
        CollectionAssert.AreEqual(funded.Loadout.Selections.Weapons.ToArray(), proposal.Loadout!.Selections.Weapons.ToArray());
        CollectionAssert.AreEqual(funded.Loadout.Selections.Armor.ToArray(), proposal.Loadout.Selections.Armor.ToArray());
        Assert.IsTrue(proposal.Evaluation.Value.Purchases!.AdditionalSpend <= allowance);
        Assert.IsTrue(proposal.Evaluation.Value.Review.Balances.RemainingNuyen >= constrained.Reserves.Nuyen);
        Assert.IsFalse(proposal.Evaluation.Blockers.Contains(CharacterCreationPurchaseCompletionBlockers.TargetsUnfulfilled));
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void No_new_conventional_firearm_does_not_propose_regular_ammo(bool includeLoadout)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var files = fixture.CaptureFiles();
        var inputs = fixture.ReadInputs(includeLoadout);
        var intent = Intent(fixture, inputs, CompletionAssistantRules.Mage);
        // Exercise both a legacy observation without loadout and a real catalog
        // whose budget admits no purchase. A preferred Taser is not currently
        // source-projectable; do not invent admission just to create a fixture.
        if (includeLoadout) intent = intent with { MaximumAdditionalPurchaseSpend = 0m };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null));
        Assert.IsFalse(proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines.Any(row => row.SourceId == AmmoSource));
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Affordable_ammo_is_not_proposed_when_real_firearm_candidates_exceed_positive_budget()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var files = fixture.CaptureFiles();
        const decimal allowance = 100m;
        var firearms = inputs.Loadout!.Authority.Options.Where(row => row.Kind == CharacterCreationLoadoutKinds.Weapon
            && row.Category is "Tasers" or "Light Pistols" or "Heavy Pistols"
            && row.IsSelectable && row.PricingIsExact && row.AvailabilityIsExact && row.Blockers.Count == 0).ToArray();
        Assert.IsTrue(firearms.Length > 0 && firearms.All(row => row.UnitCost > allowance),
            "The real candidate catalog must contain weapons, but none affordable in this positive allowance.");
        var ammo = inputs.Lifestyles.Gear.State.Authority.Options.Single(row => row.SourceId == AmmoSource);
        Assert.IsTrue(ammo.IsSelectable && ammo.PricingIsExact && ammo.Blockers.Count == 0);
        Assert.AreEqual(allowance, ammo.PackageCost * 50 / ammo.PackageQuantity,
            "Fifty rounds would fit; their omission must depend on the missing firearm, not a zero budget.");
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.Mage) with { MaximumAdditionalPurchaseSpend = allowance }));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Gear?.OptionId == ammo.OptionId));
        Assert.IsTrue(proposal.Request.Purchases.Any(row => row.Gear is not null),
            "An affordable utility must still be offered; the resource stage cannot have returned early.");
        Assert.IsTrue(proposal.Evaluation.Value!.Purchases!.AdditionalSpend <= allowance);
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Rejected_equipment_batch_keeps_affordable_ammo_then_later_utilities_in_preference_order()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var intent = Intent(fixture, inputs, CompletionAssistantRules.GeneralSurvivor);
        var funded = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.IsNotNull(funded.Loadout);
        // The full kit does not fit. After the retained weapon/armor, Core can
        // admit 100 rounds, skip the transceiver, admit the mic, skip lockpicks,
        // and admit the light. A failed batch must neither leak partial state
        // nor prevent the original ordered, per-item fallback from running.
        decimal cap = funded.Loadout.PurchaseCost + 275m;
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { MaximumAdditionalPurchaseSpend = cap }));
        var lines = proposal.Evaluation.Value!.Review.Lifestyles!.Gear.Lines;
        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual(100, lines.Single(row => row.SourceId == AmmoSource).Quantity);
        Assert.AreEqual(200m, lines.Single(row => row.SourceId == AmmoSource).TotalCost);
        Assert.AreEqual(50m, lines.Single(row => row.SourceId == MicSource).TotalCost);
        Assert.AreEqual(25m, lines.Single(row => row.SourceId == LightSource).TotalCost);
        Assert.IsFalse(lines.Any(row => row.SourceId == TransceiverSource || row.SourceId == PicksSource));
        Assert.AreEqual(cap, proposal.Evaluation.Value.Purchases!.AdditionalSpend);
        Assert.IsFalse(proposal.Evaluation.Blockers.Contains(CharacterCreationPurchaseCompletionBlockers.TargetsUnfulfilled));
        CollectionAssert.AreEqual(funded.Loadout.Selections.Weapons.ToArray(), proposal.Loadout!.Selections.Weapons.ToArray());
        CollectionAssert.AreEqual(funded.Loadout.Selections.Armor.ToArray(), proposal.Loadout.Selections.Armor.ToArray());
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    private static void SaveEquipment(CompletionRecommendationTestFixture fixture, params (Guid SourceId, int Quantity)[] items)
    {
        // Existing fixture confirms the real prerequisites, resources and empty
        // gear; this save adds genuine player selections with Core receipts.
        fixture.SaveAttributes([], []);
        fixture.PrepareContacts();
        var service = new CharacterCreationGearService(fixture.Store, fixture.Resolver);
        var state = service.Load(new(fixture.WorkspaceId)).Value!;
        var selections = items.Select(item => new CharacterCreationGearSelection(
            state.Authority.Options.Single(row => row.SourceId == item.SourceId).OptionId, item.Quantity)).ToArray();
        var preview = service.Preview(new(state.Binding, selections));
        Assert.IsNotNull(preview.Value, string.Join(",", preview.Blockers));
        Assert.IsTrue(preview.Value.CanConfirm, string.Join(",", preview.Value.Blockers));
        var saved = service.Confirm(new(preview.Value.Binding, selections, preview.Value.PreviewDigest,
            "completion-retained-practical-equipment", true));
        Assert.AreEqual(CharacterCreationGearOutcomes.Applied, saved.Outcome, string.Join(",", saved.Blockers));
    }

    private static CharacterCreationCompletionReviewInputs DisableEquipment(CharacterCreationCompletionReviewInputs inputs)
    {
        Guid[] sources = [AmmoSource, TransceiverSource, MicSource, EraserSource, PicksSource, LightSource];
        // A negative observation only: retain real nodes, package prices and
        // quantities; mark these options unavailable, never invent admission.
        var authority = inputs.Lifestyles.Gear.State.Authority;
        authority = authority with
        {
            Options = authority.Options.Select(option =>
            {
                if (!sources.Contains(option.SourceId)) return option;
                var disabled = option with { IsSelectable = false,
                    Blockers = [.. option.Blockers, CharacterCreationGearBlockers.SourceDisabled] };
                return disabled with { OptionDigest = CharacterCreationGearRules.ComputeOptionDigest(disabled) };
            }).ToArray()
        };
        authority = authority with { AuthorityDigest = CharacterCreationGearRules.ComputeAuthorityDigest(authority) };
        Assert.IsTrue(CharacterCreationGearRules.IsValidAuthority(authority));
        var state = inputs.Lifestyles.Gear.State with { Authority = authority,
            Binding = inputs.Lifestyles.Gear.State.Binding with { AuthorityDigest = authority.AuthorityDigest } };
        state = state with { SnapshotDigest = CharacterCreationGearRules.ComputeStateDigest(state) };
        var gear = inputs.Lifestyles.Gear with { State = state };
        gear = gear with { SnapshotDigest = CharacterCreationGearCompletionRules.ComputeInputsDigest(gear) };
        var lifestyles = inputs.Lifestyles with { Gear = gear };
        lifestyles = lifestyles with { SnapshotDigest = CharacterCreationLifestylesCompletionRules.ComputeInputsDigest(lifestyles) };
        inputs = inputs with { Lifestyles = lifestyles };
        return inputs with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs) };
    }
}
