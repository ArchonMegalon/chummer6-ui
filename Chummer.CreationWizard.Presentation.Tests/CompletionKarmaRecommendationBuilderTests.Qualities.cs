using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CompletionKarmaRecommendationBuilderTests
{
    private static readonly Guid QuickHealer = new("291efdb6-a8b8-49ce-b2be-72f9d3f8a243");
    private static readonly Guid Guts = new("0d0cb0fc-c79d-418e-9216-8a197369e52d");

    [TestMethod]
    public void Exact_22_wallet_keeps_retained_investment_and_compares_useful_additions_without_writes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        SaveResourceInvestment(fixture, 3);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs);
        var baseline = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent,
            CompletionRecommendationBuilder.GeneralSurvivorPack));
        Assert.IsNotNull(baseline.Evaluation.Value);
        // This is a deterministic standard-profile reproduction, not the absent
        // user's runner snapshot. The 22 comes from a real saved Core investment.
        Assert.AreEqual(22, baseline.Evaluation.Value.Review.Balances.RemainingCreationKarma);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);

        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsTrue(proposal.Evaluation.Blockers.Contains(CharacterCreationAttributeCompletionBlockers.SpecialPointsUnallocated),
            "Karma advice must not hide the separate unallocated special-points blocker.");
        AssertKarmaBaselineAdmissionRejectsOtherFailures(proposal.Evaluation);
        Assert.IsTrue(plan.Review.Balances.RemainingCreationKarma < 22);
        Assert.IsTrue(plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Any(),
            "A legal positive quality must compete with normal-attribute spending.");
        Assert.AreEqual(3, plan.Selections.TotalKarmaInvestment);
        Assert.IsTrue(inputs.Lifestyles.Qualities.SelectedOptionIds.All(plan.Selections.QualityOptionIds.Contains));
        Assert.AreEqual(plan.Selections.QualityOptionIds.Count, plan.Selections.QualityOptionIds.Distinct().Count());
        AssertAddedQualitiesAdmitted(inputs, plan);
        TestContext.WriteLine("Synthetic 22-Karma wallet: {0} remaining; proposed qualities: {1}; attribute Karma levels: {2}",
            plan.Review.Balances.RemainingCreationKarma,
            string.Join(", ", inputs.Lifestyles.Qualities.Authority.Options.Where(row =>
                plan.Selections.QualityOptionIds.Contains(row.OptionId)).Select(row => row.Name)),
            string.Join(", ", plan.Selections.AttributeAllocations.Where(row => row.KarmaLevels > 0)
                .Select(row => row.AttributeId + ":" + row.KarmaLevels)));
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Selected_positive_quality_is_preserved_once_without_replacing_the_manual_baseline()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        SaveUnchosenAttributes(fixture);
        SaveQuality(fixture, Guts);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var plan = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs))));
        Assert.IsTrue(inputs.Lifestyles.Qualities.SelectedOptionIds.All(plan.Selections.QualityOptionIds.Contains));
        Assert.AreEqual(1, inputs.Lifestyles.Qualities.Authority.Options.Count(row => row.SourceId == Guts
            && plan.Selections.QualityOptionIds.Contains(row.OptionId)));
        AssertAddedQualitiesAdmitted(inputs, plan);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Qualities_can_spend_karma_when_all_attribute_choices_are_player_locked()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var original = fixture.ReadInputs();
        var filled = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(original, Intent(fixture, original))));
        var allocations = filled.Selections.AttributeAllocations.ToArray();
        fixture.SaveAttributes(allocations, allocations.Select(row => row.AttributeId).ToArray());
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();

        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsFalse(proposal.Request.AllocateRemainingAttributeKarma);
        Assert.AreEqual(0, proposal.Request.Attributes.PermittedAttributeOrder.Count);
        CollectionAssert.AreEquivalent(allocations, plan.Selections.AttributeAllocations.ToArray());
        Assert.IsTrue(plan.Selections.QualityOptionIds.Count > inputs.Lifestyles.Qualities.SelectedOptionIds.Count);
        AssertAddedQualitiesAdmitted(inputs, plan);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void One_unreserved_karma_does_not_authorize_an_unaffordable_quality_or_attribute()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        int reserve = inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal - 1;
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { Reserves = new(Karma: reserve) }));
        Assert.IsNotNull(proposal.Evaluation.Value);
        Assert.IsTrue(proposal.Evaluation.Value.Review.Balances.RemainingCreationKarma >= reserve);
        Assert.AreEqual(0, proposal.Request.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Count());
        Assert.IsTrue(proposal.Evaluation.Value.Selections.AttributeAllocations.All(row => row.KarmaLevels == 0));
        Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "karma-remaining"
            && row.ExplanationKey == "completion.karma.remaining-bounded-or-manual"));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Only_currently_selectable_exact_source_admitted_positive_qualities_are_proposed()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var plan = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs))));
        Assert.IsTrue(plan.Selections.QualityOptionIds.Count > 0);
        AssertAddedQualitiesAdmitted(inputs, plan);
        var disabled = inputs.Lifestyles.Qualities.Authority.Options
            .Where(row => !row.IsSelectable || !row.EligibilityIsExact || row.SourceAnchorIds.Count == 0)
            .Select(row => row.OptionId).ToHashSet();
        Assert.IsFalse(plan.Selections.QualityOptionIds.Any(disabled.Contains));
        Assert.AreEqual(0, plan.Review.UpstreamBudget!.Qualities.Blockers.Count,
            "Core must independently enforce current quality limits and pairwise eligibility.");
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Unresolved_exclusive_quality_stays_manual_even_when_its_price_fits()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        // Current Core cannot project its forbidden-quality graph. A cheap
        // catalog price does not authorize the UI to resolve that exclusion.
        var exclusive = inputs.Lifestyles.Qualities.Authority.Options.Single(row => row.SourceId == QuickHealer);
        Assert.IsFalse(exclusive.IsSelectable);
        Assert.IsFalse(exclusive.EligibilityIsExact);
        Assert.IsTrue(exclusive.KarmaCost < inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsFalse(inputs.Lifestyles.Qualities.Authority.Options.Any(row => row.SourceId == QuickHealer
            && plan.Selections.QualityOptionIds.Contains(row.OptionId)));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Karma_search_cancellation_preserves_the_original_observation_and_saved_bytes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => CompletionRecommendationBuilder.BuildMissingOnly(
            inputs, Intent(fixture, inputs), cancellation.Token));
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    private static void AssertKarmaBaselineAdmissionRejectsOtherFailures(
        CharacterCreationFoundationResult<CharacterCreationCompletionPlan> original)
    {
        var admission = typeof(CompletionRecommendationBuilder).GetMethod("HasOnlyUnspentSpecialPoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(admission);
        Assert.IsNotNull(original.Value);
        bool Admits(CharacterCreationFoundationResult<CharacterCreationCompletionPlan> candidate)
            => (bool)admission.Invoke(null, [candidate])!;
        Assert.IsTrue(Admits(original));
        string[] added = [.. original.Blockers, "synthetic-other-core-blocker"];
        Assert.IsFalse(Admits(original with
        {
            Blockers = added, Value = original.Value with { Blockers = added }
        }), "An additional failure must stop Karma advice, even when both blocker lists agree.");
        Assert.IsFalse(Admits(original with { Outcome = CharacterCreationFoundationOutcomes.Conflict }));
        Assert.IsFalse(Admits(original with { Outcome = CharacterCreationFoundationOutcomes.Success }),
            "A success outcome cannot conceal retained blockers.");
        Assert.IsFalse(Admits(original with { Value = original.Value with { Blockers = [] } }),
            "A mismatched inner plan must not be admitted.");
        Assert.IsFalse(Admits(original with { Value = original.Value with
            { Review = original.Value.Review with { Blockers = ["synthetic-review-failure"] } } }));
    }

    private static CharacterCreationCompletionPlan ReviewedKarmaPlan(CompletionAssistantProposal proposal)
    {
        Assert.IsNotNull(proposal.Evaluation.Value);
        // The fixture intentionally leaves the separate special-point pool
        // unfinished. Karma advice must preserve its real Core blocked state,
        // not require or invent a fully finished character to improve a plan.
        Assert.IsTrue(proposal.Evaluation.Blockers.All(code =>
            code == CharacterCreationAttributeCompletionBlockers.SpecialPointsUnallocated),
            string.Join(",", proposal.Evaluation.Blockers));
        Assert.AreEqual(proposal.Evaluation.Blockers.Count == 0
                ? CharacterCreationFoundationOutcomes.Success : CharacterCreationFoundationOutcomes.Blocked,
            proposal.Evaluation.Outcome);
        CollectionAssert.AreEqual(proposal.Evaluation.Blockers.ToArray(), proposal.Evaluation.Value.Blockers.ToArray());
        Assert.AreEqual(0, proposal.Evaluation.Value.Review.Blockers.Count);
        return proposal.Evaluation.Value;
    }

    private static void AssertAddedQualitiesAdmitted(CharacterCreationCompletionReviewInputs inputs,
        CharacterCreationCompletionPlan plan)
    {
        foreach (string id in plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds))
        {
            var option = inputs.Lifestyles.Qualities.Authority.Options.Single(row => row.OptionId == id);
            Assert.AreEqual(CharacterCreationQualityType.Positive, option.Type);
            Assert.IsTrue(option.IsSelectable && option.EligibilityIsExact && option.SourceAnchorIds.Count > 0);
            Assert.IsFalse(option.IsFreeOrGranted || option.IsMetagenic);
            Assert.IsNull(option.FollowUpChoiceId);
            Assert.IsNull(option.FollowUpChoiceLabel);
        }
    }

    private static void SaveUnchosenAttributes(CompletionRecommendationTestFixture fixture)
    {
        var state = fixture.ReadInputs().Skills.Attributes;
        fixture.SaveAttributes(state.Attributes.Select(row =>
            new CharacterCreationAttributeAllocation(row.AttributeId, row.PriorityPointsSpent, row.KarmaLevels)).ToArray(), []);
    }

    private static void SaveResourceInvestment(CompletionRecommendationTestFixture fixture, int karma)
    {
        var service = new CharacterCreationResourcesService(fixture.Store, fixture.Resolver);
        var loaded = service.Load(new(fixture.WorkspaceId));
        Assert.IsNotNull(loaded.Value, string.Join(",", loaded.Blockers));
        var option = loaded.Value.Options.Single(row => row.KarmaInvestment == karma && row.IsEnabled && row.Blockers.Count == 0);
        var evaluated = service.Preview(new(loaded.Value.Binding, option.OptionId));
        Assert.IsNotNull(evaluated.Value, string.Join(",", evaluated.Blockers));
        Assert.IsTrue(evaluated.Value.CanConfirm, string.Join(",", evaluated.Value.Blockers));
        var saved = service.Confirm(new(evaluated.Value.Binding, option.OptionId, evaluated.Value.PreviewDigest,
            "karma-investment-fixture", true));
        Assert.AreEqual(CharacterCreationResourcesOutcomes.Applied, saved.Outcome, string.Join(",", saved.Blockers));
    }

    private static void SaveQuality(CompletionRecommendationTestFixture fixture, Guid sourceId)
    {
        var service = new CharacterCreationQualitiesService(fixture.Store, fixture.Resolver,
            new CharacterCreationPrerequisiteService(fixture.Store, fixture.Queries, fixture.Resolver),
            new CharacterCreationAttributesService(fixture.Store, fixture.Resolver));
        var loaded = service.Load(new(fixture.WorkspaceId));
        Assert.IsNotNull(loaded.Value, string.Join(",", loaded.Blockers));
        var state = loaded.Value;
        var option = state.Authority.Options.Single(row => row.SourceId == sourceId && row.IsSelectable
            && row.EligibilityIsExact && row.FollowUpChoiceId is null);
        string[] ids = [.. state.Preview.Selections.Select(row => row.OptionId), option.OptionId];
        var evaluated = service.Preview(new(state.Binding, ids));
        Assert.IsNotNull(evaluated.Value, string.Join(",", evaluated.Blockers));
        Assert.IsTrue(evaluated.Value.CanConfirm, string.Join(",", evaluated.Value.Blockers));
        var result = service.Confirm(new(evaluated.Value.Binding, ids, evaluated.Value.PreviewDigest,
            "karma-retained-quality-" + sourceId, Guid.NewGuid(), true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
    }
}
