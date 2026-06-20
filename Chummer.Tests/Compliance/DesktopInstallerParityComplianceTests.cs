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
        StringAssert.Contains(installerScriptText, "-p:ChummerInstallerIncludeSidecarPayload=false");
        StringAssert.Contains(selectionHandlersText, "DesktopReportIssueWindow.ShowAsync(this, DesktopHeadId)");
        Assert.IsFalse(selectionHandlersText.Contains("LegacyReportBugUrl", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Windows_desktop_installer_splash_uses_compact_non_clipping_layout()
    {
        string repoRoot = FindRepoRoot();
        string installerProgramPath = Path.Combine(repoRoot, "Chummer.Desktop.Installer", "Program.cs");
        string installerProgramText = File.ReadAllText(installerProgramPath);

        StringAssert.Contains(installerProgramText, "ClientSize = new Size(760, 340);");
        StringAssert.Contains(installerProgramText, "MinimumSize = new Size(760, 340);");
        StringAssert.Contains(installerProgramText, "Font = new Font(\"Segoe UI Semibold\", 12F");
        StringAssert.Contains(installerProgramText, "ClientSize = new Size(760, 360),");
        StringAssert.Contains(installerProgramText, "MinimumSize = new Size(760, 360),");
        StringAssert.Contains(installerProgramText, "Height = 72,");
        StringAssert.Contains(installerProgramText, "Use only when support asks.");
        StringAssert.Contains(installerProgramText, "MaximumSize = new Size(220, 42),");
        StringAssert.Contains(installerProgramText, "UseMnemonic = false");
        StringAssert.Contains(installerProgramText, "WrapContents = false");
        StringAssert.Contains(installerProgramText, "Shortcuts and first launch are prepared automatically.");
        StringAssert.Contains(installerProgramText, "AutoEllipsis = true");
        StringAssert.Contains(installerProgramText, "BuildInstalledPathText(targetDir)");
        StringAssert.Contains(installerProgramText, "return $\"Installed to ...{Path.DirectorySeparatorChar}{compactTail}\";");
        StringAssert.Contains(installerProgramText, ".TakeLast(3)");
        StringAssert.Contains(installerProgramText, "_progressTrack.ClientSize.Width");
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(640, 300),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(640, 280);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(600, 260),", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(600, 250);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ClientSize = new Size(520, 206);", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Font = new Font(\"Segoe UI Semibold\", 24F", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Font = new Font(\"Segoe UI Semibold\", 13.5F", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains(
            "Height = 52,\n            TextAlign = ContentAlignment.TopLeft",
            StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Install folder:\\n", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Installed to {targetDir}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Launch {primaryName}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("$\"Launch {secondaryName}\"", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("Unpacking the desktop, wiring shortcuts, and preparing first launch.", StringComparison.Ordinal));
        Assert.IsFalse(installerProgramText.Contains("ProgressTrackWidth", StringComparison.Ordinal));
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
