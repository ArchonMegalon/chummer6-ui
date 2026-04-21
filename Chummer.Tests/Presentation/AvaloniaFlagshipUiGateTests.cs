#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
public sealed class AvaloniaFlagshipUiGateTests
{
    private static readonly object HeadlessInitLock = new();
    private static readonly JsonSerializerOptions ScreenshotEvidenceJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly string[] ClassicMenuLabels = ["File", "Edit", "Special", "Tools", "Windows", "Help"];
    private static readonly string[] HiddenWorkbenchToolbarButtons =
    [
        "ImportRawButton",
        "LoadDemoRunnerButton",
        "CampaignWorkspaceButton",
        "UpdateStatusButton",
        "InstallLinkingButton",
        "SupportButton",
        "ReportIssueButton",
    ];
    private static readonly string[] HiddenRuntimeLoadedToolbarButtons =
    [
        "LoadDemoRunnerButton",
        "CampaignWorkspaceButton",
        "UpdateStatusButton",
        "InstallLinkingButton",
        "SupportButton",
        "ReportIssueButton",
    ];
    private static readonly string[] SupportedRulesetIds = [RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6];
    private static readonly string[] StandaloneMenuButtonNames =
    [
        "FileMenuButton",
        "EditMenuButton",
        "SpecialMenuButton",
        "ToolsMenuButton",
        "WindowsMenuButton",
        "HelpMenuButton",
    ];
    private static readonly string[] ExpectedFileMenuCommandIds = ["open_character", "save_character"];
    private static readonly string[] ExpectedSummaryHeaderTabSelectionOrder = ["tab-gear", "tab-profile"];
    private static readonly string[] ExpectedNavigatorWorkspaceSelection = ["runner-1"];
    private static readonly string[] ExpectedNavigatorTabSelection = ["tab-gear"];
    private static readonly string[] ExpectedNavigatorSectionActionSelection = ["action-cyberware"];
    private static readonly string[] ExpectedNavigatorWorkflowSurfaceSelection = ["workflow-progress"];
    private static readonly string[] ExpectedSettingsCommandSelection = ["global_settings"];
    private static readonly string[] ExpectedSaveDialogActionSelection = ["save"];
    private static readonly VeteranCertificationReviewStep[] VeteranCertificationReviewSteps =
    [
        new(
            "toolstrip",
            "01-initial-shell-light.png",
            "Capture initial promoted Avalonia shell after WaitForReady.",
            "Chummer5a ChummerMainForm toolStrip New/Open/OpenForPrinting/OpenForExport lineage.",
            []),
        new(
            "menu",
            "02-menu-open-light.png",
            "Click FileMenuButton and capture MenuCommandsHost.",
            "Chummer5a ChummerMainForm File/Tools/Windows/Help top menu lineage.",
            []),
        new(
            "settings",
            "03-settings-open-light.png",
            "Press Ctrl+G and capture the Global Settings dialog.",
            "Chummer5a EditGlobalSettings Global Options lineage.",
            ["Global Settings"]),
        new(
            "master_index",
            "16-master-index-dialog-light.png",
            "Execute master_index and capture the Master Index dialog.",
            "Chummer5a MasterIndex search utility lineage.",
            ["Master Index"]),
        new(
            "roster",
            "17-character-roster-dialog-light.png",
            "Execute character_roster and capture the Character Roster dialog.",
            "Chummer5a CharacterRoster watch-folder utility lineage.",
            ["Character Roster"]),
        new(
            "import",
            "18-import-dialog-light.png",
            "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.",
            "Chummer5a File/Open and Hero Lab Importer import route lineage.",
            ["Open Character"])];
    private static bool _headlessInitialized;
    private static HeadlessUnitTestSession? _headlessSession;

    [TestMethod]
    public void Blazor_root_route_ownership_stays_with_desktop_shell_anchor_and_moves_showcase_off_root()
    {
        string homePath = SourcePath("Chummer.Blazor", "Components", "Pages", "Home.razor");
        string showcasePath = SourcePath("Chummer.Blazor", "Components", "Pages", "Showcase.razor");
        string legacyPath = SourcePath("Chummer.Blazor", "Pages", "Index.razor");

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
    public void Avalonia_startup_enters_the_workbench_without_reopening_the_desktop_home_cockpit()
    {
        string appPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml.cs");
        string appText = File.ReadAllText(appPath);

        StringAssert.Contains(appText, "DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);");
        Assert.IsFalse(
            appText.Contains("DesktopHomeWindow.ShowIfNeededAsync(", StringComparison.Ordinal),
            "First launch must go straight to the workbench; install linking is the only startup prompt that should remain.");
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
        StringAssert.Contains(shellCatalogText, "Command(\"switch_ruleset\", \"command.switch_ruleset\", \"special\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"new_window\", \"command.new_window\", \"windows\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"close_window\", \"command.close_window\", \"windows\", false)");
        StringAssert.Contains(shellChromeBoundaryText, "[\"switch_ruleset\"] = \"Switch Ruleset...\"");
        StringAssert.Contains(shellChromeBoundaryText, "[\"new_window\"] = \"New Window\"");
        StringAssert.Contains(mainWindowText, "ColumnDefinitions=\"0,*,0\"");
        StringAssert.Contains(mainWindowText, "x:Name=\"LeftNavigatorRegion\"");
        StringAssert.Contains(mainWindowText, "IsVisible=\"False\"");
        StringAssert.Contains(mainWindowStateRefreshText, "ApplyWorkbenchChromeVisibility(shellFrame);");
        StringAssert.Contains(mainWindowStateRefreshText, "new GridLength(228)");
        StringAssert.Contains(mainWindowStateRefreshText, "new GridLength(0)");
        StringAssert.Contains(avaloniaProjectorText, "ShowNavigatorPane: resolvedOpenWorkspaces.Length > 1");
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

            harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton")).Length > 0);

            string[] visibleCommands = SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton"))
                .Select(command => command.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.Contains(visibleCommands, "open_character");
            CollectionAssert.Contains(visibleCommands, "save_character");
        });
    }

