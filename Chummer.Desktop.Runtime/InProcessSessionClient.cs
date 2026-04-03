using Chummer.Application.Owners;
using Chummer.Application.Session;
using Chummer.Contracts.Content;
using Chummer.Contracts.Session;
using Chummer.Infrastructure.Owners;
using Chummer.Presentation;

namespace Chummer.Desktop.Runtime;

public sealed class InProcessSessionClient : ISessionClient
{
    private readonly ISessionService _sessionService;
    private readonly IOwnerContextAccessor _ownerContextAccessor;

    public InProcessSessionClient(
        ISessionService sessionService,
        IOwnerContextAccessor? ownerContextAccessor = null)
    {
        _sessionService = sessionService;
        _ownerContextAccessor = ownerContextAccessor ?? new LocalOwnerContextAccessor();
    }

    public Task<SessionApiResult<SessionCharacterCatalog>> ListCharactersAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.ListCharacters(_ownerContextAccessor.Current));
    }

    public Task<SessionApiResult<SessionDashboardProjection>> GetCharacterProjectionAsync(string characterId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.GetCharacterProjection(_ownerContextAccessor.Current, characterId));
    }

    public Task<SessionApiResult<SessionOverlaySnapshot>> ApplyCharacterPatchesAsync(string characterId, SessionPatchRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.ApplyCharacterPatches(_ownerContextAccessor.Current, characterId, request));
    }

    public Task<SessionApiResult<SessionSyncReceipt>> SyncCharacterLedgerAsync(string characterId, SessionSyncBatch batch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.SyncCharacterLedger(_ownerContextAccessor.Current, characterId, batch));
    }

    public Task<SessionApiResult<SessionProfileCatalog>> ListProfilesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.ListProfiles(_ownerContextAccessor.Current));
    }

    public Task<SessionApiResult<SessionRuntimeStatusProjection>> GetRuntimeStateAsync(string characterId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.GetRuntimeState(_ownerContextAccessor.Current, characterId));
    }

    public Task<SessionApiResult<SessionRuntimeBundleIssueReceipt>> GetRuntimeBundleAsync(string characterId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.GetRuntimeBundle(_ownerContextAccessor.Current, characterId));
    }

    public Task<SessionApiResult<SessionRuntimeBundleRefreshReceipt>> RefreshRuntimeBundleAsync(string characterId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.RefreshRuntimeBundle(_ownerContextAccessor.Current, characterId));
    }

    public Task<SessionApiResult<SessionProfileSelectionReceipt>> SelectProfileAsync(string characterId, SessionProfileSelectionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.SelectProfile(_ownerContextAccessor.Current, characterId, request));
    }

    public Task<SessionApiResult<RulePackCatalog>> ListRulePacksAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.ListRulePacks(_ownerContextAccessor.Current));
    }

    public Task<SessionApiResult<SessionOverlaySnapshot>> UpdatePinsAsync(SessionPinUpdateRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionService.UpdatePins(_ownerContextAccessor.Current, request));
    }
}
