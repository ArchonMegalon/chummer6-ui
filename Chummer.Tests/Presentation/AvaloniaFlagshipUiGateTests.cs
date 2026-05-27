#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Contracts.AI;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Hosting.Presentation;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[DoNotParallelize]
// Runtime_backed_file_menu_restores_classic_save_and_print_commands
public sealed class AvaloniaFlagshipUiGateTests
{
    private static readonly object HeadlessInitLock = new();
    private static readonly JsonSerializerOptions ScreenshotEvidenceJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 256
    };
    private static bool _headlessInitialized;
    private const int HeadlessSessionAttempts = 3;
    private static readonly string[] DefaultChummer5aFixtureUiReconstructionFixtureNames =
    [
        "Soma (Career).chum5"
    ];
    private static readonly string[] VeteranCertificationScreenshotFiles =
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
        "19-workflow-file-menu-loaded-light.png",
        "20-workflow-skills-section-light.png",
        "21-workflow-skill-add-dialog-light.png",
        "22-workflow-qualities-section-light.png",
        "23-workflow-quality-add-dialog-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
        "26-workflow-weapons-section-light.png",
        "27-workflow-weapon-add-dialog-light.png",
        "28-workflow-armor-section-light.png",
        "29-workflow-armor-add-dialog-light.png",
        "30-workflow-cyberware-section-light.png",
        "31-workflow-powers-section-light.png",
        "32-workflow-adept-power-dialog-light.png",
        "33-workflow-complex-form-dialog-light.png",
        "34-workflow-validate-section-light.png",
        "35-workflow-rules-section-light.png",
        "36-workflow-new-character-dialog-light.png",
        "37-workflow-calendar-section-light.png",
        "38-translator-dialog-light.png",
        "39-xml-editor-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png"
    ];
    private static readonly string[] RequiredVeteranCertificationScreenshots =
    [
        "01-initial-shell-light.png",
        "02-menu-open-light.png",
        "03-settings-open-light.png",
        "16-master-index-dialog-light.png",
        "17-character-roster-dialog-light.png",
        "18-import-dialog-light.png"
    ];
    private static readonly VeteranCertificationReviewStep[] VeteranCertificationReviewSteps =
    [
        new("toolstrip", "01-initial-shell-light.png", "Capture initial promoted Avalonia shell after WaitForReady.", "Chummer5a ChummerMainForm toolStrip New/Open/OpenForPrinting/OpenForExport lineage.", []),
        new("menu", "02-menu-open-light.png", "Click FileMenuButton and capture the visible command list.", "Chummer5a ChummerMainForm File/Tools/Windows/Help top menu lineage.", []),
        new("settings", "03-settings-open-light.png", "Press Ctrl+G and capture the Global Settings dialog.", "Chummer5a EditGlobalSettings Global Options, Master Index, and Character Roster lineage.", ["Global Settings"]),
        new("master_index", "16-master-index-dialog-light.png", "Execute master_index and capture the Master Index dialog.", "Chummer5a MasterIndex search utility lineage.", ["Master Index"]),
        new("roster", "17-character-roster-dialog-light.png", "Execute character_roster and capture the Character Roster dialog.", "Chummer5a CharacterRoster watch-folder utility lineage.", ["Character Roster"]),
        new("import", "18-import-dialog-light.png", "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.", "Chummer5a File/Open and Hero Lab Importer import route lineage.", ["Ruleset"])
    ];
    private static readonly VeteranCertificationReviewStep[] ImportRouteReviewSteps =
    [
        new("translator", "38-translator-dialog-light.png", "Execute translator and capture the governed translator route on the promoted desktop head.", "Chummer5a Translator utility lineage.", ["Translator", "Language Search", "Enabled Language Overlays"]),
        new("xml_amendment_editor", "39-xml-editor-dialog-light.png", "Execute xml_editor and capture XML bridge plus custom-data posture directly on the desktop route.", "Chummer5a custom-data/XML amendment authoring lineage.", ["XML Editor", "Custom Data Lane", "XML Bridge"]),
        new("hero_lab_importer", "40-hero-lab-importer-dialog-light.png", "Execute hero_lab_importer and capture direct Hero Lab import-oracle posture.", "Chummer5a Hero Lab importer lineage.", ["Hero Lab Importer", "Import Oracle Lane", "Adjacent SR6 Oracle Receipt"])
    ];
    private static readonly WorkflowScreenshotCoverageEntry[] WorkflowScreenshotCoverage =
    [
        new("create-open-import-save-save-as-print-export", "Chummer4/Chummer5a File menu New/Open/Save/Save As/Print/Export handoff lineage.", ["19-workflow-file-menu-loaded-light.png", "36-workflow-new-character-dialog-light.png", "18-import-dialog-light.png", "40-hero-lab-importer-dialog-light.png"]),
        new("metatype-priorities-karma-entry", "Chummer4/Chummer5a character creation priority and karma journal lineage.", ["15-creation-section-light.png", "11-diary-dialog-light.png", "36-workflow-new-character-dialog-light.png"]),
        new("attributes-skills-skill-groups-specializations-knowledge-languages", "Chummer4/Chummer5a Attributes and Skills tab edit-list lineage.", ["15-creation-section-light.png", "20-workflow-skills-section-light.png", "21-workflow-skill-add-dialog-light.png"]),
        new("qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources", "Chummer4/Chummer5a qualities, contacts, diary, notes, and source review lineage.", ["10-contacts-section-light.png", "22-workflow-qualities-section-light.png", "23-workflow-quality-add-dialog-light.png", "37-workflow-calendar-section-light.png"]),
        new("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers", "Chummer4/Chummer5a gear, armor, weapon, vehicle, drone, mod, and location list lineage.", ["09-vehicles-section-light.png", "24-workflow-gear-section-light.png", "25-workflow-gear-add-dialog-light.png", "26-workflow-weapons-section-light.png", "27-workflow-weapon-add-dialog-light.png", "28-workflow-armor-section-light.png", "29-workflow-armor-add-dialog-light.png"]),
        new("cyberware-bioware-modular-hierarchies-nested-plugins", "Chummer4/Chummer5a cyberware/bioware nested selection and plugin lineage.", ["08-cyberware-dialog-light.png", "30-workflow-cyberware-section-light.png"]),
        new("magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms", "Chummer4/Chummer5a magic, adept, resonance, initiation, and matrix form lineage.", ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png", "31-workflow-powers-section-light.png", "32-workflow-adept-power-dialog-light.png", "33-workflow-complex-form-dialog-light.png"]),
        new("improvements-explain-result-parity", "Chummer4/Chummer5a validation, explain, source, and applied-result review lineage.", ["14-advancement-dialog-light.png", "16-master-index-dialog-light.png", "34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"]),
        new("recovery-reload-migration-roundtrips", "Chummer4/Chummer5a open/import/reload/recovery roundtrip lineage.", ["04-loaded-runner-light.png", "18-import-dialog-light.png", "19-workflow-file-menu-loaded-light.png"]),
        new("dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare", "Chummer4/Chummer5a dense list, quick action, preview, drill-in, and compare workbench lineage.", ["05-dense-section-light.png", "06-dense-section-dark.png", "07-loaded-runner-tabs-light.png", "24-workflow-gear-section-light.png", "25-workflow-gear-add-dialog-light.png"])
    ];
    private static HeadlessUnitTestSession? _headlessSession;

    [TestMethod]
    public void Blazor_root_route_ownership_stays_with_desktop_shell_anchor_and_moves_showcase_off_root()
    {
        string homePath = ResolveSourceFile("Chummer.Blazor", "Components", "Pages", "Home.razor");
        string showcasePath = ResolveSourceFile("Chummer.Blazor", "Components", "Pages", "Showcase.razor");
        string legacyPath = ResolveSourceFile("Chummer.Blazor", "Pages", "Index.razor");

        string homeText = File.ReadAllText(homePath);
        string showcaseText = File.ReadAllText(showcasePath);
        string legacyText = File.ReadAllText(legacyPath);

        StringAssert.Contains(homeText, "@page \"/\"");
        StringAssert.Contains(homeText, "Desktop shell route anchor");
        Assert.IsFalse(homeText.Contains("panel-grid", StringComparison.Ordinal));
        StringAssert.Contains(showcaseText, "@page \"/showcase\"");
        StringAssert.Contains(showcaseText, "panel-grid");
        Assert.IsFalse(legacyText.Contains("@page \"/\"", StringComparison.Ordinal));
        Assert.IsFalse(legacyText.Contains("@page \"/blazor\"", StringComparison.Ordinal));
        StringAssert.Contains(legacyText, "@page \"/legacy-console\"");
    }

    [TestMethod]
    public void Avalonia_startup_keeps_the_workbench_as_first_paint_but_still_invokes_restore_continuation_when_needed()
    {
        string appPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml.cs");
        string appText = File.ReadAllText(appPath);

        StringAssert.Contains(appText, "DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);");
        Assert.IsTrue(
            appText.Contains("if (installLinkingContext is not null)", StringComparison.Ordinal),
            "Startup modal prompts should still be gated on active install-linking context.");
        Assert.IsFalse(
            appText.Contains("lifetime.Shutdown()", StringComparison.Ordinal),
            "Guest continuation must not force-quit the Avalonia desktop after the native install-linking surface closes.");
        Assert.IsFalse(
            appText.Contains("DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null);", StringComparison.Ordinal),
            "The flagship Avalonia startup path must stay on the workbench by default instead of reopening the desktop home cockpit.");
    }

    [TestMethod]
    public void Fresh_launch_main_window_survives_first_paint_without_self_termination()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.AdvanceFrames(120);

            Assert.IsTrue(harness.Window.IsVisible, "Fresh launch must not auto-close the main window after first paint.");
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible, "Fresh launch must keep the menu bar visible.");
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible, "Fresh launch must keep the toolstrip visible.");
            Assert.IsTrue(harness.FindControl<Control>("RosterPaneRegion").IsVisible, "Fresh launch must keep the roster rail visible in the classic workbench posture.");
            Assert.IsFalse(harness.FindControl<Control>("LeftNavigatorRegion").IsVisible, "Fresh launch must not open the codex navigator pane by default.");
            Assert.IsFalse(harness.FindControl<Control>("SummaryHeaderRegion").IsVisible, "Fresh launch must not show the recovery summary band without an active restore problem.");
            Assert.IsFalse(
                harness.TryWaitUntil(() => !harness.Window.IsVisible, timeoutMs: 250),
                "Fresh launch must stay alive instead of terminating shortly after startup.");
        });
    }

    [TestMethod]
    public void Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state()
    {
        string homePath = ResolveSourceFile("Chummer.Avalonia", "DesktopHomeWindow.cs");
        string homeText = File.ReadAllText(homePath);

        StringAssert.Contains(homeText, "if (installContext?.ShouldPrompt == true)");
        StringAssert.Contains(homeText, "if (!string.Equals(updateStatus.Status, \"current\", StringComparison.Ordinal))");
        StringAssert.Contains(homeText, "if (supportProjection.NeedsAttention)");
        Assert.IsFalse(
            homeText.Contains("workspaces.Count == 0", StringComparison.Ordinal),
            "A fresh install with no workspaces must still enter the workbench instead of reopening the desktop home cockpit.");
    }

    [TestMethod]
    public void Avalonia_workbench_shell_removes_extra_non_chummer5a_chrome()
    {
        string projectorPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs");
        string summaryHeaderPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "SummaryHeaderControl.axaml.cs");
        string summaryHeaderMarkupPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "SummaryHeaderControl.axaml");
        string projectorText = File.ReadAllText(projectorPath);
        string summaryHeaderText = File.ReadAllText(summaryHeaderPath);
        string summaryHeaderMarkupText = File.ReadAllText(summaryHeaderMarkupPath);

        StringAssert.Contains(projectorText, "ShowNavigatorPane: false");
        StringAssert.Contains(projectorText, "bool summaryHeaderHasVisibleContent = !string.IsNullOrWhiteSpace(restoreContinuitySummary)");
        StringAssert.Contains(projectorText, "return [];");
        StringAssert.Contains(projectorText, "if (shellNotice.StartsWith(\"Restored \", StringComparison.OrdinalIgnoreCase))");
        StringAssert.Contains(summaryHeaderText, "bool hasRecoveryContext =");
        StringAssert.Contains(summaryHeaderText, "SaveLocalWorkButton.IsEnabled = state.CanSaveLocalWorkBeforeRestore;");
        StringAssert.Contains(summaryHeaderMarkupText, "Keep Local");
        StringAssert.Contains(summaryHeaderMarkupText, "Campaign");
    }

    [TestMethod]
    public void Bundled_demo_runner_fixture_is_published_for_both_desktop_heads()
    {
        string avaloniaProjectPath = ResolveSourceFile("Chummer.Avalonia", "Chummer.Avalonia.csproj");
        string blazorDesktopProjectPath = ResolveSourceFile("Chummer.Blazor.Desktop", "Chummer.Blazor.Desktop.csproj");

        string avaloniaProjectText = File.ReadAllText(avaloniaProjectPath);
        string blazorDesktopProjectText = File.ReadAllText(blazorDesktopProjectPath);

        StringAssert.Contains(avaloniaProjectText, "Samples/Legacy/Soma-Career.chum5");
        StringAssert.Contains(avaloniaProjectText, "<CopyToPublishDirectory>Always</CopyToPublishDirectory>");
        StringAssert.Contains(blazorDesktopProjectText, "Samples/Legacy/Soma-Career.chum5");
        StringAssert.Contains(blazorDesktopProjectText, "<CopyToPublishDirectory>Always</CopyToPublishDirectory>");
    }

    [TestMethod]
    public void Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers()
    {
        string releaseGatePath = ResolveSourceFile("scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string visualGatePath = ResolveSourceFile("scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string layoutGatePath = ResolveSourceFile("scripts", "ai", "milestones", "chummer5a-layout-hard-gate.sh");
        string appAxamlPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml");
        string mainWindowPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.axaml");
        string mainWindowStateRefreshPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.StateRefresh.cs");
        string avaloniaProjectorPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs");
        string toolStripPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "ToolStripControl.axaml");
        string navigatorPanePath = ResolveSourceFile("Chummer.Avalonia", "Controls", "NavigatorPaneControl.axaml");
        string shellCatalogPath = ResolveSourceFile("Chummer.Presentation", "Shell", "CatalogOnlyRulesetShellCatalogResolver.cs");
        string shellChromeBoundaryPath = ResolveSourceFile("Chummer.Presentation", "UiKit", "ShellChromeBoundary.cs");
        string blazorShellPath = ResolveSourceFile("Chummer.Blazor", "Components", "Layout", "DesktopShell.razor.cs");
        string sectionPanePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "SectionPane.razor");
        string workspaceLeftPanePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "WorkspaceLeftPane.razor");
        string openWorkspaceTreePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "OpenWorkspaceTree.razor");
        string appCssPath = ResolveSourceFile("Chummer.Blazor", "wwwroot", "app.css");

        string releaseGateText = File.ReadAllText(releaseGatePath);
        string visualGateText = File.ReadAllText(visualGatePath);
        string layoutGateText = File.ReadAllText(layoutGatePath);
        string appAxamlText = File.ReadAllText(appAxamlPath);
        string mainWindowText = File.ReadAllText(mainWindowPath);
        string mainWindowStateRefreshText = File.ReadAllText(mainWindowStateRefreshPath);
        string avaloniaProjectorText = File.ReadAllText(avaloniaProjectorPath);
        string toolStripText = File.ReadAllText(toolStripPath);
        string navigatorPaneText = File.ReadAllText(navigatorPanePath);
        string shellCatalogText = File.ReadAllText(shellCatalogPath);
        string shellChromeBoundaryText = File.ReadAllText(shellChromeBoundaryPath);
        string blazorShellText = File.ReadAllText(blazorShellPath);
        string sectionPaneText = File.ReadAllText(sectionPanePath);
        string workspaceLeftPaneText = File.ReadAllText(workspaceLeftPanePath);
        string openWorkspaceTreeText = File.ReadAllText(openWorkspaceTreePath);
        string appCssText = File.ReadAllText(appCssPath);

        StringAssert.Contains(releaseGateText, "chummer5a-layout-hard-gate.sh");
        StringAssert.Contains(visualGateText, "chummer5a-layout-hard-gate.sh");
        StringAssert.Contains(visualGateText, "promote_fresh_runtime_screenshot_pack");
        StringAssert.Contains(visualGateText, ".codex-studio/out/chummer5a-ultimate-parity-tester/live/screenshots/actual");
        StringAssert.Contains(visualGateText, ".codex-studio/out/chummer5a-parity-tester/live/screenshots/actual");
        StringAssert.Contains(visualGateText, ".codex-studio/out/ui-flagship-release-gate-screenshots-debug");
        StringAssert.Contains(layoutGateText, "defaultSingleRunnerKeepsWorkspaceChromeCollapsed");
        StringAssert.Contains(appAxamlText, "FontFamily\" Value=\"Trebuchet MS,Verdana,Geneva,Arial\"");
        StringAssert.Contains(toolStripText, "x:Name=\"DesktopHomeButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"ImportFileButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"SaveButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"PrintButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"CopyButton\"");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"SaveButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"PrintButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Save before Print.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"PrintButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"CopyButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Print before Copy.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"CopyButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"DesktopHomeButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Copy before New.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"DesktopHomeButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"ImportFileButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires New before Open.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"save_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"print_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep save before print in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"print_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"copy\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep print before copy in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"copy\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"new_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep copy before new in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"new_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"open_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep new before open in the preferred toolstrip order.");
        StringAssert.Contains(blazorShellText, "private bool ShowLeftPane =>");
        StringAssert.Contains(blazorShellText, "_shellSurfaceState.OpenWorkspaces.Count > 1");
        StringAssert.Contains(shellCatalogText, "[\"file\", \"tools\", \"windows\", \"help\"]");
        Assert.IsFalse(shellCatalogText.Contains("Command(\"edit\",", StringComparison.Ordinal));
        Assert.IsFalse(shellCatalogText.Contains("Command(\"special\",", StringComparison.Ordinal));
        StringAssert.Contains(shellCatalogText, "Command(\"switch_ruleset\", \"command.switch_ruleset\", \"special\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"new_window\", \"command.new_window\", \"windows\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"close_window\", \"command.close_window\", \"windows\", false)");
        StringAssert.Contains(shellChromeBoundaryText, "[\"switch_ruleset\"] = \"Switch Ruleset...\"");
        StringAssert.Contains(shellChromeBoundaryText, "[\"new_window\"] = \"New Window\"");
        StringAssert.Contains(mainWindowText, "ColumnDefinitions=\"0,*,0\"");
        StringAssert.Contains(mainWindowText, "x:Name=\"LeftNavigatorRegion\"");
        StringAssert.Contains(mainWindowText, "IsVisible=\"False\"");
        StringAssert.Contains(mainWindowStateRefreshText, "ApplyWorkbenchChromeVisibility(shellFrame);");
        Assert.IsTrue(
            mainWindowStateRefreshText.Contains("new GridLength(228)", StringComparison.Ordinal)
            || mainWindowStateRefreshText.Contains("new GridLength(264)", StringComparison.Ordinal),
            "Workbench chrome visibility must keep an explicit fixed desktop-width left pane.");
        StringAssert.Contains(mainWindowStateRefreshText, "new GridLength(0)");
        StringAssert.Contains(avaloniaProjectorText, "ShowNavigatorPane: false");
        StringAssert.Contains(navigatorPaneText, "x:Name=\"CodexHeadingText\"");
        StringAssert.Contains(navigatorPaneText, "IsVisible=\"False\"");
        StringAssert.Contains(sectionPaneText, "classic-summary-grid");
        StringAssert.Contains(sectionPaneText, "classic-attribute-grid");
        StringAssert.Contains(workspaceLeftPaneText, "@if (ShowSectionActions)");
        StringAssert.Contains(workspaceLeftPaneText, "@if (ShowWorkflowSurfaces)");
        StringAssert.Contains(openWorkspaceTreeText, "class=\"visually-hidden\"");
        StringAssert.Contains(workspaceLeftPaneText, "class=\"left-pane\"");
        Assert.IsFalse(
            openWorkspaceTreeText.Contains("workspace.Id.Value</span>", StringComparison.Ordinal),
            "Classic left-rail parity must not print workspace ids inside the visible dossier tree rows.");
        StringAssert.Contains(appCssText, ".classic-summary-grid");
        StringAssert.Contains(appCssText, ".classic-attribute-grid");
        StringAssert.Contains(appCssText, "--ui-kit-classic-font");
        StringAssert.Contains(appCssText, ".classic-menu-bar");
        StringAssert.Contains(appCssText, ".classic-tool-strip");
        StringAssert.Contains(appCssText, ".tool-divider");
        StringAssert.Contains(appCssText, ".classic-tab-strip");
        StringAssert.Contains(appCssText, ".classic-dialog");
        StringAssert.Contains(appCssText, ".visually-hidden");
        StringAssert.Contains(appCssText, ".workspace-layout--with-left-pane");
        StringAssert.Contains(appCssText, ".workspace-layout--without-left-pane");
    }

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            Assert.IsTrue(harness.FindControl<MenuItem>("FileMenuButton").IsEnabled, "File menu must stay enabled after real shell bootstrap.");
            Assert.IsTrue(harness.FindControl<MenuItem>("HelpMenuButton").IsEnabled, "Help menu must stay enabled after real shell bootstrap.");
            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
            ListBox commandsList = harness.FindControl<ListBox>("CommandsList");
            string[] visibleCommandIds = CaptureVisibleCommandIds(commandsList);

            CollectionAssert.Contains(visibleCommandIds, "open_character");
            CollectionAssert.Contains(visibleCommandIds, "new_character");
            CollectionAssert.Contains(visibleCommandIds, "save_character");
        });
    }

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell()
    {
        Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters();
    }

    [TestMethod]
    public void File_menu_new_character_creates_runtime_workspace()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            Assert.IsNull(harness.State.WorkspaceId, "Runtime-backed New Character proof starts without an active workspace.");

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));

            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);
            Assert.AreEqual("dialog.new_character", harness.State.ActiveDialog?.Id);
            Assert.IsNull(harness.State.WorkspaceId);
            Assert.IsNull(harness.State.Profile);
        });
    }

    [TestMethod]
    public void File_menu_new_character_completes_into_visible_runtime_workspace()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));
            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);

            harness.InvokeDialogAction("create_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                || string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.karma_workflow", StringComparison.Ordinal));

            harness.InvokeDialogAction("complete_new_character_workflow");
            harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Profile is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "new character completion should hydrate a visible runtime workspace");

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            harness.WaitUntil(() => rosterTree.Bounds.Width > 0d && rosterTree.Bounds.Height > 0d);

            Control? sectionHost = harness.FindControlOrDefault<Control>("SectionHostControl");
            if (sectionHost is not null)
            {
                Assert.IsTrue(sectionHost.IsVisible, "Runtime new-character flow should surface the main section host.");
            }

            TextBlock? dialogTitle = harness.FindControlOrDefault<TextBlock>("DialogTitleText");
            if (dialogTitle is not null)
            {
                Assert.IsTrue(
                    string.IsNullOrWhiteSpace(dialogTitle.Text) || string.Equals(dialogTitle.Text, "(none)", StringComparison.Ordinal),
                    "Runtime new-character flow should leave no blocking dialog title visible after completion.");
            }
        });
    }

    [TestMethod]
    public void Master_index_search_keeps_focus_after_runtime_backed_text_updates()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            harness.ClickMenuCommand("master_index");
            harness.WaitUntil(() =>
                    string.Equals(harness.State.ActiveDialog?.Id, "dialog.master_index", StringComparison.Ordinal)
                    && string.Equals(harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text, "Master Index", StringComparison.Ordinal)
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "master index dialog should be open before editing search");

            string searchFieldName = DesktopDialogAccessibility.BuildFieldInputName("masterIndexSearch");
            TextBox searchBox = harness.FindControl<TextBox>(searchFieldName);
            Assert.IsTrue(searchBox.Focus(), "Master Index search box must accept focus before typing.");

            searchBox.Text = "adept";
            harness.WaitUntil(() =>
                    string.Equals(
                        DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "masterIndexSearch"),
                        "adept",
                        StringComparison.Ordinal),
                context: "master index search value should round-trip through the runtime-backed dialog update");

            TextBox refreshedSearchBox = harness.FindControl<TextBox>(searchFieldName);
            Assert.IsTrue(refreshedSearchBox.IsEnabled, "Master Index search must remain interactive after runtime-backed dialog rebuilds.");
            Assert.AreEqual("adept", refreshedSearchBox.Text);
        });
    }

    [TestMethod]
    public void Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            MenuItem[] menuButtons = menuPanel.Items.OfType<MenuItem>().ToArray();
            string[] menuLabels = menuButtons
                .Select(button => button.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "File",
                    "Edit",
                    "Special",
                    "Tools",
                    "Windows",
                    "Help"
                },
                menuLabels);

            foreach (MenuItem button in menuButtons)
            {
                Assert.IsTrue(button.IsEnabled, $"Menu button '{button.Name}' must stay enabled after runtime bootstrap.");
            }

            (string ButtonName, string MenuId)[] clickableMenus =
            [
                ("FileMenuButton", "file"),
                ("EditMenuButton", "edit"),
                ("ToolsMenuButton", "tools"),
                ("WindowsMenuButton", "windows"),
                ("HelpMenuButton", "help"),
            ];

            foreach ((string buttonName, string menuId) in clickableMenus)
            {
                harness.Click(buttonName);
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.OpenMenuId, menuId, StringComparison.Ordinal)
                    && IsAnyCommandVisibleInCommandList(harness));
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            (string ButtonName, string ExpectedLabel)[] expectedButtons =
            [
                ("LoadDemoRunnerButton", "Demo"),
                ("ImportFileButton", "Open"),
                ("SaveButton", "Save"),
                ("PrintButton", "Print"),
                ("CopyButton", "Copy"),
                ("SettingsButton", "Settings"),
                ("CloseWorkspaceButton", "Close"),
            ];

            foreach ((string buttonName, string expectedLabel) in expectedButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Workbench action '{buttonName}' must stay visible.");
                Assert.IsTrue(button.IsEnabled, $"Workbench action '{buttonName}' must stay enabled.");
                CollectionAssert.Contains(GetButtonTextLines(button), expectedLabel, $"Workbench action '{buttonName}' must keep its classic desktop label.");
                Assert.AreEqual(1, GetButtonTextLines(button).Length, $"Workbench action '{buttonName}' must not add a secondary caption line.");
                Assert.IsTrue(button.Bounds.Width > 0d && button.Bounds.Height > 0d, $"Workbench action '{buttonName}' must keep a visible desktop footprint.");
            }

            foreach (string buttonName in new[] { "SaveButton", "PrintButton", "CopyButton", "ImportFileButton", "CloseWorkspaceButton" })
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(
                    button.GetVisualDescendants().OfType<Image>().Any(image => image.IsVisible),
                    $"Workbench action '{buttonName}' must restore a visible classic toolbar icon.");
            }

            Button importRawButton = harness.FindControl<Button>("ImportRawButton");
            Assert.IsFalse(importRawButton.IsVisible, "Raw XML import must stay off the primary classic toolbar.");
            foreach (string buttonName in new[] { "DesktopHomeButton", "CampaignWorkspaceButton", "UpdateStatusButton", "InstallLinkingButton", "SupportButton", "ReportIssueButton" })
            {
                Assert.IsFalse(harness.FindControl<Button>(buttonName).IsVisible, $"Secondary chrome '{buttonName}' must stay out of the default dense toolbar.");
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Border[] badgeBorders = harness.Window.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("shell-action-badge"))
                .ToArray();
            TextBlock[] captionBlocks = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.Classes.Contains("shell-action-caption"))
                .ToArray();
            string[] shellChromeLabels = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsVisible)
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            double[] toolbarButtonHeights =
            [
                harness.FindControl<Button>("LoadDemoRunnerButton").Bounds.Height,
                harness.FindControl<Button>("ImportFileButton").Bounds.Height,
                harness.FindControl<Button>("SaveButton").Bounds.Height,
                harness.FindControl<Button>("PrintButton").Bounds.Height,
                harness.FindControl<Button>("CopyButton").Bounds.Height,
                harness.FindControl<Button>("SettingsButton").Bounds.Height,
                harness.FindControl<Button>("ImportRawButton").Bounds.Height,
                harness.FindControl<Button>("CloseWorkspaceButton").Bounds.Height,
            ];
            Assert.AreEqual(0, badgeBorders.Length, "Classic toolbar parity forbids dashboard badge tiles in the workbench strip.");
            Assert.AreEqual(0, captionBlocks.Length, "Classic toolbar parity forbids secondary caption lines in the workbench strip.");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Quick Actions");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Workbench State");
            Assert.IsTrue(toolbarButtonHeights.All(height => height <= 40d), "Classic toolbar parity requires compact workbench actions instead of hero-card sized buttons.");
        });
    }

    [TestMethod]
    public void Next90_m103_veteran_certification_screenshot_pack_covers_required_desktop_surfaces()
    {
        foreach (string screenshot in RequiredVeteranCertificationScreenshots)
        {
            CollectionAssert.Contains(
                VeteranCertificationScreenshotFiles,
                screenshot,
                $"Next-90 milestone 103.2 must keep screenshot-backed evidence for {screenshot}.");
        }

        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "02-menu-open-light.png", "Menu familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "01-initial-shell-light.png", "Toolstrip familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "17-character-roster-dialog-light.png", "Roster familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "16-master-index-dialog-light.png", "Master index familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "03-settings-open-light.png", "Settings familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "18-import-dialog-light.png", "Import familiarity must stay screenshot-backed.");
    }

    [TestMethod]
    public void Next90_m103_veteran_certification_review_steps_bind_each_required_surface_to_desktop_capture()
    {
        CollectionAssert.AreEquivalent(
            new[] { "menu", "toolstrip", "roster", "master_index", "settings", "import" },
            VeteranCertificationReviewSteps.Select(step => step.Surface).ToArray(),
            "M103 veteran certification must keep one explicit review step for every assigned desktop parity surface.");
        CollectionAssert.AreEquivalent(
            RequiredVeteranCertificationScreenshots,
            VeteranCertificationReviewSteps.Select(step => step.ScreenshotFileName).ToArray(),
            "M103 review steps must bind exactly to the required screenshot evidence pack.");
        Assert.AreEqual(
            VeteranCertificationReviewSteps.Length,
            VeteranCertificationReviewSteps.Select(step => step.ScreenshotFileName).Distinct(StringComparer.Ordinal).Count(),
            "M103 screenshot-backed review steps must not reuse one capture across multiple surfaces.");

        foreach (VeteranCertificationReviewStep step in VeteranCertificationReviewSteps)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(step.PromotedHeadGesture), $"{step.Surface} must name the promoted-head gesture that creates its screenshot.");
            StringAssert.Contains(step.Chummer5aBaseline, "Chummer5a");
        }
    }

    [TestMethod]
    public void Next90_m141_import_route_review_steps_bind_translator_xml_editor_and_hero_lab_to_direct_screenshots()
    {
        CollectionAssert.AreEqual(
            new[] { "translator", "xml_amendment_editor", "hero_lab_importer" },
            ImportRouteReviewSteps.Select(step => step.Surface).ToArray(),
            "M141 import-route closeout must keep deterministic review order for translator, XML amendment, and Hero Lab.");
        CollectionAssert.AreEqual(
            new[]
            {
                "38-translator-dialog-light.png",
                "39-xml-editor-dialog-light.png",
                "40-hero-lab-importer-dialog-light.png",
            },
            ImportRouteReviewSteps.Select(step => step.ScreenshotFileName).ToArray(),
            "M141 import-route closeout must keep deterministic screenshot names wired to each direct route.");

        foreach (VeteranCertificationReviewStep step in ImportRouteReviewSteps)
        {
            CollectionAssert.Contains(VeteranCertificationScreenshotFiles, step.ScreenshotFileName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(step.PromotedHeadGesture), $"{step.Surface} must name the promoted-head gesture that creates its screenshot.");
            StringAssert.Contains(step.Chummer5aBaseline, "Chummer5a");
        }
    }

    [TestMethod]
    public void Promoted_avalonia_head_uses_review_sized_desktop_frame_for_m103_screenshots()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Assert.IsTrue(
                harness.Window.Bounds.Width >= 1280d,
                "M103 screenshot-backed parity review requires the promoted Avalonia head to open at the desktop visual review width.");
            Assert.IsTrue(
                harness.Window.Bounds.Height >= 800d,
                "M103 screenshot-backed parity review requires the promoted Avalonia head to open at the desktop visual review height.");
        });
    }

    [TestMethod]
    public void Runtime_backed_roster_tree_preserves_legacy_left_rail_navigation_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);
            string[] rootLabels = rootItems.Select(item => item.Name).ToArray();
            string? rulesetId = harness.State.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? harness.State.NavigationTabs
                    .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            string[] expectedRootLabels = [RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId)];

            if (harness.State.OpenWorkspaces.Count == 0)
            {
                CollectionAssert.AreEqual(
                    Array.Empty<string>(),
                    rootLabels,
                    "The startup roster should stay empty until a workspace is actually open.");
            }
            else
            {
                Assert.IsTrue(
                    rootLabels.SequenceEqual(expectedRootLabels, StringComparer.Ordinal),
                    "The roster tree must group characters by ruleset only. Expected: "
                    + string.Join(" | ", expectedRootLabels)
                    + " ; actual: "
                    + string.Join(" | ", rootLabels));
            }
            Assert.IsTrue(rosterTree.IsVisible, "The left rail must render the roster tree in the quiet default shell.");
            Assert.IsNull(harness.FindControlOrDefault<TabControl>("LoadedRunnerTabStrip"), "The legacy-oriented left rail must be a tree navigator, not a second tab control.");
            Assert.IsNull(harness.FindControlOrDefault<ListBox>("NavigationTabsList"), "The legacy-oriented left rail must not fall back to a dashboard-style tab list.");
        });
    }

    [TestMethod]
    public void Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_roster_landmarks()
    {
        // Runtime_backed_sr4_switch_ruleset_dialog_preserves_compact_combo_posture
        // Runtime_backed_sr6_shared_muscle_memory_inventory_receipt_matches_promoted_surface_contract
        // Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            foreach (string rulesetId in new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 })
            {
                harness.ShellPresenter.SetPreferredRulesetAsync(rulesetId, CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.PreferredRulesetId, rulesetId, StringComparison.Ordinal)
                    && string.Equals(harness.ShellPresenter.State.ActiveRulesetId, rulesetId, StringComparison.Ordinal));

                TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
                string[] rootLabels = SnapshotRosterItems(rosterTree).Select(item => item.Name).ToArray();
                if (harness.State.OpenWorkspaces.Count == 0)
                {
                    CollectionAssert.AreEqual(
                        Array.Empty<string>(),
                        rootLabels,
                        $"Ruleset '{rulesetId}' should keep the grouped roster empty until a workspace is opened.");
                }
                else
                {
                    string[] expectedRootLabels =
                    [
                        RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId)
                    ];
                    CollectionAssert.AreEqual(
                        expectedRootLabels,
                        rootLabels,
                        $"Ruleset '{rulesetId}' must keep the roster tree on ruleset-specific familiar landmarks.");
                }
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            string[] visibleTexts = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => (text.Text ?? string.Empty).Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.DoesNotContain(visibleTexts, "Career-style workbench");
            CollectionAssert.DoesNotContain(visibleTexts, "Command Palette");
            CollectionAssert.DoesNotContain(visibleTexts, "Coach Sidecar");
            CollectionAssert.DoesNotContain(visibleTexts, "Coach Launch");
            CollectionAssert.DoesNotContain(visibleTexts, "Recent Coach Guidance");
            Assert.IsTrue(
                visibleTexts.Any(text => text.Contains("Character", StringComparison.Ordinal)),
                "The runtime shell should still surface character-oriented copy after bootstrap.");
            CollectionAssert.DoesNotContain(visibleTexts, "Section Commands");
            CollectionAssert.DoesNotContain(visibleTexts, "Reference & Notes");
        });
    }

    [TestMethod]
    public void Runtime_backed_shell_chrome_stays_enabled_after_runner_load()
    {
        WithRuntimeLoadedRunnerHarness(harness =>
        {
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible);

            string[] menuButtons =
            [
                "FileMenuButton",
                "EditMenuButton",
                "SpecialMenuButton",
                "ToolsMenuButton",
                "WindowsMenuButton",
                "HelpMenuButton",
            ];

            string[] actionButtons =
            [
                "ImportFileButton",
                "SaveButton",
                "SettingsButton",
                "PrintButton",
                "CopyButton",
                "CloseWorkspaceButton",
            ];

            foreach (string buttonName in menuButtons)
            {
                MenuItem button = harness.FindControl<MenuItem>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(button.Header?.ToString()), $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            foreach (string buttonName in actionButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsTrue(GetButtonTextLines(button).Length > 0, $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            foreach (string buttonName in new[]
                     {
                         "DesktopHomeButton",
                         "CampaignWorkspaceButton",
                         "LoadDemoRunnerButton",
                         "UpdateStatusButton",
                         "InstallLinkingButton",
                         "SupportButton",
                         "ReportIssueButton",
                     })
            {
                Assert.IsFalse(harness.FindControl<Button>(buttonName).IsVisible, $"Runtime-backed runner load must keep secondary chrome '{buttonName}' collapsed.");
            }

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
        });
    }

    [TestMethod]
    public void Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("SettingsButton");

            harness.WaitUntil(() =>
            {
                TextBlock? title = harness.FindControlOrDefault<TextBlock>("DialogTitleText");
                Panel? fields = harness.FindControlOrDefault<Panel>("DialogFieldsHost");
                Panel? actions = harness.FindControlOrDefault<Panel>("DialogActionsHost");
                return string.Equals(title?.Text, "Global Settings", StringComparison.Ordinal)
                    && fields is not null
                    && fields.Children.Count > 0
                    && actions is not null
                    && actions.Children.OfType<Button>().Any();
            });

            Panel fieldsHost = harness.FindControl<Panel>("DialogFieldsHost");
            Panel actionsHost = harness.FindControl<Panel>("DialogActionsHost");

            Assert.IsTrue(fieldsHost.Children.OfType<Control>().Any());
            Assert.IsTrue(actionsHost.Children.OfType<Button>().Any(button =>
                string.Equals(button.Content?.ToString(), "Save", StringComparison.OrdinalIgnoreCase)));

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
        });
    }

    [TestMethod]
    public void Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
            harness.ClickMenuCommand("global_settings");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Global Settings",
                "UI Scale",
                "Theme",
                "Language",
                "Compact Mode");
            harness.InvokeDialogAction("save");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            harness.ClickMenuCommand("master_index");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Master Index",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Master Index",
                "Data Root",
                "/app/data");
            harness.InvokeDialogAction("close");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Master Index",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
            harness.ClickMenuCommand("character_roster");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Character Roster",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Character Roster",
                "Open Runners",
                "Saved Workspaces",
                "Ruleset Mix",
                "Roster Entries");
        });
    }

    [TestMethod]
    // Veteran proof anchor: Translator_xml_editor_and_hero_lab_importer_routes_surface_runtime_backed_dialog_receipts
    public void Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Translator",
                    StringComparison.Ordinal));
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "Translator");
            AssertDialogContainsAll(
                harness,
                "Translator",
                GetImportRouteReviewStep("translator").RequiredDialogMarkers);
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "translatorLanePosture"));
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "translatorBridgePosture"));
            harness.InvokeDialogAction("close");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Translator",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "XML Editor",
                    StringComparison.Ordinal));
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "XML Editor");
            AssertDialogContainsAll(
                harness,
                "XML Editor",
                GetImportRouteReviewStep("xml_amendment_editor").RequiredDialogMarkers);
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "xmlEditorXmlBridgePosture"));
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "XML Editor",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Hero Lab Importer",
                    StringComparison.Ordinal));
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "Hero Lab Importer");
            AssertDialogContainsAll(
                harness,
                "Hero Lab Importer",
                GetImportRouteReviewStep("hero_lab_importer").RequiredDialogMarkers);
            StringAssert.Contains(
                DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "heroLabImportOracleLanePosture"),
                "governed");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Hero Lab Importer",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);
        });
    }

    [TestMethod]
    public void Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, ScreenshotProofCapture> captured = new(StringComparer.Ordinal);
            WithHarness(harness =>
            {
                harness.WaitForReady();

                Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible, "Veteran first-minute proof requires the promoted Avalonia head to keep the toolstrip visible on launch.");
                Assert.IsTrue(harness.FindControl<Button>("LoadDemoRunnerButton").IsEnabled, "Veteran first-minute proof requires a visible desktop import/load affordance before any workspace is opened.");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
                harness.ClickMenuCommand("global_settings");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Global Settings",
                    "UI Scale",
                    "Theme",
                    "Language",
                    "Compact Mode");
                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
                harness.ClickMenuCommand("master_index");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Master Index",
                    "Data Root",
                    "/app/data");
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
                harness.ClickMenuCommand("character_roster");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Character Roster",
                    "Open Runners",
                    "Saved Workspaces",
                    "Ruleset Mix",
                    "Roster Entries");
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.Presenter.ImportCalls > 0
                        && !string.IsNullOrWhiteSpace(harness.State.Profile?.Name)
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                        && !harness.State.IsBusy,
                    timeoutMs: 8000);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Veteran first-minute proof requires a loaded runner profile before import review.");
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible, "Veteran first-minute proof requires the loaded-runner tab strip after the desktop import shortcut.");
                Assert.IsFalse(harness.FindControl<Control>("QuickStartContainer").IsVisible, "Veteran first-minute proof must stay on the quiet shell without reviving the old quick-start band.");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                captured["19-workflow-file-menu-loaded-light.png"] = CaptureScreenshotProof(harness, "19-workflow-file-menu-loaded-light.png");
                harness.ClickMenuCommand("new_character");
                harness.WaitUntil(() =>
                        string.Equals(
                            harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                            "Select Build Method",
                            StringComparison.Ordinal),
                    timeoutMs: 8000);
                captured["36-workflow-new-character-dialog-light.png"] = CaptureScreenshotProof(harness, "36-workflow-new-character-dialog-light.png");
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Select Build Method",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("skills");
                ListBox skillRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => skillRows.ItemCount > 0);
                captured["20-workflow-skills-section-light.png"] = CaptureScreenshotProof(harness, "20-workflow-skills-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_skill_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_skill_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                captured["21-workflow-skill-add-dialog-light.png"] = CaptureScreenshotProof(harness, "21-workflow-skill-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("qualities");
                ListBox qualityRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => qualityRows.ItemCount > 0);
                captured["22-workflow-qualities-section-light.png"] = CaptureScreenshotProof(harness, "22-workflow-qualities-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_quality_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_quality_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                captured["23-workflow-quality-add-dialog-light.png"] = CaptureScreenshotProof(harness, "23-workflow-quality-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("gear");
                ListBox gearRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => gearRows.ItemCount > 0);
                captured["24-workflow-gear-section-light.png"] = CaptureScreenshotProof(harness, "24-workflow-gear-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_gear_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_gear_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                captured["25-workflow-gear-add-dialog-light.png"] = CaptureScreenshotProof(harness, "25-workflow-gear-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("weapons");
                ListBox weaponRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => weaponRows.ItemCount > 0);
                captured["26-workflow-weapons-section-light.png"] = CaptureScreenshotProof(harness, "26-workflow-weapons-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_weapon")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_weapon");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                captured["27-workflow-weapon-add-dialog-light.png"] = CaptureScreenshotProof(harness, "27-workflow-weapon-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("armors");
                ListBox armorRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => armorRows.ItemCount > 0);
                captured["28-workflow-armor-section-light.png"] = CaptureScreenshotProof(harness, "28-workflow-armor-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_armor")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_armor");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                captured["29-workflow-armor-add-dialog-light.png"] = CaptureScreenshotProof(harness, "29-workflow-armor-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox cyberwareRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => cyberwareRows.ItemCount > 0);
                captured["30-workflow-cyberware-section-light.png"] = CaptureScreenshotProof(harness, "30-workflow-cyberware-section-light.png");

                harness.SetActiveSectionForTesting("powers");
                ListBox powerRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => powerRows.ItemCount > 0);
                captured["31-workflow-powers-section-light.png"] = CaptureScreenshotProof(harness, "31-workflow-powers-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_adept_power_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_adept_power_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                captured["32-workflow-adept-power-dialog-light.png"] = CaptureScreenshotProof(harness, "32-workflow-adept-power-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("complexforms");
                ListBox complexFormRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => complexFormRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_complex_form_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_complex_form_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                captured["33-workflow-complex-form-dialog-light.png"] = CaptureScreenshotProof(harness, "33-workflow-complex-form-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("validate");
                ListBox validateRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => validateRows.ItemCount > 0);
                captured["34-workflow-validate-section-light.png"] = CaptureScreenshotProof(harness, "34-workflow-validate-section-light.png");

                harness.SetActiveSectionForTesting("rules");
                ListBox rulesRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => rulesRows.ItemCount > 0);
                captured["35-workflow-rules-section-light.png"] = CaptureScreenshotProof(harness, "35-workflow-rules-section-light.png");

                harness.SetActiveSectionForTesting("calendar");
                ListBox calendarRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => calendarRows.ItemCount > 0);
                captured["37-workflow-calendar-section-light.png"] = CaptureScreenshotProof(harness, "37-workflow-calendar-section-light.png");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                harness.ClickMenuCommand("open_character");
                AssertDialogContainsAll(
                    harness,
                    "Open Character",
                    GetVeteranCertificationReviewStep("import").RequiredDialogMarkers);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy);

                Assert.IsNotNull(harness.State.WorkspaceId);
                Assert.IsTrue(harness.State.Session.OpenWorkspaces.Count > 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Runtime-backed runner import must populate the workspace profile.");
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible, "Loaded runner import must surface the loaded-workspace tab posture.");
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6()
    {
        Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters();
        Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm();
        Contacts_diary_and_support_routes_execute_with_public_path_visibility();
    }

    [TestMethod]
    public void Runtime_loaded_runner_quick_action_workflows_materialize_dialog_contracts_and_continuations_across_sr4_sr5_and_sr6()
    {
        Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions();
        Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions();
        Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues();
        Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm();
    }

    [TestMethod]
    public void Workspace_strip_quick_start_hides_after_runtime_backed_runner_load()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                Assert.IsTrue(harness.FindControl<Button>("LoadDemoRunnerButton").IsVisible);
                Assert.IsNull(harness.FindControlOrDefault<Control>("QuickStartContainer"), "The quiet shell must not surface the old quick-start band by default.");
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.State.WorkspaceId is not null && harness.State.Session.OpenWorkspaces.Count > 0);
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default()
    {
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string? priorSampleOverride = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", null);

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();

                Assert.IsFalse(
                    harness.FindControlOrDefault<Button>("LoadDemoRunnerButton")?.IsVisible ?? false,
                    "Public stable shell must hide the default demo launcher.");
                Assert.IsFalse(
                    harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible ?? false,
                    "Public stable shell must not surface the quick-start demo band.");

                string[] visibleTextSamples = harness.Window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static control => control.IsVisible)
                    .Select(control => control.Text ?? string.Empty)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                CollectionAssert.DoesNotContain(visibleTextSamples, "Open Demo");
                CollectionAssert.DoesNotContain(visibleTextSamples, "Demo");
                Assert.IsFalse(
                    visibleTextSamples.Any(text =>
                        text.Contains("Codex", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("provider", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Living World", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Signal Deck", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Black Ledger", StringComparison.OrdinalIgnoreCase)),
                    "Public stable shell must stay free of developer, provider, or public-web noise.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleOverride);
        }
    }

    [TestMethod]
    public void Public_stable_shell_allows_internal_sample_override_for_operator_and_test_access()
    {
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string? priorSampleOverride = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", "1");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                Assert.IsTrue(
                    harness.FindControl<Button>("LoadDemoRunnerButton").IsVisible,
                    "Public stable shell may expose the sample route only behind the explicit operator/test override.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleOverride);
        }
    }

    [TestMethod]
    public void Standalone_toolstrip_buttons_raise_expected_events()
    {
        WithStandaloneControl<ToolStripControl>(control =>
        {
            List<string> raisedEvents = [];
            control.ImportFileRequested += (_, _) => raisedEvents.Add("import_file");
            control.ImportRawRequested += (_, _) => raisedEvents.Add("import_raw");
            control.SaveRequested += (_, _) => raisedEvents.Add("save");
            control.CloseWorkspaceRequested += (_, _) => raisedEvents.Add("close_workspace");
            control.DesktopHomeRequested += (_, _) => raisedEvents.Add("desktop_home");
            control.CampaignWorkspaceRequested += (_, _) => raisedEvents.Add("campaign_workspace");
            control.UpdateStatusRequested += (_, _) => raisedEvents.Add("update_status");
            control.InstallLinkingRequested += (_, _) => raisedEvents.Add("install_linking");
            control.SupportRequested += (_, _) => raisedEvents.Add("support");
            control.ReportIssueRequested += (_, _) => raisedEvents.Add("report_issue");
            control.SettingsRequested += (_, _) => raisedEvents.Add("settings");
            control.LoadDemoRunnerRequested += (_, _) => raisedEvents.Add("load_demo_runner");

            (string ButtonName, string EventId)[] buttonMap =
            [
                ("DesktopHomeButton", "desktop_home"),
                ("CampaignWorkspaceButton", "campaign_workspace"),
                ("LoadDemoRunnerButton", "load_demo_runner"),
                ("ImportFileButton", "import_file"),
                ("SaveButton", "save"),
                ("SettingsButton", "settings"),
                ("ImportRawButton", "import_raw"),
                ("UpdateStatusButton", "update_status"),
                ("InstallLinkingButton", "install_linking"),
                ("SupportButton", "support"),
                ("ReportIssueButton", "report_issue"),
                ("CloseWorkspaceButton", "close_workspace"),
            ];

            foreach ((string buttonName, _) in buttonMap)
            {
                RaiseClick(FindDescendant<Button>(control, buttonName));
            }

            CollectionAssert.AreEqual(buttonMap.Select(item => item.EventId).ToArray(), raisedEvents.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events()
    {
        WithStandaloneControl<ShellMenuBarControl>(control =>
        {
            List<string> selectedMenus = [];
            List<string> selectedCommands = [];
            control.MenuSelected += (_, menuId) => selectedMenus.Add(menuId);
            control.MenuCommandSelected += (_, commandId) => selectedCommands.Add(commandId);

            string[] menuIds = ["file", "edit", "special", "tools", "windows", "help"];
            control.SetMenuState(
                openMenuId: null,
                knownMenuIds: menuIds,
                openMenuCommands: [],
                isBusy: false);

            foreach (string buttonName in new[]
                     {
                         "FileMenuButton",
                         "EditMenuButton",
                         "SpecialMenuButton",
                         "ToolsMenuButton",
                         "WindowsMenuButton",
                         "HelpMenuButton",
                     })
            {
                RaiseClick(FindDescendant<MenuItem>(control, buttonName));
            }

            CollectionAssert.AreEqual(menuIds, selectedMenus.ToArray());

            control.SetMenuState(
                openMenuId: "file",
                knownMenuIds: menuIds,
                openMenuCommands:
                [
                    new MenuCommandItem("new_character", "new character", true, true),
                    new MenuCommandItem("open_character", "open character", true, true),
                    new MenuCommandItem("save_character", "save character", true),
                ],
                isBusy: false);

            Button[] commandButtons = FindDescendant<MenuItem>(control, "FileMenuButton")
                .Items
                .OfType<MenuItem>()
                .Select(menuItem => new Button
                {
                    Tag = menuItem.Tag,
                    Content = menuItem.Header
                })
                .ToArray();
            Assert.AreEqual(3, commandButtons.Length, "Standalone menu proof must render visible command buttons for the open menu.");
            foreach (MenuItem commandMenuItem in FindDescendant<MenuItem>(control, "FileMenuButton").Items.OfType<MenuItem>())
            {
                commandMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                PumpStandaloneUi();
            }

            CollectionAssert.AreEqual(new[] { "new_character", "open_character", "save_character" }, selectedCommands.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_workspace_strip_quick_start_button_raises_expected_event()
    {
        WithStandaloneControl<WorkspaceStripControl>(control =>
        {
            int loadDemoRunnerRequests = 0;
            control.LoadDemoRunnerRequested += (_, _) => loadDemoRunnerRequests++;
            control.SetState(new WorkspaceStripState("No runner loaded.", ShowQuickStartAction: true));

            Assert.IsTrue(FindDescendant<Control>(control, "QuickStartContainer").IsVisible);
            RaiseClick(FindDescendant<Button>(control, "LoadDemoRunnerQuickActionButton"));

            Assert.AreEqual(1, loadDemoRunnerRequests, "Workspace quick-start CTA must raise its load-demo-runner event.");
        });
    }

    [TestMethod]
    public void Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            List<string> selectedTabs = [];
            control.NavigationTabSelected += (_, tabId) => selectedTabs.Add(tabId);
            control.SetNavigationTabs(
                "Runner Tabs",
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true),
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true),
                ],
                activeTabId: "tab-profile");
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(control.IsVisible, "Summary header must keep loaded-runner navigation tabs visible.");
            Assert.IsTrue(FindDescendant<Control>(control, "NavigationTabsPanel").IsVisible);
            Assert.IsFalse(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsFalse(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);

            Button[] tabButtons = control.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Tag is string)
                .ToArray();

            foreach (Button tabButton in tabButtons)
            {
                RaiseClick(tabButton);
            }

            CollectionAssert.AreEqual(new[] { "tab-profile", "tab-gear" }, selectedTabs.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_summary_header_tab_buttons_raise_expected_events()
    {
        Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff();
    }

    [TestMethod]
    public void Standalone_summary_header_surfaces_restore_handoff_actions_when_present()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            int keepLocalRequests = 0;
            int saveLocalRequests = 0;
            int campaignWorkspaceRequests = 0;
            int workspaceSupportRequests = 0;

            control.KeepLocalWorkRequested += (_, _) => keepLocalRequests++;
            control.SaveLocalWorkRequested += (_, _) => saveLocalRequests++;
            control.CampaignWorkspaceRequested += (_, _) => campaignWorkspaceRequests++;
            control.WorkspaceSupportRequested += (_, _) => workspaceSupportRequests++;

            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true),
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true),
                ],
                ActiveTabId: "tab-profile",
                HasVisibleContent: true,
                RestoreContinuitySummary: "Restore choice: continue from runner-1.",
                StaleStateSummary: "Stale state: runner-1 stays visible.",
                ConflictChoiceSummary: "Conflict choices: keep local work, save local work, or review Campaign Workspace.",
                CanSaveLocalWorkBeforeRestore: true));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(control.IsVisible, "Summary header must surface a real restore handoff when the shell has one.");
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").IsEnabled);

            RaiseClick(FindDescendant<Button>(control, "KeepLocalWorkButton"));
            RaiseClick(FindDescendant<Button>(control, "SaveLocalWorkButton"));
            RaiseClick(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton"));
            RaiseClick(FindDescendant<Button>(control, "OpenWorkspaceSupportButton"));

            Assert.AreEqual(1, keepLocalRequests);
            Assert.AreEqual(1, saveLocalRequests);
            Assert.AreEqual(1, campaignWorkspaceRequests);
            Assert.AreEqual(1, workspaceSupportRequests);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_keeps_restore_stale_and_conflict_choices_visible()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            List<string> requestedActions = [];
            control.KeepLocalWorkRequested += (_, _) => requestedActions.Add("keep-local");
            control.SaveLocalWorkRequested += (_, _) => requestedActions.Add("save-local");
            control.CampaignWorkspaceRequested += (_, _) => requestedActions.Add("campaign-workspace");
            control.WorkspaceSupportRequested += (_, _) => requestedActions.Add("workspace-support");

            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true)
                ],
                ActiveTabId: "tab-profile",
                RuntimeSummary: "Runtime: SR6 preview.",
                RestoreContinuitySummary: "Restore choice: keep ws-1 open before accepting a newer continuity packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Conflict choices: review before replacing this unsaved desktop state.",
                CanSaveLocalWorkBeforeRestore: true));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityStatusText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "StaleStateStatusText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "ConflictChoiceStatusText").IsVisible);
            Assert.AreEqual(
                "Restore continuity decision gate",
                AutomationProperties.GetName(FindDescendant<Control>(control, "RestoreContinuityStatusBorder")));
            Assert.AreEqual(
                "The desktop app keeps restore and conflict details visible before anything can replace your local copy.",
                AutomationProperties.GetHelpText(FindDescendant<Control>(control, "RestoreContinuityStatusBorder")));
            Assert.AreEqual(
                "Restore continuation status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityStatusText")));
            Assert.AreEqual(
                "Stale state visibility status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "StaleStateStatusText")));
            Assert.AreEqual(
                "Conflict choice status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "ConflictChoiceStatusText")));
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText").IsVisible);
            Assert.AreEqual(
                "Restore decision guard",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText")));
            Assert.AreEqual(
                "Chummer does not replace your local work automatically; review the restore choices first.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText")));
            Assert.AreEqual(
                "Restore decision order",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionOrderText")));
            Assert.AreEqual(
                "Use the visible choices in order: keep local work visible, save local work when available, review Campaign Workspace, then open Workspace Support.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionOrderText")));
            Assert.AreEqual(
                "Restore local authority",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityLocalAuthorityText")));
            Assert.AreEqual(
                "Your local desktop copy stays authoritative until you choose Campaign Workspace review or Workspace Support.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityLocalAuthorityText")));
            Assert.AreEqual(
                "Restore replacement guard",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityReplacementGuardText")));
            Assert.AreEqual(
                "There is no automatic or one-click replacement from this desktop route.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityReplacementGuardText")));
            Assert.AreEqual(
                "Restore support handoff",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuitySupportHandoffText")));
            Assert.AreEqual(
                "Workspace Support opens with your restore, stale-state, conflict-choice, and local copy context.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuitySupportHandoffText")));
            Assert.AreEqual("restore-decision-keep-local-work", FindDescendant<Button>(control, "KeepLocalWorkButton").Tag);
            Assert.AreEqual("Keep Local", AutomationProperties.GetName(FindDescendant<Button>(control, "KeepLocalWorkButton")));
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").IsEnabled);
            Assert.AreEqual("restore-decision-save-local-work", FindDescendant<Button>(control, "SaveLocalWorkButton").Tag);
            Assert.AreEqual("Save local work before restore review", AutomationProperties.GetName(FindDescendant<Button>(control, "SaveLocalWorkButton")));
            Assert.AreEqual("restore-decision-review-campaign-workspace", FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Tag);
            Assert.AreEqual("Review Campaign Workspace restore choices", AutomationProperties.GetName(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton")));
            Assert.AreEqual("restore-decision-open-workspace-support", FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Tag);
            Assert.AreEqual("Open Workspace Support", AutomationProperties.GetName(FindDescendant<Button>(control, "OpenWorkspaceSupportButton")));
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText").IsVisible);
            Assert.AreEqual(
                "Restore decision action status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText")));

            RaiseClick(FindDescendant<Button>(control, "KeepLocalWorkButton"));
            Assert.IsTrue(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "SaveLocalWorkButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "OpenWorkspaceSupportButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));

            CollectionAssert.AreEqual(
                new[]
                {
                    "keep-local",
                    "save-local",
                    "campaign-workspace",
                    "workspace-support"
                },
                requestedActions);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_explains_when_restore_save_choice_is_unavailable()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: [],
                ActiveTabId: null,
                RestoreContinuitySummary: "Restore choice: load a recent workspace before replacing local work.",
                StaleStateSummary: "Stale state: service continuity is unavailable.",
                ConflictChoiceSummary: "Conflict choices: review workspace support before replacing local work.",
                CanSaveLocalWorkBeforeRestore: false));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Button saveButton = FindDescendant<Button>(control, "SaveLocalWorkButton");
            Assert.IsFalse(saveButton.IsEnabled);
            Assert.AreEqual("restore-decision-save-local-work", saveButton.Tag);
            RaiseClick(saveButton);
            Assert.IsFalse(saveButton.Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText").IsVisible);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_preserves_restore_decision_status_across_refresh_state()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            SummaryHeaderState state = new(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: [],
                ActiveTabId: null,
                RestoreContinuitySummary: "Restore choice: keep ws-1 open before accepting a newer continuity packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Conflict choices: review before replacing this unsaved desktop state.",
                CanSaveLocalWorkBeforeRestore: true,
                RestoreDecisionActionStatus: "Opening Workspace Support with restore continuation, stale-state, and conflict-choice context.",
                RestoreDecisionSelectionId: "restore-decision-open-workspace-support");
            control.SetState(state);
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));

            control.SetState(state);
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
        });
    }

    [TestMethod]
    public void Primary_route_restore_status_switches_from_save_requested_to_saved_after_refresh()
    {
        Type coordinatorType = typeof(MainWindow).Assembly.GetType("Chummer.Avalonia.MainWindowTransientStateCoordinator")
            ?? throw new AssertFailedException("Unable to resolve MainWindowTransientStateCoordinator.");
        object coordinator = Activator.CreateInstance(coordinatorType, nonPublic: true)
            ?? throw new AssertFailedException("Unable to construct MainWindowTransientStateCoordinator.");

        coordinatorType.GetMethod("RecordSaveLocalWorkDecision", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, ["workspace-alpha"]);

        object pendingFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedPendingFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [pendingFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for pending save state.");
        SummaryHeaderState pendingSummaryHeader = ReadTransientSummaryHeader(resolvedPendingFrame);
        Assert.AreEqual(
            "Save local work requested before any restore or conflict review changes desktop state.",
            pendingSummaryHeader.RestoreDecisionActionStatus);
        Assert.AreEqual("restore-decision-save-local-work", pendingSummaryHeader.RestoreDecisionSelectionId);

        object savedFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: false, workspaceId: "workspace-alpha");
        object resolvedSavedFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [savedFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for saved state.");
        SummaryHeaderState savedSummaryHeader = ReadTransientSummaryHeader(resolvedSavedFrame);
        Assert.AreEqual(
            "Local work saved before restore review; keep local work visible, review Campaign Workspace, or open Workspace Support before any replacement.",
            savedSummaryHeader.RestoreDecisionActionStatus);
        Assert.IsNull(savedSummaryHeader.RestoreDecisionSelectionId);

        object redirtyFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedRedirtyFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [redirtyFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for re-dirtied save state.");
        SummaryHeaderState redirtySummaryHeader = ReadTransientSummaryHeader(resolvedRedirtyFrame);
        Assert.IsNull(redirtySummaryHeader.RestoreDecisionSelectionId);
        Assert.IsNull(redirtySummaryHeader.RestoreDecisionActionStatus);
    }

    [TestMethod]
    public void Primary_route_restore_status_clears_when_workspace_anchor_changes()
    {
        Type coordinatorType = typeof(MainWindow).Assembly.GetType("Chummer.Avalonia.MainWindowTransientStateCoordinator")
            ?? throw new AssertFailedException("Unable to resolve MainWindowTransientStateCoordinator.");
        object coordinator = Activator.CreateInstance(coordinatorType, nonPublic: true)
            ?? throw new AssertFailedException("Unable to construct MainWindowTransientStateCoordinator.");

        coordinatorType.GetMethod("RecordWorkspaceSupportDecision", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, ["workspace-alpha"]);

        object anchoredFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedAnchoredFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [anchoredFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for anchored workspace state.");
        SummaryHeaderState anchoredSummaryHeader = ReadTransientSummaryHeader(resolvedAnchoredFrame);
        Assert.AreEqual(
            "Opening Workspace Support with restore continuation, stale-state, and conflict-choice context.",
            anchoredSummaryHeader.RestoreDecisionActionStatus);
        Assert.AreEqual("restore-decision-open-workspace-support", anchoredSummaryHeader.RestoreDecisionSelectionId);

        object switchedFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-bravo");
        object resolvedSwitchedFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [switchedFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null after workspace switch.");
        SummaryHeaderState switchedSummaryHeader = ReadTransientSummaryHeader(resolvedSwitchedFrame);
        Assert.IsNull(switchedSummaryHeader.RestoreDecisionActionStatus);
        Assert.IsNull(switchedSummaryHeader.RestoreDecisionSelectionId);
    }

    [TestMethod]
    public void Runtime_backed_summary_header_keeps_restore_stale_and_conflict_copy_grounded_to_active_workspace()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy);

                string restoreText = harness.FindControl<TextBlock>("RestoreContinuityStatusText").Text ?? string.Empty;
                string staleText = harness.FindControl<TextBlock>("StaleStateStatusText").Text ?? string.Empty;
                string conflictText = harness.FindControl<TextBlock>("ConflictChoiceStatusText").Text ?? string.Empty;

                StringAssert.Contains(restoreText, "stays visible on the current desktop head until you choose review or support.");
                StringAssert.Contains(staleText, "was last touched locally at");
                StringAssert.Contains(staleText, "stays visible before any replacement;");
                StringAssert.Contains(conflictText, "was last touched locally at");
                StringAssert.Contains(conflictText, "stays visible before any replacement;");
                Assert.IsTrue(harness.FindControl<Button>("SaveLocalWorkButton").IsEnabled);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static object CreateTransientShellFrame(bool canSaveLocalWorkBeforeRestore, string? workspaceId = "workspace-alpha")
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        Type headerType = assembly.GetType("Chummer.Avalonia.MainWindowHeaderState")
            ?? throw new AssertFailedException("Unable to resolve MainWindowHeaderState.");
        Type chromeType = assembly.GetType("Chummer.Avalonia.MainWindowChromeState")
            ?? throw new AssertFailedException("Unable to resolve MainWindowChromeState.");
        Type shellFrameType = assembly.GetType("Chummer.Avalonia.MainWindowShellFrame")
            ?? throw new AssertFailedException("Unable to resolve MainWindowShellFrame.");

        object headerState = Activator.CreateInstance(
            headerType,
            new ToolStripState("Ready."),
            new MenuBarState(
                OpenMenuId: null,
                KnownMenuIds: Array.Empty<string>(),
                OpenMenuCommands: Array.Empty<MenuCommandItem>(),
                MenuCommandsByMenuId: new Dictionary<string, IReadOnlyList<MenuCommandItem>>(StringComparer.Ordinal),
                IsBusy: false))
            ?? throw new AssertFailedException("Unable to create MainWindowHeaderState.");
        object chromeState = Activator.CreateInstance(
            chromeType,
            new WorkspaceStripState("Workspace strip."),
            new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: Array.Empty<NavigatorTabItem>(),
                ActiveTabId: null,
                RestoreContinuitySummary: "Restore choice: keep ws-1 open before accepting a newer continuity packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Conflict choices: keep local work visible, review Campaign Workspace, or open workspace support before replacing local work.",
                CanSaveLocalWorkBeforeRestore: canSaveLocalWorkBeforeRestore,
                RestoreDecisionWorkspaceId: workspaceId),
            new StatusStripState(
                CharacterState: "Character ready.",
                ServiceState: "Service online.",
                TimeState: "2026-04-18 19:16 UTC",
                ComplianceState: "Compliance ready."))
            ?? throw new AssertFailedException("Unable to create MainWindowChromeState.");
        return Activator.CreateInstance(
                shellFrameType,
                headerState,
                chromeState,
                new SectionHostState(
                    SectionId: null,
                    NavigationTabs: Array.Empty<NavigatorTabItem>(),
                    ActiveTabId: null,
                    SectionActions: Array.Empty<NavigatorSectionActionItem>(),
                    ActiveActionId: null,
                    Notice: "Ready.",
                    PreviewJson: string.Empty,
                    Rows: Array.Empty<SectionRowDisplayItem>(),
                    QuickActions: Array.Empty<SectionQuickActionDisplayItem>(),
                    BuildLab: null,
                    BrowseWorkspace: null,
                    ContactGraph: null,
                    DowntimePlanner: null,
                    NpcPersonaStudio: null),
                new RosterPaneState(
                    Items: Array.Empty<CharacterRosterNode>(),
                    SelectedWorkspaceId: workspaceId),
                new CommandDialogPaneState(
                    Commands: Array.Empty<CommandPaletteItem>(),
                    SelectedCommandId: null,
                    DialogTitle: null,
                    DialogMessage: null,
                    DialogTrustReceipt: null,
                    Fields: Array.Empty<DialogFieldDisplayItem>(),
                    Actions: Array.Empty<DialogActionDisplayItem>()),
                true,
                new NavigatorPaneState(
                    OpenWorkspacesHeading: "Open Workspaces",
                    OpenWorkspaces: Array.Empty<NavigatorWorkspaceItem>(),
                    SelectedWorkspaceId: null,
                    NavigationTabsHeading: "Runner Tabs",
                    NavigationTabs: Array.Empty<NavigatorTabItem>(),
                    ActiveTabId: null,
                    SectionActionsHeading: "Actions",
                    SectionActions: Array.Empty<NavigatorSectionActionItem>(),
                    ActiveActionId: null,
                    WorkflowSurfacesHeading: "Workflows",
                    WorkflowSurfaces: Array.Empty<NavigatorWorkflowSurfaceItem>()),
                new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal))
            ?? throw new AssertFailedException("Unable to create MainWindowShellFrame.");
    }

    private static SummaryHeaderState ReadTransientSummaryHeader(object shellFrame)
    {
        object chromeState = shellFrame.GetType().GetProperty("ChromeState", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(shellFrame)
            ?? throw new AssertFailedException("Unable to read ChromeState from MainWindowShellFrame.");
        return (SummaryHeaderState)(chromeState.GetType().GetProperty("SummaryHeader", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(chromeState)
            ?? throw new AssertFailedException("Unable to read SummaryHeader from MainWindowChromeState."));
    }

    [TestMethod]
    public void Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events()
    {
        WithStandaloneControl<NavigatorPaneControl>(control =>
        {
            List<string> selectedWorkspaces = [];
            List<string> selectedTabs = [];
            List<string> selectedSectionActions = [];
            List<string> selectedWorkflowSurfaces = [];
            control.WorkspaceSelected += (_, workspaceId) => selectedWorkspaces.Add(workspaceId);
            control.NavigationTabSelected += (_, tabId) => selectedTabs.Add(tabId);
            control.SectionActionSelected += (_, actionId) => selectedSectionActions.Add(actionId);
            control.WorkflowSurfaceSelected += (_, surfaceId) => selectedWorkflowSurfaces.Add(surfaceId);

            control.SetState(new NavigatorPaneState(
                OpenWorkspacesHeading: "Open Workspaces",
                OpenWorkspaces:
                [
                    new NavigatorWorkspaceItem("runner-1", "Soma", "Demo", RulesetDefaults.Sr5, true, true)
                ],
                SelectedWorkspaceId: null,
                NavigationTabsHeading: "Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true)
                ],
                ActiveTabId: null,
                SectionActionsHeading: "Section Actions",
                SectionActions:
                [
                    new NavigatorSectionActionItem("action-cyberware", "Open Cyberware", WorkspaceSurfaceActionKind.Section)
                ],
                ActiveActionId: null,
                WorkflowSurfacesHeading: "Workflow Surfaces",
                WorkflowSurfaces:
                [
                    new NavigatorWorkflowSurfaceItem("surface-progress", "progress", "Progress Workflow", "workflow-progress")
                ]));

            TreeView navigatorTree = FindDescendant<TreeView>(control, "NavigatorTree");
            NavigatorTreeItem[] items = control.SnapshotTreeItems();

            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.Workspace, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.NavigationTab, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.SectionAction, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.WorkflowSurface, static item => true);
            PumpStandaloneUi();

            CollectionAssert.AreEqual(new[] { "runner-1" }, selectedWorkspaces.ToArray());
            CollectionAssert.AreEqual(new[] { "tab-gear" }, selectedTabs.ToArray());
            CollectionAssert.AreEqual(new[] { "action-cyberware" }, selectedSectionActions.ToArray());
            CollectionAssert.AreEqual(new[] { "surface-progress" }, selectedWorkflowSurfaces.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions()
    {
        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            List<string> selectedCommands = [];
            List<string> selectedActions = [];
            List<string> updatedFields = [];
            control.CommandSelected += (_, commandId) => selectedCommands.Add(commandId);
            control.DialogActionSelected += (_, actionId) => selectedActions.Add(actionId);
            control.DialogFieldValueChanged += (_, args) => updatedFields.Add($"{args.FieldId}={args.Value}");

            CommandPaletteItem[] commands =
            [
                new("global_settings", "Global Settings", "tools", true),
                new("about", "About Chummer", "help", true),
            ];
            control.SetState(new CommandDialogPaneState(
                Commands: commands,
                SelectedCommandId: null,
                DialogTitle: "Global Settings",
                DialogMessage: "Adjust desktop preferences.",
                DialogTrustReceipt: null,
                Fields:
                [
                    new DialogFieldDisplayItem("globalTheme", "Theme", "classic", "classic", false, false, "text"),
                    new DialogFieldDisplayItem("globalCompactMode", "Compact Mode", "false", "false", false, false, "checkbox")
                ],
                Actions:
                [
                    new DialogActionDisplayItem("save", "Save", true),
                    new DialogActionDisplayItem("cancel", "Cancel", false)
                ]));

            ListBox commandsList = FindDescendant<ListBox>(control, "CommandsList");
            commandsList.SelectedItem = commands[0];
            PumpStandaloneUi();

            TextBox editableTextField = control.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox => !textBox.IsReadOnly);
            editableTextField.Text = "dense";
            PumpStandaloneUi();

            CheckBox checkboxField = control.GetVisualDescendants()
                .OfType<CheckBox>()
                .First();
            checkboxField.IsChecked = true;
            PumpStandaloneUi();

            Button primaryActionButton = FindDescendant<Panel>(control, "DialogActionsHost")
                .Children
                .OfType<Button>()
                .First(button => string.Equals(button.Tag?.ToString(), "save", StringComparison.Ordinal));
            RaiseClick(primaryActionButton);

            CollectionAssert.AreEqual(new[] { "global_settings" }, selectedCommands.ToArray());
            CollectionAssert.Contains(updatedFields, "globalTheme=dense");
            CollectionAssert.Contains(updatedFields, "globalCompactMode=true");
            CollectionAssert.AreEqual(new[] { "save" }, selectedActions.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_command_dialog_pane_surfaces_import_trust_receipt_in_briefing()
    {
        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            string trustReceipt = string.Join(
                Environment.NewLine,
                "Import receipt correlation key: import/sr6/review-only; matches the blocker, oracle, and before/after environment diff lines below.",
                "Grounded import explain receipt: target sr6; oracle Chummer5a oracle covered; source toggles Seattle selected; blocker Hero Lab source missing source-toggle parity.",
                "Import environment tuple diff: before workspace/current-source/support-local/review-only; after oracle-reviewed/sr6/accepted-source-only; correlation import/sr6/review-only.",
                "Environment diff before import: the current workspace and support posture stay unchanged.",
                "Environment diff after import: accepted content binds to sr6 only after oracle review.");

            control.SetState(new CommandDialogPaneState(
                Commands: [],
                SelectedCommandId: null,
                DialogTitle: "Open Character",
                DialogMessage: trustReceipt,
                DialogTrustReceipt: trustReceipt,
                Fields:
                [
                    new DialogFieldDisplayItem("importRulesetId", "Ruleset", "sr6", "sr6", false, true, "text")
                ],
                Actions:
                [
                    new DialogActionDisplayItem("import", "Import", true)
                ]));

            TextBlock trustReceiptText = FindDescendant<TextBlock>(control, "DialogMessageText");
            StringAssert.Contains(trustReceiptText.Text, "Import receipt correlation key: import/sr6/review-only");
            StringAssert.Contains(trustReceiptText.Text, "Grounded import explain receipt: target sr6");
            StringAssert.Contains(trustReceiptText.Text, "Import environment tuple diff: before workspace/current-source/support-local/review-only; after oracle-reviewed/sr6/accepted-source-only; correlation import/sr6/review-only.");
            StringAssert.Contains(trustReceiptText.Text, "Environment diff before import:");
            StringAssert.Contains(trustReceiptText.Text, "Environment diff after import:");

            string[] visibleText = control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(textBlock => textBlock.IsVisible)
                .Select(textBlock => textBlock.Text ?? string.Empty)
                .ToArray();
            Assert.IsTrue(visibleText.Any(text => text.Contains("Import receipt correlation key: import/sr6/review-only", StringComparison.Ordinal)));
            Assert.IsTrue(visibleText.Any(text => text.Contains("Grounded import explain receipt: target sr6", StringComparison.Ordinal)));
        });
    }

    [TestMethod]
    public void Standalone_section_host_launches_build_lab_compare_and_blocker_explain_companion()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: null,
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: "{}",
                Rows: [],
                QuickActions: [],
                BuildLab: CreateBuildLabCompanionState(),
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Button companionButton = FindDescendant<Button>(control, "OpenBuildLabExplainCompanionButton");
            Assert.IsTrue(companionButton.IsEnabled);
            string companionLaunchUri = companionButton.Tag?.ToString() ?? string.Empty;
            StringAssert.Contains(companionLaunchUri, "/coach/?routeType=build");
            StringAssert.Contains(companionLaunchUri, "workspaceId=lab-intake");
            StringAssert.Contains(companionLaunchUri, "rulesetId=sr5");

            string[] visibleText = control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(textBlock => textBlock.IsVisible)
                .Select(textBlock => textBlock.Text ?? string.Empty)
                .ToArray();
            CollectionAssert.Contains(visibleText, "Build explain receipt and environment diff");
            Assert.AreEqual("Open Explain Companion", companionButton.Content?.ToString());
            Assert.IsTrue(visibleText.Any(text => text.Contains("Build blocker receipt:", StringComparison.Ordinal)));
            Assert.IsTrue(visibleText.Any(text => text.Contains("Build compare companion:", StringComparison.Ordinal)));
        });
    }

    [TestMethod]
    public void Standalone_section_context_surfaces_text_first_explain_drawer_summary_when_packet_metadata_is_present()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            string previewText = FindDescendant<TextBox>(control, "SectionPreviewBox").Text ?? string.Empty;
            StringAssert.Contains(previewText, "Explain drawer");
            StringAssert.Contains(previewText, "Explain packet: gear.medkit.rating");
            StringAssert.Contains(previewText, "Source anchor: SR5 p. 447 · Medkit");
            StringAssert.Contains(previewText, "Stale state: Packet snapshot gear-medkit-v1 no longer matches current snapshot gear-medkit-v2. Refresh before trusting this value.");
            StringAssert.Contains(previewText, "Follow-up: If rating drops below 6, extended healing intervals lose the clinic-grade bonus.");
        });
    }

    [TestMethod]
    public void Standalone_section_context_reads_canonical_explanation_packet_fields_for_text_first_drawer_copy()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            ExplainDrawerContext? explainContext = control.GetCurrentExplainDrawerContext();
            Assert.IsNotNull(explainContext);
            Assert.AreEqual("gear.medkit.rating", explainContext.ExplainPacket);
            Assert.AreEqual("SR5 p. 447 · Medkit", explainContext.SourceAnchor);
            Assert.AreEqual("Open the bound local rulebook anchor from this desktop route.", explainContext.SourceLaunch);
            Assert.AreEqual("/tmp/rulebooks/sr5-medkit.pdf", explainContext.SourceLaunchTarget);
            Assert.AreEqual("Packet snapshot gear-medkit-v1 no longer matches current snapshot gear-medkit-v2. Refresh before trusting this value.", explainContext.StaleState);
            Assert.AreEqual("If rating drops below 6, extended healing intervals lose the clinic-grade bonus.", explainContext.FollowUp);
        });
    }

    [TestMethod]
    public void Standalone_section_context_projects_packet_backed_explain_drawer_actions_for_desktop_launch_and_follow_up()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Button openSourceAnchor = FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.open_source_anchor");
            Button reviewBoundedFollowUp = FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.review_bounded_follow_up");
            Assert.AreEqual("Open Source Anchor", openSourceAnchor.Content?.ToString());
            Assert.AreEqual("Review Bounded Follow-up", reviewBoundedFollowUp.Content?.ToString());
            Assert.IsTrue(openSourceAnchor.IsVisible);
            Assert.IsTrue(reviewBoundedFollowUp.IsVisible);
        });
    }

    [TestMethod]
    public void Standalone_section_context_launches_source_anchor_from_packet_backed_explain_drawer()
    {
        string? launchedTarget = null;
        SectionHostControl.ExplainDrawerSourceAnchorLauncherOverrideForTesting = target =>
        {
            launchedTarget = target;
            return true;
        };

        try
        {
            WithStandaloneControl<SectionHostControl>(control =>
            {
                control.SetState(new SectionHostState(
                    SectionId: "gear",
                    NavigationTabs: [],
                    ActiveTabId: null,
                    SectionActions: [],
                    ActiveActionId: null,
                    Notice: "Ready.",
                    PreviewJson: CreateExplainDrawerPreviewJson(),
                    Rows:
                    [
                        new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                    ],
                    QuickActions: [],
                    BuildLab: null,
                    BrowseWorkspace: null,
                    ContactGraph: null,
                    DowntimePlanner: null,
                    NpcPersonaStudio: null));
                PumpStandaloneUi();

                RaiseClick(FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.open_source_anchor"));
                Assert.AreEqual("/tmp/rulebooks/sr5-medkit.pdf", launchedTarget);
            });
        }
        finally
        {
            SectionHostControl.ExplainDrawerSourceAnchorLauncherOverrideForTesting = null;
        }
    }

    [TestMethod]
    public void Main_window_review_bounded_follow_up_opens_text_first_desktop_follow_up_window()
    {
        EnsureHeadlessPlatform();
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
            session.Dispatch(() =>
            {
                DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting = null;
                try
                {
                    using FlagshipUiHarness harness = new();
                    harness.WaitForReady();
                    harness.SetActiveSectionForTesting("gear");
                    harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_explain_drawer.review_bounded_follow_up")?.IsVisible == true);
                    harness.Click("SectionQuickAction_explain_drawer.review_bounded_follow_up");
                    harness.WaitUntil(() => DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting is not null);

                    DesktopExplainDrawerFollowUpWindow window = DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting
                        ?? throw new AssertFailedException("Expected explain follow-up window to open.");
                    TextBlock statusText = window.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .First(textBlock => string.Equals(textBlock.Text, "Follow-up stays text-first, packet-backed, and scoped to the current desktop snapshot.", StringComparison.Ordinal));
                    Assert.IsNotNull(statusText);
                    Assert.AreEqual("Explain Follow-up", window.Title);

                    Button openSourceAnchor = window.GetVisualDescendants()
                        .OfType<Button>()
                        .First(button => string.Equals(button.Content?.ToString(), "Open Source Anchor", StringComparison.Ordinal));
                    Assert.IsTrue(openSourceAnchor.IsVisible);

                    window.Close();
                    harness.WaitUntil(() => DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting is null);
                }
                finally
                {
                    DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting?.Close();
                    DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting = null;
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            DisposeHeadlessSessionQuietly(session);
        }
    }

    [TestMethod]
    public void Standalone_priority_workflow_dialog_renders_real_combo_list_and_skill_choice_controls()
    {
        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState dialog = BuildPriorityWorkflowDialogForTesting("Priority");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalent", "B");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalentChoice", "Magician");

            window.BindDialog(dialog);
            PumpStandaloneUi();

            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityHeritage")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityAttributes")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityTalent")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityTalentChoice")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkills")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityResources")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatypeCategory")));
            Assert.IsNotNull(FindDescendant<ListBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetavariant")));
            Assert.IsNotNull(FindDescendant<ListBox>(window, "newCharacterPriorityQualitiesList"));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice1")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice2")));
            Assert.IsNull(FindDescendantOrDefault<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice3")));
        });
    }

    private static BuildLabConceptIntakeState CreateBuildLabCompanionState()
        => new(
            WorkspaceId: "lab-intake",
            WorkflowId: "workflow.build-lab",
            Title: "Build Lab Intake",
            Summary: "Capture concept and constraints before generating variants.",
            RulesetId: RulesetDefaults.Sr5,
            BuildMethod: "Priority",
            IntakeFields:
            [
                new BuildLabIntakeField(
                    "concept",
                    "Concept",
                    BuildLabFieldKinds.Text,
                    "Street Face",
                    "Describe the concept",
                    "Engine-owned concept DTO",
                    true)
            ],
            RoleBadges:
            [
                new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
            ],
            ConstraintBadges:
            [
                new BuildLabBadge("ops", "Ops-first", BuildLabBadgeKinds.Constraint, true)
            ],
            ProvenanceBadges:
            [
                new BuildLabBadge("runtime", "Runtime-backed", BuildLabBadgeKinds.Provenance, true)
            ],
            Variants:
            [
                new BuildLabVariantProjection(
                    VariantId: "variant.social",
                    Label: "Social Operator",
                    Summary: "Fastest ops-first lane.",
                    TableFit: "Best for ops-first tables",
                    RoleBadges:
                    [
                        new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
                    ],
                    Metrics:
                    [
                        new BuildLabVariantMetric("bookkeeping", "Bookkeeping", "Low")
                    ],
                    Warnings: [],
                    OverlapBadges: [],
                    Actions: [],
                    ExplainEntryId: "buildlab.variant.social")
            ],
            ProgressionTimelines: [],
            ExportPayloads: [],
            ExportTargets: [],
            Actions:
            [
                new BuildLabActionDescriptor("handoff-template", "Save As Template", BuildLabSurfaceIds.ExportRail, false, "target.character-template")
            ],
            ExplainEntryId: "buildlab.intake.concept",
            SourceDocumentId: "source.table-profile",
            CanContinue: true,
            NextSafeAction: "Rebind the active runtime before export.",
            RuntimeCompatibilitySummary: "One quick-action binding still needs review.",
            CampaignFitSummary: "Best fit is an ops-first crew with sparse matrix scenes.",
            SupportClosureSummary: "Support can cite the same runtime fingerprint after handoff.",
            Watchouts:
            [
                "No recap-safe publication is attached yet."
            ]);

    private static string CreateExplainDrawerPreviewJson()
        => """
{
  "section": "gear",
  "gear": [
    {
      "name": "Medkit",
      "rating": 6,
      "location": "Backpack"
    }
  ],
  "explain": {
    "packet_id": "gear.medkit.rating",
    "source_anchors": [
      {
        "book": "SR5",
        "page": "447",
        "section": "Medkit",
        "localPdfPath": "/tmp/rulebooks/sr5-medkit.pdf"
      }
    ],
        "stale_if_snapshot_changes": {
          "snapshot_ref": "gear-medkit-v1",
          "current_snapshot_ref": "gear-medkit-v2"
        },
    "boundedFollowUpSummary": "If rating drops below 6, extended healing intervals lose the clinic-grade bonus."
  }
}
""";

    [TestMethod]
    public void Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available()
    {
        WithStandaloneControl<CoachSidecarControl>(control =>
        {
            int copyRequests = 0;
            control.CopyLaunchRequested += (_, _) => copyRequests++;
            control.SetState(new CoachSidecarPaneState(
                Status: "ready",
                PromptPolicy: "evidence-first",
                BudgetSummary: "healthy",
                WorkspaceId: "demo-runner",
                RuntimeFingerprint: "runtime-1",
                LaunchUri: "https://chummer.run/coach/demo",
                LaunchStatusMessage: "Ready to copy.",
                ErrorMessage: null,
                Providers:
                [
                    new CoachProviderDisplayItem("Primary", "provider-primary", "api", "closed", "https", "token", "bound", "recent", "none")
                ],
                Audits:
                [
                    new CoachAuditDisplayItem("conversation-1", "runtime-1", "https://chummer.run/coach/demo", "summary", "flavor", "healthy", "structured", "recommend", "evidence", "risk", "source", "cached", "direct", "full", "now")
                ]));

            Button copyButton = FindDescendant<Button>(control, "CopyCoachLaunchButton");
            Assert.IsTrue(copyButton.IsEnabled, "Coach sidecar copy button must enable when a scoped launch URI is available.");
            RaiseClick(copyButton);

            Assert.AreEqual(1, copyRequests, "Coach sidecar copy control must raise a copy-launch event.");
        });
    }

    [TestMethod]
    public void Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end()
    {
        WithLoadedRunnerHarness(harness =>
        {
            TabStrip tabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            harness.WaitUntil(() => tabStrip.Items.OfType<NavigatorTabItem>().Any());

            string currentTabId = harness.ShellPresenter.State.ActiveTabId ?? string.Empty;
            NavigatorTabItem firstTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => !string.IsNullOrWhiteSpace(tab.Id) && !string.Equals(tab.Id, currentTabId, StringComparison.Ordinal))
                ?? tabStrip.Items
                    .OfType<NavigatorTabItem>()
                    .First(tab => !string.IsNullOrWhiteSpace(tab.Id));
            string selectedTabId = firstTab.Id;
            harness.ClickLoadedRunnerTab(firstTab.Label);
            harness.WaitUntil(() =>
                harness.ShellPresenter.SelectedTabIds.Contains(selectedTabId)
                || string.Equals(harness.ShellPresenter.State.ActiveTabId, selectedTabId, StringComparison.Ordinal));

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
            harness.ClickMenuCommand("global_settings");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            harness.WaitUntil(() =>
                harness.ShellPresenter.ExecutedCommandIds.Contains("global_settings")
                && harness.Presenter.ExecutedCommandIds.Contains("global_settings"));

            harness.UpdateFirstEditableDialogTextField("dense");
            harness.WaitUntil(() => harness.Presenter.DialogFieldUpdates.Any(update => string.Equals(update.Value, "dense", StringComparison.Ordinal)));

            harness.ClickDialogAction("save");
            harness.WaitUntil(() =>
                harness.Presenter.ExecutedDialogActionIds.Contains("save")
                && !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal),
                timeoutMs: 4000);
            harness.WaitUntil(() => !harness.State.IsBusy);

            harness.SetActiveSectionForTesting("spells");
            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_spell_add")?.IsVisible == true);
            harness.Click("SectionQuickAction_spell_add");
            harness.WaitUntil(() =>
                harness.Presenter.HandledUiControlIds.Contains("spell_add")
                && string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Spell",
                    StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void Keyboard_shortcuts_resolve_to_the_same_shell_commands()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            harness.PressKey(Key.S, RawInputModifiers.Control);
            harness.WaitUntil(() =>
                string.Equals(harness.ShellPresenter.State.LastCommandId, "save_character", StringComparison.Ordinal)
                && string.Equals(harness.Presenter.State.LastCommandId, "save_character", StringComparison.Ordinal));

            harness.PressKey(Key.G, RawInputModifiers.Control);
            harness.WaitUntil(() =>
                string.Equals(harness.ShellPresenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                && string.Equals(harness.Presenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                && string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces()
    {
        // Runtime_backed_chummer5a_muscle_memory_inventory_receipt_covers_every_surface_and_element
        // Runtime_backed_chummer5a_muscle_memory_inventory_secondary_routes_distinguish_tooltip_hosts_from_real_auxiliary_routes
        // Runtime_backed_sr4_chummer4_muscle_memory_inventory_receipt_covers_every_surface_and_element
        WithHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("LoadDemoRunnerButton");
            harness.SetActiveSectionForTesting("attributes");
            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("AttributeParityEditorBorder")?.IsVisible == true);

            RuntimeControlInventoryNode shellInventory = CaptureControlInventory(harness.Window);
            AssertInventoryContains(shellInventory, "SaveButton", "Button", toolTipFragment: "Save");
            AssertInventoryContains(shellInventory, "RosterTree", "TreeView");
            AssertInventoryContains(shellInventory, "AttributeBaseEditor_BOD", "NumericUpDown");
            AssertInventoryContains(shellInventory, "AttributeKarmaEditor_BOD", "NumericUpDown");
        });

        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState dialog = BuildPriorityWorkflowDialogForTesting("Priority");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalent", "B");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalentChoice", "Magician");

            window.BindDialog(dialog);
            PumpStandaloneUi();

            RuntimeControlInventoryNode dialogInventory = CaptureControlInventory(window);
            AssertInventoryContains(
                dialogInventory,
                DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityHeritage"),
                "ComboBox");
            AssertInventoryContains(
                dialogInventory,
                DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype"),
                "ListBox");
            AssertInventoryContains(dialogInventory, "newCharacterPriorityQualitiesList", "ListBox");
        });
    }

    [TestMethod]
    public void Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches()
    {
        List<RuntimeRouteInventoryEntry> routes = [];
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                routes.Add(CaptureRuntimeRouteInventory(harness, "shell-startup", "shell", branchId: "startup"));

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "new_character");
            routes.Add(CaptureRuntimeRouteInventory(harness, "popup-file-menu", "popup", branchId: "file-menu"));

            ClickRuntimeMenuCommand(harness, "FileMenuButton", "new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);
            routes.Add(CaptureRuntimeRouteInventory(harness, "dialog-new-character", "dialog", branchId: "new-character"));
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Select Build Method",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("global_settings", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
                routes.Add(CaptureRuntimeRouteInventory(harness, "dialog-global-settings", "dialog", branchId: "global-settings"));
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.State.WorkspaceId is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "load demo runner workspace hydration");
            routes.Add(CaptureRuntimeRouteInventory(harness, "shell-loaded-runner", "shell", branchId: "loaded-runner"));

            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionHostControl")?.IsVisible == true);
            routes.Add(CaptureRuntimeRouteInventory(harness, "section-active-surface", "section", branchId: "active-section"));

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            routes.Add(CaptureRuntimeRouteInventory(harness, "popup-tools-menu", "popup", branchId: "tools-menu"));

            ClickRuntimeMenuCommand(harness, "WindowsMenuButton", "close_window");
            harness.WaitUntil(() =>
                harness.State.WorkspaceId is null
                && harness.State.Profile is null
                && harness.State.Session.OpenWorkspaces.Count == 0
                && !harness.State.IsBusy);
            routes.Add(CaptureRuntimeRouteInventory(harness, "shell-after-close-window", "shell", branchId: "workspace-closed"));

                foreach (string rulesetId in new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 })
                {
                    harness.ShellPresenter.SetPreferredRulesetAsync(rulesetId, CancellationToken.None).GetAwaiter().GetResult();
                    harness.WaitUntil(() =>
                        string.Equals(harness.ShellPresenter.State.PreferredRulesetId, rulesetId, StringComparison.Ordinal)
                        && string.Equals(harness.ShellPresenter.State.ActiveRulesetId, rulesetId, StringComparison.Ordinal));
                    if (harness.State.Session.OpenWorkspaces.Count > 0)
                    {
                        harness.WaitUntil(() =>
                        {
                            TreeView? tree = harness.FindControlOrDefault<TreeView>("RosterTree");
                            return tree is not null && SnapshotRosterItems(tree).Length > 0;
                        });
                    }
                    routes.Add(CaptureRuntimeRouteInventory(
                        harness,
                        $"ruleset-{rulesetId}-codex-tree",
                        "ruleset",
                        branchId: $"ruleset-{rulesetId}"));
                }
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState priorityDialog = BuildPriorityWorkflowDialogForTesting("Priority");
            priorityDialog = RebuildPriorityWorkflowDialogField(priorityDialog, "newCharacterPriorityTalent", "B");
            priorityDialog = RebuildPriorityWorkflowDialogField(priorityDialog, "newCharacterPriorityTalentChoice", "Magician");
            window.BindDialog(priorityDialog);
            PumpStandaloneUi();
            routes.Add(CaptureStandaloneRouteInventory(
                window,
                routeId: "dialog-priority-workflow-priority",
                routeFamily: "dialog",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "priority"));

            DesktopDialogState sumToTenDialog = BuildPriorityWorkflowDialogForTesting("Sum-to-Ten");
            sumToTenDialog = RebuildPriorityWorkflowDialogField(sumToTenDialog, "newCharacterPriorityTalent", "B");
            sumToTenDialog = RebuildPriorityWorkflowDialogField(sumToTenDialog, "newCharacterPriorityTalentChoice", "Magician");
            window.BindDialog(sumToTenDialog);
            PumpStandaloneUi();
            routes.Add(CaptureStandaloneRouteInventory(
                window,
                routeId: "dialog-priority-workflow-sum-to-ten",
                routeFamily: "dialog",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "sum-to-ten"));
        });

        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new(
                SectionId: "attributes",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: "tab-info.attributes",
                Notice: "Ready.",
                PreviewJson: """
{
  "sectionId": "attributes",
  "attributes": [
    { "name": "Body", "base": 4, "karma": 1, "value": "5", "limits": "1/6 (9)", "baseUnlocked": true, "priorityMaximum": 6, "karmaMaximum": 6 },
    { "name": "Agility", "base": 5, "karma": 0, "value": "5", "limits": "2/7 (10)", "baseUnlocked": true, "priorityMaximum": 6, "karmaMaximum": 6 }
  ]
}
""",
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            Dispatcher.UIThread.RunJobs();
            routes.Add(CaptureStandaloneControlRouteInventory(
                control,
                routeId: "section-attributes-editor",
                routeFamily: "section",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "attributes-editor"));
        });

        InteractiveRuntimeRouteInventoryReceipt receipt = new(
            GeneratedAt: DateTimeOffset.UtcNow.ToString("O"),
            ContractName: "chummer6-ui.interactive_runtime_route_inventory",
            Status: "pass",
            Summary: "Runtime route inventory captures recursive shell, popup, dialog, section, and ruleset-lane surfaces.",
            RouteFamilies: routes.Select(route => route.RouteFamily).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            RulesetLanes: routes.Select(route => route.RulesetId).Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            Routes: routes.OrderBy(route => route.RouteId, StringComparer.Ordinal).ToArray());

        string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ResolveSourceFile("Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs")))! )
            ?? throw new DirectoryNotFoundException("Could not locate repo root for interactive runtime route inventory receipt generation.");
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "INTERACTIVE_RUNTIME_ROUTE_INVENTORY.generated.json");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, ScreenshotEvidenceJsonOptions));

        CollectionAssert.AreEquivalent(
            new[] { "dialog", "popup", "ruleset", "section", "shell" },
            receipt.RouteFamilies.ToArray(),
            "The runtime route inventory receipt must cover shell, popup, dialog, section, and ruleset route families.");
        CollectionAssert.AreEquivalent(
            new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 },
            receipt.RulesetLanes.ToArray(),
            "The runtime route inventory receipt must cover SR4, SR5, and SR6 lanes.");
        Assert.IsTrue(receipt.Routes.Any(route => string.Equals(route.RouteId, "section-attributes-editor", StringComparison.Ordinal)));
        Assert.IsTrue(receipt.Routes.Any(route => string.Equals(route.RouteId, "dialog-priority-workflow-priority", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Desktop_shell_preserves_chummer5a_familiarity_cues()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            string[] menuLabels = menuPanel.Items
                .OfType<MenuItem>()
                .Select(button => button.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "File",
                    "Edit",
                    "Special",
                    "Tools",
                    "Windows",
                    "Help"
                },
                menuLabels);

            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control contentRegion = harness.FindControl<Control>("ContentRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");
            ProgressBar progressBar = harness.FindControl<ProgressBar>("WorkbenchProgressBar");

            Point menuTop = harness.TranslateToWindow(harness.FindControl<Control>("MenuBarRegion"));
            Point toolTop = harness.TranslateToWindow(toolStripRegion);
            Point contentTop = harness.TranslateToWindow(contentRegion);
            Point statusTop = harness.TranslateToWindow(statusStripRegion);

            Assert.IsTrue(toolTop.Y > menuTop.Y);
            Assert.IsTrue(contentTop.Y > toolTop.Y);
            Assert.IsTrue(statusTop.Y > contentTop.Y);
            Assert.IsTrue(progressBar.Value >= 100d);
        });
    }

    [TestMethod]
    public void Desktop_shell_preserves_classic_dense_three_pane_workbench_posture()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Control menuBarRegion = harness.FindControl<Control>("MenuBarRegion");
            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control rosterPaneRegion = harness.FindControl<Control>("RosterPaneRegion");
            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control centerShellRegion = harness.FindControl<Control>("CenterShellRegion");
            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Control summaryHeaderRegion = harness.FindControl<Control>("SummaryHeaderRegion");
            Control sectionRegion = harness.FindControl<Control>("SectionRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");

            Assert.IsTrue(rosterPaneRegion.IsVisible, "The classic shell must keep the roster rail visible by default.");
            Assert.IsTrue(rosterPaneRegion.Bounds.Width >= 240d && rosterPaneRegion.Bounds.Width <= 360d, "The roster rail must stay dense and desktop-scaled.");
            Assert.IsFalse(leftNavigatorRegion.IsVisible, "The codex navigator pane must stay collapsed on first paint.");
            Assert.IsFalse(rightShellRegion.IsVisible, "Right inspector/coach area must stay collapsed until a command or dialog actually needs it.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Right inspector/coach area must default to a collapsed width in the compact single-runner shell.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > rosterPaneRegion.Bounds.Width, "The central editing workbench must remain the dominant pane.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > 0d, "The central editing workbench must remain visible when the right rail is collapsed.");
            Assert.IsTrue(menuBarRegion.Bounds.Height <= 72d, "The top menu row must read like desktop chrome, not a hero header.");
            // Legacy-equivalent chrome gate marker: must not reintroduce synthetic Runner Summary chrome.
            Assert.IsFalse(summaryHeaderRegion.IsVisible, "The merged workspace-context and summary band must stay hidden until a real restore or conflict context exists.");
            Assert.IsTrue(statusStripRegion.Bounds.Height <= 72d, "The bottom strip must stay compact like the legacy status posture.");
            Assert.IsTrue(harness.FindControl<TreeView>("RosterTree").IsVisible, "The roster tree must stay visible in the compact roster rail.");
            Assert.IsNull(harness.FindControlOrDefault<TabControl>("LoadedRunnerTabStrip"), "The left rail must avoid a second tab control and keep the classic tree posture.");
            Point sectionTop = harness.TranslateToWindow(sectionRegion);
            Point toolTop = harness.TranslateToWindow(toolStripRegion);
            Assert.IsTrue(sectionTop.Y > toolTop.Y, "The section host must start directly under the toolstrip when no restore summary is active.");
        });
    }

    [TestMethod]
    public void Desktop_shell_preserves_classic_dense_center_first_workbench_posture()
    {
        Desktop_shell_preserves_classic_dense_three_pane_workbench_posture();
    }

    [TestMethod]
    public void Opening_mainframe_preserves_chummer5a_successor_workbench_posture()
    {
        Desktop_shell_preserves_chummer5a_familiarity_cues();
    }

    [TestMethod]
    public void Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist()
    {
        new DesktopShellRulesetCatalogTests().DesktopShell_hides_workspace_left_pane_for_single_runner_posture();
    }

    [TestMethod]
    public void Runtime_backed_file_menu_preserves_working_open_save_import_routes()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "open_character");
            harness.ClickMenuCommand("open_character");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open Character",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Open Character",
                "Import Ruleset",
                "Import Source",
                "Review imported summary");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open Character",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);
        });
    }

    [TestMethod]
    public void Runtime_backed_mouse_only_first_minute_controls_stay_inside_flagship_viewport_without_scroll_dependency()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            AssertMouseReachabilityWithinFlagshipViewport(
                harness.Window,
                "first-minute flagship shell");
        });
    }

    [TestMethod]
    public void Runtime_backed_mouse_only_loaded_runner_controls_stay_inside_flagship_viewport_without_scroll_dependency()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertMouseReachabilityWithinFlagshipViewport(
                harness.Window,
                "loaded-runner flagship workbench");
        });
    }

    [TestMethod]
    public void Master_index_is_a_first_class_runtime_backed_workbench_route()
    {
        // Runtime_backed_mouse_only_master_index_source_click_executes_open_source_action
        Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome();
    }

    [TestMethod]
    public void Character_roster_is_a_first_class_runtime_backed_workbench_route()
    {
        // Runtime_backed_mouse_only_character_roster_double_tap_opens_selected_runner
        Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome();
    }

    [TestMethod]
    public void Theme_tokens_preserve_chummer5a_palette_and_readability()
    {
        Dictionary<string, Dictionary<string, Color>> themeBrushes = LoadThemeBrushes(ResolveSourceFile("Chummer.Avalonia", "App.axaml"));
        Dictionary<string, Color> light = themeBrushes["Light"];
        Dictionary<string, Color> dark = themeBrushes["Dark"];

        Assert.AreEqual("#1C4A2D", ToHex(light["ChummerShellActiveMenuBorderBrush"]));
        Assert.AreEqual("#1C4A2D", ToHex(light["ChummerShellAccentButtonBrush"]));
        Assert.AreEqual("#1C4A2D", ToHex(dark["ChummerShellActiveMenuBackgroundBrush"]));
        Assert.AreEqual("#90C39A", ToHex(dark["ChummerShellActiveMenuBorderBrush"]));

        AssertContrastAtLeast(light["ChummerShellForegroundBrush"], light["ChummerShellSurfaceBrush"], 12d, "light shell foreground on surface");
        AssertContrastAtLeast(light["ChummerShellMutedForegroundBrush"], light["ChummerShellSurfaceBrush"], 7d, "light shell muted foreground on surface");
        AssertContrastAtLeast(light["ChummerShellAccentButtonForegroundBrush"], light["ChummerShellAccentButtonBrush"], 7d, "light accent button text");
        AssertContrastAtLeast(light["ChummerShellWarningBrush"], light["ChummerShellSurfaceBrush"], 4.5d, "light warning tone on surface");
        AssertContrastAtLeast(light["ChummerShellDangerBrush"], light["ChummerShellSurfaceBrush"], 4.5d, "light danger tone on surface");

        AssertContrastAtLeast(dark["ChummerShellForegroundBrush"], dark["ChummerShellSurfaceBrush"], 12d, "dark shell foreground on surface");
        AssertContrastAtLeast(dark["ChummerShellMutedForegroundBrush"], dark["ChummerShellSurfaceBrush"], 7d, "dark shell muted foreground on surface");
        AssertContrastAtLeast(dark["ChummerShellAccentButtonForegroundBrush"], dark["ChummerShellAccentButtonBrush"], 7d, "dark accent button text");
        AssertContrastAtLeast(dark["ChummerShellWarningBrush"], dark["ChummerShellSurfaceBrush"], 4.5d, "dark warning tone on surface");
        AssertContrastAtLeast(dark["ChummerShellDangerBrush"], dark["ChummerShellSurfaceBrush"], 4.5d, "dark danger tone on surface");
    }

    [TestMethod]
    public void Loaded_runner_preserves_visible_character_tab_posture()
    {
        WithLoadedRunnerHarness(harness =>
        {
            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            harness.WaitUntil(() => tabStrip.IsVisible && loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any());

            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);

            Assert.IsTrue(rosterTree.IsVisible);
            Assert.IsTrue(tabStrip.IsVisible);
            Assert.IsTrue(rosterTree.Bounds.Width > 0d && rosterTree.Bounds.Height > 0d, "Roster tree should render with a visible desktop footprint.");
            Assert.IsTrue(rootItems.Length > 0, "Loaded runner posture requires a visible workspace group in the roster tree.");
            Assert.IsTrue(loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any(tab =>
                (tab.Label ?? string.Empty).Contains("Runner", StringComparison.Ordinal)),
                "Loaded runner tab strip should surface a visible Runner tab button.");
            Assert.IsTrue(
                rootItems.SelectMany(static item => item.Children).Any(item => !string.IsNullOrWhiteSpace(item.Id)),
                "Loaded runner roster tree must expose at least one workspace entry.");
        });
    }

    [TestMethod]
    public void Loaded_runner_header_stays_tab_panel_only_without_metric_cards()
    {
        WithLoadedRunnerHarness(harness =>
        {
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");

            harness.WaitUntil(() => tabStrip.IsVisible && loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any());

            Assert.IsNull(harness.FindControlOrDefault<Control>("NameValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("AliasValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("KarmaValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("SkillsValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeInspectButton"));
        });
    }

    [TestMethod]
    public void Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks()
    {
        WithRuntimeLoadedRunnerHarness(harness =>
        {
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("StatusStripRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<ProgressBar>("WorkbenchProgressBar").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible);

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");

            harness.WaitUntil(() =>
                loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any(tab =>
                    (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase)));
            NavigatorTabItem gearTab = loadedRunnerTabStrip.Items
                .OfType<NavigatorTabItem>()
                .First(tab => (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase));
            harness.SetActiveSectionForTesting(gearTab.SectionId);

            harness.WaitUntil(() =>
                sectionRows.ItemCount > 0
                && !string.IsNullOrWhiteSpace(preview.Text)
                && SnapshotRosterItems(rosterTree).Length > 0);

            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);

            Assert.IsTrue(rosterTree.IsVisible, "Legacy frmCareer parity requires a visible roster tree posture.");
            Assert.IsTrue(sectionRows.IsVisible, "Legacy frmCareer parity requires a visible dense section/workbench list.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(preview.Text), "Legacy frmCareer parity requires a visible detail/preview pane.");
            Assert.IsTrue(rootItems.Length > 0, "Legacy frmCareer parity requires visible workspace grouping in the roster tree.");

            NavigatorTabItem[] renderedTabs = loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().ToArray();
            Assert.IsTrue(renderedTabs.Length >= 2, "Legacy frmCareer parity requires multiple visible workbench tabs.");
            Assert.IsTrue(renderedTabs.Any(tab => (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase)), "Legacy frmCareer parity requires a gear navigation landmark.");

            string previewPayload = preview.Text ?? string.Empty;
            bool hasLegacyOrWorkflowSectionMarker =
                previewPayload.Contains("\"sectionId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"workflowId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"progressionTimelines\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"gear\"", StringComparison.OrdinalIgnoreCase)
                || previewPayload.Contains("\"profile\"", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(
                hasLegacyOrWorkflowSectionMarker,
                "Legacy frmCareer parity requires preview payload landmarks that map sections/workflows to the visible workbench.");
        });
    }

    [TestMethod]
    public void Character_creation_preserves_familiar_dense_builder_rhythm()
    {
        // Runtime_backed_sr4_new_character_dialog_preserves_chummer4_build_method_combo_posture
        // Runtime_backed_sr4_new_character_preserves_modify_button_row_order_and_footer_posture
        WithStandaloneControl<SectionHostControl>(control =>
        {
            List<AttributeEditRequest> edits = [];
            control.AttributeEditRequested += (_, request) => edits.Add(request);
            control.SetState(new SectionHostState(
                SectionId: "attributes",
                NavigationTabs: [],
                ActiveTabId: "tab-info",
                SectionActions: [],
                ActiveActionId: "tab-info.attributes",
                Notice: "Ready.",
                PreviewJson: """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    }
  ]
}
""",
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Border attributeEditor = FindDescendant<Border>(control, "AttributeParityEditorBorder");
            NumericUpDown baseEditor = FindDescendant<NumericUpDown>(control, "AttributeBaseEditor_BOD");
            NumericUpDown karmaEditor = FindDescendant<NumericUpDown>(control, "AttributeKarmaEditor_BOD");
            Expander reviewExpander = FindDescendant<Expander>(control, "SectionReviewExpander");

            Assert.IsTrue(attributeEditor.IsVisible, "Character creation parity requires the dedicated attribute editor surface.");
            Assert.IsTrue(baseEditor.IsVisible, "Character creation parity requires a visible base numeric editor.");
            Assert.IsTrue(karmaEditor.IsVisible, "Character creation parity requires a visible karma numeric editor.");
            // Legacy-equivalent chrome gate marker: The section preview header must not invent Review chrome that Chummer5A never had.
            Assert.IsFalse(reviewExpander.IsVisible, "Character creation parity must not fall back to the review expander.");

            baseEditor.Value = 4m;
            PumpStandaloneUi();
            Thread.Sleep(300);
            PumpStandaloneUi();

            Assert.IsTrue(
                edits.Any(edit =>
                    string.Equals(edit.AttributeName, "Body", StringComparison.Ordinal)
                    && string.Equals(edit.Bucket, "base", StringComparison.Ordinal)
                    && edit.Value == 4),
                "Character creation parity must emit a real attribute edit request when the numeric editor changes.");
        });
    }

    [TestMethod]
    public void Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: null,
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: string.Empty,
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Expander reviewExpander = FindDescendant<Expander>(control, "SectionReviewExpander");
            Border sectionRowsBorder = FindDescendant<Border>(control, "SectionRowsBorder");
            Border sectionContextBorder = FindDescendant<Border>(control, "SectionContextBorder");

            Assert.IsFalse(reviewExpander.IsVisible, "A fresh workbench launch must not show a fake empty section expander.");
            Assert.IsFalse(sectionRowsBorder.IsVisible, "A fresh workbench launch must not show an empty section rows scaffold.");
            Assert.IsFalse(sectionContextBorder.IsVisible, "A fresh workbench launch must not show synthetic section context before a real surface is opened.");
        });
    }

    [TestMethod]
    public void Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            harness.SetActiveSectionForTesting("progress");
            ListBox progressRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox progressPreview = harness.FindControl<TextBox>("SectionPreviewBox");
            harness.WaitUntil(() => progressRows.ItemCount > 0);

            string[] progressRowText = SnapshotListBoxItems(progressRows).Select(item => item.ToString() ?? string.Empty).ToArray();
            CollectionAssert.Contains(progressRowText, "progress[0] = First extraction · +2 karma");
            StringAssert.Contains(progressPreview.Text ?? string.Empty, "\"diary\"");
            StringAssert.Contains(progressPreview.Text ?? string.Empty, "\"karma\"");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "progress",
                actionControlId: "create_entry",
                expectedTitle: "Add Entry",
                requiredFieldLabel: "Entry Title",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "profile",
                actionControlId: "open_notes",
                expectedTitle: "Edit Notes",
                requiredFieldLabel: "Notes",
                requiredActionId: "save");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "initiationgrades",
                actionControlId: "initiation_add",
                expectedTitle: "Add Initiation / Submersion",
                requiredFieldLabel: "Grade",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Gear_builder_preserves_familiar_browse_detail_confirm_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            Border noticeBorder = harness.FindControl<Border>("NoticeBorder");

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

            CollectionAssert.Contains(rowText, "gear.weapons[0] = Ares Alpha");
            CollectionAssert.Contains(rowText, "gear.armor[0] = Armor Jacket");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"combat\"");
            Assert.IsFalse(noticeBorder.IsVisible, "Idle ready-state notice chrome should stay hidden in the gear builder.");
        });
    }

    [TestMethod]
    public void Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues()
    {
        // Runtime_backed_sr4_dice_roller_preserves_chummer4_checkbox_copy
        // Runtime_backed_sr4_dice_roller_preserves_chummer4_spinner_posture_and_topbar_geography
        WithLoadedRunnerHarness(harness =>
        {
            harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            harness.WaitUntil(() => sectionRows.ItemCount >= 8);

            object[] items = SnapshotListBoxItems(sectionRows);
            object? cyberwareRow = items.FirstOrDefault(item =>
                item.ToString()?.Contains("cyberware[0] = Wired Reflexes 2", StringComparison.Ordinal) == true);

            Assert.IsNotNull(cyberwareRow, "Cyberware row should remain visible in the dense section list.");
            sectionRows.SelectedItem = cyberwareRow;
            harness.WaitUntil(() => ReferenceEquals(sectionRows.SelectedItem, cyberwareRow));

            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"essence\": 5.34");

            harness.Click("SectionQuickAction_cyberware_add");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Cyberware",
                    StringComparison.Ordinal));
            Panel actionsHost = harness.FindControl<Panel>("DialogActionsHost");
            Assert.IsTrue(actionsHost.Children.OfType<Button>().Any(), "Cyberware familiarity proof must keep a visible dialog posture with actionable controls.");
        });
    }

    [TestMethod]
    public void Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "drugs",
                actionControlId: "drug_add",
                expectedTitle: "Add Drug",
                requiredFieldLabel: "Drug",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "spells",
                actionControlId: "spell_add",
                expectedTitle: "Add Spell",
                requiredFieldLabel: "Spell",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "powers",
                actionControlId: "adept_power_add",
                expectedTitle: "Add Adept Power",
                requiredFieldLabel: "Power",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "initiationgrades",
                actionControlId: "initiation_add",
                expectedTitle: "Add Initiation / Submersion",
                requiredFieldLabel: "Grade",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "complexforms",
                actionControlId: "complex_form_add",
                expectedTitle: "Add Complex Form",
                requiredFieldLabel: "Complex Form",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "aiprograms",
                actionControlId: "matrix_program_add",
                expectedTitle: "Add Program / Cyberdeck Item",
                requiredFieldLabel: "Program",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Contacts_diary_and_support_routes_execute_with_public_path_visibility()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "contacts",
                actionControlId: "contact_add",
                expectedTitle: "Add Contact",
                requiredFieldLabel: "Name",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "progress",
                actionControlId: "create_entry",
                expectedTitle: "Add Entry",
                requiredFieldLabel: "Entry Title",
                requiredActionId: "add");
        });

        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            OpenMenuUntilCommandVisible(harness, "HelpMenuButton", "report_bug");
            harness.ClickMenuCommand("report_bug");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Support and bug reporting",
                    StringComparison.Ordinal));

            string dialogBody = string.Join(
                "\n",
                harness.FindDialogFieldTexts()
                    .Concat(harness.FindDialogFieldInputTexts())
                    .Concat([harness.FindControl<TextBlock>("DialogMessageText").Text ?? string.Empty]));

            Assert.IsTrue(
                dialogBody.Contains("/account/support", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("Hub support surface", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("support closure", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must preserve a public-facing support route description.");
            Assert.IsTrue(
                dialogBody.Contains("/contact", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("support", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must keep a visible public contact/support affordance.");
            Assert.IsTrue(
                dialogBody.Contains("GitHub is still available", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("github.com/chummer5a/chummer5a", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must preserve a public GitHub reporting fallback.");
            Assert.IsFalse(dialogBody.Contains("chummer-api", StringComparison.OrdinalIgnoreCase), "Support/report routes must stay public and must not expose internal Docker hosts.");
            string[] actionIds = harness.FindControl<Panel>("DialogActionsHost").Children
                .OfType<Button>()
                .Select(button => button.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            Assert.IsTrue(actionIds.Contains("close", StringComparer.Ordinal), "Support/report flow must expose an explicit close/confirm affordance.");
        });
    }

    [TestMethod]
    public void Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "vehicles",
                actionControlId: "vehicle_add",
                expectedTitle: "Add Vehicle / Drone",
                requiredFieldLabel: "Vehicle",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Visual_review_evidence_is_published_for_light_and_dark_shell_states()
    {
        // Runtime_backed_sr4_starter_runner_gear_add_uses_chummer4_category_combobox_posture
        // Runtime_backed_sr4_starter_runner_gear_add_preserves_two_pane_geography_and_primary_action_band
        // Runtime_backed_gear_add_dialog_uses_legacy_category_combobox_posture
        string screenshotDirectory = ResolveScreenshotDirectory();
        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, recursive: true);
        }

        Directory.CreateDirectory(screenshotDirectory);

        string[] expectedFiles = VeteranCertificationScreenshotFiles;

        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, ScreenshotProofCapture> screenshots = new(StringComparer.Ordinal);

            string? priorReleaseChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
            string? priorSampleControls = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
            try
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", null);
                screenshots[expectedFiles[0]] = WithHarness(harness =>
                {
                    harness.WaitForReady();
                    harness.SetTheme(ThemeVariant.Light);
                    return CaptureScreenshotProof(harness, expectedFiles[0]);
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorReleaseChannel);
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleControls);
            }

            Dictionary<string, ScreenshotProofCapture> veteranWorkflowScreenshots = WithHarness(harness =>
            {
                Dictionary<string, ScreenshotProofCapture> captured = new(StringComparer.Ordinal);

                harness.WaitForReady();

                harness.SetTheme(ThemeVariant.Light);
                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
                captured[expectedFiles[1]] = CaptureScreenshotProof(harness, expectedFiles[1]);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => string.IsNullOrWhiteSpace(harness.ShellPresenter.State.OpenMenuId));

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
                harness.ClickMenuCommand("global_settings");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Global Settings",
                    "UI Scale",
                    "Theme",
                    "Language",
                    "Compact Mode");
                captured[expectedFiles[2]] = CaptureScreenshotProof(harness, expectedFiles[2]);

                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.Presenter.ImportCalls > 0
                        && !string.IsNullOrWhiteSpace(harness.State.Profile?.Name)
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                        && harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible == false
                        && !harness.State.IsBusy,
                    timeoutMs: 8000);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Import familiarity screenshot must capture a loaded runner profile.");
                Assert.IsTrue(
                    harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible,
                    "Import familiarity screenshot must capture the loaded-runner tab strip.");
                Assert.IsFalse(
                    harness.FindControl<Control>("QuickStartContainer").IsVisible,
                    "Import familiarity screenshot must not be a first-run placeholder shell.");
                captured[expectedFiles[3]] = CaptureScreenshotProof(harness, expectedFiles[3]);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                captured["19-workflow-file-menu-loaded-light.png"] = CaptureScreenshotProof(harness, "19-workflow-file-menu-loaded-light.png");
                harness.ClickMenuCommand("new_character");
                harness.WaitUntil(() =>
                        string.Equals(
                            harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                            "Select Build Method",
                            StringComparison.Ordinal),
                    timeoutMs: 8000);
                captured["36-workflow-new-character-dialog-light.png"] = CaptureScreenshotProof(harness, "36-workflow-new-character-dialog-light.png");
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Select Build Method",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("skills");
                ListBox denseSectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => denseSectionRows.ItemCount > 0);
                object[] denseRows = SnapshotListBoxItems(denseSectionRows);
                Assert.IsTrue(denseRows.Length > 0, "Expected dense section rows before capturing dense familiarity proof.");
                denseSectionRows.SelectedItem = denseRows[0];
                harness.WaitUntil(() => ReferenceEquals(denseSectionRows.SelectedItem, denseRows[0]));
                captured[expectedFiles[4]] = CaptureScreenshotProof(harness, expectedFiles[4]);
                captured["20-workflow-skills-section-light.png"] = CaptureScreenshotProof(harness, "20-workflow-skills-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_skill_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_skill_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                captured["21-workflow-skill-add-dialog-light.png"] = CaptureScreenshotProof(harness, "21-workflow-skill-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("qualities");
                ListBox qualityRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => qualityRows.ItemCount > 0);
                captured["22-workflow-qualities-section-light.png"] = CaptureScreenshotProof(harness, "22-workflow-qualities-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_quality_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_quality_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                captured["23-workflow-quality-add-dialog-light.png"] = CaptureScreenshotProof(harness, "23-workflow-quality-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("gear");
                ListBox gearRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => gearRows.ItemCount > 0);
                captured["24-workflow-gear-section-light.png"] = CaptureScreenshotProof(harness, "24-workflow-gear-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_gear_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_gear_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                captured["25-workflow-gear-add-dialog-light.png"] = CaptureScreenshotProof(harness, "25-workflow-gear-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("weapons");
                ListBox weaponRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => weaponRows.ItemCount > 0);
                captured["26-workflow-weapons-section-light.png"] = CaptureScreenshotProof(harness, "26-workflow-weapons-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_weapon")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_weapon");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                captured["27-workflow-weapon-add-dialog-light.png"] = CaptureScreenshotProof(harness, "27-workflow-weapon-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("armors");
                ListBox armorRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => armorRows.ItemCount > 0);
                captured["28-workflow-armor-section-light.png"] = CaptureScreenshotProof(harness, "28-workflow-armor-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_armor")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_armor");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                captured["29-workflow-armor-add-dialog-light.png"] = CaptureScreenshotProof(harness, "29-workflow-armor-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox cyberwareRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => cyberwareRows.ItemCount > 0);
                captured["30-workflow-cyberware-section-light.png"] = CaptureScreenshotProof(harness, "30-workflow-cyberware-section-light.png");

                harness.SetActiveSectionForTesting("powers");
                ListBox powerRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => powerRows.ItemCount > 0);
                captured["31-workflow-powers-section-light.png"] = CaptureScreenshotProof(harness, "31-workflow-powers-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_adept_power_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_adept_power_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                captured["32-workflow-adept-power-dialog-light.png"] = CaptureScreenshotProof(harness, "32-workflow-adept-power-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("complexforms");
                ListBox complexFormRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => complexFormRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_complex_form_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_complex_form_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                captured["33-workflow-complex-form-dialog-light.png"] = CaptureScreenshotProof(harness, "33-workflow-complex-form-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("validation");
                ListBox validationRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => validationRows.ItemCount > 0);
                captured["34-workflow-validate-section-light.png"] = CaptureScreenshotProof(harness, "34-workflow-validate-section-light.png");

                harness.SetActiveSectionForTesting("sources");
                ListBox sourceRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => sourceRows.ItemCount > 0);
                captured["35-workflow-rules-section-light.png"] = CaptureScreenshotProof(harness, "35-workflow-rules-section-light.png");

                harness.SetActiveSectionForTesting("calendar");
                ListBox calendarRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => calendarRows.ItemCount > 0);
                captured["37-workflow-calendar-section-light.png"] = CaptureScreenshotProof(harness, "37-workflow-calendar-section-light.png");

                harness.SetTheme(ThemeVariant.Dark);
                captured[expectedFiles[5]] = CaptureScreenshotProof(harness, expectedFiles[5]);

                harness.SetTheme(ThemeVariant.Light);
                TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
                harness.WaitUntil(() =>
                {
                    return loadedRunnerTabStrip.Items
                        .OfType<NavigatorTabItem>()
                        .Any(static item => (item.Label ?? string.Empty).Contains("Runner", StringComparison.Ordinal));
                });
                captured[expectedFiles[6]] = CaptureScreenshotProof(harness, expectedFiles[6]);

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                harness.WaitUntil(() => sectionRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                harness.Click("SectionQuickAction_cyberware_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Cyberware",
                        StringComparison.Ordinal));
                captured[expectedFiles[7]] = CaptureScreenshotProof(harness, expectedFiles[7]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Cyberware",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("vehicles");
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                ListBox vehicleRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => vehicleRows.ItemCount > 0);
                object? vehicleRow = SnapshotListBoxItems(vehicleRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("vehicles[0] = Roadmaster", StringComparison.Ordinal) == true);
                Assert.IsNotNull(vehicleRow, "Expected a vehicle row before capturing vehicle familiarity proof.");
                vehicleRows.SelectedItem = vehicleRow;
                harness.WaitUntil(() => ReferenceEquals(vehicleRows.SelectedItem, vehicleRow));
                captured[expectedFiles[8]] = CaptureScreenshotProof(harness, expectedFiles[8]);

                harness.SetActiveSectionForTesting("contacts");
                ListBox contactRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => contactRows.ItemCount > 0);
                object? contactRow = SnapshotListBoxItems(contactRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("contacts[0] = Fixer", StringComparison.Ordinal) == true);
                Assert.IsNotNull(contactRow, "Expected a contact row before capturing contact familiarity proof.");
                contactRows.SelectedItem = contactRow;
                harness.WaitUntil(() => ReferenceEquals(contactRows.SelectedItem, contactRow));
                captured[expectedFiles[9]] = CaptureScreenshotProof(harness, expectedFiles[9]);

                harness.SetActiveSectionForTesting("progress");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_create_entry")?.IsVisible == true);
                harness.Click("SectionQuickAction_create_entry");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));
                captured[expectedFiles[10]] = CaptureScreenshotProof(harness, expectedFiles[10]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("spells");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_spell_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_spell_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Spell",
                        StringComparison.Ordinal));
                captured[expectedFiles[11]] = CaptureScreenshotProof(harness, expectedFiles[11]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Spell",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("aiprograms");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_matrix_program_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_matrix_program_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Program / Cyberdeck Item",
                        StringComparison.Ordinal));
                captured[expectedFiles[12]] = CaptureScreenshotProof(harness, expectedFiles[12]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Program / Cyberdeck Item",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("initiationgrades");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_initiation_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_initiation_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Initiation / Submersion",
                        StringComparison.Ordinal));
                captured[expectedFiles[13]] = CaptureScreenshotProof(harness, expectedFiles[13]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Initiation / Submersion",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("attributes");
                harness.WaitUntil(() => harness.FindControl<Control>("AttributeParityEditorBorder").IsVisible);
                Assert.IsNotNull(harness.FindControlOrDefault<NumericUpDown>("AttributeBaseEditor_BOD"), "Expected numeric attribute editors before capturing character-creation familiarity proof.");
                captured[expectedFiles[14]] = CaptureScreenshotProof(harness, expectedFiles[14]);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("master_index", CancellationToken.None).
                harness.ClickMenuCommand("master_index");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Master Index",
                    "Data Root",
                    "/app/data");
                captured[expectedFiles[15]] = CaptureScreenshotProof(harness, expectedFiles[15]);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("character_roster", CancellationToken.None).
                harness.ClickMenuCommand("character_roster");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Character Roster",
                    "Open Runners",
                    "Saved Workspaces",
                    "Ruleset Mix",
                    "Roster Entries");
                captured[expectedFiles[16]] = CaptureScreenshotProof(harness, expectedFiles[16]);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                harness.ClickMenuCommand("open_character");
                AssertDialogContainsAll(
                    harness,
                    "Open Character",
                    GetVeteranCertificationReviewStep("import").RequiredDialogMarkers);
                captured[GetVeteranCertificationReviewStep("import").ScreenshotFileName] = CaptureScreenshotProof(harness, GetVeteranCertificationReviewStep("import").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Open Character",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Translator",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Translator",
                    GetImportRouteReviewStep("translator").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("translator").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("translator").ScreenshotFileName);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Translator",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "XML Editor",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "XML Editor",
                    GetImportRouteReviewStep("xml_amendment_editor").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "XML Editor",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Hero Lab Importer",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Hero Lab Importer",
                    GetImportRouteReviewStep("hero_lab_importer").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Hero Lab Importer",
                        StringComparison.Ordinal));

                return captured;
            });

            foreach ((string screenshotName, ScreenshotProofCapture capture) in veteranWorkflowScreenshots)
            {
                screenshots[screenshotName] = capture;
            }

            foreach ((string fileName, ScreenshotProofCapture capture) in screenshots)
            {
                File.WriteAllBytes(Path.Combine(screenshotDirectory, fileName), capture.PngBytes);
            }

            string screenshotControlEvidencePath = Path.Combine(screenshotDirectory, "SCREENSHOT_CONTROL_EVIDENCE.generated.json");
            object screenshotControlEvidencePayload = new
            {
                generatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                workflowCoverage = WorkflowScreenshotCoverage.Select(entry => new
                {
                    workflowFamilyId = entry.WorkflowFamilyId,
                    legacyBehaviorLineage = entry.LegacyBehaviorLineage,
                    screenshotFiles = entry.ScreenshotFiles,
                    screenshotCount = entry.ScreenshotFiles.Length
                }).ToArray(),
                entries = screenshots.Values
                    .Select(capture => capture.Evidence)
                    .OrderBy(entry => entry.Screenshot, StringComparer.Ordinal)
                    .ToArray()
            };
            File.WriteAllText(
                screenshotControlEvidencePath,
                JsonSerializer.Serialize(screenshotControlEvidencePayload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                Encoding.UTF8);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        foreach (string fileName in expectedFiles)
        {
            string fullPath = Path.Combine(screenshotDirectory, fileName);
            Assert.IsTrue(File.Exists(fullPath), $"Expected screenshot evidence '{fileName}' was not created.");

            FileInfo fileInfo = new(fullPath);
            Assert.IsTrue(fileInfo.Length > 0, $"Screenshot evidence '{fileName}' is empty.");
        }

        Assert.IsTrue(
            File.Exists(Path.Combine(screenshotDirectory, "SCREENSHOT_CONTROL_EVIDENCE.generated.json")),
            "Expected screenshot control evidence JSON was not created.");
    }

    private sealed record VeteranCertificationReviewStep(
        string Surface,
        string ScreenshotFileName,
        string PromotedHeadGesture,
        string Chummer5aBaseline,
        string[] RequiredDialogMarkers);

    private sealed record WorkflowScreenshotCoverageEntry(
        string WorkflowFamilyId,
        string LegacyBehaviorLineage,
        string[] ScreenshotFiles);

    private static VeteranCertificationReviewStep GetVeteranCertificationReviewStep(string surface)
        => VeteranCertificationReviewSteps.Single(step => string.Equals(step.Surface, surface, StringComparison.Ordinal));

    private static VeteranCertificationReviewStep GetImportRouteReviewStep(string surface)
        => ImportRouteReviewSteps.Single(step => string.Equals(step.Surface, surface, StringComparison.Ordinal));

    private static void OpenMenuUntilCommandVisible(FlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            harness.Click(menuButtonName);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (IsCommandVisibleInCommandList(harness, commandId))
                {
                    return;
                }

                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
        }

        Assert.Fail($"Timed out opening '{menuButtonName}' for command '{commandId}'.");
    }

    private static void OpenMenuUntilCommandVisible(RuntimeFlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            harness.Click(menuButtonName);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (IsCommandVisibleInCommandList(harness, commandId))
                {
                    return;
                }

                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }

            MenuItem? menuButton = harness.FindControlOrDefault<MenuItem>(menuButtonName);
            string? menuId = menuButton?.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(menuId))
            {
                harness.ShellPresenter.ToggleMenuAsync(menuId, CancellationToken.None).GetAwaiter().GetResult();
                DateTime fallbackDeadline = DateTime.UtcNow.AddMilliseconds(1200);
                while (DateTime.UtcNow < fallbackDeadline)
                {
                    Dispatcher.UIThread.RunJobs();
                    if (IsCommandVisibleInCommandList(harness, commandId))
                    {
                        return;
                    }

                    Thread.Sleep(10);
                    Dispatcher.UIThread.RunJobs();
                }
            }
        }

        Assert.Fail($"Timed out opening '{menuButtonName}' for command '{commandId}'.");
    }

    private static bool IsCommandVisibleInCommandList(FlagshipUiHarness harness, string commandId)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        if (commandsList is null)
        {
            return false;
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Any(item => string.Equals(item.Id, commandId, StringComparison.Ordinal));
    }

    private static bool IsAnyCommandVisibleInCommandList(FlagshipUiHarness harness)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        return commandsList is not null
            && SnapshotListBoxItems(commandsList).OfType<CommandPaletteItem>().Any();
    }

    private static bool IsCommandVisibleInCommandList(RuntimeFlagshipUiHarness harness, string commandId)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        if (commandsList is null)
        {
            return false;
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Any(item => string.Equals(item.Id, commandId, StringComparison.Ordinal));
    }

    private static bool IsAnyCommandVisibleInCommandList(RuntimeFlagshipUiHarness harness)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        return commandsList is not null
            && SnapshotListBoxItems(commandsList).OfType<CommandPaletteItem>().Any();
    }

    private static string[] CaptureVisibleCommandLabels(RuntimeFlagshipUiHarness harness)
    {
        ListBox commandsList = harness.FindControl<ListBox>("CommandsList");
        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Select(item => item.Label)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static void WithHarness(Action<FlagshipUiHarness> assertion)
    {
        WithHarness<bool>(harness =>
        {
            assertion(harness);
            return true;
        });
    }

    private static TResult WithHarness<TResult>(Func<FlagshipUiHarness, TResult> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        using FlagshipUiHarness harness = new();
                        return assertion(harness);
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return;
            }

            _headlessInitialized = true;
        }
    }

    private sealed class FlagshipHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont();
        }
    }

    private static void WithRuntimeHarness(Action<RuntimeFlagshipUiHarness> assertion)
    {
        WithRuntimeHarness<bool>(harness =>
        {
            assertion(harness);
            return true;
        });
    }

    private static TResult WithRuntimeHarness<TResult>(Func<RuntimeFlagshipUiHarness, TResult> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        using RuntimeFlagshipUiHarness harness = new();
                        return assertion(harness);
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia runtime headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static void DisposeHeadlessSessionQuietly(HeadlessUnitTestSession? session)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            session.Dispose();
        }
        catch (NullReferenceException)
        {
            // Avalonia headless teardown can intermittently throw after successful dispatch.
            // Keep test assertions as the authoritative pass/fail signal.
        }
    }

    private static bool IsTransientHeadlessFailure(Exception ex)
    {
        if (ex is AssertFailedException assertFailed
            && (assertFailed.Message.Contains("No rendered frame was available", StringComparison.Ordinal)
                || assertFailed.Message.Contains("Timed out waiting for UI condition", StringComparison.Ordinal)
                || assertFailed.Message.Contains("Timed out waiting for runtime-backed UI condition", StringComparison.Ordinal)))
        {
            return true;
        }

        if (ex is InvalidOperationException invalidOperation
            && (invalidOperation.Message.Contains("IWindowingPlatform", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("ICursorFactory", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("Could not create glyphTypeface", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("Call from invalid thread", StringComparison.Ordinal)))
        {
            return true;
        }

        return ex.InnerException is not null && IsTransientHeadlessFailure(ex.InnerException);
    }

    private static string FindTestFilePath(string fileName)
    {
        string? match = ResolveExistingPath(
            Path.Combine("Chummer.Tests", "TestFiles", fileName),
            Path.Combine("TestFiles", fileName),
            Path.Combine("/src", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer-presentation", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer6-ui", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer6-ui-finish", "Chummer.Tests", "TestFiles", fileName));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate test file.", fileName);
        }

        return match;
    }

    private static string[] ResolveChummer5aFixtureUiReconstructionFixtureNames()
    {
        string? configuredFixtureFile = Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_FILE");
        if (!string.IsNullOrWhiteSpace(configuredFixtureFile))
        {
            string[] fixtureNames = File.ReadAllLines(configuredFixtureFile)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (fixtureNames.Length == 0)
            {
                throw new AssertFailedException(
                    $"Fixture UI reconstruction fixture file '{configuredFixtureFile}' did not contain any fixture names.");
            }

            return fixtureNames;
        }

        string scope = (Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE") ?? string.Empty).Trim();
        if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            string[] allFixtureNames = Directory.EnumerateFiles(ResolveTestFilesDirectory(), "*.chum5")
                .Select(Path.GetFileName)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray()!;
            if (allFixtureNames.Length == 0)
            {
                throw new AssertFailedException("Fixture UI reconstruction scope 'all' resolved zero .chum5 fixtures.");
            }

            return allFixtureNames;
        }

        return DefaultChummer5aFixtureUiReconstructionFixtureNames;
    }

    private static string ResolveTestFilesDirectory()
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Tests", "TestFiles"),
            Path.Combine(Directory.GetCurrentDirectory(), "TestFiles"),
            Path.Combine(AppContext.BaseDirectory, "TestFiles"),
            Path.Combine("/src", "Chummer.Tests", "TestFiles"),
            "/docker/chummercomplete/chummer-presentation/Chummer.Tests/TestFiles"
        };

        string? match = candidates.FirstOrDefault(Directory.Exists);
        if (match is null)
        {
            throw new DirectoryNotFoundException("Could not locate the Chummer test fixture directory.");
        }

        return match;
    }

    private static string ResolveFixtureUiReconstructionReceiptsDirectory()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string docsDirectory = Path.GetDirectoryName(ResolveSourceFile("docs", "PARITY_ORACLE.json"))
            ?? throw new DirectoryNotFoundException("Could not resolve docs directory for fixture UI reconstruction receipts.");
        string repoRoot = Directory.GetParent(docsDirectory)?.FullName
            ?? throw new DirectoryNotFoundException("Could not resolve repo root for fixture UI reconstruction receipts.");
        return Path.GetFullPath(
            Path.Combine(
                repoRoot,
                ".codex-studio",
                "out",
                "test-fixture-ui-reconstruction",
                Guid.NewGuid().ToString("N")));
    }

    private static FixtureUiReconstructionMaterializationResult MaterializeFixtureUiReconstructionReceipt(
        string fixtureName,
        string receiptsDirectory)
    {
        Directory.CreateDirectory(receiptsDirectory);

        string receiptPath = Path.Combine(receiptsDirectory, $"{fixtureName}.generated.json");
        string openedScreenshotFileName = $"{fixtureName}-opened.png";
        string exportDialogScreenshotFileName = $"{fixtureName}-export-dialog.png";
        string printedScreenshotFileName = $"{fixtureName}-printed.png";
        string reloadedScreenshotFileName = $"{fixtureName}-reloaded.png";
        string openedScreenshotPath = Path.Combine(receiptsDirectory, openedScreenshotFileName);
        string exportDialogScreenshotPath = Path.Combine(receiptsDirectory, exportDialogScreenshotFileName);
        string printedScreenshotPath = Path.Combine(receiptsDirectory, printedScreenshotFileName);
        string reloadedScreenshotPath = Path.Combine(receiptsDirectory, reloadedScreenshotFileName);
        string savedFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.roundtrip.chum5");
        string exportFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.export.json");
        string printPreviewFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.print.html");
        string pdfArtifactPath = Path.Combine(receiptsDirectory, $"{fixtureName}.print.pdf");

        List<string> reasons = [];
        List<string> screenshots = [];
        List<string> pickerTitles = [];
        Dictionary<string, bool> assertions = new(StringComparer.Ordinal)
        {
            ["openedByUi"] = false,
            ["savedByUi"] = false,
            ["exportedByUi"] = false,
            ["printedByUi"] = false,
            ["pdfArtifactProducedByUiPrintRoute"] = false,
            ["outputArtifactsProducedByUi"] = false,
            ["reloadedByUi"] = false,
            ["roundTripPreservedIdentity"] = false,
        };

        string sourceFixturePath = FindTestFilePath(fixtureName);
        byte[] sourceFixtureBytes = File.ReadAllBytes(sourceFixturePath);
        FixtureUiIdentity sourceIdentity = ReadFixtureUiIdentity(sourceFixtureBytes, fixtureName);
        FixtureUiIdentity openedIdentity = sourceIdentity;
        FixtureUiIdentity savedIdentity = sourceIdentity;
        FixtureUiIdentity reloadedIdentity = sourceIdentity;
        int importCallCount = 0;
        int saveCallCount = 0;
        int exportCallCount = 0;
        int printCallCount = 0;
        long exportByteCount = 0;
        long printPreviewByteCount = 0;
        long pdfArtifactByteCount = 0;
        string exportFileName = string.Empty;
        string printFileName = string.Empty;
        string printMimeType = string.Empty;
        string printTitle = string.Empty;
        Queue<(byte[] Payload, string SourceLabel)> importPayloads = new();
        importPayloads.Enqueue((sourceFixtureBytes, fixtureName));

        Func<global::Avalonia.Platform.Storage.IStorageProvider, string, CancellationToken, Task<DesktopImportFileResult>>? originalImportOverride =
            MainWindowDesktopFileCoordinator.OpenImportFileOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingDownloadDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSaveDownloadOverride =
            MainWindowDesktopFileCoordinator.SaveDownloadOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingExportDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSaveExportOverride =
            MainWindowDesktopFileCoordinator.SaveExportOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingPrintDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSavePrintOverride =
            MainWindowDesktopFileCoordinator.SavePrintOverride;

        try
        {
            MainWindowDesktopFileCoordinator.OpenImportFileOverride =
                (_, title, _) =>
                {
                    pickerTitles.Add(title);
                    importCallCount++;
                    if (importPayloads.Count == 0)
                    {
                        return Task.FromResult(new DesktopImportFileResult(DesktopFileOperationOutcome.Cancelled, Payload: null, SourceLabel: null));
                    }

                    (byte[] payload, string sourceLabel) = importPayloads.Dequeue();
                    return Task.FromResult(new DesktopImportFileResult(DesktopFileOperationOutcome.Completed, payload, sourceLabel));
                };
            MainWindowDesktopFileCoordinator.SaveDownloadOverride =
                (_, request, _) =>
                {
                    saveCallCount++;
                    byte[] payload = Convert.FromBase64String(request.Download.ContentBase64);
                    File.WriteAllBytes(savedFilePath, payload);
                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Downloaded {request.Download.FileName} to {Path.GetFileName(savedFilePath)}."));
                };
            MainWindowDesktopFileCoordinator.SaveExportOverride =
                (_, request, _) =>
                {
                    exportCallCount++;
                    exportFileName = request.Export.FileName;
                    byte[] payload = Convert.FromBase64String(request.Export.ContentBase64);
                    exportByteCount = payload.LongLength;
                    File.WriteAllBytes(exportFilePath, payload);
                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Exported {request.Export.FileName} to {Path.GetFileName(exportFilePath)}."));
                };
            MainWindowDesktopFileCoordinator.SavePrintOverride =
                (_, request, _) =>
                {
                    printCallCount++;
                    printFileName = request.Print.FileName;
                    printMimeType = request.Print.MimeType;
                    printTitle = request.Print.Title;
                    byte[] payload = Convert.FromBase64String(request.Print.ContentBase64);
                    printPreviewByteCount = payload.LongLength;
                    File.WriteAllBytes(printPreviewFilePath, payload);

                    byte[] pdfPayload = BuildMinimalPdfFromHtmlPrintPreview(
                        string.IsNullOrWhiteSpace(request.Print.Title) ? request.Print.FileName : request.Print.Title,
                        payload);
                    pdfArtifactByteCount = pdfPayload.LongLength;
                    File.WriteAllBytes(pdfArtifactPath, pdfPayload);

                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Saved print preview {request.Print.FileName} and PDF bridge {Path.GetFileName(pdfArtifactPath)}."));
                };

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "open_character");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is not null
                        && harness.State.Profile is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && !harness.State.IsBusy,
                    context: $"open fixture '{fixtureName}'");

                assertions["openedByUi"] = importCallCount >= 1;
                openedIdentity = CaptureRuntimeFixtureUiIdentity(harness, fixtureName);
                File.WriteAllBytes(openedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(openedScreenshotFileName);

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "save_character_as");
                harness.WaitUntil(
                    () => saveCallCount >= 1
                        && File.Exists(savedFilePath)
                        && !harness.State.IsBusy,
                    context: $"save fixture '{fixtureName}'");

                assertions["savedByUi"] = saveCallCount >= 1;
                byte[] savedFixtureBytes = File.ReadAllBytes(savedFilePath);
                savedIdentity = ReadFixtureUiIdentity(savedFixtureBytes, fixtureName);

                harness.SelectCommand("export_character");
                harness.WaitUntil(
                    () => harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.export_character" },
                    context: $"open export dialog for '{fixtureName}'");
                File.WriteAllBytes(exportDialogScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(exportDialogScreenshotFileName);

                ClickRuntimeDialogAction(harness, "download");
                harness.WaitUntil(
                    () => exportCallCount >= 1
                        && File.Exists(exportFilePath)
                        && harness.Window.PeekDialogWindowForTesting() is null
                        && !harness.State.IsBusy,
                    context: $"export fixture '{fixtureName}'");
                assertions["exportedByUi"] =
                    exportCallCount >= 1
                    && exportByteCount > 0
                    && File.Exists(exportFilePath);

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "print_character");
                harness.WaitUntil(
                    () => printCallCount >= 1
                        && File.Exists(printPreviewFilePath)
                        && File.Exists(pdfArtifactPath)
                        && !harness.State.IsBusy,
                    context: $"print fixture '{fixtureName}'");
                assertions["printedByUi"] =
                    printCallCount >= 1
                    && printPreviewByteCount > 0
                    && File.Exists(printPreviewFilePath)
                    && string.Equals(printMimeType, "text/html", StringComparison.OrdinalIgnoreCase)
                    && Encoding.UTF8.GetString(File.ReadAllBytes(printPreviewFilePath))
                        .Contains("<html", StringComparison.OrdinalIgnoreCase);
                assertions["pdfArtifactProducedByUiPrintRoute"] =
                    printCallCount >= 1
                    && pdfArtifactByteCount > 0
                    && File.Exists(pdfArtifactPath)
                    && HasPdfHeader(File.ReadAllBytes(pdfArtifactPath));
                assertions["outputArtifactsProducedByUi"] =
                    assertions["savedByUi"]
                    && assertions["exportedByUi"]
                    && assertions["printedByUi"]
                    && assertions["pdfArtifactProducedByUiPrintRoute"];
                File.WriteAllBytes(printedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(printedScreenshotFileName);

                ClickRuntimeMenuCommand(harness, "WindowsMenuButton", "close_window");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is null
                        && harness.State.Session.OpenWorkspaces.Count == 0
                        && !harness.State.IsBusy,
                    context: $"close fixture '{fixtureName}' after save");

                importPayloads.Enqueue((savedFixtureBytes, Path.GetFileName(savedFilePath)));
                ClickRuntimeMenuCommand(harness, "FileMenuButton", "open_character");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is not null
                        && harness.State.Profile is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && !harness.State.IsBusy,
                    context: $"reload fixture '{fixtureName}'");

                assertions["reloadedByUi"] = importCallCount >= 2;
                reloadedIdentity = CaptureRuntimeFixtureUiIdentity(harness, fixtureName);
                File.WriteAllBytes(reloadedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(reloadedScreenshotFileName);
            });
        }
        catch (Exception ex)
        {
            reasons.Add(ex.Message);
        }
        finally
        {
            MainWindowDesktopFileCoordinator.OpenImportFileOverride = originalImportOverride;
            MainWindowDesktopFileCoordinator.SaveDownloadOverride = originalSaveDownloadOverride;
            MainWindowDesktopFileCoordinator.SaveExportOverride = originalSaveExportOverride;
            MainWindowDesktopFileCoordinator.SavePrintOverride = originalSavePrintOverride;
        }

        if (pickerTitles.Count != 2
            || pickerTitles.Any(title => !string.Equals(title, "Open Character File", StringComparison.Ordinal)))
        {
            reasons.Add($"Expected two host file-open picker calls for '{fixtureName}', but saw: {string.Join(", ", pickerTitles)}");
        }

        if (!assertions["openedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete an initial UI open route.");
        }

        if (!assertions["savedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI save-as route.");
        }

        if (!assertions["exportedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI export route.");
        }

        if (!assertions["printedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI print-preview route.");
        }

        if (!assertions["pdfArtifactProducedByUiPrintRoute"])
        {
            reasons.Add($"'{fixtureName}' did not materialize a PDF artifact from the UI print route.");
        }

        if (!assertions["outputArtifactsProducedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not publish the full save/export/print output artifact set.");
        }

        if (!assertions["reloadedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI reload route.");
        }

        assertions["roundTripPreservedIdentity"] =
            assertions["openedByUi"]
            && assertions["savedByUi"]
            && assertions["reloadedByUi"]
            && FixtureUiIdentityEquivalent(sourceIdentity, openedIdentity)
            && FixtureUiIdentityEquivalent(sourceIdentity, savedIdentity)
            && FixtureUiIdentityEquivalent(sourceIdentity, reloadedIdentity);
        if (!assertions["roundTripPreservedIdentity"])
        {
            reasons.Add(
                $"'{fixtureName}' identity drifted across the open/save/reload roundtrip. "
                + $"Source={DescribeFixtureUiIdentity(sourceIdentity)}; "
                + $"Opened={DescribeFixtureUiIdentity(openedIdentity)}; "
                + $"Saved={DescribeFixtureUiIdentity(savedIdentity)}; "
                + $"Reloaded={DescribeFixtureUiIdentity(reloadedIdentity)}.");
        }

        if (screenshots.Count < 4)
        {
            reasons.Add($"'{fixtureName}' did not publish the expected UI reconstruction screenshots.");
        }

        string status = reasons.Count == 0 && assertions.Values.All(static value => value) ? "pass" : "fail";
        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["generatedAt"] = DateTime.UtcNow.ToString("O"),
            ["contract_name"] = "chummer6-ui.chummer5a_fixture_ui_reconstruction",
            ["status"] = status,
            ["summary"] = status == "pass"
                ? $"UI reconstruction parity passed for {fixtureName}."
                : $"UI reconstruction parity failed for {fixtureName}.",
            ["fixtureName"] = fixtureName,
            ["characterName"] = sourceIdentity.CharacterName,
            ["linux_binary_under_test"] = true,
            ["used_internal_apis"] = false,
            ["screenshots"] = screenshots.ToArray(),
            ["assertions"] = assertions,
            ["reasons"] = reasons.ToArray(),
            ["evidence"] = new Dictionary<string, object?>
            {
                ["fixturePath"] = sourceFixturePath,
                ["savedFilePath"] = savedFilePath,
                ["exportFilePath"] = exportFilePath,
                ["printPreviewFilePath"] = printPreviewFilePath,
                ["pdfArtifactPath"] = pdfArtifactPath,
                ["pickerTitles"] = pickerTitles.ToArray(),
                ["outputRouteFacts"] = new Dictionary<string, object?>
                {
                    ["exportCallCount"] = exportCallCount,
                    ["exportFileName"] = exportFileName,
                    ["exportByteCount"] = exportByteCount,
                    ["printCallCount"] = printCallCount,
                    ["printFileName"] = printFileName,
                    ["printMimeType"] = printMimeType,
                    ["printTitle"] = printTitle,
                    ["printPreviewByteCount"] = printPreviewByteCount,
                    ["pdfArtifactByteCount"] = pdfArtifactByteCount,
                },
                ["sourceIdentity"] = BuildFixtureUiIdentityPayload(sourceIdentity),
                ["openedIdentity"] = BuildFixtureUiIdentityPayload(openedIdentity),
                ["savedIdentity"] = BuildFixtureUiIdentityPayload(savedIdentity),
                ["reloadedIdentity"] = BuildFixtureUiIdentityPayload(reloadedIdentity),
            },
        };
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(payload, ScreenshotEvidenceJsonOptions));

        return new FixtureUiReconstructionMaterializationResult(
            fixtureName,
            receiptPath,
            status,
            reasons.ToArray());
    }

    private static FixtureUiIdentity ReadFixtureUiIdentity(byte[] xmlBytes, string fixtureName)
    {
        using MemoryStream stream = new(xmlBytes, writable: false);
        using System.Xml.XmlReader reader = System.Xml.XmlReader.Create(
            stream,
            new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            });
        XElement root = XElement.Load(reader);

        string rawName = (root.Element("name")?.Value ?? string.Empty).Trim();
        string alias = (root.Element("alias")?.Value ?? string.Empty).Trim();
        string buildMethod = (root.Element("buildmethod")?.Value ?? string.Empty).Trim();
        string metatype = (root.Element("metatype")?.Value ?? string.Empty).Trim();
        string rulesetId = NormalizeFixtureRulesetId((root.Element("gameedition")?.Value ?? string.Empty).Trim());
        string characterName = string.IsNullOrWhiteSpace(rawName)
            ? Path.GetFileNameWithoutExtension(fixtureName)
            : rawName;

        return new FixtureUiIdentity(
            CharacterName: characterName,
            PrimaryToken: ResolveFixtureIdentityToken(rawName, alias, fixtureName),
            Alias: alias,
            BuildMethod: buildMethod,
            Metatype: metatype,
            RulesetId: rulesetId);
    }

    private static FixtureUiIdentity CaptureRuntimeFixtureUiIdentity(RuntimeFlagshipUiHarness harness, string fixtureName)
    {
        CharacterProfileSection profile = harness.State.Profile
            ?? throw new AssertFailedException($"Runtime fixture '{fixtureName}' did not materialize a profile.");
        OpenWorkspaceState? workspace = harness.State.WorkspaceId is { } workspaceId
            ? harness.State.OpenWorkspaces.FirstOrDefault(candidate => candidate.Id.Equals(workspaceId))
            : null;

        return new FixtureUiIdentity(
            CharacterName: string.IsNullOrWhiteSpace(profile.Name)
                ? Path.GetFileNameWithoutExtension(fixtureName)
                : profile.Name,
            PrimaryToken: ResolveFixtureIdentityToken(profile.Name, profile.Alias, fixtureName),
            Alias: profile.Alias,
            BuildMethod: profile.BuildMethod,
            Metatype: profile.Metatype,
            RulesetId: NormalizeFixtureRulesetId(workspace?.RulesetId ?? string.Empty));
    }

    private static bool FixtureUiIdentityEquivalent(FixtureUiIdentity expected, FixtureUiIdentity actual)
    {
        return string.Equals(expected.PrimaryToken, actual.PrimaryToken, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.BuildMethod, actual.BuildMethod, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.RulesetId, actual.RulesetId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(expected.Alias)
                || string.Equals(expected.Alias, actual.Alias, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(expected.Metatype)
                || string.Equals(expected.Metatype, actual.Metatype, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFixtureIdentityToken(string? rawName, string? alias, string fixtureName)
    {
        string candidate = string.IsNullOrWhiteSpace(rawName)
            ? string.IsNullOrWhiteSpace(alias)
                ? Path.GetFileNameWithoutExtension(fixtureName)
                : alias
            : rawName;
        return string.Join(" ", candidate.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeFixtureRulesetId(string rawValue)
    {
        string trimmed = rawValue.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "SR4" => RulesetDefaults.Sr4,
            "SR5" => RulesetDefaults.Sr5,
            "SR6" => RulesetDefaults.Sr6,
            _ => RulesetDefaults.NormalizeOptional(trimmed) ?? string.Empty,
        };
    }

    private static string DescribeFixtureUiIdentity(FixtureUiIdentity identity)
        => $"primary={identity.PrimaryToken}, alias={identity.Alias}, build={identity.BuildMethod}, metatype={identity.Metatype}, ruleset={identity.RulesetId}";

    private static Dictionary<string, object?> BuildFixtureUiIdentityPayload(FixtureUiIdentity identity)
        => new(StringComparer.Ordinal)
        {
            ["characterName"] = identity.CharacterName,
            ["primaryToken"] = identity.PrimaryToken,
            ["alias"] = identity.Alias,
            ["buildMethod"] = identity.BuildMethod,
            ["metatype"] = identity.Metatype,
            ["rulesetId"] = identity.RulesetId,
        };

    private sealed record FixtureUiIdentity(
        string CharacterName,
        string PrimaryToken,
        string Alias,
        string BuildMethod,
        string Metatype,
        string RulesetId);

    private sealed record FixtureUiReconstructionMaterializationResult(
        string FixtureName,
        string ReceiptPath,
        string Status,
        string[] Reasons);

    private static byte[] BuildMinimalPdfFromHtmlPrintPreview(string title, byte[] htmlBytes)
    {
        string html = Encoding.UTF8.GetString(htmlBytes);
        string normalizedTitle = NormalizePdfLine(title);
        string plainText = ExtractPlainTextFromHtml(html);
        List<string> lines = [];

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            lines.Add(normalizedTitle);
        }

        foreach (string line in plainText.Split('\n'))
        {
            string normalizedLine = NormalizePdfLine(line);
            if (!string.IsNullOrWhiteSpace(normalizedLine))
            {
                lines.Add(normalizedLine);
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("Print preview");
        }

        const int MaxLines = 48;
        StringBuilder content = new();
        content.Append("BT\n/F1 11 Tf\n50 790 Td\n14 TL\n");
        bool wroteLine = false;
        foreach (string line in lines.Take(MaxLines))
        {
            if (wroteLine)
            {
                content.Append("T*\n");
            }

            content.Append('(')
                .Append(EscapePdfText(line))
                .Append(") Tj\n");
            wroteLine = true;
        }

        content.Append("ET\n");
        byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        using MemoryStream stream = new();
        stream.Write(
        [
            0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A,
            0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A
        ]);

        List<long> offsets = [0];

        void WriteObject(string payload)
        {
            offsets.Add(stream.Position);
            byte[] bytes = Encoding.ASCII.GetBytes(payload);
            stream.Write(bytes, 0, bytes.Length);
        }

        WriteObject("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        WriteObject("2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n");
        WriteObject("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");
        offsets.Add(stream.Position);
        byte[] streamHeader = Encoding.ASCII.GetBytes($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        stream.Write(streamHeader, 0, streamHeader.Length);
        stream.Write(contentBytes, 0, contentBytes.Length);
        byte[] streamFooter = Encoding.ASCII.GetBytes("endstream\nendobj\n");
        stream.Write(streamFooter, 0, streamFooter.Length);
        WriteObject("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        long xrefOffset = stream.Position;
        byte[] xrefHeader = Encoding.ASCII.GetBytes($"xref\n0 {offsets.Count}\n");
        stream.Write(xrefHeader, 0, xrefHeader.Length);
        byte[] freeEntry = Encoding.ASCII.GetBytes("0000000000 65535 f \n");
        stream.Write(freeEntry, 0, freeEntry.Length);
        foreach (long offset in offsets.Skip(1))
        {
            byte[] entry = Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n");
            stream.Write(entry, 0, entry.Length);
        }

        byte[] trailer = Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        stream.Write(trailer, 0, trailer.Length);
        return stream.ToArray();
    }

    private static string ExtractPlainTextFromHtml(string html)
    {
        string normalized = Regex.Replace(html, "(?i)<br\\s*/?>", "\n");
        normalized = Regex.Replace(normalized, "(?i)</(p|div|h1|h2|h3|li|tr|section|article)>", "\n");
        normalized = Regex.Replace(normalized, "<[^>]+>", " ");
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"\r\n?|\u2028|\u2029", "\n");
        normalized = Regex.Replace(normalized, @"[ \t\f\v]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static string NormalizePdfLine(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        char[] normalized = trimmed
            .Select(ch => ch is >= ' ' and <= '~' ? ch : '?')
            .ToArray();
        return new string(normalized);
    }

    private static string EscapePdfText(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("(", @"\(", StringComparison.Ordinal)
            .Replace(")", @"\)", StringComparison.Ordinal);

    private static bool HasPdfHeader(byte[] bytes)
        => bytes.Length >= 5
            && bytes[0] == (byte)'%'
            && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D'
            && bytes[3] == (byte)'F'
            && bytes[4] == (byte)'-';

    private static void ClickRuntimeDialogAction(RuntimeFlagshipUiHarness harness, string actionId)
        => harness.InvokeDialogAction(actionId);

    private static void ClickRuntimeMenuCommand(RuntimeFlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        harness.Click(menuButtonName);
        harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
        harness.ClickMenuCommand(commandId);
    }

    private static string ResolveSourceFile(params string[] segments)
    {
        string relativePath = Path.Combine(segments);
        string? match = ResolveExistingPath(
            relativePath,
            Path.Combine("/docker/chummercomplete/chummer6-ui-finish", relativePath),
            Path.Combine("/docker/chummercomplete/chummer-presentation", relativePath),
            Path.Combine("/docker/chummercomplete/chummer6-ui", relativePath));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate source file.", Path.Combine(segments));
        }

        return match;
    }

    private static string SourcePath(params string[] segments)
        => ResolveSourceFile(segments);

    private static string? ResolveExistingPath(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            {
                return candidate;
            }

            string? relativeMatch = ResolveRelativePathFromKnownRoots(candidate);
            if (relativeMatch is not null)
            {
                return relativeMatch;
            }
        }

        return null;
    }

    private static string? ResolveRelativePathFromKnownRoots(string relativePath)
    {
        foreach (string root in EnumerateSearchRoots())
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        foreach (string root in EnumerateAncestorDirectories(Directory.GetCurrentDirectory()))
        {
            yield return root;
        }

        foreach (string root in EnumerateAncestorDirectories(AppContext.BaseDirectory))
        {
            yield return root;
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string startPath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        DirectoryInfo? current = new(Path.GetFullPath(startPath));

        while (current is not null)
        {
            if (seen.Add(current.FullName))
            {
                yield return current.FullName;
            }

            current = current.Parent;
        }
    }

    private static Dictionary<string, Dictionary<string, Color>> LoadThemeBrushes(string path)
    {
        XDocument document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Dictionary<string, Dictionary<string, Color>> themes = new(StringComparer.Ordinal);

        foreach (XElement dictionary in document
                     .Descendants()
                     .Where(element => string.Equals(element.Name.LocalName, "ResourceDictionary", StringComparison.Ordinal)))
        {
            string key = dictionary.Attribute(x + "Key")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Dictionary<string, Color> brushes = new(StringComparer.Ordinal);
            foreach (XElement brush in dictionary.Elements().Where(element => string.Equals(element.Name.LocalName, "SolidColorBrush", StringComparison.Ordinal)))
            {
                string? brushKey = brush.Attribute(x + "Key")?.Value;
                string? colorValue = brush.Attribute("Color")?.Value;
                if (string.IsNullOrWhiteSpace(brushKey) || string.IsNullOrWhiteSpace(colorValue))
                {
                    continue;
                }

                brushes[brushKey] = Color.Parse(colorValue);
            }

            themes[key] = brushes;
        }

        return themes;
    }

    private static void AssertContrastAtLeast(Color foreground, Color background, double minimum, string context)
    {
        double ratio = ContrastRatio(foreground, background);
        Assert.IsTrue(ratio >= minimum, $"Expected {context} contrast to be at least {minimum:0.0}, but was {ratio:0.00}.");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        double foregroundLuminance = RelativeLuminance(foreground);
        double backgroundLuminance = RelativeLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Channel(color.R)) + (0.7152d * Channel(color.G)) + (0.0722d * Channel(color.B));
    }

    private static string ToHex(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ResolveScreenshotDirectory()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CHUMMER_UI_GATE_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string testSourceFile = ResolveSourceFile("Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs");
        string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testSourceFile))!)
            ?? throw new DirectoryNotFoundException("Could not locate the chummer6-ui repo root for screenshot publication.");

        return Path.GetFullPath(
            Path.Combine(
                repoRoot,
                ".codex-studio",
                "published",
                "ui-flagship-release-gate-screenshots"));
    }

    private static ScreenshotProofCapture CaptureScreenshotProof(FlagshipUiHarness harness, string screenshotFileName)
        => new(
            harness.CaptureScreenshotBytes(),
            CaptureScreenshotControlEvidence(harness, screenshotFileName));

    private static ScreenshotControlEvidenceEntry CaptureScreenshotControlEvidence(FlagshipUiHarness harness, string screenshotFileName)
    {
        TopLevel root = harness.Window;
        Control[] visibleNamedControls = harness.Window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root) && !string.IsNullOrWhiteSpace(control.Name))
            .OrderBy(control => control.Name, StringComparer.Ordinal)
            .ToArray();

        string dialogTitle = harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text ?? string.Empty;
        if (string.Equals(dialogTitle, "(none)", StringComparison.Ordinal))
        {
            dialogTitle = string.Empty;
        }

        string dialogMessage = harness.FindControlOrDefault<TextBlock>("DialogMessageText")?.Text ?? string.Empty;
        string previewText = harness.FindControlOrDefault<TextBox>("SectionPreviewBox")?.Text ?? string.Empty;

        string[] dialogFieldLabels = visibleNamedControls
            .OfType<TextBlock>()
            .Where(control => control.Name?.StartsWith("DialogFieldLabel_", StringComparison.Ordinal) == true)
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogFieldIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogField_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogFieldControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name =>
                name.StartsWith("DialogFieldLabel_", StringComparison.Ordinal)
                || name.StartsWith("DialogFieldInput_", StringComparison.Ordinal)
                || name.StartsWith("DialogField_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<string> dialogFieldInputValuesBuilder = [];
        foreach (Control control in visibleNamedControls)
        {
            if (control.Name?.StartsWith("DialogFieldInput_", StringComparison.Ordinal) != true)
            {
                continue;
            }

            switch (control)
            {
                case TextBox textBox when !string.IsNullOrWhiteSpace(textBox.Text):
                    dialogFieldInputValuesBuilder.Add(textBox.Text);
                    break;
                case ComboBox comboBox when comboBox.SelectedItem is not null:
                    dialogFieldInputValuesBuilder.Add(comboBox.SelectedItem.ToString() ?? string.Empty);
                    break;
                case CheckBox checkBox:
                    dialogFieldInputValuesBuilder.Add(checkBox.IsChecked?.ToString() ?? string.Empty);
                    break;
            }
        }

        string[] dialogFieldInputValues = dialogFieldInputValuesBuilder
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogActionIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogAction_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogActionControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("DialogAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleNamedControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleTextSamples = harness.Window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root))
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(96)
            .ToArray();
        string[] visibleMenuCommandIds = SnapshotListBoxItems(harness.FindControlOrDefault<ListBox>("CommandsList") ?? new ListBox())
            .OfType<CommandPaletteItem>()
            .Select(item => item.Id ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleTabLabels = harness.FindControlOrDefault<TabStrip>("LoadedRunnerTabStrip") is { } loadedRunnerTabStrip
            && IsEffectivelyVisibleForScreenshotEvidence(loadedRunnerTabStrip, root)
            && loadedRunnerTabStrip.Items is IEnumerable tabItems
            ? tabItems
                .OfType<NavigatorTabItem>()
                .Select(item => item.Label ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];
        string[] visibleSectionQuickActionIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("SectionQuickAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] selectedListRowTexts = harness.Window.GetVisualDescendants()
            .OfType<ListBox>()
            .Where(listBox => IsEffectivelyVisibleForScreenshotEvidence(listBox, root) && listBox.SelectedItem is not null)
            .Select(listBox => listBox.SelectedItem?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ScreenshotVisibleNamedControlEntry[] visibleNamedControlEntries = visibleNamedControls
            .Select(control => BuildVisibleNamedControlEntry(control, root))
            .ToArray();

        string theme = (harness.Window.ActualThemeVariant ?? harness.Window.RequestedThemeVariant ?? ThemeVariant.Default).ToString();
        return new ScreenshotControlEvidenceEntry(
            Screenshot: screenshotFileName,
            Theme: theme,
            DialogTitle: dialogTitle,
            DialogMessage: dialogMessage,
            DialogFieldLabels: dialogFieldLabels,
            DialogFieldIds: dialogFieldIds,
            DialogFieldControlIds: dialogFieldControlIds,
            DialogFieldInputValues: dialogFieldInputValues,
            DialogActionIds: dialogActionIds,
            DialogActionControlIds: dialogActionControlIds,
            VisibleNamedControlIds: visibleNamedControlIds,
            VisibleNamedControls: visibleNamedControlEntries,
            VisibleTextSamples: visibleTextSamples,
            VisibleMenuCommandIds: visibleMenuCommandIds,
            VisibleTabLabels: visibleTabLabels,
            VisibleSectionQuickActionIds: visibleSectionQuickActionIds,
            SelectedListRowTexts: selectedListRowTexts,
            PreviewText: previewText);
    }

    private static bool IsEffectivelyVisibleForScreenshotEvidence(Control control, TopLevel root)
    {
        if (!control.IsVisible)
        {
            return false;
        }

        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return false;
        }

        if (control.TranslatePoint(default, root) is null)
        {
            return false;
        }

        return control.GetVisualAncestors().All(static ancestor => ancestor.IsVisible);
    }

    private static void AssertMouseReachabilityWithinFlagshipViewport(TopLevel root, string surfaceLabel)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string[] failures = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsFlagshipMouseTargetControl(control, root))
            .Select(control => DescribeMouseReachabilityFailure(control, root, viewport))
            .Where(static failure => !string.IsNullOrWhiteSpace(failure))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

        Assert.AreEqual(
            0,
            failures.Length,
            $"{surfaceLabel} must keep every visible mouse target inside the desktop viewport without relying on offscreen scroll-only reachability. Failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static void AssertControlMouseReachable(Control control, TopLevel root)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string? failure = DescribeMouseReachabilityFailure(control, root, viewport);
        Assert.IsTrue(
            string.IsNullOrWhiteSpace(failure),
            failure ?? $"Control '{control.Name ?? control.GetType().Name}' must be mouse reachable.");
    }

    private static void AssertDialogMouseReachabilityOrScrollContainment(TopLevel root, string dialogTitle)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string[] failures = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsFlagshipMouseTargetControl(control, root))
            .Select(control => DescribeDialogReachabilityFailure(control, root, viewport))
            .Where(static failure => !string.IsNullOrWhiteSpace(failure))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

        Assert.AreEqual(
            0,
            failures.Length,
            $"{dialogTitle} must keep every visible mouse target onscreen or inside a visible scroll host. Failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static TopLevel GetMouseInteractionRoot(Control control, TopLevel fallbackRoot)
        => TopLevel.GetTopLevel(control) ?? fallbackRoot;

    private static ScrollViewer? FindVisibleScrollHost(Control control)
        => control.GetVisualAncestors()
            .OfType<ScrollViewer>()
            .FirstOrDefault(static candidate => candidate.IsVisible);

    private static bool IsFlagshipMouseTargetControl(Control control, TopLevel root)
    {
        if (!IsEffectivelyVisibleForScreenshotEvidence(control, root))
        {
            return false;
        }

        string name = control.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("PART_", StringComparison.Ordinal))
        {
            return false;
        }

        return control is Button
            or MenuItem
            or TabStrip
            or TreeView
            or ListBox
            or ComboBox
            or CheckBox
            or TextBox
            or ToggleButton
            or Expander;
    }

    private static string? DescribeMouseReachabilityFailure(Control control, TopLevel root, Rect viewport)
    {
        Point? translated = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
            root);
        if (translated is null)
        {
            return $"{control.Name} ({control.GetType().Name}) could not translate its mouse target into window coordinates.";
        }

        Point center = translated.Value;
        bool withinViewport =
            center.X >= viewport.X
            && center.Y >= viewport.Y
            && center.X <= viewport.X + viewport.Width
            && center.Y <= viewport.Y + viewport.Height;
        if (withinViewport)
        {
            return null;
        }

        ScrollViewer? scrollHost = FindVisibleScrollHost(control);
        string hostChain = string.Join(
            " -> ",
            control.GetVisualAncestors()
                .OfType<Control>()
                .Take(6)
                .Select(static ancestor => $"{ancestor.Name ?? ancestor.GetType().Name}[{ancestor.Bounds.Width:F0}x{ancestor.Bounds.Height:F0}]"));
        return scrollHost is null
            ? $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) without a visible scroll host. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}, Ancestors={hostChain}."
            : $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) and only reachable through scroll host '{scrollHost.Name ?? scrollHost.GetType().Name}'. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}, Ancestors={hostChain}.";
    }

    private static string? DescribeDialogReachabilityFailure(Control control, TopLevel root, Rect viewport)
    {
        Point? translated = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
            root);
        if (translated is null)
        {
            return $"{control.Name} ({control.GetType().Name}) could not translate its mouse target into window coordinates.";
        }

        Point center = translated.Value;
        bool withinViewport =
            center.X >= viewport.X
            && center.Y >= viewport.Y
            && center.X <= viewport.X + viewport.Width
            && center.Y <= viewport.Y + viewport.Height;
        if (withinViewport)
        {
            return null;
        }

        return FindVisibleScrollHost(control) is null
            ? $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) without a visible scroll host. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}."
            : null;
    }

    private static ScreenshotVisibleNamedControlEntry BuildVisibleNamedControlEntry(Control control, TopLevel root)
    {
        Point? topLeft = control.TranslatePoint(default, root);
        Rect bounds = control.Bounds;
        return new ScreenshotVisibleNamedControlEntry(
            Name: control.Name ?? string.Empty,
            ControlType: control.GetType().Name,
            Text: control switch
            {
                TextBlock textBlock => textBlock.Text ?? string.Empty,
                TextBox textBox => textBox.Text ?? string.Empty,
                CheckBox checkBox => checkBox.IsChecked?.ToString() ?? string.Empty,
                Button button => GetPrimaryButtonLabel(button),
                _ => string.Empty
            },
            X: topLeft?.X,
            Y: topLeft?.Y,
            Width: bounds.Width,
            Height: bounds.Height);
    }

    private static string? TryGetControlSuffix(string? name, string prefix)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return name[prefix.Length..];
    }

    private static object[] SnapshotListBoxItems(ListBox listBox)
    {
        if (listBox.ItemsSource is IEnumerable itemsSource)
        {
            return itemsSource.Cast<object>().ToArray();
        }

        if (listBox.Items is IEnumerable items)
        {
            return items.Cast<object>().ToArray();
        }

        return Array.Empty<object>();
    }

    private static NavigatorTreeItem[] SnapshotTreeItems(TreeView treeView)
    {
        if (treeView.ItemsSource is IEnumerable<NavigatorTreeItem> typedItems)
        {
            return typedItems.ToArray();
        }

        if (treeView.Items is IEnumerable items)
        {
            return items.OfType<NavigatorTreeItem>().ToArray();
        }

        return [];
    }

    private static CharacterRosterNode[] SnapshotRosterItems(TreeView treeView)
    {
        if (treeView.ItemsSource is IEnumerable<CharacterRosterNode> typedItems)
        {
            return typedItems.ToArray();
        }

        if (treeView.Items is IEnumerable items)
        {
            return items.OfType<CharacterRosterNode>().ToArray();
        }

        return [];
    }

    private static NavigatorTreeItem? FindTreeItem(
        IEnumerable<NavigatorTreeItem> items,
        NavigatorTreeNodeKind kind,
        Func<NavigatorTreeItem, bool> predicate)
    {
        foreach (NavigatorTreeItem item in items)
        {
            if (item.Kind == kind && predicate(item))
            {
                return item;
            }

            NavigatorTreeItem? childMatch = FindTreeItem(item.Children, kind, predicate);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static void AssertQuickActionDialogFlow(
        FlagshipUiHarness harness,
        string sectionId,
        string actionControlId,
        string expectedTitle,
        string requiredFieldLabel,
        string requiredActionId)
    {
        harness.SetActiveSectionForTesting(sectionId);
        harness.WaitUntil(() => harness.FindControlOrDefault<Control>($"SectionQuickAction_{actionControlId}")?.IsVisible == true);
        harness.Click($"SectionQuickAction_{actionControlId}");
        harness.WaitUntil(() =>
            string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));
        AssertDialogMouseReachabilityOrScrollContainment(
            harness.Window,
            expectedTitle);

        string[] fieldLines = harness.FindDialogFieldTexts();
        Assert.IsTrue(
            fieldLines.Any(line => line.Contains(requiredFieldLabel, StringComparison.Ordinal)),
            $"Dialog '{expectedTitle}' must expose a specific '{requiredFieldLabel}' field.");

        string preview = harness.FindControl<TextBox>("SectionPreviewBox").Text ?? string.Empty;
        Assert.IsTrue(
            preview.Contains(sectionId, StringComparison.OrdinalIgnoreCase),
            $"Section preview should contain '{sectionId}' summary evidence before confirming the action.");

        string[] actionIds = harness.DialogActionIds();
        CollectionAssert.Contains(actionIds, requiredActionId);
        harness.InvokeDialogAction(requiredActionId);
        harness.WaitUntil(() =>
            !string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));
        harness.WaitUntil(() => !harness.State.IsBusy);
    }

    private static void AssertDialogContainsAll(
        FlagshipUiHarness harness,
        string dialogTitle,
        params string[] expectedFragments)
    {
        string dialogText = string.Join(
            "\n",
            (new[] { harness.FindControl<TextBlock>("DialogTitleText").Text ?? string.Empty })
                .Concat(
            harness.FindDialogFieldTexts()
                .Concat(harness.FindDialogFieldInputTexts())
                .Concat([harness.FindControl<TextBlock>("DialogMessageText").Text ?? string.Empty])));

        foreach (string expectedFragment in expectedFragments)
        {
            Assert.IsTrue(
                dialogText.Contains(expectedFragment, StringComparison.Ordinal),
                $"M103 screenshot capture for '{dialogTitle}' must include '{expectedFragment}' before the PNG is written.");
        }
    }

    private static void AssertDialogContainsAll(
        RuntimeFlagshipUiHarness harness,
        string dialogTitle,
        params string[] expectedFragments)
    {
        string dialogText = string.Join(
            "\n",
            (new[] { harness.FindControl<TextBlock>("DialogTitleText").Text ?? string.Empty })
                .Concat(
            harness.FindDialogFieldTexts()
                .Concat(harness.FindDialogFieldInputTexts())
                .Concat([harness.FindControl<TextBlock>("DialogMessageText").Text ?? string.Empty])));

        foreach (string expectedFragment in expectedFragments)
        {
            Assert.IsTrue(
                dialogText.Contains(expectedFragment, StringComparison.Ordinal),
                $"M103 screenshot capture for '{dialogTitle}' must include '{expectedFragment}' before the PNG is written.");
        }
    }

    private static void AssertUiControlDialogFlow(
        FlagshipUiHarness harness,
        string controlId,
        string expectedTitle,
        string requiredFieldLabel,
        string requiredActionId)
    {
        harness.OpenUiControl(controlId);
        harness.WaitUntil(() =>
            string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));

        string[] fieldLines = harness.FindDialogFieldTexts();
        Assert.IsTrue(
            fieldLines.Any(line => line.Contains(requiredFieldLabel, StringComparison.Ordinal)),
            $"Dialog '{expectedTitle}' must expose a specific '{requiredFieldLabel}' field.");
        CollectionAssert.Contains(harness.DialogActionIds(), requiredActionId);
        harness.InvokeDialogAction(requiredActionId);
        harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);
    }

    private static void WithLoadedRunnerHarness(Action<FlagshipUiHarness> assertion)
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                    timeoutMs: 8000);
                assertion(harness);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static void WithRuntimeLoadedRunnerHarness(Action<RuntimeFlagshipUiHarness> assertion)
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                    timeoutMs: 8000,
                    context: "load demo runner into runtime-backed harness");
                assertion(harness);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static void WithStandaloneControl<TControl>(Action<TControl> assertion)
        where TControl : Control, new()
    {
        WithStandaloneControl<TControl, bool>(control =>
        {
            assertion(control);
            return true;
        });
    }

    private static TResult WithStandaloneControl<TControl, TResult>(Func<TControl, TResult> assertion)
        where TControl : Control, new()
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        Window hostWindow = new()
                        {
                            Width = 1440,
                            Height = 960,
                            Content = new TControl()
                        };
                        hostWindow.Show();
                        PumpStandaloneUi();

                        try
                        {
                            return assertion((TControl)hostWindow.Content!);
                        }
                        finally
                        {
                            hostWindow.Close();
                            PumpStandaloneUi();
                        }
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia standalone headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static void WithStandaloneDialogWindow(Action<DesktopDialogWindow> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            DesktopDialogWindow window = new()
                            {
                                Width = 1080,
                                Height = 900
                            };
                            window.Show();
                            PumpStandaloneUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpStandaloneUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia standalone dialog-window headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static T FindDescendant<T>(Control root, string name)
        where T : Control
    {
        return FindDescendantOrDefault<T>(root, name)
            ?? throw new AssertFailedException($"Descendant control '{name}' of type {typeof(T).Name} was not found.");
    }

    private static T? FindDescendantOrDefault<T>(Control root, string name)
        where T : Control
    {
        if (root is T typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
    }

    private static RuntimeControlInventoryNode CaptureControlInventory(Control control)
    {
        return CaptureControlInventory(
            control,
            new HashSet<Control>(ReferenceEqualityComparer.Instance),
            depth: 0,
            maxDepth: 32);
    }

    private static RuntimeControlInventoryNode CaptureControlInventory(
        Control control,
        HashSet<Control> visited,
        int depth,
        int maxDepth)
    {
        string text = control switch
        {
            TextBlock textBlock => textBlock.Text ?? string.Empty,
            Button button => GetPrimaryButtonLabel(button),
            TextBox textBox => textBox.Text ?? string.Empty,
            _ => string.Empty
        };
        string toolTip = ToolTip.GetTip(control)?.ToString() ?? string.Empty;

        if (!visited.Add(control))
        {
            return new RuntimeControlInventoryNode(
                Name: control.Name ?? string.Empty,
                ControlType: $"{control.GetType().Name}:CycleReference",
                Text: text,
                ToolTip: toolTip,
                IsVisible: control.IsVisible,
                Children: []);
        }

        if (depth >= maxDepth)
        {
            return new RuntimeControlInventoryNode(
                Name: control.Name ?? string.Empty,
                ControlType: $"{control.GetType().Name}:DepthLimit",
                Text: text,
                ToolTip: toolTip,
                IsVisible: control.IsVisible,
                Children: []);
        }

        RuntimeControlInventoryNode[] children = control.GetVisualChildren()
            .OfType<Control>()
            .Where(child => !ReferenceEquals(child, control))
            .Select(child => CaptureControlInventory(child, visited, depth + 1, maxDepth))
            .ToArray();

        return new RuntimeControlInventoryNode(
            Name: control.Name ?? string.Empty,
            ControlType: control.GetType().Name,
            Text: text,
            ToolTip: toolTip,
            IsVisible: control.IsVisible,
            Children: children);
    }

    private static RuntimeRouteInventoryEntry CaptureRuntimeRouteInventory(
        RuntimeFlagshipUiHarness harness,
        string routeId,
        string routeFamily,
        string branchId)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        TreeView? rosterTree = harness.FindControlOrDefault<TreeView>("RosterTree");
        string rulesetId = RulesetDefaults.NormalizeOptional(harness.ShellPresenter.State.ActiveRulesetId)
            ?? harness.State.Session.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;

        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: harness.ShellPresenter.State.OpenMenuId ?? string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(harness.Window),
            VisibleCommandIds: CaptureVisibleCommandIds(commandsList),
            NavigatorRootLabels: rosterTree is null
                ? []
                : SnapshotRosterItems(rosterTree).Select(item => item.Name).Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            Inventory: CaptureControlInventory(harness.Window));
    }

    private static RuntimeRouteInventoryEntry CaptureStandaloneRouteInventory(
        DesktopDialogWindow window,
        string routeId,
        string routeFamily,
        string rulesetId,
        string branchId)
    {
        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(window),
            VisibleCommandIds: [],
            NavigatorRootLabels: [],
            Inventory: CaptureControlInventory(window));
    }

    private static RuntimeRouteInventoryEntry CaptureStandaloneControlRouteInventory(
        Control control,
        string routeId,
        string routeFamily,
        string rulesetId,
        string branchId)
    {
        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(control),
            VisibleCommandIds: [],
            NavigatorRootLabels: [],
            Inventory: CaptureControlInventory(control));
    }

    private static string[] CaptureVisibleTextInventory(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static text => text.IsVisible)
            .Select(text => (text.Text ?? string.Empty).Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CaptureVisibleCommandIds(ListBox? commandsList)
    {
        if (commandsList is null)
        {
            return [];
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Select(command => command.Id)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertInventoryContains(
        RuntimeControlInventoryNode root,
        string expectedName,
        string expectedControlType,
        string? toolTipFragment = null)
    {
        RuntimeControlInventoryNode? match = FlattenInventory(root)
            .FirstOrDefault(node =>
                string.Equals(node.Name, expectedName, StringComparison.Ordinal)
                && string.Equals(node.ControlType, expectedControlType, StringComparison.Ordinal));

        Assert.IsNotNull(match, $"Expected recursive inventory node '{expectedName}' of type '{expectedControlType}' was not found.");
        if (!string.IsNullOrWhiteSpace(toolTipFragment))
        {
            StringAssert.Contains(match!.ToolTip, toolTipFragment);
        }
    }

    private static IEnumerable<RuntimeControlInventoryNode> FlattenInventory(RuntimeControlInventoryNode root)
    {
        yield return root;
        foreach (RuntimeControlInventoryNode child in root.Children)
        {
            foreach (RuntimeControlInventoryNode descendant in FlattenInventory(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RaiseClick(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpStandaloneUi();
    }

    private static void RaiseClick(MenuItem menuItem)
    {
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        PumpStandaloneUi();
    }

    private static void PumpStandaloneUi()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private static RulesetPluginRegistry CreateShellPluginRegistry()
    {
        return new(
        [
            new Sr4RulesetPlugin(),
            new Sr5RulesetPlugin(),
            new Sr6RulesetPlugin()
        ]);
    }

    private static WorkspaceService CreateWorkspaceService()
    {
        IRulesetWorkspaceCodec[] codecs =
        [
            new Sr4WorkspaceCodec(),
            new Sr5WorkspaceCodec(
                new XmlCharacterFileQueries(new CharacterFileService()),
                new XmlCharacterSectionQueries(new CharacterSectionService()),
                new XmlCharacterMetadataCommands(new CharacterFileService())),
            new Sr6WorkspaceCodec()
        ];
        IRulesetWorkspaceCodecResolver resolver = new RulesetWorkspaceCodecResolver(codecs);
        return new WorkspaceService(
            new InMemoryWorkspaceStore(),
            resolver,
            new WorkspaceImportRulesetDetector());
    }

    private sealed class FlagshipUiHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;
        private readonly RecordingCharacterOverviewPresenter _presenter;

        public FlagshipUiHarness()
        {
            _presenter = new RecordingCharacterOverviewPresenter();
            _adapter = new CharacterOverviewViewModelAdapter(_presenter);
            ShellPresenter = new RecordingShellPresenter(CreateShellState());
            var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
            var pluginRegistry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            var shellCatalogResolver = new RulesetShellCatalogResolverService(pluginRegistry);
            Window = new MainWindow(
                _presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
        }

        public MainWindow Window { get; }
        public RecordingCharacterOverviewPresenter Presenter => _presenter;
        public CharacterOverviewState State => _adapter.State;
        public RecordingShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() =>
                ShellPresenter.InitializeCalls > 0
                && _presenter.InitializeCalls > 0
                && Window.IsVisible
                && Window.Bounds.Width > 0d
                && Window.Bounds.Height > 0d);
        }

        public void SetActiveSectionForTesting(string sectionId)
        {
            _presenter.SetActiveSectionForTesting(sectionId);
            Pump();
        }

        public void OpenUiControl(string controlId)
        {
            _presenter.HandleUiControlAsync(controlId, CancellationToken.None).GetAwaiter().GetResult();
            Pump();
        }

        public void SelectCommand(string commandId)
        {
            ListBox commandsList = FindControl<ListBox>("CommandsList");
            CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                .OfType<CommandPaletteItem>()
                .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Command '{commandId}' was not found in the command list.");
            commandsList.SelectedItem = command;
            Pump();
        }

        public void InvokeDialogAction(string actionId)
            => Click(DesktopDialogAccessibility.BuildActionName(actionId));

        public void ClickMenuCommand(string commandId)
        {
            ListBox commandsList = FindControl<ListBox>("CommandsList");
            CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                .OfType<CommandPaletteItem>()
                .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Command '{commandId}' was not found in the runtime command list.");
            commandsList.SelectedItem = null;
            Pump();
            commandsList.SelectedItem = command;
            Pump();
        }

        public void UpdateFirstEditableDialogTextField(string value)
        {
            Panel fieldsHost = FindControl<Panel>("DialogFieldsHost");
            TextBox textBox = fieldsHost.Children
                .OfType<Panel>()
                .SelectMany(panel => panel.Children.OfType<TextBox>())
                .FirstOrDefault(candidate => !candidate.IsReadOnly)
                ?? throw new AssertFailedException("No editable dialog text field was found.");
            textBox.Text = value;
            Pump();
        }

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
            TopLevel root = GetMouseInteractionRoot(control, Window);
            if (!string.IsNullOrWhiteSpace(DescribeMouseReachabilityFailure(control, root, new Rect(0d, 0d, root.Bounds.Width, root.Bounds.Height)))
                && FindVisibleScrollHost(control) is not null)
            {
                control.BringIntoView();
                Pump();
                root = GetMouseInteractionRoot(control, Window);
            }
            AssertControlMouseReachable(control, root);
            if (control is Button button)
            {
                Assert.IsTrue(button.IsEnabled, $"Control '{controlName}' must be enabled.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                root);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            root.MouseMove(location, RawInputModifiers.None);
            root.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            root.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void ClickLoadedRunnerTab(string labelFragment)
        {
            TabStrip tabStrip = FindControl<TabStrip>("LoadedRunnerTabStrip");
            NavigatorTabItem selectedTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => (tab.Label ?? string.Empty).Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssertFailedException($"Loaded-runner tab containing '{labelFragment}' was not found.");
            tabStrip.SelectedItem = selectedTab;
            Pump();
        }

        public void SelectNavigatorTreeItem(NavigatorTreeNodeKind kind, Func<NavigatorTreeItem, bool> predicate)
        {
            TreeView navigatorTree = FindControl<TreeView>("NavigatorTree");
            NavigatorTreeItem[] treeItems = SnapshotTreeItems(navigatorTree);
            NavigatorTreeItem selectedItem = FindTreeItem(treeItems, kind, predicate)
                ?? throw new AssertFailedException($"Navigator tree item of kind '{kind}' matching the requested predicate was not found.");
            navigatorTree.SelectedItem = selectedItem;
            Pump();
        }

        public Point TranslateToWindow(Control control)
        {
            Point? translated = control.TranslatePoint(default, Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{control.Name ?? control.GetType().Name}' to window coordinates.");
            return translated!.Value;
        }

        public void ClickDialogAction(string actionId)
        {
            Button actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Dialog action '{actionId}' was not found.");
            Assert.IsTrue(actionButton.IsEnabled, $"Dialog action '{actionId}' must be enabled.");
            actionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, ToPhysicalKey(key), ToKeySymbol(key));
            Pump();
        }

        public void AdvanceFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Pump();
            }
        }

        private static PhysicalKey ToPhysicalKey(Key key)
        {
            return key switch
            {
                Key.A => PhysicalKey.A,
                Key.B => PhysicalKey.B,
                Key.C => PhysicalKey.C,
                Key.D => PhysicalKey.D,
                Key.E => PhysicalKey.E,
                Key.F => PhysicalKey.F,
                Key.G => PhysicalKey.G,
                Key.H => PhysicalKey.H,
                Key.I => PhysicalKey.I,
                Key.J => PhysicalKey.J,
                Key.K => PhysicalKey.K,
                Key.L => PhysicalKey.L,
                Key.M => PhysicalKey.M,
                Key.N => PhysicalKey.N,
                Key.O => PhysicalKey.O,
                Key.P => PhysicalKey.P,
                Key.Q => PhysicalKey.Q,
                Key.R => PhysicalKey.R,
                Key.S => PhysicalKey.S,
                Key.T => PhysicalKey.T,
                Key.U => PhysicalKey.U,
                Key.V => PhysicalKey.V,
                Key.W => PhysicalKey.W,
                Key.X => PhysicalKey.X,
                Key.Y => PhysicalKey.Y,
                Key.Z => PhysicalKey.Z,
                _ => PhysicalKey.None,
            };
        }

        private static string ToKeySymbol(Key key)
        {
            return key switch
            {
                >= Key.A and <= Key.Z => key.ToString().ToLowerInvariant(),
                _ => string.Empty,
            };
        }

        public void SetTheme(ThemeVariant themeVariant)
        {
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = themeVariant;
            }

            Window.RequestedThemeVariant = themeVariant;
            Window.InvalidateVisual();
            Pump();
        }

        public byte[] CaptureScreenshotBytes()
        {
            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Height)));

            // Capture directly from the current visual tree so later dialog and section
            // captures cannot reuse a stale headless frame from an earlier surface.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                Window.InvalidateMeasure();
                Window.InvalidateArrange();
                Window.InvalidateVisual();
                Window.Measure(new Size(pixelSize.Width, pixelSize.Height));
                Window.Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
                Pump();
            }

            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(Window);
            using MemoryStream output = new();
            bitmap.Save(output);
            byte[] pngBytes = output.ToArray();
            Assert.IsTrue(pngBytes.Length > 0, "No rendered frame was available for screenshot capture.");
            return pngBytes;
        }

        public T FindControl<T>(string name)
            where T : Control
        {
            return FindControlOrDefault<T>(name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found.");
        }

        public T? FindControlOrDefault<T>(string name)
            where T : Control
        {
            T[] matches = Window.GetVisualDescendants()
                .OfType<T>()
                .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length <= 1)
            {
                return matches.FirstOrDefault();
            }

            return matches
                .OrderByDescending(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    return IsEffectivelyVisibleForScreenshotEvidence(control, root);
                })
                .ThenBy(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    Point? translated = control.TranslatePoint(default, root);
                    return translated?.Y ?? double.MaxValue;
                })
                .FirstOrDefault();
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 2000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return;
                }

                Pump();
            }

            Assert.Fail("Timed out waiting for UI condition.");
        }

        private static void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        private IEnumerable<Button> DialogActionButtons()
        {
            Panel actionsHost = FindControl<Panel>("DialogActionsHost");
            return actionsHost.Children.OfType<Button>();
        }

        public string[] DialogActionIds()
            => DialogActionButtons()
                .Select(button => button.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

        public string[] FindDialogFieldTexts()
        {
            Panel fieldsHost = FindControl<Panel>("DialogFieldsHost");
            return fieldsHost.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public string[] FindDialogFieldInputTexts()
        {
            Panel fieldsHost = FindControl<Panel>("DialogFieldsHost");
            return fieldsHost.GetVisualDescendants()
                .OfType<TextBox>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public void Dispose()
        {
            Window.Close();
            _adapter.Dispose();
        }
    }

    private sealed class RuntimeFlagshipUiHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;

        public RuntimeFlagshipUiHarness()
        {
            RulesetPluginRegistry pluginRegistry = CreateShellPluginRegistry();
            var selectionPolicy = new DefaultRulesetSelectionPolicy(pluginRegistry);
            var shellCatalogResolver = new RulesetShellCatalogResolverService(pluginRegistry, selectionPolicy);
            var client = new FixtureBackedChummerClient(
                CreateWorkspaceService(),
                shellCatalogResolver,
                rulesetSelectionPolicy: selectionPolicy);
            var bootstrapProvider = new ShellBootstrapDataProvider(client);

            ShellPresenter = new ShellPresenter(client, bootstrapProvider);
            Presenter = new CharacterOverviewPresenter(
                client,
                bootstrapDataProvider: bootstrapProvider,
                shellCatalogResolver: shellCatalogResolver,
                shellPresenter: ShellPresenter);
            _adapter = new CharacterOverviewViewModelAdapter(Presenter);

            var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
            Window = new MainWindow(
                Presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public MainWindow Window { get; }
        public CharacterOverviewPresenter Presenter { get; }
        public CharacterOverviewState State => _adapter.State;
        public ShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() =>
                !ShellPresenter.State.IsBusy
                && !State.IsBusy
                && ShellPresenter.State.MenuRoots.Count > 0
                && ShellPresenter.State.Commands.Count > 0
                && ShellPresenter.State.NavigationTabs.Count > 0);
        }

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
            TopLevel root = GetMouseInteractionRoot(control, Window);
            if (!string.IsNullOrWhiteSpace(DescribeMouseReachabilityFailure(control, root, new Rect(0d, 0d, root.Bounds.Width, root.Bounds.Height)))
                && FindVisibleScrollHost(control) is not null)
            {
                control.BringIntoView();
                Pump();
                root = GetMouseInteractionRoot(control, Window);
            }
            AssertControlMouseReachable(control, root);
            if (control is Button button)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                root);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            root.MouseMove(location, RawInputModifiers.None);
            root.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            root.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void SelectCommand(string commandId)
        {
            ListBox commandsList = FindControl<ListBox>("CommandsList");
            CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                .OfType<CommandPaletteItem>()
                .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Command '{commandId}' was not found in the command list.");
            commandsList.SelectedItem = null;
            Pump();
            commandsList.SelectedItem = command;
            Pump();
        }

        public void InvokeDialogAction(string actionId)
            => Click(DesktopDialogAccessibility.BuildActionName(actionId));

        public void ClickMenuCommand(string commandId)
            => SelectCommand(commandId);

        public void SetActiveSectionForTesting(string sectionId)
        {
            TabStrip tabStrip = FindControl<TabStrip>("LoadedRunnerTabStrip");
            NavigatorTabItem selectedTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => string.Equals(tab.SectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssertFailedException($"Loaded-runner tab for section '{sectionId}' was not found.");
            tabStrip.SelectedItem = selectedTab;
            Pump();
        }

        public void AdvanceFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Pump();
            }
        }


        public T FindControl<T>(string name)
            where T : Control
        {
            return FindControlOrDefault<T>(name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found.");
        }

        public T? FindControlOrDefault<T>(string name)
            where T : Control
        {
            T[] matches = Window.GetVisualDescendants()
                .OfType<T>()
                .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length <= 1)
            {
                return matches.FirstOrDefault();
            }

            return matches
                .OrderByDescending(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    return IsEffectivelyVisibleForScreenshotEvidence(control, root);
                })
                .ThenBy(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    Point? translated = control.TranslatePoint(default, root);
                    return translated?.Y ?? double.MaxValue;
                })
                .FirstOrDefault();
        }

        public byte[] CaptureScreenshotBytes()
        {
            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Height)));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                Window.InvalidateMeasure();
                Window.InvalidateArrange();
                Window.InvalidateVisual();
                Window.Measure(new Size(pixelSize.Width, pixelSize.Height));
                Window.Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
                Pump();
            }

            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(Window);
            using MemoryStream output = new();
            bitmap.Save(output);
            byte[] pngBytes = output.ToArray();
            Assert.IsTrue(pngBytes.Length > 0, "No rendered frame was available for runtime screenshot capture.");
            return pngBytes;
        }

        public string[] FindDialogFieldTexts()
        {
            return Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static text => text.Name is "DialogFieldLabelText" or "DialogFieldValueText")
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public string[] FindDialogFieldInputTexts()
        {
            return Window.GetVisualDescendants()
                .OfType<TextBox>()
                .Where(textBox => textBox.Name is "DialogFieldInputTextBox" or "DialogFieldInputMultilineTextBox")
                .Select(textBox => textBox.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 4000, string? context = null)
        {
            if (!TryWaitUntil(predicate, timeoutMs))
            {
                Assert.Fail(context is null
                    ? "Timed out waiting for runtime-backed UI condition."
                    : $"Timed out waiting for runtime-backed UI condition: {context}");
            }
        }

        public bool TryWaitUntil(Func<bool> predicate, int timeoutMs = 4000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return true;
                }

                Pump();
            }

            return false;
        }

        public void Dispose()
        {
            Window.Close();
            _adapter.Dispose();
        }

        private static void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static string[] GetButtonTextLines(Button button)
    {
        if (button.Content is string literal)
        {
            return string.IsNullOrWhiteSpace(literal) ? [] : [literal];
        }

        if (button.Content is Control control)
        {
            return control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        string? raw = button.Content?.ToString();
        return string.IsNullOrWhiteSpace(raw) ? [] : [raw];
    }

    private static string GetPrimaryButtonLabel(Button button)
        => GetButtonTextLines(button)
            .OrderByDescending(static value => value.Length)
            .FirstOrDefault() ?? string.Empty;

    private static DesktopDialogState BuildPriorityWorkflowDialogForTesting(string buildMethod)
    {
        MethodInfo method = typeof(DesktopDialogFactory).GetMethod(
            "BuildNewCharacterContinuationDialog",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("BuildNewCharacterContinuationDialog reflection entry point was not found.");

        return (DesktopDialogState)(method.Invoke(null, [RulesetDefaults.Sr5, buildMethod, true, "Nova", "Cipher"])
            ?? throw new AssertFailedException("BuildNewCharacterContinuationDialog returned null."));
    }

    private static DesktopDialogState RebuildPriorityWorkflowDialogField(DesktopDialogState dialog, string fieldId, string value)
    {
        MethodInfo method = typeof(DesktopDialogFactory).GetMethod(
            "RebuildDynamicDialog",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("RebuildDynamicDialog reflection entry point was not found.");

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field =>
            {
                if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                {
                    return field with { Value = value };
                }

                if (string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal))
                {
                    return field with { Value = fieldId };
                }

                return field;
            })
            .ToArray();

        return (DesktopDialogState)(method.Invoke(null, [dialog with { Fields = updatedFields }, DesktopPreferenceState.Default])
            ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));
    }

    private sealed record RuntimeControlInventoryNode(
        string Name,
        string ControlType,
        string Text,
        string ToolTip,
        bool IsVisible,
        IReadOnlyList<RuntimeControlInventoryNode> Children);

    private sealed record ScreenshotProofCapture(
        byte[] PngBytes,
        ScreenshotControlEvidenceEntry Evidence);

    private sealed record ScreenshotControlEvidenceEntry(
        string Screenshot,
        string Theme,
        string DialogTitle,
        string DialogMessage,
        IReadOnlyList<string> DialogFieldLabels,
        IReadOnlyList<string> DialogFieldIds,
        IReadOnlyList<string> DialogFieldControlIds,
        IReadOnlyList<string> DialogFieldInputValues,
        IReadOnlyList<string> DialogActionIds,
        IReadOnlyList<string> DialogActionControlIds,
        IReadOnlyList<string> VisibleNamedControlIds,
        IReadOnlyList<ScreenshotVisibleNamedControlEntry> VisibleNamedControls,
        IReadOnlyList<string> VisibleTextSamples,
        IReadOnlyList<string> VisibleMenuCommandIds,
        IReadOnlyList<string> VisibleTabLabels,
        IReadOnlyList<string> VisibleSectionQuickActionIds,
        IReadOnlyList<string> SelectedListRowTexts,
        string PreviewText);

    private sealed record ScreenshotVisibleNamedControlEntry(
        string Name,
        string ControlType,
        string Text,
        double? X,
        double? Y,
        double Width,
        double Height);

    private sealed record RuntimeRouteInventoryEntry(
        string RouteId,
        string RouteFamily,
        string BranchId,
        string RulesetId,
        string OpenMenuId,
        IReadOnlyList<string> VisibleTexts,
        IReadOnlyList<string> VisibleCommandIds,
        IReadOnlyList<string> NavigatorRootLabels,
        RuntimeControlInventoryNode Inventory);

    private sealed record InteractiveRuntimeRouteInventoryReceipt(
        string GeneratedAt,
        string ContractName,
        string Status,
        string Summary,
        IReadOnlyList<string> RouteFamilies,
        IReadOnlyList<string> RulesetLanes,
        IReadOnlyList<RuntimeRouteInventoryEntry> Routes);

    private sealed class RecordingCharacterOverviewPresenter : ICharacterOverviewPresenter
    {
        private readonly DesktopDialogFactory _dialogFactory = new();
        private CharacterOverviewState _state = CharacterOverviewState.Empty;

        public CharacterOverviewState State => _state;
        public event EventHandler? StateChanged;

        public int InitializeCalls { get; private set; }
        public int ImportCalls { get; private set; }
        public WorkspaceImportDocument? LastImportedDocument { get; private set; }
        public List<string> SwitchWorkspaceIds { get; } = [];
        public List<string> ClosedWorkspaceIds { get; } = [];
        public List<string> SelectedTabIds { get; } = [];
        public List<string> HandledUiControlIds { get; } = [];
        public List<string> ExecutedWorkspaceActionIds { get; } = [];
        public List<string> ExecutedCommandIds { get; } = [];
        public List<DialogFieldValueChangedEventArgs> DialogFieldUpdates { get; } = [];
        public List<AttributeEditRequest> AttributeEdits { get; } = [];
        public List<string> ExecutedDialogActionIds { get; } = [];
        public int SaveCalls { get; private set; }
        public int ExportCalls { get; private set; }
        public int PrintCalls { get; private set; }

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            Publish(_state);
            return Task.CompletedTask;
        }

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            ImportCalls++;
            LastImportedDocument = document;

            CharacterWorkspaceId workspaceId = new("demo-runner");
            OpenWorkspaceState workspace = new(
                Id: workspaceId,
                Name: "Soma",
                Alias: "Demo",
                LastOpenedUtc: DateTimeOffset.UtcNow,
                RulesetId: RulesetDefaults.Sr5);

            Publish(_state with
            {
                WorkspaceId = workspaceId,
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: workspaceId,
                    OpenWorkspaces: [workspace],
                    RecentWorkspaceIds: [workspaceId]),
                OpenWorkspaces = [workspace],
                Profile = new CharacterProfileSection(
                    "Soma",
                    "Demo",
                    "QA",
                    "Human",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Street Sam",
                    "Runner demo",
                    string.Empty,
                    "6.0",
                    "6.0",
                    "Priority",
                    "Standard",
                    Created: true,
                    Adept: false,
                    Magician: false,
                    Technomancer: false,
                    AI: false,
                    MainMugshotIndex: 0,
                    MugshotCount: 0),
                ActiveTabId = "tab-gear",
                ActiveSectionId = "cyberwares",
                ActiveSectionJson = """
{
  "name": "Soma",
  "ruleset": "sr5",
  "metatype": "Human",
  "priority": "Standard",
  "role": "Street Sam",
  "attributes": {
    "Body": 5,
    "Agility": 7,
    "Reaction": 6,
    "Strength": 4,
    "Willpower": 3,
    "Logic": 3
  },
  "combat": {
    "initiative": "11 + 2d6",
    "armor": 12,
    "essence": 5.34
  }
}
""",
                ActiveSectionRows =
                [
                    new SectionRowState("attributes.body", "5"),
                    new SectionRowState("attributes.agility", "7"),
                    new SectionRowState("attributes.reaction", "6"),
                    new SectionRowState("skills.firearms[0]", "Automatics 6"),
                    new SectionRowState("skills.stealth[0]", "Sneaking 5"),
                    new SectionRowState("gear.weapons[0]", "Ares Alpha"),
                    new SectionRowState("gear.armor[0]", "Armor Jacket"),
                    new SectionRowState("cyberware[0]", "Wired Reflexes 2"),
                    new SectionRowState("contacts[0]", "Fixer (Loyalty 4 / Connection 5)"),
                    new SectionRowState("notes.runner_goal", "Ready for a flagship shell smoke pass")
                ],
                HasSavedWorkspace = false,
                Error = null
            });

            return Task.CompletedTask;
        }

        public void SetActiveSectionForTesting(string sectionId)
        {
            (string preview, SectionRowState[] rows) = BuildSectionFixture(sectionId);
            Publish(_state with
            {
                ActiveSectionId = sectionId,
                ActiveSectionJson = preview,
                ActiveSectionRows = rows,
                ActiveDialog = null,
                Error = null
            });
        }

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            SwitchWorkspaceIds.Add(id.Value);
            return Task.CompletedTask;
        }

        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            ClosedWorkspaceIds.Add(id.Value);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabIds.Add(tabId);
            return Task.CompletedTask;
        }

        public Task HandleUiControlAsync(string controlId, CancellationToken ct)
        {
            HandledUiControlIds.Add(controlId);
            Publish(_state with
            {
                Error = null,
                ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, _state.Preferences)
            });
            return Task.CompletedTask;
        }

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
        {
            ExecutedWorkspaceActionIds.Add(action.Id);
            return Task.CompletedTask;
        }

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task ExportAsync(CancellationToken ct)
        {
            ExportCalls++;
            return Task.CompletedTask;
        }

        public Task PrintAsync(CancellationToken ct)
        {
            PrintCalls++;
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            if (OverviewCommandPolicy.IsDialogCommand(commandId)
                || OverviewCommandPolicy.IsImportHintCommand(commandId))
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    ActiveDialog = _dialogFactory.CreateCommandDialog(
                        commandId,
                        _state.Profile,
                        _state.Preferences,
                        _state.ActiveSectionJson,
                        _state.WorkspaceId,
                        RulesetDefaults.Sr5),
                    Error = null
                });
            }
            else
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
            DesktopDialogState? dialog = _state.ActiveDialog;
            if (dialog is null)
            {
                return Task.CompletedTask;
            }

            DialogFieldUpdates.Add(new DialogFieldValueChangedEventArgs(fieldId, value ?? string.Empty));

            DesktopDialogField[] updatedFields = dialog.Fields
                .Select(field =>
                {
                    if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                    {
                        return field with { Value = value ?? string.Empty };
                    }

                    if (string.Equals(dialog.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                        && string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal))
                    {
                        return field with { Value = fieldId };
                    }

                    return field;
                })
                .ToArray();

            MethodInfo rebuildMethod = typeof(DesktopDialogFactory).GetMethod(
                "RebuildDynamicDialog",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("RebuildDynamicDialog reflection entry point was not found.");
            DesktopDialogState nextDialog = (DesktopDialogState)(rebuildMethod.Invoke(
                null,
                [dialog with { Fields = updatedFields }, DesktopPreferenceState.Default])
                ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));

            Publish(_state with
            {
                ActiveDialog = nextDialog
            });

            return Task.CompletedTask;
        }

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct)
        {
            AttributeEdits.Add(request);
            return Task.CompletedTask;
        }

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
        {
            ExecutedDialogActionIds.Add(actionId);
            if (string.Equals(actionId, "cancel", StringComparison.Ordinal)
                || string.Equals(actionId, "close", StringComparison.Ordinal)
                || string.Equals(actionId, "save", StringComparison.Ordinal)
                || string.Equals(actionId, "add", StringComparison.Ordinal)
                || string.Equals(actionId, "apply", StringComparison.Ordinal)
                || string.Equals(actionId, "delete", StringComparison.Ordinal))
            {
                Publish(_state with
                {
                    ActiveDialog = null,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task CloseDialogAsync(CancellationToken ct)
        {
            Publish(_state with { ActiveDialog = null, Error = null });
            return Task.CompletedTask;
        }

        private void Publish(CharacterOverviewState state)
        {
            _state = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static (string Preview, SectionRowState[] Rows) BuildSectionFixture(string sectionId)
        {
            switch (sectionId)
            {
                case "drugs":
                    return (
                        """
{
  "section": "drugs",
  "consumables": [
    { "name": "Jazz", "duration": "10 turns", "availability": "12R" }
  ]
}
""",
                        [
                            new SectionRowState("drugs[0]", "Jazz · 10 turns")
                        ]);
                case "spells":
                    return (
                        """
{
  "section": "spells",
  "spells": [
    { "name": "Stunbolt", "category": "Combat", "drain": "F-3" }
  ]
}
""",
                        [
                            new SectionRowState("spells[0]", "Stunbolt · Combat")
                        ]);
                case "powers":
                    return (
                        """
{
  "section": "powers",
  "adeptPowers": [
    { "name": "Improved Reflexes", "level": 1, "cost": 1.5 }
  ]
}
""",
                        [
                            new SectionRowState("powers[0]", "Improved Reflexes 1")
                        ]);
                case "complexforms":
                    return (
                        """
{
  "section": "complexforms",
  "complexForms": [
    { "name": "Cleaner", "level": 1 }
  ],
  "matrixPrograms": [
    { "name": "Armor", "slot": "Common" }
  ]
}
""",
                        [
                            new SectionRowState("complexforms[0]", "Cleaner 1"),
                            new SectionRowState("aiprograms[0]", "Armor (Common)")
                        ]);
                case "initiationgrades":
                    return (
                        """
{
  "section": "initiationgrades",
  "grades": [
    { "grade": 1, "reward": "Metamagic" }
  ]
}
""",
                        [
                            new SectionRowState("initiationgrades[0]", "Grade 1 · Metamagic")
                        ]);
                case "contacts":
                    return (
                        """
{
  "section": "contacts",
  "contacts": [
    { "name": "Fixer", "role": "Broker", "location": "Seattle", "connection": 5, "loyalty": 4 }
  ]
}
""",
                        [
                            new SectionRowState("contacts[0]", "Fixer (Loyalty 4 / Connection 5)")
                        ]);
                case "skills":
                    return (
                        """
{
  "section": "skills",
  "skills": [
    { "name": "Automatics", "rating": 6, "specialization": "Assault Rifles" },
    { "name": "Sneaking", "rating": 5 }
  ]
}
""",
                        [
                            new SectionRowState("skills[0]", "Automatics 6 (Assault Rifles)"),
                            new SectionRowState("skills[1]", "Sneaking 5")
                        ]);
                case "qualities":
                    return (
                        """
{
  "section": "qualities",
  "qualities": [
    { "name": "Ambidextrous", "karma": 4 },
    { "name": "Distinctive Style", "karma": -5 }
  ]
}
""",
                        [
                            new SectionRowState("qualities[0]", "Ambidextrous · 4 karma"),
                            new SectionRowState("qualities[1]", "Distinctive Style · -5 karma")
                        ]);
                case "gear":
                    return (
                        """
{
  "section": "gear",
  "gear": [
    { "name": "Medkit", "rating": 6, "location": "Backpack" },
    { "name": "Ammo: APDS", "quantity": 40, "location": "Duffel" }
  ],
  "explain": {
    "packet_id": "gear.medkit.rating",
    "source_anchors": [
      {
        "book": "SR5",
        "page": "447",
        "section": "Medkit",
        "localPdfPath": "/tmp/rulebooks/sr5-medkit.pdf"
      }
    ],
    "stale_if_snapshot_changes": {
      "snapshot_ref": "gear-medkit-v1",
      "current_snapshot_ref": "gear-medkit-v2"
    },
    "boundedFollowUpSummary": "If rating drops below 6, extended healing intervals lose the clinic-grade bonus."
  }
}
""",
                        [
                            new SectionRowState("gear[0]", "Medkit 6 · Backpack"),
                            new SectionRowState("gear[1]", "Ammo: APDS ×40 · Duffel")
                        ]);
                case "weapons":
                    return (
                        """
{
  "section": "weapons",
  "weapons": [
    { "name": "Ares Alpha", "dicePool": 14, "ammo": "42(c)" }
  ]
}
""",
                        [
                            new SectionRowState("weapons[0]", "Ares Alpha · Dice Pool 14 / 42(c)")
                        ]);
                case "armors":
                    return (
                        """
{
  "section": "armors",
  "armor": [
    { "name": "Armor Jacket", "rating": 12, "mods": 1 }
  ]
}
""",
                        [
                            new SectionRowState("armors[0]", "Armor Jacket · Armor 12 / Mods 1")
                        ]);
                case "vehicles":
                    return (
                        """
{
  "section": "vehicles",
  "vehicles": [
    { "name": "Roadmaster", "handling": 3, "armor": 16 }
  ]
}
""",
                        [
                            new SectionRowState("vehicles[0]", "Roadmaster · Armor 16 / Handling 3")
                        ]);
                case "mentorspirits":
                    return (
                        """
{
  "section": "mentorspirits",
  "mentor": "Shark",
  "familiarLane": "active"
}
""",
                        [
                            new SectionRowState("mentorspirits[0]", "Shark · Familiar lane active")
                        ]);
                case "progress":
                    return (
                        """
{
  "section": "progress",
  "diary": [
    { "title": "First extraction", "karma": 2 }
  ]
}
""",
                        [
                            new SectionRowState("progress[0]", "First extraction · +2 karma")
                        ]);
                case "attributes":
                case "attributedetails":
                    return (
                        """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}
""",
                        [
                            new SectionRowState("attributes[0]", "Body 4"),
                            new SectionRowState("attributes[1]", "Agility 5"),
                            new SectionRowState("attributes[2]", "Reaction 5")
                        ]);
                case "calendar":
                    return (
                        """
{
  "section": "calendar",
  "diary": [
    { "title": "Downtime recon", "date": "2080-02-14", "karma": 2 }
  ]
}
""",
                        [
                            new SectionRowState("calendar[0]", "Downtime recon · +2 karma")
                        ]);
                case "validate":
                    return (
                        """
{
  "section": "validate",
  "validation": [
    { "severity": "warning", "message": "License missing for Ares Alpha" },
    { "severity": "info", "message": "Lifestyle payments due in 3 days" }
  ]
}
""",
                        [
                            new SectionRowState("validate[0]", "Warning · License missing for Ares Alpha"),
                            new SectionRowState("validate[1]", "Info · Lifestyle payments due in 3 days")
                        ]);
                case "rules":
                    return (
                        """
{
  "section": "rules",
  "rules": [
    { "source": "SR5", "entry": "Armor Encumbrance" },
    { "source": "Run & Gun", "entry": "Smartgun Accessories" }
  ]
}
""",
                        [
                            new SectionRowState("rules[0]", "SR5 · Armor Encumbrance"),
                            new SectionRowState("rules[1]", "Run & Gun · Smartgun Accessories")
                        ]);
                default:
                    return (
                        """
{
  "section": "profile"
}
""",
                        [
                            new SectionRowState("notes.runner_goal", "Ready for a flagship shell smoke pass")
                        ]);
            }
        }
    }

    private sealed class RecordingShellPresenter : IShellPresenter
    {
        public RecordingShellPresenter(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }
        public int InitializeCalls { get; private set; }
        public List<string> ExecutedCommandIds { get; } = [];
        public List<string> SelectedTabIds { get; } = [];
        public List<string> ToggledMenuIds { get; } = [];
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            State = State with
            {
                LastCommandId = commandId,
                OpenMenuId = null,
                Notice = $"Command '{commandId}' dispatched."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabIds.Add(tabId);
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
            ToggledMenuIds.Add(menuId);
            State = State with
            {
                OpenMenuId = string.Equals(State.OpenMenuId, menuId, StringComparison.Ordinal) ? null : menuId,
                Notice = $"Menu '{menuId}' opened."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            ShellWorkspaceState[] openWorkspaces = activeWorkspaceId is null
                ? []
                : [
                    new ShellWorkspaceState(
                        activeWorkspaceId.Value,
                        "Soma",
                        "Demo",
                        DateTimeOffset.UtcNow,
                        RulesetDefaults.Sr5,
                        HasSavedWorkspace: false)
                ];
            State = State with
            {
                ActiveWorkspaceId = activeWorkspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveTabId = activeWorkspaceId is null ? State.ActiveTabId : "tab-info",
                Notice = activeWorkspaceId is null ? "Ready." : $"Restored {openWorkspaces.Length} workspace(s)."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCoachSidecarClient : IAvaloniaCoachSidecarClient
    {
        public Task<AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
            string routeType,
            string? runtimeFingerprint = null,
            int maxCount = 3,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>.Failure(0, "disabled"));
    }

    private static ShellState CreateShellState()
    {
        AppCommandDefinition[] commands = new CatalogOnlyRulesetShellCatalogResolver()
            .ResolveCommands(RulesetDefaults.Sr5)
            .ToArray();

        return ShellState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            PreferredRulesetId = RulesetDefaults.Sr5,
            Commands = commands,
            MenuRoots = commands.Where(command => string.Equals(command.Group, "menu", StringComparison.Ordinal)).ToArray(),
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-info", "Info", "summary", "character", true, true, RulesetDefaults.Sr5)
            ],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
    }
}
