using Chummer.Presentation.RunnerIntelligence;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Desktop.Runtime.RunnerIntelligence;

public static class DesktopRunnerIntelligenceServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopRunnerIntelligence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RunnerIntelligenceCalculator>();
        services.AddSingleton<IRunnerIntelligenceCalculator>(provider => provider.GetRequiredService<RunnerIntelligenceCalculator>());
        services.AddSingleton<IRunnerIntelligenceScenarioCatalog, RunnerIntelligenceScenarioCatalog>();
        services.AddSingleton<DesktopRunnerIntelligenceBridge>();

        return services;
    }
}
