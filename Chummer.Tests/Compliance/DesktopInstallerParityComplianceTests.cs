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
        string installerProgramPath = Path.Combine(repoRoot, "Chummer.Desktop.Installer", "Program.cs");
        string installerProgramText = File.ReadAllText(installerProgramPath);
        string selectionHandlersPath = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.SelectionHandlers.cs");
        string selectionHandlersText = File.ReadAllText(selectionHandlersPath);

        StringAssert.Contains(installerScriptText, "\"shortcutName\": \"Chummer6 Desktop\" if secondary_head_key and primary_head_key == \"avalonia\"");
        StringAssert.Contains(installerScriptText, "\"shortcutName\": SHORTCUT_NAMES[secondary_head_key]");
        StringAssert.Contains(installerProgramText, "TryDeleteLegacyInstallDirectories(metadata);");
        StringAssert.Contains(installerProgramText, "if (metadata.InstalledHeads.Count > 1)");
        StringAssert.Contains(installerProgramText, "Yes: launch {primaryName}");
        StringAssert.Contains(installerProgramText, "No: launch {secondaryName}");
        StringAssert.Contains(installerProgramText, "foreach (InstalledHeadMetadata head in metadata.InstalledHeads)");
        StringAssert.Contains(installerProgramText, "Path.Combine(InstallRoot, $\"AvaloniaDesktop-{ridSuffix}\")");
        StringAssert.Contains(installerProgramText, "Path.Combine(InstallRoot, $\"BlazorDesktop-{ridSuffix}\")");
        StringAssert.Contains(installerProgramText, "Debug.WriteLine($\"Chummer installer could not prune legacy install directory");
        StringAssert.Contains(selectionHandlersText, "DesktopReportIssueWindow.ShowAsync(this, DesktopHeadId)");
        Assert.IsFalse(selectionHandlersText.Contains("LegacyReportBugUrl", StringComparison.Ordinal));
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
