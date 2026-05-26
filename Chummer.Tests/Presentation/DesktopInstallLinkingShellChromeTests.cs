#nullable enable annotations

using System;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopInstallLinkingShellChromeTests
{
    [TestMethod]
    public void BuildShellWindowTitle_returns_claim_title_for_unlinked_install()
    {
        string title = DesktopInstallLinkingRuntime.BuildShellWindowTitle(
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.window_title", DesktopLocalizationCatalog.DefaultLanguage),
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", DesktopLocalizationCatalog.DefaultLanguage),
            CreateInstallState(status: "guest"));

        Assert.AreEqual("Link this copy", title);
    }

    [TestMethod]
    public void BuildShellWindowTitle_appends_linked_user_for_claimed_install()
    {
        string title = DesktopInstallLinkingRuntime.BuildShellWindowTitle(
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.window_title", DesktopLocalizationCatalog.DefaultLanguage),
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", DesktopLocalizationCatalog.DefaultLanguage),
            CreateInstallState(status: "claimed", userId: "user-runner-7"));

        Assert.AreEqual("Chummer Desktop · user-runner-7", title);
    }

    [TestMethod]
    public void ResolveLinkedUserLabel_falls_back_to_subject_then_installation_id()
    {
        Assert.AreEqual(
            "subject-42",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(status: "claimed", userId: null, subjectId: "subject-42")));
        Assert.AreEqual(
            "install-1",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(status: "claimed", userId: null, subjectId: null)));
    }

    [TestMethod]
    public void BuildClaimPortalRelativePathForInstall_starts_google_oauth_handoff_with_encoded_next()
    {
        string path = DesktopInstallLinkingRuntime.BuildClaimPortalRelativePathForInstall(CreateInstallState(status: "guest"));

        StringAssert.StartsWith(path, "/auth/google/start?next=");
        StringAssert.Contains(path, Uri.EscapeDataString("/account/access/install-link?"));
        StringAssert.Contains(path, Uri.EscapeDataString("installationId=install-1"));
        StringAssert.Contains(path, Uri.EscapeDataString("installLinkMode=browser_callback"));
    }

    private static DesktopInstallLinkingState CreateInstallState(
        string status,
        string? userId = "user-runner-7",
        string? subjectId = "subject-runner-7")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool claimed = string.Equals(status, "claimed", StringComparison.Ordinal);
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
            ClaimedAtUtc: claimed ? now : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: claimed ? "grant-token" : null,
            UserId: claimed ? userId : null,
            SubjectId: claimed ? subjectId : null);
    }
}
