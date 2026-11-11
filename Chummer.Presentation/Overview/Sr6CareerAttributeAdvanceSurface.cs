using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

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
/// Client surface for Core's <see cref="Chummer.Application.Characters.ISr6CareerAttributeAdvancementService"/>.
/// Default implementations return null so compatibility clients (HTTP mode) degrade
/// honestly; the local in-process client implements them against Core's owner-bound service.
/// </summary>
/// <summary>
/// Retained state for an SR6 advancement whose commit was not observed.
/// Carries the exact submitted command so a later session can run
/// Lookup-only recovery (never a new purchase) before any further
/// advancement is allowed for the runner.
/// </summary>
public sealed record Sr6CareerAttributePendingRecovery(
    CharacterWorkspaceId WorkspaceId,
    Sr6CareerAttributeCommand Command,
    Sr6CareerAttributeOutcome Outcome,
    string? Error);

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
