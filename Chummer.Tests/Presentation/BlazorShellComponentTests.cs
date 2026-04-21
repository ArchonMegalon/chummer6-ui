#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bunit;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.Components.Shared;
using Chummer.Blazor.Components.Shell;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Journal;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class BlazorShellComponentTests
{
    [TestMethod]
    public void MenuBar_renders_open_menu_items_and_applies_enablement_state()
    {
        IReadOnlyList<AppCommandDefinition> menuRoots =
        [
            new AppCommandDefinition("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5)
        ];
        IReadOnlyList<AppCommandDefinition> menuCommands =
        [
            new AppCommandDefinition("save_character", "command.save", "file", true, true, RulesetDefaults.Sr5),
            new AppCommandDefinition("close_character", "command.close", "file", true, true, RulesetDefaults.Sr5)
        ];

        using var context = new BunitContext();
        IRenderedComponent<MenuBar> cut = context.Render<MenuBar>(parameters => parameters
            .Add(component => component.MenuRoots, menuRoots)
            .Add(component => component.OpenMenuId, "file")
            .Add(component => component.IsBusy, false)
            .Add(component => component.MenuCommands, menuId =>
                string.Equals(menuId, "file", StringComparison.Ordinal)
                    ? menuCommands
                    : Array.Empty<AppCommandDefinition>())
            .Add(component => component.IsCommandEnabled,
                command => string.Equals(command.Id, "save_character", StringComparison.Ordinal)));

        Assert.HasCount(1, cut.FindAll(".menu-btn"));
        StringAssert.Contains(cut.Find(".menu-bar").ClassName, "classic-menu-bar");
        StringAssert.Contains(cut.Find(".menu-btn").ClassName, "classic-menu-button");
        StringAssert.Contains(cut.Find(".menu-btn").ClassName, "active");
        Assert.AreEqual("File", cut.Find(".menu-btn").TextContent.Trim());

        IReadOnlyList<AngleSharp.Dom.IElement> menuButtons = cut.FindAll(".menu-item");
        Assert.HasCount(2, menuButtons);
        StringAssert.Contains(cut.Find(".menu-dropdown").ClassName, "classic-menu-dropdown");
        Assert.IsFalse(menuButtons[0].HasAttribute("disabled"));
        Assert.IsTrue(menuButtons[1].HasAttribute("disabled"));
    }

    [TestMethod]
    public void MenuBar_invokes_toggle_and_execute_callbacks()
    {
        string? toggledMenuId = null;
        string? executedCommandId = null;

        using var context = new BunitContext();
        IRenderedComponent<MenuBar> cut = context.Render<MenuBar>(parameters => parameters
            .Add(component => component.MenuRoots,
            [
                new AppCommandDefinition("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5)
            ])
            .Add(component => component.OpenMenuId, "file")
            .Add(component => component.MenuCommands, menuId =>
                string.Equals(menuId, "file", StringComparison.Ordinal)
                    ? new[]
                    {
                        new AppCommandDefinition("save_character", "command.save", "file", true, true, RulesetDefaults.Sr5)
                    }
                    : Array.Empty<AppCommandDefinition>())
            .Add(component => component.IsCommandEnabled, _ => true)
            .Add(component => component.ToggleMenuRequested, (Action<string>)(menuId => toggledMenuId = menuId))
            .Add(component => component.ExecuteCommandRequested, (Action<string>)(commandId => executedCommandId = commandId)));

        cut.Find(".menu-btn").Click();
        cut.Find(".menu-item").Click();

        Assert.AreEqual("file", toggledMenuId);
        Assert.AreEqual("save_character", executedCommandId);
    }

    [TestMethod]
    public void ToolStrip_applies_selected_and_disabled_states()
    {
        string? executedCommandId = null;

        using var context = new BunitContext();
        IRenderedComponent<ToolStrip> cut = context.Render<ToolStrip>(parameters => parameters
            .Add(component => component.Commands,
            [
                new AppCommandDefinition("save_character", "command.save", "file", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("print_character", "command.print", "file", true, true, RulesetDefaults.Sr5)
            ])
            .Add(component => component.LastCommandId, "print_character")
            .Add(component => component.IsBusy, false)
            .Add(component => component.IsCommandEnabled,
                command => string.Equals(command.Id, "print_character", StringComparison.Ordinal))
            .Add(component => component.ExecuteCommandRequested, (Action<string>)(commandId => executedCommandId = commandId)));

        IReadOnlyList<AngleSharp.Dom.IElement> toolButtons = cut.FindAll(".tool-btn");
        Assert.HasCount(2, toolButtons);
        StringAssert.Contains(cut.Find(".tool-strip").ClassName, "classic-tool-strip");
        StringAssert.Contains(toolButtons[0].ClassName, "classic-tool-button");
        Assert.IsTrue(toolButtons[0].HasAttribute("disabled"));
        Assert.IsFalse(toolButtons[1].HasAttribute("disabled"));
        StringAssert.Contains(toolButtons[1].ClassName, "selected");

        toolButtons[1].Click();
        Assert.AreEqual("print_character", executedCommandId);
    }

    [TestMethod]
    public void ToolStrip_renders_classic_group_divider_between_copy_and_new()
    {
        using var context = new BunitContext();
        IRenderedComponent<ToolStrip> cut = context.Render<ToolStrip>(parameters => parameters
            .Add(component => component.Commands,
            [
                new AppCommandDefinition("save_character", "command.save", "file", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("print_character", "command.print", "file", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("copy", "command.copy", "edit", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("new_character", "command.new", "file", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("open_character", "command.open", "file", true, true, RulesetDefaults.Sr5)
            ])
            .Add(component => component.IsCommandEnabled, _ => true));

        Assert.HasCount(1, cut.FindAll(".tool-divider"));
    }

    [TestMethod]
    public void MdiStrip_shows_unsaved_marker_for_workspace_without_save_receipt()
    {
        CharacterWorkspaceId ws1 = new("ws-1");
        CharacterWorkspaceId ws2 = new("ws-2");
        OpenWorkspaceState dirtyWorkspace = new(ws1, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr5, HasSavedWorkspace: false);
        OpenWorkspaceState savedWorkspace = new(ws2, "Neo Runner", "NR", DateTimeOffset.UtcNow.AddMinutes(-1), RulesetDefaults.Sr5, HasSavedWorkspace: true);

        using var context = new BunitContext();
        IRenderedComponent<MdiStrip> cut = context.Render<MdiStrip>(parameters => parameters
            .Add(component => component.OpenWorkspaces, [dirtyWorkspace, savedWorkspace])
            .Add(component => component.ActiveWorkspaceId, ws1)
            .Add(component => component.IsBusy, false));

        IReadOnlyList<AngleSharp.Dom.IElement> docs = cut.FindAll(".mdi-doc");
        Assert.HasCount(2, docs);
        StringAssert.Contains(docs[0].TextContent, "*");
        StringAssert.Contains(docs[0].GetAttribute("title"), "Shadowrun 5");
        StringAssert.Contains(docs[0].GetAttribute("title"), "main editor");
        Assert.IsLessThan(0, docs[1].TextContent.IndexOf('*'));
    }

    [TestMethod]
    public void MdiStrip_uses_ruleset_specific_empty_state_when_no_workspace_is_open()
    {
        using var context = new BunitContext();
        IRenderedComponent<MdiStrip> cut = context.Render<MdiStrip>(parameters => parameters
            .Add(component => component.OpenWorkspaces, Array.Empty<OpenWorkspaceState>())
            .Add(component => component.RulesetId, RulesetDefaults.Sr6)
            .Add(component => component.IsBusy, false));

        StringAssert.Contains(cut.Markup, "No open SR6 character");
    }

    [TestMethod]
    public void WorkspaceLeftPane_renders_shell_controls_and_invokes_callbacks()
    {
        CharacterWorkspaceId workspaceId = new("ws-1");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr5);
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace], [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-info",
            ActiveActionId = "tab-info.validate",
            IsBusy = false
        };

        string? openedWorkspaceId = null;
        string? closedWorkspaceId = null;
        WorkspaceSurfaceActionDefinition? executedAction = null;
        string? executedWorkflowSurfaceActionId = null;

        WorkspaceSurfaceActionDefinition summaryAction = new(
            Id: "tab-info.validate",
            Label: "Validate",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Validate,
            TargetId: "validate",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        WorkflowSurfaceActionBinding summarySurface = new(
            SurfaceId: "surface.summary",
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Label: "Refresh Summary",
            ActionId: "summary",
            RegionId: ShellRegionIds.SectionPane,
            LayoutToken: WorkflowLayoutTokens.CareerWorkbench);
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = [openWorkspace];
        IReadOnlyList<NavigationTabDefinition> navigationTabs =
        [
            new NavigationTabDefinition("tab-create", "Create", "build-lab", "character", true, true, RulesetDefaults.Sr5),
            new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5),
            new NavigationTabDefinition("tab-skills", "Skills", "skills", "character", true, true, RulesetDefaults.Sr5)
        ];
        IReadOnlyList<WorkspaceSurfaceActionDefinition> workspaceActions = [summaryAction];
        IReadOnlyList<WorkflowSurfaceActionBinding> workflowSurfaceActions = [summarySurface];

        using var context = new BunitContext();
        IRenderedComponent<WorkspaceLeftPane> cut = context.Render<WorkspaceLeftPane>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.OpenWorkspaces, openWorkspaces)
            .Add(component => component.ActiveWorkspaceId, workspaceId)
            .Add(component => component.ActiveTabId, "tab-info")
            .Add(component => component.NavigationTabs, navigationTabs)
            .Add(component => component.ActiveWorkspaceActions, workspaceActions)
            .Add(component => component.ActiveWorkflowSurfaceActions, workflowSurfaceActions)
            .Add(component => component.IsNavigationTabEnabled,
                tab => string.Equals(tab.Id, "tab-info", StringComparison.Ordinal))
            .Add(component => component.OpenWorkspaceRequested, (Action<string>)(workspace => openedWorkspaceId = workspace))
            .Add(component => component.CloseWorkspaceRequested, (Action<string>)(workspace => closedWorkspaceId = workspace))
            .Add(component => component.ExecuteWorkspaceActionRequested,
                (Action<WorkspaceSurfaceActionDefinition>)(action => executedAction = action))
            .Add(component => component.ExecuteWorkflowSurfaceRequested, (Action<string>)(actionId => executedWorkflowSurfaceActionId = actionId)));

        StringAssert.Contains(cut.Markup, "SR5 Editor Actions");
        StringAssert.Contains(cut.Markup, "SR5 Editor Flows");
        StringAssert.Contains(cut.Markup, "SR5 Characters");
        StringAssert.Contains(cut.Markup, "Ares Runner (AR) · Shadowrun 5 · main editor");
        StringAssert.Contains(cut.Markup, "Character Summary");
        Assert.AreEqual("Validate & Rebind", cut.Find(".section-actions .action-button").TextContent.Trim());

        cut.Find(".navigator .command-button").Click();
        cut.Find(".navigator .mini-btn").Click();
        cut.Find(".section-actions .action-button").Click();
        cut.Find("button[data-workflow-surface='surface.summary']").Click();

        Assert.AreEqual("ws-1", openedWorkspaceId);
        Assert.AreEqual("ws-1", closedWorkspaceId);
        Assert.AreEqual("tab-info.validate", executedAction?.Id);
        Assert.AreEqual("summary", executedWorkflowSurfaceActionId);
    }

    [TestMethod]
    public void WorkspaceLeftPane_hides_secondary_left_rail_sections_until_workspace_context_exists()
    {
        WorkspaceSurfaceActionDefinition summaryAction = new(
            Id: "tab-info.validate",
            Label: "Validate",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Validate,
            TargetId: "validate",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        WorkflowSurfaceActionBinding summarySurface = new(
            SurfaceId: "surface.summary",
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Label: "Refresh Summary",
            ActionId: "summary",
            RegionId: ShellRegionIds.SectionPane,
            LayoutToken: WorkflowLayoutTokens.CareerWorkbench);

        using var context = new BunitContext();
        IRenderedComponent<WorkspaceLeftPane> cut = context.Render<WorkspaceLeftPane>(parameters => parameters
            .Add(component => component.State, CharacterOverviewState.Empty)
            .Add(component => component.OpenWorkspaces, Array.Empty<OpenWorkspaceState>())
            .Add(component => component.ActiveWorkspaceId, null)
            .Add(component => component.ActiveWorkspaceActions, new[] { summaryAction })
            .Add(component => component.ActiveWorkflowSurfaceActions, new[] { summarySurface }));

        Assert.AreEqual(0, cut.FindAll(".section-actions").Count, "Classic first paint should not show the secondary action rail before a workspace is active.");
        Assert.AreEqual(0, cut.FindAll(".controls").Count, "Classic first paint should not show workflow chrome before a workspace is active.");
    }

    [TestMethod]
    public void SummaryHeader_renders_classic_workbench_tabs_and_invokes_selection()
    {
        string? selectedTabId = null;
        IReadOnlyList<NavigationTabDefinition> navigationTabs =
        [
            new NavigationTabDefinition("tab-create", "Create", "build-lab", "character", true, true, RulesetDefaults.Sr5),
            new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5),
            new NavigationTabDefinition("tab-skills", "Skills", "skills", "character", true, true, RulesetDefaults.Sr5)
        ];

        using var context = new BunitContext();
        IRenderedComponent<SummaryHeader> cut = context.Render<SummaryHeader>(parameters => parameters
            .Add(component => component.State, CharacterOverviewState.Empty)
            .Add(component => component.ShellSurface, ShellSurfaceState.Empty with { ActiveRulesetId = RulesetDefaults.Sr5 })
            .Add(component => component.ActiveTabId, "tab-info")
            .Add(component => component.NavigationTabs, navigationTabs)
            .Add(component => component.IsNavigationTabEnabled,
                tab => string.Equals(tab.Id, "tab-info", StringComparison.Ordinal))
            .Add(component => component.SelectTabRequested, (Action<string>)(tabId => selectedTabId = tabId)));

        AngleSharp.Dom.IElement enabledTab = cut.Find(".workbench-tab-strip #tab-info");
        AngleSharp.Dom.IElement createTab = cut.Find(".workbench-tab-strip #tab-create");
        AngleSharp.Dom.IElement disabledTab = cut.Find(".workbench-tab-strip #tab-skills");
        StringAssert.Contains(cut.Find(".workbench-tab-strip").ClassName, "classic-tab-strip");
        StringAssert.Contains(enabledTab.ClassName, "classic-workbench-tab");
        Assert.IsFalse(enabledTab.HasAttribute("disabled"));
        Assert.IsTrue(disabledTab.HasAttribute("disabled"));
        StringAssert.Contains(enabledTab.ClassName, "active");
        StringAssert.Contains(cut.Markup, "SR5 Editor Tabs");
        Assert.AreEqual("Runner", enabledTab.TextContent.Trim());
        Assert.AreEqual("Character", createTab.TextContent.Trim());

        enabledTab.Click();

        Assert.AreEqual("tab-info", selectedTabId);
    }

    [TestMethod]
    public void OpenWorkspaceTree_renders_open_and_close_actions()
    {
        CharacterWorkspaceId workspaceId = new("ws-1");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr5);
        string? openedWorkspaceId = null;
        string? closedWorkspaceId = null;

        using var context = new BunitContext();
        IRenderedComponent<OpenWorkspaceTree> cut = context.Render<OpenWorkspaceTree>(parameters => parameters
            .Add(component => component.OpenWorkspaces, [openWorkspace])
            .Add(component => component.ActiveWorkspaceId, workspaceId)
            .Add(component => component.IsBusy, false)
            .Add(component => component.OpenWorkspaceRequested, (Action<string>)(workspace => openedWorkspaceId = workspace))
            .Add(component => component.CloseWorkspaceRequested, (Action<string>)(workspace => closedWorkspaceId = workspace)));

        cut.Find(".navigator .command-button").Click();
        cut.Find(".navigator .mini-btn").Click();

        Assert.AreEqual("ws-1", openedWorkspaceId);
        Assert.AreEqual("ws-1", closedWorkspaceId);
        StringAssert.Contains(cut.Find(".navigator").ClassName, "classic-navigator");
        StringAssert.Contains(cut.Find(".navigator .command-button").ClassName, "classic-navigator-button");
        StringAssert.Contains(cut.Find(".navigator .command-button").ClassName, "selected");
        StringAssert.Contains(cut.Markup, "SR5 Characters");
        StringAssert.Contains(cut.Markup, "Shadowrun 5");
        StringAssert.Contains(cut.Markup, "main editor");
        Assert.AreEqual("ws-1", cut.Find(".navigator .command-button").GetAttribute("title"));
        Assert.AreEqual(0, cut.FindAll(".navigator .command-button .hint").Count, "Classic dossier rows must not print workspace ids into the visible left rail.");
    }

    [TestMethod]
    public void ImportPanel_renders_ruleset_specific_copy_and_accepts_all_native_formats()
    {
        using var context = new BunitContext();
        IRenderedComponent<ImportPanel> cut = context.Render<ImportPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr4)
            .Add(component => component.IsBusy, false)
            .Add(component => component.RawImportXml, string.Empty)
            .Add(component => component.LatestPortabilityActivity, new WorkspacePortabilityActivity(
                "Last portable import",
                new WorkspacePortabilityReceipt(
                    FormatId: WorkspacePortabilityFormatIds.PortableDossierV1,
                    CompatibilityState: WorkspacePortabilityCompatibilityStates.CompatibleWithWarnings,
                    ContextSummary: "Imported Runner Blue into sr4 with a bounded source toggle change.",
                    ReceiptSummary: "Import landed with a governed receipt.",
                    ProvenanceSummary: "Payload hash abcdef123456 entered workspace ws-import.",
                    PayloadSha256: "abcdef1234567890",
                    NextSafeAction: "Review the before-after environment diff before campaign handoff.",
                    SupportedExchangeModes: [WorkspacePortabilityExchangeModes.InspectOnly],
                    Notes:
                    [
                        new WorkspacePortabilityNote(
                            Code: "source-toggle",
                            Severity: WorkspacePortabilityNoteSeverities.Warning,
                            Summary: "Street Magic source toggle changed during import.")
                    ]))));

        StringAssert.Contains(cut.Markup, "Import SR4 Character File");
        StringAssert.Contains(cut.Markup, "Primary format: .chum4 with parity-safe XML fallback.");
        StringAssert.Contains(cut.Markup, "(no SR4 character file selected)");
        StringAssert.Contains(cut.Markup, "SR4 Oracle Debug Import");
        StringAssert.Contains(cut.Markup, "Import receipt");
        StringAssert.Contains(cut.Markup, "Import landed with a governed receipt.");
        StringAssert.Contains(cut.Markup, "Rule environment");
        StringAssert.Contains(cut.Markup, "chummer.portable-dossier.v1; compatible-with-warnings; inspect-only; payload abcdef1234567890.");
        StringAssert.Contains(cut.Markup, "Imported Runner Blue into sr4 with a bounded source toggle change.");
        StringAssert.Contains(cut.Markup, "Before");
        StringAssert.Contains(cut.Markup, "After");
        StringAssert.Contains(cut.Markup, "Explain receipt");
        StringAssert.Contains(cut.Markup, "Payload hash abcdef123456 entered workspace ws-import.");
        StringAssert.Contains(cut.Markup, "Support reuse");
        StringAssert.Contains(cut.Markup, "Review the before-after environment diff before campaign handoff.");
        StringAssert.Contains(cut.Markup, "Support can cite payload abcdef1234567890 with compatible-with-warnings compatibility.");
        StringAssert.Contains(cut.Markup, "Street Magic source toggle changed during import.");
        Assert.IsNotNull(cut.Find("[data-import-explain-receipt]"));
        Assert.AreEqual(".chum4,.chum5,.chum6,.xml,text/xml,application/xml", cut.Find("input[type='file']").GetAttribute("accept"));
        Assert.AreEqual("Import SR4 Raw XML", cut.Find("details button").TextContent.Trim());
    }

    [TestMethod]
    public void CommandPanel_and_ResultPanel_render_ruleset_specific_headings_and_fallback_copy()
    {
        CharacterOverviewState state = CharacterOverviewState.Empty;

        using var context = new BunitContext();
        IRenderedComponent<CommandPanel> commandCut = context.Render<CommandPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr6)
            .Add(component => component.State, state)
            .Add(component => component.Commands, Array.Empty<AppCommandDefinition>()));
        IRenderedComponent<ResultPanel> resultCut = context.Render<ResultPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr5)
            .Add(component => component.State, state));

        StringAssert.Contains(commandCut.Markup, "SR6 Setup Tools");
        StringAssert.Contains(commandCut.Markup, "No SR6 setup-workbench tools are currently available.");
        StringAssert.Contains(resultCut.Markup, "SR5 Editor Result");
        StringAssert.Contains(resultCut.Markup, "Shadowrun 5 stays on the main desktop editor");
        StringAssert.Contains(resultCut.Markup, "SR5 editor is ready");
    }

    [TestMethod]
    public void ResultPanel_renders_last_portability_activity_details()
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Notice = "Portable export prepared.",
            LatestPortabilityActivity = new WorkspacePortabilityActivity(
                "Last portable export",
                new WorkspacePortabilityReceipt(
                    FormatId: WorkspacePortabilityFormatIds.PortableDossierV1,
                    CompatibilityState: WorkspacePortabilityCompatibilityStates.CompatibleWithWarnings,
                    ContextSummary: "Runner Blue is packaged as a portable dossier on sr5.",
                    ReceiptSummary: "Portable export is ready, but inspect the package before merge or governed replace on a receiving surface.",
                    ProvenanceSummary: "Portable package portable-ws-1 captured payload hash abcdef123456 from workspace ws-1 at 2026-03-30T00:00:00.0000000+00:00.",
                    PayloadSha256: "abcdef1234567890",
                    NextSafeAction: "Open inspect-only first on the receiving surface and verify the missing sections before merge or replace.",
                    SupportedExchangeModes:
                    [
                        WorkspacePortabilityExchangeModes.InspectOnly,
                        WorkspacePortabilityExchangeModes.Merge,
                        WorkspacePortabilityExchangeModes.Replace
                    ],
                    Notes:
                    [
                        new WorkspacePortabilityNote(
                            Code: "section-coverage",
                            Severity: WorkspacePortabilityNoteSeverities.Warning,
                            Summary: "Portable package is missing contacts; receiving surfaces should inspect before governed replace.")
                    ]))
        };

        using var context = new BunitContext();
        IRenderedComponent<ResultPanel> cut = context.Render<ResultPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr5)
            .Add(component => component.State, state));

        StringAssert.Contains(cut.Markup, "Last portable export");
        StringAssert.Contains(cut.Markup, "Runner Blue is packaged as a portable dossier on sr5.");
        StringAssert.Contains(cut.Markup, "Open inspect-only first on the receiving surface");
        StringAssert.Contains(cut.Markup, "inspect-only, merge, replace");
        StringAssert.Contains(cut.Markup, "Portable package is missing contacts");
    }

    [TestMethod]
    public void SectionPane_switches_between_placeholder_and_section_payload()
    {
        using var context = new BunitContext();
        IRenderedComponent<SectionPane> emptyCut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, CharacterOverviewState.Empty));

        StringAssert.Contains(emptyCut.Markup, "Select a tab to render a workspace section");

        CharacterOverviewState sectionState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = "skills",
            ActiveSectionJson = "{\"skills\":1}",
            ActiveSectionRows = [new SectionRowState("skills[0].name", "Pistols")]
        };

        IRenderedComponent<SectionPane> sectionCut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, sectionState));

        Assert.HasCount(1, sectionCut.FindAll(".section-table tbody tr"));
        StringAssert.Contains(sectionCut.Markup, "Pistols");
        Assert.IsFalse(sectionCut.Markup.Contains("{\"skills\":1}", StringComparison.Ordinal), "The default section pane must not dump raw JSON payloads into the visible workbench.");
    }

    [TestMethod]
    public void SectionPane_formats_named_context_for_collection_sections()
    {
        using var context = new BunitContext();
        CharacterOverviewState sectionState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = "vehicles",
            ActiveSectionJson = "{\"section\":\"vehicles\"}",
            ActiveSectionRows = [new SectionRowState("vehicles[0]", "Roadmaster · Armor 16 / Handling 3")]
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, sectionState));

        StringAssert.Contains(cut.Markup, "Vehicles");
        StringAssert.Contains(cut.Markup, "1 visible entry");
        StringAssert.Contains(cut.Markup, "Roadmaster");
        StringAssert.Contains(cut.Markup, "Vehicle 1");
        Assert.IsFalse(cut.Markup.Contains("vehicles[0]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation()
    {
        using var context = new BunitContext();
        CharacterOverviewState browseState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = "browse",
            ActiveSectionJson = "{\"WorkspaceId\":\"browse-gear\"}",
            ActiveBrowseWorkspace = new BrowseWorkspaceState(
                WorkspaceId: "browse-gear",
                WorkflowId: "workflow.browse",
                DialogId: "dlg-gear",
                DialogTitle: "Browse Gear",
                DialogMode: SelectionDialogModes.MultiSelect,
                CanConfirm: true,
                ConfirmActionId: "confirm",
                CancelActionId: "cancel",
                QueryText: "armor",
                SortId: "name",
                SortDirection: BrowseSortDirections.Ascending,
                TotalCount: 5000,
                Presets:
                [
                    new BrowseWorkspacePresetState("preset.street", "Street Kit", true, true)
                ],
                Facets:
                [
                    new BrowseWorkspaceFacetState(
                        "source",
                        "Source",
                        BrowseFacetKinds.MultiSelect,
                        true,
                        [new BrowseWorkspaceFacetOptionState("official", "Official", 2, true, null)]),
                    new BrowseWorkspaceFacetState(
                        "pack",
                        "Pack",
                        BrowseFacetKinds.MultiSelect,
                        true,
                        [new BrowseWorkspaceFacetOptionState("street", "Street", 1, true, null)])
                ],
                Results:
                [
                    new BrowseWorkspaceResultItemState(
                        "armor-jacket",
                        "Armor Jacket",
                        true,
                        null,
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["Availability"] = "8R" },
                        true),
                    new BrowseWorkspaceResultItemState(
                        "helmet",
                        "Helmet",
                        true,
                        null,
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["Availability"] = "6R" },
                        false)
                ],
                SelectedItems:
                [
                    new SelectionSummaryItem("armor-jacket", "Armor Jacket", "8R")
                ],
                ActiveDetail: new BrowseItemDetail(
                    "armor-jacket",
                    "Armor Jacket",
                    ["Armored clothing"],
                    "explain.armor_jacket"),
                ActiveResultIndex: 0,
                ActiveResultItemId: "armor-jacket",
                QueryOffset: 200,
                QueryLimit: 50)
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, browseState));

        StringAssert.Contains(cut.Markup, "Browse Gear");
        StringAssert.Contains(cut.Markup, "Showing 201-202 of 5000");
        StringAssert.Contains(cut.Markup, "Street Kit");
        StringAssert.Contains(cut.Markup, "Official");
        StringAssert.Contains(cut.Markup, "Street");
        StringAssert.Contains(cut.Markup, "Armor Jacket");
        StringAssert.Contains(cut.Markup, "Armored clothing");
        Assert.AreEqual("listbox", cut.Find("[data-browse-results]").GetAttribute("role"));
        Assert.AreEqual("browse-option-armor-jacket", cut.Find("[data-browse-results]").GetAttribute("aria-activedescendant"));
        Assert.AreEqual("option", cut.Find("[data-browse-item='armor-jacket']").GetAttribute("role"));
        Assert.AreEqual("true", cut.Find("[data-browse-item='armor-jacket']").GetAttribute("aria-selected"));

        cut.Find("[data-browse-shell='browse-gear']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Find("[data-browse-item='helmet']").ClassName, "active");
            Assert.AreEqual("browse-option-helmet", cut.Find("[data-browse-results]").GetAttribute("aria-activedescendant"));
            Assert.AreEqual("true", cut.Find("[data-browse-item='helmet']").GetAttribute("aria-selected"));
            Assert.AreEqual("false", cut.Find("[data-browse-item='armor-jacket']").GetAttribute("aria-selected"));
        });
        Assert.IsFalse(cut.Markup.Contains("Armored clothing", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SectionPane_renders_build_lab_projection_from_contract_payload()
    {
        using var context = new BunitContext();
        CharacterOverviewState buildLabState = CharacterOverviewState.Empty with
        {
            ActiveSectionId = "build-lab",
            ActiveSectionJson = "{\"WorkspaceId\":\"lab-intake\"}",
            ActiveBuildLab = new BuildLabConceptIntakeState(
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
                        true),
                    new BuildLabIntakeField(
                        "table",
                        "Table Constraints",
                        BuildLabFieldKinds.Multiline,
                        "Keep matrix scenes short.",
                        null,
                        "Pulled from table profile")
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
                        Warnings:
                        [
                            new BuildLabVariantWarning("astral-gap", "Astral gap", "Needs astral backup.", BuildLabWarningKinds.Trap, true)
                        ],
                        OverlapBadges:
                        [
                            new BuildLabBadge("face-overlap", "Light face overlap", BuildLabBadgeKinds.Overlap)
                        ],
                        Actions:
                        [
                            new BuildLabActionDescriptor("inspect-social", "Inspect Timeline", BuildLabSurfaceIds.ProgressionTimelineRail, true)
                        ],
                        ExplainEntryId: "buildlab.variant.social")
                ],
                ProgressionTimelines:
                [
                    new BuildLabProgressionTimeline(
                        TimelineId: "timeline.social",
                        Title: "Social Operator Ladder",
                        Summary: "25 / 50 / 100 Karma checkpoints.",
                        VariantId: "variant.social",
                        Steps:
                        [
                            new BuildLabProgressionStep(
                                "social-25",
                                25,
                                "Opener",
                                "Table-ready lead.",
                                Outcomes:
                                [
                                    new BuildLabVariantMetric("prep", "Prep speed", "Fast")
                                ],
                                MilestoneBadges:
                                [
                                    new BuildLabBadge("25", "25 Karma", BuildLabBadgeKinds.Milestone, true)
                                ],
                                RiskBadges: [],
                                ExplainEntryId: "buildlab.timeline.social-25"),
                            new BuildLabProgressionStep(
                                "social-50",
                                50,
                                "Reliability",
                                "Fallback lanes solidify.",
                                Outcomes:
                                [
                                    new BuildLabVariantMetric("coverage", "Coverage", "Improved")
                                ],
                                MilestoneBadges:
                                [
                                    new BuildLabBadge("50", "50 Karma", BuildLabBadgeKinds.Milestone, true)
                                ],
                                RiskBadges: [],
                                ExplainEntryId: "buildlab.timeline.social-50"),
                            new BuildLabProgressionStep(
                                "social-100",
                                100,
                                "Anchor",
                                "Campaign-ready anchor.",
                                Outcomes:
                                [
                                    new BuildLabVariantMetric("coverage", "Coverage", "Broad")
                                ],
                                MilestoneBadges:
                                [
                                    new BuildLabBadge("100", "100 Karma", BuildLabBadgeKinds.Milestone, true)
                                ],
                                RiskBadges:
                                [
                                    new BuildLabBadge("blur", "Role blur", BuildLabBadgeKinds.Risk)
                                ],
                                ExplainEntryId: "buildlab.timeline.social-100")
                        ],
                        SourceDocumentId: "source.timeline")
                ],
                ExportPayloads:
                [
                    new BuildLabExportPayload(
                        PayloadId: "payload.social-operator",
                        Title: "Ops-first Social Operator",
                        Summary: "Generic hand-off payload for either Build Idea Card or local template creation.",
                        PayloadKind: "build-lab-handoff",
                        Fields:
                        [
                            new BuildLabExportField("concept", "Concept", "Street Face"),
                            new BuildLabExportField("table-fit", "Table fit", "Ops-first", true)
                        ],
                        VariantId: "variant.social",
                        TimelineId: "timeline.social",
                        QueryText: "street face ops-first",
                        SourceDocumentId: "source.timeline")
                ],
                ExportTargets:
                [
                    new BuildLabExportTarget(
                        TargetId: "target.build-idea-card",
                        Label: "Build Idea Card",
                        TargetKind: BuildLabExportTargetKinds.BuildIdeaCard,
                        WorkflowId: "workflow.coach.build-ideas",
                        Enabled: true,
                        Description: "Open grounded Build Idea Card search with the current intake payload.",
                        PayloadId: "payload.social-operator",
                        ActionId: "handoff-build-idea",
                        Badges:
                        [
                            new BuildLabBadge("build-idea", "Searchable", BuildLabBadgeKinds.Export, true)
                        ]),
                    new BuildLabExportTarget(
                        TargetId: "target.character-template",
                        Label: "Character Template",
                        TargetKind: BuildLabExportTargetKinds.CharacterTemplate,
                        WorkflowId: "workflow.templates.character",
                        Enabled: true,
                        Description: "Seed a reusable local template without re-entering the intake fields.",
                        PayloadId: "payload.social-operator",
                        ActionId: "handoff-template",
                        Badges:
                        [
                            new BuildLabBadge("template", "Local-first", BuildLabBadgeKinds.Export)
                        ])
                ],
                Actions:
                [
                    new BuildLabActionDescriptor("handoff-build-idea", "Hand Off", BuildLabSurfaceIds.ExportRail, true, "target.build-idea-card"),
                    new BuildLabActionDescriptor("handoff-template", "Save As Template", BuildLabSurfaceIds.ExportRail, true, "target.character-template")
                ],
                ExplainEntryId: "buildlab.intake.concept",
                SourceDocumentId: "source.table-profile",
                CanContinue: true,
                NextSafeAction: "Rebind the active runtime before export.",
                RuntimeCompatibilitySummary: "One quick-action binding still needs review.",
                CampaignFitSummary: "Best fit is an ops-first crew with sparse matrix scenes.",
                SupportClosureSummary: "Support can cite the same runtime fingerprint after handoff.",
                TeamCoverage: new BuildLabTeamCoverageProjection(
                    Summary: "2 of 3 required crew roles are covered before handoff; one deliberate face overlap stays visible while astral support remains missing.",
                    CoverageSummary: "Coverage score stays stable with Face and Legwork already covered before the first campaign handoff.",
                    RolePressureSummary: "Role pressure stays light because the duplicate face lane is intentional, but astral support still needs a partner runner.",
                    MissingRoleTags: ["astral"],
                    CoveredRoleTags: ["face", "legwork"],
                    DuplicateRoleTags: ["face"],
                    ExplainEntryId: "buildlab.teamcoverage.ops-first"),
                Watchouts:
                [
                    "No recap-safe publication is attached yet."
                ])
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, buildLabState));

        StringAssert.Contains(cut.Markup, "Build Lab Intake");
        StringAssert.Contains(cut.Markup, "Street Face");
        StringAssert.Contains(cut.Markup, "Ops-first");
        StringAssert.Contains(cut.Markup, "Runtime-backed");
        StringAssert.Contains(cut.Markup, "Compare Variants");
        StringAssert.Contains(cut.Markup, "buildlab.intake.concept");
        StringAssert.Contains(cut.Markup, "source.table-profile");
        StringAssert.Contains(cut.Markup, "Variant Comparison");
        StringAssert.Contains(cut.Markup, "Social Operator");
        StringAssert.Contains(cut.Markup, "Astral gap");
        StringAssert.Contains(cut.Markup, "data-build-lab-warning-kind");
        StringAssert.Contains(cut.Markup, "25 / 50 / 100 Karma");
        StringAssert.Contains(cut.Markup, "100 Karma");
        StringAssert.Contains(cut.Markup, "data-build-lab-timeline-badges");
        StringAssert.Contains(cut.Markup, "Export + Hand-off");
        StringAssert.Contains(cut.Markup, "Ops-first Social Operator");
        StringAssert.Contains(cut.Markup, "workflow.coach.build-ideas");
        StringAssert.Contains(cut.Markup, "Hand Off -&gt; Build Idea Card");
        StringAssert.Contains(cut.Markup, "Planner + team coverage");
        StringAssert.Contains(cut.Markup, "Covered roles: Face | Legwork");
        StringAssert.Contains(cut.Markup, "Missing roles: Astral");
        StringAssert.Contains(cut.Markup, "Duplicate roles: Face");
        StringAssert.Contains(cut.Markup, "Light face overlap");
        StringAssert.Contains(cut.Markup, "strongest coverage checkpoint at 100 Karma");
        StringAssert.Contains(cut.Markup, "Decision rail");
        StringAssert.Contains(cut.Markup, "Rebind the active runtime before export.");
        StringAssert.Contains(cut.Markup, "Support can cite the same runtime fingerprint after handoff.");
        StringAssert.Contains(cut.Markup, "Build blocker receipt");
        StringAssert.Contains(cut.Markup, "Explain receipt");
        StringAssert.Contains(cut.Markup, "Rule environment");
        StringAssert.Contains(cut.Markup, "Environment diff");
        StringAssert.Contains(cut.Markup, "One quick-action binding still needs review. -&gt; Rebind the active runtime before export.");
        StringAssert.Contains(cut.Markup, "One quick-action binding still needs review.");
        Assert.IsNotNull(cut.Find("[data-build-blocker-explain-receipt]"));
        StringAssert.Contains(cut.Markup, "data-build-lab-export-target");
        StringAssert.Contains(cut.Markup, "data-build-lab-optimizer-rail");
    }

    [TestMethod]
    public void GmBoardFeed_renders_tactical_cards_instead_of_generic_feed()
    {
        using var context = new BunitContext();
        IRenderedComponent<GmBoardFeed> cut = context.Render<GmBoardFeed>(parameters => parameters
            .Add(component => component.InterruptionBudget, 55)
            .Add(component => component.CurrentSessionContext, "Pass 2")
            .Add(component => component.SessionContexts, ["Pass 1", "Pass 2", "Scene break"])
            .Add(component => component.AutonomyLevel, "Tactical")
            .Add(component => component.MutedUntilLabel, "Spider muted for 15 min")
            .Add(component => component.Cards,
            [
                new GmBoardFeed.GmBoardCard(
                    Id: "spider-1",
                    Source: "Spider Feed",
                    Kind: "Escalation",
                    Title: "Trace is heating up",
                    Summary: "Matrix pressure has crossed into the current pass.",
                    Severity: "high",
                    Timestamp: "08:42 UTC",
                    Expiry: "Expires in 8m30s",
                    InitiativeSlot: "Pass 2",
                    Target: "Hostile decker",
                    PrimaryActionId: "trace-lock",
                    PrimaryActionLabel: "Lock trace lane",
                    Alerts:
                    [
                        "Condition monitor risk",
                        "NPC response ready"
                    ],
                    MinimumAutonomy: "Low",
                    ContextSnapshot: "Pass 2",
                    InvalidatesOnContextShift: true,
                    RefreshActionId: "refresh-trace",
                    RefreshActionLabel: "Refresh trace lane",
                    IsPinned: true)
            ]));

        StringAssert.Contains(cut.Markup, "GM Ops Board");
        StringAssert.Contains(cut.Markup, "Session context");
        StringAssert.Contains(cut.Markup, "Current lane: Pass 2");
        StringAssert.Contains(cut.Markup, "Autonomy");
        StringAssert.Contains(cut.Markup, "Initiative Rail");
        StringAssert.Contains(cut.Markup, "data-gm-board-card");
        StringAssert.Contains(cut.Markup, "Spider Feed");
        StringAssert.Contains(cut.Markup, "Lock trace lane");
        StringAssert.Contains(cut.Markup, "Pinned");
        StringAssert.Contains(cut.Markup, "Spider muted for 15 min");
        StringAssert.Contains(cut.Markup, "chummer-card-spider");
        Assert.IsFalse(cut.Markup.Contains("chat-log", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GmBoardFeed_renders_stale_banners_and_refresh_actions_after_context_shift()
    {
        using var context = new BunitContext();
        IRenderedComponent<GmBoardFeed> cut = context.Render<GmBoardFeed>(parameters => parameters
            .Add(component => component.InterruptionBudget, 55)
            .Add(component => component.CurrentSessionContext, "Scene break")
            .Add(component => component.SessionContexts, ["Pass 1", "Pass 2", "Scene break"])
            .Add(component => component.AutonomyLevel, "Tactical")
            .Add(component => component.Cards,
            [
                new GmBoardFeed.GmBoardCard(
                    Id: "spider-1",
                    Source: "Spider Feed",
                    Kind: "Escalation",
                    Title: "Trace is heating up",
                    Summary: "Matrix pressure has crossed into the current pass.",
                    Severity: "high",
                    Timestamp: "08:42 UTC",
                    Expiry: "Expires in 8m30s",
                    InitiativeSlot: "Pass 2",
                    Target: "Hostile decker",
                    PrimaryActionId: "trace-lock",
                    PrimaryActionLabel: "Lock trace lane",
                    Alerts:
                    [
                        "Condition monitor risk",
                        "NPC response ready"
                    ],
                    MinimumAutonomy: "Low",
                    ContextSnapshot: "Pass 2",
                    InvalidatesOnContextShift: true,
                    RefreshActionId: "refresh-trace",
                    RefreshActionLabel: "Refresh trace lane")
            ]));

        StringAssert.Contains(cut.Markup, "Session context shifted.");
        StringAssert.Contains(cut.Markup, "stale in Scene break");
        StringAssert.Contains(cut.Markup, "Invalidated by context shift.");
        StringAssert.Contains(cut.Markup, "Generated for Pass 2");
        StringAssert.Contains(cut.Markup, "Refresh trace lane");
        StringAssert.Contains(cut.Markup, "chummer-badge-stale");
        Assert.IsTrue(cut.Find("[data-gm-board-primary-action='spider-1']").HasAttribute("disabled"));
    }

    [TestMethod]
    public void GmBoardFeed_invokes_quick_and_tactical_card_actions()
    {
        GmBoardFeed.GmBoardQuickActionRequest? quickAction = null;
        string? pinnedCardId = null;
        string? dismissedCardId = null;
        string? snoozedCardId = null;
        int? mutedMinutes = null;
        string? autonomyLevel = null;

        using var context = new BunitContext();
        IRenderedComponent<GmBoardFeed> cut = context.Render<GmBoardFeed>(parameters => parameters
            .Add(component => component.InterruptionBudget, 40)
            .Add(component => component.CurrentSessionContext, "Pass 2")
            .Add(component => component.SessionContexts, ["Pass 1", "Pass 2", "Scene break"])
            .Add(component => component.AutonomyLevel, "Low")
            .Add(component => component.Cards,
            [
                new GmBoardFeed.GmBoardCard(
                    Id: "ops-1",
                    Source: "GM Board",
                    Kind: "Reminder",
                    Title: "Resource drift",
                    Summary: "Apply strain before the next combat exchange.",
                    Severity: "medium",
                    Timestamp: "08:37 UTC",
                    Expiry: "Expires in 14m",
                    InitiativeSlot: "Between scenes",
                    Target: "Crew resources",
                    PrimaryActionId: "apply-strain",
                    PrimaryActionLabel: "Apply strain",
                    Alerts:
                    [
                        "Ammo check queued"
                    ],
                    MinimumAutonomy: "Tactical",
                    ContextSnapshot: "Pass 2")
            ])
            .Add(component => component.QuickActionRequested,
                (Action<GmBoardFeed.GmBoardQuickActionRequest>)(request => quickAction = request))
            .Add(component => component.PinRequested, (Action<string>)(cardId => pinnedCardId = cardId))
            .Add(component => component.DismissRequested, (Action<string>)(cardId => dismissedCardId = cardId))
            .Add(component => component.SnoozeRequested, (Action<string>)(cardId => snoozedCardId = cardId))
            .Add(component => component.MuteRequested, (Action<int>)(minutes => mutedMinutes = minutes))
            .Add(component => component.SessionContextChanged, (Action<string>)(_ => { }))
            .Add(component => component.AutonomyLevelChanged, (Action<string>)(level => autonomyLevel = level)));

        cut.Find("[data-gm-board-autonomy='High']").Click();
        cut.Find("[data-gm-board-primary-action='ops-1']").Click();
        cut.Find("[data-gm-board-pin='ops-1']").Click();
        cut.Find("[data-gm-board-dismiss='ops-1']").Click();
        cut.Find("[data-gm-board-snooze='ops-1']").Click();
        cut.Find("[data-gm-board-mute='15']").Click();

        Assert.AreEqual("High", autonomyLevel);
        Assert.IsNotNull(quickAction);
        Assert.AreEqual("ops-1", quickAction.CardId);
        Assert.AreEqual("apply-strain", quickAction.ActionId);
        Assert.AreEqual("ops-1", pinnedCardId);
        Assert.AreEqual("ops-1", dismissedCardId);
        Assert.AreEqual("ops-1", snoozedCardId);
        Assert.AreEqual(15, mutedMinutes);
    }

    [TestMethod]
    public void BlazorHome_updates_gm_ops_surface_for_autonomy_pin_and_snooze_controls()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        Assert.IsFalse(cut.Markup.Contains("Narrative reveal window", StringComparison.Ordinal));

        cut.Find("[data-gm-board-autonomy='Narrative']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Narrative reveal window"));

        cut.Find("[data-gm-board-pin='spider-003']").Click();
        cut.Find("[data-gm-board-autonomy='Off']").Click();
        cut.WaitForAssertion(() =>
        {
        StringAssert.Contains(cut.Markup, "Narrative reveal window");
        Assert.AreEqual("status", cut.Find("[data-gm-board-stale-banner]").GetAttribute("role"));
        Assert.AreEqual("polite", cut.Find("[data-gm-board-stale-banner]").GetAttribute("aria-live"));
            StringAssert.Contains(cut.Markup, "Pinned");
        });

        cut.Find("[data-gm-board-snooze='spider-003']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.IsTrue(cut.Find("[data-gm-board-primary-action='spider-003']").HasAttribute("disabled"));
            StringAssert.Contains(cut.Markup, "Snoozed until next context refresh");
        });

        cut.Find("[data-gm-board-mute='15']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Spider muted for 15 min"));
    }

    [TestMethod]
    public void BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        cut.Find("[data-gm-board-context='Scene break']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Session context shifted.");
            StringAssert.Contains(cut.Markup, "stale in Scene break");
            Assert.AreEqual("status", cut.Find("[data-gm-board-stale-banner]").GetAttribute("role"));
            Assert.AreEqual("polite", cut.Find("[data-gm-board-stale-banner]").GetAttribute("aria-live"));
            Assert.IsTrue(cut.Find("[data-gm-board-primary-action='spider-001']").HasAttribute("disabled"));
        });

        cut.Find("[data-gm-board-refresh-context='spider-001']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.IsFalse(cut.Find("[data-gm-board-primary-action='spider-001']").HasAttribute("disabled"));
            StringAssert.Contains(cut.Markup, "Refreshed for Scene break.");
            Assert.IsFalse(cut.Markup.Contains("stale in Scene break", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions()
    {
        GeneratedAssetActionRequest? lastRequest = null;
        string? selectedAssetId = null;

        using var context = new BunitContext();
        IRenderedComponent<GeneratedAssetReviewPanel> cut = context.Render<GeneratedAssetReviewPanel>(parameters => parameters
            .Add(component => component.Assets,
            [
                new GeneratedAssetProjection(
                    AssetId: "asset-1",
                    Title: "Portrait candidate",
                    AssetKind: "Portrait",
                    Source: "Portrait Forge",
                    Summary: "Portrait summary",
                    PreviewKind: GeneratedAssetPreviewKinds.Image,
                    ReviewState: "pending",
                    CreatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 0, 0, TimeSpan.Zero),
                    PreviewUri: "/media/portrait-1.png",
                    PreviewBody: "Portrait body",
                    Metadata:
                    [
                        new GeneratedAssetMetadataField("portraitPromptSeed", "Prompt Seed", "cyberpunk noir portrait"),
                        new GeneratedAssetMetadataField("portraitStyleSelected", "Style", "Noir Ink"),
                        new GeneratedAssetMetadataField("portraitStyleOptions", "Style Options", "Noir Ink,Neon Street")
                    ],
                    ComparisonSlots:
                    [
                        new GeneratedAssetComparisonSlot(
                            "portrait-baseline",
                            "Approved portrait",
                            GeneratedAssetComparisonRoles.Baseline,
                            "Existing reveal portrait.",
                            "/media/portrait-approved.png"),
                        new GeneratedAssetComparisonSlot(
                            "portrait-candidate",
                            "Candidate portrait",
                            GeneratedAssetComparisonRoles.Candidate,
                            "Sharper tablet framing.",
                            "/media/portrait-1.png")
                    ],
                    PreviewSections:
                    [
                        new GeneratedAssetPreviewSection(
                            "portrait-reroll-01",
                            "Reroll #1",
                            "Background too busy",
                            "Adjusted style for cleaner silhouette.")
                    ],
                    AttachmentTargets:
                    [
                        new GeneratedAssetAttachmentTarget("player-reveal", "Player reveal shelf", "reveal")
                    ],
                    Actions:
                    [
                        new GeneratedAssetActionDescriptor("mark-canonical-1", "Mark Canonical", "mark_canonical", true),
                        new GeneratedAssetActionDescriptor("approve-1", "Approve", GeneratedAssetActionKinds.Approve, true),
                        new GeneratedAssetActionDescriptor("archive-1", "Archive", GeneratedAssetActionKinds.Archive, true)
                    ]),
                new GeneratedAssetProjection(
                    AssetId: "asset-2",
                    Title: "Dossier packet",
                    AssetKind: "Dossier",
                    Source: "Johnson's Briefcase",
                    Summary: "Dossier summary",
                    PreviewKind: GeneratedAssetPreviewKinds.Document,
                    ReviewState: "pending",
                    CreatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 5, 0, TimeSpan.Zero),
                    PreviewBody: "Two-page document preview",
                    PreviewSections:
                    [
                        new GeneratedAssetPreviewSection(
                            "exec",
                            "Executive Summary",
                            "Prep-facing summary",
                            "The meet is clean but the pickup lane is not."),
                        new GeneratedAssetPreviewSection(
                            "threats",
                            "Threat Markers",
                            "Keep visible during ops",
                            "Patrol uptick and one hot contact.")
                    ]),
                new GeneratedAssetProjection(
                    AssetId: "asset-3",
                    Title: "Route recap clip",
                    AssetKind: "Route video",
                    Source: "Route Cinema",
                    Summary: "Video recap summary",
                    PreviewKind: GeneratedAssetPreviewKinds.Video,
                    ReviewState: "pending",
                    CreatedAtUtc: new DateTimeOffset(2026, 03, 09, 1, 10, 0, TimeSpan.Zero),
                    PreviewUri: "/media/route-recap.mp4",
                    PreviewBody: "Narrated route clip with scene-safe beats.",
                    Metadata:
                    [
                        new GeneratedAssetMetadataField("coachRouteType", "Coach Route", "coach"),
                        new GeneratedAssetMetadataField("coachRouteClass", "Route Class", "bounded_fix"),
                        new GeneratedAssetMetadataField("coachOperator", "Operator", "shadowfeed-dispatch"),
                        new GeneratedAssetMetadataField("coachModel", "Model", "gpt-5.3-codex"),
                        new GeneratedAssetMetadataField("shadowfeedDispatchChannel", "Dispatch Channel", "shadowfeed.ops"),
                        new GeneratedAssetMetadataField("shadowfeedDispatchReceipt", "Dispatch Receipt", "pending"),
                        new GeneratedAssetMetadataField("shadowfeedReviewQueue", "Review Queue", "shadowfeed.review"),
                        new GeneratedAssetMetadataField("shadowfeedReviewer", "Reviewer", "unassigned")
                    ],
                    PreviewSections:
                    [
                        new GeneratedAssetPreviewSection(
                            "recap-card",
                            "Recap Card",
                            "Player-facing recap",
                            "Clean summary for the next reveal beat."),
                        new GeneratedAssetPreviewSection(
                            "news-card",
                            "Sixth World News Card",
                            "GM-facing aftermath card",
                            "Turns the route beat into a table-feed headline.")
                    ],
                    AttachmentTargets:
                    [
                        new GeneratedAssetAttachmentTarget("recap-feed", "Recap feed", "recap"),
                        new GeneratedAssetAttachmentTarget("news-card", "News card", "news")
                    ],
                    Actions:
                    [
                        new GeneratedAssetActionDescriptor("dispatch-3", "Dispatch", "dispatch", true),
                        new GeneratedAssetActionDescriptor("review-3", "Queue Review", "review", true),
                        new GeneratedAssetActionDescriptor("approve-3", "Approve", GeneratedAssetActionKinds.Approve, true)
                    ])
            ])
            .Add(component => component.SelectedAssetChanged, (Action<string>)(assetId => selectedAssetId = assetId))
            .Add(component => component.ActionRequested, (Action<GeneratedAssetActionRequest>)(request => lastRequest = request)));

        StringAssert.Contains(cut.Markup, "Portrait candidate");
        Assert.AreEqual("tablist", cut.Find("[role='tablist']").GetAttribute("role"));
        Assert.AreEqual("tab", cut.Find("[data-generated-asset-tab='asset-1']").GetAttribute("role"));
        Assert.AreEqual("generated-asset-panel-asset-1", cut.Find("[data-generated-asset-tab='asset-1']").GetAttribute("aria-controls"));
        Assert.AreEqual("generated-asset-tab-asset-1", cut.Find("[role='tabpanel']").GetAttribute("aria-labelledby"));
        Assert.HasCount(3, cut.FindAll("[role='tabpanel']"));
        Assert.IsNull(cut.Find("#generated-asset-panel-asset-1").GetAttribute("hidden"));
        Assert.AreEqual(string.Empty, cut.Find("#generated-asset-panel-asset-2").GetAttribute("hidden"));
        Assert.AreEqual(string.Empty, cut.Find("#generated-asset-panel-asset-3").GetAttribute("hidden"));
        Assert.HasCount(2, cut.FindAll("[data-generated-asset-compare-slot]"));
        StringAssert.Contains(cut.Markup, "Candidate portrait");
        Assert.IsNotNull(cut.Find("[data-generated-portrait-forge]"));
        Assert.IsNotNull(cut.Find("[data-generated-portrait-forge-seed]"));
        Assert.IsNotNull(cut.Find("[data-generated-portrait-forge-style-options]"));
        Assert.HasCount(1, cut.FindAll("[data-generated-portrait-forge-reroll]"));
        Assert.AreEqual("tablist", cut.Find("[role='tablist']").GetAttribute("role"));
        Assert.AreEqual("tab", cut.Find("[data-generated-asset-tab='asset-1']").GetAttribute("role"));
        Assert.AreEqual("true", cut.Find("[data-generated-asset-tab='asset-1']").GetAttribute("aria-selected"));

        cut.Find("[data-generated-asset-tab='asset-2']").Click();
        Assert.AreEqual("asset-2", selectedAssetId);
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Dossier packet");
            StringAssert.Contains(cut.Markup, "Document preview");
            Assert.HasCount(2, cut.FindAll("[data-generated-asset-preview-section]"));
            Assert.AreEqual("tabpanel", cut.Find("[role='tabpanel']").GetAttribute("role"));
            Assert.IsNull(cut.Find("#generated-asset-panel-asset-2").GetAttribute("hidden"));
            Assert.AreEqual(string.Empty, cut.Find("#generated-asset-panel-asset-1").GetAttribute("hidden"));
        });

        cut.Find("[data-generated-asset-tab='asset-1']").Click();
        cut.Find("[data-generated-asset-attach='player-reveal']").Click();
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual(GeneratedAssetActionKinds.Attach, lastRequest.ActionKind);
        Assert.AreEqual("player-reveal", lastRequest.TargetId);

        cut.Find("[data-generated-asset-tab='asset-3']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Route-video viewer");
            Assert.HasCount(2, cut.FindAll("[data-generated-asset-video-card]"));
            StringAssert.Contains(cut.Markup, "Sixth World News Card");
        });
        cut.Find("[data-generated-asset-attach='news-card']").Click();
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual(GeneratedAssetActionKinds.Attach, lastRequest.ActionKind);
        Assert.AreEqual("news-card", lastRequest.TargetId);

        Assert.IsNotNull(cut.Find("[data-generated-asset-coach-routing]"));
        Assert.IsNotNull(cut.Find("[data-generated-asset-shadowfeed-rail]"));
        cut.Find("[data-generated-asset-action='dispatch']").Click();
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual("dispatch", lastRequest.ActionKind);

        cut.Find("[data-generated-asset-action='approve']").Click();
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual("approve-3", lastRequest.ActionId);

        cut.Find("[data-generated-asset-tab='asset-1']").Click();
        cut.Find("[data-generated-asset-action='archive']").Click();
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual("archive-1", lastRequest.ActionId);
    }

    [TestMethod]
    public void CampaignJournalPanel_renders_explicit_downtime_planner_calendar_and_schedule_views()
    {
        JournalPanelProjection projection = new(
            ScopeKind: JournalScopeKinds.Campaign,
            ScopeId: "campaign.downtown-burn",
            Sections:
            [
                new JournalPanelSection(JournalPanelSurfaceIds.NotesPanel, JournalPanelSectionKinds.Notes, "Notes", 1),
                new JournalPanelSection(JournalPanelSurfaceIds.LedgerPanel, JournalPanelSectionKinds.Ledger, "Ledger", 1),
                new JournalPanelSection(JournalPanelSurfaceIds.TimelinePanel, JournalPanelSectionKinds.Timeline, "Timeline", 3)
            ],
            Notes:
            [
                new NoteListItem("note-1", "Safehouse notes", JournalScopeKinds.Campaign, 2, new DateTimeOffset(2026, 03, 10, 1, 0, 0, TimeSpan.Zero))
            ],
            LedgerEntries:
            [
                new LedgerEntryView("ledger-1", LedgerEntryKinds.Expense, "Clinic deposit", 500m, "nuyen", new DateTimeOffset(2026, 03, 09, 22, 0, 0, TimeSpan.Zero))
            ],
            TimelineEvents:
            [
                new TimelineEventView(
                    EventId: "timeline-1",
                    Kind: TimelineEventKinds.Downtime,
                    Title: "Street doc follow-up",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 10, 10, 0, 0, TimeSpan.Zero),
                    EndsAtUtc: new DateTimeOffset(2026, 03, 10, 12, 0, 0, TimeSpan.Zero)),
                new TimelineEventView(
                    EventId: "timeline-2",
                    Kind: TimelineEventKinds.Training,
                    Title: "Rigger drills",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 11, 8, 0, 0, TimeSpan.Zero),
                    EndsAtUtc: new DateTimeOffset(2026, 03, 11, 10, 0, 0, TimeSpan.Zero)),
                new TimelineEventView(
                    EventId: "timeline-3",
                    Kind: "healing",
                    Title: "Recovery cycle",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 11, 12, 0, 0, TimeSpan.Zero))
            ]);

        using var context = new BunitContext();
        IRenderedComponent<CampaignJournalPanel> cut = context.Render<CampaignJournalPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        Assert.IsNotNull(cut.Find("[data-journal-downtime-planner]"));
        Assert.IsNotNull(cut.Find("[data-journal-calendar-view]"));
        Assert.IsNotNull(cut.Find("[data-journal-schedule-view]"));
        Assert.IsNotNull(cut.Find("[data-journal-downtime-lane='downtime']"));
        Assert.IsNotNull(cut.Find("[data-journal-downtime-lane='training']"));
        Assert.IsNotNull(cut.Find("[data-journal-downtime-lane='recovery']"));
        Assert.HasCount(2, cut.FindAll("[data-journal-calendar-day]"));
        Assert.HasCount(3, cut.FindAll("[data-journal-schedule-item]"));
    }

    [TestMethod]
    public void BlazorHome_renders_explicit_downtime_planner_calendar_and_schedule_views()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        Assert.IsNotNull(cut.Find("[data-journal-downtime-planner]"));
        Assert.IsNotNull(cut.Find("[data-journal-calendar-view]"));
        Assert.IsNotNull(cut.Find("[data-journal-schedule-view]"));
        StringAssert.Contains(cut.Markup, "Downtime Planner");
        StringAssert.Contains(cut.Markup, "Calendar View");
        StringAssert.Contains(cut.Markup, "Schedule View");
    }

    [TestMethod]
    public void RuntimeInspectorPanel_renders_rule_profile_and_rulepack_diagnostics_surfaces()
    {
        using var context = new BunitContext();
        IRenderedComponent<RuntimeInspectorPanel> cut = context.Render<RuntimeInspectorPanel>(parameters => parameters
            .Add(component => component.Projection, new RuntimeInspectorProjection(
                TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
                TargetId: "official.sr5.core",
                RuntimeLock: new ResolvedRuntimeLock(
                    RulesetId: RulesetDefaults.Sr5,
                    ContentBundles:
                    [
                        new ContentBundleDescriptor("sr5.core.bundle", RulesetDefaults.Sr5, "1.0.0", "SR5 Core", "Core bundle", ["data/core.xml"])
                    ],
                    RulePacks:
                    [
                        new ArtifactVersionReference("official.sr5.core", "1.0.0"),
                        new ArtifactVersionReference("house.magic", "2.1.0")
                    ],
                    ProviderBindings: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [RulePackCapabilityIds.DeriveStat] = "official.sr5.core/derive.stat",
                        [RulePackCapabilityIds.SessionQuickActions] = "house.magic/session.quick-actions"
                    },
                    EngineApiVersion: "1.0.0",
                    RuntimeFingerprint: "sha256:sr5-runtime-fingerprint"),
                Install: new ArtifactInstallState(
                    ArtifactInstallStates.Pinned,
                    InstalledTargetKind: RuntimeInspectorTargetKinds.Workspace,
                    InstalledTargetId: "workspace-1",
                    RuntimeFingerprint: "sha256:sr5-runtime-fingerprint"),
                ResolvedRulePacks:
                [
                    new RuntimeInspectorRulePackEntry(
                        new ArtifactVersionReference("official.sr5.core", "1.0.0"),
                        "SR5 Core",
                        ArtifactVisibilityModes.Shared,
                        ArtifactTrustTiers.Official,
                        [RulePackCapabilityIds.DeriveStat],
                        SourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile),
                    new RuntimeInspectorRulePackEntry(
                        new ArtifactVersionReference("house.magic", "2.1.0"),
                        "House Magic",
                        ArtifactVisibilityModes.LocalOnly,
                        ArtifactTrustTiers.Private,
                        [RulePackCapabilityIds.SessionQuickActions],
                        SourceKind: RegistryEntrySourceKinds.OverlayDerivedProfile)
                ],
                ProviderBindings:
                [
                    new RuntimeInspectorProviderBinding(RulePackCapabilityIds.DeriveStat, "official.sr5.core/derive.stat", "official.sr5.core", SessionSafe: false),
                    new RuntimeInspectorProviderBinding(RulePackCapabilityIds.SessionQuickActions, "house.magic/session.quick-actions", "house.magic", SessionSafe: true)
                ],
                CompatibilityDiagnostics:
                [
                    new RuntimeLockCompatibilityDiagnostic(RuntimeLockCompatibilityStates.RebindRequired, "Session action provider needs a refresh.", RulesetDefaults.Sr5, "sha256:next")
                ],
                Warnings:
                [
                    new RuntimeInspectorWarning(RuntimeInspectorWarningKinds.Trust, RuntimeInspectorWarningSeverityLevels.Warning, "House Magic is private-only.", "house.magic"),
                    new RuntimeInspectorWarning(RuntimeInspectorWarningKinds.ProviderBinding, RuntimeInspectorWarningSeverityLevels.Info, "Core derive.stat binding is current.", RulePackCapabilityIds.DeriveStat)
                ],
                MigrationPreview:
                [
                    new RuntimeMigrationPreviewItem(RuntimeMigrationPreviewChangeKinds.ProviderRebound, "Quick actions will move to v2.", RulePackCapabilityIds.SessionQuickActions, "provider.v1", "provider.v2", RequiresRebind: true)
                ],
                GeneratedAtUtc: new DateTimeOffset(2026, 03, 09, 8, 0, 0, TimeSpan.Zero),
                ProfileSourceKind: RegistryEntrySourceKinds.OverlayDerivedProfile,
                Promotion: new RuntimeInspectorPromotionProjection(
                    PublicationStatus: RuleProfilePublicationStatuses.Published,
                    Visibility: ArtifactVisibilityModes.CampaignShared,
                    UpdateChannel: RuleProfileUpdateChannels.CampaignPinned,
                    PromotionSummary: "Campaign-pinned rule environment is published with campaign-shared visibility and stays on the campaign-approved rail until broader promotion is chosen.",
                    RollbackSummary: "Rollback can re-pin sha256:sr5-runtime-fingerprint on workspace:workspace-1 while the next promotion is reviewed.",
                    LineageSummary: "Overlay-derived profile compiles on top of the governed runtime lock instead of forking a local shadow rule environment.",
                    PublishedAtUtc: new DateTimeOffset(2026, 03, 08, 7, 0, 0, TimeSpan.Zero),
                    CurrentStage: RuntimeInspectorPromotionStages.CampaignApproved,
                    PromotionTargetStage: RuntimeInspectorPromotionStages.Published),
                CapabilityDescriptors:
                [
                    new RuntimeInspectorCapabilityDescriptorProjection(
                        RulePackCapabilityIds.DeriveStat,
                        RulesetCapabilityInvocationKinds.Rule,
                        "Derived Stat",
                        Explainable: true,
                        SessionSafe: false,
                        DefaultGasBudget: new RulesetGasBudget(100, 200, 4 * 1024 * 1024),
                        ProviderId: "official.sr5.core/derive.stat",
                        PackId: "official.sr5.core"),
                    new RuntimeInspectorCapabilityDescriptorProjection(
                        RulePackCapabilityIds.SessionQuickActions,
                        RulesetCapabilityInvocationKinds.Rule,
                        "Quick Actions",
                        Explainable: true,
                        SessionSafe: true,
                        DefaultGasBudget: new RulesetGasBudget(100, 200, 4 * 1024 * 1024),
                        ProviderId: "house.magic/session.quick-actions",
                        PackId: "house.magic")
                ])));

        StringAssert.Contains(cut.Markup, "Rule Profile Diagnostics");
        StringAssert.Contains(cut.Markup, "Rule Pack Diagnostics");
        StringAssert.Contains(cut.Markup, "Hub Client Diagnostics");
        StringAssert.Contains(cut.Markup, "Review State");
        StringAssert.Contains(cut.Markup, "refresh pending");
        StringAssert.Contains(cut.Markup, "Session-safe Bindings");
        StringAssert.Contains(cut.Markup, "Update Channel");
        StringAssert.Contains(cut.Markup, "campaign-pinned");
        StringAssert.Contains(cut.Markup, "Current Stage");
        StringAssert.Contains(cut.Markup, "Campaign-approved");
        StringAssert.Contains(cut.Markup, "Promote To");
        StringAssert.Contains(cut.Markup, "Published");
        StringAssert.Contains(cut.Markup, "Rollback can re-pin sha256:sr5-runtime-fingerprint");
        StringAssert.Contains(cut.Markup, "Diagnostics environment diff");
        StringAssert.Contains(cut.Markup, "Rebind to sha256:next before support closure.");
        StringAssert.Contains(cut.Markup, "Quick actions will move to v2.");
        StringAssert.Contains(cut.Markup, "derive.stat via official.sr5.core/derive.stat");
        StringAssert.Contains(cut.Markup, "attention");
        Assert.IsNotNull(cut.Find("[data-runtime-hub-diagnostics]"));
        Assert.IsNotNull(cut.Find("[data-diagnostics-environment-diff]"));
        Assert.HasCount(2, cut.FindAll("[data-runtime-rulepack-row]"));
        StringAssert.Contains(cut.Find("[data-runtime-rulepack-row='house.magic']").TextContent, "1");
        StringAssert.Contains(cut.Markup, "chummer-dense-header");
        StringAssert.Contains(cut.Markup, "chummer-dense-row");
    }

    [TestMethod]
    public void ContactNetworkPanel_renders_relationship_graph_rails()
    {
        CharacterContactsSection contacts = new(
            Count: 3,
            Contacts:
            [
                new CharacterContactSummary("Paz Ortega", "Street doc", "Redmond", 4, 5),
                new CharacterContactSummary("Mina Voss", "Fixer", "Tacoma", 6, 3),
                new CharacterContactSummary("Hexswitch", "Matrix broker", "Bellevue", 3, 2)
            ]);
        ContactRelationshipGraphState? graph = ContactRelationshipGraphProjector.FromContacts(contacts);

        using var context = new BunitContext();
        IRenderedComponent<ContactNetworkPanel> cut = context.Render<ContactNetworkPanel>(parameters => parameters
            .Add(component => component.Graph, graph));

        Assert.IsNotNull(graph);
        Assert.IsNotNull(cut.Find("[data-contact-graph-nodes]"));
        Assert.IsNotNull(cut.Find("[data-contact-faction-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-heat-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-obligation-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-favor-rail]"));
        StringAssert.Contains(cut.Markup, "Faction Status Rail");
        StringAssert.Contains(cut.Markup, "Unresolved Favor Rail");
        StringAssert.Contains(cut.Markup, "Mina Voss");
    }

    [TestMethod]
    public void BlazorHome_renders_contact_relationship_graph_rails()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        Assert.IsNotNull(cut.Find("[data-contact-graph-nodes]"));
        Assert.IsNotNull(cut.Find("[data-contact-faction-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-heat-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-obligation-rail]"));
        Assert.IsNotNull(cut.Find("[data-contact-favor-rail]"));
        StringAssert.Contains(cut.Markup, "Contact Network");
        StringAssert.Contains(cut.Markup, "Unresolved Favor Rail");
    }

    [TestMethod]
    public void NpcPersonaStudioPanel_renders_selection_evidence_and_draft_vs_approved_rails()
    {
        NpcPersonaStudioState projection = new(
            DefaultPersonaId: "decker-contact",
            SelectedPersonaId: "decker-contact",
            PromptPolicy: "decker-contact evidence-first",
            Personas:
            [
                new NpcPersonaDescriptorState(
                    PersonaId: "decker-contact",
                    Label: "Decker Contact",
                    EvidenceFirst: true,
                    Summary: "Grounded persona for NPC guidance.",
                    Provenance: "persona.registry/decker-contact",
                    ApprovalState: "approved",
                    IsSelected: true),
                new NpcPersonaDescriptorState(
                    PersonaId: "street-fixer",
                    Label: "Street Fixer",
                    EvidenceFirst: true,
                    Summary: "Fallback routing persona.",
                    Provenance: "persona.registry/street-fixer",
                    ApprovalState: "draft",
                    IsSelected: false)
            ],
            Policies:
            [
                new NpcPersonaRoutePolicyState(
                    RouteType: "coach",
                    RouteClassId: "grounded_rules_chat",
                    PersonaId: "decker-contact",
                    PrimaryProviderId: "aimagicx",
                    ToolingEnabled: true,
                    ApprovalState: "approved",
                    AllowedToolIds: ["create_apply_preview"])
            ],
            EvidenceLines:
            [
                "Prompt policy: decker-contact evidence-first",
                "Persona provenance: persona.registry/decker-contact"
            ],
            HasDraftPolicies: true,
            HasApprovedPolicies: true);

        using var context = new BunitContext();
        IRenderedComponent<NpcPersonaStudioPanel> cut = context.Render<NpcPersonaStudioPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        Assert.IsNotNull(cut.Find("[data-npc-persona-selection]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-provenance]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-policy]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-approval]"));
        StringAssert.Contains(cut.Markup, "Decker Contact");
        StringAssert.Contains(cut.Markup, "Draft vs Approved");
        StringAssert.Contains(cut.Markup, "chummer-chip-approved");
        StringAssert.Contains(cut.Markup, "chummer-dense-row");
    }

    [TestMethod]
    public void BlazorHome_renders_npc_persona_studio_rails()
    {
        using var context = new BunitContext();
        IRenderedComponent<Home> cut = context.Render<Home>();

        Assert.IsNotNull(cut.Find("[data-npc-persona-selection]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-provenance]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-policy]"));
        Assert.IsNotNull(cut.Find("[data-npc-persona-approval]"));
        StringAssert.Contains(cut.Markup, "NPC Persona Studio");
        StringAssert.Contains(cut.Markup, "Draft vs Approved");
    }

    [TestMethod]
    public void BlazorHome_updates_generated_asset_workflow_for_attach_approve_and_archive()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        Assert.HasCount(2, cut.FindAll("[data-generated-asset-compare-slot]"));

        cut.Find("[data-generated-asset-tab='asset-dossier-01']").Click();
        cut.WaitForAssertion(() => Assert.HasCount(3, cut.FindAll("[data-generated-asset-preview-section]")));
        cut.Find("[data-generated-asset-attach='gm-prep-board']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "attached:gm-prep-board"));

        cut.Find("[data-generated-asset-action='approve']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "approved");
            StringAssert.Contains(cut.Markup, "canonical");
        });

        cut.Find("[data-generated-asset-tab='asset-news-01']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Route-video viewer");
            Assert.HasCount(2, cut.FindAll("[data-generated-asset-video-card]"));
            StringAssert.Contains(cut.Markup, "Sixth World News Card");
        });
        cut.Find("[data-generated-asset-attach='news-card']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "attached:news-card"));
        cut.Find("[data-generated-asset-action='archive']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "archived"));
    }

    [TestMethod]
    public void BlazorHome_invalidates_shadowfeed_dispatch_after_context_shift_and_allows_refresh()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        cut.Find("[data-generated-asset-tab='asset-news-01']").Click();
        cut.Find("[data-generated-asset-action='dispatch']").Click();
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "dispatched"));

        cut.Find("[data-gm-board-context='Scene break']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "stale:Scene break");
            Assert.IsNotNull(cut.Find("[data-generated-asset-stale-banner]"));
        });

        cut.Find("[data-generated-asset-action='refresh_dispatch']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "dispatched");
            StringAssert.Contains(cut.Markup, "dispatch:refreshed");
        });
    }

    [TestMethod]
    public void BlazorHome_marks_portrait_candidate_as_canonical_through_shared_action_rail()
    {
        using var context = new BunitContext();
        IRenderedComponent<Showcase> cut = context.Render<Showcase>();

        cut.Find("[data-generated-asset-tab='asset-portraits-01']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(cut.Find("[data-generated-portrait-forge]"));
            Assert.IsNotNull(cut.Find("[data-generated-portrait-forge-reroll-timeline]"));
            StringAssert.Contains(cut.Markup, "candidate");
        });

        cut.Find("[data-generated-asset-action='mark_canonical']").Click();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "canonical:selected");
            StringAssert.Contains(cut.Markup, "canonical");
            StringAssert.Contains(cut.Markup, "asset-portraits-01");
        });
    }

    [TestMethod]
    public void DialogHost_renders_dialog_and_emits_events()
    {
        DesktopDialogState dialog = new(
            Id: "save-dialog",
            Title: "Save Character",
            Message: "Confirm save.",
            Fields:
            [
                new DesktopDialogField("name", "Name", "Old Name", "enter name"),
                new DesktopDialogField("houseRules", "House Rules", "false", string.Empty, false, false, "checkbox"),
                new DesktopDialogField("notes", "Notes", "Old", "enter notes", true, false, "text"),
                new DesktopDialogField("token", "Token", "abc", "readonly token", false, true, "text")
            ],
            Actions:
            [
                new DesktopDialogAction("cancel", "Cancel"),
                new DesktopDialogAction("save", "Save", true)
            ]);

        List<DialogFieldInputChange> inputChanges = [];
        List<DialogFieldCheckboxChange> checkboxChanges = [];
        string? executedActionId = null;
        int closeCount = 0;

        using var context = new BunitContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, dialog)
            .Add(component => component.CloseRequested, (Action)(() => closeCount++))
            .Add(component => component.ExecuteDialogActionRequested, (Action<string>)(actionId => executedActionId = actionId))
            .Add(component => component.FieldInputRequested, (Action<DialogFieldInputChange>)(change => inputChanges.Add(change)))
            .Add(component => component.FieldCheckboxRequested,
                (Action<DialogFieldCheckboxChange>)(change => checkboxChanges.Add(change))));

        Assert.AreEqual("Save Character", cut.Find("#dialogTitle").TextContent.Trim());
        Assert.AreEqual("dialog", cut.Find(".desktop-dialog").GetAttribute("role"));
        Assert.AreEqual("true", cut.Find(".desktop-dialog").GetAttribute("aria-modal"));
        StringAssert.Contains(cut.Find(".desktop-dialog").ClassName, "classic-dialog");
        StringAssert.Contains(cut.Find(".dialog-titlebar").ClassName, "classic-dialog-titlebar");
        StringAssert.Contains(cut.Find(".dialog-body").ClassName, "classic-dialog-grid");
        StringAssert.StartsWith(cut.Find(".desktop-dialog").GetAttribute("aria-describedby"), "dialog-description-save-dialog", StringComparison.Ordinal);
        IElement nameInput = cut.Find("input[placeholder='enter name']");
        IElement notesInput = cut.Find("textarea[placeholder='enter notes']");
        IElement readonlyToken = cut.Find("input[placeholder='readonly token']");
        IElement checkbox = cut.Find("input[type='checkbox']");
        IElement saveButton = cut.Find("#dialogFooter .action-btn.primary");
        IElement closeButton = cut.Find("#dialogClose");

        Assert.IsTrue(readonlyToken.HasAttribute("readonly"));
        Assert.AreEqual("Name: enter name", nameInput.GetAttribute("title"));
        Assert.AreEqual("Name", nameInput.GetAttribute("aria-label"));
        StringAssert.Contains(nameInput.GetAttribute("aria-description"), "Editable text field");
        Assert.AreEqual("Notes: enter notes", notesInput.GetAttribute("title"));
        Assert.AreEqual("Notes", notesInput.GetAttribute("aria-label"));
        StringAssert.Contains(notesInput.GetAttribute("aria-description"), "Editable multi-line text field");
        Assert.AreEqual("House Rules", checkbox.GetAttribute("title"));
        Assert.AreEqual("House Rules", checkbox.GetAttribute("aria-label"));
        StringAssert.Contains(checkbox.GetAttribute("aria-description"), "Editable checkbox");
        Assert.AreEqual("Save", saveButton.GetAttribute("title"));
        Assert.AreEqual("Save", saveButton.GetAttribute("aria-label"));
        StringAssert.Contains(saveButton.ClassName, "classic-dialog-action");
        StringAssert.Contains(saveButton.GetAttribute("aria-description"), "Primary dialog action");
        Assert.AreEqual("Close dialog", closeButton.GetAttribute("title"));
        Assert.AreEqual("Close dialog", closeButton.GetAttribute("aria-label"));

        cut.Find("input[placeholder='enter name']").Input("Neo");
        cut.Find("textarea[placeholder='enter notes']").Input("Updated notes");
        cut.Find("input[type='checkbox']").Change(true);
        cut.Find("#dialogFooter .action-btn.primary").Click();
        cut.Find("#dialogClose").Click();

        string[] expectedInputFieldIds = ["name", "notes"];
        CollectionAssert.AreEquivalent(
            expectedInputFieldIds,
            inputChanges.Select(change => change.FieldId).ToArray());
        Assert.AreEqual("houseRules", checkboxChanges[0].FieldId);
        Assert.IsTrue(checkboxChanges[0].Value);
        Assert.AreEqual("save", executedActionId);
        Assert.AreEqual(1, closeCount);
    }

    [TestMethod]
    public void DialogHost_renders_nothing_without_dialog_state()
    {
        using var context = new BunitContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, (DesktopDialogState?)null));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    [TestMethod]
    public void DialogHost_renders_image_preview_for_image_visual()
    {
        string portraitPath = Path.Combine(Path.GetTempPath(), $"dialog-host-portrait-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            portraitPath,
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+iV2QAAAAASUVORK5CYII="));

        try
        {
            DesktopDialogState dialog = new(
                Id: "roster-dialog",
                Title: "Character Roster",
                Message: "Confirm roster state.",
                Fields:
                [
                    new DesktopDialogField(
                        "rosterMugshot",
                        "Mugshot",
                        $"Runner Portrait{Environment.NewLine}Portrait Source | {portraitPath}{Environment.NewLine}Portrait Match | watched runner sibling",
                        "Runner Portrait",
                        IsMultiline: true,
                        IsReadOnly: true,
                        VisualKind: DesktopDialogFieldVisualKinds.Image)
                ],
                Actions:
                [
                    new DesktopDialogAction("close", "Close", true)
                ]);

            using var context = new BunitContext();
            IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
                .Add(component => component.Dialog, dialog));

            IElement preview = cut.Find(".dialog-image-preview");
            StringAssert.StartsWith(preview.GetAttribute("src"), "data:image/png;base64,", StringComparison.Ordinal);
            Assert.AreEqual("Runner Portrait", preview.GetAttribute("alt"));
        }
        finally
        {
            if (File.Exists(portraitPath))
            {
                File.Delete(portraitPath);
            }
        }
    }

    [TestMethod]
    public void StatusStrip_announces_status_via_shared_live_region_semantics()
    {
        using var context = new BunitContext();
        IRenderedComponent<StatusStrip> cut = context.Render<StatusStrip>(parameters => parameters
            .Add(component => component.LastUiUtc, "2026-03-10 12:00:00Z")
            .Add(component => component.Error, "offline")
            .Add(component => component.ComplianceState, "Ruleset: sr5"));

        IElement region = cut.Find(".status-strip");
        Assert.AreEqual("status", region.GetAttribute("role"));
        Assert.AreEqual("polite", region.GetAttribute("aria-live"));
        Assert.AreEqual("true", region.GetAttribute("aria-atomic"));
        StringAssert.Contains(region.GetAttribute("aria-label"), "Service: error");
        StringAssert.Contains(region.GetAttribute("aria-label"), "Ruleset: sr5");
    }
}
