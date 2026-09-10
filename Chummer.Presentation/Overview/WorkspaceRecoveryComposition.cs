using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Host composition, not a caller-selected display client, supplies recovery
/// authority. The public display loader still cannot issue validation receipts.
/// </summary>
public static class WorkspaceRecoveryComposition
{
    public static IServiceCollection AddChummerWorkspaceRecovery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IWorkspaceOverviewLoader>(provider =>
            WorkspaceOverviewLoader.CreateCompositionBound(provider.GetRequiredService<IChummerClient>()));
        return services;
    }
}
