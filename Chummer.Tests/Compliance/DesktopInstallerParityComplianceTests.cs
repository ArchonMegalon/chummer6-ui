#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DesktopInstallerParityComplianceTests
{
    [TestMethod]
    public void Combined_windows_desktop_installer_prunes_legacy_roots_and_keeps_shared_primary_shortcut_identity()
    {
        string repoRoot = FindRepoRoot();
        string installerScriptPath = Path.Combine(repoRoot, "scripts", "build-desktop-installer.sh");
        string installerScriptText = File.ReadAllText(installerScriptPath);
        string installerProjectPath = Path.Combine(repoRoot, "Chummer.Desktop.Installer", "Chummer.Desktop.Installer.csproj");
        string installerProjectText = File.ReadAllText(installerProjectPath);
        string installerProgramPath = Path.Combine(repoRoot, "Chummer.Desktop.Installer", "Program.cs");
        string installerProgramText = File.ReadAllText(installerProgramPath);
        string selectionHandlersPath = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.SelectionHandlers.cs");
        string selectionHandlersText = File.ReadAllText(selectionHandlersPath);

        StringAssert.Contains(installerScriptText, "\"shortcutName\": \"Chummer6 Desktop\" if secondary_head_key and primary_head_key == \"avalonia\"");
        StringAssert.Contains(installerScriptText, "\"shortcutName\": SHORTCUT_NAMES[secondary_head_key]");
        StringAssert.Contains(installerProgramText, "TryDeleteLegacyInstallDirectories(metadata);");
        StringAssert.Contains(installerProgramText, "if (metadata.InstalledHeads.Count > 1)");
        StringAssert.Contains(installerProgramText, "\"Open Chummer\"");
        StringAssert.Contains(installerProgramText, "\"Open Blazor Desktop\"");
        StringAssert.Contains(installerProgramText, "foreach (InstalledHeadMetadata head in metadata.InstalledHeads)");
        StringAssert.Contains(installerProgramText, "Thread.CurrentThread.GetApartmentState() == ApartmentState.STA");
        StringAssert.Contains(installerProgramText, "shortcutThread.SetApartmentState(ApartmentState.STA);");
        StringAssert.Contains(installerProgramText, "shortcutThread.Join(TimeSpan.FromSeconds(15))");
        StringAssert.Contains(installerProgramText, "CreateShortcutCore(shortcutPath, targetPath, description);");
        StringAssert.Contains(installerProgramText, "MoveOrCopyDirectory(tempExtractDir, targetDir, progress);");
        StringAssert.Contains(installerProgramText, "MoveOrCopyDirectory(payloadRoot, targetDir, progress);");
        StringAssert.Contains(installerProgramText, "Directory.Move(sourceDir, targetDir);");
        StringAssert.Contains(installerProgramText, "Installing application files");
        StringAssert.Contains(installerProgramText, "PreparePayloadArchiveStream(payload, tempExtractDir, progress);");
        StringAssert.Contains(installerProgramText, "payload.CanSeek");
        StringAssert.Contains(installerProgramText, "Reading packaged files");
        StringAssert.Contains(installerProgramText, "InstallerTraceFileName = \"chummer-desktop-installer-progress.log\"");
        StringAssert.Contains(installerProgramText, "TraceProgress(update);");
        StringAssert.Contains(installerProgramText, "FormatTraceArguments(args)");
        StringAssert.Contains(installerProgramText, "Path.Combine(InstallRoot, $\"AvaloniaDesktop-{ridSuffix}\")");
        StringAssert.Contains(installerProgramText, "Path.Combine(InstallRoot, $\"BlazorDesktop-{ridSuffix}\")");
        StringAssert.Contains(installerProgramText, "Debug.WriteLine($\"Chummer installer could not prune legacy install directory");
        StringAssert.Contains(installerProgramText, "RegisterUrlProtocol(metadata);");
        StringAssert.Contains(installerProgramText, "UnregisterUrlProtocol();");
        StringAssert.Contains(installerProgramText, "Software\\Classes\\{ChummerProtocolScheme}");
        StringAssert.Contains(installerProgramText, "\"URL: Chummer Protocol\"");
        StringAssert.Contains(installerProgramText, "commandKey.SetValue(string.Empty, $\"\\\"{launchPath}\\\" {InstallLinkCallbackSwitch} \\\"%1\\\"\")");
        StringAssert.Contains(installerScriptText, "Exec=/usr/bin/chummer6-$APP_KEY %u");
        StringAssert.Contains(installerScriptText, "MimeType=x-scheme-handler/chummer;");
        StringAssert.Contains(installerScriptText, "update-desktop-database /usr/share/applications");
        StringAssert.Contains(installerScriptText, "xdg-mime default chummer6-$APP_KEY.desktop x-scheme-handler/chummer");
        StringAssert.Contains(installerProgramText, "AutoUpdateSwitch = \"--auto-update\"");
        StringAssert.Contains(installerProgramText, "LaunchHeadSwitch = \"--launch-head\"");
        StringAssert.Contains(installerProgramText, "RelaunchArgSwitch = \"--relaunch-arg\"");
        StringAssert.Contains(installerProgramText, "if (autoUpdate)");
        StringAssert.Contains(installerProgramText, "LaunchInstalledApp(metadata, claimCode, requestedLaunchHeadId, relaunchArgs, null);");
        StringAssert.Contains(installerProjectText, "<ChummerInstallerIncludeSidecarPayload Condition=\"'$(ChummerInstallerIncludeSidecarPayload)' == ''\">false</ChummerInstallerIncludeSidecarPayload>");
        StringAssert.Contains(installerProjectText, "<ChummerInstallerPayloadUrl Condition=\"'$(ChummerInstallerPayloadUrl)' == ''\"></ChummerInstallerPayloadUrl>");
        StringAssert.Contains(installerProjectText, "<_Parameter1>ChummerInstallerPayloadUrl</_Parameter1>");
        StringAssert.Contains(installerScriptText, "-p:ChummerInstallerIncludeSidecarPayload=false");
        StringAssert.Contains(installerScriptText, "CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap");
        StringAssert.Contains(installerScriptText, "-p:EnableCompressionInSingleFile=false");
        StringAssert.Contains(installerScriptText, "-p:ChummerInstallerPayloadUrl=\"$bootstrap_payload_url\"");
        StringAssert.Contains(installerScriptText, "-p:ChummerInstallerPayloadSha256=\"$bootstrap_payload_sha256\"");
        StringAssert.Contains(installerScriptText, "-p:ChummerInstallerPayloadSizeBytes=\"$bootstrap_payload_size_bytes\"");
        StringAssert.Contains(installerScriptText, "\"contractName\": \"chummer6-ui.windows_bootstrap_payload\"");
        StringAssert.Contains(selectionHandlersText, "DesktopReportIssueWindow.ShowAsync(this, DesktopHeadId)");
        Assert.IsFalse(selectionHandlersText.Contains("LegacyReportBugUrl", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Windows_desktop_installer_progress_and_completion_use_compact_non_clipping_layout()
    {
        string repoRoot = FindRepoRoot();
        string installerProgramPath = Path.Combine(repoRoot, "Chummer.Desktop.Installer", "Program.cs");
        string installerProgramText = File.ReadAllText(installerProgramPath);

        StringAssert.Contains(installerProgramText, "InstallerDialogClientSize = new(780, 380);");
        StringAssert.Contains(installerProgramText, "InstallerActionButtonSize = new(184, 42);");
        StringAssert.Contains(installerProgramText, "InstallerSurfacePadding = new(30, 24, 30, 24);");
        StringAssert.Contains(installerProgramText, "ClientSize = InstallerDialogClientSize;");
        StringAssert.Contains(installerProgramText, "MinimumSize = InstallerDialogClientSize;");
        StringAssert.Contains(installerProgramText, "Font = new Font(\"Segoe UI Semibold\", 10F");
        StringAssert.Contains(installerProgramText, "Text = $\"{displayName} Installer - Installing\";");
        StringAssert.Contains(installerProgramText, "Name = \"ChummerInstallerProgressDialog\";");
        StringAssert.Contains(installerProgramText, "AccessibleName = $\"{displayName} installer progress\";");
        StringAssert.Contains(installerProgramText, "TimeSpan minimumProgressDisplay = TimeSpan.FromMilliseconds(1200);");
        StringAssert.Contains(installerProgramText, "ClientSize = InstallerDialogClientSize,");
        StringAssert.Contains(installerProgramText, "MinimumSize = InstallerDialogClientSize,");
        StringAssert.Contains(installerProgramText, "Name = \"ChummerInstallerCompletionDialog\",");
        StringAssert.Contains(installerProgramText, "AccessibleName = $\"{displayName} install complete\",");
        StringAssert.Contains(installerProgramText, "Text = $\"{displayName} Installer - Install Complete\",");
        StringAssert.Contains(installerProgramText, "void CompletePrompt(DialogResult result)");
        StringAssert.Contains(installerProgramText, "CompletePrompt(DialogResult.Yes);");
        StringAssert.Contains(installerProgramText, "CompletePrompt(DialogResult.No);");
        StringAssert.Contains(installerProgramText, "CompletePrompt(DialogResult.Cancel);");
        StringAssert.Contains(installerProgramText, "prompt.FormClosing += (_, _) =>");
        StringAssert.Contains(installerProgramText, "TraceInstaller(\"showing completion prompt title=\" + prompt.Text);");
        StringAssert.Contains(installerProgramText, "prompt.Shown += (_, _) =>");
        StringAssert.Contains(installerProgramText, "prompt.Activate();");
        StringAssert.Contains(installerProgramText, "prompt.BringToFront();");
        StringAssert.Contains(installerProgramText, "DialogResult result = prompt.ShowDialog();");
        StringAssert.Contains(installerProgramText, "TraceInstaller(\"completion prompt result=\" + result);");
        StringAssert.Contains(installerProgramText, "ShowInTaskbar = true,");
        StringAssert.Contains(installerProgramText, "RowCount = 5");
        StringAssert.Contains(installerProgramText, "Height = 36,");
        StringAssert.Contains(installerProgramText, "Height = 44,");
        StringAssert.Contains(installerProgramText, "Use only when support asks.");
        StringAssert.Contains(installerProgramText, "MinimumSize = InstallerActionButtonSize,");
        StringAssert.Contains(installerProgramText, "Padding = new Padding(14, 0, 14, 2),");
        StringAssert.Contains(installerProgramText, "UseMnemonic = false");
        StringAssert.Contains(installerProgramText, "WrapContents = false");
        StringAssert.Contains(installerProgramText, "Shortcuts and first launch are prepared automatically.");
        StringAssert.Contains(installerProgramText, "This may take a few minutes on slower systems.");
        StringAssert.Contains(installerProgramText, "AutoEllipsis = true");
        StringAssert.Contains(installerProgramText, "BuildInstalledPathText(targetDir)");
        StringAssert.Contains(installerProgramText, "return $\"Installed to ...{Path.DirectorySeparatorChar}{compactTail}\";");
        StringAssert.Contains(installerProgramText, ".TakeLast(3)");
        StringAssert.Contains(installerProgramText, "_progressTrack.ClientSize.Width");
        StringAssert.Contains(installerProgramText, "Preparing copy claim");
        StringAssert.Contains(installerProgramText, "BuildProgressDisplayStage(update.Stage)");
        StringAssert.Contains(installerProgramText, "CloseSafely()");
        StringAssert.Contains(installerProgramText, "CanUpdateControls()");
        StringAssert.Contains(installerProgramText, "_isClosing");
        StringAssert.Contains(installerProgramText, "ResolvePayloadDownloadRequest(args)");
        StringAssert.Contains(installerProgramText, "PayloadUrlSwitch = \"--payload-url\"");
        StringAssert.Contains(installerProgramText, "PayloadSha256Switch = \"--payload-sha256\"");
        StringAssert.Contains(installerProgramText, "PayloadSizeBytesSwitch = \"--payload-size-bytes\"");
        StringAssert.Contains(installerProgramText, "Downloading application files");
        StringAssert.Contains(installerProgramText, "ValidateDownloadedPayload(payloadPath, request);");
        StringAssert.Contains(installerProgramText, "Downloaded payload checksum mismatch");
        StringAssert.Contains(installerProgramText, "ShouldShowInlineCount(total.Value)");
        StringAssert.Contains(installerProgramText, "return total < ProgressUnitScale || total % ProgressUnitScale != 0;");
        StringAssert.Contains(installerProgramText, "return \"Extracting application files\";");
        StringAssert.Contains(installerProgramText, "return \"Copying application files\";");
        StringAssert.Contains(installerProgramText, "return \"Reading packaged files\";");
        StringAssert.Contains(installerProgramText, "!stage.Equals(\"Extracting application files\"");
        StringAssert.Contains(installerProgramText, "!stage.Equals(\"Copying application files\"");
        StringAssert.Contains(installerProgramText, "body.Controls.Add(hintLabel);");
        StringAssert.Contains(installerProgramText, "body.Controls.Add(heroRow);");
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(860, 420)", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MinimumSize = new Size(860, 420)", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(640, 300),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(640, 280);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(600, 260),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(600, 250);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(520, 206);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(900, 460),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MinimumSize = new Size(900, 460),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(840, 430),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MinimumSize = new Size(840, 430),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(840, 400)", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MinimumSize = new Size(840, 400)", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MaximumSize = new Size(220, 42),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Font = new Font(\"Segoe UI Semibold\", 24F", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Font = new Font(\"Segoe UI Semibold\", 13.5F", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Font = new Font(\"Segoe UI Semibold\", 11.5F", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("MinimumSize = new Size(176, 42),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains(
            "Height = 52,\n            TextAlign = ContentAlignment.TopLeft",
            StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Install folder:\\n", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Preparing first-run sign-in", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Installed to {targetDir}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Launch {primaryName}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Launch {secondaryName}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Unpacking the desktop, wiring shortcuts, and preparing first launch.", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ProgressTrackWidth", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("This usually takes less than a minute.", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("prompt.Show();", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("while (!prompt.IsDisposed && prompt.Visible)", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("splash.Close();", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains(
            "if (total.HasValue && completed.HasValue)\n            {",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Startup_update_handoff_is_visible_and_keeps_macos_manual_install_recoverable()
    {
        string repoRoot = FindRepoRoot();
        string avaloniaProgramPath = Path.Combine(repoRoot, "Chummer.Avalonia", "Program.cs");
        string avaloniaProgramText = File.ReadAllText(avaloniaProgramPath);
        string appPath = Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs");
        string appText = File.ReadAllText(appPath);
        string startupWindowPath = Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopStartupUpdateWindow.cs");
        string startupWindowText = File.ReadAllText(startupWindowPath);
        string runtimePath = Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopUpdateRuntime.cs");
        string runtimeText = File.ReadAllText(runtimePath);
        string publishBundlePath = Path.Combine(repoRoot, "scripts", "publish-download-bundle.sh");
        string publishBundleText = File.ReadAllText(publishBundlePath);
        string manifestGeneratorPath = Path.Combine(repoRoot, "scripts", "generate-releases-manifest.sh");
        string manifestGeneratorText = File.ReadAllText(manifestGeneratorPath);

        Assert.IsFalse(
            avaloniaProgramText.Contains("DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(\n            \"avalonia\"", StringComparison.Ordinal),
            "Avalonia startup must not run the updater before any visible window can appear.");
        StringAssert.Contains(avaloniaProgramText, "App.StartupArguments = args;");
        StringAssert.Contains(appText, "DesktopStartupUpdateWindow.TryRunStartupUpdateAsync(");
        StringAssert.Contains(startupWindowText, "Installing update and restarting Chummer");
        StringAssert.Contains(startupWindowText, "Keep this window open. Starting another copy can interrupt the update.");
        StringAssert.Contains(startupWindowText, "DesktopStartupUpdateViewState BuildViewState");
        StringAssert.Contains(startupWindowText, "GetCompletionDisplayDelayMs");
        StringAssert.Contains(startupWindowText, "ShowWaitText: showWaitText");
        StringAssert.Contains(startupWindowText, "return RelaunchVisibilityDelayMs;");
        StringAssert.Contains(startupWindowText, "return FailureVisibilityDelayMs;");
        StringAssert.Contains(startupWindowText, "CanResize = true");
        StringAssert.Contains(startupWindowText, "new ScrollViewer");
        StringAssert.Contains(startupWindowText, "VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto");
        StringAssert.Contains(startupWindowText, "Chummer will open automatically if this copy is already current.");
        Assert.IsFalse(startupWindowText.Contains("Height = 220", StringComparison.Ordinal));
        Assert.IsFalse(startupWindowText.Contains("CanResize = false", StringComparison.Ordinal));
        StringAssert.Contains(startupWindowText, "A macOS update is ready. Open Update Status to install it manually; this copy will stay usable.");
        StringAssert.Contains(runtimeText, "public sealed record DesktopUpdateProgressUpdate");
        StringAssert.Contains(runtimeText, "IProgress<DesktopUpdateProgressUpdate>? progress");
        StringAssert.Contains(runtimeText, "OperatingSystem.IsMacOS()");
        StringAssert.Contains(runtimeText, "macos_manual_install_required");
        StringAssert.Contains(runtimeText, "PendingInstallerPath");
        StringAssert.Contains(runtimeText, "TryOpenPendingInstaller");
        StringAssert.Contains(runtimeText, "TryBuildPendingInstallerManualCommand");
        StringAssert.Contains(runtimeText, "sudo dpkg -i");
        StringAssert.Contains(publishBundleText, "payloadFileName");
        StringAssert.Contains(publishBundleText, "payloadDownloadUrl");
        StringAssert.Contains(manifestGeneratorText, "\"installerMode\": \"bootstrap\"");
        StringAssert.Contains(manifestGeneratorText, "\"payloadFileName\"");
        StringAssert.Contains(manifestGeneratorText, "\"payloadDownloadUrl\"");
    }

    [TestMethod]
    public void Windows_installer_publish_lanes_gate_bootstrap_payload_before_promotion()
    {
        string repoRoot = FindRepoRoot();
        string gatePath = Path.Combine(repoRoot, "scripts", "verify-windows-installer-payloads.py");
        string gateText = File.ReadAllText(gatePath);
        string installerScriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "build-desktop-installer.sh"));
        string publishBundleText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "publish-download-bundle.sh"));
        string publishHttpText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "publish-download-bundle-http.sh"));
        string publishS3Text = File.ReadAllText(Path.Combine(repoRoot, "scripts", "publish-download-bundle-s3.sh"));

        StringAssert.Contains(gateText, "APPENDED_PAYLOAD_MAGIC = b\"CHUMMER6PAYLOAD1\"");
        StringAssert.Contains(gateText, "no appended payload and no bootstrap sidecar");
        StringAssert.Contains(gateText, "--require-embedded-bootstrap-metadata");
        StringAssert.Contains(gateText, "--require-manifest-row");
        StringAssert.Contains(gateText, "Windows installer is missing from the supplied release manifest");
        StringAssert.Contains(gateText, "bootstrap installer does not contain embedded");
        StringAssert.Contains(gateText, "payload zip is missing launch executable");
        StringAssert.Contains(installerScriptText, "verify_windows_installer_payload_gate \"$DIST_DIR/$installer_name\"");
        StringAssert.Contains(installerScriptText, "--heads-json-base64 \"$heads_json_base64\"");
        StringAssert.Contains(installerScriptText, "--expected-entry \"$primary_relative_root/$LAUNCH_TARGET\"");
        StringAssert.Contains(installerScriptText, "--require-embedded-bootstrap-metadata");
        StringAssert.Contains(publishBundleText, "verify_windows_installer_payload_gate");
        StringAssert.Contains(publishBundleText, "--require-embedded-bootstrap-metadata");
        StringAssert.Contains(publishBundleText, "--require-manifest-row");
        StringAssert.Contains(publishBundleText, "chummer-*-win-*-payload.zip)");
        StringAssert.Contains(publishBundleText, "file_path.name.endswith(\"-payload.zip\")");
        StringAssert.Contains(publishHttpText, "verify-windows-installer-payloads.py");
        StringAssert.Contains(publishHttpText, "--manifest \"$CANONICAL_MANIFEST_PATH\"");
        StringAssert.Contains(publishHttpText, "--require-embedded-bootstrap-metadata");
        StringAssert.Contains(publishHttpText, "--require-manifest-row");
        StringAssert.Contains(publishS3Text, "verify-windows-installer-payloads.py");
        StringAssert.Contains(publishS3Text, "--manifest \"$MANIFEST_SOURCE\"");
        StringAssert.Contains(publishS3Text, "--require-embedded-bootstrap-metadata");
        StringAssert.Contains(publishS3Text, "--require-manifest-row");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_windows_installer_payload_gate_regressions()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));

        StringAssert.Contains(verifyScriptText, "checking windows installer payload gate syntax and regression tests");
        StringAssert.Contains(verifyScriptText, "bash -n scripts/publish-download-bundle.sh");
        StringAssert.Contains(verifyScriptText, "bash -n scripts/publish-download-bundle-http.sh");
        StringAssert.Contains(verifyScriptText, "bash -n scripts/publish-download-bundle-s3.sh");
        StringAssert.Contains(verifyScriptText, "python3 -m pytest -q");
        StringAssert.Contains(verifyScriptText, "tests/test_windows_installer_payload_gate.py");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "scripts", "build-desktop-installer.sh")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the chummer-presentation repository root from the test output directory.");
    }
}
