using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Presentation.OriginBooks.Tests;

[TestClass]
public sealed class ShadowArchivePresenterTests
{
    private const string SourceDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ContentDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ArtifactDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string ReceiptDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    [TestMethod]
    public async Task Preview_Puts_runner_and_owner_before_technical_metadata()
    {
        FakeClient client = new() { PreviewResult = ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>.Succeeded(Preview()) };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel> result =
            await presenter.LoadPublicationPreviewAsync(PreviewQuery(), CancellationToken.None);

        Assert.IsTrue(result.IsReady);
        Assert.AreEqual("Nightshade", result.Value!.Identity.RunnerHeading);
        Assert.AreEqual("Tibor", result.Value.Identity.StoryOwnerLabel);
        Assert.AreEqual("Chummer.run", result.Value.Identity.TechnicalMetadataCredit);
        Assert.IsTrue(result.Value.CanConfirmPublication);
        Assert.IsTrue(result.Value.RequiresExplicitConfirmation);
        Assert.IsEmpty(result.Value.BlockedRequirements);
        Assert.AreEqual(ContentDigest, result.Value.Binding.ContentDigest);
    }

    [TestMethod]
    public async Task Preview_Fails_closed_until_character_is_finalized()
    {
        FakeClient client = new()
        {
            PreviewResult = ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>.Succeeded(
                Preview() with { CharacterFinalized = false })
        };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel> result =
            await presenter.LoadPublicationPreviewAsync(PreviewQuery(), CancellationToken.None);

        Assert.AreEqual(ShadowArchivePresentationState.NotFinalized, result.State);
        Assert.IsNull(result.Value);
        Assert.AreEqual("origin_story_not_finalized", result.Error!.Code);
    }

    [TestMethod]
    public async Task Preview_Reports_source_stale_with_exact_binding()
    {
        FakeClient client = new()
        {
            PreviewResult = ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>.Succeeded(
                Preview() with { WorkspaceRevision = 42 })
        };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel> result =
            await presenter.LoadPublicationPreviewAsync(PreviewQuery(), CancellationToken.None);

        Assert.AreEqual(ShadowArchivePresentationState.Stale, result.State);
        Assert.AreEqual(41L, result.Error!.ExpectedRevision);
        Assert.AreEqual(42L, result.Error.CurrentRevision);
        Assert.AreEqual("origin_story_source_stale", result.Error.Code);
    }

    [TestMethod]
    public async Task Preview_Rejects_a_player_as_technical_author_metadata()
    {
        FakeClient client = new()
        {
            PreviewResult = ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>.Succeeded(
                Preview() with { TechnicalAuthor = "Tibor" })
        };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicationPreviewViewModel> result =
            await presenter.LoadPublicationPreviewAsync(PreviewQuery(), CancellationToken.None);

        Assert.AreEqual(ShadowArchivePresentationState.InvalidContract, result.State);
        Assert.AreEqual("publication_preview_payload_invalid", result.Error!.Code);
    }

    [TestMethod]
    public async Task Reader_Is_public_and_exposes_revision_bound_downloads()
    {
        FakeClient client = new() { ReaderResult = ShadowArchiveClientResult<ShadowArchivePublicReaderContract>.Succeeded(Reader()) };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicReaderViewModel> result =
            await presenter.LoadPublicReaderAsync(ReaderQuery(), CancellationToken.None);

        Assert.IsTrue(result.IsReady);
        Assert.IsTrue(result.Value!.CanReadWithoutAccount);
        Assert.IsTrue(result.Value.CanDownloadWithoutAccount);
        Assert.HasCount(2, result.Value.Chapters);
        Assert.AreEqual("The Choice", result.Value.Chapters[0].Title);
        Assert.IsFalse(result.Value.Chapters[0].AllowsRawHtml);
        Assert.HasCount(1, result.Value.Downloads);
        Assert.AreEqual("Chummer.run", result.Value.Downloads[0].TechnicalMetadataCredit);
    }

    [TestMethod]
    public async Task Reader_Rejects_artifact_from_another_publication_revision()
    {
        ShadowArchivePublicReaderContract reader = Reader();
        ShadowArchiveDownloadArtifactContract invalid = reader.Downloads[0] with { PublicationRevision = 8 };
        FakeClient client = new()
        {
            ReaderResult = ShadowArchiveClientResult<ShadowArchivePublicReaderContract>.Succeeded(
                reader with { Downloads = [invalid] })
        };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchivePublicReaderViewModel> result =
            await presenter.LoadPublicReaderAsync(ReaderQuery(), CancellationToken.None);

        Assert.AreEqual(ShadowArchivePresentationState.InvalidContract, result.State);
        Assert.AreEqual("public_reader_payload_invalid", result.Error!.Code);
    }

