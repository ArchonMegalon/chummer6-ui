namespace Chummer.Presentation.Creation;

/// <summary>Ephemeral analysis status. Never part of a request, proposal or authority digest.</summary>
public enum CompletionAnalysisStage
{
    LoadingChoices,
    Prerequisites,
    Archetype,
    PreparingChoices,
    Skills,
    Magic,
    Attributes,
    Contacts,
    RulesAndBudgets,
    Equipment,
    Karma,
    FinalBudgets,
    PreparingReview,
    StartingCash,
    FinalReview
}

/// <summary>Counts completed analysis phases, not time or within-phase work.
/// Several current-stage labels can belong to one phase, especially resource
/// analysis. A completed analysis is not a successful rules/admission result.</summary>
public sealed record CompletionAnalysisProgress(
    CompletionAnalysisStage Stage, int CompletedStages, int TotalStages)
{
    public const int MissingOnlyStageCount = 7;
}

internal sealed class CompletionAnalysisReporter(
    Action<CompletionAnalysisProgress>? report, int totalStages, CancellationToken cancellationToken)
{
    private int _completed;
    private CompletionAnalysisStage _stage;

    public void Start(CompletionAnalysisStage stage)
    {
        _stage = stage;
        Report();
    }

    // Call only after the corresponding real work returned, never in finally.
    public void Complete()
    {
        cancellationToken.ThrowIfCancellationRequested();
        _completed++;
        Report();
    }

    private void Report()
    {
        cancellationToken.ThrowIfCancellationRequested();
        report?.Invoke(new(_stage, _completed, totalStages));
        cancellationToken.ThrowIfCancellationRequested();
    }
}
