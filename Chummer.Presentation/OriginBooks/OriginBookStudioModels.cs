using Chummer.Run.Contracts.Community;

namespace Chummer.Presentation.OriginBooks;

internal static class OriginBookProjectKinds
{
    internal const string OriginDossier = "origin_dossier";
    internal const string NarrativeOrigin = "narrative_origin";
    internal const string RunnerMemoir = "runner_memoir";
    internal const string IntelligenceCasefile = "intelligence_casefile";
}

internal static class OriginBookProviderStrategies
{
    internal const string InternalCanonical = "internal_canonical";
    internal const string YoubooksGroundedDrafting = "youbooks_grounded_drafting";
    internal const string InkfluenceNarrativeEdition = "inkfluence_narrative_edition";
    internal const string PremiumGuidedAuthoring = "premium_guided_authoring";
}

internal static class OriginBookPostProcessingSteps
{
    internal const string UndetectableHumanizer = "undetectable_humanizer";
}

internal static class OriginBookProjectStatuses
{
    internal const string DraftReview = "draft_review";
    internal const string ApprovedStory = "approved_story";
}

internal static class OriginBookProjectPhases
{
    internal const string StoryDraft = "story_draft";
    internal const string CanonApproved = "canon_approved";
    internal const string InternalEditionPreparation = "internal_edition_preparation";
    internal const string ProviderAuthoringQueued = "provider_authoring_queued";
    internal const string PremiumManuscriptQueued = "premium_manuscript_queued";
    internal const string MediaPreparation = "media_preparation";
}

internal static class OriginBookReviewStates
{
    internal const string DraftReview = "draft_review";
    internal const string CanonApproved = "canon_approved";
    internal const string ProviderManuscriptReviewRequired = "provider_manuscript_review_required";
    internal const string PremiumOutlineReviewRequired = "premium_outline_review_required";
    internal const string PremiumChapterReviewRequired = "premium_chapter_review_required";
}

internal static class OriginBookCanonAuditStates
{
    internal const string ProvisionalPass = "provisional_pass";
    internal const string ReviewRequired = "review_required";
    internal const string Blocked = "blocked";
}

internal static class OriginBookPremiumReviewStates
{
    internal const string NotApplicable = "not_applicable";
    internal const string OutlineReviewRequired = "outline_review_required";
    internal const string ChapterReviewRequired = "chapter_review_required";
    internal const string ReadyForOperatorQueue = "ready_for_operator_queue";
}

internal static class OriginBookPublicationStates
{
    internal const string AwaitingProviderManuscript = "awaiting_provider_manuscript";
    internal const string AwaitingHumanizedManuscript = "awaiting_humanized_manuscript";
    internal const string AwaitingStorySceneCover = "awaiting_story_scene_cover";
    internal const string AwaitingAudiobookshelfShare = "awaiting_audiobookshelf_share";
    internal const string PublishedForOwner = "published_for_owner";
}

