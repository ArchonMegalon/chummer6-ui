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
    private static readonly string[] ExpectedVeteranScreenshotSet =
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
        CollectionAssert.AreEqual(ExpectedVeteranScreenshotSet, screenshotSet);
    }

    [TestMethod]
    public void Visual_familiarity_exit_gate_pins_full_veteran_screenshot_set()
    {
        string scriptPath = FindPath("scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        string[] screenshotSet = ExtractScreenshotSet(scriptText, "required_screenshots");
        CollectionAssert.AreEqual(ExpectedVeteranScreenshotSet, screenshotSet);
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
        StringAssert.Contains(appText, "<Setter Property=\"Padding\" Value=\"3\" />");

        StringAssert.Contains(sectionHostText, "x:Name=\"BuildLabBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"BrowseWorkspaceBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"ContactGraphBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"NpcPersonaStudioBorder\"");
        StringAssert.Contains(sectionHostText, "x:Name=\"DowntimePlannerBorder\"");
        Assert.IsFalse(sectionHostText.Contains("Padding=\"8\"", StringComparison.Ordinal), "Dense workbench defaults must avoid oversized card padding.");
        Assert.IsFalse(sectionHostText.Contains("RowSpacing=\"6\"", StringComparison.Ordinal), "Dense workbench defaults must avoid oversized row spacing.");
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
