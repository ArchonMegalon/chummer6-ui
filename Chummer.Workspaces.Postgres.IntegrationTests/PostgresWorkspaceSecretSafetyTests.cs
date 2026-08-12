using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Workspaces.Postgres;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Workspaces.Postgres.IntegrationTests;

[TestClass]
[TestCategory("PostgreSQLIntegration")]
public sealed class PostgresWorkspaceSecretSafetyTests
{
    [TestMethod]
    public void UnavailablePostgres_ReturnsOnlyStableSecretFreeDiagnostics()
    {
        const string secretMarker = "do-not-leak-build-db-password";
        const string unavailableConnection =
            "Host=127.0.0.1;Port=1;Database=chummer_build_unavailable;"
            + "Username=chummer_build_runtime;Password=" + secretMarker + ";"
            + "Pooling=false;Timeout=1;Command Timeout=1;Include Error Detail=false";
        var options = new PostgresWorkspaceStoreOptions(
            unavailableConnection,
            TimeSpan.FromSeconds(1),
            requireLeastPrivilege: false);
        var owner = new OwnerScope("secret-safety-owner");
        var id = new CharacterWorkspaceId("unavailable-workspace");

        using (var store = new PostgresWorkspaceStore(options))
        {
            Chummer.Application.Workspaces.WorkspaceStoreReadResult read = store.Get(owner, id);
            Assert.AreEqual(WorkspaceOperationOutcome.Unavailable, read.Outcome);
            Assert.AreEqual("Workspace storage is unavailable.", read.Error);
            AssertSecretFree(read.Error, secretMarker);

            Chummer.Application.Workspaces.WorkspaceStoreMutationResult create =
                store.CreateWorkspaceDocument(owner, id, new WorkspaceDocument("content", "sr5"));
            Assert.AreEqual(WorkspaceOperationOutcome.Unavailable, create.Outcome);
            AssertSecretFree(create.Error, secretMarker);

            WorkspaceOwnerErasureResult erasure = store.EraseOwner(owner);
            Assert.IsFalse(erasure.Success);
            AssertSecretFree(erasure.Error, secretMarker);
            WorkspacePrivacyMaintenanceResult replay = store.ApplyDeletionReplay(owner);
            Assert.IsFalse(replay.Success);
            AssertSecretFree(replay.Error, secretMarker);
            WorkspacePrivacyMaintenanceResult purge = store.PurgeExpiredDeletionAuditReceipts();
            Assert.IsFalse(purge.Success);
            AssertSecretFree(purge.Error, secretMarker);

            InvalidOperationException readiness =
                IntegrationAssert.Throws<InvalidOperationException>(() => store.Probe(owner));
            AssertSecretFree(readiness.ToString(), secretMarker);
        }

        using (var migrator = new PostgresWorkspaceMigrator(options))
        {
            InvalidOperationException migration =
                IntegrationAssert.Throws<InvalidOperationException>(migrator.Migrate);
            AssertSecretFree(migration.ToString(), secretMarker);
            PostgresWorkspaceSchemaValidation validation = migrator.Validate();
            CollectionAssert.AreEqual(
                new[] { "postgres_unavailable" },
                validation.Problems.ToArray());
        }

        AssertSecretFree(options.ToString(), secretMarker);
    }

    private static void AssertSecretFree(string? value, string secretMarker)
    {
        string candidate = value ?? string.Empty;
        Assert.IsFalse(candidate.Contains(secretMarker, StringComparison.Ordinal));
        Assert.IsFalse(candidate.Contains("127.0.0.1", StringComparison.Ordinal));
        Assert.IsFalse(candidate.Contains("chummer_build_runtime", StringComparison.Ordinal));
        Assert.IsFalse(candidate.Contains("Password=", StringComparison.OrdinalIgnoreCase));
    }
}
