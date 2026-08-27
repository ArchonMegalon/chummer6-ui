namespace Chummer.Presentation.OriginBooks;

public enum ShadowArchivePresentationState
{
    Ready,
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
    InvalidContract,
    Unavailable
}

public sealed record ShadowArchiveBindingViewModel(
    string PublicationId,
    long PublicationRevision,
    string ContentDigest,
    long? WorkspaceRevision = null,
    string? SourceDigest = null,
    long? SignalRevision = null);

public sealed record ShadowArchiveErrorViewModel(
    string Code,
    string Message,
    long? ExpectedRevision,
    long? CurrentRevision,
    string? ExpectedDigest,
    string? CurrentDigest,
    TimeSpan? RetryAfter);

public sealed record ShadowArchivePresentationResult<T>(
    ShadowArchivePresentationState State,
    T? Value,
    ShadowArchiveErrorViewModel? Error)
{
    public bool IsReady => State == ShadowArchivePresentationState.Ready && Value is not null;

    public static ShadowArchivePresentationResult<T> Ready(T value)
        => new(ShadowArchivePresentationState.Ready, value, null);
}

public sealed record ShadowArchiveStoryIdentityViewModel(
    string RunnerHeading,
    string? RunnerHandle,
    string StoryOwnerLabel,
    string? StoryOwnerHandle,
    string TechnicalMetadataCredit);

/// <summary>
/// Hub-projected publication language edition. The Android catalog may filter only on this exact,
/// source-bound metadata; it must not infer language from prose or locale-looking titles.
/// </summary>
public sealed record ShadowArchivePublicationLanguageEditionViewModel(
    string LanguageEditionId,
    string LanguageTag,
    string DisplayName,
    string RulesetId,
    string SourceId,
    string SourceDigest);

/// <summary>
/// Hub-projected archetype bound to the rules edition and source snapshot which classified it.
/// User-facing catalog code must retain ArchetypeId as the stable filter identity.
/// </summary>
public sealed record ShadowArchiveEditionArchetypeViewModel(
    string ArchetypeId,
    string DisplayName,
    string RulesetId,
    string SourceId,
    string SourceDigest);

public sealed record ShadowArchiveCatalogMetadataViewModel(
    ShadowArchivePublicationLanguageEditionViewModel PublicationLanguage,
    IReadOnlyList<ShadowArchiveEditionArchetypeViewModel> Archetypes);

public sealed record ShadowArchivePublicationPreviewViewModel(
    string Title,
    string Summary,
    string Locale,
    string License,
    string PublicationStatus,
    bool CanConfirmPublication,
    bool RequiresExplicitConfirmation,
    IReadOnlyList<string> BlockedRequirements,
    ShadowArchiveStoryIdentityViewModel Identity,
    ShadowArchiveBindingViewModel Binding,
    DateTimeOffset? PublishedAtUtc);

public sealed record ShadowArchiveReaderChapterViewModel(
    string ChapterId,
    int Sequence,
    string Title,
    string BodyMarkdown,
    bool AllowsRawHtml);

public sealed record ShadowArchiveDownloadViewModel(
    string ArtifactId,
    string Format,
    string DisplayName,
    string MediaType,
    long ByteSize,
    string Sha256,
    Uri DownloadUri,
    string License,
    string TechnicalMetadataCredit);

public sealed record ShadowArchivePublicReaderViewModel(
    string Title,
    string Summary,
    string Locale,
    bool CanReadWithoutAccount,
    bool CanDownloadWithoutAccount,
    ShadowArchiveStoryIdentityViewModel Identity,
    IReadOnlyList<ShadowArchiveReaderChapterViewModel> Chapters,
    IReadOnlyList<ShadowArchiveDownloadViewModel> Downloads,
    ShadowArchiveBindingViewModel Binding,
    DateTimeOffset PublishedAtUtc);

public sealed record ShadowArchiveSignalViewModel(
    int VoteCount,
    bool ViewerHasVoted,
    bool CanVote,
    bool CanRetract,
    bool RequiresSignIn,
    string? BlockedReason);

public sealed record ShadowArchiveLeaderboardRowViewModel(
    int Rank,
    string RunnerDisplayName,
    string StoryOwnerLabel,
    int VoteCount,
    bool IsCurrentStory);

public sealed record ShadowArchiveLeaderboardViewModel(
    string SnapshotId,
    long SnapshotRevision,
    bool IsSealed,
    DateTimeOffset? SealedAtUtc,
    IReadOnlyList<ShadowArchiveLeaderboardRowViewModel> Rows);

public sealed record ShadowArchiveRewardArtifactViewModel(
    string Kind,
    string Status,
    Uri? PublicViewUri,
    Uri? DownloadUri);

public sealed record ShadowArchiveRewardViewModel(
    string Status,
    bool Eligible,
    bool RunnerOwnerAccepted,
    string? HoldReason,
    IReadOnlyList<ShadowArchiveRewardArtifactViewModel> Artifacts);

public sealed record ShadowArchiveCommunityViewModel(
    ShadowArchiveSignalViewModel Signal,
    ShadowArchiveLeaderboardViewModel Leaderboard,
    ShadowArchiveRewardViewModel Reward,
    ShadowArchiveBindingViewModel Binding);

public sealed record ShadowArchiveSignalCommandResult(
    ShadowArchivePresentationState State,
    ShadowArchiveSignalMutation? Mutation,
    ShadowArchiveErrorViewModel? Error)
{
    public bool CanSubmit => State == ShadowArchivePresentationState.Ready && Mutation is not null;
}
