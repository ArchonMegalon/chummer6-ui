using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Presentation.OriginBooks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class OriginStoryDecisionTextTests
{
    [TestMethod]
    public void Story_choice_copy_preserves_ids_versions_and_authority_without_inventing_outcomes()
    {
        const string military = "lifemodules.xml#module:15bd4283-f287-4be7-b174-9e5ab97bda1a";
        OriginDossierLifeModuleChoiceState Choice(string id, string label, string anchor) =>
            new(id, label, "RF", "71", 50, "50", [], [], [anchor], "choice-digest", false);
        var choices = new[] { Choice("school-a", "Military School · A", military), Choice("school-b", "Military School · B", military) };
        var state = new OriginDossierLifeModuleDecisionState("owner", "workspace", 2, "Runner", "teen-years",
            LifeModuleJourneyStageOrders.TeenYears, 3, "de-AT", "raw template", "raw prompt", [], choices,
            LtdProvenanceState: "unavailable", LtdProviderDisplay: "No provider", LtdAffectsMechanics: false,
            SelectedChoiceId: null, PendingPreviewDigest: null,
            BoundTurnSeedDigest: "sealed-turn", CheckpointDigest: "sealed-checkpoint");
        string before = JsonSerializer.Serialize(state);
        string caption = OriginStoryDecisionText.Choice(state, "school-a");
        StringAssert.Contains(caption, "Militärschule");
        StringAssert.Contains(caption, choices[0].Label);
        Assert.AreNotEqual(caption, OriginStoryDecisionText.Choice(state, "school-b"));
        Assert.IsFalse(caption.Contains("Karma", StringComparison.Ordinal));
        Assert.ThrowsExactly<ArgumentException>(() => OriginStoryDecisionText.Choice(state, "not-issued"));
        Assert.AreEqual(before, JsonSerializer.Serialize(state));
        Assert.AreNotEqual(state.DecisionPrompt, OriginStoryDecisionText.Prompt(state));

        foreach (string locale in new[] { "en-US", "de-DE", "es-ES" })
        {
            var localized = state with { Locale = locale };
            Assert.AreNotEqual(choices[0].Label, OriginStoryDecisionText.Choice(localized, "school-a"));
            Assert.AreEqual(OriginStoryDecisionText.Choice(localized, "school-a"),
                OriginStoryDecisionText.Choice(localized with { Choices = choices.Reverse().ToArray() }, "school-a"));
        }
        Assert.AreEqual(OriginStoryDecisionText.Choice(state with { Locale = "en-US" }, "school-a"),
            OriginStoryDecisionText.Choice(state with { Locale = "fr-FR" }, "school-a"));

        // Unknown, ambiguous, wrong-stage and lookalike anchors do not inherit
        // the school story. Preserve the actual module label in neutral copy.
        foreach (var altered in new[] {
            choices[0] with { SourceAnchorIds = [military + "-not-the-module"] },
            choices[0] with { SourceAnchorIds = [military, "lifemodules.xml#module:custom"] },
            choices[0] with { SourceAnchorIds = ["custom.xml#module:custom"] } })
            Assert.AreEqual("Diesen Weg einschlagen: Military School · A.",
                OriginStoryDecisionText.Choice(state with { Choices = [altered] }, altered.ChoiceId));
        Assert.AreEqual("Diesen Weg einschlagen: Military School · A.",
            OriginStoryDecisionText.Choice(state with { StageOrder = LifeModuleJourneyStageOrders.RealLife }, "school-a"));

        var fakeFinish = choices[0] with { Label = "Finish Creation" };
        Assert.IsFalse(OriginStoryDecisionText.Choice(state with { Choices = [fakeFinish] }, fakeFinish.ChoiceId)
            .StartsWith("Mit dieser Vergangenheit", StringComparison.Ordinal));
        var finish = fakeFinish with { Effects = [new("finish", "creation-stage",
            CharacterCreationLifeModuleStageIds.SelectionFinished, "false", "true", 0, [], "effect-digest")] };
        Assert.AreEqual("Mit dieser Vergangenheit in ein neues Leben aufbrechen.",
            OriginStoryDecisionText.Choice(state with { Choices = [finish] }, finish.ChoiceId));
    }

}
