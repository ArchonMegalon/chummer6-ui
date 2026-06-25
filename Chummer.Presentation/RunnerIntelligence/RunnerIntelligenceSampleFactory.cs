using System.Collections.Generic;

namespace Chummer.Presentation.RunnerIntelligence;

public interface IRunnerIntelligenceScenarioCatalog
{
    RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = "sample-runner",
        string ruleset = "SR5",
        string cohortLabel = "street samurai / 150 karma");
}

public sealed class RunnerIntelligenceScenarioCatalog : IRunnerIntelligenceScenarioCatalog
{
    public RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = "sample-runner",
        string ruleset = "SR5",
        string cohortLabel = "street samurai / 150 karma")
        => RunnerIntelligenceSampleFactory.BuildIncreaseInitiativeScenario(runnerId, ruleset, cohortLabel);
}

public static class RunnerIntelligenceSampleFactory
{
    public static RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = "sample-runner",
        string ruleset = "SR5",
        string cohortLabel = "street samurai / 150 karma")
        => new(
            "increase_initiative_force_6",
            "Increase Initiative Force 6",
            "Benchmarks Initiative posture, then models Increase Initiative Force 6 with inventory synergy and drain/stun risk without mutating the character.",
            BuildIncreaseInitiativeSample(runnerId, ruleset, cohortLabel));

    public static RunnerIntelligenceInput BuildIncreaseInitiativeSample(
        string runnerId = "sample-runner",
        string ruleset = "SR5",
        string cohortLabel = "street samurai / 150 karma")
    {
        return new RunnerIntelligenceInput(
            runnerId,
            ruleset,
            cohortLabel,
            new Dictionary<string, decimal>
            {
                ["initiative"] = 14,
                ["defense_pool"] = 12,
                ["soak_pool"] = 18
            },
            new HashSet<string>
            {
                "jazz",
                "armor_jacket"
            },
            new[]
            {
                new RunnerBenchmark(
                    "initiative",
                    "Initiative",
                    cohortLabel,
                    new decimal[] { 7, 8, 8, 9, 9, 10, 10, 11, 12, 13, 14, 16, 18 }),
                new RunnerBenchmark(
                    "defense_pool",
                    "Defense Pool",
                    cohortLabel,
                    new decimal[] { 6, 7, 8, 8, 9, 10, 11, 12, 12, 13, 14 }),
                new RunnerBenchmark(
                    "soak_pool",
                    "Soak Pool",
                    cohortLabel,
                    new decimal[] { 8, 10, 12, 14, 15, 16, 18, 20, 22, 24 })
            },
            new[]
            {
                new RunnerWhatIfEffect(
                    Id: "increase_initiative_force_6",
                    Label: "Increase Initiative Force 6",
                    TargetStatKey: "initiative",
                    StatDelta: 12,
                    InventoryItemKeys: new[] { "jazz" },
                    ResistancePool: 21,
                    RiskThreshold: 1,
                    RiskSeverity: 6,
                    RiskLabel: "Drain/Stun Risk",
                    RiskBoundary: "Illustrative staged math targets the 87% chance of taking no more than 1 Stun example until authoritative rules-engine fixtures own final drain and spellcasting resolution.",
                    Notes: new[]
                    {
                        "Models Increase Initiative plus inventory synergy without mutating the character.",
                        "Jazz is present in inventory and can be layered as a separate what-if effect by the UI."
                    })
            },
            RunnerIntelligencePrivacy.LocalOnly);
    }
}