internal sealed record OriginBookSourcePacket(
    string BookKind,
    string ProviderStrategy,
    string Alias,
    string Metatype,
    string BuildMethod,
    string RulesetId,
    string ArchetypeHint,
    string Prompt,
    string? GmAllowanceNotes,
    string? BookSurface,
    string? PrimaryVoiceStyle,
    string? AlternateVoiceStyle,
    string? PortraitStyle,
    string? VideoStyle,
    IReadOnlyList<string> GmConstraintLabels,
    string? WorkspaceName,
    string? LeadBuildPathTitle,
    string? LeadHandoffTitle,
    IReadOnlyList<string> CausalityHints,
    IReadOnlyList<string> StandoutSignals,
    IReadOnlyList<string> ContradictionFlags,
    string? RuntimeFingerprint = null)
{
    internal string Alias { get; init; } = PlayerFacingCopyHumanizer.Clean(Alias);
    internal string Metatype { get; init; } = PlayerFacingCopyHumanizer.Clean(Metatype);
    internal string BuildMethod { get; init; } = PlayerFacingCopyHumanizer.Clean(BuildMethod);
    internal string ArchetypeHint { get; init; } = PlayerFacingCopyHumanizer.Clean(ArchetypeHint);
    internal string Prompt { get; init; } = PlayerFacingCopyHumanizer.Clean(Prompt);
    internal string? GmAllowanceNotes { get; init; } = CleanOptional(GmAllowanceNotes);
    internal string? BookSurface { get; init; } = CleanOptional(BookSurface);
    internal string? PrimaryVoiceStyle { get; init; } = CleanOptional(PrimaryVoiceStyle);
    internal string? AlternateVoiceStyle { get; init; } = CleanOptional(AlternateVoiceStyle);
    internal string? PortraitStyle { get; init; } = CleanOptional(PortraitStyle);
    internal string? VideoStyle { get; init; } = CleanOptional(VideoStyle);
    internal IReadOnlyList<string> GmConstraintLabels { get; init; } = PlayerFacingCopyHumanizer.CleanLines(GmConstraintLabels);
    internal string? WorkspaceName { get; init; } = CleanOptional(WorkspaceName);
    internal string? LeadBuildPathTitle { get; init; } = CleanOptional(LeadBuildPathTitle);
    internal string? LeadHandoffTitle { get; init; } = CleanOptional(LeadHandoffTitle);
    internal IReadOnlyList<string> CausalityHints { get; init; } = PlayerFacingCopyHumanizer.CleanLines(CausalityHints);
    internal IReadOnlyList<string> StandoutSignals { get; init; } = PlayerFacingCopyHumanizer.CleanLines(StandoutSignals);
    internal IReadOnlyList<string> ContradictionFlags { get; init; } = PlayerFacingCopyHumanizer.CleanLines(ContradictionFlags);

    private static string? CleanOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? value : PlayerFacingCopyHumanizer.Clean(value);
}

internal sealed record OriginBookCanonDraft(
    string Summary,
    string Prose,
    IReadOnlyList<string> GmHooks,
    string? RuntimeFingerprint = null)
{
    internal string Summary { get; init; } = PlayerFacingCopyHumanizer.Clean(Summary);
    internal string Prose { get; init; } = PlayerFacingCopyHumanizer.Clean(Prose);
    internal IReadOnlyList<string> GmHooks { get; init; } = PlayerFacingCopyHumanizer.CleanLines(GmHooks);
}

internal sealed record OriginBookApprovalState(
    string ProjectPhase,
    string ReviewState,
    DateTimeOffset ApprovedAtUtc,
    string ApprovedBy);

internal sealed record OriginBookPremiumManuscriptPlan(
    bool PremiumGuidedAuthoringRequired,
    string QueueStatus,
    string Provider,
    string ManuscriptTarget,
    string OutlinePosture,
    bool HumanChapterReviewRequired);

internal sealed record OriginBookCanonAudit(
    string AuditStatus,
    IReadOnlyList<string> HardConflicts,
    IReadOnlyList<string> ProbableConflicts,
    IReadOnlyList<string> InventedEntities,
    IReadOnlyList<string> InventedGameEffects,
    IReadOnlyList<string> PrivacyFindings)
{
    internal IReadOnlyList<string> HardConflicts { get; init; } = PlayerFacingCopyHumanizer.CleanLines(HardConflicts);
    internal IReadOnlyList<string> ProbableConflicts { get; init; } = PlayerFacingCopyHumanizer.CleanLines(ProbableConflicts);
    internal IReadOnlyList<string> InventedEntities { get; init; } = PlayerFacingCopyHumanizer.CleanLines(InventedEntities);
    internal IReadOnlyList<string> InventedGameEffects { get; init; } = PlayerFacingCopyHumanizer.CleanLines(InventedGameEffects);
    internal IReadOnlyList<string> PrivacyFindings { get; init; } = PlayerFacingCopyHumanizer.CleanLines(PrivacyFindings);
}

