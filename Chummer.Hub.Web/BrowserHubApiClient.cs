using System.Text.Json;
using Chummer.Contracts.Content;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Presentation;
using Microsoft.JSInterop;

namespace Chummer.Hub.Web;

public sealed class BrowserHubApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IJSRuntime _jsRuntime;

    public BrowserHubApiClient(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentNullException.ThrowIfNull(configuration);
        _jsRuntime = jsRuntime;
    }

    public Task<HubCatalogResultPage> SearchAsync(BrowseQuery query, CancellationToken cancellationToken = default)
        => SendAsync<BrowseQuery, HubCatalogResultPage>("/api/hub/search", "POST", query, cancellationToken);

    public Task<HubProjectDetailProjection> GetProjectDetailAsync(string kind, string itemId, CancellationToken cancellationToken = default)
        => SendAsync<HubProjectDetailProjection>($"/api/hub/projects/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(itemId)}", "GET", cancellationToken);

    public Task<HubProjectCompatibilityMatrix> GetCompatibilityAsync(string kind, string itemId, CancellationToken cancellationToken = default)
        => SendAsync<HubProjectCompatibilityMatrix>($"/api/hub/projects/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(itemId)}/compatibility", "GET", cancellationToken);

    public Task<HubProjectInstallPreviewReceipt> PreviewInstallAsync(string kind, string itemId, CancellationToken cancellationToken = default)
        => SendAsync<RuleProfileApplyTarget, HubProjectInstallPreviewReceipt>(
            $"/api/hub/projects/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(itemId)}/install-preview",
            "POST",
            new RuleProfileApplyTarget(RuleProfileApplyTargetKinds.GlobalDefaults, "hub-preview"),
            cancellationToken);

    public Task<HubPublishDraftList> ListDraftsAsync(CancellationToken cancellationToken = default)
        => SendAsync<HubPublishDraftList>("/api/hub/publish/drafts", "GET", cancellationToken);

    public Task<HubDraftDetailProjection> GetDraftDetailAsync(string draftId, CancellationToken cancellationToken = default)
        => SendAsync<HubDraftDetailProjection>($"/api/hub/publish/drafts/{Uri.EscapeDataString(draftId)}", "GET", cancellationToken);

    public Task<HubPublishDraftReceipt> CreateDraftAsync(HubPublishDraftRequest request, CancellationToken cancellationToken = default)
        => SendAsync<HubPublishDraftRequest, HubPublishDraftReceipt>("/api/hub/publish/drafts", "POST", request, cancellationToken);

    public Task<HubPublishDraftReceipt> UpdateDraftAsync(string draftId, HubUpdateDraftRequest request, CancellationToken cancellationToken = default)
        => SendAsync<HubUpdateDraftRequest, HubPublishDraftReceipt>($"/api/hub/publish/drafts/{Uri.EscapeDataString(draftId)}", "PUT", request, cancellationToken);

    public Task<HubProjectSubmissionReceipt> SubmitDraftAsync(string kind, string itemId, string rulesetId, HubSubmitProjectRequest request, CancellationToken cancellationToken = default)
        => SendAsync<HubSubmitProjectRequest, HubProjectSubmissionReceipt>($"/api/hub/publish/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(itemId)}/submit?ruleset={Uri.EscapeDataString(rulesetId)}", "POST", request, cancellationToken);

    public Task<HubPublishDraftReceipt> ArchiveDraftAsync(string draftId, CancellationToken cancellationToken = default)
        => SendAsync<string, HubPublishDraftReceipt>($"/api/hub/publish/drafts/{Uri.EscapeDataString(draftId)}/archive", "POST", string.Empty, cancellationToken);

    public Task DeleteDraftAsync(string draftId, CancellationToken cancellationToken = default)
        => SendAsync<string, string>($"/api/hub/publish/drafts/{Uri.EscapeDataString(draftId)}", "DELETE", string.Empty, cancellationToken);

    public async Task<bool> CanModerateAsync(CancellationToken cancellationToken = default)
    {
        HubModerationCapability capability = await SendAsync<HubModerationCapability>(
            "/api/hub/moderation/capability",
            "GET",
            cancellationToken).ConfigureAwait(false);
        return capability.CanModerate;
    }

    public Task<HubModerationQueue> ListModerationQueueAsync(string? state, CancellationToken cancellationToken = default)
    {
        string path = string.IsNullOrWhiteSpace(state)
            ? "/api/hub/moderation/queue"
            : $"/api/hub/moderation/queue?state={Uri.EscapeDataString(state)}";
        return SendAsync<HubModerationQueue>(path, "GET", cancellationToken);
    }

    public Task<HubModerationDecisionReceipt> ApproveModerationAsync(string caseId, HubModerationDecisionRequest request, CancellationToken cancellationToken = default)
        => SendAsync<HubModerationDecisionRequest, HubModerationDecisionReceipt>($"/api/hub/moderation/queue/{Uri.EscapeDataString(caseId)}/approve", "POST", request, cancellationToken);

    public Task<HubModerationDecisionReceipt> RejectModerationAsync(string caseId, HubModerationDecisionRequest request, CancellationToken cancellationToken = default)
        => SendAsync<HubModerationDecisionRequest, HubModerationDecisionReceipt>($"/api/hub/moderation/queue/{Uri.EscapeDataString(caseId)}/reject", "POST", request, cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(string path, string method, CancellationToken cancellationToken)
    {
        string envelopeText = await _jsRuntime.InvokeAsync<string>("chummerHubApi.send", cancellationToken, path, method).ConfigureAwait(false);
        return DeserializeEnvelope<TResponse>(envelopeText);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(string path, string method, TRequest payload, CancellationToken cancellationToken)
    {
        string body = payload is string text
            ? text
            : JsonSerializer.Serialize(payload, JsonOptions);
        string envelopeText = await _jsRuntime.InvokeAsync<string>("chummerHubApi.send", cancellationToken, path, method, body).ConfigureAwait(false);
        return DeserializeEnvelope<TResponse>(envelopeText);
    }

    private static TResponse DeserializeEnvelope<TResponse>(string envelopeText)
    {
        HubBrowserEnvelope envelope = JsonSerializer.Deserialize<HubBrowserEnvelope>(envelopeText, JsonOptions)
            ?? throw new InvalidOperationException("Hub response envelope was not returned.");

        if (envelope.Status < 200 || envelope.Status >= 300)
        {
            throw new InvalidOperationException(TryReadErrorMessage(envelope.Text) ?? $"Hub request failed with status {envelope.Status}.");
        }

        if (typeof(TResponse) == typeof(string))
        {
            return (TResponse)(object)(envelope.Text ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(envelope.Text))
        {
            return Activator.CreateInstance<TResponse>();
        }

        return JsonSerializer.Deserialize<TResponse>(envelope.Text, JsonOptions)
            ?? throw new InvalidOperationException("Hub response payload could not be deserialized.");
    }

    private static string? TryReadErrorMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("message", out JsonElement message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            return text;
        }

        return text;
    }

    private sealed record HubBrowserEnvelope(int Status, string? Text);
    private sealed record HubModerationCapability(bool CanModerate);
}
