using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed partial class CompletionKarmaRecommendationBuilderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Missing_only_uses_real_remaining_karma_for_unlocked_role_goals_without_writes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        foreach (var target in proposal.Request.Attributes.KarmaTargets ?? [])
        {
            Assert.IsTrue(proposal.Request.Attributes.PermittedAttributeOrder.Contains(target.AttributeId));
            Assert.AreEqual(CharacterCreationAttributeCategories.Normal,
                inputs.Skills.Attributes.Attributes.Single(row => row.AttributeId == target.AttributeId).Category);
        }
        var plan = proposal.Evaluation.Value;
        Assert.IsNotNull(plan, string.Join(",", proposal.Evaluation.Blockers));
        Assert.AreSame(plan, ReviewedKarmaPlan(proposal));
        Assert.IsTrue(plan.Selections.AttributeAllocations.Any(row => row.KarmaLevels > 0)
            || plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Any(),
            "A legal useful purchase must not be replaced by silent finalization loss.");
        Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "karma-advice"));
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Karma_advice_keeps_player_zero_and_whole_wallet_reserve()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("BOD", 0, 0)], ["BOD"]);
        var inputs = fixture.ReadInputs();
        int reserve = inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal;
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { Reserves = new(Karma: reserve) }));
        Assert.IsFalse(proposal.Request.Attributes.PermittedAttributeOrder.Contains("BOD"));
        Assert.IsFalse(proposal.Request.Attributes.KarmaTargets?.Any(row => row.AttributeId == "BOD") == true);
        var plan = proposal.Evaluation.Value;
        Assert.IsNotNull(plan, string.Join(",", proposal.Evaluation.Blockers));
        Assert.AreEqual(inputs.Skills.Attributes.PendingDraft!.Allocations.Single(row => row.AttributeId == "BOD"),
            plan.Selections.AttributeAllocations.Single(row => row.AttributeId == "BOD"));
        Assert.IsTrue(plan.Selections.AttributeAllocations.All(row => row.KarmaLevels == 0));
        Assert.IsTrue(plan.Review.Balances.RemainingCreationKarma >= reserve);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Explicit_pack_and_legacy_unknown_attribute_intent_do_not_enable_karma_spending()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs);
        var explicitPack = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent,
            CompletionRecommendationBuilder.GeneralSurvivorPack));
        Assert.IsFalse(explicitPack.Request.AllocateRemainingAttributeKarma);
        Assert.IsNull(explicitPack.Request.Attributes.KarmaTargets);
        Assert.AreEqual(0, explicitPack.Request.Attributes.MaximumAdditionalKarmaSpend);

        fixture.SaveAttributes([new("BOD", 0, 0)]);
        var legacy = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var locked = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(legacy, Intent(fixture, legacy)));
        Assert.IsFalse(locked.Request.AllocateRemainingAttributeKarma);
        Assert.IsNull(locked.Request.Attributes.KarmaTargets);
        fixture.AssertFilesUnchanged(files);
    }

    private static CompletionAssistantIntent Intent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs)
        => new(fixture.Owners.Capture(), inputs.SnapshotDigest, CompletionAssistantRules.GeneralSurvivor,
            new([], []), new());

    private static CompletionAssistantProposal Proposal(CharacterCreationFoundationResult<CompletionAssistantProposal> result)
    {
        Assert.IsNotNull(result.Value, string.Join(",", result.Blockers));
        return result.Value;
    }
}
