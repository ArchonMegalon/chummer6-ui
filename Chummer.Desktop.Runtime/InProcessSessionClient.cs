using Chummer.Application.Owners;
using Chummer.Application.Session;
using Chummer.Contracts.Content;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Session;
using Chummer.Infrastructure.Owners;
using Chummer.Presentation;

namespace Chummer.Desktop.Runtime;

public sealed class InProcessSessionClient : ISessionClient
{
    private readonly ISessionService _sessionService;
    private readonly IOwnerContextAccessor _ownerContextAccessor;
    private readonly SemaphoreSlim _sessionOperations = new(1, 1);

    public InProcessSessionClient(
        ISessionService sessionService,
        IOwnerContextAccessor? ownerContextAccessor = null)
    {
        _sessionService = sessionService;
        _ownerContextAccessor = ownerContextAccessor ?? new LocalOwnerContextAccessor();
    }

    public Task<SessionApiResult<SessionCharacterCatalog>> ListCharactersAsync(CancellationToken ct)
        => ExecuteOwnerBoundAsync(_sessionService.ListCharacters, ct);

    public Task<SessionApiResult<SessionDashboardProjection>> GetCharacterProjectionAsync(string characterId, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.GetCharacterProjection(owner, characterId), ct);

    public Task<SessionApiResult<SessionOverlaySnapshot>> ApplyCharacterPatchesAsync(string characterId, SessionPatchRequest request, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.ApplyCharacterPatches(owner, characterId, request), ct);

    public Task<SessionApiResult<SessionSyncReceipt>> SyncCharacterLedgerAsync(string characterId, SessionSyncBatch batch, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.SyncCharacterLedger(owner, characterId, batch), ct);

    public Task<SessionApiResult<SessionProfileCatalog>> ListProfilesAsync(CancellationToken ct)
        => ExecuteOwnerBoundAsync(_sessionService.ListProfiles, ct);

    public Task<SessionApiResult<SessionRuntimeStatusProjection>> GetRuntimeStateAsync(string characterId, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.GetRuntimeState(owner, characterId), ct);

    public Task<SessionApiResult<SessionRuntimeBundleIssueReceipt>> GetRuntimeBundleAsync(string characterId, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.GetRuntimeBundle(owner, characterId), ct);

    public Task<SessionApiResult<SessionRuntimeBundleRefreshReceipt>> RefreshRuntimeBundleAsync(string characterId, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.RefreshRuntimeBundle(owner, characterId), ct);

    public Task<SessionApiResult<SessionProfileSelectionReceipt>> SelectProfileAsync(string characterId, SessionProfileSelectionRequest request, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.SelectProfile(owner, characterId, request), ct);

    public Task<SessionApiResult<RulePackCatalog>> ListRulePacksAsync(CancellationToken ct)
        => ExecuteOwnerBoundAsync(_sessionService.ListRulePacks, ct);

    public Task<SessionApiResult<SessionOverlaySnapshot>> UpdatePinsAsync(SessionPinUpdateRequest request, CancellationToken ct)
        => ExecuteOwnerBoundAsync(owner => _sessionService.UpdatePins(owner, request), ct);

    private Task<TResult> ExecuteOwnerBoundAsync<TResult>(
        Func<OwnerScope, TResult> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        IOwnerContextLeaseAccessor accessor = RequireOwnerLeaseAccessor();
        OwnerContextStamp expected = accessor.Capture();
        if (!expected.IsValid)
        {
            throw new InvalidOperationException(
                "Owner authority is unavailable. Reopen the session operation before continuing.");
        }

        return ExecuteOwnerBoundCoreAsync(accessor, expected, operation, cancellationToken);
    }

    private async Task<TResult> ExecuteOwnerBoundCoreAsync<TResult>(
        IOwnerContextLeaseAccessor accessor,
        OwnerContextStamp expected,
        Func<OwnerScope, TResult> operation,
        CancellationToken cancellationToken)
    {
        await _sessionOperations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The caller token may remove work before the synchronous delegate
            // starts. Once it starts, the exact service result reaches the caller.
            return await Task.Run(() =>
            {
                if (!accessor.TryAcquire(expected, out IOwnerContextLease? lease))
                {
                    throw new InvalidOperationException(
                        "Owner authority changed before session dispatch. Reopen the operation before continuing.");
                }

                // Acquire, use and dispose the writer lease on this worker thread.
                // Session services are synchronous; no await or network call may
                // be added inside this lease boundary.
                using (lease)
                {
                    OwnerContextStamp admitted = lease.Stamp;
                    if (admitted != expected)
                    {
                        throw new InvalidOperationException(
                            "The acquired owner authority does not match the requested session operation.");
                    }

                    return operation(admitted.Owner);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionOperations.Release();
        }
    }

    private IOwnerContextLeaseAccessor RequireOwnerLeaseAccessor()
        => _ownerContextAccessor as IOwnerContextLeaseAccessor
            ?? throw new InvalidOperationException(
                "This owner authority does not support safe local session dispatch.");
}
