using Chummer.Presentation.RunnerIntelligence;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Blazor.RunnerIntelligence;

public static class BlazorRunnerIntelligenceServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorRunnerIntelligence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<RunnerIntelligenceCalculator>();
        services.AddScoped<IRunnerIntelligenceCalculator>(provider => provider.GetRequiredService<RunnerIntelligenceCalculator>());
        services.AddScoped<IRunnerIntelligenceScenarioCatalog, RunnerIntelligenceScenarioCatalog>();
        services.AddScoped<BlazorRunnerIntelligencePreviewService>();

        return services;
    }
}
