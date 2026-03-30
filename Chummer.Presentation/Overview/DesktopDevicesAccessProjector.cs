namespace Chummer.Presentation.Overview;

public sealed record DesktopInstallLinkingSummaryProjection(
    IReadOnlyList<DesktopRecentInstallReceipt> RecentReceipts,
    IReadOnlyList<DesktopPendingClaimTicket> PendingClaimTickets,
    IReadOnlyList<DesktopClaimedInstallProjection> ClaimedInstallations,
    IReadOnlyList<DesktopInstallationGrantProjection> ActiveGrants)
{
    public static DesktopInstallLinkingSummaryProjection Empty { get; } = new(
        RecentReceipts: Array.Empty<DesktopRecentInstallReceipt>(),
        PendingClaimTickets: Array.Empty<DesktopPendingClaimTicket>(),
        ClaimedInstallations: Array.Empty<DesktopClaimedInstallProjection>(),
        ActiveGrants: Array.Empty<DesktopInstallationGrantProjection>());
}

public sealed record DesktopRecentInstallReceipt(
    string ReceiptId,
    string ArtifactLabel,
    string Channel,
    string Version,
    string Head,
    string Platform,
    string Arch,
    string Kind,
    string InstallAccessClass,
    DateTimeOffset IssuedAtUtc,
    string? ClaimTicketId = null,
    string? ClaimCode = null,
    DateTimeOffset? ClaimTicketExpiresAtUtc = null);

public sealed record DesktopPendingClaimTicket(
    string TicketId,
    string ClaimCode,
    string ArtifactLabel,
    string Channel,
    string Version,
    string InstallAccessClass,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? InstallationId = null);

public sealed record DesktopClaimedInstallProjection(
    string InstallationId,
    string Channel,
    string Version,
    string InstallAccessClass,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ClaimTicketId = null,
    string? HeadId = null,
    string? Platform = null,
    string? Arch = null,
    string? HostLabel = null,
    string? GrantId = null);

public sealed record DesktopInstallationGrantProjection(
    string GrantId,
    string InstallationId,
    string Status,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);
