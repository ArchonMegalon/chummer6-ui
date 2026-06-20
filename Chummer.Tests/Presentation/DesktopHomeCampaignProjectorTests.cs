#nullable enable annotations

using System;
using Chummer.Campaign.Contracts;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopHomeCampaignProjectorTests
{
    [TestMethod]
    public void Campaign_server_plane_projection_includes_adoption_goal_pin_resolution_and_black_ledger_copy()
    {
        DesktopHomeCampaignServerPlane projection = CreateServerPlaneDto().ToProjection();

        Assert.AreEqual("Campaign adoption stays attached to this desktop.", projection.AdoptionSummary);
        Assert.AreEqual("playable_with_review because one aftermath note still needs approval.", projection.AdoptionConfidenceSummary);
        Assert.AreEqual("Ghostwire upgrade (47k / 149k nuyen); Apex fixer follow-up", projection.GoalPinSummary);
        Assert.AreEqual("ResolutionReport closeout is approved and ready to publish the player-safe recap.", projection.ResolutionReportSummary);
        Assert.AreEqual("BLACK LEDGER consequence keeps the consequence record and recap publication on the same reviewed chain.", projection.BlackLedgerSummary);
        Assert.AreEqual("BLACK LEDGER details record binds adoption, resolution, and recap details together.", projection.BlackLedgerProofSummary);

        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "Campaign adoption: Campaign adoption stays attached to this desktop.");
        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "Adoption details: Adoption record adopt-001 keeps the remaining cleanup trail visible.");
        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "Goal pins: Ghostwire upgrade (47k / 149k nuyen); Apex fixer follow-up");
        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "ResolutionReport closeout: ResolutionReport closeout is approved and ready to publish the player-safe recap.");
        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "BLACK LEDGER consequence: BLACK LEDGER consequence keeps the consequence record and recap publication on the same reviewed chain.");
        CollectionAssert.Contains((System.Collections.ICollection)projection.ReadinessHighlights, "BLACK LEDGER details: BLACK LEDGER details record binds adoption, resolution, and recap details together.");
    }

    [TestMethod]
    public void Grounded_campaign_projector_keeps_adoption_and_black_ledger_receipts_visible()
    {
        DesktopHomeCampaignServerPlane serverPlane = CreateServerPlaneDto().ToProjection();
        AccountCampaignSummary summary = new(
            Dossiers: [],
            Campaigns: [],
            Runs: [],
            Crews: [],
            Workspaces: [],
            CommunityOperations: [],
            BuildLabHandoffs: [],
            RulesNavigator: [],
            MigrationReceipts: [],
            CreatorPublications: [],
            Restore: new WorkspaceRestoreProjection(
                RestoreId: "restore-1",
                UserId: "user-1",
                RecentDossiers: [],
                RecentCampaigns: [],
                RecentRuleEnvironments: [],
                RecentArtifacts: [],
                Entitlements: [],
                ClaimedDevices: [],
                ConflictSummaries: [],
                LocalOnlyNotes: [],
                GeneratedAtUtc: DateTimeOffset.Parse("2026-05-16T20:00:00Z")));

        DesktopHomeCampaignProjection projection = DesktopHomeCampaignProjector.Create(summary, serverPlane: serverPlane);
        string highlights = string.Join("\n", projection.ReadinessHighlights);

        StringAssert.Contains(highlights, "Campaign adoption: Campaign adoption stays attached to this desktop.");
        StringAssert.Contains(highlights, "Adoption confidence: playable_with_review because one aftermath note still needs approval.");
        StringAssert.Contains(highlights, "Adoption details: Adoption record adopt-001 keeps the remaining cleanup trail visible.");
        StringAssert.Contains(highlights, "Goal pins: Ghostwire upgrade (47k / 149k nuyen); Apex fixer follow-up");
        StringAssert.Contains(highlights, "ResolutionReport closeout: ResolutionReport closeout is approved and ready to publish the player-safe recap.");
        StringAssert.Contains(highlights, "BLACK LEDGER consequence: BLACK LEDGER consequence keeps the consequence record and recap publication on the same reviewed chain.");
        StringAssert.Contains(highlights, "BLACK LEDGER details: BLACK LEDGER details record binds adoption, resolution, and recap details together.");
    }

    private static DesktopHomeCampaignServerPlaneDto CreateServerPlaneDto()
        => new(
            Workspace: new DesktopHomeWorkspaceSummaryDto("workspace-1"),
            CampaignSummary: new DesktopHomeCampaignSummaryDto(
                SessionReadinessSummary: "Session return is ready.",
                RestoreSummary: "Restore packet stays attached to the claimed desktop.",
                PublicationSummary: "Publication shelf is current."),
            RosterReadiness: new DesktopHomeRosterReadinessDto("Roster stays aligned."),
            ReadinessCues: [],
            ChangePackets: [],
            RosterTransfers: [],
            DossierFreshness: [],
            RuleEnvironmentHealth: [],
            Runboard: null,
            ContinuityConflicts: [],
            RecapShelf: [],
            SupportClosures: [],
            KnownIssues: [],
            DecisionNotices: [],
            TravelMode: null,
            FirstPlayableSession: null,
            CampaignMemory: null,
            Adoption: new DesktopHomeCampaignAdoptionDto(
                Summary: "Campaign adoption stays attached to the same claimed desktop lane.",
                ConfidenceSummary: "playable_with_review because one aftermath note still needs approval.",
                NextSafeAction: "Review the aftermath note before promoting the lane.",
                EvidenceLines:
                [
                    "Adoption receipt adopt-001 keeps the remaining cleanup trail visible."
                ]),
            GoalPins:
            [
                new DesktopHomeRunnerGoalPinDto(
                    RunnerHandle: "ghostwire",
                    Label: "Ghostwire upgrade",
                    ProgressSummary: "47k / 149k nuyen",
                    NextSafeAction: "Keep saving."),
                new DesktopHomeRunnerGoalPinDto(
                    RunnerHandle: "apex",
                    Label: "Apex fixer follow-up",
                    ProgressSummary: null,
                    NextSafeAction: "Review the fixer debt.")
            ],
            ResolutionReport: new DesktopHomeResolutionReportCloseoutDto(
                Summary: "ResolutionReport closeout is approved and ready to publish the player-safe recap.",
                NextSafeAction: "Publish the recap.",
                EvidenceLines:
                [
                    "Resolution report receipt rr-001 is approved."
                ]),
            BlackLedger: new DesktopHomeBlackLedgerConsequenceDto(
                Summary: "BLACK LEDGER consequence keeps the consequence receipt and recap publication on the same governed chain.",
                ProofSummary: "BLACK LEDGER proof receipt binds adoption, resolution, and recap proof together.",
                SpoilerClass: "player_safe",
                NextSafeAction: "Review the governed consequence.",
                EvidenceLines:
                [
                    "Consequence receipt consequence-001 is attached."
                ]),
            NextSafeAction: new DesktopHomeNextSafeActionCueDto("Review the aftermath note before reopening the shared lane."),
            GeneratedAtUtc: DateTimeOffset.Parse("2026-05-16T20:00:00Z"));
}
