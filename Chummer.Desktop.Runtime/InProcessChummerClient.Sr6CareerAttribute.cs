using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

/// <summary>
/// SR6 career attribute advancement surface. Core's owner-bound service owns the
/// rules, resolves the SR6 authority from the stored document itself, and performs
/// the atomic graph/expense/receipt write directly into the workspace store. The
/// client serializes admission, carries the exact owner authority the quote was
/// prepared with, and projects outcomes without collapsing them to null.
/// </summary>
public sealed partial class InProcessChummerClient : ISr6CareerAttributeAdvancementClient
{
    /// <summary>Canonical SR6 career attributes Core's advancement rules resolve.</summary>
    internal static readonly IReadOnlyList<string> Sr6CareerAttributeIds = new[]
    {
        "Body", "Agility", "Reaction", "Strength",
        "Charisma", "Intuition", "Logic", "Willpower",
    };

    Task<Sr6CareerAttributeAdvanceEditorSnapshot?> ISr6CareerAttributeAdvancementClient.QuoteSr6CareerAttributeAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
        => QuoteSr6CareerAttributeAsync(CaptureOwnerContext(), id, ct);

    private async Task<Sr6CareerAttributeAdvanceEditorSnapshot?> QuoteSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        CharacterWorkspaceId id,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_sr6CareerAttributeAdvancementService is null)
        {
            return null;
        }

        (Sr6CareerAttributeAdvanceEditorSnapshot? Snapshot, int Omitted) quoted =
            await _workspaceOperations.Execute(() =>
        {
            // Ruleset gate first: the SR5 editor keeps its XML-mutation route; only
            // SR6 career runners route through Core's advancement service here.
            CommandResult<WorkspaceDocumentSnapshot> read = WithOwnerLease(
                owner, admittedOwner => _workspaceService.GetWorkspace(admittedOwner, id));
            if (!read.Success || read.Value is null
                || !string.Equals(
                    read.Value.Document.RulesetId,
                    RulesetDefaults.Sr6,
                    StringComparison.Ordinal))
            {
                return (Snapshot: null as Sr6CareerAttributeAdvanceEditorSnapshot, Omitted: 0);
            }

            ISr6CareerAttributeAdvancementService service = _sr6CareerAttributeAdvancementService;
            var quotes = new List<Sr6CareerAttributeQuote>();
            int omitted = 0;
            foreach (string attributeId in Sr6CareerAttributeIds)
            {
                Sr6CareerAttributeResult result = service.Quote(owner, id, attributeId);
                if (result.Outcome == Sr6CareerAttributeOutcome.Available && result.Quote is not null)
                {
                    quotes.Add(result.Quote);
                }
                else
                {
                    // Blocked, unavailable (special-authority attributes), or not
                    // present in this dossier: honestly omitted from the editor.
                    omitted++;
                }
            }

            Sr6CareerAttributeAdvanceEditorSnapshot snapshot = new(
                id,
                read.Value.ContentRevision,
                read.Value.Document.RulesetId,
                quotes.AsReadOnly(),
                omitted,
                owner);
            return (Snapshot: (Sr6CareerAttributeAdvanceEditorSnapshot?)snapshot, Omitted: omitted);
        }, ct).ConfigureAwait(false);
        return quoted.Snapshot;
    }

    Task<Sr6CareerAttributeAdvanceDispatchResult?> ISr6CareerAttributeAdvancementClient.AdvanceSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
        => AdvanceSr6CareerAttributeAsync(owner, command, workspaceId, ct);

    private async Task<Sr6CareerAttributeAdvanceDispatchResult?> AdvanceSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.IsValid)
        {
            throw new InvalidOperationException(
                "The advancement quote's owner authority is invalid. Reopen the dialog so a fresh quote can be prepared.");
        }

        ct.ThrowIfCancellationRequested();
        if (_sr6CareerAttributeAdvancementService is null)
        {
            return null;
        }

        Sr6CareerAttributeResult committed = await _workspaceOperations.ExecuteCommitAsync(
            () =>
            {
                // Core takes its own synchronous owner lease and performs the
                // atomic graph/expense/receipt write. Do not nest a UI lease or
                // carry one across roaming. Once admitted, cancellation cannot
                // hide a commit. The exact quote-borne owner stamp is dispatched;
                // Current is never recaptured here (A -> B -> A would otherwise
                // authorize a stale dialog with new authority).
                Sr6CareerAttributeResult result = _sr6CareerAttributeAdvancementService.Advance(owner, command);
                return result;
            },
            result => result.Outcome is Sr6CareerAttributeOutcome.Applied or Sr6CareerAttributeOutcome.Replayed
                ? SynchronizeOriginalCommittedWorkspaceAsync(owner, workspaceId)
                : Task.CompletedTask,
            ct).ConfigureAwait(false);

        bool resolved = committed.Outcome
            is Sr6CareerAttributeOutcome.Applied or Sr6CareerAttributeOutcome.Replayed
            && committed.Receipt is not null;
        if (resolved)
        {
            return new Sr6CareerAttributeAdvanceDispatchResult(
                committed.Outcome, committed.Receipt, command, CommitResolved: true, Error: null);
        }

        // The response was not an observed commit. Before reporting failure,
        // probe the read-only Lookup replay of the EXACT submitted command: a
        // durable commit whose receipt response was lost resolves here and is
        // reported as recovered - never re-purchased. Genuinely non-committed
        // outcomes (Blocked, Conflict, Unavailable, ...) pass through typed.
        // The probe runs AFTER the serialized commit region returned so it never
        // nests inside the serialized executor.
        if (committed.Outcome is Sr6CareerAttributeOutcome.Unavailable or Sr6CareerAttributeOutcome.Corrupt)
        {
            Sr6CareerAttributeResult probe = await _workspaceOperations.Execute(
                () => _sr6CareerAttributeAdvancementService.Lookup(owner, command), CancellationToken.None)
                .ConfigureAwait(false);
            if (probe.Outcome == Sr6CareerAttributeOutcome.Replayed && probe.Receipt is not null)
            {
                return new Sr6CareerAttributeAdvanceDispatchResult(
                    Sr6CareerAttributeOutcome.Replayed, probe.Receipt, command, CommitResolved: true, Error: null);
            }
        }

        return new Sr6CareerAttributeAdvanceDispatchResult(
            committed.Outcome,
            committed.Receipt,
            command,
            CommitResolved: false,
            Error: committed.Error);
    }

    Task<Sr6CareerAttributeReceipt?> ISr6CareerAttributeAdvancementClient.LookupSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CancellationToken ct)
        => LookupSr6CareerAttributeAsync(owner, command, ct);

    private Task<Sr6CareerAttributeReceipt?> LookupSr6CareerAttributeAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.IsValid)
        {
            throw new InvalidOperationException(
                "The advancement owner authority is invalid. Reopen the dialog so a fresh quote can be prepared.");
        }

        ct.ThrowIfCancellationRequested();
        if (_sr6CareerAttributeAdvancementService is not { } service)
        {
            return Task.FromResult<Sr6CareerAttributeReceipt?>(null);
        }

        return _workspaceOperations.Execute(() =>
        {
            Sr6CareerAttributeResult replayed = service.Lookup(owner, command);
            return replayed.Outcome == Sr6CareerAttributeOutcome.Replayed
                ? replayed.Receipt
                : null;
        }, ct);
    }
}
