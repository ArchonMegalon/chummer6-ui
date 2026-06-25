using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Chummer.Presentation.RunnerIntelligence;

public interface IRunnerIntelligenceCalculator
{
    RunnerIntelligenceReport Calculate(RunnerIntelligenceScenario scenario);

    RunnerIntelligenceReport Calculate(RunnerIntelligenceInput input);

    double CalculatePercentileRank(decimal value, IReadOnlyCollection<decimal> cohortValues);

    RunnerRiskEstimate CalculateRisk(RunnerRiskInput input);
}

public sealed class RunnerIntelligenceCalculator : IRunnerIntelligenceCalculator
{
    public RunnerIntelligenceReport Calculate(RunnerIntelligenceScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return Calculate(scenario.Input);
    }

    public RunnerIntelligenceReport Calculate(RunnerIntelligenceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var benchmarks = input.Benchmarks
            .Select(benchmark => BuildBenchmark(input, benchmark))
            .ToArray();

        var whatIfCards = input.WhatIfEffects
            .Select(effect => BuildWhatIf(input, effect))
            .ToArray();

        var opportunities = BuildOpportunities(input, benchmarks, whatIfCards).ToArray();

        return new RunnerIntelligenceReport(
            input.RunnerId,
            input.Ruleset,
            input.CohortLabel,
            input.Privacy,
            benchmarks,
            whatIfCards,
            opportunities);
    }

    public double CalculatePercentileRank(decimal value, IReadOnlyCollection<decimal> cohortValues)
    {
        ArgumentNullException.ThrowIfNull(cohortValues);

        return PercentileRank(value, cohortValues);
    }

    public RunnerRiskEstimate CalculateRisk(RunnerRiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.ResistancePool <= 0 || input.FinalSeverityThreshold <= 0)
        {
            return RunnerRiskEstimate.None;
        }

        var chanceAtOrUnderThreshold = ChanceFinalSeverityAtOrBelow(
            input.ResistancePool,
            input.IncomingSeverity,
            input.FinalSeverityThreshold);
        var chancePercent = Math.Round(chanceAtOrUnderThreshold * 100, 1, MidpointRounding.AwayFromZero);
        var expectedRisk = Math.Max(0m, input.IncomingSeverity - ExpectedHits(input.ResistancePool));

        return new RunnerRiskEstimate(
            input.Label,
            input.FinalSeverityThreshold,
            chancePercent,
            decimal.Round(expectedRisk, 2, MidpointRounding.AwayFromZero),
            input.Boundary);
    }

    private static RunnerBenchmarkResult BuildBenchmark(RunnerIntelligenceInput input, RunnerBenchmark benchmark)
    {
        var value = input.Stats.GetValueOrDefault(benchmark.StatKey);
        var percentile = PercentileRank(value, benchmark.CohortValues);
        var posture = percentile switch
        {
            >= 97 => "top 3%",
            >= 90 => "top 10%",
            >= 75 => "strong",
            >= 50 => "above median",
            >= 25 => "below median",
            _ => "exposed"
        };

        return new RunnerBenchmarkResult(
            benchmark.StatKey,
            benchmark.Label,
            value,
            percentile,
            posture,
            benchmark.CohortLabel);
    }

    private RunnerWhatIfResult BuildWhatIf(RunnerIntelligenceInput input, RunnerWhatIfEffect effect)
    {
        var currentValue = input.Stats.GetValueOrDefault(effect.TargetStatKey);
        var projectedValue = currentValue + effect.StatDelta;
        var baseline = input.Benchmarks.FirstOrDefault(item => item.StatKey == effect.TargetStatKey);
        var baselinePercentile = baseline is null ? 0 : PercentileRank(currentValue, baseline.CohortValues);
        var projectedPercentile = baseline is null ? 0 : PercentileRank(projectedValue, baseline.CohortValues);
        var risk = CalculateRisk(new RunnerRiskInput(
            effect.ResistancePool,
            effect.RiskThreshold,
            effect.RiskSeverity,
            effect.RiskLabel,
            effect.RiskBoundary));

        return new RunnerWhatIfResult(
            effect.Id,
            effect.Label,
            effect.TargetStatKey,
            currentValue,
            projectedValue,
            baselinePercentile,
            projectedPercentile,
            projectedPercentile - baselinePercentile,
            risk,
            effect.InventoryItemKeys.Where(input.Inventory.Contains).ToArray(),
            effect.Notes.ToArray());
    }

    private static IEnumerable<RunnerOpportunityCard> BuildOpportunities(
        RunnerIntelligenceInput input,
        IReadOnlyList<RunnerBenchmarkResult> benchmarks,
        IReadOnlyList<RunnerWhatIfResult> whatIfCards)
    {
        foreach (var benchmark in benchmarks.Where(item => item.Percentile < 25))
        {
            yield return new RunnerOpportunityCard(
                "exposure",
                $"Exposure: {benchmark.Label}",
                $"{benchmark.Label} is {benchmark.Posture} for {benchmark.CohortLabel}.");
        }

        foreach (var card in whatIfCards.OrderByDescending(item => item.PercentileDelta).Take(3))
        {
            yield return new RunnerOpportunityCard(
                "prep",
                $"Prep: {card.Label}",
                $"Changes {card.TargetStatKey} from percentile {card.BaselinePercentile:0.#} to {card.ProjectedPercentile:0.#}.");
        }

        if (input.Privacy.HostedCohortOptIn is false)
        {
            yield return new RunnerOpportunityCard(
                "privacy",
                "Privacy: local benchmark mode",
                "Hosted cohort export is off; use local roster and campaign benchmarks only.");
        }
    }

    public static double PercentileRank(decimal value, IReadOnlyCollection<decimal> cohortValues)
    {
        if (cohortValues.Count == 0)
        {
            return 0;
        }

        var lower = cohortValues.Count(item => item < value);
        var equal = cohortValues.Count(item => item == value);
        return Math.Round(((lower + (equal * 0.5d)) / cohortValues.Count) * 100d, 1, MidpointRounding.AwayFromZero);
    }

    public static double ChanceFinalSeverityAtOrBelow(int dicePool, decimal incomingSeverity, int finalSeverityThreshold)
    {
        var requiredHits = (int)Math.Ceiling(Math.Max(0m, incomingSeverity - finalSeverityThreshold));
        return ChanceHitsAtOrAbove(dicePool, requiredHits);
    }

    public static double ChanceHitsAtOrAbove(int dicePool, int hitThreshold)
    {
        if (hitThreshold <= 0)
        {
            return 1;
        }

        if (dicePool <= 0 || hitThreshold > dicePool)
        {
            return 0;
        }

        return 1d - ChanceHitsAtOrBelow(dicePool, hitThreshold - 1);
    }

    public static double ChanceHitsAtOrBelow(int dicePool, int hitThreshold)
    {
        if (dicePool <= 0)
        {
            return hitThreshold >= 0 ? 1 : 0;
        }

        var probability = 0d;
        for (var hits = 0; hits <= Math.Min(hitThreshold, dicePool); hits++)
        {
            probability += Binomial(dicePool, hits) * Math.Pow(1d / 3d, hits) * Math.Pow(2d / 3d, dicePool - hits);
        }

        return probability;
    }

    private static decimal ExpectedHits(int dicePool) => dicePool / 3m;

    private static double Binomial(int n, int k)
    {
        if (k < 0 || k > n)
        {
            return 0;
        }

        if (k == 0 || k == n)
        {
            return 1;
        }

        k = Math.Min(k, n - k);
        var result = 1d;
        for (var i = 1; i <= k; i++)
        {
            result *= n - (k - i);
            result /= i;
        }

        return result;
    }
}

