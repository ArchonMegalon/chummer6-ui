using System.IO;
using Chummer.Presentation.OriginBooks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class OriginBookStudioModelTests
{
    [TestMethod]
    public void OriginBookStudio_models_define_project_packet_approval_and_artifact_contracts()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "OriginBooks", "OriginBookStudioModels.cs"));

        StringAssert.Contains(source, "internal static class OriginBookProjectKinds");
        StringAssert.Contains(source, "internal static class OriginBookProjectStatuses");
        StringAssert.Contains(source, "YoubooksGroundedDrafting");
        StringAssert.Contains(source, "InkfluenceNarrativeEdition");
        StringAssert.Contains(source, "UndetectableHumanizer");
        StringAssert.Contains(source, "internal sealed record OriginBookSourcePacket");
        StringAssert.Contains(source, "internal sealed record OriginBookCanonDraft");
        StringAssert.Contains(source, "internal sealed record OriginBookApprovalState");
        StringAssert.Contains(source, "internal sealed record OriginBookPremiumManuscriptPlan");
        StringAssert.Contains(source, "internal sealed record OriginBookCanonAudit");
        StringAssert.Contains(source, "internal sealed record OriginBookPremiumReviewArtifacts");
        StringAssert.Contains(source, "internal sealed record OriginBookGoldPublication");
        StringAssert.Contains(source, "internal sealed record OriginBookArtifactSet");
        StringAssert.Contains(source, "internal sealed record OriginBookProject");
        StringAssert.Contains(source, "internal static class OriginBookProjectPhases");
        StringAssert.Contains(source, "internal static class OriginBookReviewStates");
        StringAssert.Contains(source, "internal static class OriginBookCanonAuditStates");
        StringAssert.Contains(source, "internal static class OriginBookPremiumReviewStates");
        StringAssert.Contains(source, "internal static class OriginBookPublicationStates");
        StringAssert.Contains(source, "CanonAudit");
        StringAssert.Contains(source, "PremiumReview");
        StringAssert.Contains(source, "Publication");
        StringAssert.Contains(source, "ProjectArchiveJsonPath");
        StringAssert.Contains(source, "MyFirstBookPresentationPath");
        StringAssert.Contains(source, "InkfluencePacketPath");
        StringAssert.Contains(source, "MediaFactoryNarrationReceiptPath");
        StringAssert.Contains(source, "RenderedVideoPath");
        StringAssert.Contains(source, "ToPublicationImportRequest");
        StringAssert.Contains(source, "OriginDossierPublicationImportRequest");
    }

    [TestMethod]
    public void DesktopAliceWindow_persists_origin_book_project_archive()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "origin-book-project.json");
        StringAssert.Contains(source, "OriginBookProjectKinds.OriginDossier");
        StringAssert.Contains(source, "OriginBookProviderStrategies.PremiumGuidedAuthoring");
        StringAssert.Contains(source, "OriginBookProjectPhases.PremiumManuscriptQueued");
        StringAssert.Contains(source, "OriginBookReviewStates.PremiumOutlineReviewRequired");
        StringAssert.Contains(source, "BuildOriginCanonAudit(");
        StringAssert.Contains(source, "BuildOriginPremiumReviewArtifacts(");
        StringAssert.Contains(source, "premium-outline-review.md");
        StringAssert.Contains(source, "premium-chapter-plan.json");
        StringAssert.Contains(source, "Canon audit:");
        StringAssert.Contains(source, "Outline review:");
        StringAssert.Contains(source, "Premium credit spend: deferred until live premium authoring is explicitly enabled.");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_ALLOW_LIVE_EXECUTION");
        StringAssert.Contains(source, "CHUMMER_ORIGIN_ALLOW_LIVE_PREMIUM_CONSUMPTION");
        StringAssert.Contains(source, "PersistOriginBookProjectFiles(");
        StringAssert.Contains(source, "UpdateOriginProjectArtifacts(");
        StringAssert.Contains(source, "Project archive:");
        StringAssert.Contains(source, "Premium manuscript queue:");
        StringAssert.Contains(source, "goldPublicationReady");
        StringAssert.Contains(source, "Gold publication:");
        StringAssert.Contains(source, "Gold missing:");
        StringAssert.Contains(source, "narrationDefault = \"Inkfluence\"");
        StringAssert.Contains(source, "provider = \"Inkfluence\"");
        Assert.IsFalse(source.Contains("CHUMMER_MEDIA_FACTORY_STUB_EXECUTION", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("ExecuteStubOrigin", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("media_factory_stub", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DesktopAliceWindow_routes_origin_authoring_to_book_providers_and_keeps_story_first()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "OriginBookProjectKinds.OriginDossier => OriginBookProviderStrategies.YoubooksGroundedDrafting");
        StringAssert.Contains(source, "OriginBookProjectKinds.IntelligenceCasefile => OriginBookProviderStrategies.YoubooksGroundedDrafting");
        StringAssert.Contains(source, "OriginBookProjectKinds.NarrativeOrigin => OriginBookProviderStrategies.InkfluenceNarrativeEdition");
        StringAssert.Contains(source, "OriginBookPostProcessingSteps.UndetectableHumanizer");
        StringAssert.Contains(source, "BuildOriginStoryParagraphs(packet)");
        StringAssert.Contains(source, "builder.AppendLine(HumanCopy(draft.Prose));");
        Assert.IsTrue(
            source.IndexOf("builder.AppendLine(HumanCopy(draft.Prose));", StringComparison.Ordinal)
                < source.IndexOf("builder.AppendLine(\"## Production Notes\");", StringComparison.Ordinal),
            "Origin markdown must open with story prose before production notes.");
        Assert.IsFalse(source.Contains("thin character summary", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("not unrelated cool ideas", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void OriginBookGoldPublication_requires_real_humanized_cover_playback_and_login_gated_share()
    {
        OriginBookGoldPublication pending = OriginBookGoldPublication.Pending();

        Assert.IsFalse(pending.IsGoldReady);
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "provider_authored_manuscript");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "verified_book_artifact");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "verified_audiobookshelf_playback_share");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "verified_dossier_video");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "telegram_share_delivery_receipt");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "authenticated_chummer_run_owner_url");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "approved_source_packet_receipt");
        CollectionAssert.Contains(pending.MissingGoldRequirements.ToArray(), "chummer_canon_audit_receipt");

        OriginBookGoldPublication stubbed = new(
            PublicationState: OriginBookPublicationStates.PublishedForOwner,
            ChummerRunOwnerUrl: "https://chummer.run/account/work/origin-dossiers/example",
            BookArtifactUrl: "https://chummer.run/account/work/origin-dossiers/example/book",
            AudiobookshelfShareUrl: "https://audio.chummer.run/share/example",
            DossierVideoUrl: "https://chummer.run/account/work/origin-dossiers/example/video",
            StorySceneCoverUrl: "https://chummer.run/media/origin/example-cover.png",
            SourcePacketPath: "/tmp/approved-source-packet.json",
            SourcePacketReceiptPath: "/tmp/approved-source-packet.receipt.json",
            CanonAuditReceiptPath: "/tmp/chummer-canon-audit.receipt.json",
            ProviderManuscriptPath: "/tmp/stub-provider-manuscript.md",
            ProviderManuscriptReceiptPath: "/tmp/provider-receipt.json",
            HumanizerReceiptPath: "/tmp/humanizer-receipt.json",
            BookArtifactPath: "/tmp/book.pdf",
            BookArtifactReceiptPath: "/tmp/book-receipt.json",
            StorySceneCoverPath: "/tmp/scene-cover.png",
            StorySceneCoverReceiptPath: "/tmp/scene-cover-receipt.json",
            AudiobookPath: "/tmp/origin-dossier.m4b",
            AudiobookshelfImportReceiptPath: "/tmp/audiobookshelf-receipt.json",
            DossierVideoPath: "/tmp/dossier-video.mp4",
            DossierVideoReceiptPath: "/tmp/dossier-video-receipt.json",
            TelegramShareDeliveryReceiptPath: "/tmp/telegram-share.json",
            ProviderAuthoredManuscriptImported: true,
            UndetectableHumanizerApplied: true,
            BookArtifactVerified: true,
            DossierVideoVerified: true,
            StorySceneCoverUsesSelectedCharacterFace: true,
            AudiobookshelfPlaybackVerified: true,
            TelegramShareDelivered: true,
            RequiresAuthenticatedChummerRunUser: true);
        Assert.IsFalse(stubbed.IsGoldReady, "Stub-looking provider paths must not satisfy gold readiness.");

        OriginBookGoldPublication ready = stubbed with
        {
            ProviderManuscriptPath = "/secure/origin/provider-manuscript.md",
            AudiobookshelfShareUrl = "https://chummer.run/account/work/origin-dossiers/example/listen",
            StorySceneCoverUrl = "https://chummer.run/account/work/origin-dossiers/example/cover"
        };
        Assert.IsTrue(ready.IsGoldReady);
        Assert.AreEqual(0, ready.MissingGoldRequirements.Count);
    }

    [TestMethod]
    public void OriginBookStudio_models_humanize_generated_story_copy_on_entry()
    {
        OriginBookSourcePacket packet = new(
            BookKind: OriginBookProjectKinds.RunnerMemoir,
            ProviderStrategy: OriginBookProviderStrategies.PremiumGuidedAuthoring,
            Alias: "ALICE flagship alias",
            Metatype: "approved origin canon troll",
            BuildMethod: "reporter-ready SR4 BP",
            RulesetId: "sr4",
            ArchetypeHint: "media-factory decker",
            Prompt: "Use the approved origin canon and explain receipt in a provider lane.",
            GmAllowanceNotes: "Grounded explain receipt only.",
            BookSurface: "Public Proof Shelf",
            PrimaryVoiceStyle: "Unmixr AI",
            AlternateVoiceStyle: "operator voice",
            PortraitStyle: "synthetic flagship client",
            VideoStyle: "reporter-ready release path",
            GmConstraintLabels: ["gm-only proof trail"],
            WorkspaceName: "flagship workspace",
            LeadBuildPathTitle: "provider lane",
            LeadHandoffTitle: "support handoff",
            CausalityHints: ["approved origin canon caused the first contact"],
            StandoutSignals: ["receipt-backed authority truth"],
            ContradictionFlags: ["environment truth disagrees"],
            RuntimeFingerprint: "fp");

        OriginBookCanonDraft draft = new(
            Summary: "ALICE generated an approved origin canon summary.",
            Prose: "The provider delivered a grounded explain receipt through the signed-in support lane.",
            GmHooks: ["proof trail stays visible"],
            RuntimeFingerprint: "fp");

        OriginBookCanonAudit audit = new(
            AuditStatus: OriginBookCanonAuditStates.ReviewRequired,
            HardConflicts: ["provider conflict"],
            ProbableConflicts: ["reporter-ready concern"],
            InventedEntities: ["synthetic contact"],
            InventedGameEffects: ["generated skill"],
            PrivacyFindings: ["signed-in support lane mention"]);

        Assert.AreEqual("Alice desktop alias", packet.Alias);
        Assert.AreEqual("approved origin story troll", packet.Metatype);
        Assert.AreEqual("available SR4 BP", packet.BuildMethod);
        Assert.AreEqual("render decker", packet.ArchetypeHint);
        Assert.IsFalse(packet.Prompt.Contains("provider", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(packet.BookSurface!.Contains("Proof Shelf", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("Unmixr", packet.PrimaryVoiceStyle);
        Assert.AreEqual("default voice", packet.AlternateVoiceStyle);
        AssertLinesEqual(["gm-only Details"], packet.GmConstraintLabels);
        AssertLinesEqual(["approved origin story caused the first contact"], packet.CausalityHints);
        AssertLinesEqual(["reviewed status"], packet.StandoutSignals);
        AssertLinesEqual(["environment details disagrees"], packet.ContradictionFlags);
        StringAssert.Contains(draft.Summary, "created");
        Assert.IsFalse(draft.Prose.Contains("provider", StringComparison.OrdinalIgnoreCase));
        AssertLinesEqual(["Details stays visible"], draft.GmHooks);
        AssertLinesEqual(["service conflict"], audit.HardConflicts);
        AssertLinesEqual(["available concern"], audit.ProbableConflicts);
        AssertLinesEqual(["local contact"], audit.InventedEntities);
        AssertLinesEqual(["created skill"], audit.InventedGameEffects);
        AssertLinesEqual(["account support mention"], audit.PrivacyFindings);
    }

    private static void AssertLinesEqual(string[] expected, IReadOnlyList<string> actual)
    {
        Assert.IsTrue(
            expected.SequenceEqual(actual),
            $"Expected: {string.Join(" | ", expected)}. Actual: {string.Join(" | ", actual)}.");
    }
}