    [TestMethod]
    public async Task Anonymous_reader_can_see_votes_but_must_sign_in_to_vote()
    {
        FakeClient client = new() { CommunityResult = ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>.Succeeded(Community()) };
        ShadowArchivePresenter presenter = new(client);
        ShadowArchiveViewerContext anonymous = new(false, null, null);
        ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> loaded =
            await presenter.LoadCommunityAsync(CommunityQuery(), anonymous, CancellationToken.None);

        Assert.IsTrue(loaded.IsReady);
        Assert.AreEqual(17, loaded.Value!.Signal.VoteCount);
        Assert.IsTrue(loaded.Value.Signal.RequiresSignIn);
        Assert.IsFalse(loaded.Value.Signal.CanVote);

        ShadowArchiveSignalCommandResult command = presenter.CreateSignalCommand(
            loaded.Value,
            anonymous,
            ShadowArchiveSignalIntents.Vote,
            "vote-1");
        Assert.AreEqual(ShadowArchivePresentationState.AuthenticationRequired, command.State);
        Assert.IsNull(command.Mutation);
    }

    [TestMethod]
    public async Task Vote_command_is_bound_to_seen_publication_content_and_signal_revision()
    {
        FakeClient client = new() { CommunityResult = ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>.Succeeded(Community()) };
        ShadowArchivePresenter presenter = new(client);
        ShadowArchiveViewerContext viewer = new(true, "user-1", "Tibor");
        ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> loaded =
            await presenter.LoadCommunityAsync(CommunityQuery(), viewer, CancellationToken.None);

        ShadowArchiveSignalCommandResult command = presenter.CreateSignalCommand(
            loaded.Value!,
            viewer,
            ShadowArchiveSignalIntents.Vote,
            "vote-publication-7-signal-12");

        Assert.IsTrue(command.CanSubmit);
        Assert.AreEqual(7L, command.Mutation!.ExpectedPublicationRevision);
        Assert.AreEqual(ContentDigest, command.Mutation.ExpectedContentDigest);
        Assert.AreEqual(12L, command.Mutation.ExpectedSignalRevision);
        Assert.AreEqual(ShadowArchiveSignalIntents.Vote, command.Mutation.Intent);
    }

    [TestMethod]
    public async Task Retract_command_is_available_only_for_the_recorded_vote_revision()
    {
        ShadowArchiveCommunityStatusContract community = Community() with
        {
            Signal = new(12, 17, true, false, true, null)
        };
        FakeClient client = new()
        {
            CommunityResult = ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>.Succeeded(community)
        };
        ShadowArchivePresenter presenter = new(client);
        ShadowArchiveViewerContext viewer = new(true, "user-1", "Tibor");
        ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> loaded =
            await presenter.LoadCommunityAsync(CommunityQuery(), viewer, CancellationToken.None);

        ShadowArchiveSignalCommandResult command = presenter.CreateSignalCommand(
            loaded.Value!,
            viewer,
            ShadowArchiveSignalIntents.Retract,
            "retract-publication-7-signal-12");

        Assert.IsTrue(command.CanSubmit);
        Assert.AreEqual(ShadowArchiveSignalIntents.Retract, command.Mutation!.Intent);
        Assert.AreEqual(12L, command.Mutation.ExpectedSignalRevision);
    }

    [TestMethod]
    public async Task Signal_conflict_never_applies_an_optimistic_vote()
    {
        FakeClient client = new()
        {
            SignalResult = new(
                ShadowArchiveClientResultKind.RevisionConflict,
                ErrorCode: "signal_revision_conflict",
                SafeMessage: "The vote total changed.",
                ExpectedRevision: 12,
                CurrentRevision: 13)
        };
        ShadowArchivePresenter presenter = new(client);
        ShadowArchiveSignalMutation mutation = new(
            ShadowArchiveContractNames.SignalMutation,
            "publication-1",
            7,
            ContentDigest,
            12,
            ShadowArchiveSignalIntents.Vote,
            "vote-1");

        ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> result =
            await presenter.SubmitSignalAsync(mutation, new(true, "user-1", "Tibor"), CancellationToken.None);

        Assert.AreEqual(ShadowArchivePresentationState.RevisionConflict, result.State);
        Assert.IsNull(result.Value);
        Assert.AreEqual(12L, result.Error!.ExpectedRevision);
        Assert.AreEqual(13L, result.Error.CurrentRevision);
    }

    [TestMethod]
    public async Task Community_displays_host_reward_status_without_deriving_it_from_votes()
    {
        ShadowArchiveCommunityStatusContract community = Community() with
        {
            Signal = Community().Signal with { VoteCount = 999 },
            Reward = Community().Reward with { Status = "moderation_review", Eligible = false, HoldReason = "audit_pending" }
        };
        FakeClient client = new() { CommunityResult = ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>.Succeeded(community) };
        ShadowArchivePresenter presenter = new(client);

        ShadowArchivePresentationResult<ShadowArchiveCommunityViewModel> result = await presenter.LoadCommunityAsync(
            CommunityQuery(),
            new(true, "user-1", "Tibor"),
            CancellationToken.None);

        Assert.IsTrue(result.IsReady);
        Assert.AreEqual(999, result.Value!.Signal.VoteCount);
        Assert.IsFalse(result.Value.Reward.Eligible);
        Assert.AreEqual("moderation_review", result.Value.Reward.Status);
        Assert.AreEqual("audit_pending", result.Value.Reward.HoldReason);
    }

