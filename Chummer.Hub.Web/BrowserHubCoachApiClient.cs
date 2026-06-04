using System.Text.Json;
using Chummer.Contracts.AI;
using Microsoft.JSInterop;

namespace Chummer.Hub.Web;

public sealed class BrowserHubCoachApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IJSRuntime _jsRuntime;

    public BrowserHubCoachApiClient(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentNullException.ThrowIfNull(configuration);
        _jsRuntime = jsRuntime;
    }

    public Task<AiGatewayStatusProjection> GetStatusAsync(CancellationToken cancellationToken = default)
        => SendAsync<AiGatewayStatusProjection>("/api/ai/status", "GET", cancellationToken);

    public Task<IReadOnlyList<AiProviderHealthProjection>> GetProviderHealthAsync(string routeType, CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<AiProviderHealthProjection>>($"/api/ai/provider-health?routeType={Uri.EscapeDataString(routeType)}", "GET", cancellationToken);

    public async Task<IReadOnlyList<AiConversationAuditSummary>> GetConversationAuditsAsync(string routeType, int maxCount, CancellationToken cancellationToken = default)
    {
        AiConversationAuditCatalogPage page = await SendAsync<AiConversationAuditCatalogPage>(
            $"/api/ai/conversation-audits?routeType={Uri.EscapeDataString(routeType)}&maxCount={maxCount}",
            "GET",
            cancellationToken).ConfigureAwait(false);
        return page.Items;
    }

    private async Task<TResponse> SendAsync<TResponse>(string path, string method, CancellationToken cancellationToken)
    {
        string envelopeText = await _jsRuntime.InvokeAsync<string>("chummerHubApi.send", cancellationToken, path, method).ConfigureAwait(false);
        HubBrowserEnvelope envelope = JsonSerializer.Deserialize<HubBrowserEnvelope>(envelopeText, JsonOptions)
            ?? throw new InvalidOperationException("Coach response envelope was not returned.");
        if (envelope.Status < 200 || envelope.Status >= 300)
        {
            throw new InvalidOperationException($"Coach request failed with status {envelope.Status}.");
        }

        return JsonSerializer.Deserialize<TResponse>(envelope.Text ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("Coach response payload could not be deserialized.");
    }

    private sealed record HubBrowserEnvelope(int Status, string? Text);
}
