using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private const string Sr6RecoveryStorageError =
        "The advancement recovery record could not be safely read or saved. Further purchases are blocked; keep the recovery data for review.";
    private const string Sr6RecoveryUnresolvedError =
        "The advancement stays held for recovery. Reopen this runner with the original account to check its receipt; further purchases remain blocked.";

    /// <summary>The pending entry for this displayed owner/workspace, never a global slot.</summary>
    public Sr6CareerAttributePendingRecovery? PendingSr6Recovery
    {
        get
        {
            if (State.WorkspaceId is not { } id || State.DisplayOwnerContext is not { } owner
                || !IsOriginalPersistenceOwnerCurrent(owner)) return null;
            return _sr6CareerAttributeRecoveryStore.TryRead(owner.Owner, id, out var pending) ? pending : null;
        }
    }

    private bool IsSr6RecoveryDisplayCurrent(CharacterOverviewState original, OwnerContextStamp owner, long generation)
        => IsDisplayGenerationCurrent(generation) && State.WorkspaceId == original.WorkspaceId
            && State.ContentRevision == original.ContentRevision && IsOriginalPersistenceOwnerCurrent(owner);

    private void PublishSr6RecoveryError(CharacterOverviewState original, OwnerContextStamp owner, long generation, string error)
    {
        if (IsSr6RecoveryDisplayCurrent(original, owner, generation))
            TryPublishDisplayTransition(generation, State with { Error = error }, original);
    }

    private bool TryCaptureSr6RecoveryOwner(OwnerContextStamp expectedIdentity, out OwnerContextStamp fresh)
    {
        fresh = default;
        if (_client is not IOwnerBoundWorkspaceMutationClient bound) return false;
        try
        {
            fresh = bound.CaptureOwnerContext();
            // Scope includes trusted-local identity. A same-name account is not
            // permission to read the trusted local store. Core still admits a
            // fresh live lease when Lookup actually executes.
            return fresh.IsValid && fresh.Owner == expectedIdentity.Owner && State.DisplayOwnerContext == fresh;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<bool> CanPrepareSr6AdvancementAsync(CharacterOverviewState state, CancellationToken ct)
    {
        if (state.WorkspaceId is not { } id || state.DisplayOwnerContext is not { } displayed
            || !TryCaptureSr6RecoveryOwner(displayed, out var fresh))
        {
            Publish(State with { Error = "Reload this runner with its original account before advancing an attribute." });
            return false;
        }
        long generation = CaptureDisplayGeneration();
        if (!_sr6CareerAttributeRecoveryStore.TryRead(fresh.Owner, id, out var pending))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryStorageError);
            return false;
        }
        if (pending is null) return IsSr6RecoveryDisplayCurrent(state, fresh, generation);
        await RecoverPendingSr6AdvancementAsync(state, fresh, generation, ct).ConfigureAwait(false);
        // This gesture was a recovery probe, including when it succeeded. Never
        // turn an uncertain purchase into a quote or a new purchase implicitly.
        return false;
    }

    private async Task RecoverSr6AdvancementOnOpenAsync(long generation, CancellationToken ct)
    {
        CharacterOverviewState state = State;
        // The durable command identifies the SR6 operation. The optional active
        // tab/session projection may not yet be populated when a runner opens.
        if (!IsDisplayGenerationCurrent(generation)
            || state.WorkspaceId is not { } id || state.DisplayOwnerContext is not { } displayed
            || !TryCaptureSr6RecoveryOwner(displayed, out var fresh)) return;
        if (!_sr6CareerAttributeRecoveryStore.TryRead(fresh.Owner, id, out var pending))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryStorageError);
            return;
        }
        if (pending is not null) await RecoverPendingSr6AdvancementAsync(state, fresh, generation, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fresh authorization for the original owner; read-only Lookup of the exact
    /// durable command, regardless of later character revisions. The parameter
    /// identifies the expected owner only; its old authority is never reused.
    /// </summary>
    public Task<Sr6CareerAttributeReceipt?> RecoverPendingSr6AdvancementAsync(OwnerContextStamp owner, CancellationToken ct)
        => RecoverPendingSr6AdvancementAsync(State, owner, CaptureDisplayGeneration(), ct);

    private async Task<Sr6CareerAttributeReceipt?> RecoverPendingSr6AdvancementAsync(
        CharacterOverviewState state, OwnerContextStamp owner, long generation, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        if (state.WorkspaceId is not { } id || !TryCaptureSr6RecoveryOwner(owner, out var fresh)) return null;
        if (!_sr6CareerAttributeRecoveryStore.TryRead(fresh.Owner, id, out var pending))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryStorageError);
            return null;
        }
        if (pending is null) return null;
        if (!pending.IsValid || !pending.BelongsTo(fresh.Owner, id))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryStorageError);
            return null;
        }
        if (!IsSr6RecoveryDisplayCurrent(state, fresh, generation)) return null;
        Sr6CareerAttributeReceipt? receipt;
        try
        {
            receipt = await _client.LookupSr6CareerAttributeAsync(fresh, pending.Command, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryUnresolvedError);
            return null;
        }
        if (!pending.MatchesReceipt(receipt))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryUnresolvedError);
            return null;
        }
        // The receipt can resolve the original entry after a display transition,
        // but cannot clear another owner/workspace or a newer intent incarnation.
        if (!_sr6CareerAttributeRecoveryStore.TryClear(pending))
        {
            PublishSr6RecoveryError(state, fresh, generation, Sr6RecoveryStorageError);
            return null;
        }
        if (IsSr6RecoveryDisplayCurrent(state, fresh, generation))
            TryPublishDisplayTransition(generation, State with
            {
                Error = null,
                Notice = "The held advancement was confirmed. Reload the runner to review its current values."
            }, state);
        return receipt;
    }
}