    private static ShadowArchivePublicationPreviewQuery PreviewQuery()
        => new("project-1", "workspace-1", 41, SourceDigest);

    private static ShadowArchivePublicReaderQuery ReaderQuery()
        => new("publication-1", 7, ContentDigest);

    private static ShadowArchiveCommunityQuery CommunityQuery()
        => new("publication-1", 7, ContentDigest);

    private static ShadowArchivePersonContract Runner()
        => new("runner-user-1", "Nightshade", "@nightshade");

    private static ShadowArchivePersonContract Owner()
        => new("owner-user-1", "Tibor", "@tibor");

    private static ShadowArchivePublicationPreviewContract Preview()
        => new(
            ShadowArchiveContractNames.PublicationPreview,
            "project-1",
            "workspace-1",
            41,
            SourceDigest,
            "publication-1",
            7,
            ContentDigest,
            true,
            ShadowArchivePublicationStatuses.Reviewing,
            "Nightshade: Before the Shadows",
            "A runner's origin story.",
            "de-AT",
            "CC-BY-NC-4.0",
            true,
            true,
            true,
            [],
            Runner(),
            Owner(),
            ShadowArchiveContractNames.TechnicalCredit,
            ShadowArchiveContractNames.TechnicalCredit,
            null);

    private static ShadowArchivePublicReaderContract Reader()
        => new(
            ShadowArchiveContractNames.PublicReader,
            "publication-1",
            7,
            ContentDigest,
            ShadowArchivePublicationStatuses.Published,
            true,
            false,
            "Nightshade: Before the Shadows",
            "A runner's origin story.",
            "de-AT",
            Runner(),
            Owner(),
            ShadowArchiveContractNames.TechnicalCredit,
            ShadowArchiveContractNames.TechnicalCredit,
            [
                new("chapter-2", 2, "The Cost", "Second chapter."),
                new("chapter-1", 1, "The Choice", "First chapter.")
            ],
            [new(
                "artifact-epub",
                "epub",
                "Download EPUB",
                "application/epub+zip",
                4096,
                ArtifactDigest,
                new Uri("https://chummer.run/archive/publication-1/7/story.epub"),
                7,
                ContentDigest,
                ReceiptDigest,
                "CC-BY-NC-4.0",
                ShadowArchiveContractNames.TechnicalCredit)],
            DateTimeOffset.Parse("2026-08-26T12:00:00Z"));

    private static ShadowArchiveCommunityStatusContract Community()
        => new(
            ShadowArchiveContractNames.CommunityStatus,
            "publication-1",
            7,
            ContentDigest,
            new(12, 17, false, true, false, null),
            new(
                "season-1",
                3,
                true,
                DateTimeOffset.Parse("2026-08-26T12:00:00Z"),
                [new(1, "publication-1", 7, ContentDigest, "Nightshade", "Tibor", 17)]),
            new(
                "eligible_for_owner_acceptance",
                true,
                false,
                null,
                [new("audiobook", "not_started", null, null), new("rendered_scenes", "not_started", null, null)]));

    private sealed class FakeClient : IShadowArchivePresentationClient
    {
        public ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract> PreviewResult { get; init; }
            = new(ShadowArchiveClientResultKind.NotFound);

        public ShadowArchiveClientResult<ShadowArchivePublicReaderContract> ReaderResult { get; init; }
            = new(ShadowArchiveClientResultKind.NotFound);

        public ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract> CommunityResult { get; init; }
            = new(ShadowArchiveClientResultKind.NotFound);

        public ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract> SignalResult { get; init; }
            = new(ShadowArchiveClientResultKind.NotFound);

        public Task<ShadowArchiveClientResult<ShadowArchivePublicationPreviewContract>> GetPublicationPreviewAsync(
            ShadowArchivePublicationPreviewQuery query,
            CancellationToken ct) => Task.FromResult(PreviewResult);

        public Task<ShadowArchiveClientResult<ShadowArchivePublicReaderContract>> GetPublicReaderAsync(
            ShadowArchivePublicReaderQuery query,
            CancellationToken ct) => Task.FromResult(ReaderResult);

        public Task<ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>> GetCommunityStatusAsync(
            ShadowArchiveCommunityQuery query,
            CancellationToken ct) => Task.FromResult(CommunityResult);

        public Task<ShadowArchiveClientResult<ShadowArchiveCommunityStatusContract>> MutateSignalAsync(
            ShadowArchiveSignalMutation mutation,
            CancellationToken ct) => Task.FromResult(SignalResult);
    }
}
