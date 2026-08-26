namespace Chummer.Presentation.OriginBooks;

public sealed class ShadowArchivePresenter(IShadowArchivePresentationClient client)
{
    private const string GenericUnavailableMessage = "Shadow Archive is unavailable. No publication or signal change was assumed.";
    private readonly IShadowArchivePresentationClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel>> LoadPublicationPreviewAsync(
        ShadowArchivePublicationPreviewQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!ValidPreviewQuery(query))
        {
            return Invalid<ShadowArchivePublicationPreviewViewModel>("publication_preview_query_invalid");
        }

        try
        {
            ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract> result =
                await _client.GetPublicationPreviewAsync(query, ct).ConfigureAwait(false);
            if (result.Kind != ShadowArchiveClientResultKind.Success || result.Value is null)
            {
                return Failure<ShadowArchivePublicationPreviewContract, ShadowArchivePublicationPreviewViewModel>(result);
            }

            ShadowArchivePublicationPreviewContract value = result.Value;
            string? invalid = ValidatePreview(query, value);
            if (invalid is not null)
            {
                return Invalid<ShadowArchivePublicationPreviewViewModel>(invalid);
            }

            if (!value.CharacterFinalized)
            {
                return StateFailure<ShadowArchivePublicationPreviewViewModel>(
                    ShadowArchivePresentationState.NotFinalized,
                    "origin_story_not_finalized",
                    "Finish character creation before preparing a public Origin Story edition.");
            }

            if (value.WorkspaceRevision != query.ExpectedWorkspaceRevision
                || !DigestEquals(value.SourceDigest, query.ExpectedSourceDigest))
            {
                return BindingFailure<ShadowArchivePublicationPreviewViewModel>(
                    ShadowArchivePresentationState.Stale,
                    "origin_story_source_stale",
                    "The finalized Origin Story changed. Reload the publication preview.",
                    query.ExpectedWorkspaceRevision,
                    value.WorkspaceRevision,
                    query.ExpectedSourceDigest,
                    value.SourceDigest);
            }

            if (query.ExpectedPublicationRevision is long expectedPublicationRevision
                && expectedPublicationRevision != value.PublicationRevision)
            {
                return BindingFailure<ShadowArchivePublicationPreviewViewModel>(
                    ShadowArchivePresentationState.RevisionConflict,
                    "publication_revision_conflict",
                    "A newer publication revision exists. Reload before publishing.",
                    expectedPublicationRevision,
                    value.PublicationRevision,
                    null,
                    null);
            }

            return ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel>.Ready(new(
                Title: Clean(value.Title),
                Summary: Clean(value.Summary),
                Locale: value.Locale.Trim(),
                License: Clean(value.License),
                PublicationStatus: value.PublicationStatus,
                CanConfirmPublication: value.PublicSafeValidationPassed
                    && value.RightsAndProvenanceReviewPassed
                    && value.ExplicitConfirmationRequired
                    && value.BlockedRequirements.Count == 0
                    && value.PublicationStatus == ShadowArchivePublicationStatuses.Reviewing,
                RequiresExplicitConfirmation: value.ExplicitConfirmationRequired,
                BlockedRequirements: PlayerFacingCopyHumanizer.CleanLines(value.BlockedRequirements),
                Identity: Identity(value.Runner, value.Owner),
                Binding: new(
                    value.PublicationId,
                    value.PublicationRevision,
                    NormalizeDigest(value.ContentDigest),
                    value.WorkspaceRevision,
                    NormalizeDigest(value.SourceDigest)),
                PublishedAtUtc: value.PublishedAtUtc));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable<ShadowArchivePublicationPreviewViewModel>();
        }
    }

    public async Task<ShadowArchivePresentationResult<ShadowArchivePublicReaderViewModel>> LoadPublicReaderAsync(
        ShadowArchivePublicReaderQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!ValidReaderQuery(query))
        {
            return Invalid<ShadowArchivePublicReaderViewModel>("public_reader_query_invalid");
        }

        try
        {
            ShadowArchiveClientResult<ShadowArchivePublicReaderContract> result =
                await _client.GetPublicReaderAsync(query, ct).ConfigureAwait(false);
            if (result.Kind != ShadowArchiveClientResultKind.Success || result.Value is null)
            {
                return Failure<ShadowArchivePublicReaderContract, ShadowArchivePublicReaderViewModel>(result);
            }

            ShadowArchivePublicReaderContract value = result.Value;
            string? invalid = ValidateReader(query, value);
            if (invalid is not null)
            {
                return Invalid<ShadowArchivePublicReaderViewModel>(invalid);
            }

            if (value.PublicationRevision != query.ExpectedPublicationRevision)
            {
                return BindingFailure<ShadowArchivePublicReaderViewModel>(
                    ShadowArchivePresentationState.RevisionConflict,
                    "publication_revision_conflict",
                    "This story revision was replaced. Open the current revision.",
                    query.ExpectedPublicationRevision,
                    value.PublicationRevision,
                    null,
                    null);
            }

            if (!DigestEquals(value.ContentDigest, query.ExpectedContentDigest))
            {
                return BindingFailure<ShadowArchivePublicReaderViewModel>(
                    ShadowArchivePresentationState.Stale,
                    "publication_content_stale",
                    "The story content does not match the selected revision. Reload it.",
                    null,
                    null,
                    query.ExpectedContentDigest,
                    value.ContentDigest);
            }

            return ShadowArchivePresentationResult<ShadowArchivePublicReaderViewModel>.Ready(new(
                Title: Clean(value.Title),
                Summary: Clean(value.Summary),
                Locale: value.Locale.Trim(),
                CanReadWithoutAccount: true,
                CanDownloadWithoutAccount: true,
                Identity: Identity(value.Runner, value.Owner),
                Chapters: value.Chapters
                    .OrderBy(static chapter => chapter.Sequence)
                    .Select(static chapter => new ShadowArchiveReaderChapterViewModel(
                        chapter.ChapterId,
                        chapter.Sequence,
                        Clean(chapter.Title),
                        chapter.BodyMarkdown,
                        AllowsRawHtml: false))
                    .ToArray(),
                Downloads: value.Downloads
                    .Select(static artifact => new ShadowArchiveDownloadViewModel(
                        artifact.ArtifactId,
                        artifact.Format,
                        Clean(artifact.DisplayName),
                        artifact.MediaType,
                        artifact.ByteSize,
                        NormalizeDigest(artifact.Sha256),
                        artifact.DownloadUri,
                        Clean(artifact.License),
                        ShadowArchiveContractNames.TechnicalCredit))
                    .ToArray(),
                Binding: new(value.PublicationId, value.PublicationRevision, NormalizeDigest(value.ContentDigest)),
                PublishedAtUtc: value.PublishedAtUtc));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable<ShadowArchivePublicReaderViewModel>();
        }
    }

    public async Task<ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel>> LoadCommunityAsync(
        ShadowArchiveCommunityQuery query,
        ShadowArchiveViewerContext viewer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(viewer);
        if (!ValidCommunityQuery(query))
        {
            return Invalid<ShadowArchiveCommunityViewModel>("community_query_invalid");
        }

        try
        {
            ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract> result =
                await _client.GetCommunityStatusAsync(query, ct).ConfigureAwait(false);
            return ProjectCommunityResult(result, query, viewer);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable<ShadowArchiveCommunityViewModel>();
        }
    }

    public ShadowArchiveSignalCommandResult CreateSignalCommand(
        ShadowArchiveCommunityViewModel current,
        ShadowArchiveViewerContext viewer,
        string intent,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(viewer);
        if (current.Signal is null || current.Binding is null
            || !HasText(current.Binding.PublicationId) || current.Binding.PublicationRevision <= 0
            || !IsDigest(current.Binding.ContentDigest)
            || current.Binding.SignalRevision is not > 0)
        {
            return CommandFailure(
                ShadowArchivePresentationState.InvalidContract,
                "signal_binding_invalid",
                "Reload the story before changing its vote.");
        }

        if (!viewer.IsSignedIn || string.IsNullOrWhiteSpace(viewer.UserId))
        {
            return CommandFailure(
                ShadowArchivePresentationState.AuthenticationRequired,
                "signal_sign_in_required",
                "Sign in to vote for this Origin Story.");
        }

        if (intent is not (ShadowArchiveSignalIntents.Vote or ShadowArchiveSignalIntents.Retract))
        {
            return CommandFailure(
                ShadowArchivePresentationState.InvalidContract,
                "signal_intent_invalid",
                "The requested signal action is not supported.");
        }

        bool allowed = intent == ShadowArchiveSignalIntents.Vote
            ? current.Signal.CanVote
            : current.Signal.CanRetract;
        if (!allowed)
        {
            return CommandFailure(
                ShadowArchivePresentationState.Forbidden,
                "signal_not_allowed",
                current.Signal.BlockedReason ?? "Voting is not available for this story.");
        }

        if (intent == ShadowArchiveSignalIntents.Vote && current.Signal.ViewerHasVoted)
        {
            return CommandFailure(
                ShadowArchivePresentationState.RevisionConflict,
                "signal_already_cast",
                "Your vote is already recorded. Reload before changing it.");
        }

        if (intent == ShadowArchiveSignalIntents.Retract && !current.Signal.ViewerHasVoted)
        {
            return CommandFailure(
                ShadowArchivePresentationState.RevisionConflict,
                "signal_not_cast",
                "There is no recorded vote to retract. Reload before changing it.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128)
        {
            return CommandFailure(
                ShadowArchivePresentationState.InvalidContract,
                "idempotency_key_invalid",
                "A bounded idempotency key is required before changing a vote.");
        }

        return new(
            ShadowArchivePresentationState.Ready,
            new ShadowArchiveSignalMutation(
                ShadowArchiveContractNames.SignalMutation,
                current.Binding.PublicationId,
                current.Binding.PublicationRevision,
                current.Binding.ContentDigest,
                current.Binding.SignalRevision!.Value,
                intent,
                idempotencyKey.Trim()),
            null);
    }

    public async Task<ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel>> SubmitSignalAsync(
        ShadowArchiveSignalMutation mutation,
        ShadowArchiveViewerContext viewer,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(viewer);
        if (!viewer.IsSignedIn || string.IsNullOrWhiteSpace(viewer.UserId))
        {
            return StateFailure<ShadowArchiveCommunityViewModel>(
                ShadowArchivePresentationState.AuthenticationRequired,
                "signal_sign_in_required",
                "Sign in to vote for this Origin Story.");
        }

        string? invalid = ValidateSignalMutation(mutation);
        if (invalid is not null)
        {
            return Invalid<ShadowArchiveCommunityViewModel>(invalid);
        }

        try
        {
            ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract> result =
                await _client.MutateSignalAsync(mutation, ct).ConfigureAwait(false);
            ShadowArchiveCommunityQuery query = new(
                mutation.PublicationId,
                mutation.ExpectedPublicationRevision,
                mutation.ExpectedContentDigest);
            return ProjectCommunityResult(result, query, viewer);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable<ShadowArchiveCommunityViewModel>();
        }
    }

    private static ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> ProjectCommunityResult(
        ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract> result,
        ShadowArchiveCommunityQuery query,
        ShadowArchiveViewerContext viewer)
    {
        if (result.Kind != ShadowArchiveClientResultKind.Success || result.Value is null)
        {
            return Failure<ShadowArchiveCommunityStatusContract, ShadowArchiveCommunityViewModel>(result);
        }

        ShadowArchiveCommunityStatusContract value = result.Value;
        string? invalid = ValidateCommunity(query, value);
        if (invalid is not null)
        {
            return Invalid<ShadowArchiveCommunityViewModel>(invalid);
        }

        if (value.PublicationRevision != query.ExpectedPublicationRevision)
        {
            return BindingFailure<ShadowArchiveCommunityViewModel>(
                ShadowArchivePresentationState.RevisionConflict,
                "publication_revision_conflict",
                "Votes belong to a different publication revision. Reload before voting.",
                query.ExpectedPublicationRevision,
                value.PublicationRevision,
                null,
                null);
        }

        if (!DigestEquals(value.ContentDigest, query.ExpectedContentDigest))
        {
            return BindingFailure<ShadowArchiveCommunityViewModel>(
                ShadowArchivePresentationState.Stale,
                "publication_content_stale",
                "Votes belong to different story content. Reload before voting.",
                null,
                null,
                query.ExpectedContentDigest,
                value.ContentDigest);
        }

        bool signedIn = viewer.IsSignedIn && !string.IsNullOrWhiteSpace(viewer.UserId);
        ShadowArchiveSignalContract signal = value.Signal;
        return ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel>.Ready(new(
            Signal: new(
                signal.VoteCount,
                signal.ViewerHasVoted,
                signedIn && signal.CanVote,
                signedIn && signal.CanRetract,
                !signedIn,
                !signedIn ? "Sign in to vote for this Origin Story." : CleanOptional(signal.VoteBlockedReason)),
            Leaderboard: new(
                value.Leaderboard.SnapshotId,
                value.Leaderboard.SnapshotRevision,
                value.Leaderboard.Sealed,
                value.Leaderboard.SealedAtUtc,
                value.Leaderboard.Rows.Select(row => new ShadowArchiveLeaderboardRowViewModel(
                    row.Rank,
                    Clean(row.RunnerDisplayName),
                    Clean(row.OwnerDisplayName),
                    row.VoteCount,
                    string.Equals(row.PublicationId, value.PublicationId, StringComparison.Ordinal)
                        && row.PublicationRevision == value.PublicationRevision
                        && DigestEquals(row.ContentDigest, value.ContentDigest))).ToArray()),
            Reward: new(
                value.Reward.Status,
                value.Reward.Eligible,
                value.Reward.RunnerOwnerAccepted,
                CleanOptional(value.Reward.HoldReason),
                value.Reward.Artifacts.Select(static artifact => new ShadowArchiveRewardArtifactViewModel(
                    artifact.Kind,
                    artifact.Status,
                    artifact.PublicViewUri,
                    artifact.DownloadUri)).ToArray()),
            Binding: new(
                value.PublicationId,
                value.PublicationRevision,
                NormalizeDigest(value.ContentDigest),
                SignalRevision: signal.SignalRevision)));
    }

    private static string? ValidatePreview(
        ShadowArchivePublicationPreviewQuery query,
        ShadowArchivePublicationPreviewContract value)
    {
        if (!string.Equals(value.ContractName, ShadowArchiveContractNames.PublicationPreview, StringComparison.Ordinal))
        {
            return "publication_preview_contract_name_invalid";
        }

        if (!string.Equals(query.ProjectId, value.ProjectId, StringComparison.Ordinal)
            || !string.Equals(query.WorkspaceId, value.WorkspaceId, StringComparison.Ordinal))
        {
            return "publication_preview_identity_mismatch";
        }

        if (value.WorkspaceRevision <= 0 || value.PublicationRevision <= 0
            || !IsDigest(value.SourceDigest) || !IsDigest(value.ContentDigest)
            || !HasText(value.PublicationId) || !HasText(value.Title) || !HasText(value.Summary)
            || !HasText(value.Locale) || !HasText(value.License) || value.BlockedRequirements is null
            || (!value.ExplicitConfirmationRequired && value.PublicationStatus == ShadowArchivePublicationStatuses.Reviewing)
            || !ValidIdentity(value.Runner, value.Owner) || !ValidTechnicalCredit(value.TechnicalAuthor, value.TechnicalPublisher))
        {
            return "publication_preview_payload_invalid";
        }

        return KnownPublicationStatus(value.PublicationStatus) ? null : "publication_status_invalid";
    }

    private static string? ValidateReader(
        ShadowArchivePublicReaderQuery query,
        ShadowArchivePublicReaderContract value)
    {
        if (!string.Equals(value.ContractName, ShadowArchiveContractNames.PublicReader, StringComparison.Ordinal))
        {
            return "public_reader_contract_name_invalid";
        }

        if (!string.Equals(query.PublicationId, value.PublicationId, StringComparison.Ordinal))
        {
            return "public_reader_identity_mismatch";
        }

        if (value.PublicationRevision <= 0 || !IsDigest(value.ContentDigest)
            || value.PublicationStatus != ShadowArchivePublicationStatuses.Published
            || !value.PublicAccess || value.RequiresAuthentication
            || !HasText(value.Title) || !HasText(value.Summary) || !HasText(value.Locale)
            || !ValidIdentity(value.Runner, value.Owner) || !ValidTechnicalCredit(value.TechnicalAuthor, value.TechnicalPublisher)
            || value.Chapters is null || value.Downloads is null
            || value.Chapters.Count == 0 || value.Chapters.Any(static chapter =>
                !HasText(chapter.ChapterId) || chapter.Sequence <= 0 || !HasText(chapter.Title) || !HasText(chapter.BodyMarkdown))
            || value.Chapters.Select(static chapter => chapter.Sequence).Distinct().Count() != value.Chapters.Count
            || value.Downloads.Any(artifact => !ValidArtifact(artifact, value)))
        {
            return "public_reader_payload_invalid";
        }

        return null;
    }

    private static string? ValidateCommunity(
        ShadowArchiveCommunityQuery query,
        ShadowArchiveCommunityStatusContract value)
    {
        if (!string.Equals(value.ContractName, ShadowArchiveContractNames.CommunityStatus, StringComparison.Ordinal))
        {
            return "community_status_contract_name_invalid";
        }

        if (!string.Equals(query.PublicationId, value.PublicationId, StringComparison.Ordinal)
            || value.PublicationRevision <= 0 || !IsDigest(value.ContentDigest)
            || value.Signal is null || value.Leaderboard is null || value.Reward is null
            || value.Signal.SignalRevision <= 0 || value.Signal.VoteCount < 0
            || (value.Signal.CanVote && value.Signal.CanRetract)
            || (value.Signal.ViewerHasVoted && value.Signal.CanVote)
            || (!value.Signal.ViewerHasVoted && value.Signal.CanRetract)
            || !HasText(value.Leaderboard.SnapshotId) || value.Leaderboard.SnapshotRevision <= 0
            || value.Leaderboard.Rows is null || value.Reward.Artifacts is null
            || value.Leaderboard.Sealed != value.Leaderboard.SealedAtUtc.HasValue
            || value.Leaderboard.Rows.Any(static row => row.Rank <= 0 || row.PublicationRevision <= 0
                || !IsDigest(row.ContentDigest) || !HasText(row.PublicationId)
                || !HasText(row.RunnerDisplayName) || !HasText(row.OwnerDisplayName) || row.VoteCount < 0)
            || value.Leaderboard.Rows.Select(static row => row.Rank).Distinct().Count() != value.Leaderboard.Rows.Count
            || !HasText(value.Reward.Status)
            || (value.Reward.Eligible && !value.Leaderboard.Sealed)
            || (value.Reward.RunnerOwnerAccepted && !value.Reward.Eligible)
            || value.Reward.Artifacts.Any(static artifact => !HasText(artifact.Kind) || !HasText(artifact.Status)
                || !ValidOptionalPublicUri(artifact.PublicViewUri) || !ValidOptionalPublicUri(artifact.DownloadUri)))
        {
            return "community_status_payload_invalid";
        }

        return null;
    }

    private static bool ValidPreviewQuery(ShadowArchivePublicationPreviewQuery query)
        => HasText(query.ProjectId)
            && HasText(query.WorkspaceId)
            && query.ExpectedWorkspaceRevision > 0
            && IsDigest(query.ExpectedSourceDigest)
            && query.ExpectedPublicationRevision is null or > 0;

    private static bool ValidReaderQuery(ShadowArchivePublicReaderQuery query)
        => HasText(query.PublicationId)
            && query.ExpectedPublicationRevision > 0
            && IsDigest(query.ExpectedContentDigest);

    private static bool ValidCommunityQuery(ShadowArchiveCommunityQuery query)
        => HasText(query.PublicationId)
            && query.ExpectedPublicationRevision > 0
            && IsDigest(query.ExpectedContentDigest);

    private static string? ValidateSignalMutation(ShadowArchiveSignalMutation mutation)
    {
        if (!string.Equals(mutation.ContractName, ShadowArchiveContractNames.SignalMutation, StringComparison.Ordinal)
            || !HasText(mutation.PublicationId) || mutation.ExpectedPublicationRevision <= 0
            || !IsDigest(mutation.ExpectedContentDigest) || mutation.ExpectedSignalRevision <= 0
            || mutation.Intent is not (ShadowArchiveSignalIntents.Vote or ShadowArchiveSignalIntents.Retract)
            || !HasText(mutation.IdempotencyKey) || mutation.IdempotencyKey.Trim().Length > 128)
        {
            return "signal_mutation_invalid";
        }

        return null;
    }

    private static bool ValidArtifact(
        ShadowArchiveDownloadArtifactContract artifact,
        ShadowArchivePublicReaderContract reader)
        => HasText(artifact.ArtifactId)
            && HasText(artifact.Format)
            && HasText(artifact.DisplayName)
            && HasText(artifact.MediaType)
            && artifact.Format is "html" or "markdown" or "pdf" or "epub" or "docx"
            && artifact.ByteSize > 0
            && IsDigest(artifact.Sha256)
            && IsDigest(artifact.RendererReceiptDigest)
            && ValidPublicUri(artifact.DownloadUri)
            && artifact.PublicationRevision == reader.PublicationRevision
            && DigestEquals(artifact.ContentDigest, reader.ContentDigest)
            && HasText(artifact.License)
            && string.Equals(artifact.TechnicalAuthor, ShadowArchiveContractNames.TechnicalCredit, StringComparison.Ordinal);

    private static bool ValidIdentity(ShadowArchivePersonContract runner, ShadowArchivePersonContract owner)
        => runner is not null && owner is not null
            && HasText(runner.UserId) && HasText(runner.DisplayName)
            && HasText(owner.UserId) && HasText(owner.DisplayName);

    private static bool ValidTechnicalCredit(string author, string publisher)
        => string.Equals(author, ShadowArchiveContractNames.TechnicalCredit, StringComparison.Ordinal)
            && string.Equals(publisher, ShadowArchiveContractNames.TechnicalCredit, StringComparison.Ordinal);

    private static bool KnownPublicationStatus(string value)
        => value is ShadowArchivePublicationStatuses.Draft
            or ShadowArchivePublicationStatuses.Reviewing
            or ShadowArchivePublicationStatuses.Published
            or ShadowArchivePublicationStatuses.Unpublished
            or ShadowArchivePublicationStatuses.Superseded
            or ShadowArchivePublicationStatuses.ModerationHeld
            or ShadowArchivePublicationStatuses.Removed;

    private static ShadowArchiveStoryIdentityViewModel Identity(
        ShadowArchivePersonContract runner,
        ShadowArchivePersonContract owner)
        => new(
            RunnerHeading: Clean(runner.DisplayName),
            RunnerHandle: CleanOptional(runner.Handle),
            StoryOwnerLabel: Clean(owner.DisplayName),
            StoryOwnerHandle: CleanOptional(owner.Handle),
            TechnicalMetadataCredit: ShadowArchiveContractNames.TechnicalCredit);

    private static ShadowArchivePresentationResult<TView> Failure<TContract, TView>(
        ShadowArchiveClientResult<TContract> result)
    {
        ShadowArchivePresentationState state = result.Kind switch
        {
            ShadowArchiveClientResultKind.NotFinalized => ShadowArchivePresentationState.NotFinalized,
            ShadowArchiveClientResultKind.Stale => ShadowArchivePresentationState.Stale,
            ShadowArchiveClientResultKind.RevisionConflict => ShadowArchivePresentationState.RevisionConflict,
            ShadowArchiveClientResultKind.AuthenticationRequired => ShadowArchivePresentationState.AuthenticationRequired,
            ShadowArchiveClientResultKind.Forbidden => ShadowArchivePresentationState.Forbidden,
            ShadowArchiveClientResultKind.NotFound => ShadowArchivePresentationState.NotFound,
            ShadowArchiveClientResultKind.Removed => ShadowArchivePresentationState.Removed,
            ShadowArchiveClientResultKind.ModerationHeld => ShadowArchivePresentationState.ModerationHeld,
            ShadowArchiveClientResultKind.Offline => ShadowArchivePresentationState.Offline,
            ShadowArchiveClientResultKind.RateLimited => ShadowArchivePresentationState.RateLimited,
            _ => ShadowArchivePresentationState.Unavailable
        };
        string code = HasText(result.ErrorCode) ? result.ErrorCode! : ToErrorCode(state);
        string message = HasText(result.SafeMessage) ? Clean(result.SafeMessage!) : GenericUnavailableMessage;
        return new(state, default, new(
            code,
            message,
            result.ExpectedRevision,
            result.CurrentRevision,
            NormalizeOptionalDigest(result.ExpectedDigest),
            NormalizeOptionalDigest(result.CurrentDigest),
            result.RetryAfter));
    }

    private static ShadowArchivePresentationResult<T> Invalid<T>(string code)
        => StateFailure<T>(ShadowArchivePresentationState.InvalidContract, code,
            "Shadow Archive returned an invalid or unbound response. Nothing was published or changed.");

    private static ShadowArchivePresentationResult<T> Unavailable<T>()
        => StateFailure<T>(ShadowArchivePresentationState.Unavailable, "shadow_archive_unavailable", GenericUnavailableMessage);

    private static ShadowArchivePresentationResult<T> StateFailure<T>(
        ShadowArchivePresentationState state,
        string code,
        string message)
        => new(state, default, new(code, message, null, null, null, null, null));

    private static ShadowArchivePresentationResult<T> BindingFailure<T>(
        ShadowArchivePresentationState state,
        string code,
        string message,
        long? expectedRevision,
        long? currentRevision,
        string? expectedDigest,
        string? currentDigest)
        => new(state, default, new(
            code,
            message,
            expectedRevision,
            currentRevision,
            NormalizeOptionalDigest(expectedDigest),
            NormalizeOptionalDigest(currentDigest),
            null));

    private static ShadowArchiveSignalCommandResult CommandFailure(
        ShadowArchivePresentationState state,
        string code,
        string message)
        => new(state, null, new(code, message, null, null, null, null, null));

    private static string ToErrorCode(ShadowArchivePresentationState state)
        => state.ToString().ToLowerInvariant();

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsDigest(string? value)
    {
        if (!HasText(value))
        {
            return false;
        }

        ReadOnlySpan<char> digest = value.AsSpan().Trim();
        if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            digest = digest[7..];
        }

        return digest.Length == 64 && digest.ToString().All(Uri.IsHexDigit);
    }

    private static bool DigestEquals(string left, string right)
        => IsDigest(left) && IsDigest(right)
            && string.Equals(NormalizeDigest(left), NormalizeDigest(right), StringComparison.Ordinal);

    private static string NormalizeDigest(string value)
    {
        string digest = value.Trim();
        if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            digest = digest[7..];
        }

        return digest.ToLowerInvariant();
    }

    private static string? NormalizeOptionalDigest(string? value)
        => IsDigest(value) ? NormalizeDigest(value!) : value;

    private static bool ValidPublicUri(Uri uri)
        => uri.IsAbsoluteUri && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool ValidOptionalPublicUri(Uri? uri) => uri is null || ValidPublicUri(uri);

    private static string Clean(string value) => PlayerFacingCopyHumanizer.Clean(value);

    private static string? CleanOptional(string? value)
        => HasText(value) ? Clean(value!) : null;
}
