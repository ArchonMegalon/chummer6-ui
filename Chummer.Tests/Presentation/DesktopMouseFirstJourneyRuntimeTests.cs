using Chummer.Desktop.Runtime;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopMouseFirstJourneyRuntimeTests
{
    [TestMethod]
    public void ReadPlan_uses_defaults_when_environment_is_empty()
    {
        using EnvironmentOverride scope = new();

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("sr5-priority", plan.ScenarioId);
        Assert.AreEqual("Mouse Journey Runner", plan.CharacterName);
        Assert.AreEqual("MouseRoute", plan.CharacterAlias);
        Assert.AreEqual("sr5", plan.RulesetId);
        Assert.AreEqual("Priority", plan.BuildMethod);
        Assert.IsNull(plan.MetatypeCategory);
        Assert.IsNull(plan.PriorityHeritage);
        Assert.IsNull(plan.Metatype);
        Assert.IsNull(plan.PriorityTalent);
        Assert.IsNull(plan.PriorityTalentChoice);
    }

    [TestMethod]
    public void ReadPlan_normalizes_priority_scenario_fields()
    {
        using EnvironmentOverride scope = new()
            .Set(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable, "SR6")
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "priority")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeCategoryEnvironmentVariable, "Show All")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable, "a")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable, "Shapeshifter: Vulpine")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityTalentEnvironmentVariable, "b")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityTalentChoiceEnvironmentVariable, "Mystic Adept")
            .Set(DesktopMouseFirstJourneyRuntime.CharacterNameEnvironmentVariable, "Mysad Troll")
            .Set(DesktopMouseFirstJourneyRuntime.CharacterAliasEnvironmentVariable, "MouseTroll");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("SR6", plan.RulesetId);
        Assert.AreEqual("Priority", plan.BuildMethod);
        Assert.AreEqual("Show All", plan.MetatypeCategory);
        Assert.AreEqual("A", plan.PriorityHeritage);
        Assert.AreEqual("Shapeshifter: Vulpine", plan.Metatype);
        Assert.AreEqual("B", plan.PriorityTalent);
        Assert.AreEqual("Mystic Adept", plan.PriorityTalentChoice);
        Assert.AreEqual("Mysad Troll", plan.CharacterName);
        Assert.AreEqual("MouseTroll", plan.CharacterAlias);
        Assert.AreEqual("sr6-priority-show-all-a-shapeshifter-vulpine-mystic-adept", plan.ScenarioId);
    }

    [TestMethod]
    public void ReadPlan_clears_priority_heritage_for_non_priority_builds()
    {
        using EnvironmentOverride scope = new()
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "karma")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable, "B");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("Karma", plan.BuildMethod);
        Assert.IsNull(plan.PriorityHeritage);
        Assert.IsNull(plan.PriorityTalent);
        Assert.IsNull(plan.PriorityTalentChoice);
        Assert.AreEqual("sr5-karma", plan.ScenarioId);
    }

    [TestMethod]
    public void ReadPlan_preserves_bp_build_method_for_sr4()
    {
        using EnvironmentOverride scope = new()
            .Set(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable, "sr4")
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "bp");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("sr4", plan.RulesetId);
        Assert.AreEqual("BP", plan.BuildMethod);
        Assert.AreEqual("sr4-bp", plan.ScenarioId);
    }

    private sealed class EnvironmentOverride : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentOverride()
        {
            Track(DesktopMouseFirstJourneyRuntime.ScenarioIdEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.CharacterNameEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.CharacterAliasEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.MetatypeCategoryEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityTalentEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityTalentChoiceEnvironmentVariable);

            foreach (string key in _originalValues.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        public EnvironmentOverride Set(string key, string? value)
        {
            Environment.SetEnvironmentVariable(key, value);
            return this;
        }

        public void Dispose()
        {
            foreach ((string key, string? value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private void Track(string key)
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }
    }
}
