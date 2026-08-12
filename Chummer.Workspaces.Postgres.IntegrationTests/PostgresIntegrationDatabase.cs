using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace Chummer.Workspaces.Postgres.IntegrationTests;

internal sealed class PostgresIntegrationDatabase : IAsyncDisposable
{
    internal const string ConnectionStringEnvironmentVariable =
        "CHUMMER_BUILD_POSTGRES_TEST_CONNECTION_STRING";

    private readonly string _maintenanceConnectionString;
    private readonly List<string> _runtimeRoles = [];
    private bool _disposed;

    private PostgresIntegrationDatabase(
        string databaseName,
        string maintenanceConnectionString,
        string connectionString)
    {
        DatabaseName = databaseName;
        _maintenanceConnectionString = maintenanceConnectionString;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<PostgresIntegrationDatabase> CreateAsync()
    {
        string? configured = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            Assert.Inconclusive(
                $"Set {ConnectionStringEnvironmentVariable} to an isolated PostgreSQL test-cluster "
                + "administrator connection with CREATEDB and CREATEROLE. No database was changed.");
            throw new InvalidOperationException("MSTest did not stop an inconclusive test.");
        }

        NpgsqlConnectionStringBuilder maintenanceBuilder;
        try
        {
            maintenanceBuilder = HardenConnectionString(configured);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or FormatException
                                          or OverflowException)
        {
            Assert.Fail(
                $"{ConnectionStringEnvironmentVariable} is not a valid PostgreSQL connection string.");
            throw;
        }

        string databaseName = $"chummer_build_it_{Guid.NewGuid():N}";
        string quotedDatabase = QuoteIdentifier(databaseName);
        try
        {
            await using var connection = new NpgsqlConnection(
                maintenanceBuilder.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE {quotedDatabase}";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (PostgresException exception)
        {
            Assert.Fail(
                "The PostgreSQL integration-test identity could not create an isolated database "
                + $"(SQLSTATE {exception.SqlState}). Grant CREATEDB on a disposable test cluster.");
            throw;
        }
        catch (NpgsqlException)
        {
            Assert.Fail(
                "The PostgreSQL integration-test cluster is unavailable. No connection details are reported.");
            throw;
        }

        var databaseBuilder = new NpgsqlConnectionStringBuilder(
            maintenanceBuilder.ConnectionString)
        {
            Database = databaseName,
            ApplicationName = "chummer-build-postgres-integration"
        };
        return new PostgresIntegrationDatabase(
            databaseName,
            maintenanceBuilder.ConnectionString,
            databaseBuilder.ConnectionString);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<long> QueryInt64Async(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        object? value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task ExecuteWithParametersAsync(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<PostgresRuntimeRole> CreateRuntimeRoleAsync()
    {
        string roleName = $"chummer_build_it_role_{Guid.NewGuid():N}";
        string password = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
        string quotedRole = QuoteIdentifier(roleName);
        string quotedPassword = QuoteLiteral(password);
        string quotedDatabase = QuoteIdentifier(DatabaseName);

        await using (var connection = new NpgsqlConnection(
                         _maintenanceConnectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE ROLE {quotedRole}
                    LOGIN
                    PASSWORD {quotedPassword}
                    NOSUPERUSER
                    NOCREATEDB
                    NOCREATEROLE
                    NOINHERIT
                    NOREPLICATION;
                GRANT CONNECT ON DATABASE {quotedDatabase} TO {quotedRole};
                """;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        _runtimeRoles.Add(roleName);
        var runtimeBuilder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = roleName,
            Password = password,
            Pooling = false,
            ApplicationName = "chummer-build-postgres-runtime-integration"
        };
        return new PostgresRuntimeRole(roleName, runtimeBuilder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NpgsqlConnection.ClearAllPools();
        try
        {
            await using var connection = new NpgsqlConnection(
                _maintenanceConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using (NpgsqlCommand dropDatabase = connection.CreateCommand())
            {
                dropDatabase.CommandText =
                    $"DROP DATABASE IF EXISTS {QuoteIdentifier(DatabaseName)} WITH (FORCE)";
                await dropDatabase.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            foreach (string runtimeRole in _runtimeRoles)
            {
                await using NpgsqlCommand dropRole = connection.CreateCommand();
                dropRole.CommandText =
                    $"DROP ROLE IF EXISTS {QuoteIdentifier(runtimeRole)}";
                await dropRole.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            throw new InvalidOperationException(
                "PostgreSQL integration-test cleanup failed; inspect the test cluster for "
                + $"database {DatabaseName}.");
        }
    }

    private static NpgsqlConnectionStringBuilder HardenConnectionString(string value)
    {
        var builder = new NpgsqlConnectionStringBuilder(value)
        {
            Pooling = false,
            IncludeErrorDetail = false,
            Timeout = 10,
            CommandTimeout = 10,
            ApplicationName = "chummer-build-postgres-integration-admin"
        };
        return builder;
    }

    private static string QuoteIdentifier(string value)
    {
        using var builder = new NpgsqlCommandBuilder();
        return builder.QuoteIdentifier(value);
    }

    private static string QuoteLiteral(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}

internal sealed record PostgresRuntimeRole(
    string RoleName,
    string ConnectionString);

internal static class IntegrationAssert
{
    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            Assert.Fail(
                $"Expected {typeof(TException).Name}, but observed {exception.GetType().Name}.");
        }

        Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
        throw new InvalidOperationException("MSTest did not stop a failed assertion.");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            Assert.Fail(
                $"Expected {typeof(TException).Name}, but observed {exception.GetType().Name}.");
        }

        Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
        throw new InvalidOperationException("MSTest did not stop a failed assertion.");
    }
}
