using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Workspaces.Postgres;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using NpgsqlTypes;

namespace Chummer.Workspaces.Postgres.IntegrationTests;

[TestClass]
[TestCategory("PostgreSQLIntegration")]
public sealed class PostgresWorkspaceStoreIntegrationTests
{
    [TestMethod]
    public async Task Store_SameWorkspaceIdIsIsolatedByOwnerWithoutPersistingRawOwnerIds()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var ownerA = new OwnerScope($"tenant-alpha-{Guid.NewGuid():N}");
        var ownerB = new OwnerScope($"tenant-bravo-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("shared-id");
        using var store = Store(database.ConnectionString);

        Assert.IsTrue(store.CreateWorkspaceDocument(ownerA, id, Document("alpha")).Success);
        Assert.IsTrue(store.CreateWorkspaceDocument(ownerB, id, Document("bravo")).Success);

        Assert.AreEqual("alpha", store.Get(ownerA, id).Value?.Document.Content);
        Assert.AreEqual("bravo", store.Get(ownerB, id).Value?.Document.Content);
        Assert.HasCount(1, store.List(ownerA));
        Assert.HasCount(1, store.List(ownerB));
        Assert.AreEqual(
            2L,
            await database.QueryInt64Async(
                "SELECT count(DISTINCT encode(owner_key, 'hex')) FROM chummer_build.workspaces")
                .ConfigureAwait(false));
        Assert.AreEqual(
            0L,
            await database.QueryInt64Async("""
                SELECT count(*)
                FROM chummer_build.workspaces
                WHERE octet_length(owner_key) <> 32
                """).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Store_TwoInstancesProduceExactlyOneCasWinner()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"cas-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("cas-workspace");
        using var firstStore = Store(database.ConnectionString);
        using var secondStore = Store(database.ConnectionString);
        Assert.IsTrue(firstStore.CreateWorkspaceDocument(owner, id, Document("initial")).Success);

        Task<Chummer.Application.Workspaces.WorkspaceStoreMutationResult> first = Task.Run(() =>
            firstStore.ReplaceWorkspaceDocument(owner, id, 1, Document("first-writer")));
        Task<Chummer.Application.Workspaces.WorkspaceStoreMutationResult> second = Task.Run(() =>
            secondStore.ReplaceWorkspaceDocument(owner, id, 1, Document("second-writer")));
        Chummer.Application.Workspaces.WorkspaceStoreMutationResult[] results =
            await Task.WhenAll(first, second).ConfigureAwait(false);

        Assert.AreEqual(1, results.Count(static result => result.Success));
        Assert.AreEqual(
            1,
            results.Count(static result => result.Outcome == WorkspaceOperationOutcome.Conflict));
        Chummer.Application.Workspaces.WorkspaceStoreReadResult read = firstStore.Get(owner, id);
        Assert.IsNotNull(read.Value, "The winning CAS update was not persisted.");
        Chummer.Application.Workspaces.WorkspaceStoredDocument stored = read.Value!;
        Assert.AreEqual(2L, stored.ContentRevision);
        Assert.IsTrue(stored.Document.Content is "first-writer" or "second-writer");
    }

    [TestMethod]
    public async Task Store_ConcurrentConditionalCreateProducesExactlyOneWinner()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"create-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("conditional-create");
        using var firstStore = Store(database.ConnectionString);
        using var secondStore = Store(database.ConnectionString);

        Task<Chummer.Application.Workspaces.WorkspaceStoreMutationResult> first = Task.Run(() =>
            firstStore.CreateWorkspaceDocument(owner, id, Document("first")));
        Task<Chummer.Application.Workspaces.WorkspaceStoreMutationResult> second = Task.Run(() =>
            secondStore.CreateWorkspaceDocument(owner, id, Document("second")));
        Chummer.Application.Workspaces.WorkspaceStoreMutationResult[] results =
            await Task.WhenAll(first, second).ConfigureAwait(false);

        Assert.AreEqual(1, results.Count(static result => result.Success));
        Assert.AreEqual(
            1,
            results.Count(static result => result.Outcome == WorkspaceOperationOutcome.Conflict));
        Assert.IsTrue(firstStore.Get(owner, id).Value?.Document.Content is "first" or "second");
    }

    [TestMethod]
    public async Task Store_ReopeningProviderPreservesWorkspaceAndRevisions()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"restart-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("restart-workspace");

        using (PostgresWorkspaceStore first = Store(database.ConnectionString))
        {
            Assert.IsTrue(first.CreateWorkspaceDocument(owner, id, Document("before-restart")).Success);
            Assert.IsTrue(first.ReplaceWorkspaceDocument(owner, id, 1, Document("durable")).Success);
            Assert.IsTrue(first.SaveCheckpoint(owner, id, 2).Success);
        }

