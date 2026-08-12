using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Workspaces.Postgres;

namespace Chummer.Workspaces.Postgres.IntegrationTests;

[TestClass]
[TestCategory("PostgreSQLIntegration")]
public sealed class PostgresWorkspaceMigrationIntegrationTests
{
    [TestMethod]
    public async Task Migration_EmptyDatabaseIsMigratedAndRepeatedRunIsIdempotent()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresIntegrationDatabase.CreateAsync().ConfigureAwait(false);
        var options = Options(database.ConnectionString);

        using var migrator = new PostgresWorkspaceMigrator(options);
        PostgresWorkspaceSchemaValidation before = migrator.Validate();
        Assert.IsFalse(before.Valid);
        CollectionAssert.Contains(before.Problems.ToArray(), "schema_migrations_missing");
        CollectionAssert.Contains(before.Problems.ToArray(), "workspaces_table_missing");

        migrator.Migrate();
        PostgresWorkspaceSchemaValidation first = migrator.Validate();
        Assert.IsTrue(first.Valid, string.Join(", ", first.Problems));
        Assert.AreEqual(PostgresWorkspaceMigrationCatalog.ExpectedVersion, first.AppliedVersion);

        migrator.Migrate();
        PostgresWorkspaceSchemaValidation second = migrator.Validate();
        Assert.IsTrue(second.Valid, string.Join(", ", second.Problems));
        Assert.AreEqual(
            (long)PostgresWorkspaceMigrationCatalog.ExpectedVersion,
            await database.QueryInt64Async(
                "SELECT count(*) FROM chummer_build.schema_migrations").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Migration_ConcurrentMigratorsSerializeAndWriteOneLedgerRowPerVersion()
    {
        await using PostgresIntegrationDatabase database =
            await PostgresIntegrationDatabase.CreateAsync().ConfigureAwait(false);
        var options = Options(database.ConnectionString);

        Task[] migrations = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                using var migrator = new PostgresWorkspaceMigrator(options);
                migrator.Migrate();
            }))
            .ToArray();
        await Task.WhenAll(migrations).ConfigureAwait(false);

        using var validator = new PostgresWorkspaceMigrator(options);
        PostgresWorkspaceSchemaValidation validation = validator.Validate();
        Assert.IsTrue(validation.Valid, string.Join(", ", validation.Problems));
        Assert.AreEqual(
            (long)PostgresWorkspaceMigrationCatalog.ExpectedVersion,
            await database.QueryInt64Async(
                "SELECT count(*) FROM chummer_build.schema_migrations").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Migration_ChecksumDriftFailsClosed()
    {
        await using PostgresIntegrationDatabase database =
            await MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            UPDATE chummer_build.schema_migrations
            SET checksum_sha256 = repeat('0', 64)
            WHERE version = 1
            """).ConfigureAwait(false);

        using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
        PostgresWorkspaceSchemaValidation validation = migrator.Validate();
        Assert.IsFalse(validation.Valid);
        CollectionAssert.Contains(validation.Problems.ToArray(), "migration_1_checksum_mismatch");
        _ = IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
    }

    [TestMethod]
    public async Task Migration_UnknownLedgerVersionFailsClosed()
    {
        await using PostgresIntegrationDatabase database =
            await MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync("""
            INSERT INTO chummer_build.schema_migrations(version, name, checksum_sha256)
            VALUES (999, 'V999__unexpected.sql', repeat('a', 64))
            """).ConfigureAwait(false);

        using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
        PostgresWorkspaceSchemaValidation validation = migrator.Validate();
        Assert.IsFalse(validation.Valid);
        CollectionAssert.Contains(validation.Problems.ToArray(), "migration_999_unknown");
        _ = IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
    }

    [TestMethod]
    public async Task Migration_MissingLedgerEntryFailsClosed()
    {
        await using PostgresIntegrationDatabase database =
            await MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync(
            "DELETE FROM chummer_build.schema_migrations WHERE version = 1").ConfigureAwait(false);

        using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
        PostgresWorkspaceSchemaValidation validation = migrator.Validate();
        Assert.IsFalse(validation.Valid);
        CollectionAssert.Contains(validation.Problems.ToArray(), "migration_1_missing");
        _ = IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
    }

    [TestMethod]
    public async Task Migration_RequiredColumnDriftFailsClosed()
    {
        await using PostgresIntegrationDatabase database =
            await MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync(
            "ALTER TABLE chummer_build.workspaces DROP COLUMN document_sha256").ConfigureAwait(false);

        using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
        PostgresWorkspaceSchemaValidation validation = migrator.Validate();
        Assert.IsFalse(validation.Valid, "Required-column drift must invalidate the schema contract.");
        _ = IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
    }

    [TestMethod]
    public async Task Migration_RequiredIndexDriftFailsClosed()
    {
        await using PostgresIntegrationDatabase database =
            await MigratedDatabaseAsync().ConfigureAwait(false);
        await database.ExecuteAsync(
            "DROP INDEX chummer_build.ix_chummer_build_workspaces_owner_updated").ConfigureAwait(false);

        using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
        PostgresWorkspaceSchemaValidation validation = migrator.Validate();
        Assert.IsFalse(validation.Valid, "Required-index drift must invalidate the schema contract.");
        _ = IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
    }

    internal static PostgresWorkspaceStoreOptions Options(string connectionString)
        => new(connectionString, TimeSpan.FromSeconds(10), requireLeastPrivilege: false);

    internal static async Task<PostgresIntegrationDatabase> MigratedDatabaseAsync()
    {
        PostgresIntegrationDatabase database =
            await PostgresIntegrationDatabase.CreateAsync().ConfigureAwait(false);
        try
        {
            using var migrator = new PostgresWorkspaceMigrator(Options(database.ConnectionString));
            migrator.Migrate();
            return database;
        }
        catch
        {
            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
