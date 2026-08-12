using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Workspaces;
using Chummer.Workspaces.Postgres;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Win32.SafeHandles;
using Npgsql;

namespace Chummer.Blazor.Services;

public sealed record HostedBuildWorkspaceStoreSelection(
    string Provider,
    bool MultiInstanceSafe,
    string DurabilityBoundary);

public static class HostedBuildWorkspaceStoreConfiguration
{
    public const string ProviderConfigurationKey = "CHUMMER_BUILD_WORKSPACE_STORE_PROVIDER";
    public const string ExpectedReplicaCountConfigurationKey = "CHUMMER_BUILD_EXPECTED_REPLICA_COUNT";
    public const string ConnectionStringConfigurationKey = "CHUMMER_BUILD_POSTGRES_CONNECTION_STRING";
    public const string CommandTimeoutSecondsConfigurationKey = "CHUMMER_BUILD_POSTGRES_COMMAND_TIMEOUT_SECONDS";
    public const string MaximumPoolSizeConfigurationKey = "CHUMMER_BUILD_POSTGRES_MAX_POOL_SIZE";
    public const string AggregateConnectionBudgetConfigurationKey = "CHUMMER_BUILD_POSTGRES_AGGREGATE_CONNECTION_BUDGET";
    public const string ProductionSecretDirectory = "/run/secrets/chummer-config";

    private const string FileProvider = "file";
    private const string PostgresProvider = "postgresql";
    private const int DefaultCommandTimeoutSeconds = 5;
    private const int MaximumCommandTimeoutSeconds = 60;
    private const int MaximumReplicaCount = 1024;
    private const int DefaultAggregateConnectionBudget =
        PostgresWorkspaceStoreOptions.DefaultMaximumPoolSize;
    private const int MaximumAggregateConnectionBudget =
        MaximumReplicaCount * PostgresWorkspaceStoreOptions.MaximumAllowedPoolSize;
    private const int MaximumProductionSecretBytes = 32768;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static IServiceCollection AddHostedBuildWorkspaceStore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string productionSecretDirectory = ProductionSecretDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string provider = NormalizeProvider(configuration[ProviderConfigurationKey], environment);
        int expectedReplicaCount = ParsePositiveInt(
            configuration[ExpectedReplicaCountConfigurationKey],
            ExpectedReplicaCountConfigurationKey,
            defaultValue: 1,
            maximum: MaximumReplicaCount);

        services.RemoveAll<HostedBuildWorkspaceStoreSelection>();
        services.RemoveAll<IWorkspacePrivacyLifecycleStore>();
        if (provider == FileProvider)
        {
            if (expectedReplicaCount != 1)
            {
                throw new InvalidOperationException(
                    $"{ProviderConfigurationKey}=file supports exactly one Hosted Build instance; configure the dedicated PostgreSQL provider before increasing {ExpectedReplicaCountConfigurationKey}.");
            }

            services.AddSingleton(new HostedBuildWorkspaceStoreSelection(
                Provider: FileProvider,
                MultiInstanceSafe: false,
                DurabilityBoundary: "single_instance_local_filesystem"));
            return services;
        }

