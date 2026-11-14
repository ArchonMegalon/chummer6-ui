namespace Chummer.Presentation.Overview;

// Local presentation outcome, not a Core receipt or a durable idempotency key.
public enum CareerRewardApplyStatus { Refused, Confirmed, Uncertain }

public sealed record CareerRewardApplyResult(
    Guid OperationId, CareerRewardApplyStatus Status, string? Message = null)
{
    public const string UncertainMessage = "Recording could not be confirmed. This reward may already be recorded. "
        + "Do not enter it again. Use Check recorded reward to inspect the runner.";
}
