using System;
using System.Collections.Generic;

namespace Chummer.Presentation.CharacterStatistics;

/// <summary>
/// Privacy-safe snapshot consumed by shared character statistics calculations.
/// UI projects should build this from their current character state and render the shared result.
/// </summary>
public sealed record CharacterStatisticsSnapshot(
    string CharacterKey,
    string RulesetKey,
    CharacterStatisticsAttributeSummary Attributes,
    IReadOnlyList<CharacterStatisticsInputRef> Skills,
    IReadOnlyList<CharacterStatisticsInputRef> Qualities,
    IReadOnlyList<CharacterStatisticsInputRef> Spells,
    IReadOnlyList<CharacterStatisticsInputRef> Inventory,
    string BuildContextKey,
    string ComparisonCohortKey,
    string? RecommendationGoalKey = null);

public sealed record CharacterStatisticsAttributeSummary(
    int Body,
    int Agility,
    int Reaction,
    int Strength,
    int Charisma,
    int Intuition,
    int Logic,
    int Willpower,
    int Edge,
    int Initiative,
    int InitiativeDice);

public sealed record CharacterStatisticsInputRef(
    string InputId,
    string InputKind,
    int? Rating = null,
    decimal? Quantity = null,
    bool IsAvailable = true);

public sealed record CharacterStatisticsResult(
    string ResultId,
    CharacterStatisticsResultState ResultState,
    IReadOnlyList<CharacterMetricResult> MetricResults,
    IReadOnlyList<CharacterStatisticsRecommendation> Recommendations,
    IReadOnlyList<CharacterStatisticsRiskAssessment> RiskAssessments,
    CharacterStatisticsPrivacyLevel PrivacyLevel,
    DateTimeOffset CalculatedAtUtc);

public sealed record CharacterMetricResult(
    string MetricKey,
    CharacterPercentileBand PercentileBand,
    string CohortKey,
    CharacterCohortSampleSizeBand SampleSizeBand,
    string RulesetKey,
    bool IsStale,
    IReadOnlyList<CharacterStatisticsEvidenceLine> Evidence);

public sealed record CharacterStatisticsRecommendation(
    string RecommendationId,
    string GoalKey,
    string ActionType,
    string ExpectedEffectSummary,
    string RiskSummary,
    string ProbabilityStatement,
    IReadOnlyList<CharacterStatisticsInputRef> RequiredInputs,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<CharacterStatisticsEvidenceLine> Evidence);

public sealed record CharacterStatisticsRiskAssessment(
    string RiskModelKey,
    string ThresholdKey,
    string ProbabilityBandKey,
    decimal? Probability,
    string ConsequenceType,
    CharacterStatisticsConfidenceState ConfidenceState,
    IReadOnlyList<string> InputIds,
    IReadOnlyList<CharacterStatisticsEvidenceLine> Evidence);

public sealed record CharacterStatisticsEvidenceLine(
    string EvidenceId,
    string SourceId,
    string FormulaId,
    string InputRefId,
    string Summary);

public enum CharacterStatisticsResultState
{
    Pending,
    Ready,
    Stale,
    InsufficientData,
    Error
}

public enum CharacterPercentileBand
{
    Unknown,
    BelowAverage,
    Average,
    AboveAverage,
    Top10,
    Top3,
    Top1
}

public enum CharacterCohortSampleSizeBand
{
    Unknown,
    Small,
    Medium,
    Large
}

public enum CharacterStatisticsPrivacyLevel
{
    LocalOnly,
    Anonymized,
    Publishable
}

public enum CharacterStatisticsConfidenceState
{
    Exact,
    Estimated,
    InsufficientData,
    Blocked
}
