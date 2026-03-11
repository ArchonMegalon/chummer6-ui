using System.Net.Http;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.AI;
using Microsoft.AspNetCore.Http;

namespace Chummer.Blazor;

public sealed class WorkbenchCoachApiClient : IWorkbenchCoachApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public WorkbenchCoachApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<WorkbenchCoachApiCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default)
        => SendAsync<AiGatewayStatusProjection>(HttpMethod.Get, "/api/ai/status", payload: null, ct);

    public Task<WorkbenchCoachApiCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default)
        => SendAsync<AiProviderHealthProjection[]>(
            HttpMethod.Get,
            AppendQuery(
                "/api/ai/provider-health",
                ("routeType", routeType)),
            payload: null,
            ct);

    public Task<WorkbenchCoachApiCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
        string routeType,
        string? runtimeFingerprint = null,
        int maxCount = 3,
        CancellationToken ct = default)
        => SendAsync<AiConversationAuditCatalogPage>(
            HttpMethod.Get,
            AppendQuery(
                "/api/ai/conversation-audits",
                ("routeType", routeType),
                ("runtimeFingerprint", runtimeFingerprint),
                ("maxCount", maxCount.ToString())),
            payload: null,
            ct);

    private async Task<WorkbenchCoachApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path);
        if (payload is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if ((int)response.StatusCode == StatusCodes.Status501NotImplemented)
            {
                AiNotImplementedReceipt? receipt = DeserializePayload<AiNotImplementedReceipt>(responseText);
                if (receipt is null)
                {
                    return WorkbenchCoachApiCallResult<T>.Failure(
                        (int)response.StatusCode,
                        $"Coach request '{path}' returned HTTP 501 without an AI receipt.");
                }

                return WorkbenchCoachApiCallResult<T>.FromNotImplemented((int)response.StatusCode, receipt);
            }

            if ((int)response.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                AiQuotaExceededReceipt? receipt = DeserializePayload<AiQuotaExceededReceipt>(responseText);
                if (receipt is null)
                {
                    return WorkbenchCoachApiCallResult<T>.Failure(
                        (int)response.StatusCode,
                        $"Coach request '{path}' returned HTTP 429 without an AI quota receipt.");
                }

                return WorkbenchCoachApiCallResult<T>.FromQuotaExceeded((int)response.StatusCode, receipt);
            }

            if (!response.IsSuccessStatusCode)
            {
                return WorkbenchCoachApiCallResult<T>.Failure(
                    (int)response.StatusCode,
                    ExtractErrorMessage(responseText) ?? $"Coach request '{path}' failed with HTTP {(int)response.StatusCode}.");
            }

            T? typedPayload = DeserializePayload<T>(responseText);
            if (typedPayload is null)
            {
                return WorkbenchCoachApiCallResult<T>.Failure(
                    (int)response.StatusCode,
                    $"Coach request '{path}' returned an empty payload.");
            }

            return WorkbenchCoachApiCallResult<T>.Success((int)response.StatusCode, typedPayload);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return WorkbenchCoachApiCallResult<T>.Failure(0, $"Coach request '{path}' failed in the workbench head: {ex.Message}");
        }
    }

    private static T? DeserializePayload<T>(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(responseText, JsonOptions);
    }

    private static string AppendQuery(string path, params (string Key, string? Value)[] pairs)
    {
        List<string> encoded = [];
        foreach ((string key, string? value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            encoded.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return encoded.Count == 0
            ? path
            : $"{path}?{string.Join("&", encoded)}";
    }

    private static string? ExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseText);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return responseText;
            }

            if (root.TryGetProperty("message", out JsonElement message))
            {
                return message.GetString();
            }

            if (root.TryGetProperty("error", out JsonElement error))
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            return responseText;
        }

        return responseText;
    }
}

public sealed record WorkbenchCoachApiCallResult<T>(
    bool IsSuccess,
    bool IsImplemented,
    int StatusCode,
    T? Payload,
    string? ErrorMessage = null,
    AiNotImplementedReceipt? NotImplemented = null,
    AiQuotaExceededReceipt? QuotaExceeded = null)
{
    public static WorkbenchCoachApiCallResult<T> Success(int statusCode, T payload)
        => new(true, true, statusCode, payload);

    public static WorkbenchCoachApiCallResult<T> Failure(int statusCode, string message)
        => new(false, true, statusCode, default, message);

    public static WorkbenchCoachApiCallResult<T> FromNotImplemented(int statusCode, AiNotImplementedReceipt receipt)
        => new(false, false, statusCode, default, receipt.Message, receipt);

    public static WorkbenchCoachApiCallResult<T> FromQuotaExceeded(int statusCode, AiQuotaExceededReceipt receipt)
        => new(false, true, statusCode, default, receipt.Message, null, receipt);
}
