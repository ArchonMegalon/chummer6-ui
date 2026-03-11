using Chummer.Contracts.AI;

namespace Chummer.Avalonia;

public interface IAvaloniaCoachSidecarClient
{
    Task<AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default);

    Task<AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default);

    Task<AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
        string routeType,
        string? runtimeFingerprint = null,
        int maxCount = 3,
        CancellationToken ct = default);
}
