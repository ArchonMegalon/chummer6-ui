using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chummer.Presentation.CharacterStatistics;

/// <summary>
/// Deterministic placeholder calculator used until rules-backed statistics are implemented.
/// It preserves the shared service boundary without allowing UI layers to invent calculations.
/// </summary>
public sealed class PendingCharacterStatisticsCalculator : ICharacterStatisticsCalculator
{
    public ValueTask<CharacterStatisticsResult> CalculateAsync(
        CharacterStatisticsSnapshot snapshot,
        CharacterStatisticsCalculationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var metric = new CharacterMetricResult(
            MetricKey: options.GoalKey ?? snapshot.RecommendationGoalKey ?? "general-readiness",
            PercentileBand: CharacterPercentileBand.Unknown,
            CohortKey: options.CohortKeyOverride ?? snapshot.ComparisonCohortKey,
            SampleSizeBand: CharacterCohortSampleSizeBand.Unknown,
            RulesetKey: snapshot.RulesetKey,
            IsStale: false,
            Evidence:
            [
                new CharacterStatisticsEvidenceLine(
                    EvidenceId: "pending-shared-calculation",
                    SourceId: "shared-statistics-placeholder",
                    FormulaId: "not-yet-implemented",
                    InputRefId: snapshot.CharacterKey,
                    Summary: "Shared statistics calculation is pending; UI must not invent percentile or probability values.")
            ]);

        var result = new CharacterStatisticsResult(
            ResultId: $"pending:{snapshot.CharacterKey}:{metric.MetricKey}",
            ResultState: CharacterStatisticsResultState.Pending,
            MetricResults: [metric],
            Recommendations: Array.Empty<CharacterStatisticsRecommendation>(),
            RiskAssessments: Array.Empty<CharacterStatisticsRiskAssessment>(),
            PrivacyLevel: CharacterStatisticsPrivacyLevel.LocalOnly,
            CalculatedAtUtc: DateTimeOffset.UtcNow);

        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// Deterministic placeholder cohort provider for UI integration before cohort storage exists.
/// </summary>
public sealed class PendingCharacterStatisticsCohortProvider : ICharacterStatisticsCohortProvider
{
    public ValueTask<CharacterStatisticsCohortDescriptor> DescribeCohortAsync(
        string cohortKey,
        string rulesetKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = new CharacterStatisticsCohortDescriptor(
            CohortKey: string.IsNullOrWhiteSpace(cohortKey) ? "unknown" : cohortKey,
            RulesetKey: string.IsNullOrWhiteSpace(rulesetKey) ? "unknown" : rulesetKey,
            SampleSizeBand: CharacterCohortSampleSizeBand.Unknown,
            PrivacySummary: "No cohort data has been loaded; exact percentiles are not available.",
            AllowsExactPercentile: false,
            IsStale: false);

        return ValueTask.FromResult(descriptor);
    }
}
