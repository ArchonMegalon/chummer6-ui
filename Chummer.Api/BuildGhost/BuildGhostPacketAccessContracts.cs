using System.Text.Json.Serialization;

namespace Chummer.Api.BuildGhost;

public static class BuildGhostPrivateToolAccessContract
{
    public const string DeploymentEnabledConfigurationKey = "CHUMMER_BUILD_GHOST_PRIVATE_TOOL_DEPLOYMENT_ENABLED";
    public const string StoreRootConfigurationKey = "CHUMMER_BUILD_GHOST_PACKET_ACCESS_STORE_ROOT";
    public const string ServiceTokenConfigurationKey = "CHUMMER_BUILD_GHOST_PRIVATE_TOOL_SERVICE_TOKEN";
    public const string ContractDigestConfigurationKey = "CHUMMER_BUILD_GHOST_PRIVATE_TOOL_CONTRACT_DIGEST";
    public const string AuthenticationAudience = "build-ghost-private-tool";
    public const string ContractHeaderName = "X-Chummer-Build-Ghost-Tool-Contract";
    public const string PacketDigestHeaderName = "X-Chummer-Build-Ghost-Packet-Digest";
    public const int PacketAccessTtlSeconds = 300;

    public static readonly IReadOnlySet<string> RequestKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "current-build",
        "build-tips",
        "rule-explanation",
        "build-variants",
        "group-gaps"
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildGhostToolAccessRequest(
    string Locale,
    string RequestKind);

public sealed record BuildGhostToolAccessResponse(
    string PacketAccessKey,
    string PacketDigest,
    DateTimeOffset ExpiresAtUtc);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildGhostToolResolveRequest(
    string PacketAccessKey,
    string PacketDigest,
    string Locale,
    string RequestKind);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildGhostPacketAccessBinding(
    string OwnerId,
    string WorkspaceId,
    long WorkspaceRevision,
    string SourceDigest,
    string RuntimeFingerprint,
    string Locale,
    string RequestKind,
    string PacketDigest,
    string Audience,
    DateTimeOffset ExpiresAtUtc);

public sealed record BuildGhostPacketAccessGrant(
    string PacketAccessKey,
    BuildGhostPacketAccessBinding Binding);

public sealed record BuildGhostPacketAccessRevocationResult(
    int RevokedCount,
    int ExpiredCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildGhostPacketAccessAuditRecord(
    string Schema,
    string Event,
    string EventIdHmacSha256,
    string GrantRefSha256,
    string OwnerScopeRefHmacSha256,
    string WorkspaceRefHmacSha256,
    long WorkspaceRevision,
    string PacketRefHmacSha256,
    string SourceRefHmacSha256,
    string RuntimeFingerprintRefHmacSha256,
    string LocaleRefHmacSha256,
    string RequestKindRefHmacSha256,
    string AudienceRefHmacSha256,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset OccurredAtUtc,
    string ReceiptMacHmacSha256);

public interface IBuildGhostPacketAccessStore
{
    Task<BuildGhostPacketAccessGrant> IssueAsync(
        BuildGhostPacketAccessBinding binding,
        CancellationToken ct);

    Task<BuildGhostPacketAccessBinding?> ConsumeAsync(
        string packetAccessKey,
        CancellationToken ct);

    Task<bool> RevokeAsync(
        string packetAccessKey,
        CancellationToken ct);

    Task<BuildGhostPacketAccessRevocationResult> RevokeWorkspaceAsync(
        string ownerId,
        string workspaceId,
        long throughRevision,
        CancellationToken ct);

    Task<int> CleanupExpiredAsync(CancellationToken ct);
}

public sealed record BuildGhostPrivateToolAccessOptions(
    bool Enabled,
    string StoreRoot,
    string ServiceToken,
    string ContractDigest,
    bool StoreRootExplicitlyConfigured = true,
    int MaximumAuditRecords = 2048)
{
    public bool IsConfigured
        => Enabled
            && StoreRootExplicitlyConfigured
            && Path.IsPathFullyQualified(StoreRoot)
            && System.Text.Encoding.UTF8.GetByteCount(ServiceToken) >= 32
            && IsSha256(ContractDigest)
            && MaximumAuditRecords > 0;

    private static bool IsSha256(string? value)
        => value is { Length: 71 }
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && value.AsSpan(7).ToArray().All(static character => char.IsAsciiHexDigit(character));
}
