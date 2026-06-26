using System;
using System.Collections.Generic;
using System.Linq;

namespace Chummer.Presentation.CharacterStatistics;

public sealed record CharacterStatisticsProjection(
    string ResultId,
    string StateLabel,
    string PrivacyLabel,
    IReadOnlyList<CharacterMetricProjection> Metrics,
    IReadOnlyList<CharacterRecommendationProjection> Recommendations,
    IReadOnlyList<CharacterRiskProjection> Risks,
    DateTimeOffset CalculatedAtUtc)
{
    public static CharacterStatisticsProjection FromResult(CharacterStatisticsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new CharacterStatisticsProjection(
            ResultId: result.ResultId,
            StateLabel: FormatResultState(result.ResultState),
            PrivacyLabel: FormatPrivacyLevel(result.PrivacyLevel),
            Metrics: result.MetricResults.Select(CharacterMetricProjection.FromMetric).ToArray(),
            Recommendations: result.Recommendations.Select(CharacterRecommendationProjection.FromRecommendation).ToArray(),
            Risks: result.RiskAssessments.Select(CharacterRiskProjection.FromRisk).ToArray(),
            CalculatedAtUtc: result.CalculatedAtUtc);
    }

    private static string FormatResultState(CharacterStatisticsResultState state) => state switch
    {
        CharacterStatisticsResultState.Pending => "Pending shared calculation",
        CharacterStatisticsResultState.Ready => "Ready",
        CharacterStatisticsResultState.Stale => "Stale",
        CharacterStatisticsResultState.InsufficientData => "Insufficient data",
        CharacterStatisticsResultState.Error => "Error",
        _ => "Unknown"
    };

    private static string FormatPrivacyLevel(CharacterStatisticsPrivacyLevel privacyLevel) => privacyLevel switch
    {
        CharacterStatisticsPrivacyLevel.LocalOnly => "Local only",
        CharacterStatisticsPrivacyLevel.Anonymized => "Anonymized",
        CharacterStatisticsPrivacyLevel.Publishable => "Publishable",
        _ => "Unknown"
    };
}

public sealed record CharacterMetricProjection(
    string MetricKey,
    string PercentileLabel,
    string CohortKey,
    string SampleSizeLabel,
    string RulesetKey,
    bool IsStale,
    IReadOnlyList<string> EvidenceSummaries)
{
    public static CharacterMetricProjection FromMetric(CharacterMetricResult metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        return new CharacterMetricProjection(
            MetricKey: metric.MetricKey,
            PercentileLabel: FormatPercentileBand(metric.PercentileBand),
            CohortKey: metric.CohortKey,
            SampleSizeLabel: FormatSampleSize(metric.SampleSizeBand),
            RulesetKey: metric.RulesetKey,
            IsStale: metric.IsStale,
            EvidenceSummaries: metric.Evidence.Select(evidence => evidence.Summary).ToArray());
    }

    private static string FormatPercentileBand(CharacterPercentileBand percentileBand) => percentileBand switch
    {
        CharacterPercentileBand.Top1 => "Top 1% band",
        CharacterPercentileBand.Top3 => "Top 3% band",
        CharacterPercentileBand.Top10 => "Top 10% band",
        CharacterPercentileBand.AboveAverage => "Above average",
        CharacterPercentileBand.Average => "Average",
        CharacterPercentileBand.BelowAverage => "Below average",
        CharacterPercentileBand.Unknown => "Unknown percentile",
        _ => "Unknown percentile"
    };

    private static string FormatSampleSize(CharacterCohortSampleSizeBand sampleSizeBand) => sampleSizeBand switch
    {
        CharacterCohortSampleSizeBand.Small => "Small cohort",
        CharacterCohortSampleSizeBand.Medium => "Medium cohort",
        CharacterCohortSampleSizeBand.Large => "Large cohort",
        CharacterCohortSampleSizeBand.Unknown => "Unknown cohort size",
        _ => "Unknown cohort size"
    };
}

public sealed record CharacterRecommendationProjection(
    string RecommendationId,
    string GoalKey,
    string ActionType,
    string ExpectedEffectSummary,
    string RiskSummary,
    string ProbabilityStatement,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> EvidenceSummaries)
{
    public static CharacterRecommendationProjection FromRecommendation(CharacterStatisticsRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        return new CharacterRecommendationProjection(
            RecommendationId: recommendation.RecommendationId,
            GoalKey: recommendation.GoalKey,
            ActionType: recommendation.ActionType,
            ExpectedEffectSummary: recommendation.ExpectedEffectSummary,
            RiskSummary: recommendation.RiskSummary,
            ProbabilityStatement: recommendation.ProbabilityStatement,
            Assumptions: recommendation.Assumptions,
            BlockingReasons: recommendation.BlockingReasons,
            EvidenceSummaries: recommendation.Evidence.Select(evidence => evidence.Summary).ToArray());
    }
}

public sealed record CharacterRiskProjection(
    string RiskModelKey,
    string ThresholdKey,
    string ProbabilityLabel,
    string ConsequenceType,
    string ConfidenceLabel,
    IReadOnlyList<string> EvidenceSummaries)
{
    public static CharacterRiskProjection FromRisk(CharacterStatisticsRiskAssessment risk)
    {
        ArgumentNullException.ThrowIfNull(risk);

        return new CharacterRiskProjection(
            RiskModelKey: risk.RiskModelKey,
            ThresholdKey: risk.ThresholdKey,
            ProbabilityLabel: risk.Probability is { } probability ? $"{probability:P0}" : risk.ProbabilityBandKey,
            ConsequenceType: risk.ConsequenceType,
            ConfidenceLabel: FormatConfidence(risk.ConfidenceState),
            EvidenceSummaries: risk.Evidence.Select(evidence => evidence.Summary).ToArray());
    }

    private static string FormatConfidence(CharacterStatisticsConfidenceState confidenceState) => confidenceState switch
    {
        CharacterStatisticsConfidenceState.Exact => "Exact",
        CharacterStatisticsConfidenceState.Estimated => "Estimated",
        CharacterStatisticsConfidenceState.InsufficientData => "Insufficient data",
        CharacterStatisticsConfidenceState.Blocked => "Blocked",
        _ => "Unknown"
    };
}
