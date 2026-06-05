#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DesktopVisualFamiliarityGateGuardTests
{
    private static readonly string[] ExpectedFlagshipScreenshotSet =
    [
        "01-initial-shell-light.png",
        "02-menu-open-light.png",
        "03-settings-open-light.png",
        "04-loaded-runner-light.png",
        "05-dense-section-light.png",
        "06-dense-section-dark.png",
        "07-loaded-runner-tabs-light.png",
        "08-cyberware-dialog-light.png",
        "09-vehicles-section-light.png",
        "10-contacts-section-light.png",
        "11-diary-dialog-light.png",
        "12-magic-dialog-light.png",
        "13-matrix-dialog-light.png",
        "14-advancement-dialog-light.png",
        "15-creation-section-light.png",
        "16-master-index-dialog-light.png",
        "17-character-roster-dialog-light.png",
    ];

    private static readonly string[] ExpectedVisualFamiliarityScreenshotSet =
    [
        ..ExpectedFlagshipScreenshotSet,
        "18-import-dialog-light.png",
        "38-translator-dialog-light.png",
        "39-xml-editor-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png",
    ];

    [TestMethod]
    public void Flagship_release_gate_pins_full_veteran_screenshot_set()
    {
        string scriptPath = FindPath("scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        string[] screenshotSet = ExtractScreenshotSet(scriptText, "expected_screenshots");
        CollectionAssert.AreEqual(ExpectedFlagshipScreenshotSet, screenshotSet);
    }

    [TestMethod]
    public void Visual_familiarity_exit_gate_pins_full_veteran_screenshot_set()
    {
        string scriptPath = FindPath("scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        string[] screenshotSet = ExtractScreenshotSet(scriptText, "required_screenshots");
        CollectionAssert.AreEqual(ExpectedVisualFamiliarityScreenshotSet, screenshotSet);
    }

    [TestMethod]
    public void Avalonia_dense_workbench_defaults_remain_compact_for_legacy_familiarity()
    {
        string appPath = FindPath("Chummer.Avalonia", "App.axaml");
        string sectionHostPath = FindPath("Chummer.Avalonia", "Controls", "SectionHostControl.axaml");
        string appText = File.ReadAllText(appPath);
        string sectionHostText = File.ReadAllText(sectionHostPath);

        StringAssert.Contains(appText, "<Style Selector=\"Border.shell-card\">");
        StringAssert.Contains(appText, "<Setter Property=\"CornerRadius\" Value=\"0\" />");
        StringAssert.Contains(appText, "<Setter Property=\"Padding\" Value=\"2\" />");

        StringAssert.Contains(sectionHostText, "x:Name=\"SectionContextBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"ClassicCharacterSheetBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"AttributeParityEditorBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"BuildLabBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"SectionRowsBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"SectionQuickActionsBorder\"");
        Assert.IsFalse(sectionHostText.Contains("Padding=\"8\"", StringComparison.Ordinal), "Dense workbench defaults must avoid oversized card padding.");
        Assert.IsFalse(sectionHostText.Contains("RowSpacing=\"6\"", StringComparison.Ordinal), "Dense workbench defaults must avoid oversized row spacing.");
    }

    [TestMethod]
    public void Pixefy_visual_verifier_fail_closes_without_windows_authority_and_forbidden_inline_shell_checks()
    {
        string verifierPath = FindPath("scripts", "verify_pixefy_chummer5a_screenshot_comparison.py");
        string verifierText = File.ReadAllText(verifierPath);

        StringAssert.Contains(verifierText, "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(verifierText, "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json");
        StringAssert.Contains(verifierText, "releaseAuthorityPlatform");
        StringAssert.Contains(verifierText, "real_menu_items");
        StringAssert.Contains(verifierText, "dedicated_desktop_dialog_window");
        StringAssert.Contains(verifierText, "forbiddenInlineSurface");
        StringAssert.Contains(verifierText, "02-menu-open-light.png");
        StringAssert.Contains(verifierText, "rightShellVisible");
        StringAssert.Contains(verifierText, "rightShellWidth");
        StringAssert.Contains(verifierText, "inlineCommandSurfaceVisible");
        StringAssert.Contains(verifierText, "dialogWindowVisible");
        StringAssert.Contains(verifierText, "Select Build Method");
    }

    [TestMethod]
    public void Screenshot_control_evidence_source_pins_authority_metadata_and_forbidden_inline_shell_markers()
    {
        string gateTestsPath = FindPath("Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs");
        string gateTestsText = File.ReadAllText(gateTestsPath);

        StringAssert.Contains(gateTestsText, "visualBaseline = \"Chummer5a\"");
        StringAssert.Contains(gateTestsText, "releaseAuthorityPlatform = \"windows\"");
        StringAssert.Contains(gateTestsText, "menuInteractionMode = \"real_menu_items\"");
        StringAssert.Contains(gateTestsText, "dialogHostPolicy = \"dedicated_desktop_dialog_window\"");
        StringAssert.Contains(gateTestsText, "forbiddenInlineSurface = \"RightShellRegion\"");
        StringAssert.Contains(gateTestsText, "windowsDesktopExitGate = \".codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json\"");
        StringAssert.Contains(gateTestsText, "startupSmokeAndExecutableGate = \".codex-studio/published/NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json\"");
        StringAssert.Contains(gateTestsText, "RightShellVisible: rightShellVisible");
        StringAssert.Contains(gateTestsText, "RightShellWidth: rightShellWidth");
        StringAssert.Contains(gateTestsText, "InlineCommandSurfaceVisible: inlineCommandSurfaceVisible");
        StringAssert.Contains(gateTestsText, "DialogWindowVisible: dialogWindowVisible");
        StringAssert.Contains(gateTestsText, "string[] visibleMenuCommandIds = CaptureVisibleCommandIds(harness);");
    }

    private static string[] ExtractScreenshotSet(string scriptText, string listName)
    {
        Match listMatch = Regex.Match(
            scriptText,
            $@"{Regex.Escape(listName)}\s*=\s*\[(?<body>.*?)\]",
            RegexOptions.Singleline);

        Assert.IsTrue(listMatch.Success, $"Could not find screenshot list '{listName}'.");

        MatchCollection itemMatches = Regex.Matches(
            listMatch.Groups["body"].Value,
            "\"(?<file>[0-9]{2}-[^\"]+\\.png)\"");

        List<string> screenshots = itemMatches
            .Select(match => match.Groups["file"].Value)
            .ToList();

        Assert.IsTrue(screenshots.Count > 0, $"Screenshot list '{listName}' was empty.");
        return screenshots.ToArray();
    }

    private static string FindPath(params string[] parts)
    {
        foreach (string? root in CandidateRoots())
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            DirectoryInfo current = new(root);
            while (true)
            {
                string candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                if (current.Parent is null)
                {
                    break;
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException("Could not locate file.", Path.Combine(parts));
    }

    private static IEnumerable<string?> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }
}
