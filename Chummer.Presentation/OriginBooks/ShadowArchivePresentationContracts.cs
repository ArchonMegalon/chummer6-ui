namespace Chummer.Presentation.OriginBooks;

public static class ShadowArchiveContractNames
{
    public const string PublicationPreview = "chummer.shadow-archive.publication-preview/v1";
    public const string PublicReader = "chummer.shadow-archive.public-reader/v1";
    public const string CommunityStatus = "chummer.shadow-archive.community-status/v1";
    public const string SignalMutation = "chummer.shadow-archive.signal-mutation/v1";
    public const string TechnicalCredit = "Chummer.run";
}

public static class ShadowArchivePublicationStatuses
{
    public const string Draft = "draft";
    public const string Reviewing = "reviewing";
    public const string Published = "published";
    public const string Unpublished = "unpublished";
    public const string Superseded = "superseded";
    public const string ModerationHeld = "moderation_held";
    public const string Removed = "removed";
}

public static class ShadowArchiveSignalIntents
{
    public const string Vote = "vote";
    public const string Retract = "retract";
}

public enum ShadowArchiveClientResultKind
{
    Success,
    NotFinalized,
    Stale,
    RevisionConflict,
    AuthenticationRequired,
    Forbidden,
    NotFound,
    Removed,
    ModerationHeld,
    Offline,
    RateLimited,
    Unavailable
}

public sealed record ShadowArchiveClientResult<T>(
    ShadowArchiveClientResultKind Kind,
    T? Value = default,
    string? ErrorCode = null,
    string? SafeMessage = null,
    long? ExpectedRevision = null,
    long? CurrentRevision = null,
    string? ExpectedDigest = null,
    string? CurrentDigest = null,
    TimeSpan? RetryAfter = null)
{
    public static ShadowArchiveClientResult<T> Succeeded(T value)
        => new(ShadowArchiveClientResultKind.Success, value);
}

public sealed record ShadowArchiveViewerContext(
    bool IsSignedIn,
    string? UserId,
    string? DisplayName);

public sealed record ShadowArchivePublicationPreviewQuery(
    string ProjectId,
    string WorkspaceId,
    long ExpectedWorkspaceRevision,
    string ExpectedSourceDigest,
    long? ExpectedPublicationRevision = null);

public sealed record ShadowArchivePublicReaderQuery(
    string PublicationId,
    long ExpectedPublicationRevision,
    string ExpectedContentDigest);

public sealed record ShadowArchiveCommunityQuery(
    string PublicationId,
    long ExpectedPublicationRevision,
    string ExpectedContentDigest);

public sealed record ShadowArchiveSignalMutation(
    string ContractName,
    string PublicationId,
    long ExpectedPublicationRevision,
    string ExpectedContentDigest,
    long ExpectedSignalRevision,
    string Intent,
    string IdempotencyKey);

public sealed record ShadowArchivePersonContract(
    string UserId,
    string DisplayName,
    string? Handle);

public sealed record ShadowArchivePublicationPreviewContract(
    string ContractName,
    string ProjectId,
    string WorkspaceId,
    long WorkspaceRevision,
    string SourceDigest,
    string PublicationId,
    long PublicationRevision,
    string ContentDigest,
    bool CharacterFinalized,
    string PublicationStatus,
    string Title,
    string Summary,
    string Locale,
    string License,
    bool PublicSafeValidationPassed,
    bool RightsAndProvenanceReviewPassed,
    bool ExplicitConfirmationRequired,
    IReadOnlyList<string> BlockedRequirements,
    ShadowArchivePersonContract Runner,
    ShadowArchivePersonContract Owner,
    string TechnicalAuthor,
    string TechnicalPublisher,
    DateTimeOffset? PublishedAtUtc);

public sealed record ShadowArchiveReaderChapterContract(
    string ChapterId,
    int Sequence,
    string Title,
    string BodyMarkdown);

public sealed record ShadowArchiveDownloadArtifactContract(
    string ArtifactId,
    string Format,
    string DisplayName,
    string MediaType,
    long ByteSize,
    string Sha256,
    Uri DownloadUri,
    long PublicationRevision,
    string ContentDigest,
    string RendererReceiptDigest,
    string License,
    string TechnicalAuthor);

public sealed record ShadowArchivePublicReaderContract(
    string ContractName,
    string PublicationId,
    long PublicationRevision,
    string ContentDigest,
    string PublicationStatus,
    bool PublicAccess,
    bool RequiresAuthentication,
    string Title,
    string Summary,
    string Locale,
    ShadowArchivePersonContract Runner,
    ShadowArchivePersonContract Owner,
    string TechnicalAuthor,
    string TechnicalPublisher,
    IReadOnlyList<ShadowArchiveReaderChapterContract> Chapters,
    IReadOnlyList<ShadowArchiveDownloadArtifactContract> Downloads,
    DateTimeOffset PublishedAtUtc);

public sealed record ShadowArchiveSignalContract(
    long SignalRevision,
    int VoteCount,
    bool ViewerHasVoted,
    bool CanVote,
    bool CanRetract,
    string? VoteBlockedReason);

public sealed record ShadowArchiveLeaderboardRowContract(
    int Rank,
    string PublicationId,
    long PublicationRevision,
    string ContentDigest,
    string RunnerDisplayName,
    string OwnerDisplayName,
    int VoteCount);

public sealed record ShadowArchiveLeaderboardContract(
    string SnapshotId,
    long SnapshotRevision,
    bool Sealed,
    DateTimeOffset? SealedAtUtc,
    IReadOnlyList<ShadowArchiveLeaderboardRowContract> Rows);

public sealed record ShadowArchiveRewardArtifactContract(
    string Kind,
    string Status,
    Uri? PublicViewUri,
    Uri? DownloadUri);

public sealed record ShadowArchiveRewardContract(
    string Status,
    bool Eligible,
    bool RunnerOwnerAccepted,
    string? HoldReason,
    IReadOnlyList<ShadowArchiveRewardArtifactContract> Artifacts);

public sealed record ShadowArchiveCommunityStatusContract(
    string ContractName,
    string PublicationId,
    long PublicationRevision,
    string ContentDigest,
    ShadowArchiveSignalContract Signal,
    ShadowArchiveLeaderboardContract Leaderboard,
    ShadowArchiveRewardContract Reward);

/// <summary>
/// Transport-neutral presentation port. An adapter maps the Hub-owned community contracts into
/// these read projections; this interface is not publication, ranking, reward, or provider authority.
/// </summary>
public interface IShadowArchivePresentationClient
{
    Task<ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>> GetPublicationPreviewAsync(
        ShadowArchivePublicationPreviewQuery query,
        CancellationToken ct);

    Task<ShadowArchiveClientResult<ShadowArchivePublicReaderContract>> GetPublicReaderAsync(
        ShadowArchivePublicReaderQuery query,
        CancellationToken ct);

    Task<ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>> GetCommunityStatusAsync(
        ShadowArchiveCommunityQuery query,
        CancellationToken ct);

    Task<ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>> MutateSignalAsync(
        ShadowArchiveSignalMutation mutation,
        CancellationToken ct);
}
