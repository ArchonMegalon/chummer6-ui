using System.Net;
using System.Net.Http.Json;
using Chummer.Contracts.Content;
using Chummer.Contracts.Session;

namespace Chummer.Presentation;

public sealed class HttpSessionClient : ISessionClient
{
    private readonly HttpClient _httpClient;

    public HttpSessionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<SessionApiResult<SessionCharacterCatalog>> ListCharactersAsync(CancellationToken ct)
        => SendAsync<SessionCharacterCatalog>(HttpMethod.Get, "/api/session/characters", payload: null, ct);

    public Task<SessionApiResult<SessionDashboardProjection>> GetCharacterProjectionAsync(string characterId, CancellationToken ct)
        => SendAsync<SessionDashboardProjection>(
            HttpMethod.Get,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}",
            payload: null,
            ct);

    public Task<SessionApiResult<SessionOverlaySnapshot>> ApplyCharacterPatchesAsync(string characterId, SessionPatchRequest request, CancellationToken ct)
        => SendAsync<SessionOverlaySnapshot>(
            HttpMethod.Post,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/patches",
            request,
            ct);

    public Task<SessionApiResult<SessionSyncReceipt>> SyncCharacterLedgerAsync(string characterId, SessionSyncBatch batch, CancellationToken ct)
        => SendAsync<SessionSyncReceipt>(
            HttpMethod.Post,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/sync",
            batch,
            ct);

    public Task<SessionApiResult<SessionProfileCatalog>> ListProfilesAsync(CancellationToken ct)
        => SendAsync<SessionProfileCatalog>(HttpMethod.Get, "/api/session/profiles", payload: null, ct);

    public Task<SessionApiResult<SessionRuntimeStatusProjection>> GetRuntimeStateAsync(string characterId, CancellationToken ct)
        => SendAsync<SessionRuntimeStatusProjection>(
            HttpMethod.Get,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/runtime-state",
            payload: null,
            ct);

    public Task<SessionApiResult<SessionRuntimeBundleIssueReceipt>> GetRuntimeBundleAsync(string characterId, CancellationToken ct)
        => SendAsync<SessionRuntimeBundleIssueReceipt>(
            HttpMethod.Get,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/runtime-bundle",
            payload: null,
            ct);

    public Task<SessionApiResult<SessionRuntimeBundleRefreshReceipt>> RefreshRuntimeBundleAsync(string characterId, CancellationToken ct)
        => SendAsync<SessionRuntimeBundleRefreshReceipt>(
            HttpMethod.Post,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/runtime-bundle/refresh",
            payload: null,
            ct);

    public Task<SessionApiResult<SessionProfileSelectionReceipt>> SelectProfileAsync(string characterId, SessionProfileSelectionRequest request, CancellationToken ct)
        => SendAsync<SessionProfileSelectionReceipt>(
            HttpMethod.Post,
            $"/api/session/characters/{Uri.EscapeDataString(characterId)}/profile",
            request,
            ct);

    public Task<SessionApiResult<RulePackCatalog>> ListRulePacksAsync(CancellationToken ct)
        => SendAsync<RulePackCatalog>(HttpMethod.Get, "/api/session/rulepacks", payload: null, ct);

    public Task<SessionApiResult<SessionOverlaySnapshot>> UpdatePinsAsync(SessionPinUpdateRequest request, CancellationToken ct)
        => SendAsync<SessionOverlaySnapshot>(HttpMethod.Post, "/api/session/pins", request, ct);

    private async Task<SessionApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotImplemented)
        {
            SessionNotImplementedReceipt? receipt = await response.Content.ReadFromJsonAsync<SessionNotImplementedReceipt>(ct);
            if (receipt is null)
            {
                throw new InvalidOperationException($"Session endpoint '{path}' returned HTTP 501 without a session receipt.");
            }

            return SessionApiResult<T>.FromNotImplemented(receipt);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Session request '{path}' failed with HTTP {(int)response.StatusCode}.");
        }

        T? result = await response.Content.ReadFromJsonAsync<T>(ct);
        if (result is null)
        {
            throw new InvalidOperationException($"Session request '{path}' returned an empty payload.");
        }

        return SessionApiResult<T>.Implemented(result);
    }
}
