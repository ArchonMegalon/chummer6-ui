using System.Text.Json.Serialization;

namespace Chummer.Hub.Web;

public static class CampaignViewerRoles
{
    public const string GameMaster = "gm";
    public const string Owner = "owner";
    public const string Player = "player";

    public static bool IsGameMaster(string? role)
        => string.Equals(role, GameMaster, StringComparison.Ordinal)
            || string.Equals(role, Owner, StringComparison.Ordinal);
}

public static class CampaignInviteHandoffStatuses
{
    public const string None = "none";
    public const string Fragment = "fragment";
    public const string RejectedQuery = "rejected-query";
    public const string InvalidFragment = "invalid-fragment";
}

public interface ICampaignCollaborationClient
{
    Task<IReadOnlyList<CampaignEligibleCharacterProjection>> GetEligibleCharactersAsync(
        CancellationToken cancellationToken = default);

    Task<CampaignWorkspaceProjection> GetCampaignAsync(
        string campaignId,
        CancellationToken cancellationToken = default);

    Task<CampaignJoinReceipt> JoinCampaignAsync(
        string inviteId,
        CampaignJoinRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignMutationReceipt> UpdatePlayerSafeSheetAsync(
        string campaignId,
        string dossierId,
        CampaignCharacterEditRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignGmAuthorityReceipt> UpdateGmEditAuthorityAsync(
        string campaignId,
        string dossierId,
        CampaignGmAuthorityUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignMutationReceipt> SaveRunsiteDraftAsync(
        string campaignId,
        RunsiteDraftSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignMutationReceipt> PublishRunsiteAsync(
        string campaignId,
        RunsitePublishRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CampaignInviteFragmentHandoff
{
    public string Status { get; init; } = CampaignInviteHandoffStatuses.None;

    public string? Secret { get; set; }

    public bool MustScrub { get; init; }

    [JsonIgnore]
    public bool HasUsableFragmentSecret
        => string.Equals(Status, CampaignInviteHandoffStatuses.Fragment, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(Secret);

    public override string ToString()
        => $"{nameof(CampaignInviteFragmentHandoff)} {{ Status = {Status}, Secret = [REDACTED], MustScrub = {MustScrub} }}";
}

public sealed class CampaignJoinRequest
{
    public CampaignJoinRequest(
        string secret,
        string dossierId,
        string authoritativeCharacterId,
        long expectedCharacterRevision,
        bool grantGmEditAuthority,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 256)
        {
            throw new ArgumentException("A bounded invite secret is required.", nameof(secret));
        }

        DossierId = NormalizeIdentifier(dossierId, nameof(dossierId));
        AuthoritativeCharacterId = NormalizeIdentifier(
            authoritativeCharacterId,
            nameof(authoritativeCharacterId));
        if (expectedCharacterRevision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCharacterRevision),
                "An existing character revision is required.");
        }

        IdempotencyKey = NormalizeIdentifier(idempotencyKey, nameof(idempotencyKey));

        Secret = secret;
        ExpectedCharacterRevision = expectedCharacterRevision;
        GrantGmEditAuthority = grantGmEditAuthority;
    }

    [JsonPropertyName("secret")]
    public string Secret { get; }

    public string DossierId { get; }

    public string AuthoritativeCharacterId { get; }

    public long ExpectedCharacterRevision { get; }

    public bool GrantGmEditAuthority { get; }

    public string IdempotencyKey { get; }

    public override string ToString()
        => $"{nameof(CampaignJoinRequest)} {{ Secret = [REDACTED], DossierId = {DossierId}, ExpectedCharacterRevision = {ExpectedCharacterRevision}, GrantGmEditAuthority = {GrantGmEditAuthority} }}";

    private static string NormalizeIdentifier(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 128)
        {
            throw new ArgumentException("A bounded identifier is required.", parameterName);
        }

        return normalized;
    }
}

public sealed record CampaignJoinReceipt(
    bool Joined,
    string CampaignId,
    string DossierId,
    string ViewerRole,
    bool AlreadyJoined,
    long BindingRevision,
    long CurrentCharacterRevision,
    bool GmEditAuthorityGranted);

public sealed record CampaignEligibleCharacterProjection(
    string DossierId,
    string AuthorityKind,
    string AuthoritativeCharacterId,
    string RunnerHandle,
    string DisplayName,
    string Status,
    long CurrentRevision,
    DateTimeOffset UpdatedAtUtc);

public sealed record CampaignWorkspaceProjection(
    string CampaignId,
    string Name,
    string Summary,
    string ViewerRole,
    bool CanManage,
    string? ActiveRunId,
    IReadOnlyList<CampaignRosterMemberProjection> Roster,
    CampaignRunsiteProjection Runsite);

public sealed record CampaignRosterMemberProjection(
    string MemberId,
    string DisplayName,
    string Role,
    string AuthorityKind,
    string AuthoritativeCharacterId,
    bool GmEditAuthorityGranted,
    long GmAuthorityBindingRevision,
    bool IsOwnedByViewer,
    PlayerSafeCharacterSheetProjection? PlayerSafeSheet);

public sealed record PlayerSafeCharacterSheetProjection(
    string DossierId,
    string RunnerHandle,
    string DisplayName,
    string Status,
    string Role,
    bool CanManage,
    bool GmEditAuthorityGranted,
    long GmAuthorityBindingRevision,
    bool IsOwnedByViewer,
    long Revision,
    string RuleEnvironmentFingerprint,
    IReadOnlyList<CampaignPublicationSafeSectionProjection> Sections);

public sealed record CampaignPublicationSafeSectionProjection(
    string ProjectionId,
    string Kind,
    string Label,
    string Summary,
    string? ArtifactId = null,
    string Audience = "campaign",
    string? OwnershipSummary = null,
    string? PublicationState = null,
    string? TrustBand = null,
    bool Discoverable = false,
    string? PublicationSummary = null,
    string? CreatorPublicationId = null,
    string? NextSafeAction = null,
    string? ProvenanceSummary = null,
    string? AuditSummary = null,
    string? CompatibilitySummary = null,
    string? LineageSummary = null);

public sealed record CampaignCharacterEditRequest(
    long ExpectedRevision,
    string IdempotencyKey,
    string RunnerHandle,
    string DisplayName,
    string Status,
    string Reason,
    IReadOnlyList<CampaignPublicationSafeSectionProjection> Sections);

public sealed record CampaignGmAuthorityUpdateRequest(
    long ExpectedBindingRevision,
    bool GrantGmEditAuthority,
    string IdempotencyKey,
    string Reason);

public sealed record CampaignGmAuthorityReceipt(
    bool Applied,
    long BindingRevision,
    long CurrentCharacterRevision,
    bool GmEditAuthorityGranted,
    bool Changed,
    string? Message = null);

public sealed record CampaignRunsiteProjection(
    string? RunId,
    long Revision,
    PublishedRunsiteProjection? Published,
    RunsiteDraftProjection? Draft);

public sealed record RunsitePlayerSectionProjection(
    string Heading,
    string Body);

public sealed record PublishedRunsiteProjection(
    string Title,
    string Summary,
    IReadOnlyList<RunsitePlayerSectionProjection> Sections,
    long Revision,
    DateTimeOffset PublishedAtUtc);

public sealed record RunsiteDraftProjection(
    string Title,
    string Summary,
    IReadOnlyList<RunsitePlayerSectionProjection> PlayerSections,
    string? GmNotes,
    long Revision,
    DateTimeOffset UpdatedAtUtc);

public sealed record RunsiteDraftSaveRequest(
    string RunId,
    long ExpectedRevision,
    string Title,
    string Summary,
    IReadOnlyList<RunsitePlayerSectionProjection> PlayerSections,
    string? GmNotes);

public sealed record RunsitePublishRequest(
    string RunId,
    long ExpectedRevision);

public sealed record CampaignMutationReceipt(
    bool Applied,
    long Revision,
    string? Message = null);
