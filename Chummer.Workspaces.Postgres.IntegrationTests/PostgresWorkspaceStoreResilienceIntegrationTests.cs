using Chummer.Application.Workspaces;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Workspaces.Postgres;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using System.Reflection;

namespace Chummer.Workspaces.Postgres.IntegrationTests;

[TestClass]
[TestCategory("PostgreSQLIntegration")]
public sealed class PostgresWorkspaceStoreResilienceIntegrationTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(PostgresWorkspaceStoreOptions.MaximumListPageSize + 1)]
    public void Options_RejectUnboundedListPageSizes(int pageSize)
    {
        _ = IntegrationAssert.Throws<ArgumentOutOfRangeException>(() =>
            new PostgresWorkspaceStoreOptions(
                "Host=localhost;Database=chummer_build;Username=unused",
                TimeSpan.FromSeconds(1),
                requireLeastPrivilege: false,
                listPageSize: pageSize));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(PostgresWorkspaceStoreOptions.MaximumAllowedPoolSize + 1)]
    public void Options_RejectUnboundedMaximumPoolSizes(int maximumPoolSize)
    {
        _ = IntegrationAssert.Throws<ArgumentOutOfRangeException>(() =>
            new PostgresWorkspaceStoreOptions(
                "Host=localhost;Database=chummer_build;Username=unused",
                TimeSpan.FromSeconds(1),
                requireLeastPrivilege: false,
                listPageSize: PostgresWorkspaceStoreOptions.DefaultListPageSize,
                maximumPoolSize: maximumPoolSize));
    }

    [TestMethod]
    public void DataSource_SingleHostOverridesPoolSettingsWithoutUnsupportedSessionTargeting()
    {
        var options = new PostgresWorkspaceStoreOptions(
            "Host=localhost;Database=chummer_build;Username=unused;"
            + "Pooling=false;Minimum Pool Size=100;Maximum Pool Size=200;"
            + "Target Session Attributes=any",
            TimeSpan.FromSeconds(1),
            requireLeastPrivilege: false,
            listPageSize: PostgresWorkspaceStoreOptions.DefaultListPageSize,
            maximumPoolSize: 3);
        using var store = new PostgresWorkspaceStore(options);
        FieldInfo? dataSourceField = typeof(PostgresWorkspaceStore).GetField(
            "_dataSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(dataSourceField);
        var dataSource = dataSourceField!.GetValue(store) as NpgsqlDataSource;
        Assert.IsNotNull(dataSource);
        var configured = new NpgsqlConnectionStringBuilder(dataSource!.ConnectionString);

        Assert.IsTrue(configured.Pooling);
        Assert.AreEqual(0, configured.MinPoolSize);
        Assert.AreEqual(3, configured.MaxPoolSize);
        Assert.IsTrue(string.IsNullOrEmpty(configured.TargetSessionAttributes));
    }

    [TestMethod]
    public void DataSource_MultiHostOverridesPoolSettingsAndTargetsReadWriteSessions()
    {
        var options = new PostgresWorkspaceStoreOptions(
            "Host=primary.invalid,standby.invalid;Database=chummer_build;Username=unused;"
            + "Pooling=false;Minimum Pool Size=100;Maximum Pool Size=200;"
            + "Target Session Attributes=any",
            TimeSpan.FromSeconds(1),
            requireLeastPrivilege: false,
            listPageSize: PostgresWorkspaceStoreOptions.DefaultListPageSize,
            maximumPoolSize: 3);
        using var store = new PostgresWorkspaceStore(options);
        FieldInfo? dataSourceField = typeof(PostgresWorkspaceStore).GetField(
            "_dataSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(dataSourceField);
        var dataSource = dataSourceField!.GetValue(store) as NpgsqlDataSource;
        Assert.IsNotNull(dataSource);
        var configured = new NpgsqlConnectionStringBuilder(dataSource!.ConnectionString);

        Assert.IsTrue(configured.Pooling);
        Assert.AreEqual(0, configured.MinPoolSize);
        Assert.AreEqual(3, configured.MaxPoolSize);
        Assert.AreEqual("read-write", configured.TargetSessionAttributes);
    }

    [TestMethod]
    public async Task List_KeysetPagesRemainCompleteOrderedAndOwnerScoped()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"paged-{Guid.NewGuid():N}");
        var otherOwner = new OwnerScope($"paged-other-{Guid.NewGuid():N}");
        using var store = Store(database.ConnectionString, listPageSize: 2);

        string[] insertedIds = ["echo", "alpha", "delta", "bravo", "charlie"];
        foreach (string value in insertedIds)
        {
            Assert.IsTrue(
                store.CreateWorkspaceDocument(
                    owner,
                    new CharacterWorkspaceId(value),
                    Document(value)).Success);
        }

        Assert.IsTrue(
            store.CreateWorkspaceDocument(
                otherOwner,
                new CharacterWorkspaceId("not-visible"),
                Document("other-owner")).Success);
        await database.ExecuteAsync("""
            UPDATE chummer_build.workspaces
            SET updated_at_utc = TIMESTAMPTZ '2026-07-15 00:00:00+00'
            """).ConfigureAwait(false);

        IReadOnlyList<WorkspaceStoreEntry> listed = store.List(owner);

        CollectionAssert.AreEqual(
            new[] { "alpha", "bravo", "charlie", "delta", "echo" },
            listed.Select(static entry => entry.Id.Value).ToArray());
        Assert.IsFalse(listed.Any(static entry => entry.Id.Value == "not-visible"));
    }

    [TestMethod]
    public async Task Mutations_PreserveConditionalCreateCheckpointAndDeleteReplayContract()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"idempotency-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("idempotency-contract");
        using var store = Store(database.ConnectionString);

        WorkspaceStoreMutationResult created =
            store.CreateWorkspaceDocument(owner, id, Document("original"));
        WorkspaceStoreMutationResult duplicate =
            store.CreateWorkspaceDocument(owner, id, Document("original"));
        Assert.IsTrue(created.Success);
        Assert.AreEqual(WorkspaceOperationOutcome.Conflict, duplicate.Outcome);

        WorkspaceStoreMutationResult firstCheckpoint = store.SaveCheckpoint(owner, id, 1);
        WorkspaceStoreMutationResult repeatedCheckpoint = store.SaveCheckpoint(owner, id, 1);
        Assert.IsTrue(firstCheckpoint.Success);
        Assert.IsTrue(repeatedCheckpoint.Success);
        Assert.IsNotNull(firstCheckpoint.Entry);
        Assert.IsNotNull(repeatedCheckpoint.Entry);
        Assert.AreEqual(1L, repeatedCheckpoint.Entry.Value.SavedRevision);
        Assert.AreEqual(
            firstCheckpoint.Entry.Value.LastUpdatedUtc,
            repeatedCheckpoint.Entry.Value.LastUpdatedUtc,
            "A replayed checkpoint must not manufacture another state transition.");

        Assert.IsTrue(store.Delete(owner, id, 1).Success);
        Assert.AreEqual(
            WorkspaceOperationOutcome.Missing,
            store.Delete(owner, id, 1).Outcome,
            "Delete replay remains Missing until the product defines durable tombstone semantics.");
        Assert.AreEqual(
            0L,
            await database.QueryInt64Async(
                "SELECT count(*) FROM chummer_build.workspaces").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task List_RemainsMetadataOnlyWhileGetAndCasSurfaceDocumentCorruption()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"metadata-list-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("metadata-list");
        using var store = Store(database.ConnectionString);
        Assert.IsTrue(store.CreateWorkspaceDocument(owner, id, Document("trusted")).Success);
        await database.ExecuteAsync("""
            UPDATE chummer_build.workspaces
            SET document_sha256 = decode(repeat('00', 32), 'hex')
            """).ConfigureAwait(false);

        IReadOnlyList<WorkspaceStoreEntry> listed = store.List(owner);

        Assert.HasCount(1, listed);
        Assert.AreEqual(id, listed[0].Id);
        Assert.AreEqual(WorkspaceOperationOutcome.Corrupt, store.Get(owner, id).Outcome);
        Assert.AreEqual(
            WorkspaceOperationOutcome.Corrupt,
            store.ReplaceWorkspaceDocument(owner, id, 1, Document("replacement")).Outcome);
    }

    [TestMethod]
    public async Task Get_RetriesOneTransientReadFailureAndThenSucceeds()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"read-retry-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("read-retry");
        using (PostgresWorkspaceStore adminStore = Store(database.ConnectionString))
        {
            Assert.IsTrue(adminStore.CreateWorkspaceDocument(owner, id, Document("durable")).Success);
        }

        PostgresRuntimeRole runtime = await CreateRuntimeRoleWithGrantsAsync(database).ConfigureAwait(false);
        await InstallReadFailurePolicyAsync(
                database,
                runtime.RoleName,
                failOnlyFirstAttempt: true)
            .ConfigureAwait(false);
        using var store = Store(runtime.ConnectionString);

        WorkspaceStoreReadResult result = store.Get(owner, id);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual("durable", result.Value?.Document.Content);
        Assert.AreEqual(
            2L,
            await database.QueryInt64Async(
                "SELECT last_value FROM chummer_build.test_read_attempts").ConfigureAwait(false),
            "A read must make at most one bounded retry after a transient failure.");
    }

    [TestMethod]
    public async Task Replace_DoesNotRetryAnAmbiguousTransientMutationFailure()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresWorkspaceMigrationIntegrationTests.MigratedDatabaseAsync().ConfigureAwait(false);
        var owner = new OwnerScope($"write-no-retry-{Guid.NewGuid():N}");
        var id = new CharacterWorkspaceId("write-no-retry");
        using (PostgresWorkspaceStore adminStore = Store(database.ConnectionString))
        {
            Assert.IsTrue(adminStore.CreateWorkspaceDocument(owner, id, Document("original")).Success);
        }

        PostgresRuntimeRole runtime = await CreateRuntimeRoleWithGrantsAsync(database).ConfigureAwait(false);
        await InstallReadFailurePolicyAsync(
                database,
                runtime.RoleName,
                failOnlyFirstAttempt: false)
            .ConfigureAwait(false);
        using var store = Store(runtime.ConnectionString);

        WorkspaceStoreMutationResult result =
            store.ReplaceWorkspaceDocument(owner, id, 1, Document("replacement"));

        Assert.AreEqual(WorkspaceOperationOutcome.Unavailable, result.Outcome);
        Assert.AreEqual(
            1L,
            await database.QueryInt64Async(
                "SELECT last_value FROM chummer_build.test_read_attempts").ConfigureAwait(false),
            "A mutation must not be replayed after PostgreSQL reports an ambiguous transient failure.");
    }

    private static PostgresWorkspaceStore Store(
        string connectionString,
        int listPageSize = PostgresWorkspaceStoreOptions.DefaultListPageSize)
        => new(new PostgresWorkspaceStoreOptions(
            connectionString,
            TimeSpan.FromSeconds(10),
            requireLeastPrivilege: false,
            listPageSize: listPageSize));

    private static WorkspaceDocument Document(string content)
        => new(content, "sr5", WorkspaceDocumentFormat.NativeXml);

    private static async Task<PostgresRuntimeRole> CreateRuntimeRoleWithGrantsAsync(
        PostgresIntegrationDatabase database)
    {
        PostgresRuntimeRole runtime = await database.CreateRuntimeRoleAsync().ConfigureAwait(false);
        using var grants = new PostgresWorkspaceRuntimeGrantHelper(
            PostgresWorkspaceMigrationIntegrationTests.Options(database.ConnectionString));
        grants.GrantRuntimePrivileges(runtime.RoleName);
        return runtime;
    }

    private static async Task InstallReadFailurePolicyAsync(
        PostgresIntegrationDatabase database,
        string runtimeRole,
        bool failOnlyFirstAttempt)
    {
        string quotedRole;
        using (var builder = new NpgsqlCommandBuilder())
        {
            quotedRole = builder.QuoteIdentifier(runtimeRole);
        }

        string failurePredicate = failOnlyFirstAttempt
            ? "attempt = 1"
            : "TRUE";
        await database.ExecuteAsync($"""
            CREATE SEQUENCE chummer_build.test_read_attempts;

            CREATE FUNCTION chummer_build.test_allow_workspace_read()
            RETURNS boolean
            LANGUAGE plpgsql
            VOLATILE
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $function$
            DECLARE
                attempt bigint;
            BEGIN
                attempt := nextval('chummer_build.test_read_attempts'::regclass);
                IF {failurePredicate} THEN
                    RAISE EXCEPTION 'injected transient read failure'
                        USING ERRCODE = '40001';
                END IF;
                RETURN TRUE;
            END
            $function$;

            REVOKE ALL ON FUNCTION chummer_build.test_allow_workspace_read() FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION chummer_build.test_allow_workspace_read() TO {quotedRole};
            CREATE POLICY test_workspace_read_retry
                ON chummer_build.workspaces
                FOR SELECT
                TO {quotedRole}
                USING (chummer_build.test_allow_workspace_read());
            CREATE POLICY test_workspace_update_visibility
                ON chummer_build.workspaces
                FOR UPDATE
                TO {quotedRole}
                USING (TRUE)
                WITH CHECK (TRUE);
            ALTER TABLE chummer_build.workspaces ENABLE ROW LEVEL SECURITY;
            """).ConfigureAwait(false);
    }
}
