#nullable enable annotations

using System;
using System.Collections.Generic;
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
    public void ShouldShow_returns_false_for_clean_launch_without_restore_update_or_support_pressure()
    {
        bool shouldShow = InvokeShouldShow(
            installContext: null,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsFalse(shouldShow, "A clean launch must land on the workbench instead of a dashboard-style home overlay.");
    }

    [TestMethod]
    public void ShouldShow_returns_true_when_install_linking_prompt_is_pending()
    {
        DesktopInstallLinkingStartupContext installContext = new(
            State: CreateInstallState(status: "guest"),
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "first_run");

        bool shouldShow = InvokeShouldShow(
            installContext,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Install claim and restore guidance must show in-app when the linking prompt is pending.");
    }

    [TestMethod]
    public void ShouldShow_returns_true_when_update_posture_needs_attention()
    {
        bool shouldShow = InvokeShouldShow(
            installContext: null,
            updateStatus: CreateUpdateStatus(status: "update-available"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Update and recovery pressure must keep the desktop home surface visible.");
    }

    [TestMethod]
    public void ShouldShow_returns_true_when_support_needs_attention_even_without_local_workspaces()
    {
        bool shouldShow = InvokeShouldShow(
            installContext: null,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: null),
            supportProjection: CreateSupportProjection(needsAttention: true));

        Assert.IsTrue(shouldShow, "Support recovery must surface the home overlay when the install needs attention.");
    }

    [TestMethod]
    public void ShouldShow_returns_true_when_account_restore_can_continue_without_local_workspaces()
    {
        bool shouldShow = InvokeShouldShow(
            installContext: null,
            updateStatus: CreateUpdateStatus(status: "current"),
            workspaces: [],
            campaignProjection: CreateCampaignProjection(leadWorkspaceId: "workspace-restore"),
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsTrue(shouldShow, "Account-backed restore continuation must surface before an empty workbench when no local workspace is open yet.");
    }

    [TestMethod]
    public void ShouldShow_returns_false_when_local_workspace_already_exists_even_if_restore_lane_is_available()
    {
        bool shouldShow = InvokeShouldShow(
            installContext: null,
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
            supportProjection: CreateSupportProjection(needsAttention: false));

        Assert.IsFalse(shouldShow, "A live local workspace should keep startup on the workbench instead of forcing the restore home overlay.");
    }

    private static bool InvokeShouldShow(
        DesktopInstallLinkingStartupContext? installContext,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<WorkspaceListItem> workspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeSupportProjection supportProjection)
    {
        MethodInfo shouldShow = typeof(App).Assembly
            .GetType("Chummer.Avalonia.DesktopHomeWindow", throwOnError: true)!
            .GetMethod("ShouldShow", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("DesktopHomeWindow.ShouldShow was not found.");

        object? result = shouldShow.Invoke(
            obj: null,
            parameters:
            [
                installContext,
                updateStatus,
                workspaces,
                campaignProjection,
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
            ClaimedAtUtc: null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private");
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
}
