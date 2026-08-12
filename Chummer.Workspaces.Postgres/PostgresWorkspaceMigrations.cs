using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Chummer.Workspaces.Postgres;

public sealed record PostgresWorkspaceMigration(
    int Version,
    string Name,
    string Sql,
    string ChecksumSha256);

public sealed record PostgresWorkspaceSchemaValidation(
    bool Valid,
    int AppliedVersion,
    IReadOnlyList<string> Problems);

public static class PostgresWorkspaceMigrationCatalog
{
    private static readonly (int Version, string Name)[] MigrationNames =
    [
        (1, "V001__chummer_build_workspace.sql"),
        (2, "V002__workspace_deletion_journal.sql")
    ];

    public static int ExpectedVersion => MigrationNames[^1].Version;

    public static IReadOnlyList<PostgresWorkspaceMigration> Load()
    {
        Assembly assembly = typeof(PostgresWorkspaceMigrationCatalog).Assembly;
        string[] resources = assembly.GetManifestResourceNames();
        return MigrationNames.Select(item =>
        {
            string resourceName = resources.Single(name =>
                name.EndsWith(item.Name, StringComparison.Ordinal));
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded PostgreSQL workspace migration {item.Version} is unavailable.");
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            // Keep the ledger checksum stable across LF and CRLF checkouts.
            string sql = reader.ReadToEnd()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            string checksum = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
                .ToLowerInvariant();
            return new PostgresWorkspaceMigration(item.Version, item.Name, sql, checksum);
        }).ToArray();
    }
}

/// <summary>
/// Release-job migrator for the dedicated chummer_build schema. The runtime
/// identity must not own this schema or write its migration ledger.
/// </summary>
public sealed class PostgresWorkspaceMigrator : IDisposable
{
    private const long AdvisoryLockKey = 0x4348554D4255494C;
    private readonly NpgsqlDataSource _dataSource;
    private readonly int _commandTimeoutSeconds;
    private readonly IReadOnlyList<PostgresWorkspaceMigration> _migrations;

    public PostgresWorkspaceMigrator(PostgresWorkspaceStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataSource = PostgresWorkspaceDataSourceFactory.Create(options);
        _commandTimeoutSeconds = PostgresWorkspaceDataSourceFactory.ToCommandTimeoutSeconds(
            options.CommandTimeout);
        _migrations = PostgresWorkspaceMigrationCatalog.Load();
    }

