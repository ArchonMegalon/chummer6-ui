using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Creation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CompletionMagicRecommendationBuilderTests
{
    private const string Hermetic = "19320625-bc1a-492f-8904-da6a847e5700";
    private const string Shamanic = "8d185e0e-5f49-4992-babd-d1ac9c848f68";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("Magician", CharacterCreationMagicResonanceKinds.Tradition)]
    [DataRow("Technomancer", CharacterCreationMagicResonanceKinds.Stream)]
    public void Missing_magic_uses_actual_talent_and_fills_free_slots_with_source_admitted_advice(
        string talent, string identityKind)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        string before = JsonSerializer.Serialize(inputs);
        // Role advice must not grant or suppress the actual selected talent.
        var intent = Intent(fixture, inputs) with { ArchetypeId = CompletionAssistantRules.GeneralSurvivor };
        var choices = CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs, identityKind);
        Assert.IsTrue(choices.Count > 0, "The real Core profile must supply this identity choice.");
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        var magic = Magic(proposal);

        Assert.IsTrue(magic.IsMagicComplete, string.Join(",", magic.Blockers));
        Assert.IsTrue(magic.HasChanges);
        Assert.AreEqual(0m, magic.Preview.SpellBudget.Remaining);
        Assert.AreEqual(0m, magic.Preview.ComplexFormBudget.Remaining);
        Assert.AreEqual(choices[0].Identity, identityKind == CharacterCreationMagicResonanceKinds.Tradition
            ? magic.Selections.Tradition : magic.Selections.Stream);
        Assert.IsTrue(identityKind == CharacterCreationMagicResonanceKinds.Tradition
            ? magic.Selections.Spells.Count > 0 && magic.Selections.ComplexForms.Count == 0
            : magic.Selections.ComplexForms.Count > 0 && magic.Selections.Spells.Count == 0);
        Assert.AreEqual(JsonSerializer.Serialize(inputs.Magic.Selections), JsonSerializer.Serialize(proposal.Request.Magic.ManualSelections));
        Assert.IsNull(proposal.Request.Magic.DesiredTotalMysticAdeptPowerPoints);
        Assert.AreEqual(0, proposal.Request.Magic.MaximumAdditionalPowerPointKarmaSpend);
        Assert.AreEqual(0, proposal.Request.Magic.RankedAdeptPowers.Count);
        Assert.AreEqual("completion.coverage.proposed-magic", proposal.Coverage.Single(row => row.Category == "magic").ExplanationKey);
        AssertAdmitted(proposal.Request.Magic.RankedSpells, inputs.Magic.Authority.Spells);
        AssertAdmitted(proposal.Request.Magic.RankedComplexForms, inputs.Magic.Authority.ComplexForms);
        Assert.AreEqual(proposal.ProposalDigest,
            Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent)).ProposalDigest);
        var additive = Proposal(CompletionRecommendationBuilder.Build(inputs, intent));
        Assert.AreEqual(0, additive.Request.Magic.RankedTraditions.Count);
        Assert.AreEqual(0, additive.Request.Magic.RankedStreams.Count);
        Assert.AreEqual(0, additive.Request.Magic.RankedSpells.Count);
        Assert.AreEqual(0, additive.Request.Magic.RankedComplexForms.Count);
        Assert.AreEqual(before, JsonSerializer.Serialize(inputs));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Editable_tradition_is_exact_digest_bound_and_never_silently_falls_back()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: "Magician");
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var intent = Intent(fixture, inputs);
        var automatic = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent));
        Assert.AreEqual(Hermetic, Magic(automatic).Selections.Tradition!.SourceId);
        var shamanic = CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs,
            CharacterCreationMagicResonanceKinds.Tradition).Single(row => row.Identity.SourceId == Shamanic).Identity;
        var selected = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, intent with { PreferredTradition = shamanic }));
        CollectionAssert.AreEqual(new[] { shamanic }, selected.Request.Magic.RankedTraditions.ToArray());
        Assert.AreEqual(shamanic, Magic(selected).Selections.Tradition);
        Assert.AreNotEqual(automatic.ProposalDigest, selected.ProposalDigest);
        Assert.IsNull(inputs.Magic.Selections.Tradition);

        var unavailable = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { PreferredTradition = shamanic with { SourceId = "unavailable-source" } });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, unavailable.Outcome);
        Assert.IsNull(unavailable.Value);
        var wrongKind = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { PreferredTradition = shamanic with { Kind = CharacterCreationMagicResonanceKinds.Stream } });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, wrongKind.Outcome);
        Assert.IsNull(wrongKind.Value);
        var stale = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            intent with { InputsSnapshotDigest = "stale", PreferredTradition = shamanic });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, stale.Outcome);
        Assert.IsNull(stale.Value);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    [DataRow("Magician", CharacterCreationMagicResonanceKinds.Tradition)]
    [DataRow("Technomancer", CharacterCreationMagicResonanceKinds.Stream)]
    public void Real_saved_magic_choices_and_receipts_are_kept_exact(string talent, string identityKind)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent);
        var original = fixture.ReadInputs();
        var intent = Intent(fixture, original);
        if (talent == "Magician")
            intent = intent with { PreferredTradition = new(CharacterCreationMagicResonanceKinds.Tradition, Shamanic) };
        var initial = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(original, intent));
        var chosen = Magic(initial).Selections;
        var attributes = initial.Evaluation.Value!.Selections.AttributeAllocations;
        fixture.SaveAttributes(attributes, attributes.Select(row => row.AttributeId).ToArray());
        fixture.SaveMagic(chosen);
        var inputs = fixture.ReadInputs();
        Assert.IsNotNull(inputs.Magic.SavedMagicState);
        var files = fixture.CaptureFiles();
        string before = JsonSerializer.Serialize(inputs.Magic);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        Assert.AreEqual(JsonSerializer.Serialize(inputs.Magic.Selections), JsonSerializer.Serialize(Magic(proposal).Selections));
        Assert.IsFalse(Magic(proposal).HasChanges);
        Assert.AreEqual(0, CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs, identityKind).Count);
        Assert.AreEqual("completion.coverage.retained-magic", proposal.Coverage.Single(row => row.Category == "magic").ExplanationKey);
        if (talent == "Magician")
        {
            var replacement = CompletionRecommendationBuilder.BuildMissingOnly(inputs,
                Intent(fixture, inputs) with { PreferredTradition = new(CharacterCreationMagicResonanceKinds.Tradition, Hermetic) });
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, replacement.Outcome);
            Assert.IsNull(replacement.Value);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(fixture.ReadInputs().Magic));
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Identity_advice_omits_disabled_blocked_and_ambiguous_source_rows()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: "Magician");
        var inputs = fixture.ReadInputs();
        var choices = CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs, CharacterCreationMagicResonanceKinds.Tradition);
        var hermetic = choices.Single(row => row.Identity.SourceId == Hermetic);
        var shamanic = choices.Single(row => row.Identity.SourceId == Shamanic);
        // These detached rows test the read-only advice filter only. They are
        // never resealed or offered to Core as genuine rule/source authority.
        var changed = inputs with { Magic = inputs.Magic with { Authority = inputs.Magic.Authority with
        {
            Traditions = [hermetic, hermetic with { IsEnabled = false }, shamanic with { Blockers = ["blocked-source"] }]
        } } };
        Assert.AreEqual(0, CompletionRecommendationBuilder.GetMagicIdentityChoices(changed,
            CharacterCreationMagicResonanceKinds.Tradition).Count);
        var forged = CompletionRecommendationBuilder.BuildMissingOnly(changed, Intent(fixture, inputs));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, forged.Outcome);
        Assert.IsNull(forged.Value);
        Assert.AreEqual(choices.Count, CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs,
            CharacterCreationMagicResonanceKinds.Tradition).Count);
    }

    [TestMethod]
    [DataRow("Mundane")]
    [DataRow("Mystic Adept")]
    [DataRow("Aspected Magician")]
    public void Unsupported_magic_advice_does_not_invent_entitlement_or_power_point_purchase(string talent)
    {
        using var fixture = talent == "Aspected Magician"
            ? CompletionRecommendationTestFixture.Create(TestContext, zeroRatedTalentGroup: "Conjuring")
            : CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent == "Mundane" ? null : talent);
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { ArchetypeId = CompletionAssistantRules.Mage }));
        Assert.AreEqual(0, proposal.Request.Magic.RankedTraditions.Count);
        Assert.AreEqual(0, proposal.Request.Magic.RankedStreams.Count);
        Assert.AreEqual(0, proposal.Request.Magic.RankedSpells.Count);
        Assert.AreEqual(0, proposal.Request.Magic.RankedComplexForms.Count);
        Assert.AreEqual(0, proposal.Request.Magic.RankedAdeptPowers.Count);
        Assert.IsNull(proposal.Request.Magic.DesiredTotalMysticAdeptPowerPoints);
        Assert.AreEqual(0, proposal.Request.Magic.MaximumAdditionalPowerPointKarmaSpend);
        Assert.AreEqual(0, CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs,
            CharacterCreationMagicResonanceKinds.Tradition).Count);
        if (talent != "Mundane")
            Assert.IsTrue(proposal.RemainingTasks.Contains("completion.remaining.magic-choices"));
        fixture.AssertFilesUnchanged(files);
    }

    private static CompletionAssistantIntent Intent(CompletionRecommendationTestFixture fixture,
        CharacterCreationCompletionReviewInputs inputs)
        => new(fixture.Owners.Capture(), inputs.SnapshotDigest, CompletionAssistantRules.Mage, new([], []), new());

    private static CompletionAssistantProposal Proposal(CharacterCreationFoundationResult<CompletionAssistantProposal> result)
    {
        Assert.IsNotNull(result.Value, string.Join(",", result.Blockers));
        return result.Value;
    }

    private static CharacterCreationMagicCompletionPlan Magic(CompletionAssistantProposal proposal)
    {
        Assert.IsNotNull(proposal.Evaluation.Value, string.Join(",", proposal.Evaluation.Blockers));
        Assert.IsNotNull(proposal.Evaluation.Value.Magic, string.Join(",", proposal.Evaluation.Blockers));
        return proposal.Evaluation.Value.Magic;
    }

    private static void AssertAdmitted(IReadOnlyList<CharacterCreationMagicResonanceOptionIdentity> identities,
        IReadOnlyList<CharacterCreationMagicResonanceCatalogOption> options)
    {
        foreach (var identity in identities)
            Assert.AreEqual(1, options.Count(row => row.Identity == identity && row.IsEnabled && row.Blockers.Count == 0));
    }
}
