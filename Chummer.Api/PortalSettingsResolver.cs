using Microsoft.Extensions.Configuration;

namespace Chummer.Api;

public static class PortalSettingsResolver
{
    public static string ResolveSetting(
        IConfiguration configuration,
        string configurationKey,
        string environmentVariable,
        string fallbackAliasValue)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariable);

        string? direct = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct.Trim();
        }

        string? configured = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return fallbackAliasValue;
    }
}
