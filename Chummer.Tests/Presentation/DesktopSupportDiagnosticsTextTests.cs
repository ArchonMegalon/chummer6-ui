using System;
using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopSupportDiagnosticsTextTests
{
    [TestMethod]
    public void BuildSupportCenterDiagnostics_formats_environment_diff_and_support_reuse()
    {
        string text = DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics(
            CreateInstallState(),
            CreateUpdateStatus(),
            CreateSupportProjection());

        StringAssert.Contains(text, "Diagnostics environment diff: avalonia on linux/x64, channel preview, install install-123. Before 6.0.1-preview -> after 6.0.2-preview.");
        StringAssert.Contains(text, "Before: Released on install-123 at 6.0.1-preview. Environment: Install is ready for reporter verification..");
        StringAssert.Contains(text, "After: Install the reporter-ready fix and verify the tracked case. Target receipt: 6.0.2-preview. Outcome: Fix reached preview..");
        StringAssert.Contains(text, "Explain receipt: support/case-123 resolves Released against avalonia:preview -> 6.0.2-preview.");
        StringAssert.Contains(text, "Support reuse: cite install install-123, case case-123, and Install is ready for reporter verification.");
    }

    [TestMethod]
    public void BuildTrackedCaseDiagnostics_prefers_case_specific_before_after_receipts()
    {
        string text = DesktopSupportDiagnosticsText.BuildTrackedCaseDiagnostics(
            CreateInstallState(),
            CreateUpdateStatus(),
            CreateSupportProjection(),
            CreateSupportCase());

        StringAssert.Contains(text, "Diagnostics environment diff: avalonia on linux/x64, channel preview, install install-123. Before 6.0.0-preview -> after 6.0.3-preview.");
        StringAssert.Contains(text, "Before: released_to_reporter_channel on install-case-456 at 6.0.0-preview. Environment: Install is ready for reporter verification..");
        StringAssert.Contains(text, "After: Install the reporter-ready fix and verify the tracked case. Target receipt: 6.0.3-preview. Outcome: Fix reached preview..");
        StringAssert.Contains(text, "Explain receipt: support/case-456 resolves Released against avalonia:preview -> 6.0.3-preview.");
        StringAssert.Contains(text, "Support reuse: cite install install-123, case case-456, and Install is ready for reporter verification.");
    }

    private static DesktopInstallLinkingState CreateInstallState()
        => new(
            InstallationId: "install-123",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: DateTimeOffset.Parse("2026-04-15T09:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-04-15T09:10:00+00:00"),
            LaunchCount: 4,
            LastStartedAtUtc: DateTimeOffset.Parse("2026-04-15T09:10:00+00:00"),
            ClaimedAtUtc: DateTimeOffset.Parse("2026-04-15T09:01:00+00:00"),
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private");

    private static DesktopUpdateClientStatus CreateUpdateStatus()
        => new(
            HeadId: "avalonia",
            InstalledVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "/downloads/manifest.json",
            LastCheckedAtUtc: DateTimeOffset.Parse("2026-04-15T09:12:00+00:00"),
            LastManifestVersion: "6.0.2-preview",
            LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-04-15T09:08:00+00:00"),
            LastError: null,
            Status: "update_available",
            RecommendedAction: "Install the reporter-ready fix and verify the tracked case.");

    private static DesktopHomeSupportProjection CreateSupportProjection()
        => new(
            CaseId: "case-123",
            Summary: "Tracked case needs reporter verification.",
            NextSafeAction: "Install the reporter-ready fix and verify the tracked case.",
            PrimaryActionLabel: "Open update",
            PrimaryActionHref: "/downloads",
            DetailHref: "/account/support/case-123",
            InstallReadinessSummary: "Install is ready for reporter verification.",
            StatusLabel: "Released",
            StageLabel: "Released",
            UpdatedLabel: "2026-04-15 09:10 UTC",
            FixedReleaseLabel: "6.0.2-preview",
            AffectedInstallSummary: "Affected install install-123.",
            FollowUpLaneSummary: "Follow-up stays in account support.",
            ReleaseProgressSummary: "Fix reached preview.",
            VerificationSummary: "Reporter should verify locally.",
            HasTrackedCase: true,
            NeedsAttention: true,
            FixReadyOnLinkedInstall: true,
            NeedsInstallUpdate: true,
            NeedsLinkedInstall: false,
            Highlights: []);

    private static DesktopSupportCaseDetails CreateSupportCase()
        => new(
            CaseId: "case-456",
            Kind: "bug_report",
            Status: "released_to_reporter_channel",
            Title: "Support case",
            Summary: "Case-specific summary.",
            Detail: "Case-specific detail.",
            CandidateOwnerRepo: "chummer6-ui",
            DesignImpactSuspected: false,
            CreatedAtUtc: DateTimeOffset.Parse("2026-04-14T09:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-04-15T09:00:00+00:00"),
            Source: "desktop_feedback",
            InstallationId: "install-case-456",
            ApplicationVersion: "6.0.0-preview",
            ReleaseChannel: "preview",
            HeadId: "avalonia",
            Platform: "linux",
            Arch: "x64",
            FixedVersion: "6.0.3-preview",
            FixedChannel: "preview");
}