internal sealed record OriginBookPremiumReviewArtifacts(
    string OutlineReviewState,
    string ChapterReviewState,
    string? OutlineMarkdownPath,
    string? ChapterPlanJsonPath,
    IReadOnlyList<string> ChapterReviewPaths);

internal sealed record OriginBookGoldPublication(
    string PublicationState,
    string? ChummerRunOwnerUrl,
    string? BookArtifactUrl,
    string? AudiobookshelfShareUrl,
    string? DossierVideoUrl,
    string? StorySceneCoverUrl,
    string? SourcePacketPath,
    string? SourcePacketReceiptPath,
    string? CanonAuditReceiptPath,
    string? ProviderManuscriptPath,
    string? ProviderManuscriptReceiptPath,
    string? HumanizerReceiptPath,
    string? BookArtifactPath,
    string? BookArtifactReceiptPath,
    string? StorySceneCoverPath,
    string? StorySceneCoverReceiptPath,
    string? AudiobookPath,
    string? AudiobookshelfImportReceiptPath,
    string? DossierVideoPath,
    string? DossierVideoReceiptPath,
    string? TelegramShareDeliveryReceiptPath,
    bool ProviderAuthoredManuscriptImported,
    bool UndetectableHumanizerApplied,
    bool BookArtifactVerified,
    bool DossierVideoVerified,
    bool StorySceneCoverUsesSelectedCharacterFace,
    bool AudiobookshelfPlaybackVerified,
    bool TelegramShareDelivered,
    bool RequiresAuthenticatedChummerRunUser)
{
    internal static OriginBookGoldPublication Pending()
        => new(
            PublicationState: OriginBookPublicationStates.AwaitingProviderManuscript,
            ChummerRunOwnerUrl: null,
            BookArtifactUrl: null,
            AudiobookshelfShareUrl: null,
            DossierVideoUrl: null,
            StorySceneCoverUrl: null,
            SourcePacketPath: null,
            SourcePacketReceiptPath: null,
            CanonAuditReceiptPath: null,
            ProviderManuscriptPath: null,
            ProviderManuscriptReceiptPath: null,
            HumanizerReceiptPath: null,
            BookArtifactPath: null,
            BookArtifactReceiptPath: null,
            StorySceneCoverPath: null,
            StorySceneCoverReceiptPath: null,
            AudiobookPath: null,
            AudiobookshelfImportReceiptPath: null,
            DossierVideoPath: null,
            DossierVideoReceiptPath: null,
            TelegramShareDeliveryReceiptPath: null,
            ProviderAuthoredManuscriptImported: false,
            UndetectableHumanizerApplied: false,
            BookArtifactVerified: false,
            DossierVideoVerified: false,
            StorySceneCoverUsesSelectedCharacterFace: false,
            AudiobookshelfPlaybackVerified: false,
            TelegramShareDelivered: false,
            RequiresAuthenticatedChummerRunUser: true);

    internal bool IsGoldReady
        => string.Equals(PublicationState, OriginBookPublicationStates.PublishedForOwner, StringComparison.Ordinal)
            && ProviderAuthoredManuscriptImported
            && UndetectableHumanizerApplied
            && BookArtifactVerified
            && DossierVideoVerified
            && StorySceneCoverUsesSelectedCharacterFace
            && AudiobookshelfPlaybackVerified
            && TelegramShareDelivered
            && RequiresAuthenticatedChummerRunUser
            && HasRealPath(SourcePacketPath)
            && HasRealPath(SourcePacketReceiptPath)
            && HasRealPath(CanonAuditReceiptPath)
            && HasRealPath(ProviderManuscriptPath)
            && HasRealPath(ProviderManuscriptReceiptPath)
            && HasRealPath(HumanizerReceiptPath)
            && HasRealPath(BookArtifactPath)
            && HasRealPath(BookArtifactReceiptPath)
            && HasRealPath(StorySceneCoverPath)
            && HasRealPath(StorySceneCoverReceiptPath)
            && HasRealPath(AudiobookPath)
            && HasRealPath(AudiobookshelfImportReceiptPath)
            && HasRealPath(DossierVideoPath)
            && HasRealPath(DossierVideoReceiptPath)
            && HasRealPath(TelegramShareDeliveryReceiptPath)
            && HasOwnerArtifactUrl(BookArtifactUrl, "book")
            && HasOwnerArtifactUrl(AudiobookshelfShareUrl, "listen")
            && HasOwnerArtifactUrl(DossierVideoUrl, "video")
            && HasOwnerArtifactUrl(StorySceneCoverUrl, "cover")
            && HasChummerRunOwnerUrl(ChummerRunOwnerUrl);

    internal IReadOnlyList<string> MissingGoldRequirements
    {
        get
        {
            List<string> missing = [];
            if (!string.Equals(PublicationState, OriginBookPublicationStates.PublishedForOwner, StringComparison.Ordinal))
            {
                missing.Add("published_for_owner_state");
            }

            AddIfMissing(missing, HasRealPath(SourcePacketPath) && HasRealPath(SourcePacketReceiptPath), "approved_source_packet_receipt");
            AddIfMissing(missing, HasRealPath(CanonAuditReceiptPath), "chummer_canon_audit_receipt");
            AddIfMissing(missing, ProviderAuthoredManuscriptImported && HasRealPath(ProviderManuscriptPath) && HasRealPath(ProviderManuscriptReceiptPath), "provider_authored_manuscript");
            AddIfMissing(missing, UndetectableHumanizerApplied && HasRealPath(HumanizerReceiptPath), "undetectable_humanizer_receipt");
            AddIfMissing(missing, BookArtifactVerified && HasRealPath(BookArtifactPath) && HasRealPath(BookArtifactReceiptPath) && HasOwnerArtifactUrl(BookArtifactUrl, "book"), "verified_book_artifact");
            AddIfMissing(missing, StorySceneCoverUsesSelectedCharacterFace && HasRealPath(StorySceneCoverPath) && HasRealPath(StorySceneCoverReceiptPath) && HasOwnerArtifactUrl(StorySceneCoverUrl, "cover"), "story_scene_cover_with_selected_face");
            AddIfMissing(missing, HasRealPath(AudiobookPath) && HasRealPath(AudiobookshelfImportReceiptPath), "audiobook_import_receipt");
            AddIfMissing(missing, AudiobookshelfPlaybackVerified && HasOwnerArtifactUrl(AudiobookshelfShareUrl, "listen"), "verified_audiobookshelf_playback_share");
            AddIfMissing(missing, DossierVideoVerified && HasRealPath(DossierVideoPath) && HasRealPath(DossierVideoReceiptPath) && HasOwnerArtifactUrl(DossierVideoUrl, "video"), "verified_dossier_video");
            AddIfMissing(missing, TelegramShareDelivered && HasRealPath(TelegramShareDeliveryReceiptPath), "telegram_share_delivery_receipt");
            AddIfMissing(missing, RequiresAuthenticatedChummerRunUser && HasChummerRunOwnerUrl(ChummerRunOwnerUrl), "authenticated_chummer_run_owner_url");
            return missing;
        }
    }

    private static void AddIfMissing(List<string> missing, bool passed, string requirement)
    {
        if (!passed)
        {
            missing.Add(requirement);
        }
    }

    private static bool HasHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "https" or "http";

    private static bool HasChummerRunOwnerUrl(string? value)
        => HasHttpUrl(value)
            && value!.Contains("chummer.run", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.AbsolutePath.Contains("/account/work/origin-dossiers/", StringComparison.OrdinalIgnoreCase);

    private static bool HasOwnerArtifactUrl(string? value, string artifactKind)
        => HasChummerRunOwnerUrl(value)
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.AbsolutePath.EndsWith($"/{artifactKind}", StringComparison.OrdinalIgnoreCase);

    private static bool HasRealPath(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !value.Contains("stub", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("fallback", StringComparison.OrdinalIgnoreCase);
}

internal sealed record OriginBookArtifactSet(
    string BundleDirectory,
    string CanonJsonPath,
    string CanonMarkdownPath,
    string ProjectArchiveJsonPath,
    string MyFirstBookPacketPath,
    string MyFirstBookPresentationPath,
    string? DossierPdfPath,
    string? MarkupGoPacketPath,
    string? PortraitSetJsonPath,
    string? PortraitContactSheetPath,
    IReadOnlyList<string> PortraitCandidatePaths,
    string? SelectedPortraitPath,
    string? SceneBriefMarkdownPath,
    string? SceneSetJsonPath,
    IReadOnlyList<string> SceneCandidatePaths,
    string? SelectedScenePath,
    string? InkfluencePacketPath,
    string? InkfluenceScriptPath,
    string? UnmixrPacketPath,
    string? UnmixrScriptPath,
    string? MediaFactoryNarrationRequestPath,
    string? MediaFactoryNarrationRunbookPath,
    string? MediaFactoryNarrationReceiptPath,
    string? VidBoardPacketPath,
    string? VideoStoryboardPath,
    string? VideoPosterPath,
    string? MediaFactoryVideoReceiptPath,
    string? RenderedVideoPath);

internal sealed record OriginBookProject(
    string ProjectId,
    string BookKind,
    string ProjectStatus,
    OriginBookSourcePacket Packet,
    OriginBookCanonDraft Canon,
    OriginBookCanonAudit CanonAudit,
    OriginBookApprovalState Approval,
    OriginBookPremiumManuscriptPlan PremiumPlan,
    OriginBookPremiumReviewArtifacts PremiumReview,
    OriginBookGoldPublication Publication,
    OriginBookArtifactSet Artifacts,
    string? GmAllowanceNotes,
    string? RuntimeFingerprint = null)
{
    internal string? GmAllowanceNotes { get; init; } = string.IsNullOrWhiteSpace(GmAllowanceNotes)
        ? GmAllowanceNotes
        : PlayerFacingCopyHumanizer.Clean(GmAllowanceNotes);

    internal DateTimeOffset ApprovedAtUtc => Approval.ApprovedAtUtc;
    internal string ProjectPhase => Approval.ProjectPhase;
    internal string ReviewState => Approval.ReviewState;
    internal string AuditStatus => CanonAudit.AuditStatus;
    internal string OutlineReviewState => PremiumReview.OutlineReviewState;
    internal string ChapterReviewState => PremiumReview.ChapterReviewState;
    internal string? PremiumOutlineMarkdownPath => PremiumReview.OutlineMarkdownPath;
    internal string? PremiumChapterPlanJsonPath => PremiumReview.ChapterPlanJsonPath;
    internal IReadOnlyList<string> PremiumChapterReviewPaths => PremiumReview.ChapterReviewPaths;
    internal string BundleDirectory => Artifacts.BundleDirectory;
    internal string CanonJsonPath => Artifacts.CanonJsonPath;
    internal string CanonMarkdownPath => Artifacts.CanonMarkdownPath;
    internal string ProjectArchiveJsonPath => Artifacts.ProjectArchiveJsonPath;
    internal string MyFirstBookPacketPath => Artifacts.MyFirstBookPacketPath;
    internal string MyFirstBookPresentationPath => Artifacts.MyFirstBookPresentationPath;
    internal string? DossierPdfPath => Artifacts.DossierPdfPath;
    internal string? MarkupGoPacketPath => Artifacts.MarkupGoPacketPath;
    internal string? PortraitSetJsonPath => Artifacts.PortraitSetJsonPath;
    internal string? PortraitContactSheetPath => Artifacts.PortraitContactSheetPath;
    internal IReadOnlyList<string> PortraitCandidatePaths => Artifacts.PortraitCandidatePaths;
    internal string? SelectedPortraitPath => Artifacts.SelectedPortraitPath;
    internal string? SceneBriefMarkdownPath => Artifacts.SceneBriefMarkdownPath;
    internal string? SceneSetJsonPath => Artifacts.SceneSetJsonPath;
    internal IReadOnlyList<string> SceneCandidatePaths => Artifacts.SceneCandidatePaths;
    internal string? SelectedScenePath => Artifacts.SelectedScenePath;
    internal string? InkfluencePacketPath => Artifacts.InkfluencePacketPath;
    internal string? InkfluenceScriptPath => Artifacts.InkfluenceScriptPath;
    internal string? UnmixrPacketPath => Artifacts.UnmixrPacketPath;
    internal string? UnmixrScriptPath => Artifacts.UnmixrScriptPath;
    internal string? MediaFactoryNarrationRequestPath => Artifacts.MediaFactoryNarrationRequestPath;
    internal string? MediaFactoryNarrationRunbookPath => Artifacts.MediaFactoryNarrationRunbookPath;
    internal string? MediaFactoryNarrationReceiptPath => Artifacts.MediaFactoryNarrationReceiptPath;
    internal string? VidBoardPacketPath => Artifacts.VidBoardPacketPath;
    internal string? VideoStoryboardPath => Artifacts.VideoStoryboardPath;
    internal string? VideoPosterPath => Artifacts.VideoPosterPath;
    internal string? MediaFactoryVideoReceiptPath => Artifacts.MediaFactoryVideoReceiptPath;
    internal string? RenderedVideoPath => Artifacts.RenderedVideoPath;

    internal OriginDossierPublicationImportRequest ToPublicationImportRequest()
        => new(
            ProjectId: ProjectId,
            Title: $"{Packet.Alias} Origin Dossier",
            RunnerAlias: Packet.Alias,
            PublicationState: Publication.PublicationState,
            BookArtifactUrl: Publication.BookArtifactUrl,
            AudiobookshelfShareUrl: Publication.AudiobookshelfShareUrl,
            DossierVideoUrl: Publication.DossierVideoUrl,
            StorySceneCoverUrl: Publication.StorySceneCoverUrl,
            ProviderAuthoredManuscriptImported: Publication.ProviderAuthoredManuscriptImported,
            UndetectableHumanizerApplied: Publication.UndetectableHumanizerApplied,
            BookArtifactVerified: Publication.BookArtifactVerified,
            DossierVideoVerified: Publication.DossierVideoVerified,
            StorySceneCoverUsesSelectedCharacterFace: Publication.StorySceneCoverUsesSelectedCharacterFace,
            AudiobookshelfPlaybackVerified: Publication.AudiobookshelfPlaybackVerified,
            TelegramShareDelivered: Publication.TelegramShareDelivered,
            SourcePacketPath: Publication.SourcePacketPath,
            SourcePacketReceiptPath: Publication.SourcePacketReceiptPath,
            CanonAuditReceiptPath: Publication.CanonAuditReceiptPath,
            ProviderManuscriptPath: Publication.ProviderManuscriptPath,
            ProviderManuscriptReceiptPath: Publication.ProviderManuscriptReceiptPath,
            HumanizerReceiptPath: Publication.HumanizerReceiptPath,
            BookArtifactPath: Publication.BookArtifactPath,
            BookArtifactReceiptPath: Publication.BookArtifactReceiptPath,
            StorySceneCoverPath: Publication.StorySceneCoverPath,
            StorySceneCoverReceiptPath: Publication.StorySceneCoverReceiptPath,
            AudiobookPath: Publication.AudiobookPath,
            AudiobookshelfImportReceiptPath: Publication.AudiobookshelfImportReceiptPath,
            DossierVideoPath: Publication.DossierVideoPath,
            DossierVideoReceiptPath: Publication.DossierVideoReceiptPath,
            TelegramShareDeliveryReceiptPath: Publication.TelegramShareDeliveryReceiptPath,
            MissingGoldRequirements: Publication.MissingGoldRequirements);
}
