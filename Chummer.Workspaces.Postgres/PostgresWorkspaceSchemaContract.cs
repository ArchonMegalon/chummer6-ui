using System.Text.RegularExpressions;
using Npgsql;

namespace Chummer.Workspaces.Postgres;

internal static partial class PostgresWorkspaceSchemaContract
{
    private static readonly ExpectedColumn[] MigrationColumns =
    [
        new("version", "int4", IsNullable: false, HasDefault: false),
        new("name", "text", IsNullable: false, HasDefault: false),
        new("checksum_sha256", "text", IsNullable: false, HasDefault: false),
        new("applied_at_utc", "timestamptz", IsNullable: false, HasDefault: true),
    ];

    private static readonly ExpectedColumn[] WorkspaceColumns =
    [
        new("owner_key", "bytea", IsNullable: false, HasDefault: false),
        new("workspace_id", "text", IsNullable: false, HasDefault: false),
        new("document_json", "jsonb", IsNullable: false, HasDefault: false),
        new("document_sha256", "bytea", IsNullable: false, HasDefault: false),
        new("content_revision", "int8", IsNullable: false, HasDefault: false),
        new("saved_revision", "int8", IsNullable: false, HasDefault: false),
        new("updated_at_utc", "timestamptz", IsNullable: false, HasDefault: true),
    ];

    private static readonly ExpectedColumn[] DeletionJournalColumns =
    [
        new("operation_id", "uuid", IsNullable: false, HasDefault: false),
        new("owner_key", "bytea", IsNullable: false, HasDefault: false),
        new("subject_kind", "text", IsNullable: false, HasDefault: false),
        new("subject_key", "bytea", IsNullable: false, HasDefault: false),
        new("content_revision", "int8", IsNullable: true, HasDefault: false),
        new("deleted_at_utc", "timestamptz", IsNullable: false, HasDefault: false),
        new("replay_expires_at_utc", "timestamptz", IsNullable: false, HasDefault: false),
        new("audit_expires_at_utc", "timestamptz", IsNullable: false, HasDefault: false),
        new("receipt_sha256", "bytea", IsNullable: false, HasDefault: false),
    ];

