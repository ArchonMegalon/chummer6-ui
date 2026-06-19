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
        StringAssert.Contains(text, "TryOpenClaimPortalForInstall(");
        StringAssert.Contains(text, "out string loginUrl");
        StringAssert.Contains(text, "out string? failureReason");
        StringAssert.Contains(text, "ShowManualBrowserFallbackAsync(loginUrl, failureReason)");
        StringAssert.Contains(text, "desktop.install_link.button.copy_login_url");
        StringAssert.Contains(text, "PollForClaimedInstallAsync");
        StringAssert.Contains(text, "DesktopInstallLinkingRuntime.LoadOrCreateState(_state.HeadId)");
    }

    [TestMethod]
    public void Install_link_window_contains_first_run_optional_tools_visibility_choice()
    {
        string installWindowSource = File.ReadAllText(FindPath("Chummer.Avalonia", "DesktopInstallLinkingWindow.cs"));
        string preferenceSource = File.ReadAllText(FindPath("Chummer.Avalonia", "MainWindow.PreferenceState.cs"));
        string localizationSource = File.ReadAllText(FindPath("Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));

        StringAssert.Contains(installWindowSource, "InstallLinkGuidedToolsVisibleOption");
        StringAssert.Contains(installWindowSource, "InstallLinkGuidedToolsHiddenOption");
        StringAssert.Contains(installWindowSource, "InstallLinkFeatureVisibility");
        StringAssert.Contains(installWindowSource, "CreateGuidedToolsPreferencePanel");
        StringAssert.Contains(installWindowSource, "ApplyGuidedFeaturePreference");
        StringAssert.Contains(installWindowSource, "DisableAiFeatures");
        StringAssert.Contains(installWindowSource, "DesktopPreferenceRuntime.SaveState");
        StringAssert.Contains(installWindowSource, "ApplyExternalPreferenceState(nextPreferences)");
        StringAssert.Contains(localizationSource, "desktop.install_link.preference.visible_choice");
        StringAssert.Contains(localizationSource, "desktop.install_link.preference.hidden_choice");
        StringAssert.Contains(localizationSource, "Use assisted features");
        StringAssert.Contains(localizationSource, "Keep the interface manual");
        StringAssert.Contains(localizationSource, "desktop.devices.section.interface");
        StringAssert.Contains(localizationSource, "Account and Devices");
        StringAssert.Contains(localizationSource, "desktop.devices.section.current_description");
        StringAssert.Contains(localizationSource, "desktop.devices.button.use_latest_claim");
        StringAssert.Contains(File.ReadAllText(FindPath("Chummer.Avalonia", "DesktopDevicesAccessWindow.cs")), "DevicesAccessGuidedToolsHiddenOption");
        StringAssert.Contains(localizationSource, "desktop.install_link.status.guided_tools_hidden");
        StringAssert.Contains(preferenceSource, "_preferPersistedPreferencesOnNextRefresh");
    }

    [TestMethod]
    public void Help_menu_login_video_preview_reuses_matrix_uplink_without_forcing_browser_or_exit()
    {
        string installWindowSource = File.ReadAllText(FindPath("Chummer.Avalonia", "DesktopInstallLinkingWindow.cs"));
        string selectionSource = File.ReadAllText(FindPath("Chummer.Avalonia", "MainWindow.SelectionHandlers.cs"));
        string projectorSource = File.ReadAllText(FindPath("Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs"));
        string catalogSource = File.ReadAllText(FindPath("Chummer.Presentation", "Shell", "CatalogOnlyRulesetShellCatalogResolver.cs"));
        string labelSource = File.ReadAllText(FindPath("Chummer.Presentation", "UiKit", "ShellChromeBoundary.cs"));

        StringAssert.Contains(catalogSource, "Command(\"show_login_video\", \"command.show_login_video\", \"help\", false)");
        StringAssert.Contains(projectorSource, "\"show_login_video\"");
        StringAssert.Contains(labelSource, "[\"show_login_video\"] = \"Show Login Video\"");
        StringAssert.Contains(selectionSource, "case \"show_login_video\":");
        StringAssert.Contains(selectionSource, "DesktopInstallLinkingWindow.ShowLoginVideoAsync(this, DesktopHeadId)");
        StringAssert.Contains(installWindowSource, "ShowLoginVideoAsync(Window owner, string headId)");
        StringAssert.Contains(installWindowSource, "PromptReason: \"desktop_help_login_video\"");
        StringAssert.Contains(installWindowSource, "loginVideoPreview: true");
        StringAssert.Contains(installWindowSource, "_allowGuestClose = loginVideoPreview;");
        StringAssert.Contains(installWindowSource, "The browser will not open unless you press the login button.");
        StringAssert.Contains(installWindowSource, "_exitButton.Content = \"Close\";");
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

    [TestMethod]
    public void Report_issue_window_keeps_labels_visible_and_actions_human_facing()
    {
        string reportWindowSource = File.ReadAllText(FindPath("Chummer.Avalonia", "DesktopReportIssueWindow.cs"));
        string localizationSource = File.ReadAllText(FindPath("Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));

        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.bug.title_label\"), _bugTitleBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.bug.expected_label\"), _bugExpectedBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.bug.actual_label\"), _bugActualBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.bug.repro_label\"), _bugReproStepsBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.bug.evidence_label\"), _bugEvidenceBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.feedback.summary_label\"), _feedbackSummaryBox)");
        StringAssert.Contains(reportWindowSource, "CreateField(S(\"desktop.report.feedback.detail_label\"), _feedbackDetailBox)");
        StringAssert.Contains(reportWindowSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellForegroundBrush\"");
        StringAssert.Contains(reportWindowSource, "ToolTip.SetTip(box, null);");
        StringAssert.Contains(localizationSource, "[\"desktop.report.button.open_bug\"] = \"Open Bug Report\"");
        StringAssert.Contains(localizationSource, "[\"desktop.report.button.copy_bug\"] = \"Copy Bug Report\"");
        StringAssert.Contains(localizationSource, "[\"desktop.report.button.open_feedback\"] = \"Open Feedback\"");
        StringAssert.Contains(localizationSource, "[\"desktop.report.button.copy_feedback\"] = \"Copy Feedback\"");
        Assert.IsFalse(localizationSource.Contains("Open Private Bug Draft", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Open Private Feedback Draft", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Privaten Fehlerentwurf", StringComparison.Ordinal));
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
