#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using Chummer.Avalonia;
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
    public void BuildShellWindowTitle_appends_human_facing_linked_identity_for_claimed_install()
    {
        string title = DesktopInstallLinkingRuntime.BuildShellWindowTitle(
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.window_title", DesktopLocalizationCatalog.DefaultLanguage),
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", DesktopLocalizationCatalog.DefaultLanguage),
            CreateInstallState(status: "claimed", userId: "runner@chummer.run"));

        Assert.AreEqual("Chummer Desktop · runner@chummer.run", title);
    }

    [TestMethod]
    public void BuildShellWindowTitle_prefers_linked_email_over_opaque_ids_when_available()
    {
        string title = DesktopInstallLinkingRuntime.BuildShellWindowTitle(
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.window_title", DesktopLocalizationCatalog.DefaultLanguage),
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", DesktopLocalizationCatalog.DefaultLanguage),
            CreateInstallState(
                status: "claimed",
                userId: "1234543",
                subjectId: "subject-42",
                linkedEmail: "runner@example.test"));

        Assert.AreEqual("Chummer Desktop · runner@example.test", title);
    }

    [TestMethod]
    public void ResolveLinkedUserLabel_suppresses_opaque_ids_and_prefers_human_labels()
    {
        Assert.AreEqual(
            "subject@example.test",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(status: "claimed", userId: null, subjectId: "subject@example.test")));
        Assert.AreEqual(
            "linked account",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(status: "claimed", userId: "1234543", subjectId: null)));
        Assert.AreEqual(
            "linked account",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(status: "claimed", userId: null, subjectId: "subject-42")));
        Assert.AreEqual(
            "runner@example.test",
            DesktopInstallLinkingRuntime.ResolveLinkedUserLabel(CreateInstallState(
                status: "claimed",
                userId: "1234543",
                subjectId: "subject-42",
                linkedEmail: "runner@example.test")));
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
        StringAssert.Contains(text, "desktopLifetime.Shutdown();");
        StringAssert.Contains(text, "desktop.install_link.button.login_website");
        Assert.IsFalse(
            text.Contains("e.Cancel = true;", StringComparison.Ordinal),
            "Closing the unlinked install-link window should exit the desktop instead of trapping the user in the dialog.");
    }

    [TestMethod]
    public void Avalonia_startup_supports_headless_install_linking_before_graphics_init()
    {
        string source = FindPath("Chummer.Avalonia", "Program.cs");
        string text = File.ReadAllText(source);

        StringAssert.Contains(text, "DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(");
        StringAssert.Contains(text, "Console.Out");
        StringAssert.Contains(text, "Console.Error");
        Assert.IsTrue(
            text.IndexOf("TryHandleHeadlessInstallLinkModeAsync(", StringComparison.Ordinal)
            < text.IndexOf("BuildAvaloniaApp()", StringComparison.Ordinal),
            "Headless install-link mode must run before Avalonia platform detection so WSL/no-GUI linking does not require GL startup.");
    }

    [TestMethod]
    public void Install_link_window_uses_flagship_matrix_uplink_shell_with_local_callback_polling()
    {
        string source = FindPath("Chummer.Avalonia", "DesktopInstallLinkingWindow.cs");
        string text = File.ReadAllText(source);

        StringAssert.Contains(text, "InstallLinkMatrixUplinkHero");
        StringAssert.Contains(text, "InstallLinkMatrixUplinkRender");
        StringAssert.Contains(text, "Assets/install-link/matrix-uplink-login.png");
        StringAssert.Contains(text, "InstallLinkMatrixIdentityVault");
        StringAssert.Contains(text, "HOST VAULT DOSSIER");
        StringAssert.Contains(text, "NOT IMPORTED");
        StringAssert.Contains(text, "EXTRACTED");
        StringAssert.Contains(text, "InstallLinkMatrixJackOutMonitor");
        StringAssert.Contains(text, "SAFEHOUSE MONITOR");
        StringAssert.Contains(text, "Live client overlay");
        StringAssert.Contains(text, "Only the verified email claim leaves the host.");
        StringAssert.Contains(text, "CreateMatrixSignalRail");
        StringAssert.Contains(text, "WrapPanel");
        StringAssert.Contains(text, "BeginAutomaticHandoffAsync();");
        StringAssert.Contains(text, "TryOpenClaimPortalForInstall(_state)");
        StringAssert.Contains(text, "PollForClaimedInstallAsync");
        StringAssert.Contains(text, "DesktopInstallLinkingRuntime.LoadOrCreateState(_state.HeadId)");
    }

    [TestMethod]
    public void Install_link_matrix_uplink_render_is_packaged_as_avalonia_resource()
    {
        string assetPath = FindPath("Chummer.Avalonia", "Assets", "install-link", "matrix-uplink-login.png");
        string projectPath = FindPath("Chummer.Avalonia", "Chummer.Avalonia.csproj");
        string projectText = File.ReadAllText(projectPath);

        Assert.IsTrue(new FileInfo(assetPath).Length > 100_000, "The install-link render should be a real flagship image asset, not a tiny placeholder.");
        StringAssert.Contains(projectText, "Assets\\install-link\\*.png");
    }

    [TestMethod]
    public void Matrix_uplink_overlay_extracts_only_verified_email_for_claimed_install()
    {
        DesktopInstallLinkingState state = CreateInstallState(
            status: "claimed",
            userId: "opaque-user-42",
            subjectId: "subject-42",
            linkedEmail: "tibor@example.test");

        Assert.AreEqual("t****@e****.test -> tibor@example.test", DesktopInstallLinkingWindow.BuildEmailClaimDisplay(state));
        Assert.AreEqual("tibor@example.test", DesktopInstallLinkingWindow.BuildMonitorEmailDisplay(state));
        Assert.AreEqual("CHUMMER UPLINK COMPLETE", DesktopInstallLinkingWindow.BuildMonitorStateDisplay(state));
        Assert.AreEqual("LOCAL INSTALL LINKED", DesktopInstallLinkingWindow.BuildMonitorLinkDisplay(state));
        Assert.AreEqual("tibor@example.test", DesktopInstallLinkingWindow.ResolveEmailForOverlay(state));
    }

    [TestMethod]
    public void Matrix_uplink_overlay_keeps_unclaimed_profile_data_sealed()
    {
        DesktopInstallLinkingState state = CreateInstallState(status: "guest");

        Assert.AreEqual("email claim sealed", DesktopInstallLinkingWindow.BuildEmailClaimDisplay(state));
        Assert.AreEqual("email pending", DesktopInstallLinkingWindow.BuildMonitorEmailDisplay(state));
        Assert.AreEqual("UPLINK WAITING", DesktopInstallLinkingWindow.BuildMonitorStateDisplay(state));
        Assert.AreEqual("LOCAL INSTALL PENDING", DesktopInstallLinkingWindow.BuildMonitorLinkDisplay(state));
        Assert.IsNull(DesktopInstallLinkingWindow.ResolveEmailForOverlay(state));
    }

    [TestMethod]
    public void Matrix_uplink_overlay_uses_failed_jackout_state_when_claim_fails()
    {
        DesktopInstallLinkingState state = CreateInstallState(status: "guest", lastClaimError: "expired claim");

        Assert.AreEqual("UPLINK LOST", DesktopInstallLinkingWindow.BuildMonitorStateDisplay(state));
    }

    [TestMethod]
    public void Matrix_uplink_overlay_compacts_long_email_for_monitor_fit()
    {
        string compact = DesktopInstallLinkingWindow.CompactOverlayValue(
            "very.long.runner.identity.with.scope@example.very-long-domain.test",
            34);

        Assert.IsTrue(compact.Length <= 34);
        StringAssert.Contains(compact, "...");
        StringAssert.EndsWith(compact, "domain.test");
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
        string? subjectId = "subject-runner-7",
        string? linkedEmail = null,
        string? lastClaimError = null)
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
            LastClaimError: lastClaimError,
            GrantToken: claimed ? "grant-token" : null,
            UserId: claimed ? userId : null,
            SubjectId: claimed ? subjectId : null,
            LinkedEmail: claimed ? linkedEmail : null);
    }
}
