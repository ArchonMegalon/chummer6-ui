#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class ClassicFormPortDesktopGuardTests
{
    [TestMethod]
    public void Classic_formport_receipts_are_dense_and_ready()
    {
        string repoRoot = FindRepoRoot();
        string publishedRoot = Path.Combine(repoRoot, ".codex-studio", "published");

        AssertPublishedJsonStatus(publishedRoot, "CLASSIC_MODE_BOUNDARY.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "LEGACY_FORM_INVENTORY.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "FORM_PORT_CONTRACTS.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "CLASSIC_MODE_NO_NOISE_GATE.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "CLASSIC_PIXEL_CONTACT_SHEETS.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "CLASSIC_VETERAN_TASK_TIME_BUDGETS.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "FORM_PORT_COVERAGE_MATRIX.generated.json", "pass");
        AssertPublishedJsonStatus(publishedRoot, "CLASSIC_FORMPORT_REALITY_AUDIT.generated.json", "pass");

        string humanReview = File.ReadAllText(Path.Combine(publishedRoot, "CLASSIC_FORM_PORT_HUMAN_REVIEW.md"));
        StringAssert.Contains(humanReview, "PASS");
        StringAssert.Contains(humanReview, "CLASSIC_FORM_PORT_DESKTOP_READY");

        string verdict = File.ReadAllText(Path.Combine(publishedRoot, "CLASSIC_FORM_PORT_DESKTOP_VERDICT.md")).Trim();
        Assert.AreEqual("CLASSIC_FORM_PORT_DESKTOP_READY", verdict);
    }

    [TestMethod]
    public void Classic_formport_source_contract_stays_wired()
    {
        string repoRoot = FindRepoRoot();
        string appText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"));
        string policyText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "ClassicModePolicy.cs"));
        string refreshText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.StateRefresh.cs"));
        string parserText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormDesignerParser.cs"));
        string classicSurfaceText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortSurfaceControl.cs"));
        string hostText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPortHostControl.axaml.cs"));
        string bridgeText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortViewModelBridge.cs"));
        string mainWindowText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.axaml"));
        string mainWindowCodeText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.axaml.cs"));
        string bindingText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ControlBinding.cs"));
        string classicMenuText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicMenuBar.axaml"));
        string classicToolStripText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicToolStrip.axaml"));
        string classicStatusStripText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicStatusStrip.axaml"));
        string hostAxamlText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPortHostControl.axaml"));

        StringAssert.Contains(policyText, "DesktopUiMode.Classic");
        StringAssert.Contains(policyText, "return DesktopUiMode.Classic;");
        StringAssert.Contains(appText, "CreateDesktopWindow");
        StringAssert.Contains(appText, "ClassicModePolicy.ResolveCurrentMode()");
        StringAssert.Contains(refreshText, "ClassicFormPortHostControl.IsVisible = showClassicFormPort;");
        StringAssert.Contains(refreshText, "SectionHostControl.IsVisible = !showClassicFormPort;");
        StringAssert.Contains(parserText, "Parse(string relativeDesignerPath)");
        StringAssert.Contains(hostText, "CharacterCareerClassicPort");
        StringAssert.Contains(hostText, "CharacterCreateClassicPort");
        StringAssert.Contains(hostText, "SettingsClassicPort");
        StringAssert.Contains(hostText, "MasterIndexClassicPort");
        Assert.IsFalse(hostText.Contains("GearClassicPort", StringComparison.Ordinal));
        StringAssert.Contains(mainWindowText, "x:Name=\"ClassicMenuBarControl\"");
        StringAssert.Contains(mainWindowText, "x:Name=\"ClassicToolStripControl\"");
        StringAssert.Contains(mainWindowText, "x:Name=\"ClassicStatusStripControl\"");
        StringAssert.Contains(mainWindowCodeText, "classicToolStrip: ClassicToolStripControl");
        StringAssert.Contains(mainWindowCodeText, "classicMenuBar: ClassicMenuBarControl");
        StringAssert.Contains(mainWindowCodeText, "classicStatusStrip: ClassicStatusStripControl");
        StringAssert.Contains(mainWindowCodeText, "_controls.ApplyDesktopModeChrome(ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic);");
        StringAssert.Contains(bindingText, "IToolStripSurface activeToolStrip = ClassicModePolicy.IsClassicDefault() ? classicToolStrip : toolStrip;");
        StringAssert.Contains(bindingText, "ClassicMenuBar.SetState(shellFrame.HeaderState.MenuBar);");
        StringAssert.Contains(bindingText, "ClassicStatusStrip.SetState(shellFrame.ChromeState.StatusStrip);");
        Assert.IsFalse(classicMenuText.Contains("ShellMenuBarControl", StringComparison.Ordinal));
        Assert.IsFalse(classicToolStripText.Contains("ToolStripControl", StringComparison.Ordinal));
        Assert.IsFalse(classicStatusStripText.Contains("StatusStripControl", StringComparison.Ordinal));
        Assert.IsFalse(hostAxamlText.Contains("Legacy form-native surface projection", StringComparison.Ordinal));
        StringAssert.Contains(classicMenuText, "Header=\"File\"");
        StringAssert.Contains(classicStatusStripText, "Mode: Classic");
        Assert.IsFalse(classicToolStripText.Contains("Classic Mode default: form-native workbench", StringComparison.Ordinal));
        Assert.IsFalse(hostAxamlText.Contains("Classic Mode", StringComparison.Ordinal));
        Assert.IsFalse(hostText.Contains("Legacy form:", StringComparison.Ordinal));
        Assert.IsFalse(hostText.Contains("Classic Default", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceText.Contains("Classic form-native projection", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceText.Contains("snapshot.EventHandlers", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceText.Contains("state.Rows.Take(20)", StringComparison.Ordinal));
        Assert.IsFalse(bridgeText.Contains("foreach (var row in state.Rows)", StringComparison.Ordinal));
        Assert.IsFalse(bridgeText.Contains("ContainsAny(haystack", StringComparison.Ordinal));
        Assert.IsFalse(bridgeText.Contains("ReadPreviewFacts(state.PreviewJson)", StringComparison.Ordinal));
        StringAssert.Contains(bridgeText, "ClassicFormPortDocument");
        StringAssert.Contains(bridgeText, "CreateFromPreview");
        Assert.IsFalse(bridgeText.Contains("CreateFromSectionRows", StringComparison.Ordinal));
        Assert.IsFalse(bridgeText.Contains("CreateFromRows(IReadOnlyList<SectionRowDisplayItem> rows)", StringComparison.Ordinal));
        Assert.IsFalse(bridgeText.Contains("IReadOnlyList<ClassicPortLineItem>", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceText.Contains("IsEnabled = false", StringComparison.Ordinal));
        AssertRuntimePortUsesDenseClassicControls(repoRoot, "CharacterCareerClassicPort.axaml", "CharacterCareerClassicPort.axaml.cs");
        AssertRuntimePortUsesDenseClassicControls(repoRoot, "CharacterCreateClassicPort.axaml", "CharacterCreateClassicPort.axaml.cs");
        AssertRuntimePortUsesDenseClassicControls(repoRoot, "SettingsClassicPort.axaml", "SettingsClassicPort.axaml.cs");
        AssertRuntimePortUsesDenseClassicControls(repoRoot, "MasterIndexClassicPort.axaml", "MasterIndexClassicPort.axaml.cs");
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "CharacterCareerClassicPort.axaml")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "CharacterCreateClassicPort.axaml")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "SettingsClassicPort.axaml")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "MasterIndexClassicPort.axaml")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "GearClassicPort.axaml")));
    }

    private static void AssertPublishedJsonStatus(string publishedRoot, string fileName, string expectedStatus)
    {
        string path = Path.Combine(publishedRoot, fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.AreEqual(expectedStatus, document.RootElement.GetProperty("status").GetString(), fileName);
    }

    private static void AssertRuntimePortUsesDenseClassicControls(string repoRoot, string xamlFileName, string codeFileName)
    {
        string xamlText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", xamlFileName));
        string codeText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", codeFileName));
        bool hasDenseControls =
            xamlText.Contains("ListBox", StringComparison.Ordinal)
            && xamlText.Contains("ComboBox", StringComparison.Ordinal)
            && (xamlText.Contains("TreeView", StringComparison.Ordinal) || xamlText.Contains("ContextMenu", StringComparison.Ordinal));
        bool projectsGenericRows =
            codeText.Contains("RenderFieldRows(", StringComparison.Ordinal)
            || codeText.Contains("MatchRows(state.Rows", StringComparison.Ordinal)
            || codeText.Contains("FindValue(state.Rows", StringComparison.Ordinal);

        Assert.IsTrue(hasDenseControls, $"{xamlFileName} must expose dense classic controls.");
        Assert.IsFalse(projectsGenericRows, $"{codeFileName} must not project generic row data.");
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
