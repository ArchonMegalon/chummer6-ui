using Chummer.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chummer.Desktop.Runtime;

public static class ServiceCollectionDesktopRuntimeExtensions
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";

    public static IServiceCollection AddChummerLocalRuntimeClient(
        this IServiceCollection services,
        string baseDirectory,
        string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = baseDirectory;
        _ = currentDirectory;

        services.RemoveAll<IChummerClient>();
        services.RemoveAll<ISessionClient>();
        services.TryAddSingleton(CreateApiHttpClient());
        services.TryAddSingleton<IChummerClient, HttpChummerClient>();
        services.TryAddSingleton<ISessionClient, HttpSessionClient>();
        return services;
    }

    [Obsolete("Use AddChummerLocalRuntimeClient instead.")]
    public static IServiceCollection AddChummerDesktopRuntimeClient(
        this IServiceCollection services,
        string baseDirectory,
        string currentDirectory)
        => AddChummerLocalRuntimeClient(services, baseDirectory, currentDirectory);

    private static HttpClient CreateApiHttpClient()
    {
        HttpClient client = new()
        {
            BaseAddress = ResolveApiBaseAddress(),
            Timeout = TimeSpan.FromSeconds(20)
        };

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    private static Uri ResolveApiBaseAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        const string fallback = "http://chummer-api:8080";
        if (!Uri.TryCreate(fallback, UriKind.Absolute, out Uri? fallbackUri))
        {
            throw new InvalidOperationException($"Invalid fallback API base address '{fallback}'.");
        }

        return fallbackUri;
    }
}
