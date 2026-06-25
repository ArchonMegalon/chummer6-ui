using System.Collections.Generic;
using Chummer.Presentation.RunnerIntelligence;

namespace Chummer.Desktop.Runtime.RunnerIntelligence;

public sealed class DesktopRunnerIntelligenceBridge
{
    private readonly IRunnerIntelligenceCalculator _calculator;
    private readonly IRunnerIntelligenceScenarioCatalog _scenarioCatalog;

    public DesktopRunnerIntelligenceBridge()
        : this(new RunnerIntelligenceCalculator(), new RunnerIntelligenceScenarioCatalog())
    {
    }

    public DesktopRunnerIntelligenceBridge(
        IRunnerIntelligenceCalculator calculator,
        IRunnerIntelligenceScenarioCatalog scenarioCatalog)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _scenarioCatalog = scenarioCatalog ?? throw new ArgumentNullException(nameof(scenarioCatalog));
    }

    public RunnerIntelligenceReport Calculate(RunnerIntelligenceInput input)
        => _calculator.Calculate(input);

    public RunnerIntelligenceReport Calculate(RunnerIntelligenceScenario scenario)
        => _calculator.Calculate(scenario);

    public double CalculatePercentileRank(decimal value, IReadOnlyCollection<decimal> cohortValues)
        => _calculator.CalculatePercentileRank(value, cohortValues);

    public RunnerRiskEstimate CalculateRisk(RunnerRiskInput input)
        => _calculator.CalculateRisk(input);

    public RunnerIntelligenceScenario BuildIncreaseInitiativeScenario(
        string runnerId = RunnerIntelligenceSampleFactory.DefaultRunnerId,
        string ruleset = RunnerIntelligenceSampleFactory.DefaultRuleset,
        string cohortLabel = RunnerIntelligenceSampleFactory.DefaultCohortLabel)
        => _scenarioCatalog.BuildIncreaseInitiativeScenario(runnerId, ruleset, cohortLabel);

    public RunnerIntelligenceReport CalculateIncreaseInitiativeSample()
        => _calculator.Calculate(BuildIncreaseInitiativeScenario());
}
