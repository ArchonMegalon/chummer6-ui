using System.Text.Json.Serialization;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Error text of the definite pre-dispatch refusal emitted when the recovery store
/// cannot durably record the exact replacement bytes before a reward dispatch. Both
/// the mutation pipeline (which returns it) and the caller (which classifies it)
/// reference this exact string; it must never drift.
/// </summary>
public static class CareerRewardRecoveryMessages
{
    public const string PreDispatchRecoveryRefusal =
        "The reward could not be recorded for recovery before sending. Nothing was changed.";
}

/// <summary>
/// Durable intent for one career reward (karma or nuyen) dispatch, written BEFORE the
/// operation is sent. Owner identity survives restart; live authorization deliberately
/// does not — recovery always recaptures a fresh owner context and verifies
/// <see cref="BelongsTo"/> against the durable identity. EntryId fences stale clears,
/// including an attempted reuse of the same operation. A confirmed outcome is marked
/// <see cref="Resolved"/> rather than cleared, so a crash before the user's explicit
/// acknowledgment cannot lose the confirmed result.
/// </summary>
public sealed record CareerRewardPendingRecovery(
    Guid EntryId,
    string OriginalOwnerId,
    bool OriginalOwnerIsLocalSingleUser,
    CharacterWorkspaceId WorkspaceId,
    Guid OperationId,
    long ExpectedRevision,
    WorkspaceDocumentFormat? ReplacementFormat,
    string? ReplacementRulesetId,
    string? ReplacementContent,
    bool Resolved = false)
{
    public bool BelongsTo(OwnerScope owner, CharacterWorkspaceId workspaceId)
        => OriginalOwnerId == owner.NormalizedValue
            && OriginalOwnerIsLocalSingleUser == owner.IsLocalSingleUser
            && WorkspaceId == workspaceId;

    /// <summary>True only once the exact next-revision payload has been recorded.</summary>
    [JsonIgnore]
    public bool HasReplacement => ReplacementContent is not null;

    /// <summary>
    /// True once recovery attributed the original dispatch to an exact committed
    /// payload. A resolved entry retains its recorded proof; only the explicit
    /// acknowledgment of this exact operation may clear it.
    /// </summary>
    [JsonIgnore]
    public bool IsResolved => Resolved;

    [JsonIgnore]
    public bool IsValid => EntryId != Guid.Empty
        && !string.IsNullOrWhiteSpace(OriginalOwnerId)
        && !string.IsNullOrWhiteSpace(WorkspaceId.Value)
        && OperationId != Guid.Empty
        && ExpectedRevision > 0
        && (ReplacementContent is null
            || (ReplacementFormat is not null && ReplacementRulesetId is not null));
}

public interface ICareerRewardRecoveryStore
{
    bool TryRead(OwnerScope owner, CharacterWorkspaceId workspaceId, out CareerRewardPendingRecovery? pending);
    bool TryAdd(CareerRewardPendingRecovery pending);
    bool TryRecordReplacement(Guid entryId, WorkspaceDocumentFormat replacementFormat, string replacementRulesetId, string replacementContent);
    /// <summary>Marks a replacement-bearing pending intent as confirmed-committed. The resolved entry is cleared only by TryClear from the explicit acknowledgment of this exact operation.</summary>
    bool TryMarkResolved(Guid entryId, Guid operationId);
    bool TryClear(CareerRewardPendingRecovery expected);
}

/// <summary>
/// Per-owner/workspace pending map. False means unavailable/corrupt or a failed
/// compare-and-swap, never permission to dispatch. No in-memory fallback is safe.
/// </summary>
public sealed class UnavailableCareerRewardRecoveryStore : ICareerRewardRecoveryStore
{
    public bool TryRead(OwnerScope owner, CharacterWorkspaceId workspaceId, out CareerRewardPendingRecovery? pending)
    {
        pending = null;
        return false;
    }
    public bool TryAdd(CareerRewardPendingRecovery pending) => false;
    public bool TryRecordReplacement(Guid entryId, WorkspaceDocumentFormat replacementFormat, string replacementRulesetId, string replacementContent) => false;
    public bool TryMarkResolved(Guid entryId, Guid operationId) => false;
    public bool TryClear(CareerRewardPendingRecovery expected) => false;
}