    public void Migrate()
    {
        try
        {
            using NpgsqlConnection connection = _dataSource.OpenConnection();
            AcquireMigrationLock(connection);
            try
            {
                Bootstrap(connection);
                ValidateMigrationLedger(connection, requireAllKnownMigrations: false);
                foreach (PostgresWorkspaceMigration migration in _migrations)
                {
                    ApplyMigration(connection, migration);
                }
                ValidateMigrationLedger(connection, requireAllKnownMigrations: true);
                PostgresWorkspaceSchemaValidation validation =
                    PostgresWorkspaceSchemaContract.Validate(
                        connection,
                        _commandTimeoutSeconds,
                        requireWritablePrimary: true);
                if (!validation.Valid)
                {
                    throw new InvalidOperationException(
                        "PostgreSQL workspace schema validation failed after migration: "
                        + string.Join(",", validation.Problems)
                        + ".");
                }
            }
            finally
            {
                // A session advisory lock is also released when the connection
                // closes. An unlock transport failure must not obscure a known
                // migration result or leak provider diagnostics.
                TryReleaseMigrationLock(connection);
            }
        }
        catch (NpgsqlException)
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace migration is unavailable.");
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace migration is unavailable.");
        }
    }

    public PostgresWorkspaceSchemaValidation Validate()
    {
        try
        {
            using NpgsqlConnection connection = _dataSource.OpenConnection();
            return PostgresWorkspaceSchemaContract.Validate(
                connection,
                _commandTimeoutSeconds,
                requireWritablePrimary: true);
        }
        catch (NpgsqlException)
        {
            return new PostgresWorkspaceSchemaValidation(
                false,
                0,
                ["postgres_unavailable"]);
        }
        catch (TimeoutException)
        {
            return new PostgresWorkspaceSchemaValidation(
                false,
                0,
                ["postgres_unavailable"]);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                          or InvalidCastException)
        {
            return new PostgresWorkspaceSchemaValidation(
                false,
                0,
                ["schema_validation_failed"]);
        }
    }

    public void Dispose() => _dataSource.Dispose();

    private void Bootstrap(NpgsqlConnection connection)
    {
        using NpgsqlCommand command = CreateCommand(connection, transaction: null, """
            CREATE SCHEMA IF NOT EXISTS chummer_build;
            REVOKE ALL ON SCHEMA chummer_build FROM PUBLIC;
            CREATE TABLE IF NOT EXISTS chummer_build.schema_migrations (
                version integer PRIMARY KEY CHECK (version > 0),
                name text NOT NULL UNIQUE CHECK (char_length(name) BETWEEN 1 AND 256),
                checksum_sha256 text NOT NULL CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$'),
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
            );
            REVOKE ALL ON chummer_build.schema_migrations FROM PUBLIC;
            ALTER DEFAULT PRIVILEGES IN SCHEMA chummer_build
                REVOKE ALL ON TABLES FROM PUBLIC;
            ALTER DEFAULT PRIVILEGES IN SCHEMA chummer_build
                REVOKE ALL ON SEQUENCES FROM PUBLIC;
            ALTER DEFAULT PRIVILEGES
                REVOKE ALL ON FUNCTIONS FROM PUBLIC;
            """);
        command.ExecuteNonQuery();
    }

    private void ApplyMigration(
        NpgsqlConnection connection,
        PostgresWorkspaceMigration migration)
    {
        using NpgsqlTransaction transaction = connection.BeginTransaction();
        using NpgsqlCommand read = CreateCommand(connection, transaction, """
            SELECT checksum_sha256
            FROM chummer_build.schema_migrations
            WHERE version = @version
            """);
        read.Parameters.AddWithValue("version", migration.Version);
        object? existing = read.ExecuteScalar();
        if (existing is string checksum)
        {
            if (!string.Equals(checksum, migration.ChecksumSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PostgreSQL workspace migration {migration.Version} checksum does not match the applied migration.");
            }

            transaction.Commit();
            return;
        }

        using NpgsqlCommand apply = CreateCommand(connection, transaction, migration.Sql);
        apply.ExecuteNonQuery();

        using NpgsqlCommand record = CreateCommand(connection, transaction, """
            INSERT INTO chummer_build.schema_migrations(
                version,
                name,
                checksum_sha256)
            VALUES (@version, @name, @checksum)
            """);
        record.Parameters.AddWithValue("version", migration.Version);
        record.Parameters.AddWithValue("name", migration.Name);
        record.Parameters.AddWithValue("checksum", migration.ChecksumSha256);
        record.ExecuteNonQuery();
        transaction.Commit();
    }

    private void ValidateMigrationLedger(
        NpgsqlConnection connection,
        bool requireAllKnownMigrations)
    {
        using NpgsqlCommand command = CreateCommand(connection, transaction: null, """
            SELECT version, name, checksum_sha256
            FROM chummer_build.schema_migrations
            ORDER BY version
            """);
        var applied = new Dictionary<int, (string Name, string Checksum)>();
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                applied[reader.GetInt32(0)] = (reader.GetString(1), reader.GetString(2));
            }
        }

        bool unknownVersion = applied.Keys.Any(version =>
            _migrations.All(migration => migration.Version != version));
        bool knownMigrationDrift = _migrations.Any(migration =>
            applied.TryGetValue(
                migration.Version,
                out var ledgerEntry)
            && (!string.Equals(ledgerEntry.Name, migration.Name, StringComparison.Ordinal)
                || !string.Equals(
                    ledgerEntry.Checksum,
                    migration.ChecksumSha256,
                    StringComparison.Ordinal)));
        bool missingKnown = requireAllKnownMigrations
            && _migrations.Any(migration => !applied.ContainsKey(migration.Version));
        if (unknownVersion || knownMigrationDrift || missingKnown)
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace migration ledger validation failed.");
        }
    }

    private void AcquireMigrationLock(NpgsqlConnection connection)
    {
        using NpgsqlCommand command = CreateCommand(
            connection,
            transaction: null,
            "SELECT pg_advisory_lock(@key)");
        command.Parameters.AddWithValue("key", AdvisoryLockKey);
        command.ExecuteNonQuery();
    }

    private void TryReleaseMigrationLock(NpgsqlConnection connection)
    {
        try
        {
            using NpgsqlCommand command = CreateCommand(
                connection,
                transaction: null,
                "SELECT pg_advisory_unlock(@key)");
            command.Parameters.AddWithValue("key", AdvisoryLockKey);
            command.ExecuteNonQuery();
        }
        catch (NpgsqlException)
        {
        }
        catch (TimeoutException)
        {
        }
    }

    private NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql)
    {
        NpgsqlCommand command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}

