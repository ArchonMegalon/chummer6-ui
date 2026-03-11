using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Shell;

public sealed class NullEngineEvaluator : IEngineEvaluator
{
    public ValueTask<RulesetCapabilityInvocationResult> EvaluateAsync(
        RulesetCapabilityInvocationRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult(new RulesetCapabilityInvocationResult(
            Success: false,
            Output: null,
            Diagnostics:
            [
                new RulesetCapabilityDiagnostic(
                    Code: RulesetCapabilityDiagnosticCodes.EngineUnavailable,
                    Message: RulesetCapabilityDiagnosticCodes.EngineUnavailable,
                    Severity: RulesetCapabilityDiagnosticSeverities.Warning)
            ],
            Explain: null));
    }
}