    [TestMethod]
    public void Runtime_backed_special_and_windows_menus_surface_real_commands()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Click("SpecialMenuButton");
            harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("SpecialMenuButton")).Length > 0);
            string[] specialCommands = SnapshotMenuCommands(harness.FindControl<MenuItem>("SpecialMenuButton"))
                .Select(command => command.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.Contains(specialCommands, "switch_ruleset");

            harness.Click("WindowsMenuButton");
            harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("WindowsMenuButton")).Length > 0);
            string[] windowsCommands = SnapshotMenuCommands(harness.FindControl<MenuItem>("WindowsMenuButton"))
                .Select(command => command.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.Contains(windowsCommands, "new_window");
            CollectionAssert.Contains(windowsCommands, "close_window");
        });
    }

    [TestMethod]
    public void Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            MenuItem[] menuItems = SnapshotRootMenuItems(menuPanel);
            string[] menuLabels = menuItems
                .Select(menuItem => menuItem.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(ClassicMenuLabels, menuLabels);

            foreach (MenuItem menuItem in menuItems)
            {
                Assert.IsTrue(menuItem.IsEnabled, $"Menu item '{menuItem.Name}' must stay enabled after runtime bootstrap.");
            }

            (string MenuName, string MenuId)[] clickableMenus =
            [
                ("FileMenuButton", "file"),
                ("EditMenuButton", "edit"),
                ("SpecialMenuButton", "special"),
                ("ToolsMenuButton", "tools"),
                ("WindowsMenuButton", "windows"),
                ("HelpMenuButton", "help"),
            ];

            foreach ((string menuName, string menuId) in clickableMenus)
            {
                harness.Click(menuName);
                harness.WaitUntil(() => string.Equals(harness.ShellPresenter.State.OpenMenuId, menuId, StringComparison.Ordinal));
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_file_menu_preserves_working_open_save_import_routes()
    {
        WithLoadedRunnerHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("FileMenuButton");
            harness.WaitUntil(() =>
            {
                MenuItem[] commands = SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton"));
                return commands.Any(command => string.Equals(command.Tag?.ToString(), "open_character", StringComparison.Ordinal))
                    && commands.Any(command => string.Equals(command.Tag?.ToString(), "save_character", StringComparison.Ordinal));
            });

            harness.ClickMenuCommand("open_character");
            Assert.IsNotNull(harness.FindControlOrDefault<MenuItem>("FileMenuButton"), "File command dispatch must keep runtime-backed menu routing active.");

            harness.Click("ImportFileButton");
            Assert.IsTrue(harness.FindControl<Button>("ImportFileButton").IsEnabled, "Import action must stay first-class in the workbench toolstrip.");
        });
    }

    [TestMethod]
    public void Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            (string ButtonName, string ExpectedLabel, string ExpectedToolTip)[] expectedButtons =
            [
                ("SaveButton", "Save", "Save Workspace"),
                ("PrintButton", "Print", "Print Character"),
                ("CopyButton", "Copy", "Copy"),
                ("DesktopHomeButton", "New", "New Character"),
                ("ImportFileButton", "Open", "Import Character File"),
                ("CloseWorkspaceButton", "Close", "Close Active Workspace"),
                ("SettingsButton", "Options", "Settings"),
            ];

            foreach ((string buttonName, string expectedLabel, string expectedToolTip) in expectedButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Workbench action '{buttonName}' must stay visible.");
                Assert.IsTrue(button.IsEnabled, $"Workbench action '{buttonName}' must stay enabled.");
                Assert.IsInstanceOfType<string>(button.Content, $"Workbench action '{buttonName}' must stay a flat classic toolbar label, not a dashboard tile.");
                CollectionAssert.Contains(GetButtonTextLines(button), expectedLabel, $"Workbench action '{buttonName}' must keep its classic desktop label.");
                Assert.AreEqual(1, GetButtonTextLines(button).Length, $"Workbench action '{buttonName}' must not add a secondary caption line.");
                Assert.AreEqual(expectedToolTip, ToolTip.GetTip(button)?.ToString(), $"Workbench action '{buttonName}' must keep the full command text as hover help.");
                Assert.IsTrue(button.Bounds.Width > 0d && button.Bounds.Height > 0d, $"Workbench action '{buttonName}' must keep a visible desktop footprint.");
            }

            foreach (string buttonName in HiddenWorkbenchToolbarButtons)
            {
                Assert.IsFalse(
                    harness.FindControl<Button>(buttonName).IsVisible,
                    $"Workbench chrome must hide non-primary side workflows from the default toolbar: {buttonName}.");
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
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            double[] toolbarButtonHeights =
            [
                harness.FindControl<Button>("SaveButton").Bounds.Height,
                harness.FindControl<Button>("PrintButton").Bounds.Height,
                harness.FindControl<Button>("CopyButton").Bounds.Height,
                harness.FindControl<Button>("DesktopHomeButton").Bounds.Height,
                harness.FindControl<Button>("ImportFileButton").Bounds.Height,
                harness.FindControl<Button>("CloseWorkspaceButton").Bounds.Height,
                harness.FindControl<Button>("SettingsButton").Bounds.Height,
                harness.FindControl<Button>("LoadDemoRunnerButton").Bounds.Height,
            ];
            Assert.AreEqual(0, badgeBorders.Length, "Classic toolbar parity forbids dashboard badge tiles in the workbench strip.");
            Assert.AreEqual(0, captionBlocks.Length, "Classic toolbar parity forbids secondary caption lines in the workbench strip.");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Quick Actions");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Workbench State");
            Assert.IsTrue(toolbarButtonHeights.All(height => height <= 40d), "Classic toolbar parity requires compact workbench actions instead of hero-card sized buttons.");
            Assert.IsFalse(harness.FindControl<Button>("ImportRawButton").IsVisible, "Raw XML import must stay off the primary toolbar by default.");
            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Assert.IsTrue(rightShellRegion.IsVisible, "The collapsed right rail must stay mounted so command palette and dialog surfaces remain routable.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Classic workbench parity still requires the default right rail to surrender its space.");
        });
    }

    [TestMethod]
    public void Opening_mainframe_preserves_chummer5a_successor_workbench_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible, "Workbench-first startup must expose a real File menu immediately.");
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible, "Workbench-first startup must expose runtime-backed workbench actions immediately.");
            Assert.IsNull(harness.FindControlOrDefault<Control>("WorkspaceStripRegion"), "Fresh startup must not mount an extra workspace strip above the editor.");
            Assert.IsNull(harness.FindControlOrDefault<Control>("QuickStartContainer"), "Fresh startup must not reopen a desktop home cockpit or quick-start filler.");
            Assert.IsFalse(leftNavigatorRegion.IsVisible, "Fresh startup must not spend first-paint width on workspace chrome.");
            Assert.IsTrue(leftNavigatorRegion.Bounds.Width <= 1d, "Fresh startup must collapse the workspace rail until multiple workspaces exist.");

            string[] visibleTexts = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsVisible)
                .Select(text => (text.Text ?? string.Empty).Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            CollectionAssert.DoesNotContain(visibleTexts, "mainframe");
            CollectionAssert.DoesNotContain(visibleTexts, "dashboard");
            CollectionAssert.DoesNotContain(visibleTexts, "control center");
        });
    }

    [TestMethod]
    public void Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control centerShellRegion = harness.FindControl<Control>("CenterShellRegion");

            Assert.IsFalse(leftNavigatorRegion.IsVisible, "Single-runner startup must keep the workspace rail collapsed.");
            Assert.IsTrue(leftNavigatorRegion.Bounds.Width <= 1d, "Single-runner startup must surrender workspace-rail width back to the workbench.");
            Assert.IsTrue(centerShellRegion.Bounds.Width >= 800d, "Single-runner startup must leave the center workbench dominant.");
            Assert.IsFalse(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible, "Fresh startup must not show runner-tab chrome before a runner is loaded.");
            Assert.IsNull(harness.FindControlOrDefault<ListBox>("NavigationTabsList"), "The shell must not fall back to a roomy dashboard tab list.");
        });
    }

    [TestMethod]
    public void Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            string[] supportedRulesetIds =
            [
                RulesetDefaults.Sr4,
                RulesetDefaults.Sr5,
                RulesetDefaults.Sr6
            ];

            foreach (string rulesetId in supportedRulesetIds)
            {
                harness.ShellPresenter.SetPreferredRulesetAsync(rulesetId, CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.PreferredRulesetId, rulesetId, StringComparison.Ordinal)
                    && string.Equals(harness.ShellPresenter.State.ActiveRulesetId, rulesetId, StringComparison.Ordinal));

                Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
                TabStrip tabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
                var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
                string openWorkspacesHeading = RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId);
                string navigationTabsHeading = RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(rulesetId);
                string sectionActionsHeading = RulesetUiDirectiveCatalog.BuildSectionActionsHeading(rulesetId);
                string workflowSurfacesHeading = RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading(rulesetId);
                NavigatorTabItem[] projectedTabs = SnapshotLoadedRunnerTabs(tabStrip);
                string[] expectedTabLabels = harness.ShellPresenter.State.NavigationTabs
                    .Where(tab => availabilityEvaluator.IsNavigationTabEnabled(tab, harness.State))
                    .Select(tab => RulesetUiDirectiveCatalog.FormatNavigationTabLabel(rulesetId, tab.Id, tab.Label))
                    .ToArray();
                string[] actualTabLabels = projectedTabs
                    .Select(tab => tab.Label)
                    .ToArray();

                CollectionAssert.AreEqual(
                    expectedTabLabels,
                    actualTabLabels,
                    $"Ruleset '{rulesetId}' must keep the runtime-backed tab landmarks aligned with the active ruleset.");
                Assert.IsFalse(leftNavigatorRegion.IsVisible, "Single-runner ruleset switching must keep the auxiliary navigator rail collapsed.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(openWorkspacesHeading));
                Assert.IsFalse(string.IsNullOrWhiteSpace(navigationTabsHeading));
                Assert.IsFalse(string.IsNullOrWhiteSpace(sectionActionsHeading));
                Assert.IsFalse(string.IsNullOrWhiteSpace(workflowSurfacesHeading));
            }
        });
    }

    [TestMethod]
    public void Master_index_is_a_first_class_runtime_backed_workbench_route()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            AppCommandDefinition? command = harness.ShellPresenter.State.Commands
                .FirstOrDefault(item => string.Equals(item.Id, "master_index", StringComparison.Ordinal));
            Assert.IsNotNull(command, "Master index must remain a first-class runtime-backed shell command.");
            Assert.IsTrue(string.Equals(command.Group, "tools", StringComparison.Ordinal), "Master index must remain a Tools-lane workbench route.");
            Assert.IsTrue(command.EnabledByDefault, "Master index command must be enabled by default in the runtime shell.");
        });
    }

    [TestMethod]
    public void Character_roster_is_a_first_class_runtime_backed_workbench_route()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            AppCommandDefinition? command = harness.ShellPresenter.State.Commands
                .FirstOrDefault(item => string.Equals(item.Id, "character_roster", StringComparison.Ordinal));
            Assert.IsNotNull(command, "Character roster must remain a first-class runtime-backed shell command.");
            Assert.IsTrue(string.Equals(command.Group, "tools", StringComparison.Ordinal), "Character roster must remain a Tools-lane workbench route.");
            Assert.IsTrue(command.EnabledByDefault, "Character roster command must be enabled by default in the runtime shell.");
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
            Assert.IsFalse(visibleTexts.Any(text => text.Contains("Ruleset posture:", StringComparison.Ordinal)));
            Assert.IsFalse(visibleTexts.Any(text => text.Contains("flagship", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(harness.FindControl<Control>("LeftNavigatorRegion").IsVisible, "Classic single-runner orientation must keep workspace chrome collapsed.");
            Assert.IsFalse(harness.FindControl<Control>("RestoreContinuityStatusBorder").IsVisible, "Default startup must not mount the restore continuity banner without a real restore handoff.");
            Assert.IsTrue(harness.FindControl<ListBox>("SectionRowsList").IsVisible, "Classic orientation must keep the dense section list visible.");
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
                "SaveButton",
                "PrintButton",
                "CopyButton",
                "DesktopHomeButton",
                "ImportFileButton",
                "CloseWorkspaceButton",
                "SettingsButton",
            ];

            foreach (string menuName in menuButtons)
            {
                MenuItem menuItem = harness.FindControl<MenuItem>(menuName);
                Assert.IsTrue(menuItem.IsVisible, $"Runtime-backed runner load must keep '{menuName}' visible.");
                Assert.IsTrue(menuItem.IsEnabled, $"Runtime-backed runner load must keep '{menuName}' enabled.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(menuItem.Header?.ToString()), $"Runtime-backed runner load must not blank the label for '{menuName}'.");
            }

            foreach (string buttonName in actionButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsTrue(GetButtonTextLines(button).Length > 0, $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            foreach (string buttonName in HiddenRuntimeLoadedToolbarButtons)
            {
                Assert.IsFalse(harness.FindControl<Button>(buttonName).IsVisible, $"Dense workbench parity keeps '{buttonName}' out of the default toolbar.");
            }

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton")).Length > 0);
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
            Button saveButton = actionsHost.Children
                .OfType<Button>()
                .First(button => string.Equals(button.Content?.ToString(), "Save", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("Save", ToolTip.GetTip(saveButton)?.ToString(), "Windowed dialog actions must expose hover help.");
            Assert.AreEqual("Save", AutomationProperties.GetName(saveButton), "Windowed dialog actions must keep explicit automation names.");
            StringAssert.Contains(
                AutomationProperties.GetHelpText(saveButton) ?? string.Empty,
                "Primary dialog action",
                "Windowed dialog actions must keep automation help text.");

            TextBox editableField = fieldsHost.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox => !textBox.IsReadOnly);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ToolTip.GetTip(editableField)?.ToString()), "Runtime-built dialog fields must expose hover help.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(editableField)), "Runtime-built dialog fields must keep automation names.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(editableField)), "Runtime-built dialog fields must keep automation help text.");

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton")).Length > 0);
        });
    }

    [TestMethod]
    public void Desktop_dialog_surfaces_use_real_windowed_dialogs_and_quiet_blazor_chrome()
    {
        string postRefreshSource = File.ReadAllText(ResolveSourceFile("Chummer.Avalonia", "MainWindow.PostRefreshCoordinators.cs"));
        StringAssert.Contains(postRefreshSource, "dialogWindow.BindDialog(activeDialog);");
        StringAssert.Contains(postRefreshSource, "dialogWindow.Show(owner);");

        string dialogHostSource = File.ReadAllText(ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "DialogHost.razor"));
        Assert.IsFalse(
            dialogHostSource.Contains("Dialog trust receipt", StringComparison.Ordinal),
            "Default dialog chrome must not spend space on trust-receipt copy.");
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
                Assert.IsNull(harness.FindControlOrDefault<Control>("QuickStartContainer"));
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.State.WorkspaceId is not null && harness.State.Session.OpenWorkspaces.Count > 0);
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("QuickStartContainer") is null);
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
    public void Standalone_toolstrip_buttons_raise_expected_events()
    {
        WithStandaloneControl<ToolStripControl>(control =>
        {
            List<string> raisedEvents = [];
            control.ImportFileRequested += (_, _) => raisedEvents.Add("import_file");
            control.ImportRawRequested += (_, _) => raisedEvents.Add("import_raw");
            control.SaveRequested += (_, _) => raisedEvents.Add("save");
            control.PrintRequested += (_, _) => raisedEvents.Add("print");
            control.CopyRequested += (_, _) => raisedEvents.Add("copy");
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
                ("SaveButton", "save"),
                ("PrintButton", "print"),
                ("CopyButton", "copy"),
                ("DesktopHomeButton", "desktop_home"),
                ("ImportFileButton", "import_file"),
                ("CloseWorkspaceButton", "close_workspace"),
                ("SettingsButton", "settings"),
            ];

            foreach ((string buttonName, _) in buttonMap)
            {
                RaiseClick(FindDescendant<Button>(control, buttonName));
            }

            CollectionAssert.AreEqual(buttonMap.Select(item => item.EventId).ToArray(), raisedEvents.ToArray());
            Assert.IsFalse(FindDescendant<Button>(control, "CampaignWorkspaceButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "UpdateStatusButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "InstallLinkingButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "SupportButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "ReportIssueButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "ImportRawButton").IsVisible);
            Assert.IsFalse(FindDescendant<Button>(control, "LoadDemoRunnerButton").IsVisible);
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

            foreach (string buttonName in StandaloneMenuButtonNames)
            {
                OpenMenuItem(FindDescendant<MenuItem>(control, buttonName));
            }

            CollectionAssert.AreEqual(menuIds, selectedMenus.ToArray());

            control.SetMenuState(
                openMenuId: "file",
                knownMenuIds: menuIds,
                openMenuCommands:
                [
                    new MenuCommandItem("open_character", "open character", true, true),
                    new MenuCommandItem("save_character", "save character", true),
                ],
                isBusy: false);

            MenuItem[] commandItems = SnapshotMenuCommands(FindDescendant<MenuItem>(control, "FileMenuButton"));
            Assert.AreEqual(2, commandItems.Length, "Standalone menu proof must render visible dropdown command items for the open menu.");

            foreach (MenuItem commandItem in commandItems)
            {
                RaiseMenuItemClick(commandItem);
            }

            CollectionAssert.AreEqual(ExpectedFileMenuCommandIds, selectedCommands.ToArray());
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
    public void Standalone_summary_header_tab_buttons_raise_expected_events()
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

            TabStrip tabStrip = FindDescendant<TabStrip>(control, "LoadedRunnerTabStrip");
            NavigatorTabItem[] tabItems = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .ToArray();
            Assert.AreEqual(2, tabItems.Length, "Standalone summary-header proof must render a tab item for every visible runner tab.");

            tabStrip.SelectedItem = tabItems[1];
            PumpStandaloneUi();
            tabStrip.SelectedItem = tabItems[0];
            PumpStandaloneUi();

            CollectionAssert.AreEqual(ExpectedSummaryHeaderTabSelectionOrder, selectedTabs.ToArray());
        });
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
            control.WorkflowSurfaceSelected += (_, actionId) => selectedWorkflowSurfaces.Add(actionId);

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

            CollectionAssert.AreEqual(ExpectedNavigatorWorkspaceSelection, selectedWorkspaces.ToArray());
            CollectionAssert.AreEqual(ExpectedNavigatorTabSelection, selectedTabs.ToArray());
            CollectionAssert.AreEqual(ExpectedNavigatorSectionActionSelection, selectedSectionActions.ToArray());
            CollectionAssert.AreEqual(ExpectedNavigatorWorkflowSurfaceSelection, selectedWorkflowSurfaces.ToArray());
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

            Assert.AreEqual("Theme", ToolTip.GetTip(editableTextField)?.ToString(), "Editable dialog text fields must expose field-aware hover help.");
            Assert.AreEqual("Theme", AutomationProperties.GetName(editableTextField), "Editable dialog text fields must keep automation names.");
            StringAssert.Contains(
                AutomationProperties.GetHelpText(editableTextField) ?? string.Empty,
                "Editable text field",
                "Editable dialog text fields must keep automation help text.");
            Assert.AreEqual("Compact Mode", ToolTip.GetTip(checkboxField)?.ToString(), "Dialog checkboxes must expose hover help.");
            Assert.AreEqual("Compact Mode", AutomationProperties.GetName(checkboxField), "Dialog checkboxes must keep automation names.");
            StringAssert.Contains(
                AutomationProperties.GetHelpText(checkboxField) ?? string.Empty,
                "Editable checkbox",
                "Dialog checkboxes must keep automation help text.");
            Assert.AreEqual("Save", ToolTip.GetTip(primaryActionButton)?.ToString(), "Dialog action buttons must expose hover help.");
            Assert.AreEqual("Save", AutomationProperties.GetName(primaryActionButton), "Dialog action buttons must keep automation names.");
            StringAssert.Contains(
                AutomationProperties.GetHelpText(primaryActionButton) ?? string.Empty,
                "Primary dialog action",
                "Dialog action buttons must keep automation help text.");

            CollectionAssert.AreEqual(ExpectedSettingsCommandSelection, selectedCommands.ToArray());
            CollectionAssert.Contains(updatedFields, "globalTheme=dense");
            CollectionAssert.Contains(updatedFields, "globalCompactMode=true");
            CollectionAssert.AreEqual(ExpectedSaveDialogActionSelection, selectedActions.ToArray());
        });
    }

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
            harness.WaitUntil(() => SnapshotLoadedRunnerTabs(tabStrip).Length > 0);

            NavigatorTabItem targetTab = SnapshotLoadedRunnerTabs(tabStrip)
                .First(tab => !string.IsNullOrWhiteSpace(tab.Id)
                    && !string.Equals(tab.Id, harness.ShellPresenter.State.ActiveTabId, StringComparison.Ordinal));
            string selectedTabId = targetTab.Id;
            harness.ClickLoadedRunnerTab(targetTab.Label);
            harness.WaitUntil(() => harness.ShellPresenter.SelectedTabIds.Contains(selectedTabId));

            harness.SelectCommand("global_settings");
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
                && harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null,
                timeoutMs: 4000);

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
    public void Desktop_shell_preserves_chummer5a_familiarity_cues()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            string[] menuLabels = SnapshotRootMenuItems(menuPanel)
                .Select(menuItem => menuItem.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(ClassicMenuLabels, menuLabels);

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
    public void Desktop_shell_preserves_classic_dense_center_first_workbench_posture()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Control menuBarRegion = harness.FindControl<Control>("MenuBarRegion");
            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control centerShellRegion = harness.FindControl<Control>("CenterShellRegion");
            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Control summaryHeaderRegion = harness.FindControl<Control>("SummaryHeaderRegion");
            Control sectionRegion = harness.FindControl<Control>("SectionRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");
            Control loadedRunnerTabStripBorder = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            Control restoreContinuityStatusBorder = harness.FindControl<Control>("RestoreContinuityStatusBorder");
            Control noticeBorder = harness.FindControl<Control>("NoticeBorder");
            TextBlock? codexHeading = harness.FindControlOrDefault<TextBlock>("CodexHeadingText");
            TextBlock? codexCaption = harness.FindControlOrDefault<TextBlock>("CodexCaptionText");

            Assert.IsNull(harness.FindControlOrDefault<Control>("WorkspaceStripRegion"), "The default workbench must not spend a dedicated row on workspace-strip chrome.");
            Assert.IsFalse(leftNavigatorRegion.IsVisible, "The default workbench must not show workspace rail chrome for a single runner.");
            Assert.IsTrue(leftNavigatorRegion.Bounds.Width <= 1d, "The default workbench must not reserve meaningful width for the workspace rail.");
            Assert.IsTrue(rightShellRegion.IsVisible, "The collapsed right rail must stay mounted so command palette and dialog surfaces remain routable.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Collapsed right rail must surrender space back to the workbench.");
            Assert.IsTrue(centerShellRegion.Bounds.Width >= 900d, "The central editing workbench must keep the reclaimed single-runner width.");
            Assert.IsTrue(menuBarRegion.Bounds.Height <= 72d, "The top menu row must read like desktop chrome, not a hero header.");
            Assert.IsTrue(statusStripRegion.Bounds.Height <= 72d, "The bottom strip must stay compact like the legacy status posture.");
            Assert.IsNull(codexHeading, "The compact single-runner shell must not mount a codex heading placeholder.");
            Assert.IsNull(codexCaption, "The compact single-runner shell must not mount a codex caption placeholder.");
            Assert.IsFalse(loadedRunnerTabStripBorder.IsVisible, "The default workbench must not spend header height on runner tabs before a runner is loaded.");
            Assert.IsFalse(restoreContinuityStatusBorder.IsVisible, "The default workbench must not spend first-paint height on restore continuity chrome.");
            Assert.IsFalse(noticeBorder.IsVisible, "The default workbench must not spend first-paint height on posture copy.");

            Point summaryTop = harness.TranslateToWindow(summaryHeaderRegion);
            Point sectionTop = harness.TranslateToWindow(sectionRegion);
            Assert.IsTrue(sectionTop.Y > summaryTop.Y, "Summary header must sit above the dense section host.");
        });
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
            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip tabStripControl = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");

            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("QuickStartContainer") is null);
            harness.WaitUntil(() => tabStrip.IsVisible && SnapshotLoadedRunnerTabs(tabStripControl).Length > 0);

            Assert.IsFalse(leftNavigatorRegion.IsVisible, "Loaded single-runner posture must keep workspace chrome collapsed.");
            Assert.IsTrue(tabStrip.IsVisible);
            Assert.IsTrue(SnapshotLoadedRunnerTabs(tabStripControl).Any(tab =>
                tab.Label.Contains("Runner", StringComparison.Ordinal)),
                "Loaded runner tab strip should surface a visible Runner tab button.");
        });
    }

    [TestMethod]
    public void Loaded_runner_header_stays_tab_panel_only_without_metric_cards()
    {
        WithLoadedRunnerHarness(harness =>
        {
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip tabStripControl = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            Control restoreContinuityStatusBorder = harness.FindControl<Control>("RestoreContinuityStatusBorder");
            Control noticeBorder = harness.FindControl<Control>("NoticeBorder");

            harness.WaitUntil(() => tabStrip.IsVisible && SnapshotLoadedRunnerTabs(tabStripControl).Length > 0);

            Assert.IsFalse(restoreContinuityStatusBorder.IsVisible, "Loaded runner header must remain a tab strip, not a restore dossier.");
            Assert.IsFalse(noticeBorder.IsVisible, "Loaded runner shell must not pin a posture banner above the workbench.");
            Assert.IsNull(harness.FindControlOrDefault<Control>("NameValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("AliasValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("KarmaValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("SkillsValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeInspectButton"));
            CollectionAssert.DoesNotContain(
                harness.Window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(text => (text.Text ?? string.Empty).Trim())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToArray(),
                "Runner Tabs");
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

            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            TabStrip tabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");

            harness.WaitUntil(() =>
                harness.FindControlOrDefault<Control>("QuickStartContainer") is null
                && sectionRows.ItemCount > 0
                && !string.IsNullOrWhiteSpace(preview.Text)
                && SnapshotLoadedRunnerTabs(tabStrip).Length > 0);

            Assert.IsFalse(leftNavigatorRegion.IsVisible, "Legacy frmCareer parity must keep workspace chrome collapsed for a single runner.");
            Assert.IsTrue(sectionRows.IsVisible, "Legacy frmCareer parity requires a visible dense section/workbench list.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(preview.Text), "Legacy frmCareer parity requires a visible detail/preview pane.");

            NavigatorTabItem[] tabItems = SnapshotLoadedRunnerTabs(tabStrip);
            Assert.IsTrue(
                tabItems.Any(item =>
                    item.Id.Contains("info", StringComparison.OrdinalIgnoreCase)
                    || item.SectionId.Contains("character", StringComparison.OrdinalIgnoreCase)
                    || item.SectionId.Contains("profile", StringComparison.OrdinalIgnoreCase)),
                "Legacy frmCareer parity requires a stable info/profile navigation landmark.");
            Assert.IsTrue(
                tabItems.Any(item =>
                    item.Id.Contains("gear", StringComparison.OrdinalIgnoreCase)
                    || item.SectionId.Contains("gear", StringComparison.OrdinalIgnoreCase)),
                "Legacy frmCareer parity requires a gear navigation landmark.");

            NavigatorTabItem? gearTab = tabItems.FirstOrDefault(item =>
                item.Id.Contains("gear", StringComparison.OrdinalIgnoreCase)
                || item.SectionId.Contains("gear", StringComparison.OrdinalIgnoreCase)
                || item.Label.Contains("Gear", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(gearTab, "Legacy frmCareer parity requires a gear tab in the loaded-runner header.");
            tabStrip.SelectedItem = gearTab;
            harness.WaitUntil(() => string.Equals(harness.ShellPresenter.State.ActiveTabId, gearTab.Id, StringComparison.Ordinal));

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
        WithLoadedRunnerHarness(harness =>
        {
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox sectionPreview = harness.FindControl<TextBox>("SectionPreviewBox");
            Control classicCharacterSheet = harness.FindControl<Control>("ClassicCharacterSheetBorder");

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

            Assert.IsTrue(classicCharacterSheet.IsVisible, "Dense character-sheet posture must surface a visible runner summary band.");
            CollectionAssert.Contains(rowText, "attributes.body = 5");
            CollectionAssert.Contains(rowText, "attributes.agility = 7");
            CollectionAssert.Contains(rowText, "skills.firearms[0] = Automatics 6");
            StringAssert.Contains(sectionPreview.Text ?? string.Empty, "\"attributes\"");
            StringAssert.Contains(sectionPreview.Text ?? string.Empty, "\"combat\"");
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
                requiredFieldLabel: "Entry Name",
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
    public void Gear_builder_preserves_familiar_browse_detail_confirm_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            TextBlock notice = harness.FindControl<TextBlock>("NoticeText");
            Border noticeBorder = harness.FindControl<Border>("NoticeBorder");

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

            CollectionAssert.Contains(rowText, "gear.weapons[0] = Ares Alpha");
            CollectionAssert.Contains(rowText, "gear.armor[0] = Armor Jacket");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"combat\"");
            Assert.IsFalse(noticeBorder.IsVisible, "Routine command-dispatch noise should stay hidden in the dense classic workbench.");
            Assert.IsTrue(string.IsNullOrWhiteSpace(notice.Text), "Routine command-dispatch copy should not consume visible workbench space.");
        });
    }

    [TestMethod]
    public void Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues()
    {
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
    public void Non_classic_sections_surface_a_named_workbench_context_instead_of_an_untitled_row_dump()
    {
        WithLoadedRunnerHarness(harness =>
        {
            harness.SetActiveSectionForTesting("vehicles");

            Border contextBorder = harness.FindControl<Border>("SectionContextBorder");
            TextBlock contextTitle = harness.FindControl<TextBlock>("SectionContextTitleText");
            TextBlock contextSummary = harness.FindControl<TextBlock>("SectionContextSummaryText");

            harness.WaitUntil(() => contextBorder.IsVisible && !string.IsNullOrWhiteSpace(contextTitle.Text));

            Assert.IsTrue(contextBorder.IsVisible, "Non-classic sections must expose a named section context header.");
            Assert.AreEqual("Vehicles", contextTitle.Text);
            StringAssert.Contains(contextSummary.Text ?? string.Empty, "visible");
            StringAssert.Contains(contextSummary.Text ?? string.Empty, "Roadmaster");
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
                sectionId: "complexforms",
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
                sectionId: "mentorspirits",
                actionControlId: "contact_add",
                expectedTitle: "Add Contact",
                requiredFieldLabel: "Name",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "progress",
                actionControlId: "create_entry",
                expectedTitle: "Add Entry",
                requiredFieldLabel: "Entry Name",
                requiredActionId: "add");

            harness.Click("HelpMenuButton");
            harness.WaitUntil(() =>
            {
                MenuItem[] commands = SnapshotMenuCommands(harness.FindControl<MenuItem>("HelpMenuButton"));
                return commands.Any(command => string.Equals(command.Tag?.ToString(), "report_bug", StringComparison.Ordinal));
            });

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

            StringAssert.Contains(dialogBody, "/account/support");
            StringAssert.Contains(dialogBody, "/contact");
            StringAssert.Contains(dialogBody, "github.com/chummer5a/chummer5a/issues/new/choose");
            Assert.IsFalse(dialogBody.Contains("chummer-api", StringComparison.OrdinalIgnoreCase), "Support/report routes must stay public and must not expose internal Docker hosts.");
            Assert.IsTrue(harness.DialogActionIds().Contains("close"), "Support/report flow must expose an explicit close/confirm affordance.");
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
        string screenshotDirectory = ResolveScreenshotDirectory();
        string screenshotEvidencePath = Path.Combine(screenshotDirectory, "SCREENSHOT_CONTROL_EVIDENCE.generated.json");
        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, recursive: true);
        }

        Directory.CreateDirectory(screenshotDirectory);

        string[] expectedFiles =
        [
            GetVeteranCertificationReviewStep("toolstrip").ScreenshotFileName,
            GetVeteranCertificationReviewStep("menu").ScreenshotFileName,
            GetVeteranCertificationReviewStep("settings").ScreenshotFileName,
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
            GetVeteranCertificationReviewStep("master_index").ScreenshotFileName,
            GetVeteranCertificationReviewStep("roster").ScreenshotFileName,
            GetVeteranCertificationReviewStep("import").ScreenshotFileName
        ];

        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            VeteranCertificationCapturePacket packet = WithIsolatedHarness(harness =>
            {
                Dictionary<string, byte[]> captured = new(StringComparer.Ordinal);
                Dictionary<string, string> capturedHashes = new(StringComparer.Ordinal);
                Dictionary<string, ScreenshotUiEvidence> uiEvidence = new(StringComparer.Ordinal);

                void CaptureCurrentFrame(string fileName)
                {
                    byte[] pngBytes = Array.Empty<byte>();
                    ScreenshotUiEvidence currentEvidence = new(
                        Screenshot: fileName,
                        Theme: string.Empty,
                        DialogTitle: string.Empty,
                        DialogMessage: string.Empty,
                        DialogFieldLabels: [],
                        DialogFieldInputValues: [],
                        DialogActionIds: [],
                        VisibleNamedControlIds: [],
                        VisibleNamedControls: [],
                        VisibleTextSamples: [],
                        VisibleMenuCommandIds: [],
                        VisibleTabLabels: [],
                        VisibleSectionQuickActionIds: [],
                        SelectedListRowTexts: [],
                        PreviewText: string.Empty);
                    string screenshotHash = string.Empty;
                    string? duplicateScreenshot = null;

                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        Control screenshotRoot = harness.GetScreenshotRootControlForTesting();
                        pngBytes = harness.CaptureScreenshotBytes(screenshotRoot);
                        currentEvidence = harness.CaptureScreenshotUiEvidence(fileName, screenshotRoot);
                        screenshotHash = ComputeSha256Hex(pngBytes);
                        duplicateScreenshot = capturedHashes
                            .FirstOrDefault(entry => string.Equals(entry.Value, screenshotHash, StringComparison.Ordinal))
                            .Key;
                        if (string.IsNullOrEmpty(duplicateScreenshot))
                        {
                            break;
                        }

                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                        Thread.Sleep(20);
                        Dispatcher.UIThread.RunJobs();
                    }

                    captured[fileName] = pngBytes;
                    capturedHashes[fileName] = screenshotHash;
                    uiEvidence[fileName] = currentEvidence;

                    string duplicateDialogTitle = !string.IsNullOrEmpty(duplicateScreenshot)
                        && uiEvidence.TryGetValue(duplicateScreenshot, out ScreenshotUiEvidence? duplicateEvidence)
                        ? duplicateEvidence.DialogTitle
                        : string.Empty;
                    Assert.IsTrue(
                        string.IsNullOrEmpty(duplicateScreenshot),
                        $"Screenshot '{fileName}' dialog '{currentEvidence.DialogTitle}' duplicated rendered frame '{duplicateScreenshot}' dialog '{duplicateDialogTitle}'.");
                }

                harness.WaitForReady();

                harness.SetTheme(ThemeVariant.Light);
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("toolstrip").ScreenshotFileName);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton")).Length > 0);
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("menu").ScreenshotFileName);

                harness.CloseMenu("FileMenuButton");

                harness.PressKey(Key.G, RawInputModifiers.Control);
                AssertDialogContainsAll(
                    harness,
                    GetVeteranCertificationReviewStep("settings").RequiredDialogMarkers);
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("settings").ScreenshotFileName);

                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.Presenter.ImportCalls > 0);
                CaptureCurrentFrame(expectedFiles[3]);

                ListBox denseSectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => denseSectionRows.ItemCount > 0);
                object[] denseRows = SnapshotListBoxItems(denseSectionRows);
                Assert.IsTrue(denseRows.Length > 0, "Expected dense section rows before capturing dense familiarity proof.");
                denseSectionRows.SelectedItem = denseRows[0];
                harness.WaitUntil(() => ReferenceEquals(denseSectionRows.SelectedItem, denseRows[0]));
                CaptureCurrentFrame(expectedFiles[4]);

                harness.SetTheme(ThemeVariant.Dark);
                CaptureCurrentFrame(expectedFiles[5]);

                harness.SetTheme(ThemeVariant.Light);
                TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
                harness.WaitUntil(() =>
                    harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible
                    && SnapshotLoadedRunnerTabs(loadedRunnerTabStrip).Any(item =>
                        item.Id.Contains("info", StringComparison.OrdinalIgnoreCase)
                        || item.Label.Contains("Runner", StringComparison.OrdinalIgnoreCase)));
                NavigatorTabItem selectedTab = SnapshotLoadedRunnerTabs(loadedRunnerTabStrip)
                    .First(tab => !string.IsNullOrWhiteSpace(tab.Id)
                        && !string.Equals(tab.Id, harness.ShellPresenter.State.ActiveTabId, StringComparison.Ordinal));
                string selectedTabId = selectedTab.Id;
                harness.ClickLoadedRunnerTab(selectedTab.Label);
                harness.WaitUntil(() => harness.ShellPresenter.SelectedTabIds.Contains(selectedTabId));
                CaptureCurrentFrame(expectedFiles[6]);

                ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => sectionRows.ItemCount >= 8);
                object? cyberwareRow = SnapshotListBoxItems(sectionRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("cyberware[0] = Wired Reflexes 2", StringComparison.Ordinal) == true);
                Assert.IsNotNull(cyberwareRow, "Expected a cyberware row before capturing cyberware familiarity proof.");
                sectionRows.SelectedItem = cyberwareRow;
                harness.WaitUntil(() => ReferenceEquals(sectionRows.SelectedItem, cyberwareRow));
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                harness.Click("SectionQuickAction_cyberware_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Cyberware",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(expectedFiles[7]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.SetActiveSectionForTesting("vehicles");
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                ListBox vehicleRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => vehicleRows.ItemCount > 0);
                object? vehicleRow = SnapshotListBoxItems(vehicleRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("vehicles[0] = Roadmaster", StringComparison.Ordinal) == true);
                Assert.IsNotNull(vehicleRow, "Expected a vehicle row before capturing vehicle familiarity proof.");
                vehicleRows.SelectedItem = vehicleRow;
                harness.WaitUntil(() => ReferenceEquals(vehicleRows.SelectedItem, vehicleRow));
                CaptureCurrentFrame(expectedFiles[8]);

                harness.SetActiveSectionForTesting("contacts");
                ListBox contactRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => contactRows.ItemCount > 0);
                object? contactRow = SnapshotListBoxItems(contactRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("contacts[0] = Fixer", StringComparison.Ordinal) == true);
                Assert.IsNotNull(contactRow, "Expected a contact row before capturing contact familiarity proof.");
                contactRows.SelectedItem = contactRow;
                harness.WaitUntil(() => ReferenceEquals(contactRows.SelectedItem, contactRow));
                CaptureCurrentFrame(expectedFiles[9]);

                harness.SetActiveSectionForTesting("progress");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_create_entry")?.IsVisible == true);
                harness.Click("SectionQuickAction_create_entry");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(expectedFiles[10]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.SetActiveSectionForTesting("spells");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_spell_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_spell_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Spell",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(expectedFiles[11]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.SetActiveSectionForTesting("complexforms");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_matrix_program_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_matrix_program_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Program / Cyberdeck Item",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(expectedFiles[12]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.SetActiveSectionForTesting("initiationgrades");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_initiation_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_initiation_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Initiation / Submersion",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(expectedFiles[13]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.SetActiveSectionForTesting("attributes");
                ListBox attributeRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => attributeRows.ItemCount > 0);
                object? attributeRow = SnapshotListBoxItems(attributeRows).FirstOrDefault();
                Assert.IsNotNull(attributeRow, "Expected visible attributes rows before capturing character-creation familiarity proof.");
                attributeRows.SelectedItem = attributeRow;
                harness.WaitUntil(() => ReferenceEquals(attributeRows.SelectedItem, attributeRow));
                CaptureCurrentFrame(expectedFiles[14]);

                harness.Click("ToolsMenuButton");
                harness.WaitUntil(() =>
                {
                    MenuItem[] commands = SnapshotMenuCommands(harness.FindControl<MenuItem>("ToolsMenuButton"));
                    return commands.Any(command => string.Equals(command.Tag?.ToString(), "master_index", StringComparison.Ordinal));
                });
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("master_index", CancellationToken.None).
                harness.ClickMenuCommand("master_index");
                AssertDialogContainsAll(
                    harness,
                    GetVeteranCertificationReviewStep("master_index").RequiredDialogMarkers);
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("master_index").ScreenshotFileName);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.Click("ToolsMenuButton");
                harness.WaitUntil(() =>
                {
                    MenuItem[] commands = SnapshotMenuCommands(harness.FindControl<MenuItem>("ToolsMenuButton"));
                    return commands.Any(command => string.Equals(command.Tag?.ToString(), "character_roster", StringComparison.Ordinal));
                });
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("character_roster", CancellationToken.None).
                harness.ClickMenuCommand("character_roster");
                AssertDialogContainsAll(
                    harness,
                    GetVeteranCertificationReviewStep("roster").RequiredDialogMarkers);
                harness.WaitUntil(() =>
                    harness.ScreenshotRootContainsVisibleText("Selected Runner")
                    && harness.ScreenshotRootContainsVisibleText("Roster Entries"));
                harness.SetTheme(ThemeVariant.Dark);
                harness.SetTheme(ThemeVariant.Light);
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("roster").ScreenshotFileName);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() =>
                {
                    MenuItem[] commands = SnapshotMenuCommands(harness.FindControl<MenuItem>("FileMenuButton"));
                    return commands.Any(command => string.Equals(command.Tag?.ToString(), "open_character", StringComparison.Ordinal));
                });
                harness.ClickMenuCommand("open_character");
                AssertDialogContainsAll(
                    harness,
                    GetVeteranCertificationReviewStep("import").RequiredDialogMarkers);
                CaptureCurrentFrame(GetVeteranCertificationReviewStep("import").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                return new VeteranCertificationCapturePacket(captured, uiEvidence);
            });

            foreach ((string fileName, byte[] pngBytes) in packet.Screenshots)
            {
                File.WriteAllBytes(Path.Combine(screenshotDirectory, fileName), pngBytes);
            }

            ScreenshotControlEvidencePacket evidencePacket = new(
                ContractName: "chummer6-ui.ui_flagship_release_gate.screenshot_control_evidence",
                ScreenshotDirectory: screenshotDirectory,
                Entries: expectedFiles
                    .Select(fileName => packet.UiEvidence[fileName])
                    .ToArray());
            File.WriteAllText(
                screenshotEvidencePath,
                JsonSerializer.Serialize(
                    evidencePacket,
                    ScreenshotEvidenceJsonOptions));
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

        Assert.IsTrue(File.Exists(screenshotEvidencePath), "Expected screenshot control evidence was not created.");
        Assert.IsTrue(new FileInfo(screenshotEvidencePath).Length > 0, "Screenshot control evidence is empty.");
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
        HeadlessUnitTestSession session = GetHeadlessSession();
        return session.Dispatch(() =>
            {
                using FlagshipUiHarness harness = new();
                return assertion(harness);
            },
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static TResult WithIsolatedHarness<TResult>(Func<FlagshipUiHarness, TResult> assertion)
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
        finally
        {
            DisposeHeadlessSessionQuietly(session);
        }
    }

    private static HeadlessUnitTestSession GetHeadlessSession()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return _headlessSession
                    ?? throw new InvalidOperationException("Headless session was marked initialized without an active session.");
            }

            _headlessSession = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
            _headlessInitialized = true;
            return _headlessSession;
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
        HeadlessUnitTestSession session = GetHeadlessSession();
        return session.Dispatch(() =>
            {
                using RuntimeFlagshipUiHarness harness = new();
                return assertion(harness);
            },
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();
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

    private static string FindTestFilePath(string fileName)
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Tests", "TestFiles", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", fileName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", fileName),
            Path.Combine("/src", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer-presentation/Chummer.Tests/TestFiles", fileName)
        };

        string? match = candidates.FirstOrDefault(path => File.Exists(path));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate test file.", fileName);
        }

        return match;
    }

    private static string ResolveSourceFile(params string[] segments)
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(segments)),
            Path.Combine(AppContext.BaseDirectory, Path.Combine(segments)),
            Path.Combine("/docker/chummercomplete/chummer-presentation", Path.Combine(segments)),
            Path.Combine("/docker/chummercomplete/chummer6-ui", Path.Combine(segments))
        };

        string? match = candidates.FirstOrDefault(path => File.Exists(path));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate source file.", Path.Combine(segments));
        }

        return match;
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

        return Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                ".codex-studio",
                "out",
                "test-ui-flagship-gate",
                Guid.NewGuid().ToString("N")));
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

    private static VeteranCertificationReviewStep GetVeteranCertificationReviewStep(string surface)
        => VeteranCertificationReviewSteps.First(step => string.Equals(step.Surface, surface, StringComparison.Ordinal));

    private static void AssertDialogContainsAll(FlagshipUiHarness harness, IReadOnlyList<string> requiredTexts)
    {
        harness.WaitUntil(() =>
        {
            string dialogBody = string.Join(
                "\n",
                harness.FindDialogFieldTexts()
                    .Concat(harness.FindDialogFieldInputTexts())
                    .Concat([harness.FindControlOrDefault<TextBlock>("DialogMessageText")?.Text ?? string.Empty])
                    .Concat([harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text ?? string.Empty]));

            return requiredTexts.All(requiredText => dialogBody.Contains(requiredText, StringComparison.Ordinal));
        });
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

    private static string ComputeSha256Hex(byte[] data)
        => Convert.ToHexStringLower(SHA256.HashData(data));

    private static string NormalizeEvidenceText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ExtractEvidenceControlText(Control control)
    {
        return control switch
        {
            TextBlock textBlock => NormalizeEvidenceText(textBlock.Text),
            TextBox textBox => NormalizeEvidenceText(textBox.Text ?? textBox.Watermark?.ToString()),
            Button button => NormalizeEvidenceText(GetPrimaryButtonLabel(button)),
            MenuItem menuItem => NormalizeEvidenceText(menuItem.Header?.ToString()),
            _ => string.Empty,
        };
    }

    private static string[] CaptureVisibleTextSamples(Control root)
        => root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(textBlock => textBlock.IsVisible)
            .Select(textBlock => NormalizeEvidenceText(textBlock.Text))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] CaptureVisibleNamedControlIds(Control root)
        => root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control =>
                control.IsVisible
                && control.Bounds.Width > 0d
                && control.Bounds.Height > 0d
                && !string.IsNullOrWhiteSpace(control.Name))
            .Select(control => control.Name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static VisibleNamedControlEvidence[] CaptureVisibleNamedControls(Control root)
        => root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control =>
                control.IsVisible
                && control.Bounds.Width > 0d
                && control.Bounds.Height > 0d
                && !string.IsNullOrWhiteSpace(control.Name))
            .Select(control => new VisibleNamedControlEvidence(
                Name: control.Name ?? string.Empty,
                ControlType: control.GetType().Name,
                Text: ExtractEvidenceControlText(control),
                X: Math.Round(control.Bounds.X, 2),
                Y: Math.Round(control.Bounds.Y, 2),
                Width: Math.Round(control.Bounds.Width, 2),
                Height: Math.Round(control.Bounds.Height, 2)))
            .GroupBy(control => control.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(control => control.Name, StringComparer.Ordinal)
            .ToArray();

    private sealed record VeteranCertificationReviewStep(
        string Surface,
        string ScreenshotFileName,
        string Gesture,
        string Chummer5aBaseline,
        string[] RequiredDialogMarkers);

    private sealed record VeteranCertificationCapturePacket(
        Dictionary<string, byte[]> Screenshots,
        Dictionary<string, ScreenshotUiEvidence> UiEvidence);

    private sealed record ScreenshotControlEvidencePacket(
        string ContractName,
        string ScreenshotDirectory,
        ScreenshotUiEvidence[] Entries);

    private sealed record ScreenshotUiEvidence(
        string Screenshot,
        string Theme,
        string DialogTitle,
        string DialogMessage,
        string[] DialogFieldLabels,
        string[] DialogFieldInputValues,
        string[] DialogActionIds,
        string[] VisibleNamedControlIds,
        VisibleNamedControlEvidence[] VisibleNamedControls,
        string[] VisibleTextSamples,
        string[] VisibleMenuCommandIds,
        string[] VisibleTabLabels,
        string[] VisibleSectionQuickActionIds,
        string[] SelectedListRowTexts,
        string PreviewText);

    private sealed record VisibleNamedControlEvidence(
        string Name,
        string ControlType,
        string Text,
        double X,
        double Y,
        double Width,
        double Height);

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
        harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);
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
                    harness.Presenter.ImportCalls > 0
                    && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                    && harness.FindControlOrDefault<Control>("QuickStartContainer") is null);
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
                    && !harness.State.IsBusy);
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
        HeadlessUnitTestSession session = GetHeadlessSession();
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

    [ClassCleanup]
    public static void CleanupHeadlessSession()
        => ResetHeadlessSession();

    private static void ResetHeadlessSession()
    {
        lock (HeadlessInitLock)
        {
            DisposeHeadlessSessionQuietly(_headlessSession);
            _headlessSession = null;
            _headlessInitialized = false;
        }
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

    private static void RaiseClick(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpStandaloneUi();
    }

    private static void OpenMenuItem(MenuItem menuItem)
    {
        Assert.IsTrue(menuItem.IsEnabled, $"Menu item '{menuItem.Name}' must be enabled.");
        menuItem.IsSubMenuOpen = true;
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        PumpStandaloneUi();
    }

    private static void RaiseMenuItemClick(MenuItem menuItem)
    {
        Assert.IsTrue(menuItem.IsEnabled, $"Menu item '{menuItem.Header}' must be enabled.");
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        PumpStandaloneUi();
    }

    private static MenuItem[] SnapshotRootMenuItems(Menu menu)
        => menu.Items
            .OfType<MenuItem>()
            .ToArray();

    private static MenuItem[] SnapshotMenuCommands(MenuItem rootMenuItem)
        => rootMenuItem.Items
            .OfType<MenuItem>()
            .Where(static item => !string.IsNullOrWhiteSpace(item.Tag?.ToString()))
            .ToArray();

    private static NavigatorTabItem[] SnapshotLoadedRunnerTabs(TabStrip tabStrip)
        => tabStrip.Items
            .OfType<NavigatorTabItem>()
            .ToArray();

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
            if (control is Button button)
            {
                Assert.IsTrue(button.IsEnabled, $"Control '{controlName}' must be enabled.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                Assert.IsTrue(menuItem.IsEnabled, $"Control '{controlName}' must be enabled.");
                menuItem.IsSubMenuOpen = true;
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void ClickLoadedRunnerTab(string labelFragment)
        {
            TabStrip tabStrip = FindControl<TabStrip>("LoadedRunnerTabStrip");
            NavigatorTabItem tabItem = SnapshotLoadedRunnerTabs(tabStrip)
                .FirstOrDefault(tab => tab.Label.Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssertFailedException($"Loaded-runner tab containing '{labelFragment}' was not found.");
            Assert.IsTrue(tabItem.Enabled, $"Loaded-runner tab '{labelFragment}' must be enabled.");
            tabStrip.SelectedItem = tabItem;
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

        public void InvokeDialogAction(string actionId)
        {
            Button actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Dialog action '{actionId}' was not found.");

            actionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        public void ClickMenuCommand(string commandId)
        {
            MenuItem commandItem = Window.GetVisualDescendants()
                .OfType<MenuItem>()
                .SelectMany(SnapshotMenuCommands)
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Menu command '{commandId}' was not found.");

            commandItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Pump();
        }

        public void CloseMenu(string controlName)
        {
            MenuItem menuItem = FindControl<MenuItem>(controlName);
            menuItem.IsSubMenuOpen = false;
            Pump();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, ToPhysicalKey(key), ToKeySymbol(key));
            Pump();
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
            => CaptureScreenshotBytes(GetScreenshotRootControl());

        public byte[] CaptureScreenshotBytes(Control screenshotRoot)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (screenshotRoot.GetVisualRoot() is Visual visualRoot)
                {
                    visualRoot.InvalidateVisual();
                }

                screenshotRoot.InvalidateMeasure();
                screenshotRoot.InvalidateArrange();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                screenshotRoot.InvalidateVisual();
                Pump();
                PixelSize pixelSize = new(
                    Math.Max(1, (int)Math.Ceiling(screenshotRoot.Bounds.Width)),
                    Math.Max(1, (int)Math.Ceiling(screenshotRoot.Bounds.Height)));
                using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
                bitmap.Render(screenshotRoot);
                using MemoryStream output = new();
                bitmap.Save(output);
                if (output.Length > 0)
                {
                    return output.ToArray();
                }
            }

            throw new AssertFailedException("No rendered frame was available for screenshot capture.");
        }

        public ScreenshotUiEvidence CaptureScreenshotUiEvidence(string screenshotFileName)
            => CaptureScreenshotUiEvidence(screenshotFileName, GetScreenshotRootControl());

        public ScreenshotUiEvidence CaptureScreenshotUiEvidence(string screenshotFileName, Control screenshotRoot)
        {
            string[] visibleNamedControlIds = CaptureVisibleNamedControlIds(screenshotRoot);
            VisibleNamedControlEvidence[] visibleNamedControls = CaptureVisibleNamedControls(screenshotRoot);
            string[] visibleTextSamples = CaptureVisibleTextSamples(screenshotRoot);
            string[] visibleMenuCommandIds = screenshotRoot.GetVisualDescendants()
                .OfType<MenuItem>()
                .Where(menuItem =>
                    menuItem.IsVisible
                    && !string.IsNullOrWhiteSpace(menuItem.Tag?.ToString())
                    && (menuItem.Name is null || !menuItem.Name.EndsWith("MenuButton", StringComparison.Ordinal)))
                .Select(menuItem => menuItem.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            string[] visibleTabLabels = FindDescendantOrDefault<TabStrip>(screenshotRoot, "LoadedRunnerTabStrip") is { IsVisible: true } tabStrip
                ? SnapshotLoadedRunnerTabs(tabStrip)
                    .Select(tab => NormalizeEvidenceText(tab.Label))
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];
            string[] visibleSectionQuickActionIds = screenshotRoot.GetVisualDescendants()
                .OfType<Control>()
                .Where(control =>
                    control.IsVisible
                    && !string.IsNullOrWhiteSpace(control.Name)
                    && control.Name.StartsWith("SectionQuickAction_", StringComparison.Ordinal))
                .Select(control => control.Name!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            string[] selectedListRowTexts = screenshotRoot.GetVisualDescendants()
                .OfType<ListBox>()
                .Where(listBox => listBox.IsVisible && listBox.Bounds.Height > 0d && listBox.Bounds.Width > 0d)
                .Select(listBox => NormalizeEvidenceText(listBox.SelectedItem?.ToString()))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string previewText = FindDescendantOrDefault<TextBox>(screenshotRoot, "SectionPreviewBox") is { } previewBox
                ? NormalizeEvidenceText(previewBox.Text)
                : string.Empty;
            string dialogTitle = FindDescendantOrDefault<TextBlock>(screenshotRoot, "DialogTitleText") is { IsVisible: true } titleBlock
                ? NormalizeEvidenceText(titleBlock.Text)
                : string.Empty;
            string dialogMessage = FindDescendantOrDefault<TextBlock>(screenshotRoot, "DialogMessageText") is { IsVisible: true } messageBlock
                ? NormalizeEvidenceText(messageBlock.Text)
                : string.Empty;
            Panel? dialogFieldsPanel = FindDescendantOrDefault<Panel>(screenshotRoot, "DialogFieldsPanel")
                ?? FindDescendantOrDefault<Panel>(screenshotRoot, "DialogFieldsHost");
            string[] dialogFieldLabels = dialogFieldsPanel is { IsVisible: true }
                ? dialogFieldsPanel.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(textBlock => NormalizeEvidenceText(textBlock.Text))
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];
            string[] dialogFieldInputValues = dialogFieldsPanel is { IsVisible: true }
                ? dialogFieldsPanel.GetVisualDescendants()
                    .OfType<TextBox>()
                    .Select(textBox => NormalizeEvidenceText(textBox.Text ?? textBox.Watermark?.ToString()))
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];
            Panel? dialogActionsPanel = FindDescendantOrDefault<Panel>(screenshotRoot, "DialogActionsPanel")
                ?? FindDescendantOrDefault<Panel>(screenshotRoot, "DialogActionsHost");
            string[] dialogActionIds = dialogActionsPanel is { IsVisible: true }
                ? dialogActionsPanel.GetVisualDescendants()
                    .OfType<Button>()
                    .Select(button => button.Tag?.ToString() ?? string.Empty)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : [];

            return new ScreenshotUiEvidence(
                Screenshot: screenshotFileName,
                Theme: screenshotRoot.ActualThemeVariant.ToString(),
                DialogTitle: dialogTitle,
                DialogMessage: dialogMessage,
                DialogFieldLabels: dialogFieldLabels,
                DialogFieldInputValues: dialogFieldInputValues,
                DialogActionIds: dialogActionIds,
                VisibleNamedControlIds: visibleNamedControlIds,
                VisibleNamedControls: visibleNamedControls,
                VisibleTextSamples: visibleTextSamples,
                VisibleMenuCommandIds: visibleMenuCommandIds,
                VisibleTabLabels: visibleTabLabels,
                VisibleSectionQuickActionIds: visibleSectionQuickActionIds,
                SelectedListRowTexts: selectedListRowTexts,
                PreviewText: previewText);
        }

        private Window GetScreenshotRoot()
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string? activeDialogTitle = _adapter.State.ActiveDialog?.Title;
                DesktopDialogWindow[] dialogWindows = desktop.Windows
                    .OfType<DesktopDialogWindow>()
                    .ToArray();
                DesktopDialogWindow? dialogWindow = dialogWindows
                    .LastOrDefault(window =>
                        window.IsVisible
                        && !string.IsNullOrWhiteSpace(activeDialogTitle)
                        && string.Equals(window.Title, activeDialogTitle, StringComparison.Ordinal))
                    ?? dialogWindows.LastOrDefault(window => window.IsVisible);
                if (dialogWindow is not null)
                {
                    return dialogWindow;
                }
            }

            return Window;
        }

        private Control GetScreenshotRootControl()
        {
            Window screenshotRoot = GetScreenshotRoot();
            return screenshotRoot is DesktopDialogWindow
                ? screenshotRoot
                : screenshotRoot.Content as Control ?? screenshotRoot;
        }

        public Control GetScreenshotRootControlForTesting()
            => GetScreenshotRootControl();

        public T? FindControlOnScreenshotRootOrDefault<T>(string name)
            where T : Control
        {
            return GetScreenshotRootControl()
                .GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        public bool ScreenshotRootContainsVisibleText(string text)
        {
            return GetScreenshotRootControl()
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(textBlock =>
                    textBlock.IsVisible
                    && textBlock.Bounds.Width > 0d
                    && textBlock.Bounds.Height > 0d
                    && (textBlock.Text?.Contains(text, StringComparison.Ordinal) ?? false));
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
            return Window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
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
            return fieldsHost.Children
                .OfType<Panel>()
                .SelectMany(panel => panel.Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public string[] FindDialogFieldInputTexts()
        {
            Panel fieldsHost = FindControl<Panel>("DialogFieldsHost");
            return fieldsHost.Children
                .OfType<Panel>()
                .SelectMany(panel => panel.Children.OfType<TextBox>().Select(text => text.Text ?? string.Empty))
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
            if (control is Button button)
            {
                Assert.IsTrue(button.IsEnabled, $"Control '{controlName}' must be enabled.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                Assert.IsTrue(menuItem.IsEnabled, $"Control '{controlName}' must be enabled.");
                menuItem.IsSubMenuOpen = true;
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
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
            return Window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 4000)
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

            Assert.Fail("Timed out waiting for runtime-backed UI condition.");
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
        public List<string> ExecutedDialogActionIds { get; } = [];
        public int SaveCalls { get; private set; }

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

            Publish(_state with
            {
                ActiveDialog = dialog with
                {
                    Fields = dialog.Fields
                        .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                            ? field with { Value = value ?? string.Empty }
                            : field)
                        .ToArray()
                }
            });

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
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
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
        AppCommandDefinition[] commands =
        [
            new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5),
            new("edit", "menu.edit", "menu", false, true, RulesetDefaults.Sr5),
            new("special", "menu.special", "menu", false, true, RulesetDefaults.Sr5),
            new("tools", "menu.tools", "menu", false, true, RulesetDefaults.Sr5),
            new("windows", "menu.windows", "menu", false, true, RulesetDefaults.Sr5),
            new("help", "menu.help", "menu", false, true, RulesetDefaults.Sr5),
            new("open_character", "command.open_character", "file", false, true, RulesetDefaults.Sr5),
            new("save_character", "command.save_character", "file", true, true, RulesetDefaults.Sr5),
            new("global_settings", "command.global_settings", "tools", false, true, RulesetDefaults.Sr5),
            new("master_index", "command.master_index", "tools", false, true, RulesetDefaults.Sr5),
            new("character_roster", "command.character_roster", "tools", false, true, RulesetDefaults.Sr5),
            new("report_bug", "command.report_bug", "help", false, true, RulesetDefaults.Sr5),
            new("about", "command.about", "help", false, true, RulesetDefaults.Sr5)
        ];

        return ShellState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            PreferredRulesetId = RulesetDefaults.Sr5,
            Commands = commands,
            MenuRoots = commands.Where(command => string.Equals(command.Group, "menu", StringComparison.Ordinal)).ToArray(),
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-info", "Info", "summary", "character", true, true, RulesetDefaults.Sr5),
                new NavigationTabDefinition("tab-gear", "Gear", "gear", "character", true, true, RulesetDefaults.Sr5),
                new NavigationTabDefinition("tab-cyberware", "Cyberware", "cyberware", "character", true, true, RulesetDefaults.Sr5)
            ],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
    }

    private static string SourcePath(params string[] segments)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "Chummer.Blazor")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new DirectoryNotFoundException("Could not resolve chummer-presentation source root from the test output directory.");
        }

        return Path.GetFullPath(Path.Combine([current.FullName, .. segments]));
    }
}
