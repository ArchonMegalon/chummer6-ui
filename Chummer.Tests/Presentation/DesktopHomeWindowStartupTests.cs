#nullable enable annotations

using System;
using System.Collections.Generic;
using Chummer.Avalonia;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopHomeWindowStartupTests
{
    [TestMethod]
    public void ShouldShowOnStartup_keeps_first_launch_on_real_workbench_when_no_follow_through_is_needed()
    {
        bool shouldShow = DesktopHomeWindow.ShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "guest"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(watchouts: []),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsFalse(shouldShow, "First launch without install/update/support or restore follow-through should stay on the main workbench quick-start shell.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_keeps_restore_review_visible_when_local_work_is_newer_than_server_continuity()
    {
        WorkspaceListItem[] workspaces =
        [
            new(
                Id: new CharacterWorkspaceId("runner-007"),
                Summary: new CharacterFileSummary(
                    Name: "Runner 007",
                    Alias: "Ghostwire",
                    Metatype: "Human",
                    BuildMethod: "Priority",
                    CreatedVersion: "6.0.0",
                    AppVersion: "6.0.0",
                    Karma: 0m,
                    Nuyen: 0m,
                    Created: true),
                LastUpdatedUtc: new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
                RulesetId: "sr5")
        ];

        bool shouldShow = DesktopHomeWindow.ShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: workspaces,
            campaignProjection: CreateCampaignProjection(watchouts: []),
            campaignServerPlane: new DesktopHomeCampaignServerPlane(
                WorkspaceId: "runner-007",
                SessionReadinessSummary: "Tonight is ready.",
                RestoreSummary: "Server continuity is available.",
                PublicationSummary: "Publication posture is grounded.",
                RosterSummary: "Roster is aligned.",
                RunboardSummary: "Ops digest is current.",
                TravelModeSummary: "Travel cache is ready.",
                TravelPrefetchInventorySummary: "1 packet ready.",
                CampaignMemorySummary: "Memory aligned.",
                CampaignMemoryReturnSummary: "Return route is grounded.",
                AdoptionSummary: null,
                AdoptionConfidenceSummary: null,
                AdoptionEvidenceSummary: null,
                GoalPinSummary: null,
                ResolutionReportSummary: null,
                BlackLedgerSummary: null,
                BlackLedgerProofSummary: null,
                FirstPlayableSession: null,
                NextSafeAction: "Review the calmer workspace digest before replacing local work.",
                ReadinessHighlights: [],
                Watchouts: [],
                SupportHighlights: [],
                DecisionNotices: [],
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 7, 11, 0, 0, TimeSpan.Zero)),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Restore review should still interrupt startup when local work is newer than the server continuity packet.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_opens_restore_review_when_claimed_install_has_server_continuity_but_no_local_workspace()
    {
        bool shouldShow = DesktopHomeWindow.ShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(watchouts: []),
            campaignServerPlane: new DesktopHomeCampaignServerPlane(
                WorkspaceId: "runner-restore",
                SessionReadinessSummary: "Tonight is ready.",
                RestoreSummary: "Server continuity is available.",
                PublicationSummary: "Publication posture is grounded.",
                RosterSummary: "Roster is aligned.",
                RunboardSummary: "Ops digest is current.",
                TravelModeSummary: "Travel cache is ready.",
                TravelPrefetchInventorySummary: "1 packet ready.",
                CampaignMemorySummary: "Memory aligned.",
                CampaignMemoryReturnSummary: "Return route is grounded.",
                AdoptionSummary: null,
                AdoptionConfidenceSummary: null,
                AdoptionEvidenceSummary: null,
                GoalPinSummary: null,
                ResolutionReportSummary: null,
                BlackLedgerSummary: null,
                BlackLedgerProofSummary: null,
                FirstPlayableSession: null,
                NextSafeAction: "Review the restore continuation before opening a fresh workspace.",
                ReadinessHighlights: [],
                Watchouts: [],
                SupportHighlights: [],
                DecisionNotices: [],
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 7, 11, 0, 0, TimeSpan.Zero)),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Claimed installs without a restored local workspace should open the native restore continuation flow when server continuity is present.");
    }

    private static DesktopInstallLinkingState CreateInstallState(string status)
        => new(
            InstallationId: "install-123",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            Status: status,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-07T09:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-07T09:10:00+00:00"),
            LaunchCount: 1,
            LastStartedAtUtc: DateTimeOffset.Parse("2026-05-07T09:10:00+00:00"),
            ClaimedAtUtc: string.Equals(status, "claimed", StringComparison.Ordinal) ? DateTimeOffset.Parse("2026-05-07T09:01:00+00:00") : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: string.Equals(status, "claimed", StringComparison.Ordinal) ? "grant-token" : null);

    private static DesktopUpdateClientStatus CreateUpdateStatus(string status)
        => new(
            HeadId: "avalonia",
            InstalledVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "/downloads/manifest.json",
            LastCheckedAtUtc: DateTimeOffset.Parse("2026-05-07T09:12:00+00:00"),
            LastManifestVersion: "6.0.1-preview",
            LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-05-07T09:08:00+00:00"),
            LastError: null,
            Status: status,
            RecommendedAction: "Stay on the current workbench.");

    private static DesktopHomeCampaignProjection CreateCampaignProjection(IReadOnlyList<string> watchouts)
        => new(
            Summary: "Campaign posture is calm.",
            NextSafeAction: "Stay on the workbench quick-start shell.",
            RestoreSummary: "No restore packet is pending.",
            DeviceRoleSummary: "This copy is a workstation.",
            SupportClosureSummary: "Support is quiet.",
            LeadWorkspaceId: null,
            ReadinessHighlights: [],
            Watchouts: watchouts);

    private static DesktopHomeSupportProjection CreateSupportProjection(bool needsAttention)
        => new(
            CaseId: null,
            Summary: "No tracked support cases are attached.",
            NextSafeAction: "Keep building.",
            PrimaryActionLabel: null,
            PrimaryActionHref: null,
            DetailHref: null,
            InstallReadinessSummary: null,
            StatusLabel: null,
            StageLabel: null,
            UpdatedLabel: null,
            FixedReleaseLabel: null,
            AffectedInstallSummary: null,
            FollowUpLaneSummary: null,
            ReleaseProgressSummary: null,
            VerificationSummary: null,
            HasTrackedCase: false,
            NeedsAttention: needsAttention,
            FixReadyOnLinkedInstall: false,
            NeedsInstallUpdate: false,
            NeedsLinkedInstall: false,
            Highlights: []);
}
