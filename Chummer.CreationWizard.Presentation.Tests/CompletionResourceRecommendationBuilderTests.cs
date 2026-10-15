using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CompletionResourceRecommendationBuilderTests
{
    private const string Perception = "04e1eb3e-e82d-485b-a7fd-1e677df2a070";
    private const string English = "907a3a8f-2c40-4543-a5a9-891bcde41f6b";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Default_missing_only_balances_real_pools_and_reports_actual_ratings_without_writes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs);
        var files = fixture.CaptureFiles();
        string original = JsonSerializer.Serialize(inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = Plan(proposal);

        Assert.IsTrue(proposal.FillsRemainingResources);
        Assert.AreEqual("deterministic-resource-plan", proposal.RecommendationMode);
        Assert.IsTrue(proposal.Request.Attributes.FillRemainingPoints);
        Assert.IsTrue(proposal.Request.Skills.FillRemainingPoints);
        Assert.AreEqual(0, proposal.Request.Attributes.Targets!.Count);
        Assert.AreEqual(0, proposal.Request.Skills.Targets!.Count);
        Assert.IsTrue(proposal.Request.Attributes.PermittedAttributeOrder.Contains("EDG"));
        Assert.AreEqual(0, plan.Attributes!.Preview.NormalPointBudget.Remaining);
        Assert.AreEqual(0, plan.Skills!.Preview.ActiveSkillPointBudget.Remaining);
        Assert.AreEqual(0, plan.Skills.Preview.SkillGroupPointBudget.Remaining);
        Assert.AreEqual(0, plan.Skills.Preview.KnowledgeSkillPointBudget.Remaining);
        Assert.IsTrue(plan.Selections.GroupAllocations.Count > 0);
        Assert.IsTrue(plan.Selections.SkillAllocations.Any(row => row.Kind == CharacterCreationSkillKinds.Knowledge
            && !row.IsNativeLanguage && row.Rating > 0));
        Assert.AreEqual(English, proposal.Request.Skills.NativeLanguageSourceId);
        Assert.IsTrue(plan.Selections.SkillAllocations.Any(row => row.SourceSkillId == English && row.IsNativeLanguage));
        foreach (var suggestion in proposal.Suggestions.Where(row => row.EvaluatedRating is not null))
            Assert.AreEqual(suggestion.EvaluatedRating, suggestion.TargetRating,
                "Balanced advice must display the actual Core result, not an unused starter floor.");
        Assert.IsTrue(proposal.Coverage.Any(row => row.Category == "loadout"
            && row.State == CompletionCoverageState.Unsupported));
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.practical-loadout"));
        Assert.AreEqual(0m, proposal.Request.MaximumAdditionalPurchaseSpend);
        Assert.AreEqual(0, proposal.Request.Purchases.Count);
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Full_resource_path_keeps_chosen_zero_skills_native_and_group_members_exact()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("BOD", 0, 0)], ["BOD"]);
        var beforeSkills = fixture.ReadInputs();
        var influence = beforeSkills.Skills.Authority.SkillGroups.Single(row => row.Name == "Influence");
        fixture.SaveSkills([
            new(Perception, CharacterCreationSkillKinds.Active, 1, null, false),
            new(English, CharacterCreationSkillKinds.Knowledge, null, null, true)
        ], [new(influence.GroupId, 1)]);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs) with
        {
            // Neither declared defaults nor automatic full filling can unlock
            // Core-recorded choices or inherited group members.
            Provenance = [new(new(CompletionChoiceDomain.Attribute, "BOD"), CompletionChoiceOrigin.UntouchedDefault),
                new(new(CompletionChoiceDomain.ActiveSkill, Perception), CompletionChoiceOrigin.UntouchedDefault)]
        };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = Plan(proposal);
        Assert.IsFalse(proposal.Request.Attributes.PermittedAttributeOrder.Contains("BOD"));
        Assert.IsFalse(proposal.Request.Skills.PermittedTargetOrder.Any(row => row.SourceId == Perception
            || row.SourceId == influence.GroupId || influence.MemberSkillSourceIds.Contains(row.SourceId)));
        Assert.IsNull(proposal.Request.Skills.NativeLanguageSourceId);
        Assert.AreEqual(inputs.Skills.Attributes.PendingDraft!.Allocations.Single(row => row.AttributeId == "BOD"),
            plan.Selections.AttributeAllocations.Single(row => row.AttributeId == "BOD"));
        foreach (var original in inputs.Skills.Allocations)
            Assert.AreEqual(original, plan.Selections.SkillAllocations.Single(row =>
                row.Kind == original.Kind && row.SourceSkillId == original.SourceSkillId));
        Assert.AreEqual(inputs.Skills.GroupAllocations.Single(),
            plan.Selections.GroupAllocations.Single(row => row.GroupId == influence.GroupId));
    }

    [TestMethod]
    public void Resource_reserves_and_explicit_purchase_cap_are_bound_and_Core_evaluated()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var intent = Intent(fixture, inputs) with
        {
            Reserves = new(NormalAttributePoints: 2, SpecialAttributePoints: 4,
                ActiveSkillPoints: 2, SkillGroupPoints: 1, KnowledgeSkillPoints: 2, Nuyen: 1000m),
            MaximumAdditionalPurchaseSpend = 5000m
        };
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = Plan(proposal);
        Assert.AreEqual(2, plan.Attributes!.Preview.NormalPointBudget.Remaining);
        Assert.IsTrue(plan.Attributes.Preview.SpecialPointBudget.Remaining >= 4);
        Assert.AreEqual(2, plan.Skills!.Preview.ActiveSkillPointBudget.Remaining);
        Assert.AreEqual(1, plan.Skills.Preview.SkillGroupPointBudget.Remaining);
        Assert.AreEqual(2, plan.Skills.Preview.KnowledgeSkillPointBudget.Remaining);
        Assert.AreEqual(5000m, proposal.Request.MaximumAdditionalPurchaseSpend);
        Assert.AreEqual(1000m, proposal.Request.NuyenToReserve);
        Assert.IsTrue(proposal.Request.Purchases.Count > 0,
            "A funded explicit cap should produce a real admitted resource tail, not an unconsumed setting.");
        Assert.AreEqual(1, proposal.Request.Purchases.Count(row => row.Lifestyle is not null));
        Assert.IsTrue(proposal.Request.Purchases.Any(row => row.Gear is not null));
        Assert.IsNotNull(plan.Purchases);
        Assert.IsTrue(plan.Purchases.IsPurchasePlanComplete);
        Assert.IsTrue(plan.Purchases.AdditionalSpend <= 5000m);
        Assert.IsTrue(plan.Review.Balances.RemainingNuyen >= 1000m);
        Assert.AreEqual(1, plan.Selections.Lifestyles.Count);
        Assert.IsTrue(plan.Selections.Gear.Count > 0);
        Assert.IsTrue(proposal.Request.Contacts.Count > inputs.Contacts.Contacts.Count);
        Assert.IsFalse(proposal.Request.PermitNegativeQualityAdditions);
        Assert.AreEqual(inputs.Lifestyles.Gear.Resources.PendingDraft?.KarmaInvestment ?? 0,
            proposal.Request.TotalKarmaInvestment);
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
        fixture.AssertFilesUnchanged(files);

        var invalid = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { MaximumAdditionalPurchaseSpend = -1m });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, invalid.Outcome);
        Assert.IsNull(invalid.Value);
    }

    [TestMethod]
    public void Unaffordable_optional_tail_is_omitted_without_fabricating_a_finished_loadout()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { MaximumAdditionalPurchaseSpend = 1m }));
        Assert.AreEqual(0, proposal.Request.Purchases.Count);
        Assert.IsNull(proposal.Request.StartingLifestyleId);
        Assert.IsFalse(proposal.Evaluation.Blockers.Contains(CharacterCreationPurchaseCompletionBlockers.TargetsUnfulfilled));
        Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.practical-loadout"));
        AssertDefaultStreetCash(proposal);

        var gearOnly = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { MaximumAdditionalPurchaseSpend = 100m }));
        Assert.IsTrue(gearOnly.Request.Purchases.Any(row => row.Gear is not null),
            "An unaffordable home must not suppress independently affordable fixed gear.");
        Assert.IsFalse(gearOnly.Request.Purchases.Any(row => row.Lifestyle is not null));
        Assert.IsFalse(gearOnly.Evaluation.Blockers.Contains(CharacterCreationPurchaseCompletionBlockers.TargetsUnfulfilled));
        Assert.IsTrue(gearOnly.RemainingTasks.Contains("completion.remaining.practical-loadout"));
        AssertDefaultStreetCash(gearOnly);
        fixture.AssertFilesUnchanged(files);

        void AssertDefaultStreetCash(CompletionAssistantProposal candidate)
        {
            var plan = Plan(candidate);
            Assert.AreEqual(0, plan.Selections.Lifestyles.Count);
            Assert.IsNull(plan.Selections.StartingLifestyleId);
            Assert.AreEqual(CompletionCoverageState.CoreValidated,
                candidate.Coverage.Single(row => row.Category == "lifestyle").State);
            Assert.IsFalse(candidate.RemainingTasks.Contains("completion.remaining.lifestyle-choices"),
                "Core permits zero purchased homes and resolves default Street cash; do not invent a missing-home gate.");
            var service = new OwnerBoundCharacterCreationCompletionService(
                fixture.Store, fixture.Owners, fixture.Resolver, fixture.Queries);
            var cash = service.LoadStartingCash(fixture.Owners.Capture(), fixture.WorkspaceId,
                new(inputs.SnapshotDigest, plan.Selections));
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, cash.Outcome, string.Join(",", cash.Blockers));
            Assert.IsNotNull(cash.Value);
            Assert.AreEqual("Street", cash.Value.Name);
            Assert.IsTrue(CharacterCreationKarmaFinalizationBudgetRules.IsValidStartingCashSource(cash.Value),
                "Fallback terms must be genuine Core authority, not UI-invented dice or a fabricated home.");
        }
    }

    [TestMethod]
    public void Group_advice_uses_its_own_reserve_not_the_stale_original_knowledge_budget()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        var observed = CharacterCreationSkillCompletionPlanner.Plan(inputs.Skills,
            inputs.Skills.Attributes.PendingDraft?.Allocations ?? [],
            new(inputs.Skills.Binding, inputs.Skills.Attributes.SnapshotDigest, inputs.Skills.SnapshotDigest, [])
            { Targets = [] }, CancellationToken.None);
        Assert.IsNotNull(observed.Value, string.Join(",", observed.Blockers));
        var original = observed.Value.Preview;
        decimal remainingKnowledge = original.KnowledgeSkillPointBudget.Remaining;
        Assert.AreEqual(decimal.Truncate(remainingKnowledge), remainingKnowledge,
            "The real Core fixture must provide an integral knowledge-point budget.");
        int knowledgeReserve = checked(decimal.ToInt32(remainingKnowledge) + 1);
        var intent = Intent(fixture, inputs) with { Reserves = new(KnowledgeSkillPoints: knowledgeReserve) };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var plan = Plan(proposal);

        Assert.IsTrue(proposal.Request.Skills.PermittedTargetOrder.Any(row =>
            row.Kind == CharacterCreationSkillCompletionKinds.Group));
        Assert.IsTrue(plan.Selections.GroupAllocations.Count > 0);
        Assert.AreEqual(0, plan.Skills!.Preview.SkillGroupPointBudget.Remaining);
        Assert.AreEqual(knowledgeReserve, plan.Skills.Preview.KnowledgeSkillPointBudget.Remaining,
            "The final Core plan must preserve the requested reserve after its actual attribute additions.");
        Assert.IsTrue(plan.Skills.Preview.KnowledgeSkillPointBudget.Total > original.KnowledgeSkillPointBudget.Total);
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
    }

    [TestMethod]
    public void Unavailable_mage_skill_for_a_mundane_is_not_relabelled_as_an_achieved_zero()
    {
        const string spellcasting = "c4367a39-4065-4b1d-aa62-e9dce377e452";
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var inputs = fixture.ReadInputs();
        Assert.AreEqual(CharacterCreationMagicResonanceKinds.Mundane, inputs.Magic.SelectedTalent.Kind);
        Assert.IsTrue(inputs.Skills.Authority.ActiveSkills.Any(row => row.SourceSkillId == spellcasting));
        var intent = Intent(fixture, inputs) with { ArchetypeId = CompletionAssistantRules.Mage };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var suggestion = proposal.Suggestions.Single(row => row.Choice ==
            new CompletionChoiceIdentity(CompletionChoiceDomain.ActiveSkill, spellcasting));

        Assert.AreEqual(0, suggestion.EvaluatedRating);
        Assert.IsTrue(suggestion.TargetRating > 0,
            "An unavailable skill is unmet authored advice, not a successful zero-point allocation.");
        Assert.AreEqual(CompletionSuggestionState.TargetUnmet, suggestion.State);
        Assert.IsFalse(Plan(proposal).Selections.SkillAllocations.Any(row => row.SourceSkillId == spellcasting));
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
    }

    [TestMethod]
    public void Contacts_use_free_budget_for_useful_ratings_deterministically_without_workspace_changes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("CHA", 5, 0)], ["CHA"]);
        var inputs = fixture.ReadInputs();
        var original = JsonSerializer.Serialize(inputs);
        var files = fixture.CaptureFiles();
        var intent = Intent(fixture, inputs);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var contacts = ContactProjection(inputs, proposal);

        Assert.IsTrue(contacts.ContactBudget.Remaining == 0,
            "A useful network should consume the available ordinary free contact budget.");
        Assert.IsTrue(proposal.Request.Contacts.Count is > 0 and <= 4);
        Assert.AreEqual(proposal.Request.Contacts.Count,
            proposal.Request.Contacts.Select(row => row.ContactId).Distinct().Count());
        var fixer = proposal.Request.Contacts.Single(row => row.Identity.Role == "Fixer");
        var medic = proposal.Request.Contacts.Single(row => row.Identity.Role == "Street Medic");
        Assert.IsTrue(fixer.Connection > fixer.Loyalty && fixer.Loyalty > 1);
        Assert.IsTrue(medic.Loyalty > medic.Connection && medic.Connection > 1);
        var repeated = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        CollectionAssert.AreEqual(proposal.Request.Contacts.ToArray(), repeated.Request.Contacts.ToArray());
        Assert.AreEqual(proposal.ProposalDigest, repeated.ProposalDigest);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(CompletionAssistantRules.GeneralSurvivor, "Fixer", 4, 3)]
    [DataRow(CompletionAssistantRules.Face, "Corporate Liaison", 5, 2)]
    [DataRow(CompletionAssistantRules.StreetSamurai, "Arms Dealer", 4, 2)]
    [DataRow(CompletionAssistantRules.Mage, "Talismonger", 4, 2)]
    [DataRow(CompletionAssistantRules.Decker, "Matrix Broker", 4, 2)]
    [DataRow(CompletionAssistantRules.Rigger, "Mechanic", 2, 4)]
    public void Contact_roles_and_relationship_targets_follow_the_archetype(
        string archetype, string primaryRole, int connection, int loyalty)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("CHA", 5, 0)], ["CHA"]);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { ArchetypeId = archetype }));

        var primary = proposal.Request.Contacts.First();
        Assert.AreEqual(primaryRole, primary.Identity.Role);
        Assert.AreEqual(connection, primary.Connection);
        Assert.AreEqual(loyalty, primary.Loyalty);
        Assert.IsTrue(ContactProjection(inputs, proposal).ContactBudget.Remaining == 0);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Contact_advice_preserves_all_saved_identities_and_ratings_including_role_aliases()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("CHA", 5, 0)], ["CHA"]);
        fixture.PrepareContacts();
        fixture.SaveContact(new(Guid.Parse("dd5c2634-9d90-475f-a91b-0f2b17ac66e1"),
            new("fixer", "My broker", "Redmond", "Keep these notes", "Personal detail", "Human", "Female", "42",
                "Professional", "Favors", "Chess", "Family", ""), 1, 1));
        fixture.SaveContact(new(Guid.Parse("dd5c2634-9d90-475f-a91b-0f2b17ac66e2"),
            new("Doctor Quinn", "street DOC", "Tacoma", "Chosen by the player", "", "Elf", "Male", "55",
                "Professional", "Cash", "Music", "Private", ""), 1, 2));
        // A previously accepted suggestion has become a saved user choice too.
        fixture.SaveContact(new(Guid.Parse("dd5c2634-9d90-475f-a91b-0f2b17ac66e3"),
            new("Matrix Broker", "Matrix Broker", "", "", "", "", "", "", "Professional", "", "", "", ""), 1, 1));
        var inputs = fixture.ReadInputs();
        var original = JsonSerializer.Serialize(inputs);
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { ArchetypeId = CompletionAssistantRules.Decker }));

        CollectionAssert.AreEqual(inputs.Contacts.Contacts.ToArray(),
            proposal.Request.Contacts.Take(inputs.Contacts.Contacts.Count).ToArray());
        var added = proposal.Request.Contacts.Skip(inputs.Contacts.Contacts.Count).ToArray();
        Assert.AreEqual(1, added.Length);
        Assert.AreEqual("Electronics Technician", added.Single().Identity.Role);
        Assert.AreEqual(proposal.Request.Contacts.Count,
            proposal.Request.Contacts.Select(row => row.ContactId).Distinct().Count());
        ContactProjection(inputs, proposal);
        Assert.AreEqual(original, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void Tiny_contact_budgets_keep_legal_partial_advice_without_overspending(int existingLoyalty)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("CHA", 0, 0)], ["CHA"]);
        if (existingLoyalty > 0)
        {
            fixture.PrepareContacts();
            fixture.SaveContact(new(Guid.Parse("dd5c2634-9d90-475f-a91b-0f2b17ac66e4"),
                new("Old friend", "Neighbor", "", "", "", "", "", "", "Personal", "", "", "", ""), 1, existingLoyalty));
        }
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));

        var projection = ContactProjection(inputs, proposal);
        Assert.AreEqual(1, proposal.Request.Contacts.Count);
        if (existingLoyalty > 0)
            CollectionAssert.AreEqual(inputs.Contacts.Contacts.ToArray(), proposal.Request.Contacts.ToArray());
        else
        {
            Assert.IsTrue(projection.ContactBudget.Remaining == 0);
            Assert.AreEqual("Fixer", proposal.Request.Contacts.Single().Identity.Role);
            Assert.AreEqual(2, proposal.Request.Contacts.Single().Connection);
            Assert.AreEqual(1, proposal.Request.Contacts.Single().Loyalty);
        }
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Contacts_use_proposed_charisma_and_leave_whole_request_reserves_intact()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        fixture.SaveAttributes([new("BOD", 0, 0)], ["BOD"]);
        var inputs = fixture.ReadInputs();
        var baseline = CharacterCreationContactsService.PreviewCompletion(inputs.Contacts,
            inputs.Skills.Attributes.PendingDraft!.Allocations, inputs.Lifestyles.Qualities.SelectedOptionIds,
            inputs.Contacts.Contacts);
        Assert.IsNotNull(baseline.Value, string.Join(",", baseline.Blockers));
        var files = fixture.CaptureFiles();
        var intent = Intent(fixture, inputs) with
        {
            ArchetypeId = CompletionAssistantRules.Face,
            Reserves = new(Karma: 25, Nuyen: 1000m)
        };
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var projection = ContactProjection(inputs, proposal);

        Assert.IsTrue(projection.ContactBudget.Total > baseline.Value.ContactBudget.Total);
        Assert.IsTrue(projection.ContactBudget.Remaining == 0);
        Assert.AreEqual(intent.Reserves.Karma, proposal.Request.KarmaToReserve);
        Assert.AreEqual(intent.Reserves.Nuyen, proposal.Request.NuyenToReserve);
        Assert.IsTrue(Plan(proposal).Review.Balances.RemainingCreationKarma is { } karma && karma >= intent.Reserves.Karma);
        Assert.IsTrue(Plan(proposal).Review.Balances.RemainingNuyen is { } nuyen && nuyen >= intent.Reserves.Nuyen);
        CollectionAssert.AreEqual(inputs.Lifestyles.Qualities.SelectedOptionIds.ToArray(),
            proposal.Request.QualityOptionIds.ToArray(), "Contact advice cannot introduce qualities to manufacture free budget.");
        fixture.AssertFilesUnchanged(files);
    }

    private static CharacterCreationContactsCompletionProjection ContactProjection(
        CharacterCreationCompletionReviewInputs inputs, CompletionAssistantProposal proposal)
    {
        var plan = Plan(proposal);
        var preview = CharacterCreationContactsService.PreviewCompletion(inputs.Contacts,
            plan.Selections.AttributeAllocations, proposal.Request.QualityOptionIds, proposal.Request.Contacts);
        Assert.AreEqual(CharacterCreationContactOutcomes.Available, preview.Outcome, string.Join(",", preview.Blockers));
        Assert.IsNotNull(preview.Value, string.Join(",", preview.Blockers));
        Assert.AreEqual(0, preview.Value.Blockers.Count);
        Assert.IsTrue(preview.Value.AdditionalKarmaCost == 0);
        Assert.IsTrue(preview.Value.ContactBudget.IsExact && preview.Value.ContactBudget.Overspend == 0);
        Assert.IsTrue(preview.Value.HighPlacesBudget.IsExact && preview.Value.HighPlacesBudget.Overspend == 0);
        return preview.Value;
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

    private static CharacterCreationCompletionPlan Plan(CompletionAssistantProposal proposal)
    {
        Assert.IsNotNull(proposal.Evaluation.Value, string.Join(",", proposal.Evaluation.Blockers));
        return proposal.Evaluation.Value;
    }
}
