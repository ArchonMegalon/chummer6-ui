using Chummer.Application.Owners;

namespace Chummer.Presentation;

/// <summary>
/// Presentation-facing sync feedback. Published only after the owner fence passes,
/// never while holding the state-file lease, and never carrying raw server errors.
/// </summary>
public sealed record WorkspaceSyncFeedback(OwnerContextStamp Stamp, string Message);

/// <summary>
/// Presentation subscribes to sync feedback and reads the current value for initial
/// surface composition.
/// </summary>
public interface IWorkspaceSyncFeedbackSource
{
    WorkspaceSyncFeedback? CurrentWorkspaceSyncFeedback { get; }

    event Action<WorkspaceSyncFeedback>? WorkspaceSyncFeedbackChanged;
}
