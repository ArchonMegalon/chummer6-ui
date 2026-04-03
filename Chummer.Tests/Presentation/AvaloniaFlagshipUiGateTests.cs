#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
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
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Hosting.Presentation;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaFlagshipUiGateTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            Assert.IsTrue(harness.FindControl<Button>("FileMenuButton").IsEnabled, "File menu must stay enabled after real shell bootstrap.");
            Assert.IsTrue(harness.FindControl<Button>("HelpMenuButton").IsEnabled, "Help menu must stay enabled after real shell bootstrap.");
            harness.Click("FileMenuButton");

            harness.WaitUntil(() =>
            {
                Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return host is not null && host.Children.Count > 0;
            });

            Panel menuHost = harness.FindControl<Panel>("MenuCommandsHost");
            string[] visibleCommands = menuHost.Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.Contains(visibleCommands, "open character");
            CollectionAssert.Contains(visibleCommands, "save character");
        });
    }

    [TestMethod]
    public void Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Panel menuPanel = harness.FindControl<Panel>("MenuBarPanel");
            Button[] menuButtons = menuPanel.Children.OfType<Button>().ToArray();
            string[] menuLabels = menuButtons
                .Select(button => button.Content?.ToString() ?? string.Empty)
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

            foreach (Button button in menuButtons)
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
                {
                    Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                    return string.Equals(harness.ShellPresenter.State.OpenMenuId, menuId, StringComparison.Ordinal)
                        && host is not null
                        && host.Children.Count > 0
                        && harness.FindControl<Control>("MenuCommandsRegion").IsVisible;
                });
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
                ("DesktopHomeButton", "Desktop Home"),
                ("CampaignWorkspaceButton", "Campaign Workspace"),
                ("LoadDemoRunnerButton", "Load Demo Runner"),
                ("ImportFileButton", "Import Character File"),
                ("SaveButton", "Save Workspace"),
                ("SettingsButton", "Settings"),
                ("ImportRawButton", "Import Raw XML"),
                ("UpdateStatusButton", "Update Status"),
                ("InstallLinkingButton", "Link This Copy"),
                ("SupportButton", "Open Support"),
                ("ReportIssueButton", "Report Issue"),
                ("CloseWorkspaceButton", "Close Active Workspace"),
            ];

            foreach ((string buttonName, string expectedLabel) in expectedButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Workbench action '{buttonName}' must stay visible.");
                Assert.IsTrue(button.IsEnabled, $"Workbench action '{buttonName}' must stay enabled.");
                Assert.IsInstanceOfType<string>(button.Content, $"Workbench action '{buttonName}' must stay a flat classic toolbar label, not a dashboard tile.");
                CollectionAssert.Contains(GetButtonTextLines(button), expectedLabel, $"Workbench action '{buttonName}' must keep its classic desktop label.");
                Assert.AreEqual(1, GetButtonTextLines(button).Length, $"Workbench action '{buttonName}' must not add a secondary caption line.");
                Assert.IsTrue(button.Bounds.Width > 0d && button.Bounds.Height > 0d, $"Workbench action '{buttonName}' must keep a visible desktop footprint.");
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
                harness.FindControl<Button>("DesktopHomeButton").Bounds.Height,
                harness.FindControl<Button>("CampaignWorkspaceButton").Bounds.Height,
                harness.FindControl<Button>("LoadDemoRunnerButton").Bounds.Height,
                harness.FindControl<Button>("ImportFileButton").Bounds.Height,
                harness.FindControl<Button>("SaveButton").Bounds.Height,
                harness.FindControl<Button>("SettingsButton").Bounds.Height,
                harness.FindControl<Button>("ImportRawButton").Bounds.Height,
                harness.FindControl<Button>("UpdateStatusButton").Bounds.Height,
                harness.FindControl<Button>("InstallLinkingButton").Bounds.Height,
                harness.FindControl<Button>("SupportButton").Bounds.Height,
                harness.FindControl<Button>("ReportIssueButton").Bounds.Height,
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
                "DesktopHomeButton",
                "CampaignWorkspaceButton",
                "LoadDemoRunnerButton",
                "ImportFileButton",
                "SaveButton",
                "SettingsButton",
                "UpdateStatusButton",
                "InstallLinkingButton",
                "SupportButton",
                "ReportIssueButton",
            ];

            foreach (string buttonName in menuButtons.Concat(actionButtons))
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsTrue(GetButtonTextLines(button).Length > 0, $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            harness.Click("FileMenuButton");
            harness.WaitUntil(() =>
            {
                Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return host is not null
                    && host.Children.Count > 0
                    && harness.FindControl<Control>("MenuCommandsRegion").IsVisible;
            });
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
            harness.WaitUntil(() =>
            {
                Panel? menuHost = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return menuHost is not null && menuHost.Children.Count > 0;
            });
        });
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
                Assert.IsTrue(harness.FindControl<Control>("QuickStartContainer").IsVisible);
                Assert.AreEqual(
                    GetPrimaryButtonLabel(harness.FindControl<Button>("LoadDemoRunnerButton")),
                    GetPrimaryButtonLabel(harness.FindControl<Button>("LoadDemoRunnerQuickActionButton")),
                    "First-run CTA wording should stay aligned with the primary toolstrip action.");
                harness.Click("LoadDemoRunnerQuickActionButton");
                harness.WaitUntil(() => harness.State.WorkspaceId is not null && harness.State.Session.OpenWorkspaces.Count > 0);
                harness.WaitUntil(() => !harness.FindControl<Control>("QuickStartContainer").IsVisible);
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

            Panel menuPanel = harness.FindControl<Panel>("MenuBarPanel");
            string[] menuLabels = menuPanel.Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString() ?? string.Empty)
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
            Control workspaceStripRegion = harness.FindControl<Control>("WorkspaceStripRegion");
            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control centerShellRegion = harness.FindControl<Control>("CenterShellRegion");
            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Control summaryHeaderRegion = harness.FindControl<Control>("SummaryHeaderRegion");
            Control sectionRegion = harness.FindControl<Control>("SectionRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");

            Assert.IsTrue(toolStripRegion.Bounds.Width > workspaceStripRegion.Bounds.Width, "The immediate quick-action strip must dominate the row under the menu.");
            Assert.IsTrue(leftNavigatorRegion.Bounds.Width >= 240d && leftNavigatorRegion.Bounds.Width <= 360d, "Left navigation must stay dense and desktop-scaled.");
            Assert.IsTrue(rightShellRegion.Bounds.Width >= 240d && rightShellRegion.Bounds.Width <= 360d, "Right inspector/coach area must stay present instead of collapsing into overlays.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > leftNavigatorRegion.Bounds.Width, "The central editing workbench must remain the dominant pane.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > rightShellRegion.Bounds.Width, "The central editing workbench must remain the dominant pane.");
            Assert.IsTrue(menuBarRegion.Bounds.Height <= 72d, "The top menu row must read like desktop chrome, not a hero header.");
            Assert.IsTrue(statusStripRegion.Bounds.Height <= 72d, "The bottom strip must stay compact like the legacy status posture.");

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
            ListBox tabs = harness.FindControl<ListBox>("NavigationTabsList");
            TextBlock tabsHeader = harness.FindControl<TextBlock>("NavigationTabsHeader");
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            Panel tabStripPanel = harness.FindControl<Panel>("LoadedRunnerTabStripPanel");
            Control quickStart = harness.FindControl<Control>("QuickStartContainer");

            harness.WaitUntil(() => !quickStart.IsVisible);
            harness.WaitUntil(() => tabStrip.IsVisible && tabStripPanel.Children.Count > 0);

            Assert.IsTrue(tabs.IsVisible);
            Assert.IsTrue(tabStrip.IsVisible);
            StringAssert.Contains(tabsHeader.Text ?? string.Empty, "Tabs");
            Assert.IsTrue(tabs.Bounds.Width > 0d && tabs.Bounds.Height > 0d, "Navigation tabs should render with a visible desktop footprint.");
            Assert.IsTrue(tabStripPanel.Children.OfType<Button>().Any(button =>
                (button.Content?.ToString() ?? string.Empty).Contains("Runner", StringComparison.Ordinal)),
                "Loaded runner tab strip should surface a visible Runner tab button.");
            object[] tabItems = SnapshotListBoxItems(tabs);
            if (tabItems.Length > 0)
            {
                StringAssert.Contains(tabItems[0].ToString() ?? string.Empty, "Runner");
            }
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

            ListBox tabs = harness.FindControl<ListBox>("NavigationTabsList");
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            Control quickStart = harness.FindControl<Control>("QuickStartContainer");

            harness.WaitUntil(() =>
                !quickStart.IsVisible
                && tabs.ItemCount >= 2
                && sectionRows.ItemCount > 0
                && !string.IsNullOrWhiteSpace(preview.Text));

            Assert.IsTrue(tabs.IsVisible, "Legacy frmCareer parity requires a visible loaded-runner tab posture.");
            Assert.IsTrue(sectionRows.IsVisible, "Legacy frmCareer parity requires a visible dense section/workbench list.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(preview.Text), "Legacy frmCareer parity requires a visible detail/preview pane.");

            string[] tabItems = SnapshotListBoxItems(tabs).Select(item => item.ToString() ?? string.Empty).ToArray();
            Assert.IsTrue(tabItems.Any(item => item.Contains("profile", StringComparison.OrdinalIgnoreCase)), "Legacy frmCareer parity requires an info/profile navigation landmark.");
            Assert.IsTrue(tabItems.Any(item => item.Contains("gear", StringComparison.OrdinalIgnoreCase)), "Legacy frmCareer parity requires a gear navigation landmark.");
            string previewPayload = preview.Text ?? string.Empty;
            bool hasLegacyOrWorkflowSectionMarker =
                previewPayload.Contains("\"sectionId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"workflowId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"progressionTimelines\"", StringComparison.Ordinal);
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

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

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

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

            CollectionAssert.Contains(rowText, "gear.weapons[0] = Ares Alpha");
            CollectionAssert.Contains(rowText, "gear.armor[0] = Armor Jacket");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"combat\"");
            StringAssert.Contains(notice.Text ?? string.Empty, "Notice:");
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
    public void Magic_matrix_and_consumables_workflows_execute_with_specific_dialog_fields_and_confirm_actions()
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
                Panel? menuHost = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return menuHost is not null
                    && menuHost.Children.OfType<Button>().Any(button => string.Equals(button.Tag?.ToString(), "report_bug", StringComparison.Ordinal));
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
        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, recursive: true);
        }

        Directory.CreateDirectory(screenshotDirectory);

        string[] expectedFiles =
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
            "12-magic-matrix-dialog-light.png"
        ];

        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, byte[]> screenshots = WithHarness(harness =>
            {
                Dictionary<string, byte[]> captured = new(StringComparer.Ordinal);

                harness.WaitForReady();

                harness.SetTheme(ThemeVariant.Light);
                captured[expectedFiles[0]] = harness.CaptureScreenshotBytes();

                harness.Click("FileMenuButton");
                harness.WaitUntil(() =>
                {
                    Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                    return host is not null && host.Children.Count > 0;
                });
                captured[expectedFiles[1]] = harness.CaptureScreenshotBytes();

                harness.Click("FileMenuButton");
                harness.WaitUntil(() =>
                {
                    Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                    return host is not null && host.Children.Count == 0;
                });

                harness.PressKey(Key.G, RawInputModifiers.Control);
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                captured[expectedFiles[2]] = harness.CaptureScreenshotBytes();

                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.Presenter.ImportCalls > 0);
                captured[expectedFiles[3]] = harness.CaptureScreenshotBytes();

                ListBox denseSectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => denseSectionRows.ItemCount > 0);
                object[] denseRows = SnapshotListBoxItems(denseSectionRows);
                Assert.IsTrue(denseRows.Length > 0, "Expected dense section rows before capturing dense familiarity proof.");
                denseSectionRows.SelectedItem = denseRows[0];
                harness.WaitUntil(() => ReferenceEquals(denseSectionRows.SelectedItem, denseRows[0]));
                captured[expectedFiles[4]] = harness.CaptureScreenshotBytes();

                harness.SetTheme(ThemeVariant.Dark);
                captured[expectedFiles[5]] = harness.CaptureScreenshotBytes();

                harness.SetTheme(ThemeVariant.Light);
                ListBox tabs = harness.FindControl<ListBox>("NavigationTabsList");
                harness.WaitUntil(() => tabs.ItemCount > 0 && tabs.SelectedItem is not null);
                captured[expectedFiles[6]] = harness.CaptureScreenshotBytes();

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
                captured[expectedFiles[7]] = harness.CaptureScreenshotBytes();
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
                captured[expectedFiles[8]] = harness.CaptureScreenshotBytes();

                harness.SetActiveSectionForTesting("contacts");
                ListBox contactRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => contactRows.ItemCount > 0);
                object? contactRow = SnapshotListBoxItems(contactRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("contacts[0] = Fixer", StringComparison.Ordinal) == true);
                Assert.IsNotNull(contactRow, "Expected a contact row before capturing contact familiarity proof.");
                contactRows.SelectedItem = contactRow;
                harness.WaitUntil(() => ReferenceEquals(contactRows.SelectedItem, contactRow));
                captured[expectedFiles[9]] = harness.CaptureScreenshotBytes();

                harness.SetActiveSectionForTesting("progress");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_create_entry")?.IsVisible == true);
                harness.Click("SectionQuickAction_create_entry");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));
                captured[expectedFiles[10]] = harness.CaptureScreenshotBytes();
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
                captured[expectedFiles[11]] = harness.CaptureScreenshotBytes();
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);

                return captured;
            });

            foreach ((string fileName, byte[] pngBytes) in screenshots)
            {
                File.WriteAllBytes(Path.Combine(screenshotDirectory, fileName), pngBytes);
            }
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
        finally
        {
            DisposeHeadlessSessionQuietly(session);
        }
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
                harness.WaitUntil(() => harness.Presenter.ImportCalls > 0);
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
        }

        public MainWindow Window { get; }
        public RecordingCharacterOverviewPresenter Presenter => _presenter;
        public RecordingShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() => ShellPresenter.InitializeCalls > 0 && _presenter.InitializeCalls > 0);
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

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
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

            Point? translated = actionButton.TranslatePoint(
                new Point(actionButton.Bounds.Width / 2d, actionButton.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate dialog action '{actionId}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
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
            Panel menuHost = FindControl<Panel>("MenuCommandsHost");
            Button button = menuHost.Children
                .OfType<Button>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Menu command '{commandId}' was not found.");

            Point? translated = button.TranslatePoint(
                new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate menu command '{commandId}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers);
            Pump();
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
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Pump();
            using var bitmap = Window.CaptureRenderedFrame();
            if (bitmap is null)
            {
                throw new AssertFailedException("No rendered frame was available for screenshot capture.");
            }

            using MemoryStream output = new();
            bitmap.Save(output);
            return output.ToArray();
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
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
        public Task HandleUiControlAsync(string controlId, CancellationToken ct)
        {
            Publish(_state with
            {
                Error = null,
                ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, _state.Preferences)
            });
            return Task.CompletedTask;
        }
        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
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
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
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
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
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
                new NavigationTabDefinition("tab-info", "Info", "summary", "character", true, true, RulesetDefaults.Sr5)
            ],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
    }
}
