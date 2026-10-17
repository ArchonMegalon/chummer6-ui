using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CompletionKarmaRecommendationBuilderTests
{
    private const string KarmaPerception = "04e1eb3e-e82d-485b-a7fd-1e677df2a070";
    private const string KarmaSprawlLife = "0af06344-3637-4160-813b-a33dda2d16aa";

    [TestMethod]
    public void Future_skill_karma_exact_22_wallet_combines_useful_knowledge_and_qualities_without_spending_reserved_points()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        SaveResourceInvestment(fixture, 3);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Knowledge, KarmaSprawlLife, 22);
        var baseline = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { Reserves = intent.Reserves with { Karma = 22 } })));
        Assert.AreEqual(22, baseline.Review.Balances.RemainingCreationKarma);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);

        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.AreEqual(0, plan.Review.Balances.RemainingCreationKarma,
            "The standard source fixture admits useful knowledge plus positive qualities for this exact 22-Karma wallet.");
        Assert.AreEqual(3, plan.Review.Balances.SkillsKarmaCost);
        Assert.AreEqual(3, plan.Review.Balances.ResourcesKarmaInvestment);
        Assert.AreEqual(3, plan.Selections.TotalKarmaInvestment);
        var purchased = plan.Review.Skills!.Skills.Single(row => row.SourceSkillId == KarmaSprawlLife);
        Assert.AreEqual(2, purchased.Rating);
        Assert.AreEqual(2, purchased.KarmaLevels);
        Assert.AreEqual(0, purchased.PointCost);
        Assert.IsNull(purchased.SpecializationOptionId, "A wallet does not justify an arbitrary lore specialization.");
        Assert.IsTrue(plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Any());
        AssertAddedQualitiesAdmitted(inputs, plan);
        AssertCanonicalEqual(baseline.Selections.SkillAllocations, plan.Selections.SkillAllocations);
        AssertCanonicalEqual(baseline.Selections.GroupAllocations, plan.Selections.GroupAllocations);
        AssertCanonicalEqual(baseline.Review.Skills!.KnowledgeSkillPointBudget, plan.Review.Skills.KnowledgeSkillPointBudget);
        AssertCanonicalEqual(baseline.Review.Skills.Skills.Single(row => row.IsNativeLanguage),
            plan.Review.Skills.Skills.Single(row => row.IsNativeLanguage));
        AssertFutureSkillPlan(inputs, proposal);
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(CompletionAssistantRules.GeneralSurvivor, KarmaPerception, 3, null)]
    [DataRow(CompletionAssistantRules.GeneralSurvivor, "9cff9aa7-d092-4f89-8b7b-3ab835818874", 2, null)]
    [DataRow(CompletionAssistantRules.Rigger, "ae91a8a6-80e7-4f52-b9eb-21725a5528a4", 2, "Remote Operation")]
    public void Future_skill_karma_funds_a_missing_active_role_goal_with_exact_source_identity(
        string archetype, string sourceId, int rating, string? specialization)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Active, sourceId, 25, archetype);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        var source = inputs.Skills.Authority.ActiveSkills.Single(row => row.SourceSkillId == sourceId);
        var purchase = plan.Selections.SkillKarmaPurchases!.Single();
        Assert.AreEqual(source.SourceSkillId, purchase.SourceSkillId);
        Assert.AreEqual(source.Kind, purchase.Kind);
        Assert.AreEqual(source.SourceNodeDigest, purchase.SourceNodeDigest);
        Assert.AreEqual(rating, purchase.Rating);
        Assert.AreEqual(specialization is null ? null : source.Specializations.Single(row => row.Name == specialization).OptionId,
            purchase.SpecializationOptionId);
        var projected = plan.Review.Skills!.Skills.Single(row => row.SourceSkillId == source.SourceSkillId);
        Assert.AreEqual(0, projected.PointCost);
        Assert.AreEqual(rating, projected.KarmaLevels);
        Assert.AreEqual(specialization is not null, projected.KarmaSpecialization);
        Assert.IsTrue(projected.KarmaCost > 0);
        if (source.SkillGroup is not null)
            Assert.IsFalse(plan.Review.Skills.SkillGroups.Any(row => row.PointCost > 0
                && row.MemberSkillSourceIds.Contains(source.SourceSkillId)),
                "Catalog membership alone must not reject a legal individual purchase in an unfunded group.");
        Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "karma-skill-active-" + source.SourceSkillId
            && row.State == CompletionCoverageState.CoreValidated));
        if (specialization is not null)
        {
            CollectionAssert.Contains(projected.SourceAnchorIds.ToArray(),
                source.Specializations.Single(row => row.Name == specialization).SourceAnchorId);
            Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "karma-specialization-active-" + source.SourceSkillId
                && row.ExplanationKey == "completion.karma.reason.remote-operation"));
        }
        AssertFutureSkillPlan(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Skill_karma_competes_with_attributes_and_qualities_and_reaches_the_selected_plan_summary()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Active, KarmaPerception, 25);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsTrue(proposal.Request.Attributes.PermittedAttributeOrder.Count > 0,
            "Attribute candidates must remain in the same mixed search.");
        Assert.IsTrue(plan.Review.Balances.SkillsKarmaCost > 0);
        Assert.IsTrue(plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Any());
        var balances = plan.Review.Balances;
        Assert.AreEqual(balances.KarmaAfterAttributesAndQualities - balances.SkillsKarmaCost
            - balances.MysticAdeptPowerPointKarmaCost - balances.ResourcesKarmaInvestment - balances.ContactsKarmaCost,
            balances.RemainingCreationKarma);
        Assert.IsTrue(balances.RemainingCreationKarma >= 0);
        AssertFutureSkillPlan(inputs, proposal);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Skill_karma_raises_a_point_funded_rating_without_reallocating_the_point_pool()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Active, KarmaPerception, 25);
        intent = intent with { Reserves = intent.Reserves with { ActiveSkillPoints = inputs.Skills.SelectedActiveSkillPoints - 1 } };
        var baseline = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { Reserves = intent.Reserves with { Karma = 25 } })));
        var point = baseline.Skills!.Preview.Skills.Single(row => row.SourceSkillId == KarmaPerception);
        Assert.AreEqual(1, point.Rating);
        Assert.AreEqual(1, point.PointCost);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        var increased = plan.Skills!.Preview.Skills.Single(row => row.SourceSkillId == KarmaPerception);
        Assert.IsTrue(increased.Rating > point.Rating);
        Assert.AreEqual(increased.Rating - point.Rating, increased.KarmaLevels);
        Assert.AreEqual(point.PointCost, increased.PointCost);
        AssertCanonicalEqual(baseline.Selections.SkillAllocations, plan.Selections.SkillAllocations);
        AssertCanonicalEqual(baseline.Skills.Preview.ActiveSkillPointBudget, plan.Skills.Preview.ActiveSkillPointBudget);
        AssertFutureSkillPlan(inputs, proposal);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void One_unreserved_karma_cannot_buy_an_unaffordable_active_skill()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        var inputs = ReadSkillKarmaInputs(fixture);
        int total = inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal;
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Active, KarmaPerception, total);
        intent = intent with { Reserves = intent.Reserves with { Karma = total - 1 } };
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        var source = inputs.Skills.Authority.ActiveSkills.Single(row => row.SourceSkillId == KarmaPerception);
        var quote = CharacterCreationSkillsService.PreviewCompletionPurchases(inputs.Skills,
            plan.Selections.AttributeAllocations, plan.Selections.SkillAllocations, plan.Selections.GroupAllocations,
            [new(source.SourceSkillId, source.Kind, source.SourceNodeDigest, 1)]);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, quote.Outcome, string.Join(",", quote.Blockers));
        Assert.IsTrue(quote.Value!.Skills.Sum(row => row.KarmaCost) > 1, "Use Core's source price to establish unaffordability.");
        Assert.IsFalse(plan.Selections.SkillKarmaPurchases?.Count > 0);
        Assert.AreEqual(total, plan.Review.Balances.RemainingCreationKarma);
        Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "karma-remaining"
            && row.ExplanationKey == "completion.karma.remaining-bounded-or-manual"));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Skill_karma_keeps_funded_groups_and_Core_rejects_the_individual_compensation_purchase()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        var catalog = fixture.ReadInputs().Skills.Authority;
        var group = catalog.SkillGroups.Single(row => row.Name == "Athletics");
        fixture.SaveSkills([], [new(group.GroupId, 1)]);
        var inputs = ReadSkillKarmaInputs(fixture);
        Assert.IsTrue(inputs.Skills.KarmaPurchasePolicy!.CompensateSkillGroupKarmaDifference);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsFalse(plan.Selections.SkillKarmaPurchases?.Any(row => row.Kind == CharacterCreationSkillKinds.Active
            && group.MemberSkillSourceIds.Contains(row.SourceSkillId)) == true);
        AssertCanonicalEqual(inputs.Skills.GroupAllocations, plan.Selections.GroupAllocations
            .Where(row => row.GroupId == group.GroupId).ToArray());
        var source = catalog.ActiveSkills.Single(row => row.SourceSkillId == "a9fa961d-07e5-46da-8edc-403ae3e6cc75");
        var quote = CharacterCreationSkillsService.PreviewCompletionPurchases(inputs.Skills,
            plan.Selections.AttributeAllocations, plan.Selections.SkillAllocations, plan.Selections.GroupAllocations,
            [new(source.SourceSkillId, source.Kind, source.SourceNodeDigest, 2)]);
        CollectionAssert.Contains(quote.Blockers.ToArray(), CharacterCreationSkillKarmaPurchaseBlockers.GroupCompensationUnsupported);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Mixed_selection_preserves_a_baseline_purchase_without_inventing_specialization_grounding(bool atCap)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        LockPointFundedAttributes(fixture);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = OnlyKarmaSkillIntent(fixture, inputs, CharacterCreationSkillKinds.Knowledge, KarmaSprawlLife, 25);
        var template = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { Reserves = intent.Reserves with { Karma = 25 } }));
        var source = inputs.Skills.Authority.KnowledgeSkills.Single(row => row.SourceSkillId == KarmaSprawlLife);
        var purchase = new CharacterCreationSkillKarmaPurchase(source.SourceSkillId, source.Kind, source.SourceNodeDigest,
            atCap ? inputs.Skills.KarmaPurchasePolicy!.MaxKnowledgeSkillRatingCreate : 1,
            atCap ? null : source.Specializations.Single(row => row.Name == "Street Rumors").OptionId);
        var request = template.Request with { KarmaToReserve = 0,
            Magic = template.Request.Magic with { KarmaToReserve = 0 }, SkillKarmaPurchases = [purchase] };
        var baseline = CharacterCreationCompletionPlanner.Plan(inputs, request);
        Assert.IsNotNull(baseline.Value, string.Join(",", baseline.Blockers));
        Assert.AreEqual(0, baseline.Value.Review.Blockers.Count, string.Join(",", baseline.Blockers));
        string original = JsonSerializer.Serialize(baseline);
        var files = fixture.CaptureFiles();
        var completed = CompleteRetainedKarmaPlan(inputs, intent, baseline);
        var plan = completed.Evaluation.Value!;
        Assert.IsTrue(plan.Selections.QualityOptionIds.Except(baseline.Value.Selections.QualityOptionIds).Any(),
            "Retained purchases must survive an actual mixed selection, not just the original quote.");
        var retained = plan.Selections.SkillKarmaPurchases!.Single(row => row.Kind == source.Kind && row.SourceSkillId == source.SourceSkillId);
        Assert.AreEqual(purchase.SourceNodeDigest, retained.SourceNodeDigest);
        Assert.AreEqual(purchase.SpecializationOptionId, retained.SpecializationOptionId);
        Assert.IsTrue(retained.Rating >= purchase.Rating);
        Assert.IsFalse(completed.Coverage.Any(row => row.Category == "karma-specialization-knowledge-" + source.SourceSkillId),
            "A player's retained lore specialization has no equipment or authored role grounding.");
        AssertCanonicalEqual(completed.Request.SkillKarmaPurchases, plan.Selections.SkillKarmaPurchases);
        AssertCanonicalEqual(plan.Review.Skills!.Skills, plan.Skills!.Preview.Skills);
        Assert.AreEqual(retained.Rating, plan.Skills.Preview.Skills.Single(row => row.Kind == source.Kind && row.SourceSkillId == source.SourceSkillId).Rating);
        CollectionAssert.AreEqual(baseline.Blockers.ToArray(), completed.Evaluation.Blockers.ToArray());
        Assert.AreEqual(baseline.Outcome, completed.Evaluation.Outcome);
        if (atCap)
        {
            Assert.AreEqual(purchase, retained);
            var rejected = CharacterCreationSkillsService.PreviewCompletionPurchases(inputs.Skills,
                plan.Selections.AttributeAllocations, plan.Selections.SkillAllocations, plan.Selections.GroupAllocations,
                [purchase with { Rating = purchase.Rating + 1 }]);
            Assert.AreNotEqual(CharacterCreationFoundationOutcomes.Success, rejected.Outcome);
            Assert.IsTrue(rejected.Blockers.Count > 0);
        }
        var replay = CharacterCreationCompletionPlanner.Plan(inputs, completed.Request);
        Assert.AreEqual(plan.PlanDigest, replay.Value!.PlanDigest);
        Assert.AreEqual(original, JsonSerializer.Serialize(baseline));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Future_skill_karma_preserves_manual_ratings_zeros_groups_native_language_specializations_and_exclusions()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("BOD", 0, 0)], ["BOD"]);
        var catalog = fixture.ReadInputs().Skills.Authority;
        var pistols = catalog.ActiveSkills.Single(row => row.SourceSkillId == "adf31a50-b228-4e09-a09c-46ab9f5e59a1");
        var knowledge = catalog.KnowledgeSkills.Single(row => row.SourceSkillId == KarmaSprawlLife);
        var native = catalog.KnowledgeSkills.Single(row => row.SourceSkillId == "907a3a8f-2c40-4543-a5a9-891bcde41f6b");
        var group = catalog.SkillGroups.Single(row => row.Name == "Athletics");
        fixture.SaveSkills([
            new(KarmaPerception, CharacterCreationSkillKinds.Active, 0, null, false),
            new(pistols.SourceSkillId, pistols.Kind, 2, pistols.Specializations.Single(row => row.Name == "Tasers").OptionId, false),
            new(knowledge.SourceSkillId, knowledge.Kind, 1, knowledge.Specializations.Single(row => row.Name == "Street Rumors").OptionId, false),
            new(native.SourceSkillId, native.Kind, null, null, true)
        ], [new(group.GroupId, 1)]);
        var inputs = ReadSkillKarmaInputs(fixture);
        var intent = Intent(fixture, inputs) with
        {
            Reserves = new(Karma: 3, Nuyen: 500m, SkillGroupPoints: 1),
            ExcludedChoices = [new(CompletionChoiceDomain.ActiveSkill, "47cb1e8b-c285-4c54-9aaa-75305ad6dd4f")],
            Provenance = [new(new(CompletionChoiceDomain.ActiveSkill, "ae91a8a6-80e7-4f52-b9eb-21725a5528a4"), CompletionChoiceOrigin.Unknown)]
        };
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        foreach (var manual in inputs.Skills.Allocations)
        {
            Assert.AreEqual(manual, plan.Selections.SkillAllocations.Single(row => row.Kind == manual.Kind && row.SourceSkillId == manual.SourceSkillId));
            Assert.IsFalse(plan.Selections.SkillKarmaPurchases?.Any(row => row.Kind == manual.Kind && row.SourceSkillId == manual.SourceSkillId) == true);
        }
        foreach (var manual in inputs.Skills.GroupAllocations)
            Assert.AreEqual(manual, plan.Selections.GroupAllocations.Single(row => row.GroupId == manual.GroupId));
        Assert.AreEqual(inputs.Skills.Attributes.PendingDraft!.Allocations.Single(row => row.AttributeId == "BOD"),
            plan.Selections.AttributeAllocations.Single(row => row.AttributeId == "BOD"));
        foreach (string locked in group.MemberSkillSourceIds.Concat(intent.ExcludedChoices.Select(row => row.SourceId))
                     .Concat(intent.Provenance.Select(row => row.Choice.SourceId)))
            Assert.IsFalse(plan.Selections.SkillKarmaPurchases?.Any(row => row.SourceSkillId == locked) == true);
        Assert.IsTrue(plan.Review.Balances.RemainingCreationKarma >= 3);
        Assert.IsTrue(plan.Review.Balances.RemainingNuyen >= 500m);
        Assert.AreEqual(3, proposal.Request.KarmaToReserve);
        Assert.AreEqual(500m, proposal.Request.NuyenToReserve);
        AssertCanonicalEqual(inputs.Magic.Selections, proposal.Request.Magic.ManualSelections);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Future_skill_karma_honors_full_wallet_reserve_and_cancels_before_karma_work_without_writes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = ReadSkillKarmaInputs(fixture);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        int reserve = inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal;
        var intent = Intent(fixture, inputs) with { Reserves = new(Karma: reserve, Nuyen: 777m) };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsFalse(plan.Selections.SkillKarmaPurchases?.Count > 0);
        Assert.AreEqual(reserve, plan.Review.Balances.RemainingCreationKarma);
        Assert.AreEqual(reserve, plan.Selections.KarmaToReserve);
        Assert.IsTrue(plan.Review.Balances.RemainingNuyen >= 777m);
        using var cancellation = new CancellationTokenSource();
        bool reachedKarma = false;
        Assert.ThrowsExactly<OperationCanceledException>(() => CompletionRecommendationBuilder.BuildMissingOnly(
            inputs, intent, cancellation.Token, progress =>
            {
                if (progress.Stage != CompletionAnalysisStage.Karma) return;
                reachedKarma = true;
                cancellation.Cancel();
            }));
        Assert.IsTrue(reachedKarma);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow("Sorcery", null)]
    [DataRow(null, "Magician")]
    public void Skill_karma_preserves_Priority_grants_including_zero_rated_group_choices(string? groupName, string? talent)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, zeroRatedTalentGroup: groupName, magicTalent: talent);
        var inputs = ReadSkillKarmaInputs(fixture);
        var grants = inputs.Skills.PrerequisiteDraft.TalentSelection!.GrantPlan!;
        Assert.IsTrue(grants.ActiveSkills.Count > 0 || grants.SkillGroups.Count > 0);
        if (groupName is not null)
        {
            Assert.AreEqual(0, grants.SkillGroups.Single().BaseRating);
            Assert.AreEqual(0, inputs.Skills.GroupAllocations.Count);
        }
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        Assert.IsNotNull(proposal.Evaluation.Value, string.Join(",", proposal.Evaluation.Blockers));
        var selections = proposal.Evaluation.Value.Selections;
        foreach (var allocation in inputs.Skills.Allocations)
            Assert.AreEqual(allocation, selections.SkillAllocations.Single(row => row.Kind == allocation.Kind
                && row.SourceSkillId == allocation.SourceSkillId));
        var protectedIds = grants.ActiveSkills.Select(row => row.SourceId)
            .Concat(grants.SkillGroups.SelectMany(row => row.MemberSkillSourceIds)).ToHashSet(StringComparer.Ordinal);
        Assert.IsFalse(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.Kind == CharacterCreationSkillKinds.Active
            && protectedIds.Contains(row.SourceId)));
        Assert.IsFalse(selections.SkillKarmaPurchases?.Any(row => row.Kind == CharacterCreationSkillKinds.Active
            && protectedIds.Contains(row.SourceSkillId)) == true);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Missing_skill_policy_retains_existing_karma_advice_and_discloses_the_capability_gap()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        Assert.IsNull(inputs.Skills.KarmaPurchasePolicy);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsNull(proposal.Request.SkillKarmaPurchases);
        Assert.IsNull(plan.Selections.SkillKarmaPurchases);
        Assert.IsTrue(plan.Selections.AttributeAllocations.Any(row => row.KarmaLevels > 0)
            || plan.Selections.QualityOptionIds.Except(inputs.Lifestyles.Qualities.SelectedOptionIds).Any());
        var coverage = proposal.Coverage.Single(row => row.Category == "karma-skills");
        Assert.AreEqual(CompletionCoverageState.Unsupported, coverage.State);
        CollectionAssert.Contains(coverage.CoreBlockers.ToArray(), CharacterCreationSkillKarmaPurchaseBlockers.PolicyUnavailable);
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.karma.skill-policy-unavailable"));
        fixture.AssertFilesUnchanged(files);
    }

    private static CharacterCreationCompletionReviewInputs ReadSkillKarmaInputs(CompletionRecommendationTestFixture fixture)
    {
        var loaded = new OwnerBoundCharacterCreationCompletionReader(fixture.Store, fixture.Owners, fixture.Resolver, fixture.Queries)
            .LoadCompletionInputs(fixture.Owners.Capture(), new(fixture.WorkspaceId) { IncludeKarmaPurchasePolicy = true });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, loaded.Outcome, string.Join(",", loaded.Blockers));
        Assert.IsNotNull(loaded.Value);
        Assert.IsNotNull(loaded.Value.Skills.KarmaPurchasePolicy);
        return loaded.Value;
    }

    private static void LockPointFundedAttributes(CompletionRecommendationTestFixture fixture)
    {
        var inputs = fixture.ReadInputs();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs) with
            { Reserves = new(Karma: inputs.Lifestyles.Qualities.Binding.CreationKarmaTotal) }));
        var allocations = ReviewedKarmaPlan(proposal).Selections.AttributeAllocations.ToArray();
        Assert.IsTrue(allocations.All(row => row.KarmaLevels == 0));
        fixture.SaveAttributes(allocations, allocations.Select(row => row.AttributeId).ToArray());
    }

    private static CompletionAssistantIntent OnlyKarmaSkillIntent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs, string kind, string sourceId, int wallet,
        string archetype = CompletionAssistantRules.GeneralSurvivor)
    {
        // Ask Core for the actual proposed knowledge pool; no attribute-to-point
        // formula or invented policy is embedded in the fixture.
        var baseline = ReviewedKarmaPlan(Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { ArchetypeId = archetype, Reserves = new(Karma: wallet) })));
        var budget = baseline.Review.Skills!.KnowledgeSkillPointBudget;
        const string native = "907a3a8f-2c40-4543-a5a9-891bcde41f6b";
        var excluded = baseline.Request.Skills.PermittedTargetOrder
            .Where(row => !(row.Kind == kind && row.SourceId == sourceId) && row.SourceId != native)
            .Select(row => new CompletionChoiceIdentity(row.Kind == CharacterCreationSkillKinds.Active
                ? CompletionChoiceDomain.ActiveSkill : row.Kind == CharacterCreationSkillKinds.Knowledge
                    ? CompletionChoiceDomain.KnowledgeSkill : CompletionChoiceDomain.SkillGroup, row.SourceId))
            .Concat(inputs.Skills.Authority.SkillGroups.Select(row => new CompletionChoiceIdentity(CompletionChoiceDomain.SkillGroup, row.GroupId)))
            // Excluding a group also locks its members. Reserve the group pool,
            // but keep the target member of an unfunded group eligible for Karma.
            .Where(choice => choice.Domain != CompletionChoiceDomain.SkillGroup || kind != CharacterCreationSkillKinds.Active
                || !inputs.Skills.Authority.SkillGroups.Single(row => row.GroupId == choice.SourceId)
                    .MemberSkillSourceIds.Contains(sourceId))
            .Distinct().ToArray();
        return Intent(fixture, inputs) with
        {
            Reserves = new(ActiveSkillPoints: inputs.Skills.SelectedActiveSkillPoints,
                SkillGroupPoints: inputs.Skills.SelectedSkillGroupPoints, KnowledgeSkillPoints: checked((int)(budget.Used + budget.Remaining))),
            ArchetypeId = archetype, ExcludedChoices = excluded, NativeLanguageSourceId = native
        };
    }

    private static void AssertFutureSkillPlan(CharacterCreationCompletionReviewInputs inputs, CompletionAssistantProposal proposal)
    {
        var plan = ReviewedKarmaPlan(proposal);
        Assert.IsNotNull(proposal.Request.SkillKarmaPurchases);
        Assert.IsTrue(proposal.Request.SkillKarmaPurchases.Count > 0);
        foreach (var purchase in proposal.Request.SkillKarmaPurchases)
        {
            Assert.IsTrue(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.Kind == purchase.Kind && row.SourceId == purchase.SourceSkillId));
            Assert.IsFalse(inputs.Skills.Allocations.Any(row => row.Kind == purchase.Kind && row.SourceSkillId == purchase.SourceSkillId));
        }
        AssertCanonicalEqual(proposal.Request.SkillKarmaPurchases, plan.Selections.SkillKarmaPurchases);
        AssertCanonicalEqual(plan.Review.Skills!.Skills, plan.Skills!.Preview.Skills);
        foreach (var purchase in plan.Selections.SkillKarmaPurchases!)
        {
            var projected = plan.Skills.Preview.Skills.Single(row => row.Kind == purchase.Kind && row.SourceSkillId == purchase.SourceSkillId);
            Assert.AreEqual(purchase.Rating, projected.Rating);
            foreach (string anchor in inputs.Skills.KarmaPurchasePolicy!.SourceAnchorIds)
                CollectionAssert.Contains(projected.SourceAnchorIds.ToArray(), anchor);
            var summary = proposal.Suggestions.Single(row => row.Choice.SourceId == purchase.SourceSkillId
                && row.Choice.Domain == (purchase.Kind == CharacterCreationSkillKinds.Active
                    ? CompletionChoiceDomain.ActiveSkill : CompletionChoiceDomain.KnowledgeSkill));
            Assert.AreEqual(projected.EffectiveRating, summary.EvaluatedRating);
            Assert.AreEqual(CompletionSuggestionState.TargetMet, summary.State);
        }
        var quote = CharacterCreationSkillsService.PreviewCompletionPurchases(inputs.Skills, plan.Selections.AttributeAllocations,
            plan.Selections.SkillAllocations, plan.Selections.GroupAllocations, proposal.Request.SkillKarmaPurchases);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, quote.Outcome, string.Join(",", quote.Blockers));
        Assert.AreEqual(plan.Review.Balances.SkillsKarmaCost, quote.Value!.Skills.Sum(row => row.KarmaCost));
        var review = CharacterCreationCompletionReviewService.Review(inputs, new(inputs.SnapshotDigest, plan.Selections));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, review.Outcome, string.Join(",", review.Blockers));
        Assert.AreEqual(plan.Review.ReviewDigest, review.Value!.ReviewDigest);
        var replay = CharacterCreationCompletionPlanner.Plan(inputs, proposal.Request);
        Assert.AreEqual(proposal.Evaluation.Outcome, replay.Outcome);
        CollectionAssert.AreEqual(proposal.Evaluation.Blockers.ToArray(), replay.Blockers.ToArray());
        Assert.AreEqual(plan.PlanDigest, replay.Value!.PlanDigest);
        Assert.AreEqual(plan.PlanDigest, CharacterCreationCompletionPlanRules.ComputePlanDigest(plan));
        Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
    }

    private static (CharacterCreationCompletionPlanRequest Request,
        CharacterCreationFoundationResult<CharacterCreationCompletionPlan> Evaluation,
        IReadOnlyList<CompletionCategoryCoverage> Coverage) CompleteRetainedKarmaPlan(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantIntent intent,
        CharacterCreationFoundationResult<CharacterCreationCompletionPlan> baseline)
    {
        // Exercise the existing mixed-selection continuation with a real Core
        // baseline. The public missing-only entry point starts without purchases.
        var method = typeof(CompletionRecommendationBuilder).GetMethod("CompleteKarmaAdvice",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);
        var resource = Activator.CreateInstance(method.GetParameters()[2].ParameterType,
            new object[] { baseline.Value!.Request, baseline });
        var result = method.Invoke(null, [inputs, intent, resource, CancellationToken.None])!;
        var completed = result.GetType().GetProperty("Completed")!.GetValue(result)!;
        return ((CharacterCreationCompletionPlanRequest)completed.GetType().GetProperty("Request")!.GetValue(completed)!,
            (CharacterCreationFoundationResult<CharacterCreationCompletionPlan>)completed.GetType().GetProperty("Evaluation")!.GetValue(completed)!,
            (IReadOnlyList<CompletionCategoryCoverage>)result.GetType().GetProperty("Coverage")!.GetValue(result)!);
    }

    private static void AssertCanonicalEqual<T>(T expected, T actual)
        => Assert.AreEqual(CharacterCreationFinalizationDigest.Compute(expected), CharacterCreationFinalizationDigest.Compute(actual));
}
