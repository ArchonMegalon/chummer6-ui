using Npgsql;

namespace Chummer.Workspaces.Postgres;

public sealed class PostgresWorkspaceStoreOptions
{
    public const int DefaultListPageSize = 128;
    public const int MaximumListPageSize = 1024;
    public const int DefaultMaximumPoolSize = 10;
    public const int MaximumAllowedPoolSize = 256;

    public static TimeSpan DefaultCommandTimeout { get; } = TimeSpan.FromSeconds(5);

    public PostgresWorkspaceStoreOptions(string connectionString)
        : this(
            connectionString,
            DefaultCommandTimeout,
            requireLeastPrivilege: true,
            listPageSize: DefaultListPageSize,
            maximumPoolSize: DefaultMaximumPoolSize)
    {
    }

    public PostgresWorkspaceStoreOptions(
        string connectionString,
        TimeSpan commandTimeout,
        bool requireLeastPrivilege = true)
        : this(
            connectionString,
            commandTimeout,
            requireLeastPrivilege,
            DefaultListPageSize,
            DefaultMaximumPoolSize)
    {
    }

    public PostgresWorkspaceStoreOptions(
        string connectionString,
        TimeSpan commandTimeout,
        bool requireLeastPrivilege,
        int listPageSize)
        : this(
            connectionString,
            commandTimeout,
            requireLeastPrivilege,
            listPageSize,
            DefaultMaximumPoolSize)
    {
    }

    public PostgresWorkspaceStoreOptions(
        string connectionString,
        TimeSpan commandTimeout,
        bool requireLeastPrivilege,
        int listPageSize,
        int maximumPoolSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (commandTimeout <= TimeSpan.Zero
            || commandTimeout.TotalSeconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandTimeout),
                "The PostgreSQL workspace command timeout must be positive and bounded.");
        }

        if (listPageSize is < 1 or > MaximumListPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(listPageSize),
                $"The PostgreSQL workspace list page size must be between 1 and {MaximumListPageSize}.");
        }

        if (maximumPoolSize is < 1 or > MaximumAllowedPoolSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPoolSize),
                $"The PostgreSQL workspace maximum pool size must be between 1 and {MaximumAllowedPoolSize}.");
        }

        ConnectionString = connectionString;
        CommandTimeout = commandTimeout;
        RequireLeastPrivilege = requireLeastPrivilege;
        ListPageSize = listPageSize;
        MaximumPoolSize = maximumPoolSize;
    }

    internal string ConnectionString { get; }

    public TimeSpan CommandTimeout { get; }

    public bool RequireLeastPrivilege { get; }

    public int ListPageSize { get; }

    public int MaximumPoolSize { get; }

    public override string ToString()
        => $"{nameof(PostgresWorkspaceStoreOptions)} {{ CommandTimeout = {CommandTimeout}, RequireLeastPrivilege = {RequireLeastPrivilege}, ListPageSize = {ListPageSize}, MaximumPoolSize = {MaximumPoolSize} }}";
}

internal static class PostgresWorkspaceDataSourceFactory
{
    private const string ReadWriteTargetSession = "read-write";

    public static NpgsqlDataSource Create(PostgresWorkspaceStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            int timeoutSeconds = ToCommandTimeoutSeconds(options.CommandTimeout);
            var connectionString = new NpgsqlConnectionStringBuilder(options.ConnectionString)
            {
                Timeout = timeoutSeconds,
                CommandTimeout = timeoutSeconds,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = options.MaximumPoolSize
            };
            connectionString.TargetSessionAttributes = IsMultiHost(connectionString.Host)
                ? ReadWriteTargetSession
                : null;
            return NpgsqlDataSource.Create(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or FormatException
                                          or OverflowException
                                          or InvalidOperationException
                                          or NotSupportedException)
        {
            throw new ArgumentException(
                "The PostgreSQL workspace connection configuration is invalid.",
                nameof(options));
        }
    }

    public static int ToCommandTimeoutSeconds(TimeSpan timeout)
        => checked((int)Math.Max(1, Math.Ceiling(timeout.TotalSeconds)));

    private static bool IsMultiHost(string? host)
        => !string.IsNullOrWhiteSpace(host)
            && host.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length > 1;
}
