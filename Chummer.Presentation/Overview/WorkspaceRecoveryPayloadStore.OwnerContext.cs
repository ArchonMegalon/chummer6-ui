using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class WorkspaceRecoveryPayloadStore
{
    public bool RetireOwner(OwnerContextStamp originalOwner)
    {
        if (!originalOwner.IsValid) return false;
        lock (_gate)
        {
            // Tombstones are memory-only, bounded and never evicted into renewed
            // authority. A fresh presenter/account epoch is required at capacity.
            if (_disposed || (!_retiredOwners.Contains(originalOwner)
                    && _retiredOwners.Count >= MaxActiveCaptureIntents)) return false;
            _retiredOwners.Add(originalOwner);
            foreach (RecoveryKey key in _entries.Keys.Where(key => key.OriginalOwner == originalOwner).ToArray())
            {
                Entry entry = _entries[key];
                _entries.Remove(key);
                _retainedBytes -= entry.Bytes.LongLength;
                entry.Zero();
            }
            foreach (RecoveryKey key in _captureFailures.Keys.Where(key => key.OriginalOwner == originalOwner).ToArray())
                _captureFailures.Remove(key);
            // A committing capture still occupies capacity until its finally
            // runs. Retirement cannot be used to admit unbounded in-flight work.
            foreach (long token in _captureIntents.Where(pair => pair.Value.OriginalOwner == originalOwner
                         && !pair.Value.IsCommitting)
                         .Select(pair => pair.Key).ToArray())
                _captureIntents.Remove(token);
            return true;
        }
    }

    // Facets share the same bounded vault, generations, zeroing and capture
    // arbitration. They contain no second cache and no credentials.
    public IWorkspaceRecoveryPayloadStore ForOwner(OwnerContextStamp originalOwner)
    {
        if (!originalOwner.IsValid)
            throw new ArgumentException("Original recovery owner authority is required.", nameof(originalOwner));
        return new OwnerPartition(this, originalOwner);
    }

    public bool TryBeginCaptureIntent(CharacterWorkspaceId workspaceId, long sourceRevision, out IWorkspaceRecoveryCaptureIntent? captureIntent)
        => TryBeginCaptureIntentCore(workspaceId, sourceRevision, out captureIntent, originalOwner: null);

    public bool SetProtected(CharacterWorkspaceId workspaceId, long expectedSourceRevision, bool protectedFromEviction)
        => SetProtectedCore(workspaceId, expectedSourceRevision, protectedFromEviction, originalOwner: null);

    public WorkspaceRecoveryCopyAvailability GetAvailability(CharacterWorkspaceId workspaceId, long expectedSourceRevision)
        => GetAvailabilityCore(workspaceId, expectedSourceRevision, originalOwner: null);

    public bool TryAcquireLease(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration, out WorkspaceRecoveryPayloadLease? lease)
        => TryAcquireLeaseCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, out lease, originalOwner: null);

    public bool MarkExported(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration)
        => MarkExportedCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, originalOwner: null);

    public bool CanCloseAfterExport(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration)
        => CanCloseAfterExportCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, originalOwner: null);

    public bool TryCommitExplicitClose(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration, Action localCommit)
        => TryCommitExplicitCloseCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, localCommit, originalOwner: null);

    private sealed class OwnerPartition(WorkspaceRecoveryPayloadStore vault, OwnerContextStamp originalOwner)
        : IWorkspaceRecoveryPayloadStore
    {
        public bool RetireOwner(OwnerContextStamp requestedOwner)
            => requestedOwner == originalOwner && vault.RetireOwner(originalOwner);

        public IWorkspaceRecoveryPayloadStore ForOwner(OwnerContextStamp requestedOwner)
            => requestedOwner == originalOwner
                ? this
                : throw new InvalidOperationException("A recovery partition cannot change owner authority.");

        // The presenter owns the vault lifetime, not a borrowed partition.
        public void Dispose() { }

        public bool TryBeginCaptureIntent(CharacterWorkspaceId workspaceId, long sourceRevision, out IWorkspaceRecoveryCaptureIntent? captureIntent)
            => vault.TryBeginCaptureIntentCore(workspaceId, sourceRevision, out captureIntent, originalOwner);

        public bool SetProtected(CharacterWorkspaceId workspaceId, long expectedSourceRevision, bool protectedFromEviction)
            => vault.SetProtectedCore(workspaceId, expectedSourceRevision, protectedFromEviction, originalOwner);

        public WorkspaceRecoveryCopyAvailability GetAvailability(CharacterWorkspaceId workspaceId, long expectedSourceRevision)
            => vault.GetAvailabilityCore(workspaceId, expectedSourceRevision, originalOwner);

        public bool TryAcquireLease(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration, out WorkspaceRecoveryPayloadLease? lease)
            => vault.TryAcquireLeaseCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, out lease, originalOwner);

        public bool MarkExported(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration)
            => vault.MarkExportedCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, originalOwner);

        public bool CanCloseAfterExport(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration)
            => vault.CanCloseAfterExportCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, originalOwner);

        public bool TryCommitExplicitClose(CharacterWorkspaceId workspaceId, long expectedSourceRevision, long expectedLocalGeneration, Action localCommit)
            => vault.TryCommitExplicitCloseCore(workspaceId, expectedSourceRevision, expectedLocalGeneration, localCommit, originalOwner);

    }
}
