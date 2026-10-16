using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Files;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation.Creation;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
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
    [DataRow("Mystic Adept", CharacterCreationMagicResonanceKinds.Tradition)]
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
        Assert.AreEqual(inputs.Magic.Selections.MysticAdeptPowerPoints, magic.Selections.MysticAdeptPowerPoints);
        CollectionAssert.AreEqual(inputs.Magic.Selections.AdeptPowers.ToArray(), magic.Selections.AdeptPowers.ToArray());
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
    [DataRow("Magician")]
    [DataRow("Mystic Adept")]
    public void Editable_tradition_is_exact_digest_bound_and_never_silently_falls_back(string talent)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent);
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
    [DataRow("Mystic Adept", CharacterCreationMagicResonanceKinds.Tradition)]
    [DataRow("Technomancer", CharacterCreationMagicResonanceKinds.Stream)]
    public void Real_saved_magic_choices_and_receipts_are_kept_exact(string talent, string identityKind)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent);
        var original = fixture.ReadInputs();
        var intent = Intent(fixture, original);
        if (identityKind == CharacterCreationMagicResonanceKinds.Tradition)
            intent = intent with { PreferredTradition = new(CharacterCreationMagicResonanceKinds.Tradition, Shamanic) };
        if (talent == "Mystic Adept") intent = intent with { Reserves = new(Karma: 10) };
        var initial = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(original, intent));
        var chosen = Magic(initial).Selections;
        var attributes = initial.Evaluation.Value!.Selections.AttributeAllocations;
        if (talent == "Mystic Adept")
        {
            // Only this explicit player request buys a point and allocates powers.
            // The subsequent assistant must retain those admitted saved choices.
            var manual = CharacterCreationMagicCompletionPlanner.Plan(original.Magic, original.Lifestyles.Qualities,
                attributes, original.Lifestyles.Qualities.SelectedOptionIds, initial.Request.Magic with
                {
                    DesiredTotalMysticAdeptPowerPoints = 1, MaximumAdditionalPowerPointKarmaSpend = 10,
                    KarmaToReserve = 0,
                    RankedAdeptPowers = original.Magic.Authority.AdeptPowers
                        .Where(row => row.IsEnabled && row.Blockers.Count == 0)
                        .Take(CharacterCreationMagicCompletionPlanner.MaximumCandidatesPerKind)
                        .Select(row => row.Identity).ToArray()
                });
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, manual.Outcome, string.Join(",", manual.Blockers));
            Assert.IsNotNull(manual.Value);
            chosen = manual.Value.Selections;
            Assert.AreEqual(1, chosen.MysticAdeptPowerPoints);
            Assert.IsTrue(chosen.AdeptPowers.Count > 0);
        }
        fixture.SaveAttributes(attributes, attributes.Select(row => row.AttributeId).ToArray());
        fixture.SaveMagic(chosen);
        var inputs = fixture.ReadInputs();
        Assert.IsNotNull(inputs.Magic.SavedMagicState);
        var files = fixture.CaptureFiles();
        string before = JsonSerializer.Serialize(inputs.Magic);
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs, Intent(fixture, inputs)));
        Assert.AreEqual(JsonSerializer.Serialize(inputs.Magic.Selections), JsonSerializer.Serialize(Magic(proposal).Selections));
        Assert.IsFalse(Magic(proposal).HasChanges);
        Assert.IsNull(proposal.Request.Magic.DesiredTotalMysticAdeptPowerPoints);
        Assert.AreEqual(0, proposal.Request.Magic.MaximumAdditionalPowerPointKarmaSpend);
        Assert.AreEqual(0, proposal.Request.Magic.RankedAdeptPowers.Count);
        Assert.AreEqual(0, CompletionRecommendationBuilder.GetMagicIdentityChoices(inputs, identityKind).Count);
        Assert.AreEqual("completion.coverage.retained-magic", proposal.Coverage.Single(row => row.Category == "magic").ExplanationKey);
        if (identityKind == CharacterCreationMagicResonanceKinds.Tradition)
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
    [DataRow("Magician")]
    [DataRow("Mystic Adept")]
    public void Identity_advice_omits_disabled_blocked_and_ambiguous_source_rows(string talent)
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: talent);
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

    [TestMethod]
    public void Mystic_adept_magic_advice_cannot_hide_an_unavailable_Core_karma_reserve()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext, magicTalent: "Mystic Adept");
        var inputs = fixture.ReadInputs();
        var files = fixture.CaptureFiles();
        var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
            Intent(fixture, inputs) with { Reserves = new(Karma: 1000) }));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, proposal.Evaluation.Outcome);
        CollectionAssert.Contains(Magic(proposal).Blockers.ToArray(), CharacterCreationMagicCompletionBlockers.KarmaReserveUnavailable);
        Assert.AreEqual(1000, proposal.Request.Magic.KarmaToReserve);
        Assert.AreEqual(0, Magic(proposal).Selections.MysticAdeptPowerPoints);
        Assert.AreEqual(0, proposal.Request.Magic.MaximumAdditionalPowerPointKarmaSpend);
        CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.finalization-review");
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Sum_to_ten_actual_mystic_adept_gets_magic_advice_without_unlocking_legacy_attributes()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        var workspace = CreateSumToTenMysticAdept(fixture);
        var attributes = new CharacterCreationAttributesService(fixture.Store, fixture.Resolver);
        var state = attributes.Load(new(workspace)).Value!;
        // A genuine saved draft without recorded edit intent, including zeros.
        var preview = attributes.Preview(new(state.Binding, [])).Value!;
        var saved = attributes.Confirm(new(preview.Binding, [], preview.PreviewDigest, true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, saved.Outcome, string.Join(",", saved.Blockers));
        IOwnerBoundCharacterCreationCompletionReader reader =
            new OwnerBoundCharacterCreationCompletionReader(fixture.Store, fixture.Owners, fixture.Resolver, fixture.Queries);
        var loaded = reader.LoadCompletionInputs(fixture.Owners.Capture(), new(workspace));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, loaded.Outcome, string.Join(",", loaded.Blockers));
        Assert.IsNotNull(loaded.Value);
        var inputs = loaded.Value;
        var files = fixture.CaptureFiles();
        Assert.AreEqual(CharacterCreationBuildMethods.SumToTen, inputs.Skills.PrerequisiteDraft.BuildMethod);
        Assert.AreEqual(CharacterCreationMagicResonanceKinds.MysticAdept, inputs.Magic.SelectedTalent.Kind);
        Assert.IsNull(inputs.Skills.Attributes.PendingDraft!.PlayerChosenAttributeIds);
        foreach (string style in new[] { CompletionAssistantRules.Mage, string.Empty })
        {
            var proposal = Proposal(CompletionRecommendationBuilder.BuildMissingOnly(inputs,
                Intent(fixture, inputs) with { ArchetypeId = style }));
            Assert.IsTrue(Magic(proposal).IsMagicComplete, string.Join(",", Magic(proposal).Blockers));
            Assert.IsNotNull(Magic(proposal).Selections.Tradition);
            Assert.IsTrue(Magic(proposal).Selections.Spells.Count > 0);
            Assert.AreEqual(0m, Magic(proposal).Preview.SpellBudget.Remaining);
            Assert.AreEqual(0, proposal.Request.Attributes.PermittedAttributeOrder.Count);
            CollectionAssert.AreEqual(inputs.Skills.Attributes.PendingDraft.Allocations.ToArray(),
                proposal.Evaluation.Value!.Selections.AttributeAllocations.ToArray());
            Assert.AreEqual(0, Magic(proposal).Selections.MysticAdeptPowerPoints);
            Assert.AreEqual(0, Magic(proposal).Selections.AdeptPowers.Count);
            Assert.IsNull(proposal.Request.Magic.DesiredTotalMysticAdeptPowerPoints);
            Assert.AreEqual(CompletionCoverageState.Unsupported,
                proposal.Coverage.Single(row => row.Category == "adept-powers").State);
            CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), CompletionAssistantRules.AttributeChoiceIntentRequired);
            CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.magic-choices");
            CollectionAssert.Contains(proposal.RemainingTasks.ToArray(), "completion.remaining.finalization-review");
        }
        fixture.AssertFilesUnchanged(files);
    }

    private static CharacterWorkspaceId CreateSumToTenMysticAdept(CompletionRecommendationTestFixture fixture)
    {
        var codec = new Sr5WorkspaceCodec(fixture.Queries,
            new XmlCharacterSectionQueries(new CharacterSectionService(fixture.Resolver)),
            new XmlCharacterMetadataCommands(new CharacterFileService()));
        var bootstrap = new CharacterCreationBootstrapService(fixture.Store,
            new RulesetWorkspaceCodecResolver([codec]), fixture.Queries, fixture.Resolver);
        Assert.IsTrue(CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(
            CharacterCreationBuildMethods.SumToTen, out string profile));
        var created = bootstrap.Create(new(CharacterCreationBootstrapSchemas.RequestV1,
            CharacterCreationBootstrapStages.AwaitingFoundationSelection, RulesetDefaults.Sr5,
            "Sum-to-Ten Mystic Adept", "Completion", CharacterCreationBuildMethods.SumToTen, profile));
        Assert.AreEqual(CharacterCreationBootstrapOutcomes.Success, created.Outcome, string.Join(",", created.Blockers));
        Assert.IsNotNull(created.Value);
        var workspace = created.Value.WorkspaceId;
        var service = new CharacterCreationPrerequisiteService(fixture.Store, fixture.Queries, fixture.Resolver);
        var state = service.Load(new(workspace)).Value!;
        var ranks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CharacterCreationPriorityCategoryIds.Heritage] = "E", [CharacterCreationPriorityCategoryIds.Talent] = "B",
            [CharacterCreationPriorityCategoryIds.Attributes] = "A", [CharacterCreationPriorityCategoryIds.Skills] = "C",
            [CharacterCreationPriorityCategoryIds.Resources] = "D"
        };
        var heritage = state.Authority.Options.Single(row => row.CategoryId == CharacterCreationPriorityCategoryIds.Heritage
                && row.Rank == "E").HeritageOptions.First(row => row.IsEnabled
                && row.MetavariantSourceId is null && row.MetatypeName == "Human");
        var talent = state.Authority.Options.Single(row => row.CategoryId == CharacterCreationPriorityCategoryIds.Talent
                && row.Rank == "B").TalentOptions.First(row => row.IsEnabled && row.Value == "Mystic Adept");
        var grants = talent.ActiveSkillGrant!.Options.Where(row => row.IsEnabled)
            .Take(talent.ActiveSkillGrant.Quantity).Select(row => row.SelectionId).ToArray();
        var preview = service.Preview(new(state.Binding, ranks)
        {
            HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId,
            TalentActiveSkillSelectionIds = grants, TalentSkillGroupSelectionIds = []
        }).Value!;
        var confirmed = service.Confirm(new(preview.Binding, ranks, preview.PreviewDigest, true)
        {
            HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId,
            TalentActiveSkillSelectionIds = grants, TalentSkillGroupSelectionIds = []
        });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, confirmed.Outcome, string.Join(",", confirmed.Blockers));
        return workspace;
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
