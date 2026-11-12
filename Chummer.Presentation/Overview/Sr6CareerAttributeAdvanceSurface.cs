using Chummer.Application.Owners;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using System.Text.Json.Serialization;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Editor-level snapshot for the SR6 attribute advancement surface: Core quotes for
/// the dossier's career attributes plus the revision context the Core service
/// validates on Advance/Lookup.
/// </summary>
public sealed record Sr6CareerAttributeAdvanceEditorSnapshot(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    string RulesetId,
    IReadOnlyList<Sr6CareerAttributeQuote> Attributes,
    int OmittedAttributeCount,
    OwnerContextStamp Owner);

/// <summary>
/// Typed dispatch outcome for one confirmed SR6 advancement. <see cref="CommitResolved"/>
/// means the durable receipt was observed directly (Applied) or recovered through the
/// read-only Lookup replay of the exact submitted command. A dispatch that may have
/// committed but was not observed keeps <see cref="CommitResolved"/> false and carries
/// the exact command so the caller can recover it - never a new purchase.
/// </summary>
public sealed record Sr6CareerAttributeAdvanceDispatchResult(
    Sr6CareerAttributeOutcome Outcome,
    Sr6CareerAttributeReceipt? Receipt,
    Sr6CareerAttributeCommand Command,
    bool CommitResolved,
    string? Error);

/// <summary>
/// Durable intent, written before dispatch. Owner identity survives restart; live
/// authorization deliberately does not. EntryId fences stale clears, including an
/// attempted reuse of the same operation. CommandDigest binds the entire command.
/// </summary>
public sealed record Sr6CareerAttributePendingRecovery(
    Guid EntryId,
    string OriginalOwnerId,
    bool OriginalOwnerIsLocalSingleUser,
    CharacterWorkspaceId WorkspaceId,
    Sr6CareerAttributeCommand Command,
    string CommandDigest)
{
    public bool BelongsTo(OwnerScope owner, CharacterWorkspaceId workspaceId)
        => OriginalOwnerId == owner.NormalizedValue
            && OriginalOwnerIsLocalSingleUser == owner.IsLocalSingleUser
            && WorkspaceId == workspaceId;

    [JsonIgnore]
    public bool IsValid => EntryId != Guid.Empty
        && !string.IsNullOrWhiteSpace(OriginalOwnerId)
        && OriginalOwnerId == new OwnerScope(OriginalOwnerId).NormalizedValue
        && (!OriginalOwnerIsLocalSingleUser || OriginalOwnerId == OwnerScope.LocalSingleUser.NormalizedValue)
        && Sr6CareerAttributeIntegrity.IsConfirmed(Command)
        && Command.Quote.OwnerId == OriginalOwnerId
        && Command.Quote.WorkspaceId == WorkspaceId
        && CommandDigest == Sr6CareerAttributeIntegrity.Digest(Command);

    public bool MatchesReceipt(Sr6CareerAttributeReceipt? receipt)
        => IsValid && receipt is not null
            && Sr6CareerAttributeIntegrity.IsConfirmed(receipt.Command)
            && Sr6CareerAttributeIntegrity.Digest(receipt.Command) == CommandDigest
            && receipt.ReceiptDigest == Sr6CareerAttributeIntegrity.ReceiptDigest(receipt);
}

/// <summary>
/// Per-owner/workspace pending map. False means unavailable/corrupt or a failed
/// compare-and-swap, never permission to dispatch. No in-memory fallback is safe.
/// </summary>
public interface ISr6CareerAttributeRecoveryStore
{
    bool TryRead(OwnerScope owner, CharacterWorkspaceId workspaceId, out Sr6CareerAttributePendingRecovery? pending);
    bool TryAdd(Sr6CareerAttributePendingRecovery pending);
    bool TryClear(Sr6CareerAttributePendingRecovery expected);
}

public sealed class UnavailableSr6CareerAttributeRecoveryStore : ISr6CareerAttributeRecoveryStore
{
    public bool TryRead(OwnerScope owner, CharacterWorkspaceId workspaceId, out Sr6CareerAttributePendingRecovery? pending)
    {
        pending = null;
        return false;
    }
    public bool TryAdd(Sr6CareerAttributePendingRecovery pending) => false;
    public bool TryClear(Sr6CareerAttributePendingRecovery expected) => false;
}

public interface ISr6CareerAttributeAdvancementClient
{
    Task<Sr6CareerAttributeAdvanceEditorSnapshot?> QuoteSr6CareerAttributeAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
        => Task.FromResult<Sr6CareerAttributeAdvanceEditorSnapshot?>(null);

    /// <summary>
    /// Dispatches one confirmed advancement under the exact owner authority the quote
    /// was prepared with. Callers must carry that stamp (never recapture Current) so
    /// an A→B→A owner transition cannot authorize a stale dialog with new authority.
    /// </summary>
    Task<Sr6CareerAttributeAdvanceDispatchResult?> AdvanceSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => Task.FromResult<Sr6CareerAttributeAdvanceDispatchResult?>(null);

    /// <summary>
    /// Read-only replay probe for an already-submitted operation. Never purchases;
    /// resolves only the exact command's durable receipt.
    /// </summary>
    Task<Sr6CareerAttributeReceipt?> LookupSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CancellationToken ct)
        => Task.FromResult<Sr6CareerAttributeReceipt?>(null);
}
