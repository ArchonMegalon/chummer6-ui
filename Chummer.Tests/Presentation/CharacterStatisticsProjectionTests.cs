#nullable enable annotations

using System.Threading.Tasks;
using Chummer.Presentation.CharacterStatistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterStatisticsProjectionTests
{
    [TestMethod]
    public async Task PendingCalculator_returns_local_pending_result_without_invented_percentiles()
    {
        var calculator = new PendingCharacterStatisticsCalculator();
        CharacterStatisticsSnapshot snapshot = CreateSnapshot();

        CharacterStatisticsResult result = await calculator.CalculateAsync(
            snapshot,
            new CharacterStatisticsCalculationOptions(
                GoalKey: "improve-initiative",
                CohortKeyOverride: "sr5-local-runners"));

        Assert.AreEqual(CharacterStatisticsResultState.Pending, result.ResultState);
        Assert.AreEqual(CharacterStatisticsPrivacyLevel.LocalOnly, result.PrivacyLevel);
        Assert.AreEqual(1, result.MetricResults.Count);
        Assert.AreEqual(CharacterPercentileBand.Unknown, result.MetricResults[0].PercentileBand);
        Assert.AreEqual(CharacterCohortSampleSizeBand.Unknown, result.MetricResults[0].SampleSizeBand);
        Assert.AreEqual("sr5-local-runners", result.MetricResults[0].CohortKey);
        Assert.AreEqual(0, result.Recommendations.Count);
        Assert.AreEqual(0, result.RiskAssessments.Count);
        StringAssert.Contains(
            result.MetricResults[0].Evidence[0].Summary,
            "UI must not invent percentile or probability values");
    }

    [TestMethod]
    public async Task ProjectionService_formats_pending_result_for_shared_ui_consumers()
    {
        var service = new CharacterStatisticsProjectionService(new PendingCharacterStatisticsCalculator());

        CharacterStatisticsProjection projection = await service.ProjectAsync(
            CreateSnapshot(),
            new CharacterStatisticsCalculationOptions(GoalKey: "survivability"));

        Assert.AreEqual("Pending shared calculation", projection.StateLabel);
        Assert.AreEqual("Local only", projection.PrivacyLabel);
        Assert.AreEqual(1, projection.Metrics.Count);
        Assert.AreEqual("Unknown percentile", projection.Metrics[0].PercentileLabel);
        Assert.AreEqual("Unknown cohort size", projection.Metrics[0].SampleSizeLabel);
        Assert.AreEqual("survivability", projection.Metrics[0].MetricKey);
    }

    private static CharacterStatisticsSnapshot CreateSnapshot() => new(
        CharacterKey: "runner-local-001",
        RulesetKey: "sr5",
        Attributes: new CharacterStatisticsAttributeSummary(
            Body: 3,
            Agility: 4,
            Reaction: 5,
            Strength: 2,
            Charisma: 3,
            Intuition: 4,
            Logic: 3,
            Willpower: 4,
            Edge: 2,
            Initiative: 9,
            InitiativeDice: 1),
        Skills:
        [
            new CharacterStatisticsInputRef("skill-infiltration", "skill", Rating: 4)
        ],
        Qualities: [],
        Spells: [],
        Inventory: [],
        BuildContextKey: "career",
        ComparisonCohortKey: "sr5-local");
}
