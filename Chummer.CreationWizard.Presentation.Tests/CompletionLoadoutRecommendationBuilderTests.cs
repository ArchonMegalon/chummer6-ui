using System.Text.Json;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed partial class CompletionLoadoutRecommendationBuilderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(CompletionAssistantRules.StreetSamurai)]
    [DataRow(CompletionAssistantRules.Face)]
    [DataRow(CompletionAssistantRules.GeneralSurvivor)]
    public void Funded_loadout_uses_typed_Core_choices_and_real_summary_without_writes(string archetype)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        Assert.IsNotNull(inputs.Loadout);
        Assert.IsTrue(CharacterCreationLoadoutRules.IsValidAuthority(inputs.Loadout.Authority));
        var files = fixture.CaptureFiles();
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var intent = Intent(fixture, inputs, archetype);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.AreEqual(intent.OwnerContext, proposal.Intent.OwnerContext);
        Assert.IsNotNull(proposal.Loadout, string.Join(",", proposal.Evaluation.Blockers));
        Assert.AreEqual(0, proposal.Loadout.Blockers.Count);
        Assert.AreEqual(1, proposal.Request.Purchases.Count(row => row.Weapon is not null));
        Assert.AreEqual(1, proposal.Request.Purchases.Count(row => row.Armor is not null));
        Assert.AreEqual(2, proposal.Loadout.Lines.Count);
        foreach (var target in proposal.Request.Purchases.Where(row => row.Weapon is not null || row.Armor is not null))
        {
            Assert.IsNull(target.Gear, "A weapon or armor is never a generic Gear purchase.");
            Assert.IsNull(target.Lifestyle);
        }
        foreach (var line in proposal.Loadout.Lines)
        {
            var source = inputs.Loadout.Authority.Options.Single(row => row.OptionId == line.OptionId);
            Assert.IsTrue(source.IsSelectable && source.PricingIsExact && source.AvailabilityIsExact);
            Assert.AreEqual(source.Name, line.Name);
            Assert.AreEqual(source.UnitCost, line.UnitCost);
            Assert.AreEqual(1, line.Quantity);
            Assert.AreEqual(source.UnitCost, line.TotalCost);
            Assert.AreEqual(source.SourceId, line.SourceId);
        }
        Assert.AreEqual(proposal.Loadout.Lines.Sum(row => row.TotalCost), proposal.Loadout.PurchaseCost);
        Assert.IsTrue(proposal.Evaluation.Value!.Purchases!.AdditionalSpend <= intent.MaximumAdditionalPurchaseSpend!.Value);
        Assert.IsTrue(proposal.Evaluation.Value.Review.Balances.RemainingNuyen >= intent.Reserves.Nuyen);
        Assert.AreEqual(inputs.Loadout.SnapshotDigest, proposal.Evaluation.Value.Purchases.Request.LoadoutSnapshotDigest);
        Assert.AreEqual(CompletionCoverageState.CoreValidated, proposal.Coverage.Single(row => row.Category == "weapon").State);
        Assert.AreEqual(CompletionCoverageState.CoreValidated, proposal.Coverage.Single(row => row.Category == "armor").State);
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.ammo-binding-loading-modifications"));
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.commlinks-rated-gear-child-graphs"));
        Assert.AreEqual(CompletionCoverageState.Unsupported, proposal.Coverage.Single(row => row.Category == "loadout").State);

        AssertFreshCore(inputs, proposal);
        var repeated = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.AreEqual(proposal.ProposalDigest, repeated.ProposalDigest);
        Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
        CollectionAssert.AreEqual(original, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Missing_source_authority_never_produces_typed_advice_or_a_validated_summary()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var original = fixture.ReadInputs(includeLoadout: true);
        Assert.IsNotNull(original.Loadout);
        // A negative observation, never a fabricated approval or source price.
        var unavailable = original.Loadout with { Authority = CharacterCreationLoadoutAuthority.Unavailable };
        unavailable = unavailable with { SnapshotDigest = CharacterCreationLoadoutCompletionRules.ComputeInputsDigest(unavailable) };
        var inputs = original with { Loadout = unavailable };
        inputs = inputs with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs) };
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.GeneralSurvivor)));
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null || row.Armor is not null));
        Assert.IsNull(proposal.Loadout);
        Assert.IsTrue(proposal.Evaluation.Blockers.Count > 0);
        Assert.IsFalse(proposal.Coverage.Any(row => row.Category is "weapon" or "armor"
            && row.State == CompletionCoverageState.CoreValidated));
        AssertFreshCore(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Actual_funds_and_zero_spend_permission_prevent_loadout_additions()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var intent = Intent(fixture, inputs, CompletionAssistantRules.GeneralSurvivor);
        var files = fixture.CaptureFiles();
        var noSpend = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { MaximumAdditionalPurchaseSpend = 0m }));
        Assert.IsNotNull(noSpend.Evaluation.Value);
        Assert.IsNotNull(noSpend.Evaluation.Value.Review.Balances.RemainingNuyen);
        decimal available = noSpend.Evaluation.Value.Review.Balances.RemainingNuyen.Value;
        Assert.IsTrue(available > 1m);
        var reservedIntent = intent with { Reserves = new(Nuyen: available - 1m) };
        var reserved = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, reservedIntent));
        foreach (var proposal in new[] { noSpend, reserved })
        {
            Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null || row.Armor is not null));
            Assert.IsNotNull(proposal.Loadout);
            Assert.AreEqual(0, proposal.Loadout.Lines.Count);
            AssertFreshCore(inputs, proposal);
        }
        Assert.AreEqual(intent.MaximumAdditionalPurchaseSpend!.Value, reserved.Request.MaximumAdditionalPurchaseSpend);
        Assert.AreEqual(reservedIntent.Reserves.Nuyen, reserved.Request.NuyenToReserve);
        Assert.IsTrue(reserved.Evaluation.Value!.Review.Balances.RemainingNuyen >= reservedIntent.Reserves.Nuyen);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Catalog_order_does_not_change_archetype_choices()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        Assert.IsNotNull(inputs.Loadout);
        // Only catalog enumeration changes. All source nodes, prices and blockers
        // remain the real reader's values; the public Core digest rules bind order.
        var authority = inputs.Loadout.Authority with { Options = inputs.Loadout.Authority.Options.Reverse().ToArray() };
        authority = authority with { AuthorityDigest = CharacterCreationLoadoutRules.ComputeAuthorityDigest(authority) };
        var loadout = inputs.Loadout with { Authority = authority };
        loadout = loadout with { SnapshotDigest = CharacterCreationLoadoutCompletionRules.ComputeInputsDigest(loadout) };
        var reordered = inputs with { Loadout = loadout };
        reordered = reordered with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(reordered) };
        var selectedWeapons = new List<string>();
        foreach (string archetype in new[] { CompletionAssistantRules.StreetSamurai, CompletionAssistantRules.Face })
        {
            var first = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs, archetype)));
            var second = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(reordered, Intent(fixture, reordered, archetype)));
            Assert.IsNotNull(first.Loadout);
            Assert.IsNotNull(second.Loadout);
            CollectionAssert.AreEqual(first.Loadout.Selections.Weapons.ToArray(), second.Loadout.Selections.Weapons.ToArray());
            CollectionAssert.AreEqual(first.Loadout.Selections.Armor.ToArray(), second.Loadout.Selections.Armor.ToArray());
            selectedWeapons.Add(first.Loadout.Selections.Weapons.Single().OptionId);
            AssertFreshCore(reordered, second);
        }
        Assert.AreNotEqual(selectedWeapons[0], selectedWeapons[1], "The real source fixture should exercise distinct role advice.");
    }

    [TestMethod]
    public void Nonempty_original_typed_choices_fail_closed_without_overwrite()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var original = fixture.ReadInputs(includeLoadout: true);
        var selected = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(original,
            Intent(fixture, original, CompletionAssistantRules.Face)));
        Assert.IsNotNull(selected.Loadout);
        Assert.AreEqual(2, selected.Loadout.Lines.Count);
        // A genuine proposed basket is not an admitted original graph. Core's
        // current input contract requires empty original selections, even when
        // the caller recomputes both public snapshot digests.
        var loadout = original.Loadout! with { OriginalSelections = selected.Loadout.Selections };
        loadout = loadout with { SnapshotDigest = CharacterCreationLoadoutCompletionRules.ComputeInputsDigest(loadout) };
        var inputs = original with { Loadout = loadout };
        inputs = inputs with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(inputs) };
        var files = fixture.CaptureFiles();
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(inputs);
        var preview = CharacterCreationLoadoutCompletionService.Preview(loadout, loadout.OriginalSelections);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, preview.Outcome);
        Assert.IsNull(preview.Value);
        CollectionAssert.AreEqual(new[] { CharacterCreationLoadoutCompletionSchemas.RequestInvalid }, preview.Blockers.ToArray());
        var result = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.StreetSamurai));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, result.Outcome);
        var proposal = Proposal(result);
        Assert.IsFalse(proposal.Request.Purchases.Any(row => row.Weapon is not null || row.Armor is not null));
        Assert.IsNull(proposal.Loadout);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, proposal.Evaluation.Outcome);
        Assert.IsNull(proposal.Evaluation.Value);
        CollectionAssert.AreEqual(new[] { CharacterCreationCompletionReviewSchemas.AuthorityUnavailable },
            proposal.Evaluation.Blockers.ToArray());
        Assert.IsFalse(proposal.Coverage.Any(row => row.Category is "weapon" or "armor"
            && row.State == CompletionCoverageState.CoreValidated));
        AssertFreshCore(inputs, proposal);
        CollectionAssert.AreEqual(before, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow("weapons", "weapon")]
    [DataRow("armors", "armor")]
    public void Unpriced_saved_manual_loadout_stays_blocked_and_unchanged(string collection, string kind)
    {
        var manual = new XElement(collection,
            new XElement(kind, new XElement("guid", Guid.NewGuid()), new XElement("name", "Manual retained choice")));
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, originalLoadout: manual);
        var document = fixture.Store.Get(fixture.WorkspaceId).Value!.Document;
        Assert.IsTrue(XNode.DeepEquals(manual, XElement.Parse(document.Content).Element(collection)));
        var files = fixture.CaptureFiles();
        // Bootstrap and prerequisite confirmation are genuine, but this manual
        // graph has no source-bound purchase cost. Both readers must reject it
        // at the upstream budget boundary, before optional loadout projection.
        IOwnerBoundCharacterCreationCompletionReader reader =
            new OwnerBoundCharacterCreationCompletionReader(fixture.Store, fixture.Owners, fixture.Resolver, fixture.Queries);
        var ordinary = reader.LoadCompletionInputs(fixture.Owners.Capture(), new(fixture.WorkspaceId));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, ordinary.Outcome);
        Assert.IsTrue(ordinary.Blockers.Contains(CharacterCreationResourcesBlockers.PurchaseCostAuthorityRequired),
            string.Join(",", ordinary.Blockers));
        Assert.IsNull(ordinary.Value);
        var result = reader.LoadCompletionInputsWithLoadout(fixture.Owners.Capture(), new(fixture.WorkspaceId));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, result.Outcome);
        Assert.IsTrue(result.Blockers.Contains(CharacterCreationResourcesBlockers.PurchaseCostAuthorityRequired),
            string.Join(",", result.Blockers));
        CollectionAssert.AreEqual(ordinary.Blockers.ToArray(), result.Blockers.ToArray());
        Assert.IsNull(result.Value);
        Assert.AreEqual(document.Content, fixture.Store.Get(fixture.WorkspaceId).Value!.Document.Content);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Pre_canceled_loadout_advice_preserves_original_observation_and_files()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs(includeLoadout: true);
        var files = fixture.CaptureFiles();
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(inputs);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs, CompletionAssistantRules.GeneralSurvivor), canceled.Token));
        CollectionAssert.AreEqual(original, JsonSerializer.SerializeToUtf8Bytes(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    private static CompletionAssistantIntent Intent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs, string archetype)
        => new(fixture.Owners.Capture(), inputs.SnapshotDigest, archetype, new([], []), new(Nuyen: 1000m))
        { MaximumAdditionalPurchaseSpend = 10000m };

    private static CompletionAssistantProposal Proposal(CharacterCreationFoundationResult<CompletionAssistantProposal> result)
    {
        Assert.IsNotNull(result.Value, string.Join(",", result.Blockers));
        return result.Value;
    }

    private static void AssertFreshCore(CharacterCreationCompletionReviewInputs inputs, CompletionAssistantProposal proposal)
        => CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(CharacterCreationCompletionPlanner.Plan(inputs, proposal.Request)),
            JsonSerializer.SerializeToUtf8Bytes(proposal.Evaluation), "Changed baskets require the final fresh whole Core evaluation.");
}
