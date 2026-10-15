using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CompletionRecommendationPlanningReuseTests
{
    private const string Perception = "04e1eb3e-e82d-485b-a7fd-1e677df2a070";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Resource_planning_reuse_matches_fresh_Core_with_and_without_accepted_purchases()
    {
        using var fixture = SavedBaseline();
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        byte[] originalInputs = JsonSerializer.SerializeToUtf8Bytes(inputs);
        Assert.IsNotNull(inputs.Skills.Attributes.PendingDraft);
        Assert.IsNotNull(inputs.Skills.SavedSkillsState);
        Assert.AreEqual(0, inputs.Lifestyles.Lifestyles.Count);
        Assert.IsTrue(inputs.Lifestyles.Gear.State.PendingDraft is null
            || inputs.Lifestyles.Gear.State.PendingDraft.Lines.Count == 0);

        foreach (decimal cap in new[] { 1m, 10000m })
        {
            var intent = Intent(fixture, inputs, cap);
            byte[] originalIntent = JsonSerializer.SerializeToUtf8Bytes(intent);
            var result = CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent);
            Assert.IsNotNull(result.Value, string.Join(",", result.Blockers));
            var proposal = result.Value;
            Assert.IsNotNull(proposal.Evaluation.Value, string.Join(",", proposal.Evaluation.Blockers));
            var plan = proposal.Evaluation.Value;
            Assert.AreEqual(CompletionAllocationPolicy.MissingOnly, proposal.AllocationPolicy);
            Assert.IsTrue(proposal.FillsRemainingResources);
            Assert.AreEqual(cap, proposal.Request.MaximumAdditionalPurchaseSpend);

            if (cap == 1m)
            {
                Assert.AreEqual(0, proposal.Request.Purchases.Count,
                    "Exercise rejected optional candidates and reuse of the unchanged whole request.");
                Assert.IsNull(proposal.Request.StartingLifestyleId);
                Assert.AreEqual(0, plan.Selections.Lifestyles.Count);
                Assert.AreEqual(0, plan.Selections.Gear.Count);
            }
            else
            {
                Assert.IsTrue(proposal.Request.Purchases.Any(row => row.Lifestyle is not null));
                Assert.IsTrue(proposal.Request.Purchases.Any(row => row.Gear is not null),
                    "Exercise an accepted cumulative purchase basket, not the unchanged-request path twice.");
                Assert.IsNotNull(plan.Purchases);
                Assert.IsTrue(plan.Purchases.AdditionalSpend > 0m);
                Assert.IsTrue(plan.Purchases.AdditionalSpend <= cap);
                Assert.IsTrue(plan.Selections.Lifestyles.Count > 0);
                Assert.IsTrue(plan.Selections.Gear.Count > 0);
                Assert.AreEqual(proposal.Request.StartingLifestyleId, plan.Selections.StartingLifestyleId);
            }

            // Recompute from the caller's original actual inputs and the final
            // complete request, independently of any private cached result.
            var fresh = CharacterCreationCompletionPlanner.Plan(inputs, proposal.Request);
            Assert.AreEqual(CharacterCreationFinalizationDigest.Compute(fresh),
                CharacterCreationFinalizationDigest.Compute(proposal.Evaluation),
                "Reusing an earlier result must preserve the complete Core evaluation, including blockers and costs.");
            CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(fresh),
                JsonSerializer.SerializeToUtf8Bytes(proposal.Evaluation));

            CollectionAssert.AreEqual(inputs.Skills.Attributes.PendingDraft.Allocations.ToArray(),
                plan.Selections.AttributeAllocations.ToArray(), "Saved legacy attribute choices must stay exact.");
            Assert.AreEqual(0, proposal.Request.Attributes.PermittedAttributeOrder.Count);
            Assert.IsFalse(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == Perception));
            Assert.IsNull(proposal.Request.Skills.NativeLanguageSourceId);
            foreach (var original in inputs.Skills.Allocations)
                Assert.AreEqual(original, plan.Selections.SkillAllocations.Single(row =>
                    row.Kind == original.Kind && row.SourceSkillId == original.SourceSkillId));
            Assert.AreEqual(1, plan.Selections.SkillAllocations.Count(row => row.IsNativeLanguage));

            var repeated = CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent);
            Assert.IsNotNull(repeated.Value, string.Join(",", repeated.Blockers));
            Assert.AreEqual(proposal.ProposalDigest, repeated.Value.ProposalDigest);
            CollectionAssert.AreEqual(JsonSerializer.SerializeToUtf8Bytes(proposal.Evaluation),
                JsonSerializer.SerializeToUtf8Bytes(repeated.Value.Evaluation));
            Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
            CollectionAssert.AreEqual(originalIntent, JsonSerializer.SerializeToUtf8Bytes(intent));
            CollectionAssert.AreEqual(originalInputs, JsonSerializer.SerializeToUtf8Bytes(inputs));
            fixture.AssertFilesUnchanged(files);
        }
    }

    [TestMethod]
    public void Pre_canceled_resource_planning_never_changes_saved_choices_inputs_or_files()
    {
        using var fixture = SavedBaseline();
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs, 10000m);
        byte[] originalInputs = JsonSerializer.SerializeToUtf8Bytes(inputs);
        byte[] originalIntent = JsonSerializer.SerializeToUtf8Bytes(intent);
        var files = fixture.CaptureFiles();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, canceled.Token));

        CollectionAssert.AreEqual(originalInputs, JsonSerializer.SerializeToUtf8Bytes(inputs));
        CollectionAssert.AreEqual(originalIntent, JsonSerializer.SerializeToUtf8Bytes(intent));
        fixture.AssertFilesUnchanged(files);
    }

    private CompletionRecommendationTestFixture SavedBaseline()
    {
        var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        try
        {
            // Ordinary Core saves, not synthesized drafts or repaired hashes.
            // Unknown legacy edit intent conservatively retains every attribute.
            fixture.SaveAttributes([new("BOD", 3, 0)]);
            var inputs = fixture.ReadInputs();
            var native = inputs.Skills.Authority.KnowledgeSkills.First(row => row.CanBeNativeLanguage);
            fixture.SaveSkills([
                new(Perception, CharacterCreationSkillKinds.Active, 2, null, false),
                new(native.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)
            ], []);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private static CompletionAssistantIntent Intent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs, decimal cap)
        => new(fixture.Owners.Capture(), inputs.SnapshotDigest, CompletionAssistantRules.GeneralSurvivor,
            new([], []), new()) { MaximumAdditionalPurchaseSpend = cap };
}