public sealed record RunnerIntelligenceScenario(
    string Id,
    string Label,
    string Summary,
    RunnerIntelligenceInput Input);

public sealed record RunnerIntelligenceInput(
    string RunnerId,
    string Ruleset,
    string CohortLabel,
    IReadOnlyDictionary<string, decimal> Stats,
    IReadOnlySet<string> Inventory,
    IReadOnlyList<RunnerBenchmark> Benchmarks,
    IReadOnlyList<RunnerWhatIfEffect> WhatIfEffects,
    RunnerIntelligencePrivacy Privacy);

public sealed record RunnerBenchmark(
    string StatKey,
    string Label,
    string CohortLabel,
    IReadOnlyCollection<decimal> CohortValues);

public sealed record RunnerWhatIfEffect(
    string Id,
    string Label,
    string TargetStatKey,
    decimal StatDelta,
    IReadOnlyList<string> InventoryItemKeys,
    int ResistancePool,
    int RiskThreshold,
    decimal RiskSeverity,
    string RiskLabel,
    string RiskBoundary,
    IReadOnlyList<string> Notes);

public sealed record RunnerRiskInput(
    int ResistancePool,
    int FinalSeverityThreshold,
    decimal IncomingSeverity,
    string Label,
    string Boundary);

public sealed record RunnerIntelligencePrivacy(
    bool HostedCohortOptIn,
    bool SelfHostLocalOnly,
    IReadOnlyList<string> ExcludedFields)
{
    public static RunnerIntelligencePrivacy LocalOnly { get; } = new(
        false,
        true,
        new ReadOnlyCollection<string>(
            new[]
            {
                "character names",
                "aliases",
                "owner ids",
                "workspace ids",
                "file names",
                "document contents",
                "XML",
                "notes",
                "dossier text"
            }));
}

public sealed record RunnerIntelligenceReport(
    string RunnerId,
    string Ruleset,
    string CohortLabel,
    RunnerIntelligencePrivacy Privacy,
    IReadOnlyList<RunnerBenchmarkResult> Benchmarks,
    IReadOnlyList<RunnerWhatIfResult> WhatIfCards,
    IReadOnlyList<RunnerOpportunityCard> Opportunities);

public sealed record RunnerBenchmarkResult(
    string StatKey,
    string Label,
    decimal Value,
    double Percentile,
    string Posture,
    string CohortLabel);

public sealed record RunnerWhatIfResult(
    string Id,
    string Label,
    string TargetStatKey,
    decimal BaselineValue,
    decimal ProjectedValue,
    double BaselinePercentile,
    double ProjectedPercentile,
    double PercentileDelta,
    RunnerRiskEstimate Risk,
    IReadOnlyList<string> AvailableInventoryKeys,
    IReadOnlyList<string> Notes);

public sealed record RunnerRiskEstimate(
    string Label,
    int Threshold,
    double ChanceAtOrBelowThresholdPercent,
    decimal ExpectedUnresistedSeverity,
    string Boundary)
{
    public static RunnerRiskEstimate None { get; } = new(
        "none",
        0,
        100,
        0,
        "No risk model was supplied for this what-if effect.");
}

public sealed record RunnerOpportunityCard(
    string Kind,
    string Title,
    string Body);
