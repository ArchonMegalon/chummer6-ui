using Chummer.Application.Owners;
using Chummer.Infrastructure.DependencyInjection;
using Chummer.Presentation;
using Chummer.Presentation.OriginBooks;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chummer.Desktop.Runtime;

public static class ServiceCollectionDesktopRuntimeExtensions
{
    private const string ClientModeEnvironmentVariable = "CHUMMER_CLIENT_MODE";
    private const string LegacyDesktopClientModeEnvironmentVariable = "CHUMMER_DESKTOP_CLIENT_MODE";
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string StatePathEnvironmentVariable = "CHUMMER_STATE_PATH";
    private const string HttpClientMode = "http";

    public static IServiceCollection AddChummerLocalRuntimeClient(
        this IServiceCollection services,
        string baseDirectory,
        string currentDirectory,
        string? desktopHeadId = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ConfigureDesktopStatePath(desktopHeadId);
        services.AddChummerHeadlessCore(baseDirectory, currentDirectory);
        services.AddSr4Ruleset();
        services.AddSr5Ruleset();
        services.AddSr6Ruleset();

        services.RemoveAll<IChummerClient>();
        services.RemoveAll<ISessionClient>();
        services.RemoveAll<HttpClient>();
        services.RemoveAll<IOwnerContextAccessor>();

        if (!string.IsNullOrWhiteSpace(desktopHeadId))
        {
            services.TryAddSingleton<IOwnerContextAccessor>(_ => new DesktopInstallOwnerContextAccessor(desktopHeadId));
        }
        else
        {
            services.TryAddSingleton<IOwnerContextAccessor, Chummer.Infrastructure.Owners.LocalOwnerContextAccessor>();
        }

        if (UseHttpClientMode())
        {
            services.TryAddSingleton(CreateApiHttpClient());
            services.TryAddSingleton<IDesktopWorkspaceRoamingSync, NoOpDesktopWorkspaceRoamingSync>();
            services.TryAddSingleton<IChummerClient, HttpChummerClient>();
            services.TryAddSingleton(static provider => (IOriginDossierPublicationClient)provider.GetRequiredService<IChummerClient>());
            services.TryAddSingleton<ISessionClient, HttpSessionClient>();
            return services;
        }

        if (!string.IsNullOrWhiteSpace(desktopHeadId))
        {
            services.TryAddSingleton<IDesktopWorkspaceRoamingSync>(provider => new GrantBoundDesktopWorkspaceRoamingSync(
                desktopHeadId,
                provider.GetRequiredService<Chummer.Application.Workspaces.IWorkspaceStore>(),
                provider.GetRequiredService<Chummer.Application.Workspaces.IWorkspaceService>()));
        }
        else
        {
            services.TryAddSingleton<IDesktopWorkspaceRoamingSync, NoOpDesktopWorkspaceRoamingSync>();
        }

        services.TryAddSingleton<IChummerClient, InProcessChummerClient>();
        services.TryAddSingleton<ISessionClient, InProcessSessionClient>();
        return services;
    }

    [Obsolete("Use AddChummerLocalRuntimeClient instead.")]
    public static IServiceCollection AddChummerDesktopRuntimeClient(
        this IServiceCollection services,
        string baseDirectory,
        string currentDirectory)
        => AddChummerLocalRuntimeClient(services, baseDirectory, currentDirectory);

    private static void ConfigureDesktopStatePath(string? desktopHeadId)
    {
        if (string.IsNullOrWhiteSpace(desktopHeadId))
        {
            return;
        }

        string? configuredStatePath = Environment.GetEnvironmentVariable(StatePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredStatePath))
        {
            return;
        }

        string sharedDesktopRoot = DesktopStateRootResolver.Resolve("Chummer6", "Chummer6");
        Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, Path.Combine(sharedDesktopRoot, "state"));
    }

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

        throw new InvalidOperationException(
            $"Set {ApiBaseUrlEnvironmentVariable} when {ClientModeEnvironmentVariable}=http (legacy: {LegacyDesktopClientModeEnvironmentVariable}=http). " +
            $"HTTP desktop client mode requires {ApiBaseUrlEnvironmentVariable} to be set to an absolute URL.");
    }

    private static bool UseHttpClientMode()
        => string.Equals(NormalizeMode(Environment.GetEnvironmentVariable(ClientModeEnvironmentVariable)), HttpClientMode, StringComparison.Ordinal)
            || string.Equals(NormalizeMode(Environment.GetEnvironmentVariable(LegacyDesktopClientModeEnvironmentVariable)), HttpClientMode, StringComparison.Ordinal);

    private static string? NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        return mode.Trim().ToLowerInvariant();
    }
}
