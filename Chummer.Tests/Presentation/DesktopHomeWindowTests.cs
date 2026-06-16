#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Chummer.Avalonia;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopHomeWindowTests
{
    [TestMethod]
    public void ShouldShowOnStartup_returns_false_for_clean_launch_without_restore_update_or_support_pressure()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsFalse(shouldShow, "A clean launch must land on the workbench instead of a dashboard-style home overlay.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_install_linking_prompt_is_pending()
    {
        DesktopInstallLinkingStartupContext installContext = new(
            State: CreateInstallState(status: "guest"),
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "first_run");

        bool shouldShow = InvokeShouldShowOnStartup(
            installContext,
            installContext.State,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Install claim and restore guidance must show in-app when the linking prompt is pending.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_guest_install_has_no_local_workspace()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "guest"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Unlinked installs must show only the linking gate instead of landing on the workbench.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_install_claim_result_needs_native_follow_through()
    {
        DesktopInstallLinkingStartupContext installContext = new(
            State: CreateInstallState(status: "claimed"),
            ClaimResult: new DesktopInstallClaimResult(
                Succeeded: true,
                AlreadyClaimed: false,
                Message: "This copy is now linked.",
                State: CreateInstallState(status: "claimed")),
            StartupClaimCode: null,
            ShouldPrompt: false,
            PromptReason: "claim_complete");

        bool shouldShow = InvokeShouldShowOnStartup(
            installContext,
            installContext.State,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Fresh claim results should keep the desktop in the native follow-through lane instead of dropping straight into the workbench.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_update_posture_needs_attention()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "update-available"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Update and recovery pressure must keep the desktop home surface visible.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_support_needs_attention_even_without_local_workspaces()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: true));

        Assert.IsTrue(shouldShow, "Support recovery must surface the home overlay when the install needs attention.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_claimed_install_has_restore_continuity_but_no_local_workspace()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: "workspace-restore"),
            campaignServerPlane: CreateCampaignServerPlane(DateTimeOffset.UtcNow),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "A claimed install with restore continuity but no local workspace must surface the native restore flow.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_local_workspace_is_newer_than_server_continuity()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "claimed"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces:
            [
                new WorkspaceListItem(
                    new CharacterWorkspaceId("workspace-local"),
                    new CharacterFileSummary(
                        Name: "Local Runner",
                        Alias: "runner-local",
                        Metatype: "Human",
                        BuildMethod: "Priority",
                        CreatedVersion: "SR5",
                        AppVersion: "1.0.0",
                        Karma: 0,
                        Nuyen: 0,
                        Created: true),
                    DateTimeOffset.UtcNow,
                    "SR5")
            ],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: "workspace-restore"),
            campaignServerPlane: CreateCampaignServerPlane(DateTimeOffset.UtcNow.AddMinutes(-5)),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Restore review must interrupt startup when local work is newer than the server continuity packet.");
    }

    [TestMethod]
    public void ShouldShowOnStartup_returns_true_when_guest_install_already_has_local_workspace()
    {
        bool shouldShow = InvokeShouldShowOnStartup(
            installContext: null,
            installState: CreateInstallState(status: "guest"),
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces:
            [
                new WorkspaceListItem(
                    new CharacterWorkspaceId("workspace-local"),
                    new CharacterFileSummary(
                        Name: "Guest Runner",
                        Alias: "runner-guest",
                        Metatype: "Human",
                        BuildMethod: "Priority",
                        CreatedVersion: "SR5",
                        AppVersion: "1.0.0",
                        Karma: 0,
                        Nuyen: 0,
                        Created: true),
                    DateTimeOffset.UtcNow,
                    "SR5")
            ],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            campaignServerPlane: null,
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Unlinked installs must not expose local workbench content even when local workspaces already exist.");
    }

    [TestMethod]
    public void DesktopHome_source_wires_horizon_workbench_and_keeps_section_copy_visible()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"));

        StringAssert.Contains(source, "desktop.home.section.horizons");
        StringAssert.Contains(source, "desktop.home.horizons.summary");
        StringAssert.Contains(source, "CreateHorizonsWorkbenchBody()");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchCatalog.ListEntries()");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.OpenKarmaForgeAsync(this, _installState.HeadId)");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.OpenAsync(this, _installState.HeadId, entry)");
        StringAssert.Contains(source, "CreateHorizonQuickLaunchRow(");
        StringAssert.Contains(source, "Children =");
        StringAssert.Contains(source, "body");
        StringAssert.Contains(source, "content.Children.Add(actionContent);");
    }

    private static bool InvokeShouldShowOnStartup(
        DesktopInstallLinkingStartupContext? installContext,
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomeSupportProjection supportProjection)
    {
        MethodInfo shouldShow = typeof(App).Assembly
            .GetType("Chummer.Avalonia.DesktopHomeWindow", throwOnError: true)!
            .GetMethod("ShouldShowOnStartup", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("DesktopHomeWindow.ShouldShowOnStartup was not found.");

        object? result = shouldShow.Invoke(
            obj: null,
            parameters:
            [
                installContext,
                installState,
                updateStatus,
                workspaces,
                campaignProjection,
                campaignServerPlane,
                supportProjection
            ]);

        return result is true;
    }

    private static DesktopInstallLinkingState CreateInstallState(string status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopInstallLinkingState(
            InstallationId: "install-1",
            HeadId: "avalonia",
            ApplicationVersion: "1.0.0",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            Status: status,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 1,
            LastStartedAtUtc: now,
            ClaimedAtUtc: string.Equals(status, "claimed", StringComparison.Ordinal) ? now : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: string.Equals(status, "claimed", StringComparison.Ordinal) ? "grant-token" : null);
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus(string status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopUpdateClientStatus(
            HeadId: "avalonia",
            InstalledVersion: "1.0.0",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "/tmp/manifest.json",
            LastCheckedAtUtc: now,
            LastManifestVersion: "1.0.0",
            LastManifestPublishedAtUtc: now,
            LastError: null,
            Status: status,
            RecommendedAction: "Continue into the workbench when the desktop route is healthy.");
    }

    private static DesktopHomeSupportProjection CreateSupportProjection(bool needsAttention)
        => new(
            CaseId: null,
            Summary: needsAttention ? "Tracked case needs action." : "Support is quiet.",
            NextSafeAction: needsAttention ? "Review the tracked case before continuing." : "Continue into the workbench.",
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
            HasTrackedCase: needsAttention,
            NeedsAttention: needsAttention,
            FixReadyOnLinkedInstall: false,
            NeedsInstallUpdate: false,
            NeedsLinkedInstall: false,
            Highlights: []);

    private static DesktopHomeCampaignProjection CreateCampaignProjection(string? leadWorkspaceId)
        => new(
            Summary: "Campaign posture summary.",
            NextSafeAction: "Open the grounded campaign route.",
            RestoreSummary: "Restore packet summary.",
            DeviceRoleSummary: "Claimed device posture.",
            SupportClosureSummary: "Support closure summary.",
            LeadWorkspaceId: leadWorkspaceId,
            ReadinessHighlights: [],
            Watchouts: []);

    private static DesktopHomeCampaignServerPlane CreateCampaignServerPlane(DateTimeOffset generatedAtUtc)
        => new(
            WorkspaceId: "workspace-restore",
            SessionReadinessSummary: "Session readiness is grounded.",
            RestoreSummary: "Restore packet summary.",
            PublicationSummary: "Publication posture is aligned.",
            RosterSummary: "Roster summary is ready.",
            RunboardSummary: "Runboard summary is current.",
            TravelModeSummary: "Travel mode summary is ready.",
            TravelPrefetchInventorySummary: "Travel inventory is ready.",
            CampaignMemorySummary: "Campaign memory is aligned.",
            CampaignMemoryReturnSummary: "Campaign memory return is grounded.",
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
            GeneratedAtUtc: generatedAtUtc);
}
