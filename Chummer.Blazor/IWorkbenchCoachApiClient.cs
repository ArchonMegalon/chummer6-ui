using Chummer.Contracts.AI;

namespace Chummer.Blazor;

public interface IWorkbenchCoachApiClient
{
    Task<WorkbenchCoachApiCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default);

    Task<WorkbenchCoachApiCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default);

    Task<WorkbenchCoachApiCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
        string routeType,
        string? runtimeFingerprint = null,
        int maxCount = 3,
        CancellationToken ct = default);
}