    private static readonly IReadOnlyDictionary<string, string> ExpectedConstraints =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema_migrations_pkey"] = "PRIMARY KEY (version)",
            ["schema_migrations_name_key"] = "UNIQUE (name)",
            ["schema_migrations_version_check"] = "CHECK (version > 0)",
            ["schema_migrations_name_check"] =
                "CHECK (char_length(name) >= 1 AND char_length(name) <= 256)",
            ["schema_migrations_checksum_sha256_check"] =
                "CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$')",
            ["workspaces_pkey"] = "PRIMARY KEY (owner_key, workspace_id)",
            ["workspaces_owner_key_check"] = "CHECK (octet_length(owner_key) = 32)",
            ["workspaces_workspace_id_check"] =
                "CHECK (char_length(workspace_id) >= 1 AND char_length(workspace_id) <= 256)",
            ["workspaces_document_json_check"] =
                "CHECK (jsonb_typeof(document_json) = 'object')",
            ["workspaces_document_sha256_check"] =
                "CHECK (octet_length(document_sha256) = 32)",
            ["workspaces_content_revision_check"] = "CHECK (content_revision > 0)",
            ["workspaces_check"] =
                "CHECK (saved_revision >= 0 AND saved_revision <= content_revision)",
            ["workspace_deletion_journal_pkey"] = "PRIMARY KEY (operation_id)",
            ["deletion_journal_owner_key_length"] =
                "CHECK (octet_length(owner_key) = 32)",
            ["deletion_journal_subject_kind"] =
                "CHECK (subject_kind = ANY (ARRAY['workspace', 'owner']))",
            ["deletion_journal_subject_key_length"] =
                "CHECK (octet_length(subject_key) = 32)",
            ["deletion_journal_content_revision"] =
                "CHECK (content_revision IS NULL OR content_revision > 0)",
            ["deletion_journal_replay_window"] =
                "CHECK (replay_expires_at_utc > deleted_at_utc)",
            ["deletion_journal_audit_window"] =
                "CHECK (audit_expires_at_utc > replay_expires_at_utc)",
            ["deletion_journal_receipt_length"] =
                "CHECK (octet_length(receipt_sha256) = 32)",
        };

    public static PostgresWorkspaceSchemaValidation Validate(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        bool requireWritablePrimary)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var problems = new List<string>();

        using (NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            SELECT
                NOT pg_is_in_recovery()
                    AND current_setting('transaction_read_only') = 'off',
                to_regclass('chummer_build.schema_migrations') IS NOT NULL,
                to_regclass('chummer_build.workspaces') IS NOT NULL,
                to_regclass('chummer_build.workspace_deletion_journal') IS NOT NULL,
                to_regclass('chummer_build.ix_chummer_build_workspaces_owner_updated') IS NOT NULL,
                to_regclass('chummer_build.ix_chummer_build_workspace_deletion_replay') IS NOT NULL,
                to_regclass('chummer_build.ix_chummer_build_workspace_deletion_audit_expiry') IS NOT NULL
            """))
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                problems.Add("database_state_unavailable");
            }
            else
            {
                if (requireWritablePrimary && !reader.GetBoolean(0))
                    problems.Add("writable_primary_required");
                if (!reader.GetBoolean(1))
                    problems.Add("schema_migrations_missing");
                if (!reader.GetBoolean(2))
                    problems.Add("workspaces_table_missing");
                if (!reader.GetBoolean(3))
                    problems.Add("workspace_deletion_journal_missing");
                if (!reader.GetBoolean(4))
                    problems.Add("owner_updated_index_missing");
                if (!reader.GetBoolean(5))
                    problems.Add("deletion_replay_index_missing");
                if (!reader.GetBoolean(6))
                    problems.Add("deletion_audit_expiry_index_missing");
            }
        }

        if (problems.Any(problem => problem.EndsWith("_missing", StringComparison.Ordinal)))
        {
            return new PostgresWorkspaceSchemaValidation(false, 0, problems);
        }

        int appliedVersion = ValidateLedger(connection, commandTimeoutSeconds, problems);
        ValidateColumns(
            connection,
            commandTimeoutSeconds,
            "schema_migrations",
            MigrationColumns,
            problems);
        ValidateColumns(
            connection,
            commandTimeoutSeconds,
            "workspaces",
            WorkspaceColumns,
            problems);
        ValidateColumns(
            connection,
            commandTimeoutSeconds,
            "workspace_deletion_journal",
            DeletionJournalColumns,
            problems);
        ValidateConstraints(connection, commandTimeoutSeconds, problems);
        ValidateIndex(connection, commandTimeoutSeconds, problems);
        ValidateBoundaryObjects(connection, commandTimeoutSeconds, problems);

        return new PostgresWorkspaceSchemaValidation(
            problems.Count == 0,
            appliedVersion,
            problems);
    }

    private static int ValidateLedger(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        ICollection<string> problems)
    {
        IReadOnlyList<PostgresWorkspaceMigration> expected =
            PostgresWorkspaceMigrationCatalog.Load();
        var applied = new Dictionary<int, (string Name, string Checksum)>();
        using NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            SELECT version, name, checksum_sha256
            FROM chummer_build.schema_migrations
            ORDER BY version
            """);
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                applied[reader.GetInt32(0)] = (reader.GetString(1), reader.GetString(2));
            }
        }

        foreach (PostgresWorkspaceMigration migration in expected)
        {
            if (!applied.TryGetValue(migration.Version, out var ledgerEntry))
            {
                problems.Add($"migration_{migration.Version}_missing");
            }
            else if (!string.Equals(ledgerEntry.Name, migration.Name, StringComparison.Ordinal))
            {
                problems.Add($"migration_{migration.Version}_name_mismatch");
            }
            else if (!string.Equals(
                         ledgerEntry.Checksum,
                         migration.ChecksumSha256,
                         StringComparison.Ordinal))
            {
                problems.Add($"migration_{migration.Version}_checksum_mismatch");
            }
        }

        foreach (int version in applied.Keys.Except(expected.Select(static item => item.Version)))
        {
            problems.Add($"migration_{version}_unknown");
        }

        return applied.Keys.DefaultIfEmpty(0).Max();
    }

    private static void ValidateColumns(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        string table,
        IReadOnlyList<ExpectedColumn> expected,
        ICollection<string> problems)
    {
        var actual = new List<ExpectedColumn>();
        using NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            SELECT
                column_name,
                udt_name,
                is_nullable = 'YES',
                column_default IS NOT NULL
            FROM information_schema.columns
            WHERE table_schema = 'chummer_build'
              AND table_name = @table
            ORDER BY ordinal_position
            """);
        command.Parameters.AddWithValue("table", table);
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                actual.Add(new ExpectedColumn(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetBoolean(3)));
            }
        }

        if (!actual.SequenceEqual(expected))
        {
            problems.Add($"{table}_columns_drifted");
        }
    }

    private static void ValidateConstraints(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        ICollection<string> problems)
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        using NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            SELECT constraint_entry.conname, pg_get_constraintdef(constraint_entry.oid, true)
            FROM pg_constraint AS constraint_entry
            JOIN pg_class AS relation ON relation.oid = constraint_entry.conrelid
            JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = 'chummer_build'
              AND relation.relname IN ('schema_migrations', 'workspaces', 'workspace_deletion_journal')
            ORDER BY constraint_entry.conname
            """);
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                actual[reader.GetString(0)] = NormalizeDefinition(reader.GetString(1));
            }
        }

        if (actual.Count != ExpectedConstraints.Count
            || ExpectedConstraints.Any(expected =>
                !actual.TryGetValue(expected.Key, out string? definition)
                || !string.Equals(
                    definition,
                    NormalizeDefinition(expected.Value),
                    StringComparison.Ordinal)))
        {
            problems.Add("constraints_drifted");
        }
    }

    private static void ValidateIndex(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        ICollection<string> problems)
    {
        using NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            SELECT pg_get_indexdef('chummer_build.ix_chummer_build_workspaces_owner_updated'::regclass)
            """);
        string actual = NormalizeDefinition(Convert.ToString(command.ExecuteScalar()) ?? string.Empty);
        string expected = NormalizeDefinition("""
            CREATE INDEX ix_chummer_build_workspaces_owner_updated
            ON chummer_build.workspaces USING btree
            (owner_key, updated_at_utc DESC, workspace_id)
            """);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            problems.Add("owner_updated_index_drifted");
        }

        ValidateIndexDefinition(
            connection,
            commandTimeoutSeconds,
            "chummer_build.ix_chummer_build_workspace_deletion_replay",
            """
            CREATE INDEX ix_chummer_build_workspace_deletion_replay
            ON chummer_build.workspace_deletion_journal USING btree
            (owner_key, replay_expires_at_utc, subject_kind, subject_key)
            """,
            "deletion_replay_index_drifted",
            problems);
        ValidateIndexDefinition(
            connection,
            commandTimeoutSeconds,
            "chummer_build.ix_chummer_build_workspace_deletion_audit_expiry",
            """
            CREATE INDEX ix_chummer_build_workspace_deletion_audit_expiry
            ON chummer_build.workspace_deletion_journal USING btree
            (audit_expires_at_utc)
            """,
            "deletion_audit_expiry_index_drifted",
            problems);
    }

    private static void ValidateIndexDefinition(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        string qualifiedIndex,
        string expected,
        string problem,
        ICollection<string> problems)
    {
        using NpgsqlCommand command = CreateCommand(
            connection,
            commandTimeoutSeconds,
            "SELECT pg_get_indexdef(@index::regclass)");
        command.Parameters.AddWithValue("index", qualifiedIndex);
        string actual = NormalizeDefinition(Convert.ToString(command.ExecuteScalar()) ?? string.Empty);
        if (!string.Equals(actual, NormalizeDefinition(expected), StringComparison.Ordinal))
        {
            problems.Add(problem);
        }
    }

    private static void ValidateBoundaryObjects(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        ICollection<string> problems)
    {
        using NpgsqlCommand command = CreateCommand(connection, commandTimeoutSeconds, """
            WITH schema_owner AS (
                SELECT oid AS schema_oid, nspowner AS owner_oid
                FROM pg_namespace
                WHERE nspname = 'chummer_build'
            ), default_object_types(object_type) AS (
                VALUES ('r'::"char"), ('S'::"char"), ('f'::"char")
            ), effective_global_defaults AS (
                SELECT
                    object_type,
                    COALESCE(default_acl.defaclacl, acldefault(object_type, schema_owner.owner_oid)) AS acl
                FROM schema_owner
                CROSS JOIN default_object_types
                LEFT JOIN pg_default_acl AS default_acl
                  ON default_acl.defaclnamespace = 0
                 AND default_acl.defaclrole = schema_owner.owner_oid
                 AND default_acl.defaclobjtype = object_type
            )
            SELECT
                NOT EXISTS (
                    SELECT 1
                    FROM pg_class AS relation
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    WHERE namespace.nspname = 'chummer_build'
                      AND relation.relkind IN ('r', 'p', 'S', 'v', 'm', 'f')
                      AND (
                          relation.relkind <> 'r'
                          OR relation.relname NOT IN ('schema_migrations', 'workspaces', 'workspace_deletion_journal'))
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM pg_proc AS procedure
                    JOIN pg_namespace AS namespace ON namespace.oid = procedure.pronamespace
                    WHERE namespace.nspname = 'chummer_build'
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM pg_trigger AS trigger
                    JOIN pg_class AS relation ON relation.oid = trigger.tgrelid
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    WHERE namespace.nspname = 'chummer_build'
                      AND NOT trigger.tgisinternal
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM pg_class AS relation
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    WHERE namespace.nspname = 'chummer_build'
                      AND relation.relkind IN ('r', 'p')
                      AND (relation.relrowsecurity OR relation.relforcerowsecurity)
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM pg_namespace AS namespace
                    CROSS JOIN LATERAL aclexplode(
                        COALESCE(namespace.nspacl, acldefault('n', namespace.nspowner))) AS acl
                    WHERE namespace.nspname = 'chummer_build'
                      AND acl.grantee = 0
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM pg_class AS relation
                    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                    CROSS JOIN LATERAL aclexplode(
                        COALESCE(relation.relacl, acldefault('r', relation.relowner))) AS acl
                    WHERE namespace.nspname = 'chummer_build'
                      AND relation.relkind IN ('r', 'p')
                      AND acl.grantee = 0
                ),
                NOT EXISTS (
                    SELECT 1
                    FROM effective_global_defaults
                    CROSS JOIN LATERAL aclexplode(effective_global_defaults.acl) AS acl
                    WHERE acl.grantee = 0
                    UNION ALL
                    SELECT 1
                    FROM pg_default_acl AS default_acl
                    JOIN schema_owner
                      ON schema_owner.schema_oid = default_acl.defaclnamespace
                     AND schema_owner.owner_oid = default_acl.defaclrole
                    CROSS JOIN LATERAL aclexplode(default_acl.defaclacl) AS acl
                    WHERE acl.grantee = 0
                )
            """);
        using NpgsqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            problems.Add("boundary_validation_unavailable");
            return;
        }

        string[] problemNames =
        [
            "unexpected_relations_present",
            "unexpected_functions_present",
            "unexpected_triggers_present",
            "row_security_posture_drifted",
            "public_schema_acl_present",
            "public_table_acl_present",
            "unsafe_default_acl_present",
        ];
        for (int index = 0; index < problemNames.Length; index++)
        {
            if (!reader.GetBoolean(index))
            {
                problems.Add(problemNames[index]);
            }
        }
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        int commandTimeoutSeconds,
        string sql)
    {
        NpgsqlCommand command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = sql;
        return command;
    }

    private static string NormalizeDefinition(string definition)
        => DefinitionWhitespaceAndParens()
            .Replace(definition, string.Empty)
            .Replace("::text", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

    [GeneratedRegex(@"[\s()]", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex DefinitionWhitespaceAndParens();

    private sealed record ExpectedColumn(
        string Name,
        string Type,
        bool IsNullable,
        bool HasDefault);
}