        if (environment.IsProduction()
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ConnectionStringConfigurationKey)))
        {
            throw new InvalidOperationException(
                $"Production must load {ConnectionStringConfigurationKey} from the mounted KeyPerFile secret directory, not a process environment variable.");
        }

        string connectionString = environment.IsProduction()
            ? ReadProductionSecret(productionSecretDirectory)
            : NormalizeRequiredSecret(
                configuration[ConnectionStringConfigurationKey],
                ConnectionStringConfigurationKey);

        ValidateTransport(connectionString, environment);
        int commandTimeoutSeconds = ParsePositiveInt(
            configuration[CommandTimeoutSecondsConfigurationKey],
            CommandTimeoutSecondsConfigurationKey,
            DefaultCommandTimeoutSeconds,
            MaximumCommandTimeoutSeconds);
        int maximumPoolSize = ParseConnectionBudget(
            configuration[MaximumPoolSizeConfigurationKey],
            MaximumPoolSizeConfigurationKey,
            environment.IsProduction(),
            PostgresWorkspaceStoreOptions.DefaultMaximumPoolSize,
            PostgresWorkspaceStoreOptions.MaximumAllowedPoolSize);
        int aggregateConnectionBudget = ParseConnectionBudget(
            configuration[AggregateConnectionBudgetConfigurationKey],
            AggregateConnectionBudgetConfigurationKey,
            environment.IsProduction(),
            DefaultAggregateConnectionBudget,
            MaximumAggregateConnectionBudget);
        if (expectedReplicaCount > aggregateConnectionBudget / maximumPoolSize)
        {
            throw new InvalidOperationException(
                $"{ExpectedReplicaCountConfigurationKey} multiplied by {MaximumPoolSizeConfigurationKey} must not exceed {AggregateConnectionBudgetConfigurationKey}.");
        }

        var options = new PostgresWorkspaceStoreOptions(
            connectionString,
            TimeSpan.FromSeconds(commandTimeoutSeconds),
            requireLeastPrivilege: environment.IsProduction(),
            listPageSize: PostgresWorkspaceStoreOptions.DefaultListPageSize,
            maximumPoolSize: maximumPoolSize);

        services.RemoveAll<IWorkspaceStore>();
        services.RemoveAll<IWorkspaceStoreReadinessProbe>();
        services.AddSingleton(options);
        services.AddSingleton<PostgresWorkspaceStore>();
        services.AddSingleton<IWorkspaceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresWorkspaceStore>());
        services.AddSingleton<IWorkspaceStoreReadinessProbe>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresWorkspaceStore>());
        services.AddSingleton<IWorkspacePrivacyLifecycleStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresWorkspaceStore>());
        services.AddSingleton(new HostedBuildWorkspaceStoreSelection(
            Provider: PostgresProvider,
            MultiInstanceSafe: true,
            DurabilityBoundary: "shared_transactional_postgresql"));
        return services;
    }

    private static string NormalizeProvider(string? configured, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    $"Production must explicitly set {ProviderConfigurationKey} to file or postgresql; file is single-instance only.");
            }

            return FileProvider;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            FileProvider => FileProvider,
            "postgres" or PostgresProvider => PostgresProvider,
            _ => throw new InvalidOperationException(
                $"{ProviderConfigurationKey} must be file or postgresql.")
        };
    }

    private static string NormalizeRequiredSecret(string? configured, string key)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException($"{key} is required when the PostgreSQL workspace provider is selected.");
        }

        return configured.Trim();
    }

    private static int ParsePositiveInt(
        string? configured,
        string key,
        int defaultValue,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return defaultValue;
        }

        if (!int.TryParse(configured.Trim(), out int value)
            || value <= 0
            || value > maximum)
        {
            throw new InvalidOperationException($"{key} must be an integer from 1 through {maximum}.");
        }

        return value;
    }

    private static int ParseConnectionBudget(
        string? configured,
        string key,
        bool requireExplicit,
        int defaultValue,
        int maximum)
    {
        if (requireExplicit && string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"Production PostgreSQL selection must explicitly set {key}.");
        }

        return ParsePositiveInt(configured, key, defaultValue, maximum);
    }

    private static void ValidateTransport(string connectionString, IHostEnvironment environment)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException($"{ConnectionStringConfigurationKey} is invalid.");
        }

        if (environment.IsProduction()
            && builder.SslMode is not SslMode.VerifyFull)
        {
            throw new InvalidOperationException(
                "Hosted Build production PostgreSQL transport must verify the CA chain and server hostname (VerifyFull).");
        }
    }

    private static string ReadProductionSecret(string secretDirectory)
    {
        if (string.IsNullOrWhiteSpace(secretDirectory)
            || !Path.IsPathFullyQualified(secretDirectory))
        {
            throw new InvalidOperationException(
                "Hosted Build production PostgreSQL secret directory is invalid.");
        }

        try
        {
            string fullSecretDirectory = Path.GetFullPath(secretDirectory);
            ValidateProductionSecretDirectory(fullSecretDirectory);

            string secretPath = Path.Combine(
                fullSecretDirectory,
                ConnectionStringConfigurationKey);
            ValidateProductionSecretPathBeforeOpen(secretPath);

            // Portable .NET does not expose descriptor-relative O_NOFOLLOW or Unix owner ids.
            // The path checks reject stable links and unsafe immediate-parent permissions, while
            // all content, length, attributes, and Unix-mode checks below bind to one opened
            // handle. A rename by an actor controlling the path between the check and
            // File.OpenHandle, or an unsafe writable ancestor above the configured directory,
            // remains a deployment boundary and must be prevented by the secret mount.
            using SafeFileHandle handle = File.OpenHandle(
                secretPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.SequentialScan);

            long expectedLength = ValidateOpenedProductionSecret(handle);
            return ReadBoundedProductionSecret(handle, expectedLength);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
        {
            throw new InvalidOperationException(
                "Hosted Build production PostgreSQL connection secret is unavailable or unsafe.");
        }
    }

    private static void ValidateProductionSecretDirectory(string secretDirectory)
    {
        var directory = new DirectoryInfo(secretDirectory);
        directory.Refresh();
        FileAttributes attributes = directory.Attributes;
        if (!directory.Exists
            || (attributes & FileAttributes.Directory) == 0
            || (attributes & FileAttributes.ReparsePoint) != 0
            || directory.LinkTarget is not null)
        {
            throw UnsafeProductionSecret();
        }

        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode forbiddenDirectoryMode =
                UnixFileMode.GroupWrite
                | UnixFileMode.OtherWrite;
            if ((File.GetUnixFileMode(secretDirectory) & forbiddenDirectoryMode) != 0)
            {
                throw new InvalidOperationException(
                    "Hosted Build production PostgreSQL secret directory permissions are unsafe.");
            }
        }
    }

    private static void ValidateProductionSecretPathBeforeOpen(string secretPath)
    {
        FileAttributes attributes = File.GetAttributes(secretPath);
        if (HasUnsafeSecretFileAttributes(attributes))
        {
            throw UnsafeProductionSecret();
        }

        var file = new FileInfo(secretPath);
        file.Refresh();
        if (!file.Exists
            || file.LinkTarget is not null
            || file.Length is <= 0 or > MaximumProductionSecretBytes)
        {
            throw UnsafeProductionSecret();
        }
    }

    private static long ValidateOpenedProductionSecret(SafeFileHandle handle)
    {
        FileAttributes attributes = File.GetAttributes(handle);
        if (handle.IsInvalid || HasUnsafeSecretFileAttributes(attributes))
        {
            throw UnsafeProductionSecret();
        }

        long length = RandomAccess.GetLength(handle);
        if (length is <= 0 or > MaximumProductionSecretBytes)
        {
            throw UnsafeProductionSecret();
        }

        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode forbiddenFileMode =
                UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherWrite
                | UnixFileMode.OtherExecute;
            UnixFileMode mode = File.GetUnixFileMode(handle);
            if ((mode & UnixFileMode.UserRead) == 0
                || (mode & forbiddenFileMode) != 0)
            {
                throw new InvalidOperationException(
                    "Hosted Build production PostgreSQL connection secret permissions are unsafe.");
            }
        }

        return length;
    }

    private static string ReadBoundedProductionSecret(
        SafeFileHandle handle,
        long expectedLength)
    {
        byte[] buffer = new byte[MaximumProductionSecretBytes + 1];
        try
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = RandomAccess.Read(
                    handle,
                    buffer.AsSpan(totalRead),
                    fileOffset: totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            long finalLength = RandomAccess.GetLength(handle);
            if (totalRead is <= 0 or > MaximumProductionSecretBytes
                || totalRead != expectedLength
                || finalLength != expectedLength)
            {
                throw UnsafeProductionSecret();
            }

            ReadOnlySpan<byte> secretBytes = buffer.AsSpan(0, totalRead);
            if (secretBytes.Length >= 3
                && secretBytes[0] == 0xEF
                && secretBytes[1] == 0xBB
                && secretBytes[2] == 0xBF)
            {
                secretBytes = secretBytes[3..];
            }

            return NormalizeRequiredSecret(
                StrictUtf8.GetString(secretBytes),
                ConnectionStringConfigurationKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static bool HasUnsafeSecretFileAttributes(FileAttributes attributes)
        => (attributes & (FileAttributes.Directory
                          | FileAttributes.Device
                          | FileAttributes.ReparsePoint)) != 0;

    private static InvalidOperationException UnsafeProductionSecret()
        => new(
            "Hosted Build production PostgreSQL connection secret is unavailable or unsafe.");
}
