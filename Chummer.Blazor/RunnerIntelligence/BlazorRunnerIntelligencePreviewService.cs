using Chummer.Presentation.RunnerIntelligence;

namespace Chummer.Blazor.RunnerIntelligence;

public sealed class BlazorRunnerIntelligencePreviewService
{
    private readonly IRunnerIntelligenceCalculator _calculator;
    private readonly IRunnerIntelligenceScenarioCatalog _scenarioCatalog;

    public BlazorRunnerIntelligencePreviewService(
        IRunnerIntelligenceCalculator calculator,
        IRunnerIntelligenceScenarioCatalog scenarioCatalog)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _scenarioCatalog = scenarioCatalog ?? throw new ArgumentNullException(nameof(scenarioCatalog));
    }

    public RunnerIntelligenceReport BuildIncreaseInitiativePreview()
        => _calculator.Calculate(_scenarioCatalog.BuildIncreaseInitiativeScenario());
}
