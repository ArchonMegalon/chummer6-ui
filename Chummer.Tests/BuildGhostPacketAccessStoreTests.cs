#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Api.BuildGhost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class BuildGhostPacketAccessStoreTests
{
    private const string ServiceToken = "packet-access-store-test-token-00000001";
    private const string ContractDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [TestMethod]
    public async Task Workspace_revocation_is_scope_and_revision_bound_and_blocks_late_issue()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore store = CreateStore(stateDirectory, time);
            BuildGhostPacketAccessGrant target = await store.IssueAsync(
                Binding(time, "owner-a", "workspace-a", 7, "target"),
                CancellationToken.None);
            BuildGhostPacketAccessGrant laterRevision = await store.IssueAsync(
                Binding(time, "owner-a", "workspace-a", 8, "later"),
                CancellationToken.None);
            BuildGhostPacketAccessGrant otherOwner = await store.IssueAsync(
                Binding(time, "owner-b", "workspace-a", 7, "other-owner"),
                CancellationToken.None);
            BuildGhostPacketAccessGrant otherWorkspace = await store.IssueAsync(
                Binding(time, "owner-a", "workspace-b", 7, "other-workspace"),
                CancellationToken.None);
            BuildGhostPacketAccessGrant expiredOtherScope = await store.IssueAsync(
                Binding(
                    time,
                    "owner-b",
                    "workspace-b",
                    7,
                    "expired-other-scope",
                    TimeSpan.FromSeconds(1)),
                CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(2));

            BuildGhostPacketAccessRevocationResult revoked = await store.RevokeWorkspaceAsync(
                "owner-a",
                "workspace-a",
                7,
                CancellationToken.None);

            Assert.AreEqual(1, revoked.RevokedCount);
            Assert.AreEqual(0, revoked.ExpiredCount);
            Assert.AreEqual(
                4,
                Directory.GetFiles(
                    Path.Combine(stateDirectory, "pending"),
                    "*.json",
                    SearchOption.TopDirectoryOnly).Length,
                "Workspace revocation touched a grant in another owner/workspace scope.");
            Assert.IsNull(await store.ConsumeAsync(target.PacketAccessKey, CancellationToken.None));
            Assert.IsNotNull(await store.ConsumeAsync(laterRevision.PacketAccessKey, CancellationToken.None));
            Assert.IsNotNull(await store.ConsumeAsync(otherOwner.PacketAccessKey, CancellationToken.None));
            Assert.IsNotNull(await store.ConsumeAsync(otherWorkspace.PacketAccessKey, CancellationToken.None));
            Assert.IsNull(await store.ConsumeAsync(expiredOtherScope.PacketAccessKey, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.IssueAsync(
                Binding(time, "owner-a", "workspace-a", 7, "late-revoked"),
                CancellationToken.None));
            Assert.IsNotNull(await store.IssueAsync(
                Binding(time, "owner-a", "workspace-a", 8, "late-allowed"),
                CancellationToken.None));

            string protectedState = await ReadTreeAsync(Path.Combine(stateDirectory, "audit"))
                + await ReadTreeAsync(Path.Combine(stateDirectory, "revocations"));
            Assert.IsFalse(protectedState.Contains("owner-a", StringComparison.Ordinal));
            Assert.IsFalse(protectedState.Contains("owner-b", StringComparison.Ordinal));
            Assert.IsFalse(protectedState.Contains("workspace-a", StringComparison.Ordinal));
            Assert.IsFalse(protectedState.Contains("workspace-b", StringComparison.Ordinal));
            StringAssert.Contains(protectedState, "\"event\":\"revoked\"");
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Concurrent_consume_and_revoke_have_exactly_one_terminal_winner()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 10, 30, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore store = CreateStore(stateDirectory, time);
            BuildGhostPacketAccessGrant grant = await store.IssueAsync(
                Binding(time, "race-owner", "race-workspace", 3, "race"),
                CancellationToken.None);
            TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<BuildGhostPacketAccessBinding?> consume = Task.Run(async () =>
            {
                await start.Task;
                return await store.ConsumeAsync(grant.PacketAccessKey, CancellationToken.None);
            });
            Task<bool> revoke = Task.Run(async () =>
            {
                await start.Task;
                return await store.RevokeAsync(grant.PacketAccessKey, CancellationToken.None);
            });

            start.SetResult();
            await Task.WhenAll(consume, revoke);

            bool consumeWon = consume.Result is not null;
            Assert.AreEqual(!consumeWon, revoke.Result);
            Assert.IsNull(await store.ConsumeAsync(grant.PacketAccessKey, CancellationToken.None));
            BuildGhostPacketAccessAuditRecord[] records = await ReadAuditAsync(stateDirectory);
            BuildGhostPacketAccessAuditRecord[] terminal = records
                .Where(static record => record.Event is "consumed" or "revoked")
                .ToArray();
            Assert.AreEqual(1, terminal.Length);
            Assert.AreEqual(consumeWon ? "consumed" : "revoked", terminal[0].Event);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Expiry_cleanup_is_audited_records_are_digest_only_and_retention_is_bounded()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 11, 0, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore store = CreateStore(stateDirectory, time, maximumAuditRecords: 3);
            BuildGhostPacketAccessGrant expired = await store.IssueAsync(
                Binding(time, "audit-owner", "audit-workspace", 4, "expired", TimeSpan.FromSeconds(1)),
                CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, await store.CleanupExpiredAsync(CancellationToken.None));
            Assert.IsNull(await store.ConsumeAsync(expired.PacketAccessKey, CancellationToken.None));
            Assert.IsTrue(
                (await ReadAuditAsync(stateDirectory)).Any(static record => record.Event == "expired"));

            BuildGhostPacketAccessGrant consumed = await store.IssueAsync(
                Binding(time, "audit-owner", "audit-workspace", 5, "consumed"),
                CancellationToken.None);
            Assert.IsNotNull(await store.ConsumeAsync(consumed.PacketAccessKey, CancellationToken.None));

            BuildGhostPacketAccessAuditRecord[] records = await ReadAuditAsync(stateDirectory);
            Assert.AreEqual(3, records.Length);
            Assert.IsTrue(records.Any(static record => record.Event == "consumed"));
            foreach (BuildGhostPacketAccessAuditRecord record in records)
            {
                AssertMac(record.EventIdHmacSha256);
                AssertDigest(record.GrantRefSha256);
                AssertMac(record.OwnerScopeRefHmacSha256);
                AssertMac(record.WorkspaceRefHmacSha256);
                AssertMac(record.PacketRefHmacSha256);
                AssertMac(record.SourceRefHmacSha256);
                AssertMac(record.RuntimeFingerprintRefHmacSha256);
                AssertMac(record.LocaleRefHmacSha256);
                AssertMac(record.RequestKindRefHmacSha256);
                AssertMac(record.AudienceRefHmacSha256);
                AssertMac(record.ReceiptMacHmacSha256);
            }

            string allState = await ReadTreeAsync(stateDirectory);
            Assert.IsFalse(allState.Contains(expired.PacketAccessKey, StringComparison.Ordinal));
            Assert.IsFalse(allState.Contains(consumed.PacketAccessKey, StringComparison.Ordinal));
            string auditState = await ReadTreeAsync(Path.Combine(stateDirectory, "audit"));
            Assert.IsFalse(auditState.Contains("audit-owner", StringComparison.Ordinal));
            Assert.IsFalse(auditState.Contains("audit-workspace", StringComparison.Ordinal));
            Assert.IsFalse(auditState.Contains("runtime-expired", StringComparison.Ordinal));
            Assert.IsFalse(auditState.Contains("runtime-consumed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Interrupted_claim_is_recovered_once_and_never_replays()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 11, 30, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore first = CreateStore(stateDirectory, time);
            BuildGhostPacketAccessGrant grant = await first.IssueAsync(
                Binding(time, "recovery-owner", "recovery-workspace", 6, "recovery"),
                CancellationToken.None);
            string pendingPath = Directory.GetFiles(
                Path.Combine(stateDirectory, "pending"),
                "*.json",
                SearchOption.TopDirectoryOnly).Single();
            string claimPath = Path.Combine(
                stateDirectory,
                "claims",
                $"consume.{time.GetUtcNow().UtcTicks}.{Path.GetFileName(pendingPath)}");
            File.Move(pendingPath, claimPath, overwrite: false);

            FileBuildGhostPacketAccessStore restarted = CreateStore(stateDirectory, time);
            Assert.AreEqual(0, await restarted.CleanupExpiredAsync(CancellationToken.None));
            Assert.IsNull(await restarted.ConsumeAsync(grant.PacketAccessKey, CancellationToken.None));
            BuildGhostPacketAccessAuditRecord[] records = await ReadAuditAsync(stateDirectory);
            Assert.AreEqual(1, records.Count(static record => record.Event == "consumed"));
            Assert.AreEqual(
                0,
                Directory.GetFiles(
                    Path.Combine(stateDirectory, "claims"),
                    "*.json",
                    SearchOption.TopDirectoryOnly).Length);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Plain_sha_recomputed_revocation_mac_fails_closed()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore store = CreateStore(stateDirectory, time);
            await store.RevokeWorkspaceAsync("tamper-owner", "tamper-workspace", 2, CancellationToken.None);
            string markerPath = Directory.GetFiles(
                Path.Combine(stateDirectory, "revocations"),
                "*.json",
                SearchOption.TopDirectoryOnly).Single();
            JsonObject marker = JsonNode.Parse(await File.ReadAllTextAsync(markerPath))?.AsObject()
                ?? throw new AssertFailedException("Revocation marker was missing.");
            marker["throughRevision"] = 3;
            marker["receiptMacHmacSha256"] = PlainShaMac(marker.ToJsonString());
            await File.WriteAllTextAsync(markerPath, marker.ToJsonString());

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.IssueAsync(
                Binding(time, "tamper-owner", "tamper-workspace", 3, "tamper"),
                CancellationToken.None));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Plain_sha_recomputed_audit_mac_and_wrong_key_authority_fail_closed()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            MutableAuditTimeProvider time = new(new DateTimeOffset(2026, 8, 22, 12, 30, 0, TimeSpan.Zero));
            FileBuildGhostPacketAccessStore store = CreateStore(stateDirectory, time);
            await store.IssueAsync(
                Binding(time, "keyed-owner", "keyed-workspace", 9, "keyed-audit"),
                CancellationToken.None);
            string auditPath = Directory.GetFiles(
                Path.Combine(stateDirectory, "audit"),
                "issued-*.json",
                SearchOption.TopDirectoryOnly).Single();
            JsonObject audit = JsonNode.Parse(await File.ReadAllTextAsync(auditPath))?.AsObject()
                ?? throw new AssertFailedException("Audit record was missing.");
            audit["workspaceRevision"] = 10;
            audit["receiptMacHmacSha256"] = PlainShaMac(audit.ToJsonString());
            await File.WriteAllTextAsync(auditPath, audit.ToJsonString());

            Assert.ThrowsExactly<InvalidDataException>(() => CreateStore(stateDirectory, time));
            Assert.ThrowsExactly<InvalidDataException>(() => new FileBuildGhostPacketAccessStore(
                new BuildGhostPrivateToolAccessOptions(
                    Enabled: true,
                    StoreRoot: stateDirectory,
                    ServiceToken: "different-packet-access-token-00000001",
                    ContractDigest: ContractDigest),
                time));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Legacy_unkeyed_pending_state_is_rejected_before_use()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            string pendingRoot = Path.Combine(stateDirectory, "pending");
            Directory.CreateDirectory(pendingRoot);
            File.WriteAllText(
                Path.Combine(pendingRoot, new string('a', 64) + ".json"),
                "{\"ownerId\":\"legacy-owner\",\"workspaceId\":\"legacy-workspace\"}");

            Assert.ThrowsExactly<InvalidDataException>(() => CreateStore(
                stateDirectory,
                new MutableAuditTimeProvider(DateTimeOffset.UtcNow)));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_rejects_missing_or_malformed_key_material()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            Assert.ThrowsExactly<ArgumentException>(() => new FileBuildGhostPacketAccessStore(
                new BuildGhostPrivateToolAccessOptions(
                    Enabled: true,
                    StoreRoot: stateDirectory,
                    ServiceToken: "short",
                    ContractDigest: ContractDigest)));
            Assert.ThrowsExactly<ArgumentException>(() => new FileBuildGhostPacketAccessStore(
                new BuildGhostPrivateToolAccessOptions(
                    Enabled: true,
                    StoreRoot: stateDirectory,
                    ServiceToken: ServiceToken,
                    ContractDigest: "not-a-digest")));
            Assert.AreEqual(
                0,
                Directory.GetFiles(stateDirectory, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static FileBuildGhostPacketAccessStore CreateStore(
        string stateDirectory,
        TimeProvider time,
        int maximumAuditRecords = 2048)
        => new(
            new BuildGhostPrivateToolAccessOptions(
                Enabled: true,
                StoreRoot: stateDirectory,
                ServiceToken: ServiceToken,
                ContractDigest: ContractDigest,
                MaximumAuditRecords: maximumAuditRecords),
            time);

    private static BuildGhostPacketAccessBinding Binding(
        TimeProvider time,
        string owner,
        string workspace,
        long revision,
        string discriminator,
        TimeSpan? lifetime = null)
        => new(
            owner,
            workspace,
            revision,
            $"sha256:source-{discriminator}",
            $"runtime-{discriminator}",
            "en-US",
            "current-build",
            $"sha256:packet-{discriminator}",
            BuildGhostPrivateToolAccessContract.AuthenticationAudience,
            time.GetUtcNow().Add(lifetime ?? TimeSpan.FromMinutes(5)));

    private static async Task<BuildGhostPacketAccessAuditRecord[]> ReadAuditAsync(string stateDirectory)
    {
        string[] paths = Directory.GetFiles(
            Path.Combine(stateDirectory, "audit"),
            "*.json",
            SearchOption.TopDirectoryOnly);
        BuildGhostPacketAccessAuditRecord[] records = new BuildGhostPacketAccessAuditRecord[paths.Length];
        for (int index = 0; index < paths.Length; index++)
        {
            records[index] = JsonSerializer.Deserialize<BuildGhostPacketAccessAuditRecord>(
                await File.ReadAllTextAsync(paths[index]),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new AssertFailedException("Audit record was missing.");
        }

        return records;
    }

    private static async Task<string> ReadTreeAsync(string root)
    {
        string[] paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        string[] contents = new string[paths.Length];
        for (int index = 0; index < paths.Length; index++)
        {
            contents[index] = await File.ReadAllTextAsync(paths[index]);
        }

        return string.Join('\n', contents);
    }

    private static void AssertDigest(string value)
    {
        Assert.AreEqual(71, value.Length);
        StringAssert.StartsWith(value, "sha256:");
        Assert.IsTrue(value.AsSpan(7).ToArray().All(char.IsAsciiHexDigit));
    }

    private static void AssertMac(string value)
    {
        Assert.AreEqual(76, value.Length);
        StringAssert.StartsWith(value, "hmac-sha256:");
        Assert.IsTrue(value.AsSpan(12).ToArray().All(char.IsAsciiHexDigit));
    }

    private static string PlainShaMac(string value)
        => "hmac-sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string CreateStateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "chummer-build-ghost-access-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class MutableAuditTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }
}
