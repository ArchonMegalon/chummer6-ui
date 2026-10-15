using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CompletionRecommendationBuilderTests
{
    private const string Perception = "04e1eb3e-e82d-485b-a7fd-1e677df2a070";
    private static readonly CompletionChoiceIdentity BodyChoice = new(CompletionChoiceDomain.Attribute, "BOD");
    private static readonly CompletionChoiceIdentity PerceptionChoice = new(CompletionChoiceDomain.ActiveSkill, Perception);
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Starter_uses_real_Core_review_deterministically_without_mutating_inputs_or_workspace()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var pack = CompletionRecommendationBuilder.GeneralSurvivorPack;
        var intent = Intent(fixture, inputs, Permissions(pack));
        string inputBytes = JsonSerializer.Serialize(inputs);
        string intentBytes = JsonSerializer.Serialize(intent);
        var files = fixture.CaptureFiles();

        var first = Proposal(CompletionRecommendationBuilder.Build(inputs, intent));
        var again = Proposal(CompletionRecommendationBuilder.Build(inputs, intent));
        var plan = Reviewed(first);
        Assert.AreEqual(first.ProposalDigest, again.ProposalDigest);
        Assert.AreEqual(first.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(first));
        Assert.AreEqual("deterministic-starter", first.RecommendationMode);
        Assert.IsTrue(first.RequiresFinalizationReview);
        Assert.IsNull(typeof(CompletionAssistantProposal).GetProperty("CanApply"));
        Assert.IsNull(typeof(CompletionAssistantProposal).GetProperty("CanFinalize"));
        Assert.AreEqual(inputs.SnapshotDigest, plan.Review.InputsSnapshotDigest);
        Assert.AreEqual(3, plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
        Assert.AreEqual(3, plan.Skills!.Preview.Skills.Single(row => row.SourceSkillId == Perception).EffectiveRating);
        Assert.IsTrue(first.Suggestions.All(row => row.State == CompletionSuggestionState.TargetMet));
        Assert.IsTrue(plan.Attributes.Preview.NormalPointBudget.Remaining > 0,
            "A starter role kit must not silently spend every remaining point to maximum ratings.");
        Assert.AreEqual(inputBytes, JsonSerializer.Serialize(inputs));
        Assert.AreEqual(intentBytes, JsonSerializer.Serialize(intent));
        fixture.AssertFilesUnchanged(files);

        var writable = pack.Attributes as IList<CompletionAttributeRecommendation>;
        Assert.IsNotNull(writable, "The detached test pack must be independently mutable for this isolation check.");
        writable[0] = new(new("BOD", 6), "test.changed-pack");
        Assert.AreEqual(3, CompletionRecommendationBuilder.GeneralSurvivorPack.Attributes[0].Target.TargetUnaugmentedRating);
        Assert.AreEqual(first.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(first));
    }

    [TestMethod]
    public void Detaching_preserves_exact_trusted_local_stamp_without_promoting_a_value_only_local_name()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var pack = SmallPack();
        var baseline = Intent(fixture, inputs, Permissions(pack));
        foreach (var owner in new[] { OwnerScope.LocalSingleUser, new OwnerScope(OwnerScope.LocalSingleUser.Value) })
        {
            var intent = baseline with { OwnerContext = baseline.OwnerContext with { Owner = owner } };
            var proposal = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
            Assert.AreEqual(intent.OwnerContext, proposal.Intent.OwnerContext);
            Assert.AreEqual(owner.IsLocalSingleUser, proposal.Intent.OwnerContext.Owner.IsLocalSingleUser);
            Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
            Assert.AreNotSame(intent, proposal.Intent);
            Assert.AreNotSame(intent.Permissions.AttributeIds, proposal.Intent.Permissions.AttributeIds);
        }
    }

    [TestMethod]
    public void Missing_only_uses_admitted_fresh_choices_without_changing_the_additive_contract()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        Assert.IsNull(inputs.Skills.Attributes.PendingDraft);
        Assert.IsTrue(inputs.Skills.Attributes.CanEdit);
        Assert.IsNotNull(inputs.Contacts.BootstrapBinding);
        var pack = SmallPack();
        var intent = Intent(fixture, inputs, new([], []));
        var files = fixture.CaptureFiles();
        string originalInputs = JsonSerializer.Serialize(inputs);
        string originalIntent = JsonSerializer.Serialize(intent);

        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack));
        var plan = Reviewed(proposal);
        Assert.AreEqual(CompletionAllocationPolicy.MissingOnly, proposal.AllocationPolicy);
        Assert.AreEqual(3, plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
        Assert.AreEqual(2, plan.Skills!.Preview.Skills.Single(row => row.SourceSkillId == Perception).EffectiveRating);
        Assert.AreEqual(originalIntent, JsonSerializer.Serialize(proposal.Intent),
            "The original request remains bound; inferred safe permissions belong to the evaluated Core request.");
        Assert.AreEqual(intent.OwnerContext, proposal.Intent.OwnerContext);
        Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack)).ProposalDigest);
        Assert.IsFalse(proposal.RemainingTasks.Contains(CompletionAssistantRules.AttributeChoiceIntentRequired));

        var additive = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
        Assert.AreEqual(CompletionAllocationPolicy.ExplicitAdditive, additive.AllocationPolicy);
        Assert.AreEqual(0, additive.Request.Attributes.PermittedAttributeOrder.Count);
        Assert.AreEqual(0, additive.Request.Skills.PermittedTargetOrder.Count);
        Assert.AreNotEqual(additive.ProposalDigest, proposal.ProposalDigest);
        Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
        Assert.AreEqual(originalIntent, JsonSerializer.Serialize(intent));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Missing_only_keeps_legacy_saved_zero_attributes_manual_skills_groups_and_native_choices_locked()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("BOD", 0, 0)]);
        var initial = fixture.ReadInputs();
        var influence = initial.Skills.Authority.SkillGroups.Single(row => row.Name == "Influence");
        var athletics = initial.Skills.Authority.SkillGroups.Single(row => row.Name == "Athletics");
        var languages = initial.Skills.Authority.KnowledgeSkills.Where(row => row.CanBeNativeLanguage).Take(2).ToArray();
        fixture.SaveSkills([
            new(Perception, CharacterCreationSkillKinds.Active, 1, null, false),
            new(languages[0].SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true),
            new(languages[1].SourceSkillId, CharacterCreationSkillKinds.Knowledge, 1, null, false)
        ], [new(influence.GroupId, 1)]);
        var inputs = fixture.ReadInputs();
        Assert.IsNotNull(inputs.Skills.Attributes.PendingDraft);
        Assert.IsNull(inputs.Skills.Attributes.PendingDraft.PlayerChosenAttributeIds,
            "The legacy save has no per-attribute choice authority; every saved row must stay locked.");
        // Both explicit BOD zero and synthesized omitted REA zero are persisted.
        Assert.IsTrue(inputs.Skills.Attributes.PendingDraft.Allocations.Any(row => row.AttributeId == "BOD"));
        Assert.IsTrue(inputs.Skills.Attributes.PendingDraft.Allocations.Any(row => row.AttributeId == "REA"));
        var pack = SmallPack() with
        {
            Attributes = [new(new("BOD", 3), "test.explicit-zero"), new(new("REA", 3), "test.omitted-zero")],
            Skills = [
                new(new(CharacterCreationSkillKinds.Active, Perception, 3), "test.retained"),
                new(new(CharacterCreationSkillKinds.Active, influence.MemberSkillSourceIds[0], 3), "test.group-backed"),
                new(new(CharacterCreationSkillCompletionKinds.Group, influence.GroupId, 3), "test.retained-group"),
                new(new(CharacterCreationSkillCompletionKinds.Group, athletics.GroupId, 1), "test.missing-group"),
                new(new(CharacterCreationSkillKinds.Knowledge, languages[1].SourceSkillId, 3), "test.retained-language")
            ]
        };
        var intent = Intent(fixture, inputs, Permissions(pack)) with
        {
            // Even caller-labelled defaults/additive permission cannot unlock retained choices.
            Provenance = [new(BodyChoice, CompletionChoiceOrigin.UntouchedDefault),
                new(PerceptionChoice, CompletionChoiceOrigin.UntouchedDefault)],
            NativeLanguageSourceId = languages[1].SourceSkillId
        };
        var files = fixture.CaptureFiles();
        string originalInputs = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack));
        var plan = Reviewed(proposal);
        Assert.AreEqual(0, proposal.Request.Attributes.PermittedAttributeOrder.Count);
        Assert.IsNull(proposal.Request.Skills.NativeLanguageSourceId,
            "A chosen numeric language must not be repurposed through the native-language field.");
        Assert.IsFalse(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == Perception
            || row.SourceId == influence.GroupId || influence.MemberSkillSourceIds.Contains(row.SourceId)
            || row.SourceId == languages[1].SourceSkillId));
        Assert.IsTrue(proposal.Request.Skills.PermittedTargetOrder.Any(row =>
            row.Kind == CharacterCreationSkillCompletionKinds.Group && row.SourceId == athletics.GroupId));
        Assert.IsTrue(athletics.MemberSkillSourceIds.All(id => proposal.Request.Skills.PermittedTargetOrder.Any(row =>
            row.Kind == CharacterCreationSkillKinds.Active && row.SourceId == id)));
        CollectionAssert.AreEqual(inputs.Skills.Attributes.PendingDraft.Allocations.ToArray(), plan.Selections.AttributeAllocations.ToArray());
        CollectionAssert.AreEqual(inputs.Skills.Allocations.ToArray(), plan.Selections.SkillAllocations.ToArray());
        Assert.AreEqual(inputs.Skills.GroupAllocations.Single(), plan.Selections.GroupAllocations.Single(row => row.GroupId == influence.GroupId));
        Assert.AreEqual(1, plan.Selections.GroupAllocations.Single(row => row.GroupId == athletics.GroupId).Rating);
        Assert.AreEqual(CompletionSuggestionState.Locked, proposal.Suggestions.Single(row => row.Choice == PerceptionChoice).State);
        Assert.AreEqual(CompletionCoverageState.Unsupported, Coverage(proposal, "attribute-choice-intent").State);
        CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), CompletionAssistantRules.AttributeChoiceIntentRequired);
        Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);

        var excluded = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { ExcludedChoices = [new(CompletionChoiceDomain.ActiveSkill, athletics.MemberSkillSourceIds[0])] }, pack));
        Assert.IsFalse(excluded.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == athletics.GroupId));
        CollectionAssert.AreEqual(inputs.Skills.GroupAllocations.ToArray(), Reviewed(excluded).Selections.GroupAllocations.ToArray());
    }

    [TestMethod]
    public void Missing_only_uses_recorded_attribute_intent_without_guessing_from_saved_zeros()
    {
        foreach (bool choseBodyZero in new[] { false, true })
        {
            using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
            string[] chosenIds = choseBodyZero ? ["BOD"] : [];
            fixture.SaveAttributes(choseBodyZero ? [new("BOD", 0, 0)] : [], chosenIds);
            var inputs = fixture.ReadInputs();
            var draft = inputs.Skills.Attributes.PendingDraft;
            Assert.IsNotNull(draft);
            Assert.IsNotNull(draft.PlayerChosenAttributeIds);
            CollectionAssert.AreEqual(chosenIds, draft.PlayerChosenAttributeIds.ToArray());
            Assert.IsTrue(draft.Allocations.All(row => row.PriorityPoints == 0 && row.KarmaLevels == 0),
                "Both observed drafts contain the same dense zero allocations; only recorded choice intent differs.");
            var pack = SmallPack() with
            {
                Attributes = [new(new("BOD", 3), "test.recorded-choice"), new(new("REA", 3), "test.untouched"),
                    new(new("INT", 3), "test.unknown-provenance"), new(new("AGI", 3), "test.excluded")],
                Skills = []
            };
            var intent = Intent(fixture, inputs, Permissions(pack)) with
            {
                Provenance = [new(BodyChoice, CompletionChoiceOrigin.UntouchedDefault),
                    new(new(CompletionChoiceDomain.Attribute, "INT"), CompletionChoiceOrigin.Unknown)],
                ExcludedChoices = [new(CompletionChoiceDomain.Attribute, "AGI")]
            };
            var files = fixture.CaptureFiles();
            string originalInputs = JsonSerializer.Serialize(inputs);
            string originalIntent = JsonSerializer.Serialize(intent);
            var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack));
            var plan = Reviewed(proposal);

            CollectionAssert.AreEqual(choseBodyZero ? new[] { "REA" } : new[] { "BOD", "REA" },
                proposal.Request.Attributes.PermittedAttributeOrder.ToArray());
            Assert.AreEqual(choseBodyZero ? 1 : 3,
                plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
            Assert.AreEqual(3, plan.Attributes.Preview.Attributes.Single(row => row.AttributeId == "REA").UnaugmentedCurrent);
            Assert.AreEqual(1, plan.Attributes.Preview.Attributes.Single(row => row.AttributeId == "INT").UnaugmentedCurrent);
            Assert.AreEqual(1, plan.Attributes.Preview.Attributes.Single(row => row.AttributeId == "AGI").UnaugmentedCurrent);
            if (choseBodyZero)
            {
                Assert.AreEqual(draft.Allocations.Single(row => row.AttributeId == "BOD"),
                    plan.Selections.AttributeAllocations.Single(row => row.AttributeId == "BOD"));
                Assert.AreEqual(CompletionSuggestionState.Locked,
                    proposal.Suggestions.Single(row => row.Choice == BodyChoice).State);
            }
            Assert.IsFalse(proposal.RemainingTasks.Contains(CompletionAssistantRules.AttributeChoiceIntentRequired));
            Assert.IsFalse(proposal.Coverage.Any(row => row.Category == "attribute-choice-intent"));
            Assert.AreEqual(CompletionAllocationPolicy.MissingOnly, proposal.AllocationPolicy);
            Assert.AreEqual(proposal.ProposalDigest, CompletionAssistantRules.ComputeProposalDigest(proposal));
            Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
            Assert.AreEqual(originalIntent, JsonSerializer.Serialize(proposal.Intent));
            var changedIntentAuthority = inputs with
            {
                Skills = inputs.Skills with { Attributes = inputs.Skills.Attributes with
                {
                    PendingDraft = draft with { PlayerChosenAttributeIds = choseBodyZero ? [] : ["BOD"] }
                } }
            };
            var stale = CompletionRecommendationBuilder.BuildMissingOnly(changedIntentAuthority, intent, pack);
            Assert.IsNull(stale.Value);
            CollectionAssert.Contains(stale.Blockers.ToArray(), CompletionAssistantRules.SnapshotConflict);
            fixture.AssertFilesUnchanged(files);
        }
    }

    [TestMethod]
    public void Missing_only_treats_explicit_unknown_imported_and_player_provenance_as_locks_not_absence()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var pack = SmallPack();
        foreach (var origin in new[] { CompletionChoiceOrigin.Unknown, CompletionChoiceOrigin.PlayerSet,
                     CompletionChoiceOrigin.Imported, CompletionChoiceOrigin.AcceptedAssistant })
        {
            var intent = Intent(fixture, inputs, Permissions(pack)) with
            {
                Provenance = [new(BodyChoice, origin), new(PerceptionChoice, origin)]
            };
            var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack));
            Assert.AreEqual(0, proposal.Request.Attributes.PermittedAttributeOrder.Count);
            Assert.AreEqual(0, proposal.Request.Skills.PermittedTargetOrder.Count);
            Assert.AreEqual(1, Reviewed(proposal).Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
            Assert.IsTrue(proposal.Suggestions.All(row => row.State == CompletionSuggestionState.Locked));
        }
    }

    [TestMethod]
    public void Missing_only_preserves_a_real_zero_rated_Priority_group_choice_without_an_allocation_row()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, zeroRatedTalentGroup: "Sorcery");
        var inputs = fixture.ReadInputs();
        var grant = inputs.Skills.PrerequisiteDraft.TalentSelection!.GrantPlan!.SkillGroups.Single();
        Assert.AreEqual(0, grant.BaseRating);
        Assert.AreEqual(0, inputs.Skills.GroupAllocations.Count);
        // Priority grants and the editable Skills catalogue have separate digest
        // domains. Match the admitted group by its exact source members and name.
        var group = inputs.Skills.Authority.SkillGroups.Single(row => row.Name == grant.CanonicalName
            && row.MemberSkillSourceIds.Order(StringComparer.Ordinal)
                .SequenceEqual(grant.MemberSkillSourceIds.Order(StringComparer.Ordinal)));
        var pack = SmallPack() with
        {
            Attributes = [],
            Skills = [
                new(new(CharacterCreationSkillCompletionKinds.Group, group.GroupId, 1), "test.selected-zero-group"),
                new(new(CharacterCreationSkillKinds.Active, grant.MemberSkillSourceIds[0], 1), "test.selected-zero-member"),
                new(new(CharacterCreationSkillKinds.Active, Perception, 2), "test.unselected-skill")
            ]
        };
        var intent = Intent(fixture, inputs, Permissions(pack)) with
        {
            Provenance = [new(new(CompletionChoiceDomain.ActiveSkill, grant.MemberSkillSourceIds[0]),
                CompletionChoiceOrigin.UntouchedDefault)]
        };
        var files = fixture.CaptureFiles();
        string originalInputs = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent, pack));
        CollectionAssert.AreEqual(new[] { new CharacterCreationSkillCompletionTarget(CharacterCreationSkillKinds.Active, Perception) },
            proposal.Request.Skills.PermittedTargetOrder.ToArray());
        Assert.AreEqual(1, proposal.Request.Skills.Targets!.Count);
        Assert.AreEqual(Perception, proposal.Request.Skills.Targets[0].SourceId);
        Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Zero_default_imported_and_player_provenance_never_unlock_choices_and_exclusions_override_permission()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([]); // A genuine saved zero allocation, not a fabricated draft.
        var inputs = fixture.ReadInputs();
        var pack = SmallPack();
        foreach (var origin in new[] { CompletionChoiceOrigin.Unknown, CompletionChoiceOrigin.UntouchedDefault,
                     CompletionChoiceOrigin.PlayerSet, CompletionChoiceOrigin.Imported })
        {
            var intent = Intent(fixture, inputs, new([], [])) with
            {
                Provenance = [new(BodyChoice, origin), new(PerceptionChoice, origin)]
            };
            var locked = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
            var plan = Reviewed(locked);
            Assert.AreEqual(CompletionSuggestionState.Locked,
                locked.Suggestions.Single(row => row.Choice == BodyChoice).State);
            Assert.IsTrue(locked.Suggestions.Single(row => row.Choice == PerceptionChoice).State
                is CompletionSuggestionState.Locked or CompletionSuggestionState.NotEvaluated);
            Assert.IsTrue(locked.Suggestions.All(row => row.Origin == origin));
            Assert.AreEqual(1, plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
            Assert.IsFalse(plan.Selections.SkillAllocations.Any(row => row.SourceSkillId == Perception));
            Assert.AreEqual(0, locked.Request.Attributes.Targets!.Count);
            Assert.AreEqual(0, locked.Request.Skills.Targets!.Count);
        }

        var excludedIntent = Intent(fixture, inputs, Permissions(pack)) with
        {
            ExcludedChoices = [BodyChoice, PerceptionChoice]
        };
        var excluded = Proposal(CompletionRecommendationBuilder.Build(inputs, excludedIntent, pack));
        Assert.IsTrue(excluded.Suggestions.All(row => row.State == CompletionSuggestionState.Excluded));
        Assert.AreEqual(0, excluded.Request.Attributes.PermittedAttributeOrder.Count);
        Assert.AreEqual(0, excluded.Request.Skills.PermittedTargetOrder.Count);
        Assert.IsFalse(Reviewed(excluded).Selections.SkillAllocations.Any(row => row.SourceSkillId == Perception));

        // Excluding one canonical member must also prevent raising it indirectly
        // through a permitted skill group. Membership comes from real Core data.
        var group = inputs.Skills.Authority.SkillGroups.Single(row => row.Name == "Athletics");
        var groupPack = pack with
        {
            Attributes = [],
            Skills = [new(new(CharacterCreationSkillCompletionKinds.Group, group.GroupId, 1), "test.mobility")]
        };
        var groupIntent = Intent(fixture, inputs, new([], [
            new(CharacterCreationSkillCompletionKinds.Group, group.GroupId),
            .. group.MemberSkillSourceIds.Select(id => new CharacterCreationSkillCompletionTarget(CharacterCreationSkillKinds.Active, id))
        ])) with
        {
            ExcludedChoices = [new(CompletionChoiceDomain.ActiveSkill, group.MemberSkillSourceIds[0])]
        };
        var blockedGroup = Proposal(CompletionRecommendationBuilder.Build(inputs, groupIntent, groupPack));
        Assert.IsFalse(blockedGroup.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == group.MemberSkillSourceIds[0]));
        var groupPlan = Reviewed(blockedGroup);
        Assert.AreEqual(0, groupPlan.Selections.GroupAllocations.Count);
        CollectionAssert.Contains(groupPlan.Skills!.Blockers.ToArray(), CharacterCreationSkillCompletionBlockers.GroupMemberLocked);
    }

    [TestMethod]
    public void Explicit_reserves_limit_actual_Core_spending_and_leave_unmet_targets_visible()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var empty = SmallPack() with { Attributes = [], Skills = [] };
        var baselineIntent = Intent(fixture, inputs, new([], []));
        var baseline = Reviewed(Proposal(CompletionRecommendationBuilder.Build(inputs, baselineIntent, empty)));
        var reserves = new CompletionAssistantReserves(
            NormalAttributePoints: (int)baseline.Attributes!.Preview.NormalPointBudget.Remaining - 1,
            SpecialAttributePoints: (int)baseline.Attributes.Preview.SpecialPointBudget.Remaining,
            ActiveSkillPoints: (int)baseline.Skills!.Preview.ActiveSkillPointBudget.Remaining - 1,
            SkillGroupPoints: (int)baseline.Skills.Preview.SkillGroupPointBudget.Remaining,
            KnowledgeSkillPoints: (int)baseline.Skills.Preview.KnowledgeSkillPointBudget.Remaining,
            Karma: baseline.Review.Balances.RemainingCreationKarma!.Value,
            Nuyen: baseline.Review.Balances.RemainingNuyen!.Value);
        var pack = SmallPack(6, 6);
        var proposal = Proposal(CompletionRecommendationBuilder.Build(inputs,
            Intent(fixture, inputs, Permissions(pack)) with { Reserves = reserves }, pack));
        var plan = Reviewed(proposal);

        Assert.AreEqual(2, plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
        Assert.AreEqual(1, plan.Skills!.Preview.Skills.Single(row => row.SourceSkillId == Perception).EffectiveRating);
        Assert.AreEqual((decimal)reserves.NormalAttributePoints, plan.Attributes.Preview.NormalPointBudget.Remaining);
        Assert.AreEqual((decimal)reserves.ActiveSkillPoints, plan.Skills.Preview.ActiveSkillPointBudget.Remaining);
        Assert.IsTrue(plan.Skills.Preview.SkillGroupPointBudget.Remaining >= reserves.SkillGroupPoints);
        Assert.IsTrue(plan.Skills.Preview.KnowledgeSkillPointBudget.Remaining >= reserves.KnowledgeSkillPoints);
        Assert.IsTrue(plan.Review.Balances.RemainingCreationKarma >= reserves.Karma);
        Assert.IsTrue(plan.Review.Balances.RemainingNuyen >= reserves.Nuyen);
        Assert.IsTrue(proposal.Suggestions.All(row => row.State == CompletionSuggestionState.TargetUnmet));
        CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.starter-targets");
        Assert.AreEqual(reserves.Karma, proposal.Request.KarmaToReserve);
        Assert.AreEqual(reserves.Nuyen, proposal.Request.NuyenToReserve);
    }

    [TestMethod]
    public void Each_skill_target_uses_its_own_Core_rating_when_other_targets_remain_unmet()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([]);
        var initial = fixture.ReadInputs();
        var influence = initial.Skills.Authority.SkillGroups.Single(row => row.Name == "Influence");
        var native = initial.Skills.Authority.KnowledgeSkills.First(row => row.CanBeNativeLanguage);
        fixture.SaveSkills([new(native.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)],
            [new(influence.GroupId, 2)]);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        string originalInputs = JsonSerializer.Serialize(inputs);
        var baselinePack = SmallPack() with { Attributes = [], Skills = [] };
        var baseline = Reviewed(Proposal(CompletionRecommendationBuilder.Build(inputs,
            Intent(fixture, inputs, new([], [])), baselinePack)));
        string member = influence.MemberSkillSourceIds[0];
        string pistols = inputs.Skills.Authority.ActiveSkills.Single(row => row.Name == "Pistols").SourceSkillId;
        var pack = baselinePack with
        {
            Skills = [
                new(new(CharacterCreationSkillKinds.Active, member, 2), "test.group-backed"),
                new(new(CharacterCreationSkillKinds.Active, Perception, 0), "test.zero-goal"),
                new(new(CharacterCreationSkillKinds.Active, pistols, 1), "test.unspent-reserve"),
                new(new(CharacterCreationSkillKinds.Knowledge, native.SourceSkillId, 0), "test.nonnumeric-native")
            ]
        };
        var intent = Intent(fixture, inputs, Permissions(pack)) with
        {
            Reserves = new(ActiveSkillPoints: (int)baseline.Skills!.Preview.ActiveSkillPointBudget.Remaining)
        };
        var proposal = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
        var plan = Reviewed(proposal);
        CollectionAssert.Contains(plan.Skills!.Blockers.ToArray(), CharacterCreationSkillCompletionBlockers.TargetsUnfulfilled);
        var grouped = proposal.Suggestions.Single(row => row.Choice.SourceId == member);
        Assert.AreEqual(CompletionSuggestionState.TargetMet, grouped.State);
        Assert.AreEqual(2, grouped.EvaluatedRating);
        var absentZero = proposal.Suggestions.Single(row => row.Choice.SourceId == Perception);
        Assert.AreEqual(CompletionSuggestionState.TargetMet, absentZero.State);
        Assert.AreEqual(0, absentZero.EvaluatedRating);
        var reserved = proposal.Suggestions.Single(row => row.Choice.SourceId == pistols);
        Assert.AreEqual(CompletionSuggestionState.TargetUnmet, reserved.State);
        Assert.AreEqual(0, reserved.EvaluatedRating);
        var nativeGoal = proposal.Suggestions.Single(row => row.Choice.SourceId == native.SourceSkillId);
        Assert.AreEqual(CompletionSuggestionState.TargetUnmet, nativeGoal.State);
        Assert.IsNull(nativeGoal.EvaluatedRating);
        Assert.IsFalse(plan.Skills.Preview.Skills.Any(row => row.Kind == CharacterCreationSkillKinds.Active),
            "An effective group rating and reserved numeric goals must not create individual allocations.");
        CollectionAssert.AreEqual(inputs.Skills.Allocations.ToArray(), plan.Selections.SkillAllocations.ToArray());
        CollectionAssert.AreEqual(inputs.Skills.GroupAllocations.ToArray(), plan.Selections.GroupAllocations.ToArray());
        Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Unknown_exact_kit_source_is_visible_and_never_substituted_with_a_similar_skill()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        const string unavailable = "00000000-0000-0000-0000-000000000123";
        var pack = SmallPack() with
        {
            Attributes = [],
            Skills = [new(new(CharacterCreationSkillKinds.Active, unavailable, 3), "test.notice-danger")]
        };
        Assert.IsFalse(inputs.Skills.Authority.ActiveSkills.Any(row => row.SourceSkillId == unavailable));
        var proposal = Proposal(CompletionRecommendationBuilder.Build(inputs, Intent(fixture, inputs, Permissions(pack)), pack));
        var suggestion = proposal.Suggestions.Single();
        Assert.AreEqual(unavailable, suggestion.Choice.SourceId);
        Assert.AreEqual(CompletionSuggestionState.SourceUnavailable, suggestion.State);
        Assert.AreEqual(0, proposal.Request.Skills.Targets!.Count);
        Assert.AreEqual(0, proposal.Request.Skills.PermittedTargetOrder.Count);
        Assert.IsFalse(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == Perception));
        CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.starter-targets");
        CollectionAssert.AreEqual(inputs.Skills.Allocations.ToArray(),
            Reviewed(proposal).Selections.SkillAllocations.Where(row => !row.IsNativeLanguage).ToArray());
    }

    [TestMethod]
    public void Missing_sources_do_not_invalidate_known_targets_or_leave_unknown_permissions()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        const string missingAttribute = "UNAVAILABLE";
        const string missingSkill = "00000000-0000-0000-0000-000000000123";
        var originalPack = SmallPack();
        var pack = originalPack with
        {
            Attributes = [.. originalPack.Attributes, new(new(missingAttribute, 3), "test.missing-attribute")],
            Skills = [.. originalPack.Skills, new(new(CharacterCreationSkillKinds.Active, missingSkill, 3), "test.missing-skill")]
        };
        var intent = Intent(fixture, inputs, Permissions(pack));
        var files = fixture.CaptureFiles();
        string originalInputs = JsonSerializer.Serialize(inputs);
        string originalIntent = JsonSerializer.Serialize(intent);
        var proposal = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
        var plan = Reviewed(proposal);

        Assert.AreEqual(3, plan.Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent);
        Assert.AreEqual(2, plan.Skills!.Preview.Skills.Single(row => row.SourceSkillId == Perception).EffectiveRating);
        CollectionAssert.AreEqual(new[] { "BOD" }, proposal.Request.Attributes.PermittedAttributeOrder.ToArray());
        CollectionAssert.AreEqual(new[] { new CharacterCreationSkillCompletionTarget(CharacterCreationSkillKinds.Active, Perception) },
            proposal.Request.Skills.PermittedTargetOrder.ToArray());
        Assert.AreEqual(1, proposal.Request.Attributes.Targets!.Count);
        Assert.AreEqual(1, proposal.Request.Skills.Targets!.Count);
        Assert.IsTrue(proposal.Suggestions.Where(row => row.Choice.SourceId is missingAttribute or missingSkill)
            .All(row => row.State == CompletionSuggestionState.SourceUnavailable && row.EvaluatedRating is null));
        Assert.IsTrue(proposal.Suggestions.Where(row => row.Choice == BodyChoice || row.Choice == PerceptionChoice)
            .All(row => row.State == CompletionSuggestionState.TargetMet));
        Assert.AreEqual(originalIntent, JsonSerializer.Serialize(proposal.Intent),
            "The original user permissions remain bound as intent; only the evaluated request drops unknown source identities.");
        Assert.AreEqual(originalInputs, JsonSerializer.Serialize(inputs));
        Assert.AreEqual(originalIntent, JsonSerializer.Serialize(intent));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Proposal_binds_intent_owner_transition_and_exact_original_input_and_source_identity()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var pack = SmallPack();
        var intent = Intent(fixture, inputs, Permissions(pack));
        var original = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
        var reworded = Proposal(CompletionRecommendationBuilder.Build(inputs, intent with { Concept = "A cautious street medic" }, pack));
        var transitioned = Proposal(CompletionRecommendationBuilder.Build(inputs, intent with
        {
            OwnerContext = intent.OwnerContext with { TransitionRevision = intent.OwnerContext.TransitionRevision + 2 }
        }, pack));
        Assert.AreNotEqual(original.ProposalDigest, reworded.ProposalDigest);
        Assert.AreNotEqual(original.ProposalDigest, transitioned.ProposalDigest,
            "Same owner name after an A-to-B-to-A transition must not reuse the proposal identity.");
        Assert.AreEqual(CompletionCoverageState.Unsupported, Coverage(reworded, "concept").State);

        var invalidOwner = CompletionRecommendationBuilder.Build(inputs, intent with { OwnerContext = default }, pack);
        Assert.IsNull(invalidOwner.Value);
        CollectionAssert.Contains(invalidOwner.Blockers.ToArray(), CompletionAssistantRules.IntentInvalid);
        var changedSource = inputs with
        {
            Skills = inputs.Skills with
            {
                Binding = inputs.Skills.Binding with
                {
                    SkillsAuthorityDigest = CharacterCreationSkillsDigest.Compute("different-source-authority")
                }
            }
        };
        Assert.AreNotEqual(inputs.SnapshotDigest, CharacterCreationCompletionReviewRules.ComputeInputsDigest(changedSource));
        var drift = CompletionRecommendationBuilder.Build(changedSource, intent, pack);
        Assert.IsNull(drift.Value);
        CollectionAssert.Contains(drift.Blockers.ToArray(), CompletionAssistantRules.SnapshotConflict);

        var malformed = inputs with
        {
            Skills = inputs.Skills with
            {
                Attributes = inputs.Skills.Attributes with { Attributes = [null!] }
            }
        };
        malformed = malformed with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(malformed) };
        var malformedResult = CompletionRecommendationBuilder.Build(malformed,
            intent with { InputsSnapshotDigest = malformed.SnapshotDigest }, pack);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, malformedResult.Outcome);
        Assert.IsNull(malformedResult.Value);

        fixture.SaveAttributes([new("BOD", 3, 0)]);
        var freshInputs = fixture.ReadInputs();
        var stale = CompletionRecommendationBuilder.Build(freshInputs, intent, pack);
        Assert.IsNull(stale.Value);
        CollectionAssert.Contains(stale.Blockers.ToArray(), CompletionAssistantRules.SnapshotConflict);
        var fresh = Proposal(CompletionRecommendationBuilder.Build(freshInputs,
            intent with { InputsSnapshotDigest = freshInputs.SnapshotDigest }, pack));
        Assert.AreNotEqual(original.ProposalDigest, fresh.ProposalDigest);
        Assert.AreEqual(4, Reviewed(fresh).Attributes!.Preview.Attributes.Single(row => row.AttributeId == "BOD").UnaugmentedCurrent,
            "An explicitly permitted addition must not lower a saved manual rating above the recommendation.");
    }

    [TestMethod]
    public void Optional_or_unsupported_domains_are_honest_and_opt_ins_do_not_invent_purchases()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var pack = SmallPack() with { Attributes = [], Skills = [] };
        var intent = Intent(fixture, inputs, new([], []));
        var basic = Proposal(CompletionRecommendationBuilder.Build(inputs, intent, pack));
        var optedIn = Proposal(CompletionRecommendationBuilder.Build(inputs,
            intent with { PermitImplants = true, PermitNegativeQualities = true }, pack));
        foreach (var proposal in new[] { basic, optedIn })
        {
            var plan = Reviewed(proposal);
            Assert.AreEqual(CompletionCoverageState.Unsupported, Coverage(proposal, "loadout").State);
            Assert.AreEqual(CompletionCoverageState.NotEvaluated, Coverage(proposal, "identity").State);
            Assert.AreEqual(CompletionCoverageState.OptionalManual, Coverage(proposal, "specializations").State);
            Assert.AreEqual(CompletionCoverageState.CoreValidated, Coverage(proposal, "qualities").State);
            Assert.AreEqual(CompletionCoverageState.CoreValidated, Coverage(proposal, "contacts").State);
            Assert.AreEqual(CompletionCoverageState.NotApplicable, Coverage(proposal, "magic").State);
            Assert.AreEqual(CompletionCoverageState.NotApplicable, Coverage(proposal, "life-modules").State);
            Assert.IsFalse(proposal.Coverage.Any(row => row.Category == "concept"));
            Assert.IsFalse(proposal.RemainingTasks.Contains(Coverage(proposal, "specializations").ExplanationKey));
            CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), Coverage(proposal, "loadout").ExplanationKey);
            CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.finalization-review");
            Assert.AreEqual(0, proposal.Request.Purchases.Count);
            Assert.AreEqual(0, plan.Selections.Gear.Count);
            Assert.IsNull(plan.Selections.StartingLifestyleId);
            CollectionAssert.AreEqual(inputs.Lifestyles.Lifestyles.ToArray(), plan.Selections.Lifestyles.ToArray());
            CollectionAssert.AreEqual(inputs.Contacts.Contacts.ToArray(), plan.Selections.Contacts.ToArray());
            CollectionAssert.AreEqual(inputs.Lifestyles.Qualities.SelectedOptionIds.ToArray(), plan.Selections.QualityOptionIds.ToArray());
            Assert.AreEqual(JsonSerializer.Serialize(inputs.Magic.Selections), JsonSerializer.Serialize(plan.Selections.MagicSelections));
            Assert.AreEqual(inputs.Lifestyles.Gear.Resources.PendingDraft?.KarmaInvestment ?? 0,
                plan.Selections.TotalKarmaInvestment);
        }
        Assert.AreEqual(CompletionCoverageState.OptionalManual, Coverage(basic, "implants").State);
        Assert.AreEqual(CompletionCoverageState.Unsupported, Coverage(optedIn, "implants").State);
        CollectionAssert.Contains(optedIn.RemainingTasks.ToArray(), Coverage(optedIn, "implants").ExplanationKey);
        Assert.AreNotEqual(basic.ProposalDigest, optedIn.ProposalDigest);
    }

    [TestMethod]
    public void Archetype_prefill_is_editable_no_evidence_advice_and_never_replaces_an_explicit_player_role()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        string before = JsonSerializer.Serialize(inputs);
        var suggested = Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs));
        Assert.AreEqual(CompletionAssistantRules.GeneralSurvivor, suggested.ArchetypeId);
        Assert.AreEqual(CompletionArchetypeConfidence.NoEvidence, suggested.Confidence);
        Assert.IsTrue(suggested.WasPrefilled);
        Assert.IsTrue(suggested.IsEditable);
        Assert.IsTrue(suggested.HasRecommendationPack);
        Assert.AreEqual("deterministic-baseline", suggested.RecommendationMode);
        Assert.AreEqual(0, suggested.Evidence.Count);
        Assert.AreEqual(inputs.SnapshotDigest, suggested.InputsSnapshotDigest);
        Assert.AreEqual(suggested.SuggestionDigest, CompletionAssistantRules.ComputeArchetypeSuggestionDigest(suggested));
        Assert.AreEqual(suggested.SuggestionDigest,
            Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs)).SuggestionDigest);

        const string customRole = "player-custom-social-medic";
        var selected = Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs, customRole));
        Assert.AreEqual(customRole, selected.ArchetypeId);
        Assert.AreEqual(CompletionArchetypeConfidence.PlayerSelected, selected.Confidence);
        Assert.IsFalse(selected.WasPrefilled);
        Assert.IsFalse(selected.HasRecommendationPack);
        Assert.AreNotEqual(suggested.SuggestionDigest, selected.SuggestionDigest);
        var intent = Intent(fixture, inputs, new([], [])) with
        {
            ArchetypeId = " ", Provenance = [new(BodyChoice, CompletionChoiceOrigin.UntouchedDefault)],
            ExcludedChoices = [PerceptionChoice], Reserves = new(Karma: 5, Nuyen: 100m)
        };
        var prefilled = Proposal(CompletionRecommendationBuilder.Build(inputs, intent));
        Assert.AreEqual(CompletionAssistantRules.GeneralSurvivor, prefilled.Intent.ArchetypeId);
        Assert.AreEqual(JsonSerializer.Serialize(intent.Permissions), JsonSerializer.Serialize(prefilled.Intent.Permissions));
        Assert.AreEqual(JsonSerializer.Serialize(intent.Provenance), JsonSerializer.Serialize(prefilled.Intent.Provenance));
        Assert.AreEqual(JsonSerializer.Serialize(intent.ExcludedChoices), JsonSerializer.Serialize(prefilled.Intent.ExcludedChoices));
        Assert.AreEqual(intent.Reserves, prefilled.Intent.Reserves);
        Assert.AreEqual(0, prefilled.Request.Attributes.Targets!.Count);
        Assert.AreEqual(0, prefilled.Request.Skills.Targets!.Count);

        var unknown = CompletionRecommendationBuilder.Build(inputs, intent with { ArchetypeId = customRole });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, unknown.Outcome);
        Assert.IsNull(unknown.Value);
        CollectionAssert.Contains(unknown.Blockers.ToArray(), CompletionAssistantRules.ArchetypeUnsupported);
        Assert.AreEqual(before, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Archetype_uses_real_saved_social_skill_evidence_and_binds_changed_source_observations()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var initial = fixture.ReadInputs();
        var original = Archetype(CompletionRecommendationBuilder.SuggestArchetype(initial));
        fixture.SaveAttributes([]);
        var prepared = fixture.ReadInputs();
        var etiquette = prepared.Skills.Authority.ActiveSkills.Single(row => row.Name == "Etiquette");
        var negotiation = prepared.Skills.Authority.ActiveSkills.Single(row => row.Name == "Negotiation");
        var native = prepared.Skills.Authority.KnowledgeSkills.First(row => row.CanBeNativeLanguage);
        fixture.SaveSkills([
            new(etiquette.SourceSkillId, CharacterCreationSkillKinds.Active, 4, null, false),
            new(negotiation.SourceSkillId, CharacterCreationSkillKinds.Active, 4, null, false),
            new(native.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)
        ], []);
        var inputs = fixture.ReadInputs();
        Assert.IsNotNull(inputs.Skills.SavedSkillsState, "The inference fixture must carry real admitted saved skills.");
        var files = fixture.CaptureFiles();
        string before = JsonSerializer.Serialize(inputs);
        var face = Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs));
        Assert.AreEqual(CompletionAssistantRules.Face, face.ArchetypeId);
        Assert.IsTrue(face.WasPrefilled);
        Assert.IsTrue(face.Confidence is CompletionArchetypeConfidence.Tentative or CompletionArchetypeConfidence.Supported);
        Assert.IsTrue(face.HasRecommendationPack);
        Assert.IsTrue(face.Evidence.Any(row => row.Kind == CompletionArchetypeEvidenceKind.ActiveSkill
            && row.SourceId == etiquette.SourceSkillId && row.ObservedValue == 4));
        Assert.IsTrue(face.Evidence.Any(row => row.Kind == CompletionArchetypeEvidenceKind.ActiveSkill
            && row.SourceId == negotiation.SourceSkillId && row.ObservedValue == 4));
        Assert.AreNotEqual(original.SuggestionDigest, face.SuggestionDigest);
        Assert.AreEqual(face.SuggestionDigest, CompletionAssistantRules.ComputeArchetypeSuggestionDigest(face));

        var proposed = Proposal(CompletionRecommendationBuilder.Build(inputs,
            Intent(fixture, inputs, new([], [])) with { ArchetypeId = string.Empty }));
        Assert.AreEqual(CompletionAssistantRules.Face, proposed.Intent.ArchetypeId);
        CollectionAssert.AreEqual(inputs.Skills.Allocations.ToArray(), Reviewed(proposed).Selections.SkillAllocations.ToArray());
        Assert.AreEqual(0, proposed.Request.Skills.PermittedTargetOrder.Count);
        Assert.AreEqual(0, proposed.Request.Attributes.PermittedAttributeOrder.Count);
        Assert.AreEqual(0, proposed.Request.Skills.Targets!.Count);

        var changedSource = inputs with
        {
            Skills = inputs.Skills with
            {
                Binding = inputs.Skills.Binding with
                {
                    SkillsAuthorityDigest = CharacterCreationSkillsDigest.Compute("another-archetype-source")
                }
            }
        };
        var stale = CompletionRecommendationBuilder.SuggestArchetype(changedSource);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, stale.Outcome);
        Assert.IsNull(stale.Value);
        CollectionAssert.Contains(stale.Blockers.ToArray(), CompletionAssistantRules.SnapshotConflict);
        var malformed = inputs with
        {
            Skills = inputs.Skills with { Attributes = inputs.Skills.Attributes with { Attributes = null! } }
        };
        malformed = malformed with { SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(malformed) };
        var invalid = CompletionRecommendationBuilder.SuggestArchetype(malformed);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, invalid.Outcome);
        Assert.IsNull(invalid.Value);
        foreach (var brokenTalent in new[]
        {
            inputs.Magic.SelectedTalent with { Identity = null! },
            inputs.Magic.SelectedTalent with { Blockers = null! }
        })
        {
            var malformedTalent = inputs with { Magic = inputs.Magic with { SelectedTalent = brokenTalent } };
            malformedTalent = malformedTalent with
            {
                SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(malformedTalent)
            };
            var rejected = CompletionRecommendationBuilder.SuggestArchetype(malformedTalent);
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, rejected.Outcome);
            Assert.IsNull(rejected.Value, "Malformed talent data must be rejected, not thrown or inferred as mundane.");
        }
        foreach (var unavailableAuthority in new[]
        {
            inputs with { Skills = inputs.Skills with { Authority = inputs.Skills.Authority with { IsAuthoritative = false } } },
            inputs with { Magic = inputs.Magic with { Authority = inputs.Magic.Authority with { IsAuthoritative = false } } }
        })
        {
            var unavailable = unavailableAuthority with
            {
                SnapshotDigest = CharacterCreationCompletionReviewRules.ComputeInputsDigest(unavailableAuthority)
            };
            var rejected = CompletionRecommendationBuilder.SuggestArchetype(unavailable);
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, rejected.Outcome);
            Assert.IsNull(rejected.Value);
            CollectionAssert.Contains(rejected.Blockers.ToArray(), CompletionAssistantRules.ArchetypeBaselineUnavailable);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Archetype_recognizes_a_real_saved_social_group_without_inventing_individual_skill_purchases()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([]);
        var initial = fixture.ReadInputs();
        var influence = initial.Skills.Authority.SkillGroups.Single(row => row.Name == "Influence");
        var native = initial.Skills.Authority.KnowledgeSkills.First(row => row.CanBeNativeLanguage);
        fixture.SaveSkills([new(native.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)],
            [new(influence.GroupId, 2)]);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        Assert.AreEqual(0, inputs.Skills.Allocations.Count(row => row.Kind == CharacterCreationSkillKinds.Active));
        var suggested = Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs));
        Assert.AreEqual(CompletionAssistantRules.Face, suggested.ArchetypeId);
        Assert.IsTrue(suggested.Confidence is CompletionArchetypeConfidence.Tentative or CompletionArchetypeConfidence.Supported);
        Assert.IsTrue(suggested.Evidence.Any(row => row.Kind == CompletionArchetypeEvidenceKind.SkillGroup
            && row.SourceId == influence.GroupId && row.ObservedValue == 2));
        Assert.AreEqual(0, inputs.Skills.Allocations.Count(row => row.Kind == CharacterCreationSkillKinds.Active));

        var chosen = Archetype(CompletionRecommendationBuilder.SuggestArchetype(inputs, CompletionAssistantRules.StreetSamurai));
        Assert.AreEqual(CompletionAssistantRules.StreetSamurai, chosen.ArchetypeId);
        Assert.AreEqual(CompletionArchetypeConfidence.PlayerSelected, chosen.Confidence);
        Assert.IsFalse(chosen.WasPrefilled);
        Assert.IsTrue(chosen.Alternatives.Any(row => row.ArchetypeId == CompletionAssistantRules.Face),
            "Keep evidence-backed alternatives visible without replacing the player's selection.");
        fixture.AssertFilesUnchanged(files);
    }

    private static CompletionArchetypeSuggestion Archetype(CharacterCreationFoundationResult<CompletionArchetypeSuggestion> result)
    {
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
        Assert.IsNotNull(result.Value);
        return result.Value;
    }

    private static CompletionAssistantIntent Intent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs, CompletionAdditivePermissions permissions)
        => new(fixture.Owners.Capture(), inputs.SnapshotDigest, CompletionAssistantRules.GeneralSurvivor,
            permissions, new())
        {
            NativeLanguageSourceId = inputs.Skills.Authority.KnowledgeSkills.First(row => row.CanBeNativeLanguage).SourceSkillId
        };

    private static CompletionRecommendationPack SmallPack(int body = 3, int perception = 2)
        => new("test.survivor", "1", CompletionAssistantRules.GeneralSurvivor,
            [new(new("BOD", body), "test.resilience")],
            [new(new(CharacterCreationSkillKinds.Active, Perception, perception), "test.awareness")]);

    private static CompletionAdditivePermissions Permissions(CompletionRecommendationPack pack)
        => new(pack.Attributes.Select(row => row.Target.AttributeId).ToArray(),
            pack.Skills.Select(row => new CharacterCreationSkillCompletionTarget(row.Target.Kind, row.Target.SourceId)).ToArray());

    private static CompletionAssistantProposal Proposal(CharacterCreationFoundationResult<CompletionAssistantProposal> result)
    {
        Assert.IsNotNull(result.Value, string.Join(",", result.Blockers));
        return result.Value;
    }

    private static CharacterCreationCompletionPlan Reviewed(CompletionAssistantProposal proposal)
    {
        Assert.IsNotNull(proposal.Evaluation.Value, string.Join(",", proposal.Evaluation.Blockers));
        Assert.IsTrue(proposal.CanReview);
        return proposal.Evaluation.Value;
    }

    private static CompletionCategoryCoverage Coverage(CompletionAssistantProposal proposal, string category)
        => proposal.Coverage.Single(row => row.Category == category);
}
