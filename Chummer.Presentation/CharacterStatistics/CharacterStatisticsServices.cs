using System.Threading;
using System.Threading.Tasks;

namespace Chummer.Presentation.CharacterStatistics;

/// <summary>
/// Shared character statistics calculation entry point. UI layers call this interface and render the returned DTOs.
/// Implementations must live outside Blazor/Avalonia UI projects.
/// </summary>
public interface ICharacterStatisticsCalculator
{
    ValueTask<CharacterStatisticsResult> CalculateAsync(
        CharacterStatisticsSnapshot snapshot,
        CharacterStatisticsCalculationOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides anonymized comparison/cohort posture for percentile calculations.
/// </summary>
public interface ICharacterStatisticsCohortProvider
{
    ValueTask<CharacterStatisticsCohortDescriptor> DescribeCohortAsync(
        string cohortKey,
        string rulesetKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Produces explainable recommendations from shared statistics results.
/// </summary>
public interface ICharacterStatisticsRecommendationEngine
{
    ValueTask<CharacterStatisticsResult> AddRecommendationsAsync(
        CharacterStatisticsSnapshot snapshot,
        CharacterStatisticsResult baseResult,
        CharacterStatisticsCalculationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record CharacterStatisticsCalculationOptions(
    bool IncludeRecommendations = true,
    bool IncludeRiskAssessments = true,
    bool AllowExactPercentile = false,
    string? GoalKey = null,
    string? CohortKeyOverride = null);

public sealed record CharacterStatisticsCohortDescriptor(
    string CohortKey,
    string RulesetKey,
    CharacterCohortSampleSizeBand SampleSizeBand,
    string PrivacySummary,
    bool AllowsExactPercentile,
    bool IsStale);
