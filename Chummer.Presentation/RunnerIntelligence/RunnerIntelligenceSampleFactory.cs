using System.Collections.Generic;

namespace Chummer.Presentation.RunnerIntelligence;

public interface IRunnerIntelligenceScenarioCatalog
{
    RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = RunnerIntelligenceSampleFactory.DefaultRunnerId,
        string ruleset = RunnerIntelligenceSampleFactory.DefaultRuleset,
        string cohortLabel = RunnerIntelligenceSampleFactory.DefaultCohortLabel);
}

public sealed class RunnerIntelligenceScenarioCatalog : IRunnerIntelligenceScenarioCatalog
{
    public RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = RunnerIntelligenceSampleFactory.DefaultRunnerId,
        string ruleset = RunnerIntelligenceSampleFactory.DefaultRuleset,
        string cohortLabel = RunnerIntelligenceSampleFactory.DefaultCohortLabel)
        => RunnerIntelligenceSampleFactory.BuildIncreaseInitiativeScenario(runnerId, ruleset, cohortLabel);
}

public static class RunnerIntelligenceSampleFactory
{
    public const string DefaultRunnerId = "sample-runner";
    public const string DefaultRuleset = "SR5";
    public const string DefaultCohortLabel = "street samurai / 150 karma";
    public const string IncreaseInitiativeScenarioId = "increase_initiative_force_6";
    public const string IncreaseInitiativeLabel = "Increase Initiative Force 6";
    public const string InitiativeStatKey = "initiative";
    public const string JazzInventoryKey = "jazz";
    public const int IncreaseInitiativeForce = 6;
    public const decimal IncreaseInitiativeStatDelta = 12;
    public const int IncreaseInitiativeResistancePool = 21;
    public const int IncreaseInitiativeRiskThreshold = 1;
    public const decimal IncreaseInitiativeIncomingSeverity = 6;
    public const double IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent = 87.3d;

    public static RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = DefaultRunnerId,
        string ruleset = DefaultRuleset,
        string cohortLabel = DefaultCohortLabel)
        => new(
            IncreaseInitiativeScenarioId,
            IncreaseInitiativeLabel,
            $"Benchmarks Initiative posture, then models {IncreaseInitiativeLabel} with inventory synergy and drain/stun risk without mutating the character.",
            BuildIncreaseInitiativeSample(runnerId, ruleset, cohortLabel));

    public static RunnerIntelligenceInput BuildIncreaseInitiativeSample(
        string runnerId = DefaultRunnerId,
        string ruleset = DefaultRuleset,
        string cohortLabel = DefaultCohortLabel)
    {
        return new RunnerIntelligenceInput(
            runnerId,
            ruleset,
            cohortLabel,
            new Dictionary<string, decimal>
            {
                [InitiativeStatKey] = 14,
                ["defense_pool"] = 12,
                ["soak_pool"] = 18
            },
            new HashSet<string>
            {
                JazzInventoryKey,
                "armor_jacket"
            },
            new[]
            {
                new RunnerBenchmark(
                    InitiativeStatKey,
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
                    Id: IncreaseInitiativeScenarioId,
                    Label: IncreaseInitiativeLabel,
                    TargetStatKey: InitiativeStatKey,
                    StatDelta: IncreaseInitiativeStatDelta,
                    InventoryItemKeys: new[] { JazzInventoryKey },
                    ResistancePool: IncreaseInitiativeResistancePool,
                    RiskThreshold: IncreaseInitiativeRiskThreshold,
                    RiskSeverity: IncreaseInitiativeIncomingSeverity,
                    RiskLabel: "Drain/Stun Risk",
                    RiskBoundary: $"Illustrative staged math targets the {IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent:0.#}% chance of taking no more than {IncreaseInitiativeRiskThreshold} Stun example until authoritative rules-engine fixtures own final drain and spellcasting resolution.",
                    Notes: new[]
                    {
                        $"Models {IncreaseInitiativeLabel} plus inventory synergy without mutating the character.",
                        "Jazz is present in inventory and can be layered as a separate what-if effect by the UI."
                    })
            },
            RunnerIntelligencePrivacy.LocalOnly);
    }
}
