using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WorkspaceConflictState
{
    public WorkspaceConflictState(
        string Operation,
        long ExpectedContentRevision,
        long? ActualContentRevision,
        string Message)
    {
        if (string.IsNullOrWhiteSpace(Operation))
            throw new ArgumentException("A workspace conflict operation is required.", nameof(Operation));
        if (ExpectedContentRevision <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedContentRevision), "Expected content revision must be positive.");
        if (ActualContentRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(ActualContentRevision), "Actual content revision must be positive when known.");
        if (string.IsNullOrWhiteSpace(Message))
            throw new ArgumentException("A workspace conflict message is required.", nameof(Message));

        this.Operation = Operation.Trim();
        this.ExpectedContentRevision = ExpectedContentRevision;
        this.ActualContentRevision = ActualContentRevision;
        this.Message = Message.Trim();
    }

    public string Operation { get; init; }

    public long ExpectedContentRevision { get; init; }

    public long? ActualContentRevision { get; init; }

    public string Message { get; init; }

    public WorkspaceOperationOutcome Outcome => WorkspaceOperationOutcome.Conflict;
}

public sealed record OpenWorkspaceState(
    CharacterWorkspaceId Id,
    string Name,
    string Alias,
    DateTimeOffset LastOpenedUtc,
    string RulesetId,
    long ContentRevision = 0,
    long SavedRevision = 0,
    WorkspaceConflictState? ConflictState = null)
{
    public bool IsDirty => ContentRevision != SavedRevision;

    public bool HasSavedWorkspace => SavedRevision > 0;

    public OpenWorkspaceState(
        CharacterWorkspaceId Id,
        string Name,
        string Alias,
        DateTimeOffset LastOpenedUtc,
        string RulesetId,
        bool HasSavedWorkspace)
        : this(
            Id,
            Name,
            Alias,
            LastOpenedUtc,
            RulesetId,
            ContentRevision: HasSavedWorkspace ? 1 : 0,
            SavedRevision: HasSavedWorkspace ? 1 : 0,
            ConflictState: null)
    {
    }
}
