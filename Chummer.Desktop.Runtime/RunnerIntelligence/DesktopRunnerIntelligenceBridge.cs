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

    public RunnerIntelligenceReport CalculateIncreaseInitiativeSample()
        => _calculator.Calculate(_scenarioCatalog.BuildIncreaseInitiativeScenario());
}
