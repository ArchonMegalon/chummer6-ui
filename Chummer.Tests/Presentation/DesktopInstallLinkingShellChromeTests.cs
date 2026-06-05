#nullable enable annotations

using System;
using System.IO;
using System.Linq;
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

        StringAssert.StartsWith(path, "/login?next=");
        StringAssert.Contains(path, Uri.EscapeDataString("/account/access/install-link?"));
        StringAssert.Contains(path, Uri.EscapeDataString("installationId=install-1"));
        StringAssert.Contains(path, Uri.EscapeDataString("installLinkMode=browser_callback"));
    }

    [TestMethod]
    public void Windows_install_link_gate_copy_stays_fail_closed_until_user_links_in_browser()
    {
        string formPath = FindPath("Chummer", "Forms", "DesktopInstallLinkingGateForm.cs");
        string formText = File.ReadAllText(formPath);

        StringAssert.Contains(formText, "I'm not linked. Please link and log in on the website.");
        StringAssert.Contains(formText, "Log in on the website");
        StringAssert.Contains(formText, "This install is not linked to a Chummer account yet.");
        Assert.IsFalse(
            formText.Contains("dashboard", StringComparison.OrdinalIgnoreCase),
            "The unlinked Windows gate must not suggest that the desktop continues into dashboard or workbench content before linking.");
        Assert.IsFalse(
            formText.Contains("premium", StringComparison.OrdinalIgnoreCase),
            "The unlinked Windows gate should stay narrowly focused on account linking.");
    }

    [TestMethod]
    public void Winforms_startup_uses_sync_install_linking_bridge_without_direct_async_blocker_calls()
    {
        string programPath = FindPath("Chummer", "Program.cs");
        string programText = File.ReadAllText(programPath);

        StringAssert.Contains(programText, "DesktopInstallLinkingRuntime.InitializeForStartup(");
        Assert.IsFalse(
            programText.Contains("DesktopInstallLinkingRuntime.InitializeForStartupAsync(", StringComparison.Ordinal),
            "Program startup should go through the explicit sync bridge instead of directly blocking on the async runtime API.");
        Assert.IsFalse(
            programText.Contains("InitializeForStartupAsync(\n", StringComparison.Ordinal)
            || programText.Contains("InitializeForStartupAsync(\r\n", StringComparison.Ordinal),
            "Program.cs should not directly call the async startup entrypoint.");
    }

    [TestMethod]
    public void Guest_install_link_window_keeps_login_path_but_allows_explicit_exit()
    {
        string source = FindPath("Chummer.Avalonia", "DesktopInstallLinkingWindow.cs");
        string text = File.ReadAllText(source);

        StringAssert.Contains(text, "desktop.install_link.button.exit_desktop");
        StringAssert.Contains(text, "_allowGuestClose = true;");
        StringAssert.Contains(text, "DesktopInstallLinkingRuntime.IsClaimed(_state) || _allowGuestClose");
        StringAssert.Contains(text, "desktop.install_link.button.login_website");
    }

    private static string FindPath(params string[] parts)
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(new[] { current }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate {Path.Combine(parts)} from the test output directory.");
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