        using PostgresWorkspaceStore reopened = Store(database.ConnectionString);
        Chummer.Application.Workspaces.WorkspaceStoreReadResult read = reopened.Get(owner, id);
        Assert.IsNotNull(read.Value, "The workspace did not survive provider restart.");
        Chummer.Application.Workspaces.WorkspaceStoredDocument stored = read.Value!;
        Assert.AreEqual("durable", stored.Document.Content);
        Assert.AreEqual(2L, stored.ContentRevision);
        Assert.AreEqual(2L, stored.SavedRevision);
    }

    [TestMethod]
    public async Task ReadinessProbe_IsSideEffectFreeAndLeavesNoProbeRows()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"readiness-{Guid.NewGuid():N}");
        using var store = Store(database.ConnectionString);

        store.Probe(owner);

        Assert.IsEmpty(store.List(owner));
        Assert.AreEqual(
            0L,
            await database.QueryInt64Async("""
                SELECT count(*)
                FROM chummer_build.workspaces
                WHERE workspace_id LIKE 'readiness-%'
                """).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task ReadinessProbe_FailsWhenMigrationLedgerIsCorrupt()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            UPDATE chummer_build.schema_migrations
            SET checksum_sha256 = repeat('0', 64)
            WHERE version = 1
            """).ConfigureAwait(false);
        using var store = Store(database.ConnectionString);

        _ = IntegrationAssert.Throws<InvalidOperationException>(() =>
            store.Probe(new OwnerScope($"readiness-ledger-{Guid.NewGuid():N}")));
    }

    [TestMethod]
    public async Task ReadinessProbe_FailsWhenMigrationLedgerHasUnknownVersion()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            INSERT INTO chummer_build.schema_migrations(version, name, checksum_sha256)
            VALUES (999, 'V999__unexpected.sql', repeat('a', 64))
            """).ConfigureAwait(false);
        using var store = Store(database.ConnectionString);

        _ = IntegrationAssert.Throws<InvalidOperationException>(() =>
            store.Probe(new OwnerScope($"readiness-ledger-unknown-{Guid.NewGuid():N}")));
    }

    [TestMethod]
    public async Task ReadinessProbe_FailsWhenRequiredIndexIsMissing()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync(
            "DROP INDEX chummer_build.ix_chummer_build_workspaces_owner_updated").ConfigureAwait(false);
        using var store = Store(database.ConnectionString);

        _ = IntegrationAssert.Throws<InvalidOperationException>(() =>
            store.Probe(new OwnerScope($"readiness-index-{Guid.NewGuid():N}")));
    }

    [TestMethod]
    public async Task ReadinessProbe_FailsWhenRequiredConstraintIsMissing()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            DO $block$
            DECLARE
                required_constraint text;
            BEGIN
                SELECT constraint_row.conname
                INTO required_constraint
                FROM pg_catalog.pg_constraint AS constraint_row
                INNER JOIN pg_catalog.pg_class AS table_row
                    ON table_row.oid = constraint_row.conrelid
                INNER JOIN pg_catalog.pg_namespace AS namespace_row
                    ON namespace_row.oid = table_row.relnamespace
                WHERE namespace_row.nspname = 'chummer_build'
                  AND table_row.relname = 'workspaces'
                  AND constraint_row.contype = 'c'
                ORDER BY constraint_row.conname
                LIMIT 1;

                IF required_constraint IS NULL THEN
                    RAISE EXCEPTION 'required test constraint was not found';
                END IF;

                EXECUTE format(
                    'ALTER TABLE chummer_build.workspaces DROP CONSTRAINT %I',
                    required_constraint);
            END
            $block$;
            """).ConfigureAwait(false);
        using var store = Store(database.ConnectionString);

        _ = IntegrationAssert.Throws<InvalidOperationException>(() =>
            store.Probe(new OwnerScope($"readiness-constraint-{Guid.NewGuid():N}")));
    }

    [TestMethod]
    public async Task ReadinessProbe_ForcedDeleteFailureStillRemovesProbeRow()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            CREATE FUNCTION chummer_build.reject_readiness_delete()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF OLD.workspace_id LIKE 'readiness-%' THEN
                    RAISE EXCEPTION 'forced readiness delete failure';
                END IF;
                RETURN OLD;
            END
            $function$;

            CREATE TRIGGER reject_readiness_delete
            BEFORE DELETE ON chummer_build.workspaces
            FOR EACH ROW
            EXECUTE FUNCTION chummer_build.reject_readiness_delete();
            """).ConfigureAwait(false);
        using var store = Store(database.ConnectionString);

        _ = IntegrationAssert.Throws<InvalidOperationException>(() =>
            store.Probe(new OwnerScope($"readiness-cleanup-{Guid.NewGuid():N}")));
        Assert.AreEqual(
            0L,
            await database.QueryInt64Async("""
                SELECT count(*)
                FROM chummer_build.workspaces
                WHERE workspace_id LIKE 'readiness-%'
                """).ConfigureAwait(false),
            "A failed readiness probe must not leave an orphan workspace row.");
    }

    [TestMethod]
    public async Task Store_PayloadHashCorruptionIsDetectedByReadAndCas()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"corrupt-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("corrupt-payload");
        using var store = Store(database.ConnectionString);
        Assert.IsTrue(store.CreateWorkspaceDocument(owner, id, Document("trusted")).Success);
        await database.ExecuteAsync("""
            UPDATE chummer_build.workspaces
            SET document_sha256 = decode(repeat('00', 32), 'hex')
            """).ConfigureAwait(false);

        Assert.AreEqual(WorkspaceOperationOutcome.Corrupt, store.Get(owner, id).Outcome);
        Assert.AreEqual(
            WorkspaceOperationOutcome.Corrupt,
            store.ReplaceWorkspaceDocument(owner, id, 1, Document("replacement")).Outcome);
    }

    [TestMethod]
    [DataRow("rulesetId", "\"sr6\"")]
    [DataRow("workspaceSchemaVersion", "2")]
    [DataRow("payloadKind", "\"tampered-kind\"")]
    [DataRow("format", "\"Json\"")]
    public async Task Store_MetadataOnlyTamperingIsDetected(
        string jsonField,
        string replacementJson)
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"metadata-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("metadata-tamper");
        using var store = Store(database.ConnectionString);
        Assert.IsTrue(store.CreateWorkspaceDocument(owner, id, Document("trusted")).Success);

        await database.ExecuteWithParametersAsync(
            """
            UPDATE chummer_build.workspaces
            SET document_json = jsonb_set(
                document_json,
                ARRAY[@field]::text[],
                @replacement::jsonb,
                false)
            """,
            new NpgsqlParameter("field", NpgsqlDbType.Text) { Value = jsonField },
            new NpgsqlParameter("replacement", NpgsqlDbType.Text) { Value = replacementJson })
            .ConfigureAwait(false);

        Assert.AreEqual(
            WorkspaceOperationOutcome.Corrupt,
            store.Get(owner, id).Outcome,
            $"Tampering with {jsonField} must invalidate the full persisted document hash.");
    }

    [TestMethod]
    public async Task RuntimeRole_HasOnlyRequiredDmlAndStoreRemainsFunctional()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        PostgresRuntimeRole runtime = await database.CreateRuntimeRoleAsync().ConfigureAwait(false);
        var adminOptions = PostgresWorkspaceMigrationIntegrationTests.Options(
            database.ConnectionString);
        using (var grants = new PostgresWorkspaceRuntimeGrantHelper(adminOptions))
        {
            grants.GrantRuntimePrivileges(runtime.RoleName);
            Assert.IsTrue(grants.ValidateRuntimePrivileges(runtime.RoleName));
        }

        var runtimeOptions = new PostgresWorkspaceStoreOptions(
            runtime.ConnectionString,
            TimeSpan.FromSeconds(10),
            requireLeastPrivilege: true);
        var owner = new OwnerScope($"runtime-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("runtime-workspace");
        using (var store = new PostgresWorkspaceStore(runtimeOptions))
        {
            Assert.IsTrue(store.CreateWorkspaceDocument(owner, id, Document("created")).Success);
            Assert.IsTrue(store.Get(owner, id).Success);
            Assert.IsTrue(store.ReplaceWorkspaceDocument(owner, id, 1, Document("updated")).Success);
            Assert.IsTrue(store.SaveCheckpoint(owner, id, 2).Success);
            Assert.IsTrue(store.Delete(owner, id, 2).Success);
        }

        PostgresException schemaDdl = await IntegrationAssert.ThrowsAsync<PostgresException>(
            () => ExecuteAsRuntimeAsync(
                runtime.ConnectionString,
                "CREATE TABLE chummer_build.forbidden(id integer)")).ConfigureAwait(false);
        Assert.AreEqual("42501", schemaDdl.SqlState);

        PostgresException ledgerWrite = await IntegrationAssert.ThrowsAsync<PostgresException>(
            () => ExecuteAsRuntimeAsync(
                runtime.ConnectionString,
                """
                INSERT INTO chummer_build.schema_migrations(version, name, checksum_sha256)
                VALUES (99, 'forbidden', repeat('f', 64))
                """)).ConfigureAwait(false);
        Assert.AreEqual("42501", ledgerWrite.SqlState);
    }

    private static PostgresWorkspaceStore Store(string connectionString)
        => new(PostgresWorkspaceMigrationIntegrationTests.Options(connectionString));

    private static WorkspaceDocument Document(string content)
        => new(content, "sr5", WorkspaceDocumentFormat.NativeXml);

    private static async Task ExecuteAsRuntimeAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
