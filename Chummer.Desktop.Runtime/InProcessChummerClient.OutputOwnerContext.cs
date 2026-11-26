using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;

namespace Chummer.Desktop.Runtime;

public sealed partial class InProcessChummerClient : IOwnerBoundWorkspaceOutputClient
{
    public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct)
        => ReadOriginalOutputAsync(originalOwner, id, expectedContentRevision,
            owner => _workspaceService.Download(owner, id), receipt => receipt.Id, ct);

    public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct)
        => ReadOriginalOutputAsync(originalOwner, id, expectedContentRevision,
            owner => _workspaceService.Export(owner, id), receipt => receipt.Id, ct);

    public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedContentRevision, CancellationToken ct)
        => ReadOriginalOutputAsync(originalOwner, id, expectedContentRevision,
            owner => _workspaceService.Print(owner, id), receipt => receipt.Id, ct);

    private Task<CommandResult<T>> ReadOriginalOutputAsync<T>(OwnerContextStamp originalOwner,
        CharacterWorkspaceId id, long expectedRevision, Func<OwnerScope, CommandResult<T>> read,
        Func<T, CharacterWorkspaceId> receiptId, CancellationToken ct) where T : class
        => _workspaceOperations.Execute(() => WithOwnerLease(originalOwner, owner =>
        {
            // Core owns rendering and document formats. Its canonical before and
            // after snapshots reject intervening revisions; never recalculate or
            // reconstruct an export/print receipt in the UI/runtime adapter.
            CommandResult<WorkspaceDocumentSnapshot> before = _workspaceService.GetWorkspace(owner, id);
            if (expectedRevision <= 0 || !before.Success || before.Value is not { } original
                || original.Id != id || original.ContentRevision != expectedRevision)
                return new CommandResult<T>(false, null, "The output revision changed. Reload before exporting.",
                    WorkspaceOperationOutcome.Conflict);
            // File-backed reads rehydrate auxiliary receipt collections. Record
            // equality compares their identities, not their persisted values.
            // Capture Core's canonical value digest before output preparation so
            // an in-place auxiliary mutation cannot change both sides of the check.
            string originalAuxiliaryStateDigest = original.Document.AuxiliaryStateDigest;
            CommandResult<T> result = read(owner);
            if (!result.Success || result.Value is null) return result;
            CommandResult<WorkspaceDocumentSnapshot> after = _workspaceService.GetWorkspace(owner, id);
            if (receiptId(result.Value) != id || !after.Success || after.Value is not { } current
                || current.Id != id || current.ContentRevision != expectedRevision
                || !HasSameOutputDocument(original.Document, current.Document, originalAuxiliaryStateDigest))
                return new CommandResult<T>(false, null, "The output source changed while preparing it.",
                    WorkspaceOperationOutcome.Conflict);
            return result;
        }), ct);

    private static bool HasSameOutputDocument(WorkspaceDocument original, WorkspaceDocument current,
        string originalAuxiliaryStateDigest)
        // Retain record equality for every other document/state member. Replace
        // only auxiliary reference equality with Core's authoritative value digest.
        => (current with { State = current.State with { AuxiliaryState = original.State.AuxiliaryState } }) == original
            && string.Equals(current.AuxiliaryStateDigest, originalAuxiliaryStateDigest, StringComparison.Ordinal);
}