/// <summary>
/// Provisions and validates the least-privilege runtime role independently of
/// the migration identity. Role names are validated before identifier quoting.
/// </summary>
public sealed class PostgresWorkspaceRuntimeGrantHelper : IDisposable
{
    private static readonly Regex RuntimeRolePattern = new(
        "^[a-z_][a-z0-9_]{0,62}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly NpgsqlDataSource _dataSource;
    private readonly int _commandTimeoutSeconds;

    public PostgresWorkspaceRuntimeGrantHelper(PostgresWorkspaceStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataSource = PostgresWorkspaceDataSourceFactory.Create(options);
        _commandTimeoutSeconds = PostgresWorkspaceDataSourceFactory.ToCommandTimeoutSeconds(
            options.CommandTimeout);
    }

    public void GrantRuntimePrivileges(string runtimeRole)
    {
        if (string.IsNullOrWhiteSpace(runtimeRole)
            || !RuntimeRolePattern.IsMatch(runtimeRole))
        {
            throw new ArgumentException(
                "The PostgreSQL workspace runtime role name is invalid.",
                nameof(runtimeRole));
        }

        string quotedRole;
        using (var builder = new NpgsqlCommandBuilder())
        {
            quotedRole = builder.QuoteIdentifier(runtimeRole);
        }

        try
        {
            using NpgsqlConnection connection = _dataSource.OpenConnection();
            using NpgsqlTransaction transaction = connection.BeginTransaction();
            using NpgsqlCommand command = connection.CreateCommand();
            command.CommandTimeout = _commandTimeoutSeconds;
            command.Transaction = transaction;
            command.CommandText = $"""
                REVOKE ALL ON SCHEMA chummer_build FROM {quotedRole};
                GRANT USAGE ON SCHEMA chummer_build TO {quotedRole};

                REVOKE ALL ON ALL TABLES IN SCHEMA chummer_build FROM {quotedRole};
                GRANT SELECT ON chummer_build.schema_migrations TO {quotedRole};
                GRANT SELECT, INSERT, UPDATE, DELETE
                    ON chummer_build.workspaces TO {quotedRole};
                GRANT SELECT, INSERT, DELETE
                    ON chummer_build.workspace_deletion_journal TO {quotedRole};

                REVOKE CREATE ON SCHEMA chummer_build FROM {quotedRole};
                REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                    ON chummer_build.schema_migrations FROM {quotedRole};
                REVOKE TRUNCATE, REFERENCES, TRIGGER
                    ON chummer_build.workspaces FROM {quotedRole};
                REVOKE UPDATE, TRUNCATE, REFERENCES, TRIGGER
                    ON chummer_build.workspace_deletion_journal FROM {quotedRole};
                """;
            command.ExecuteNonQuery();
            if (!ValidateRole(
                    connection,
                    _commandTimeoutSeconds,
                    runtimeRole,
                    transaction))
            {
                throw new InvalidOperationException(
                    "PostgreSQL workspace runtime privilege validation failed.");
            }
            transaction.Commit();
        }
        catch (NpgsqlException)
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace runtime privilege provisioning is unavailable.");
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                "PostgreSQL workspace runtime privilege provisioning is unavailable.");
        }
    }

    public bool ValidateRuntimePrivileges(string runtimeRole)
    {
        if (string.IsNullOrWhiteSpace(runtimeRole)
            || !RuntimeRolePattern.IsMatch(runtimeRole))
        {
            return false;
        }

        try
        {
            using NpgsqlConnection connection = _dataSource.OpenConnection();
            return ValidateRole(connection, _commandTimeoutSeconds, runtimeRole);
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void Dispose() => _dataSource.Dispose();

    internal static bool ValidateCurrentRole(
        NpgsqlConnection connection,
        int commandTimeoutSeconds)
        => ValidateRole(connection, commandTimeoutSeconds, runtimeRole: null);

    private static bool ValidateRole(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        string? runtimeRole,
        NpgsqlTransaction? transaction = null)
    {
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.Transaction = transaction;
        command.CommandText = """
            WITH target_role AS (
                SELECT
                    oid,
                    rolname,
                    rolsuper,
                    rolbypassrls,
                    rolcreatedb,
                    rolcreaterole
                FROM pg_roles
                WHERE rolname = COALESCE(@role, current_user::text)
            )
            SELECT
                NOT target_role.rolsuper
                AND NOT target_role.rolbypassrls
                AND NOT target_role.rolcreatedb
                AND NOT target_role.rolcreaterole
                AND NOT EXISTS (
                    SELECT 1
                    FROM pg_auth_members AS membership
                    WHERE membership.member = target_role.oid)
                AND has_database_privilege(
                    target_role.rolname,
                    current_database(),
                    'CONNECT')
                AND NOT has_database_privilege(
                    target_role.rolname,
                    current_database(),
                    'CREATE')
                AND NOT EXISTS (
                    SELECT 1
                    FROM pg_namespace AS namespace
                    WHERE namespace.nspname = 'chummer_build'
                      AND namespace.nspowner = target_role.oid)
                AND NOT EXISTS (
                    SELECT 1
                    FROM pg_class AS relation
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    WHERE namespace.nspname = 'chummer_build'
                      AND relation.relowner = target_role.oid)
                AND has_schema_privilege(
                    target_role.rolname,
                    'chummer_build',
                    'USAGE')
                AND NOT has_schema_privilege(
                    target_role.rolname,
                    'chummer_build',
                    'CREATE')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'SELECT')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'INSERT')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'UPDATE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'DELETE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'TRUNCATE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'REFERENCES')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.schema_migrations',
                    'TRIGGER')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'SELECT')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'INSERT')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'UPDATE')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'DELETE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'TRUNCATE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'REFERENCES')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspaces',
                    'TRIGGER')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'SELECT')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'INSERT')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'UPDATE')
                AND has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'DELETE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'TRUNCATE')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'REFERENCES')
                AND NOT has_table_privilege(
                    target_role.rolname,
                    'chummer_build.workspace_deletion_journal',
                    'TRIGGER')
                AND NOT EXISTS (
                    SELECT 1
                    FROM pg_default_acl AS default_acl
                    JOIN pg_namespace AS namespace
                      ON namespace.oid = default_acl.defaclnamespace
                    CROSS JOIN LATERAL aclexplode(default_acl.defaclacl) AS acl
                    WHERE namespace.nspname = 'chummer_build'
                      AND acl.grantee = target_role.oid)
            FROM target_role
            """;
        command.Parameters.Add("role", NpgsqlDbType.Text).Value =
            runtimeRole is null ? DBNull.Value : runtimeRole;
        return Convert.ToBoolean(command.ExecuteScalar());
    }
}
