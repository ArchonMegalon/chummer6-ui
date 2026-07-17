#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.Components.Shared;
using Chummer.Blazor.Components.Shell;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Journal;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class BlazorShellComponentTests
{
    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Setup<bool>("chummerDialogs.isSameDialogRefresh", _ => true).SetResult(false);
        context.JSInterop.SetupVoid("chummerDialogs.revealActiveDialog");
        context.JSInterop.Setup<double[]>("chummerDialogs.captureDialogScroll", _ => true).SetResult([180d, 0d]);
        context.JSInterop.SetupVoid("chummerDialogs.restoreDialogScroll", _ => true);
        context.JSInterop.Setup<bool>("chummerDialogs.restorePendingDialogScroll", _ => true).SetResult(false);
        context.Services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        context.Services.AddSingleton<IRunnerIntelligenceCalculator, RunnerIntelligenceCalculator>();
        context.Services.AddSingleton<IRunnerIntelligenceScenarioCatalog, RunnerIntelligenceScenarioCatalog>();
        context.Services.AddSingleton<BlazorRunnerIntelligencePreviewService>();
        return context;
    }

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

        using var context = CreateContext();
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

        using var context = CreateContext();
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

        using var context = CreateContext();
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
        using var context = CreateContext();
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
    public void Preview_menu_links_execute_shared_shell_commands_without_query_roundtrip()
    {
        using var context = CreateContext();
        FakeCharacterOverviewPresenter presenter = RegisterPreviewShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.AreEqual("false", cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").GetAttribute("aria-expanded"));

        cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").Click();
        Assert.AreEqual("true", cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").GetAttribute("aria-expanded"));
        cut.Find("nav.classic-chummer-menu a[role='menuitem'][data-browser-shell-command='new_character']").Click();
        Assert.AreEqual("new_character", presenter.ExecutedCommandId);
        Assert.AreEqual("false", cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").GetAttribute("aria-expanded"));
        StringAssert.EndsWith(navigation.Uri, "/workbench");

        cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").Click();
        Assert.AreEqual("true", cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").GetAttribute("aria-expanded"));
        cut.Find("nav.classic-chummer-menu a[role='menuitem'][data-browser-shell-command='open_character']").Click();
        Assert.AreEqual("open_character", presenter.ExecutedCommandId);
        Assert.AreEqual("false", cut.Find("nav.classic-chummer-menu [data-classic-menu-trigger='file']").GetAttribute("aria-expanded"));
        StringAssert.EndsWith(navigation.Uri, "/workbench");
    }

    [TestMethod]
    public void MdiStrip_shows_unsaved_marker_for_workspace_without_save_receipt()
    {
        CharacterWorkspaceId ws1 = new("ws-1");
        CharacterWorkspaceId ws2 = new("ws-2");
        OpenWorkspaceState dirtyWorkspace = new(ws1, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr5, HasSavedWorkspace: false);
        OpenWorkspaceState savedWorkspace = new(ws2, "Neo Runner", "NR", DateTimeOffset.UtcNow.AddMinutes(-1), RulesetDefaults.Sr5, HasSavedWorkspace: true);

        using var context = CreateContext();
        IRenderedComponent<MdiStrip> cut = context.Render<MdiStrip>(parameters => parameters
            .Add(component => component.OpenWorkspaces, [dirtyWorkspace, savedWorkspace])
            .Add(component => component.ActiveWorkspaceId, ws1)
            .Add(component => component.IsBusy, false));

        IReadOnlyList<AngleSharp.Dom.IElement> docs = cut.FindAll(".mdi-doc");
        IReadOnlyList<AngleSharp.Dom.IElement> closeButtons = cut.FindAll(".mdi-close");
        Assert.HasCount(2, docs);
        Assert.HasCount(2, closeButtons);
        StringAssert.Contains(docs[0].TextContent, "*");
        StringAssert.Contains(docs[0].GetAttribute("title"), "Shadowrun 5");
        StringAssert.Contains(docs[0].GetAttribute("title"), "main editor");
        Assert.IsLessThan(0, docs[1].TextContent.IndexOf('*'));
        Assert.AreEqual("Close dossier", closeButtons[0].GetAttribute("title"));
        Assert.AreEqual("Close dossier", closeButtons[0].GetAttribute("aria-label"));
    }

    [TestMethod]
    public void MdiStrip_uses_ruleset_specific_empty_state_when_no_workspace_is_open()
    {
        using var context = CreateContext();
        IRenderedComponent<MdiStrip> cut = context.Render<MdiStrip>(parameters => parameters
            .Add(component => component.OpenWorkspaces, Array.Empty<OpenWorkspaceState>())
            .Add(component => component.RulesetId, RulesetDefaults.Sr6)
            .Add(component => component.IsBusy, false));

        StringAssert.Contains(cut.Markup, "No open SR6 dossier");
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

        using var context = CreateContext();
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
        Assert.AreEqual("tab-info", cut.Find("button[data-nav-tab='tab-info']").Id);
        Assert.AreEqual("Review Character", cut.Find(".section-actions .action-button").TextContent.Trim());
        Assert.AreEqual("tab-info.validate", cut.Find(".section-actions .action-button").GetAttribute("data-workspace-action"));

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

        using var context = CreateContext();
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
    public void WorkspaceLeftPane_keeps_shell_posture_when_workspace_list_remains_open_without_active_selection()
    {
        CharacterWorkspaceId workspaceId = new("ws-1");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr6);
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [openWorkspace],
                RecentWorkspaceIds: [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            ActiveTabId = "tab-create",
            IsBusy = false
        };

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
        IReadOnlyList<NavigationTabDefinition> navigationTabs =
        [
            new NavigationTabDefinition("tab-create", "Create", "build-lab", "character", true, true, RulesetDefaults.Sr5),
            new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5)
        ];

        using var context = CreateContext();
        IRenderedComponent<WorkspaceLeftPane> cut = context.Render<WorkspaceLeftPane>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.OpenWorkspaces, [openWorkspace])
            .Add(component => component.ActiveWorkspaceId, null)
            .Add(component => component.ActiveTabId, "tab-create")
            .Add(component => component.NavigationTabs, navigationTabs)
            .Add(component => component.ActiveWorkspaceActions, new[] { summaryAction })
            .Add(component => component.ActiveWorkflowSurfaceActions, new[] { summarySurface })
            .Add(component => component.IsNavigationTabEnabled, _ => true));

        Assert.IsNotNull(cut.Find("button[data-nav-tab='tab-create']"));
        Assert.IsNotNull(cut.Find("button[data-nav-tab='tab-info']"));
        StringAssert.Contains(cut.Markup, "Ares Runner (AR) · Shadowrun 6");
        StringAssert.Contains(cut.Markup, "Sixth World editor");
        StringAssert.Contains(cut.Markup, "SR5 Editor Tabs");
        Assert.AreEqual(0, cut.FindAll(".section-actions").Count, "Workspace actions should stay hidden until a dossier is actively selected.");
        Assert.AreEqual(0, cut.FindAll(".controls").Count, "Workflow chrome should stay hidden until a dossier is actively selected.");
    }

    [TestMethod]
    public void SummaryHeader_renders_ruleset_specific_heading_and_runtime_inspector_action()
    {
        bool inspectRequested = false;
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Profile = new CharacterProfileSection(
                Name: "Apex",
                Alias: "Runner",
                Metatype: "Human",
                PlayerName: string.Empty,
                Metavariant: string.Empty,
                Sex: string.Empty,
                Age: string.Empty,
                Height: string.Empty,
                Weight: string.Empty,
                Hair: string.Empty,
                Eyes: string.Empty,
                Skin: string.Empty,
                Concept: string.Empty,
                Description: string.Empty,
                Background: string.Empty,
                CreatedVersion: "1.0",
                AppVersion: "1.0",
                BuildMethod: "Priority",
                GameplayOption: "Standard",
                Created: true,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                MainMugshotIndex: 0,
                MugshotCount: 0),
            Build = new CharacterBuildSection(
                BuildMethod: "Priority",
                PriorityMetatype: "A",
                PriorityAttributes: "B",
                PrioritySpecial: "C",
                PrioritySkills: "D",
                PriorityResources: "E",
                PriorityTalent: "Mundane",
                SumToTen: 10,
                Special: 0,
                TotalSpecial: 0,
                TotalAttributes: 0,
                ContactPoints: 0,
                ContactPointsUsed: 0),
            Progress = new CharacterProgressSection(
                Karma: 7,
                Nuyen: 0,
                StartingNuyen: 0,
                StreetCred: 3,
                Notoriety: 0,
                PublicAwareness: 0,
                BurntStreetCred: 0,
                BuildKarma: 0,
                TotalAttributes: 0,
                TotalSpecial: 0,
                PhysicalCmFilled: 0,
                StunCmFilled: 0,
                TotalEssence: 6,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false)
        };
        ShellSurfaceState shellSurface = ShellSurfaceState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr5.core",
                Title: "Official SR5 Core",
                RulesetId: RulesetDefaults.Sr5,
                RuntimeFingerprint: "fingerprint-sr5",
                InstallState: ArtifactInstallStates.Installed,
                WarningCount: 0)
        };

        using var context = CreateContext();
        IRenderedComponent<SummaryHeader> cut = context.Render<SummaryHeader>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.ShellSurface, shellSurface)
            .Add(component => component.InspectRuntimeRequested, (Action)(() => inspectRequested = true)));

        StringAssert.Contains(cut.Markup, "Desktop Summary · SR5 Editor");
        Assert.AreEqual("Apex", cut.Find("#summaryName").GetAttribute("value"));
        Assert.AreEqual("Runner", cut.Find("#summaryAlias").GetAttribute("value"));
        Assert.AreEqual("Human", cut.Find("#summaryMetatype").GetAttribute("value"));
        Assert.AreEqual("Priority", cut.Find("#summaryBuildMethod").GetAttribute("value"));
        Assert.AreEqual("7", cut.Find("#summaryKarma").GetAttribute("value"));
        Assert.AreEqual("3", cut.Find("#summaryStreetCred").GetAttribute("value"));
        StringAssert.Contains(cut.Find("#summaryRuntime").GetAttribute("value"), "sr5");

        cut.Find("#summaryRuntimeInspect").Click();

        Assert.IsTrue(inspectRequested);
    }

    [TestMethod]
    public void OpenWorkspaceTree_renders_open_and_close_actions()
    {
        CharacterWorkspaceId workspaceId = new("ws-1");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Ares Runner", "AR", DateTimeOffset.UtcNow, RulesetDefaults.Sr5);
        string? openedWorkspaceId = null;
        string? closedWorkspaceId = null;

        using var context = CreateContext();
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
        string openDossierTitle = cut.Find(".navigator .command-button").GetAttribute("title") ?? string.Empty;
        StringAssert.Contains(openDossierTitle, "Open SR5 character: Ares Runner (AR)");
        StringAssert.Contains(openDossierTitle, "Shadowrun 5");
        Assert.AreEqual(0, cut.FindAll(".navigator .command-button .hint").Count, "Classic runner rows must not print workspace ids into the visible left rail.");
    }

    [TestMethod]
    public void ImportPanel_renders_ruleset_specific_copy_and_accepts_all_native_formats()
    {
        using var context = CreateContext();
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

        StringAssert.Contains(cut.Markup, "Import SR4 Runner File");
        StringAssert.Contains(cut.Markup, "Primary format: .chum4 with XML fallback.");
        StringAssert.Contains(cut.Markup, "(no SR4 runner file selected)");
        StringAssert.Contains(cut.Markup, "SR4 Runner XML Review");
        StringAssert.Contains(cut.Markup, "Runner import review");
        StringAssert.Contains(cut.Markup, "Runner import setup");
        StringAssert.Contains(cut.Markup, "Import landed with a reviewed record.");
        StringAssert.Contains(cut.Markup, "Rules setup");
        StringAssert.Contains(cut.Markup, "chummer.portable-dossier.v1; compatible-with-warnings; inspect-only; payload abcdef1234567890.");
        StringAssert.Contains(cut.Markup, "Imported Runner Blue into sr4 with a bounded source toggle change.");
        StringAssert.Contains(cut.Markup, "Before");
        StringAssert.Contains(cut.Markup, "After");
        StringAssert.Contains(cut.Markup, "Explanation");
        StringAssert.Contains(cut.Markup, "Source");
        StringAssert.Contains(cut.Markup, "Payload hash abcdef123456 entered workspace ws-import.");
        StringAssert.Contains(cut.Markup, "Support note");
        StringAssert.Contains(cut.Markup, "Review the before-after environment change before campaign next step.");
        StringAssert.Contains(cut.Markup, "Support can use payload abcdef1234567890 with compatible-with-warnings compatibility.");
        StringAssert.Contains(cut.Markup, "Street Magic source toggle changed during import.");
        Assert.IsFalse(cut.Markup.Contains("explain receipt", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cut.Markup.Contains("environment diff", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(cut.Find("[data-import-explain-receipt]"));
        Assert.AreEqual(".chum4,.chum5,.chum6,.xml,text/xml,application/xml", cut.Find("input[type='file']").GetAttribute("accept"));
        Assert.AreEqual("Import SR4 Runner XML", cut.Find("details button").TextContent.Trim());
    }

    [TestMethod]
    public void CommandPanel_and_ResultPanel_render_ruleset_specific_headings_and_fallback_copy()
    {
        CharacterOverviewState state = CharacterOverviewState.Empty;

        using var context = CreateContext();
        IRenderedComponent<CommandPanel> commandCut = context.Render<CommandPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr6)
            .Add(component => component.State, state)
            .Add(component => component.Commands, Array.Empty<AppCommandDefinition>()));
        IRenderedComponent<ResultPanel> resultCut = context.Render<ResultPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr5)
            .Add(component => component.State, state));

        StringAssert.Contains(commandCut.Markup, "SR6 Editor Commands");
        StringAssert.Contains(commandCut.Markup, "No SR6 editor commands are currently available.");
        StringAssert.Contains(resultCut.Markup, "SR5 Editor Result");
        StringAssert.Contains(resultCut.Markup, "Shadowrun 5 uses the main desktop editor");
        StringAssert.Contains(resultCut.Markup, "SR5 editor is ready");
    }

    [TestMethod]
    public void ResultPanel_renders_last_portability_activity_details()
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Notice = "Portable export ready.",
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

        using var context = CreateContext();
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
    public void ResultPanel_save_receipt_uses_dossier_copy_when_workspace_is_saved()
    {
        CharacterWorkspaceId workspaceId = new("saved-dossier");
        OpenWorkspaceState savedWorkspace = new(
            workspaceId,
            "Saved Dossier",
            "SD",
            DateTimeOffset.UtcNow,
            RulesetDefaults.Sr5,
            HasSavedWorkspace: true);
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [savedWorkspace], [workspaceId]),
            OpenWorkspaces = [savedWorkspace],
            WorkspaceId = workspaceId,
            LastCommandId = "save_character"
        };

        using var context = CreateContext();
        IRenderedComponent<ResultPanel> cut = context.Render<ResultPanel>(parameters => parameters
            .Add(component => component.RulesetId, RulesetDefaults.Sr5)
            .Add(component => component.State, state));

        IElement receipt = cut.Find("[data-result-dispatch='save']");
        StringAssert.Contains(receipt.TextContent, "Saved in this browser");
        StringAssert.Contains(receipt.TextContent, "Dossier:");
        StringAssert.Contains(receipt.TextContent, "saved-dossier");
        StringAssert.Contains(receipt.TextContent, "This dossier is saved and ready to reopen.");
        Assert.IsFalse(receipt.TextContent.Contains("Runner:", StringComparison.Ordinal));
        Assert.IsFalse(receipt.TextContent.Contains("This runner is saved and ready to reopen.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SectionPane_switches_between_placeholder_and_section_payload()
    {
        using var context = CreateContext();
        IRenderedComponent<SectionPane> emptyCut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, CharacterOverviewState.Empty));

        StringAssert.Contains(emptyCut.Markup, "Select a tab to render a dossier section");

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
    public void SectionPane_renders_sr6_attribute_workbench_and_emits_attribute_edits()
    {
        using var context = CreateContext();

        CharacterWorkspaceId workspaceId = new("ws-sr6-attribute-workbench");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Nova", "Cipher", DateTimeOffset.UtcNow, RulesetDefaults.Sr6);
        AttributeEditRequest? editRequest = null;
        CharacterOverviewState sectionState = CharacterOverviewState.Empty with
        {
            WorkspaceId = workspaceId,
            OpenWorkspaces = [openWorkspace],
            ActiveSectionId = "attributes",
            ActiveSectionJson = """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "base": 3,
      "karma": 1,
      "value": 4,
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
            ActiveSectionRows = []
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, sectionState)
            .Add(component => component.AttributeEditRequested, (Action<AttributeEditRequest>)(request => editRequest = request)));

        Assert.IsTrue(cut.Markup.Contains("data-sr6-attribute-workbench", StringComparison.Ordinal));
        Assert.AreEqual("1 SR6 attribute ready  •  Body rating 4  •  range 1 / 6 (9)", cut.Find(".sr6-attribute-workbench__summary").TextContent.Trim());
        CollectionAssert.AreEqual(
            new[] { "Attribute", "Start", "Adjustment", "Rating", "Legal Range" },
            cut.FindAll(".sr6-attribute-table span").Select(node => node.TextContent.Trim()).ToArray());
        Assert.AreEqual("Body", cut.Find("[data-sr6-attribute='BOD'] .sr6-attribute-row__name").TextContent.Trim());
        Assert.AreEqual("ready", cut.Find("[data-sr6-attribute='BOD']").GetAttribute("data-sr6-attribute-state"));
        Assert.AreEqual("Start 1 to 6", cut.Find("[data-sr6-attribute='BOD'] [data-sr6-stepper-group='base'] .sr6-attribute-stepper").GetAttribute("title"));
        Assert.AreEqual("Adjustment 0 to 5", cut.Find("[data-sr6-attribute='BOD'] [data-sr6-stepper-group='karma'] .sr6-attribute-stepper").GetAttribute("title"));
        Assert.AreEqual("4", cut.Find("[data-sr6-attribute='BOD'] [data-sr6-attribute-total]").TextContent.Trim());
        Assert.AreEqual("1 / 6 (9)", cut.Find("[data-sr6-attribute='BOD'] [data-sr6-attribute-limits]").TextContent.Trim());
        Assert.AreEqual(0, cut.FindAll(".section-table").Count);

        cut.Find("[data-sr6-attribute='BOD'] button[data-sr6-stepper='base-increase']").Click();

        Assert.IsNotNull(editRequest);
        Assert.AreEqual("Body", editRequest.AttributeName);
        Assert.AreEqual("base", editRequest.Bucket);
        Assert.AreEqual(4, editRequest.Value);
    }

    [TestMethod]
    public void SectionPane_renders_quality_quick_action_and_invokes_ui_control()
    {
        using var context = CreateContext();

        CharacterWorkspaceId workspaceId = new("ws-quality");
        OpenWorkspaceState openWorkspace = new(workspaceId, "Quality Runner", "QR", DateTimeOffset.UtcNow, RulesetDefaults.Sr4);
        string? invokedControlId = null;
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            WorkspaceId = workspaceId,
            OpenWorkspaces = [openWorkspace],
            ActiveSectionId = "qualities",
            ActiveSectionJson = "{\"qualities\":[]}",
            ActiveSectionRows = [new SectionRowState("qualities", "No entries")]
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.ExecuteUiControlRequested, (Action<string>)(controlId => invokedControlId = controlId)));

        AngleSharp.Dom.IElement addQuality = cut.Find("button[data-section-quick-action='quality_add']");
        Assert.AreEqual("Add Quality", addQuality.TextContent.Trim());

        addQuality.Click();

        Assert.AreEqual("quality_add", invokedControlId);
    }

    [TestMethod]
    public void SectionPane_renders_startup_workbench_with_first_class_restore_and_utility_actions()
    {
        using var context = CreateContext();

        DefaultCommandAvailabilityEvaluator evaluator = new();
        string? executedCommandId = null;
        string? loadedWorkspaceId = null;
        CharacterOverviewState startupState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: [new CharacterWorkspaceId("ws-recent-1"), new CharacterWorkspaceId("ws-recent-2")]),
            Commands =
            [
                new AppCommandDefinition("open_character", "Open", "file", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("new_character", "New", "file", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("new_character_origin", "Origin", "tools", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("character_roster", "Roster", "tools", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("master_index", "Index", "tools", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("auto_alice", "Alice", "tools", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("global_settings", "Options", "tools", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("report_bug", "Report Issue", "help", false, true, RulesetDefaults.Sr5)
            ]
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, startupState)
            .Add(component => component.IsCommandEnabled, command => evaluator.IsCommandEnabled(command, startupState))
            .Add(component => component.ExecuteCommandRequested, (Action<string>)(commandId => executedCommandId = commandId))
            .Add(component => component.LoadWorkspaceRequested, (Action<string>)(workspaceId => loadedWorkspaceId = workspaceId)));

        StringAssert.Contains(cut.Markup, "Continue Chummer Online");
        StringAssert.Contains(cut.Markup, "Start a fresh dossier, reopen a saved dossier, or jump straight into classic utilities from Chummer Online.");
        StringAssert.Contains(cut.Markup, "reopen a saved dossier");
        StringAssert.Contains(cut.Markup, "Recent Dossiers");
        StringAssert.Contains(cut.Markup, "Origin Dossier");
        StringAssert.Contains(cut.Markup, "Character Roster");
        StringAssert.Contains(cut.Markup, "Master Index");
        StringAssert.Contains(cut.Markup, "Auto ALICE");
        StringAssert.Contains(cut.Markup, "ws-recent-1");
        StringAssert.Contains(cut.Markup, "Restore this Chummer Online dossier continuation.");
        Assert.IsFalse(cut.Markup.Contains("Start a fresh runner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Find("[data-startup-command='open_character']").HasAttribute("disabled"));
        Assert.IsFalse(cut.Find("[data-startup-command='report_bug']").HasAttribute("disabled"));

        cut.Find("[data-startup-command='open_character']").Click();
        cut.Find("[data-recent-workspace-id='ws-recent-1']").Click();

        Assert.AreEqual("open_character", executedCommandId);
        Assert.AreEqual("ws-recent-1", loadedWorkspaceId);
    }

    [TestMethod]
    public void SectionPane_startup_workbench_without_recent_runners_uses_open_dossier_copy()
    {
        using var context = CreateContext();

        DefaultCommandAvailabilityEvaluator evaluator = new();
        CharacterOverviewState startupState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: []),
            Commands =
            [
                new AppCommandDefinition("open_character", "Open", "file", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("new_character", "New", "file", false, true, RulesetDefaults.Sr5)
            ]
        };

        IRenderedComponent<SectionPane> cut = context.Render<SectionPane>(parameters => parameters
            .Add(component => component.State, startupState)
            .Add(component => component.IsCommandEnabled, command => evaluator.IsCommandEnabled(command, startupState)));

        StringAssert.Contains(cut.Markup, "Open Dossier...");
        StringAssert.Contains(cut.Markup, "No recent dossiers yet.");
        StringAssert.Contains(cut.Markup, "restore a dossier from disk");
        Assert.IsNotNull(cut.Find("[data-startup-command='open_character']"));
    }

    [TestMethod]
    public void MetadataPanel_uses_dossier_metadata_copy()
    {
        using var context = CreateContext();

        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            WorkspaceId = new CharacterWorkspaceId("ws-meta")
        };

        IRenderedComponent<MetadataPanel> cut = context.Render<MetadataPanel>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.LoadWorkspaceId, "queued-dossier"));

        StringAssert.Contains(cut.Markup, "Dossier Metadata");
        StringAssert.Contains(cut.Markup, "Dossier ID");
        StringAssert.Contains(cut.Markup, "Update Dossier Metadata");
        StringAssert.Contains(cut.Markup, "Save Dossier");
        StringAssert.Contains(cut.Markup, "Load Dossier");
        StringAssert.Contains(cut.Markup, "placeholder=\"Dossier id\"");
        StringAssert.Contains(cut.Markup, "value=\"ws-meta\"");
    }

    [TestMethod]
    public void DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState originBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(originWizard);

        using var context = CreateContext();

        IRenderedComponent<DialogHost> wizardCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originWizard));
        StringAssert.Contains(wizardCut.Markup, "Advanced story controls");
        StringAssert.Contains(wizardCut.Markup, "Story Preview");
        Assert.AreEqual(
            "Optional dossier identity, life-path steering, and GM guidance for the story packet.",
            wizardCut.Find(".dialog-origin-advanced-content .dialog-note").TextContent.Trim());
        CollectionAssert.AreEqual(
            new[] { "Dossier", "Life Path", "GM Steering" },
            wizardCut.FindAll(".dialog-origin-subpanel > h4").Select(element => element.TextContent.Trim()).ToArray());
        Assert.AreEqual(0, wizardCut.FindAll(".dialog-body > .dialog-note").Count);
        Assert.IsNotNull(wizardCut.Find("[data-origin-wizard]"));
        Assert.IsNotNull(wizardCut.Find("select[data-field-id='newCharacterOriginMetatypePreference']"));
        Assert.IsNotNull(wizardCut.Find("select[data-field-id='newCharacterOriginBuildPreference']"));
        Assert.IsNotNull(wizardCut.Find("[data-origin-story-preview]"));
        Assert.IsNotNull(wizardCut.Find("[data-origin-story-preview] .dialog-origin-narrative"));

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));
        StringAssert.Contains(buildCut.Markup, "Origin Build Handoff");
        StringAssert.Contains(buildCut.Markup, "Build Handoff");
        StringAssert.Contains(buildCut.Markup, "Book Preview");
        StringAssert.Contains(buildCut.Markup, "Build Translation");
        StringAssert.Contains(buildCut.Markup, "aria-labelledby=\"dialogTitle\"");
        CollectionAssert.AreEqual(
            new[] { "Dossier", "Ruleset", "Method" },
            buildCut.FindAll(".dialog-origin-summary-strip .dialog-origin-summary-label").Select(element => element.TextContent.Trim()).ToArray());
        Assert.AreEqual(0, buildCut.FindAll(".dialog-body > .dialog-note").Count);
        Assert.IsNotNull(buildCut.Find("[data-origin-build]"));
        Assert.IsNotNull(buildCut.Find("[data-origin-book-preview]"));
        Assert.IsNotNull(buildCut.Find("[data-origin-build-support]"));
        var dossierLink = buildCut.Find("[data-origin-dossier-route-link]");
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Cipher", dossierLink.GetAttribute("data-origin-dossier-route-link"));
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Cipher", dossierLink.QuerySelector("code")!.TextContent.Trim());
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Cipher", dossierLink.QuerySelector("a")!.GetAttribute("href"));
        StringAssert.Contains(dossierLink.TextContent, "Open clean Origin Dossier route");
        Assert.IsFalse(buildCut.Markup.Contains("Open clean Chummer Online route", StringComparison.Ordinal));
        StringAssert.Contains(buildCut.Markup, "Show Origin Dossier link");
        Assert.IsFalse(buildCut.Markup.Contains("Show dossier link", StringComparison.Ordinal));
        StringAssert.Contains(buildCut.Markup, "clean Origin Dossier route");
        StringAssert.Contains(buildCut.Markup, "Use this clean route to reopen Origin Dossier without publishing the story text.");
        Assert.IsFalse(buildCut.Markup.Contains("Use this route to reopen the Origin Dossier workflow without publishing the story text.", StringComparison.Ordinal));
        StringAssert.Contains(buildCut.Markup, "story text stays local");
        Assert.IsFalse(buildCut.Markup.Contains("Opens Chummer Online directly into the Origin Dossier workflow.", StringComparison.Ordinal));
        Assert.IsNotNull(buildCut.Find(".dialog-origin-preview .dialog-origin-narrative"));
        Assert.IsFalse(buildCut.Markup.Contains("Runner", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_specialized_shells_fail_closed_to_dossier_identity_when_hidden_values_are_stale()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originWizard = baseWizard with
        {
            Fields = baseWizard.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterName" => field with { Value = string.Empty },
                    "newCharacterAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => string.Equals(field.Id, "newCharacterWorkflowAlias", StringComparison.Ordinal)
                    ? field with { Value = string.Empty }
                    : field)
                .ToArray()
        };

        using var wizardContext = CreateContext();

        IRenderedComponent<DialogHost> wizardCut = wizardContext.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originWizard));
        Assert.AreEqual("New dossier", wizardCut.Find("[data-field-id='newCharacterName'] input").GetAttribute("value"));
        Assert.AreEqual("Dossier", wizardCut.Find("[data-field-id='newCharacterAlias'] input").GetAttribute("value"));

        using var buildContext = CreateContext();

        IRenderedComponent<DialogHost> buildCut = buildContext.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));
        Assert.AreEqual(
            "Dossier",
            buildCut.FindAll(".dialog-origin-summary-strip .dialog-origin-summary-card strong")[0].TextContent.Trim());
        Assert.IsFalse(buildCut.Markup.Contains(">Runner<", StringComparison.Ordinal));
        Assert.IsFalse(buildCut.Markup.Contains(">Pending<", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_wizard_recovers_summary_and_story_preview_when_hidden_display_fields_are_blank()
    {
        static string NormalizeText(string value)
            => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        DesktopDialogState baseDialog = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        string expectedMetatype = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginMetatype");
        string expectedArchetype = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginArchetype");
        string expectedPath = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginPathSummary");
        string expectedStory = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginSummary");
        string expectedGmSummary = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginGmRequirementSummary");
        string expectedPressure = DesktopDialogFieldValueParser.GetValue(baseDialog, "newCharacterOriginQualityFocus");
        DesktopDialogState dialog = baseDialog with
        {
            Fields = baseDialog.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginMetatype" => field with { Value = string.Empty },
                    "newCharacterOriginArchetype" => field with { Value = string.Empty },
                    "newCharacterOriginPathSummary" => field with { Value = string.Empty },
                    "newCharacterOriginSummary" => field with { Value = string.Empty },
                    "newCharacterOriginGmRequirementSummary" => field with { Value = string.Empty },
                    "newCharacterOriginQualityFocus" => field with { Value = string.Empty },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, dialog));

        CollectionAssert.AreEqual(
            new[] { expectedMetatype, expectedArchetype, expectedPath },
            cut.Find("[data-origin-summary-strip]")
                .QuerySelectorAll(".dialog-origin-summary-card strong")
                .Select(element => element.TextContent.Trim())
                .ToArray());
        Assert.AreEqual(
            NormalizeText(expectedStory),
            NormalizeText(cut.Find("[data-origin-story-preview]").TextContent));

        cut.Find("[data-origin-advanced-toggle]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            CollectionAssert.AreEqual(
                new[] { expectedGmSummary, expectedPressure },
                cut.Find(".dialog-origin-advanced-content .dialog-origin-summary-strip")
                    .QuerySelectorAll(".dialog-origin-summary-card strong")
                    .Select(element => element.TextContent.Trim())
                    .ToArray());
        });
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_clean_route_when_hidden_link_value_is_blank()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginDossierLink" => field with { Value = string.Empty },
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        var dossierLink = buildCut.Find("[data-origin-dossier-route-link]");
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Dossier", dossierLink.GetAttribute("data-origin-dossier-route-link"));
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Dossier", dossierLink.QuerySelector("code")!.TextContent.Trim());
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr4&alias=Dossier", dossierLink.QuerySelector("a")!.GetAttribute("href"));
        Assert.IsFalse(dossierLink.TextContent.Contains("alias=Runner", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_preview_title_and_constraints_route_when_hidden_values_are_stale()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginDossierLink" => field with { Value = string.Empty },
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string bookPreviewText = buildCut.Find(".dialog-origin-book p").TextContent.Trim();
        Assert.AreEqual("Dossier: Origin Dossier", bookPreviewText.Split('\n', StringSplitOptions.RemoveEmptyEntries).First());

        string constraintsText = buildCut.FindAll(".dialog-visual-pre")
            .Single(element => element.TextContent.Contains("Dossier Link |", StringComparison.Ordinal))
            .TextContent;
        StringAssert.Contains(constraintsText, "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Dossier");
        Assert.IsFalse(constraintsText.Contains("alias=Cipher", StringComparison.Ordinal));
        Assert.IsFalse(bookPreviewText.Contains("Cipher: Origin Dossier", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_constraints_route_when_constraints_label_is_legacy_cased()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginImplications" => field with
                    {
                        Value = "dossier link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                "sheet changes | Sheet changes visible after the origin packet.",
                        Placeholder = "dossier link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                      "sheet changes | Sheet changes visible after the origin packet."
                    },
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string constraintsText = buildCut.FindAll(".dialog-visual-pre")
            .Single(element => element.TextContent.Contains("Dossier Link |", StringComparison.Ordinal))
            .TextContent;
        StringAssert.Contains(constraintsText, "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Dossier");
        Assert.IsFalse(constraintsText.Contains("alias=Cipher", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_constraints_route_when_constraints_label_uses_colon_separator()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginImplications" => field with
                    {
                        Value = "dossier link: /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                "sheet changes | Sheet changes visible after the origin packet.",
                        Placeholder = "dossier link: /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                      "sheet changes | Sheet changes visible after the origin packet."
                    },
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string constraintsText = buildCut.FindAll(".dialog-visual-pre")
            .Single(element => element.TextContent.Contains("Dossier Link |", StringComparison.Ordinal))
            .TextContent;
        StringAssert.Contains(constraintsText, "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Dossier");
        Assert.IsFalse(constraintsText.Contains("alias=Cipher", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_build_does_not_duplicate_dossier_link_line_when_canonicalizing_constraints()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterOriginImplications" => field with
                    {
                        Value = "dossier link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                "sheet changes | Sheet changes visible after the origin packet.",
                        Placeholder = "dossier link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                      "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Cipher" + Environment.NewLine +
                                      "sheet changes | Sheet changes visible after the origin packet."
                    },
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string constraintsText = buildCut.FindAll(".dialog-visual-pre")
            .Single(element => element.TextContent.Contains("Dossier Link |", StringComparison.Ordinal))
            .TextContent;
        StringAssert.Contains(constraintsText, "Dossier Link | /app?command=new_character_origin&ruleset=sr4&alias=Dossier");
        Assert.IsFalse(constraintsText.Contains("alias=Cipher", StringComparison.Ordinal));
        int dossierCount = constraintsText.Split("Dossier Link |", StringSplitOptions.None).Length - 1;
        Assert.AreEqual(1, dossierCount, "The duplicated dossier link constraints lines should be collapsed to a single canonical row.");
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_stale_one_line_book_preview_title()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterWorkflowAlias" => field with { Value = "Runner" },
                    "newCharacterOriginBookPreview" => field with { Value = "Runner: Origin Dossier" },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        Assert.AreEqual("Dossier: Origin Dossier", buildCut.Find(".dialog-origin-book p").TextContent.Trim());
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_story_panel_when_hidden_story_is_blank()
    {
        static string NormalizeText(string value)
            => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        string expectedStory = DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterOriginSummary");
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => string.Equals(field.Id, "newCharacterOriginStory", StringComparison.Ordinal)
                    ? field with { Value = string.Empty }
                    : field)
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        Assert.AreEqual(
            NormalizeText(expectedStory),
            NormalizeText(buildCut.Find(".dialog-origin-narrative").TextContent));
    }

    [TestMethod]
    public void DialogHost_origin_build_uses_bound_dialog_message_for_book_preview_guidance()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        const string customMessage = "Confirm the fiction first; only then continue into guided chargen.";
        DesktopDialogState originBuild = baseBuild with { Message = customMessage };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        StringAssert.Contains(buildCut.Markup, customMessage);
        Assert.IsFalse(
            buildCut.Markup.Contains(DesktopDialogFactory.BuildOriginBuildDialogMessageDisplayValue(), StringComparison.Ordinal),
            "Origin build guidance should follow the bound dialog message instead of a stale hardcoded fallback.");
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_dossier_link_notes_when_hidden_notes_are_stale()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        string expectedNotes = DesktopDialogFactory.BuildOriginDossierLinkNotesDisplayValue();
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => string.Equals(field.Id, "newCharacterOriginDossierLinkNotes", StringComparison.Ordinal)
                    ? field with { Value = "Opens Chummer Online directly into the Origin Dossier workflow." }
                    : field)
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string notesText = buildCut.FindAll(".dialog-visual-pre")
            .First(element => element.TextContent.Contains("story text stays local", StringComparison.Ordinal))
            .TextContent;
        StringAssert.Contains(notesText, expectedNotes);
        Assert.IsFalse(notesText.Contains("Chummer Online", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_book_preview_body_when_hidden_preview_is_blank()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        string expectedStory = DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterOriginSummary");
        string expectedBuildSummary = (DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterOriginImplications") ?? string.Empty)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(line => line.StartsWith("Build |", StringComparison.Ordinal))
            .Split('|', 2, StringSplitOptions.TrimEntries)[1];
        string expectedGmRequirements = (DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterOriginImplications") ?? string.Empty)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(line => line.StartsWith("GM Requirements |", StringComparison.Ordinal))
            .Split('|', 2, StringSplitOptions.TrimEntries)[1];
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => string.Equals(field.Id, "newCharacterOriginBookPreview", StringComparison.Ordinal)
                    ? field with { Value = string.Empty }
                    : field)
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        string bookPreviewText = buildCut.Find("[data-origin-book-preview]").TextContent;
        StringAssert.Contains(bookPreviewText, expectedStory);
        StringAssert.Contains(bookPreviewText, $"The shape of the build is visible in the fiction: {expectedBuildSummary}");
        StringAssert.Contains(bookPreviewText, $"At the table, the story keeps these constraints in view: {expectedGmRequirements}");
        StringAssert.Contains(bookPreviewText, "When this origin feels right, start character creation.");
    }

    [TestMethod]
    public void DialogHost_origin_build_recovers_summary_ruleset_and_method_when_workflow_fields_are_blank()
    {
        DesktopDialogState baseWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        DesktopDialogState baseBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(baseWizard);
        string expectedRoute = DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterOriginDossierLink");
        string expectedRulesetLabel = (RulesetDefaults.NormalizeOptional(DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterWorkflowRulesetId")) ?? RulesetDefaults.Sr5).ToUpperInvariant();
        string expectedBuildMethod = DesktopDialogFieldValueParser.GetValue(baseBuild, "newCharacterWorkflowBuildMethod");
        DesktopDialogState originBuild = baseBuild with
        {
            Fields = baseBuild.Fields
                .Select(field => field.Id switch
                {
                    "newCharacterWorkflowRulesetId" => field with { Value = string.Empty },
                    "newCharacterWorkflowBuildMethod" => field with { Value = string.Empty },
                    _ => field
                })
                .ToArray()
        };

        using var context = CreateContext();

        IRenderedComponent<DialogHost> buildCut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originBuild));

        IReadOnlyList<string> summaryValues = buildCut.FindAll(".dialog-origin-summary-card strong")
            .Select(element => element.TextContent.Trim())
            .ToArray();
        var dossierLink = buildCut.Find("[data-origin-dossier-route-link]");

        Assert.AreEqual(expectedRulesetLabel, summaryValues[1]);
        Assert.AreEqual(expectedBuildMethod, summaryValues[2]);
        Assert.AreEqual(expectedRoute, dossierLink.GetAttribute("data-origin-dossier-route-link"));
        Assert.AreEqual(expectedRoute, dossierLink.QuerySelector("code")!.TextContent.Trim());
        Assert.IsFalse(string.Equals("Pending", summaryValues[2], StringComparison.Ordinal));
        Assert.IsFalse(dossierLink.TextContent.Contains("ruleset=sr5", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DialogHost_keeps_origin_advanced_controls_open_across_dialog_rerenders()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");

        using var context = CreateContext();

        IRenderedComponent<DialogHostHarness> cut = context.Render<DialogHostHarness>();
        cut.InvokeAsync(() => cut.Instance.SetDialog(originWizard)).GetAwaiter().GetResult();

        cut.Find("[data-origin-advanced-toggle]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });

        DesktopDialogState updatedWizard = originWizard with
        {
            Fields = originWizard.Fields
                .Select(field => string.Equals(field.Id, "newCharacterOriginBackground", StringComparison.Ordinal)
                    ? field with
                    {
                        Value = "corporate",
                        Placeholder = "corporate"
                    }
                    : field)
                .ToArray()
        };

        cut.InvokeAsync(() => cut.Instance.SetDialog(updatedWizard)).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });
    }

    [TestMethod]
    public async Task DialogHost_keeps_origin_advanced_controls_open_across_multiple_origin_select_changes()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");

        using var context = CreateContext();
        IRenderedComponent<LiveOriginDialogHostHarness> cut = context.Render<LiveOriginDialogHostHarness>(parameters => parameters
            .Add(component => component.InitialDialog, originWizard));

        cut.Find("[data-origin-advanced-toggle]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });

        await cut.Find("select[data-field-id='newCharacterOriginMetatypePreference']")
            .ChangeAsync(new ChangeEventArgs { Value = "human" });
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });

        await cut.Find("select[data-field-id='newCharacterOriginBuildPreference']")
            .ChangeAsync(new ChangeEventArgs { Value = "BP" });
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });
    }

    [TestMethod]
    public async Task DialogHost_keeps_origin_advanced_controls_open_when_parent_recreates_the_dialog_host_during_select_refreshes()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");

        using var context = CreateContext();
        IRenderedComponent<RemountingOriginDialogHostHarness> cut = context.Render<RemountingOriginDialogHostHarness>(parameters => parameters
            .Add(component => component.InitialDialog, originWizard));

        cut.Find("[data-origin-advanced-toggle]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });

        foreach (string fieldId in new[]
                 {
                     "newCharacterOriginMetatypePreference",
                     "newCharacterOriginArchetypeIntent",
                     "newCharacterRulesetId",
                     "newCharacterOriginBuildPreference",
                     "newCharacterOriginBackground",
                     "newCharacterOriginTurningPoint",
                     "newCharacterOriginTrainingPath",
                     "newCharacterOriginUpgradeExposure",
                     "newCharacterOriginPressureCost",
                     "newCharacterOriginMotivation",
                     "newCharacterOriginTone",
                     "newCharacterOriginGmConstraintPreset"
                 })
        {
            IElement select = cut.Find($"select[data-field-id='{fieldId}']");
            string currentValue = select.GetAttribute("value")
                ?? throw new AssertFailedException($"Origin select '{fieldId}' did not expose a current value.");
            string nextValue = select.Children
                .Where(option => string.Equals(option.TagName, "OPTION", StringComparison.OrdinalIgnoreCase))
                .Select(option => option.GetAttribute("value"))
                .First(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, currentValue, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Origin select '{fieldId}' did not expose an alternative value.");

            await select.ChangeAsync(new ChangeEventArgs { Value = nextValue });
            cut.WaitForAssertion(() =>
            {
                Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"), $"Origin advanced controls should stay expanded after the parent remounts the dialog host on '{fieldId}'.");
                Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"), $"Origin advanced controls should stay visible after the parent remounts the dialog host on '{fieldId}'.");
            });
        }
    }

    [TestMethod]
    public async Task DialogHost_restores_origin_scroll_when_parent_recreates_the_dialog_host_during_select_refreshes()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");

        using var context = CreateContext();
        IRenderedComponent<RemountingOriginDialogHostHarness> cut = context.Render<RemountingOriginDialogHostHarness>(parameters => parameters
            .Add(component => component.InitialDialog, originWizard));

        cut.Find("[data-origin-advanced-toggle]").Click();
        IElement select = cut.Find("select[data-field-id='newCharacterOriginBuildPreference']");
        string currentValue = select.GetAttribute("value")
            ?? throw new AssertFailedException("Origin build-preference select did not expose a current value.");
        string nextValue = select.Children
            .Where(option => string.Equals(option.TagName, "OPTION", StringComparison.OrdinalIgnoreCase))
            .Select(option => option.GetAttribute("value"))
            .First(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, currentValue, StringComparison.Ordinal))
            ?? throw new AssertFailedException("Origin build-preference select did not expose an alternative value.");

        await select.TriggerEventAsync("onfocus", new FocusEventArgs());
        int restoreInvocationsBeforeChange = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.restorePendingDialogScroll", StringComparison.Ordinal));

        await select.ChangeAsync(new ChangeEventArgs { Value = nextValue });
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        });

        int restoreInvocationsAfterChange = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.restorePendingDialogScroll", StringComparison.Ordinal));

        Assert.IsTrue(
            restoreInvocationsAfterChange > restoreInvocationsBeforeChange,
            "A remounted Origin Dossier dialog host should consume the shared pending scroll restore so select refreshes do not jump the viewport.");
    }

    [TestMethod]
    public async Task DialogHost_updates_origin_select_scroll_capture_when_another_select_gains_focus_before_refresh()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        List<DialogFieldInputChange> inputChanges = [];

        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originWizard)
            .Add(component => component.FieldInputRequested, (Action<DialogFieldInputChange>)(change => inputChanges.Add(change))));

        cut.Find("[data-origin-advanced-toggle]").Click();
        IElement buildPreferenceSelect = cut.Find("select[data-field-id='newCharacterOriginBuildPreference']");
        IElement metatypePreferenceSelect = cut.Find("select[data-field-id='newCharacterOriginMetatypePreference']");

        await buildPreferenceSelect.TriggerEventAsync("onfocus", new FocusEventArgs());

        int captureInvocationsAfterFirstFocus = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal));

        await metatypePreferenceSelect.TriggerEventAsync("onfocus", new FocusEventArgs());

        int captureInvocationsAfterSecondFocus = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal));

        await metatypePreferenceSelect.ChangeAsync(new ChangeEventArgs { Value = "human" });

        int captureInvocationsAfterChange = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal));

        Assert.AreEqual(1, inputChanges.Count);
        Assert.AreEqual("newCharacterOriginMetatypePreference", inputChanges[0].FieldId);
        Assert.AreEqual("human", inputChanges[0].Value);
        Assert.AreEqual(1, captureInvocationsAfterFirstFocus, "The first origin select focus should arm the scroll capture.");
        Assert.AreEqual(
            captureInvocationsAfterFirstFocus + 1,
            captureInvocationsAfterSecondFocus,
            "A second origin select focus should replace the prior scroll anchor with the latest active select.");
        Assert.AreEqual(
            captureInvocationsAfterSecondFocus + 1,
            captureInvocationsAfterChange,
            "The later origin select change should recapture the latest scroll anchor right before refresh so the dialog does not jump back to an earlier focus position.");
    }

    [TestMethod]
    public async Task DialogHost_recaptures_origin_select_scroll_when_the_same_select_changes_after_focus()
    {
        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        List<DialogFieldInputChange> inputChanges = [];

        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, originWizard)
            .Add(component => component.FieldInputRequested, (Action<DialogFieldInputChange>)(change => inputChanges.Add(change))));

        cut.Find("[data-origin-advanced-toggle]").Click();
        IElement metatypePreferenceSelect = cut.Find("select[data-field-id='newCharacterOriginMetatypePreference']");

        await metatypePreferenceSelect.TriggerEventAsync("onfocus", new FocusEventArgs());

        int captureInvocationsAfterFocus = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal));

        await metatypePreferenceSelect.ChangeAsync(new ChangeEventArgs { Value = "human" });

        int captureInvocationsAfterChange = context.JSInterop.Invocations.Count(invocation =>
            string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal));

        Assert.AreEqual(1, inputChanges.Count);
        Assert.AreEqual("newCharacterOriginMetatypePreference", inputChanges[0].FieldId);
        Assert.AreEqual("human", inputChanges[0].Value);
        Assert.AreEqual(1, captureInvocationsAfterFocus, "The origin select focus should arm the first scroll capture.");
        Assert.AreEqual(
            captureInvocationsAfterFocus + 1,
            captureInvocationsAfterChange,
            "Changing the same origin select after focus should recapture scroll state so the advanced controls stay anchored at the current viewport.");
    }

    [TestMethod]
    public void App_restoreDialogScroll_prefers_origin_field_anchor_before_advanced_panel_anchor()
    {
        string source = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "Chummer.Blazor",
            "Components",
            "App.razor"));

        StringAssert.Contains(source, "if (anchoredRestorePending && !restoreOriginFieldAnchor())");
        StringAssert.Contains(source, "restoreOriginAdvancedAnchor();");
        Assert.IsFalse(
            source.Contains("const shouldPreferOriginAdvancedAnchor = function()", StringComparison.Ordinal),
            "Origin wizard scroll restore should not prefer the advanced-panel anchor ahead of the active field anchor.");
    }

    private sealed class DialogHostHarness : ComponentBase
    {
        private DesktopDialogState? _dialog;

        public void SetDialog(DesktopDialogState dialog)
        {
            _dialog = dialog;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DialogHost>(0);
            builder.AddAttribute(1, nameof(DialogHost.Dialog), _dialog);
            builder.CloseComponent();
        }
    }

    private sealed class LiveOriginDialogHostHarness : ComponentBase
    {
        [Parameter]
        public DesktopDialogState? InitialDialog { get; set; }

        private DesktopDialogState? _dialog;

        protected override void OnParametersSet()
        {
            _dialog ??= InitialDialog;
        }

        private void OnFieldInputRequested(DialogFieldInputChange change)
        {
            if (_dialog is null)
            {
                return;
            }

            _dialog = _dialog with
            {
                Fields = _dialog.Fields
                    .Select(field => string.Equals(field.Id, change.FieldId, StringComparison.Ordinal)
                        ? field with { Value = change.Value ?? string.Empty }
                        : field)
                    .ToArray()
            };
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DialogHost>(0);
            builder.AddAttribute(1, nameof(DialogHost.Dialog), _dialog);
            builder.AddAttribute(2, nameof(DialogHost.FieldInputRequested), EventCallback.Factory.Create<DialogFieldInputChange>(this, OnFieldInputRequested));
            builder.CloseComponent();
        }
    }

    private sealed class RemountingOriginDialogHostHarness : ComponentBase
    {
        [Parameter]
        public DesktopDialogState? InitialDialog { get; set; }

        private DesktopDialogState? _dialog;
        private bool _originWizardAdvancedControlsOpen;
        private bool _useAlternateKey;

        protected override void OnParametersSet()
        {
            _dialog ??= InitialDialog;
        }

        private void OnFieldInputRequested(DialogFieldInputChange change)
        {
            if (_dialog is null)
            {
                return;
            }

            _dialog = _dialog with
            {
                Fields = _dialog.Fields
                    .Select(field => string.Equals(field.Id, change.FieldId, StringComparison.Ordinal)
                        ? field with { Value = change.Value ?? string.Empty }
                        : field)
                    .ToArray()
            };
            _useAlternateKey = !_useAlternateKey;
            StateHasChanged();
        }

        private void OnOriginWizardAdvancedControlsOpenChanged(bool isOpen)
        {
            _originWizardAdvancedControlsOpen = isOpen;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DialogHost>(0);
            builder.SetKey(_useAlternateKey ? "origin-dialog-host-b" : "origin-dialog-host-a");
            builder.AddAttribute(1, nameof(DialogHost.Dialog), _dialog);
            builder.AddAttribute(2, nameof(DialogHost.FieldInputRequested), EventCallback.Factory.Create<DialogFieldInputChange>(this, OnFieldInputRequested));
            builder.AddAttribute(3, nameof(DialogHost.OriginWizardAdvancedControlsOpen), _originWizardAdvancedControlsOpen);
            builder.AddAttribute(4, nameof(DialogHost.OriginWizardAdvancedControlsOpenChanged), EventCallback.Factory.Create<bool>(this, OnOriginWizardAdvancedControlsOpenChanged));
            builder.CloseComponent();
        }
    }

    [TestMethod]
    public void SectionPane_formats_named_context_for_collection_sections()
    {
        using var context = CreateContext();
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
        using var context = CreateContext();
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
        using var context = CreateContext();
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
                        Summary: "Reusable payload for either Build Idea Card or local template creation.",
                        PayloadKind: "build-lab-export",
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
                SupportClosureSummary: "Support can use the same runtime fingerprint after the next step.",
                TeamCoverage: new BuildLabTeamCoverageProjection(
                    Summary: "2 of 3 required crew roles are covered before the next step; one deliberate face overlap stays visible while astral support remains missing.",
                    CoverageSummary: "Coverage score stays grounded with Face and Legwork already covered before the first campaign step.",
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
        StringAssert.Contains(cut.Markup, "Coverage score stays grounded with Face and Legwork already covered before the first campaign step.");
        Assert.IsFalse(cut.Markup.Contains("Coverage score stays stable with Face and Legwork already covered before the first campaign step.", StringComparison.Ordinal));
        StringAssert.Contains(cut.Markup, "Light face overlap");
        StringAssert.Contains(cut.Markup, "strongest coverage checkpoint at 100 Karma");
        StringAssert.Contains(cut.Markup, "Decision rail");
        StringAssert.Contains(cut.Markup, "Rebind the active runtime before export.");
        StringAssert.Contains(cut.Markup, "Support can use the same runtime fingerprint after the next step.");
        StringAssert.Contains(cut.Markup, "Build blocker details");
        StringAssert.Contains(cut.Markup, "Explanation");
        StringAssert.Contains(cut.Markup, "Rule environment");
        StringAssert.Contains(cut.Markup, "Environment change");
        StringAssert.Contains(cut.Markup, "One quick-action binding still needs review. -&gt; Rebind the active runtime before export.");
        StringAssert.Contains(cut.Markup, "One quick-action binding still needs review.");
        Assert.IsNotNull(cut.Find("[data-build-blocker-explain-receipt]"));
        StringAssert.Contains(cut.Markup, "data-build-lab-export-target");
        StringAssert.Contains(cut.Markup, "data-build-lab-optimizer-rail");
    }

    [TestMethod]
    public void GmBoardFeed_renders_tactical_cards_instead_of_generic_feed()
    {
        using var context = CreateContext();
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
        using var context = CreateContext();
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

        using var context = CreateContext();
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
        using var context = CreateContext();
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
        using var context = CreateContext();
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

        using var context = CreateContext();
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
                        new GeneratedAssetMetadataField("shadowfeedDispatchReceipt", "Dispatch Status", "pending"),
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
            StringAssert.Contains(cut.Markup, "Video preview");
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

        using var context = CreateContext();
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
        using var context = CreateContext();
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
        using var context = CreateContext();
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

        StringAssert.Contains(cut.Markup, "Rules setup health");
        StringAssert.Contains(cut.Markup, "Rule Pack Diagnostics");
        StringAssert.Contains(cut.Markup, "Desktop connection");
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
        StringAssert.Contains(cut.Markup, "System details");
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
    public void RuntimeInspectorPanel_uses_current_and_no_diff_badges_for_clean_local_diagnostics()
    {
        using var context = CreateContext();
        IRenderedComponent<RuntimeInspectorPanel> cut = context.Render<RuntimeInspectorPanel>(parameters => parameters
            .Add(component => component.Projection, new RuntimeInspectorProjection(
                TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
                TargetId: "official.sr6.core",
                RuntimeLock: new ResolvedRuntimeLock(
                    RulesetId: RulesetDefaults.Sr6,
                    ContentBundles:
                    [
                        new ContentBundleDescriptor("sr6.core.bundle", RulesetDefaults.Sr6, "1.0.0", "SR6 Core", "Core bundle", ["data/sr6-core.xml"])
                    ],
                    RulePacks:
                    [
                        new ArtifactVersionReference("official.sr6.core", "1.0.0")
                    ],
                    ProviderBindings: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [RulePackCapabilityIds.DeriveStat] = "official.sr6.core/derive.stat"
                    },
                    EngineApiVersion: "1.0.0",
                    RuntimeFingerprint: "sha256:sr6-runtime-fingerprint"),
                Install: new ArtifactInstallState(
                    ArtifactInstallStates.Pinned,
                    InstalledTargetKind: RuntimeInspectorTargetKinds.Workspace,
                    InstalledTargetId: "workspace-sr6",
                    RuntimeFingerprint: "sha256:sr6-runtime-fingerprint"),
                ResolvedRulePacks:
                [
                    new RuntimeInspectorRulePackEntry(
                        new ArtifactVersionReference("official.sr6.core", "1.0.0"),
                        "SR6 Core",
                        ArtifactVisibilityModes.Shared,
                        ArtifactTrustTiers.Official,
                        [RulePackCapabilityIds.DeriveStat],
                        SourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile)
                ],
                ProviderBindings:
                [
                    new RuntimeInspectorProviderBinding(RulePackCapabilityIds.DeriveStat, "official.sr6.core/derive.stat", "official.sr6.core", SessionSafe: false)
                ],
                CompatibilityDiagnostics: [],
                Warnings: [],
                MigrationPreview: [],
                GeneratedAtUtc: new DateTimeOffset(2026, 03, 10, 9, 0, 0, TimeSpan.Zero),
                ProfileSourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile,
                CapabilityDescriptors:
                [
                    new RuntimeInspectorCapabilityDescriptorProjection(
                        RulePackCapabilityIds.DeriveStat,
                        RulesetCapabilityInvocationKinds.Rule,
                        "Derived Stat",
                        Explainable: true,
                        SessionSafe: false,
                        DefaultGasBudget: new RulesetGasBudget(100, 200, 4 * 1024 * 1024),
                        ProviderId: "official.sr6.core/derive.stat",
                        PackId: "official.sr6.core")
                ])));

        StringAssert.Contains(cut.Markup, ">current<");
        StringAssert.Contains(cut.Markup, ">no diff<");
        Assert.IsFalse(cut.Markup.Contains(">stable<", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find("[data-diagnostics-environment-diff]"));
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

        using var context = CreateContext();
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
        using var context = CreateContext();
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

        using var context = CreateContext();
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
        using var context = CreateContext();
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
        using var context = CreateContext();
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
            StringAssert.Contains(cut.Markup, "Video preview");
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
        using var context = CreateContext();
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
        using var context = CreateContext();
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
                new DesktopDialogField(
                    "ruleset",
                    "Ruleset",
                    "sr5",
                    "choose ruleset",
                    InputType: "select",
                    Options:
                    [
                        new DesktopDialogFieldOption("sr4", "SR4"),
                        new DesktopDialogFieldOption("sr5", "SR5")
                    ]),
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

        using var context = CreateContext();
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
        IElement rulesetSelect = cut.Find("select[data-field-id='ruleset']");
        IElement notesInput = cut.Find("textarea[placeholder='enter notes']");
        IElement readonlyToken = cut.Find("input[placeholder='readonly token']");
        IElement checkbox = cut.Find("input[type='checkbox']");
        IElement saveButton = cut.Find("#dialogFooter .action-btn.primary");
        IElement closeButton = cut.Find("#dialogClose");

        Assert.IsTrue(readonlyToken.HasAttribute("readonly"));
        Assert.IsTrue(string.IsNullOrEmpty(nameInput.GetAttribute("title")));
        Assert.AreEqual("Name", nameInput.GetAttribute("aria-label"));
        StringAssert.Contains(nameInput.GetAttribute("aria-description"), "Editable text field");
        Assert.AreEqual("Ruleset", rulesetSelect.GetAttribute("aria-label"));
        StringAssert.Contains(rulesetSelect.GetAttribute("aria-description"), "Editable");
        StringAssert.Contains(rulesetSelect.GetAttribute("aria-description"), "choose ruleset");
        Assert.IsTrue(string.IsNullOrEmpty(notesInput.GetAttribute("title")));
        Assert.AreEqual("Notes", notesInput.GetAttribute("aria-label"));
        StringAssert.Contains(notesInput.GetAttribute("aria-description"), "Editable multi-line text field");
        Assert.IsTrue(string.IsNullOrEmpty(checkbox.GetAttribute("title")));
        Assert.AreEqual("House Rules", checkbox.GetAttribute("aria-label"));
        StringAssert.Contains(checkbox.GetAttribute("aria-description"), "Editable checkbox");
        Assert.AreEqual("Save", saveButton.GetAttribute("title"));
        Assert.AreEqual("Save", saveButton.GetAttribute("aria-label"));
        StringAssert.Contains(saveButton.ClassName, "classic-dialog-action");
        StringAssert.Contains(saveButton.GetAttribute("aria-description"), "Primary dialog action");
        Assert.AreEqual("Close dialog", closeButton.GetAttribute("title"));
        Assert.AreEqual("Close dialog", closeButton.GetAttribute("aria-label"));

        cut.Find("input[placeholder='enter name']").Input("Neo");
        cut.Find("select[data-field-id='ruleset']").Change("sr4");
        cut.Find("textarea[placeholder='enter notes']").Input("Updated notes");
        cut.Find("input[type='checkbox']").Change(true);
        cut.Find("#dialogFooter .action-btn.primary").Click();
        cut.Find("#dialogClose").Click();

        string[] expectedInputFieldIds = ["name", "ruleset", "notes"];
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
        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, (DesktopDialogState?)null));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    [TestMethod]
    public void DialogHost_renders_explain_receipt_for_trust_dialogs()
    {
        DesktopDialogState dialog = new(
            Id: "import-support",
            Title: "Import support",
            Message: "Review the support details before continuing.",
            Fields:
            [
                new DesktopDialogField("environment", "Rule environment", "sr5; approved; payload sha256:abc123.", string.Empty, IsReadOnly: true),
                new DesktopDialogField("before", "Before", "Incoming chum5 payload before workspace merge.", string.Empty, IsReadOnly: true),
                new DesktopDialogField("after", "After", "Rebind gear plugins after import.", string.Empty, IsReadOnly: true),
                new DesktopDialogField("receipt", "Explanation", "dialog/import-support", string.Empty, IsReadOnly: true),
                new DesktopDialogField("support", "Support reuse", "Support can use payload sha256:abc123.", string.Empty, IsReadOnly: true)
            ],
            Actions:
            [
                new DesktopDialogAction("continue", "Continue", true)
            ]);

        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, dialog));

        IElement explainReceipt = cut.Find("[data-dialog-explain-receipt]");
        StringAssert.Contains(explainReceipt.TextContent, "Incoming chum5 payload before workspace merge.");
        StringAssert.Contains(explainReceipt.TextContent, "dialog/import-support");
        StringAssert.Contains(explainReceipt.TextContent, "Support can use payload sha256:abc123.");
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
                        $"Dossier Mugshot{Environment.NewLine}Portrait Source | {portraitPath}{Environment.NewLine}Portrait Match | watched dossier sibling",
                        "Dossier Mugshot",
                        IsMultiline: true,
                        IsReadOnly: true,
                        VisualKind: DesktopDialogFieldVisualKinds.Image)
                ],
                Actions:
                [
                    new DesktopDialogAction("close", "Close", true)
                ]);

            using var context = CreateContext();
            IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
                .Add(component => component.Dialog, dialog));

            IElement preview = cut.Find(".dialog-image-preview");
            StringAssert.StartsWith(preview.GetAttribute("src"), "data:image/png;base64,", StringComparison.Ordinal);
            Assert.AreEqual("Dossier Mugshot", preview.GetAttribute("alt"));
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
    public void DialogHost_roster_hierarchy_uses_dossier_copy_and_keeps_empty_state_non_draggable()
    {
        DesktopDialogState dialog = new(
            Id: "dialog.character_roster",
            Title: "Character Roster",
            Message: "Organize dossiers.",
            Fields:
            [
                new DesktopDialogField(
                    "rosterCustomFolders",
                    "Saved Dossiers",
                    $"[Saved Dossiers]{Environment.NewLine}Campaign A · custom{Environment.NewLine}   └─ no saved dossiers yet",
                    string.Empty,
                    IsMultiline: true,
                    IsReadOnly: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Tree)
            ],
            Actions:
            [
                new DesktopDialogAction("close", "Close", true)
            ]);

        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, dialog));

        IElement toolbar = cut.Find("[data-roster-hierarchy-toolbar='rosterCustomFolders']");
        StringAssert.Contains(toolbar.TextContent, "Dossier library tree");
        StringAssert.Contains(toolbar.TextContent, "Create your own folder hierarchy, then drag dossiers or custom folders onto any directory.");

        IElement folderLine = cut.FindAll("[data-roster-tree-line]")
            .Single(element => element.TextContent.Contains("Campaign A · custom", StringComparison.Ordinal));
        StringAssert.Contains(folderLine.GetAttribute("title"), "Drop a dossier or link row here");
        StringAssert.Contains(folderLine.ClassName, "is-drop-target");

        IElement emptyLine = cut.FindAll("[data-roster-tree-line]")
            .Single(element => element.TextContent.Contains("no saved dossiers yet", StringComparison.Ordinal));
        Assert.IsFalse(emptyLine.ClassName.Contains("is-draggable", StringComparison.Ordinal), "The dossier empty-state line must not present as draggable.");
        Assert.AreEqual("-1", emptyLine.GetAttribute("tabindex"));
        Assert.AreEqual("presentation", emptyLine.GetAttribute("data-roster-line-kind"));
    }

    [TestMethod]
    public void DialogHost_renders_priority_workflow_with_real_controls_and_commit_gate()
    {
        PriorityWorkflowDialogRuntimeState runtimeState = new(
            Mode: "Priority",
            SumToTenLabel: "Total priority value: 10 / 10",
            MetavariantOptions:
            [
                new DesktopDialogFieldOption("Elf", "Elf"),
                new DesktopDialogFieldOption("Dryad", "Dryad")
            ],
            SelectedMetavariant: "Dryad",
            MetatypeKarma: "30",
            SpecialAttributes: "5",
            Source: "Run Faster p. 65",
            InspectAttributes:
            [
                new PriorityWorkflowInspectAttributeState("BOD", "3"),
                new PriorityWorkflowInspectAttributeState("AGI", "7")
            ],
            Qualities: ["Low-Light Vision", "Keen Ears"],
            ForceVisible: false,
            Force: 1,
            PossessionVisible: false,
            PossessionBased: false,
            PossessionMethodOptions: [],
            SelectedPossessionMethod: string.Empty,
            SkillSelectionLabel: "Choose the magical skills granted by your talent path.",
            SkillChoice1: new PriorityWorkflowChoiceState(
                true,
                "Spellcasting",
                [
                    new DesktopDialogFieldOption("Spellcasting", "Spellcasting"),
                    new DesktopDialogFieldOption("Counterspelling", "Counterspelling")
                ]),
            SkillChoice2: new PriorityWorkflowChoiceState(
                true,
                "Assensing",
                [
                    new DesktopDialogFieldOption("Assensing", "Assensing"),
                    new DesktopDialogFieldOption("Summoning", "Summoning")
                ]),
            SkillChoice3: PriorityWorkflowChoiceState.Hidden,
            CanCommit: false);

        DesktopDialogState dialog = new(
            Id: "dialog.new_character.priority_workflow",
            Title: "Priority Build",
            Message: "Allocate priorities before creating the character.",
            Fields:
            [
                SelectField("newCharacterMetatypeCategory", "Category", "Metahuman", "Standard", "Core choices", "Metahuman", "Non-human choices"),
                SelectField("newCharacterMetatype", "Metatype", "Elf", "Elf", "Elf", "Troll", "Troll"),
                SelectField("newCharacterPriorityHeritage", "Metatype", "A", "A", "A", "B", "B"),
                SelectField("newCharacterPriorityAttributes", "Attributes", "B", "A", "A", "B", "B", "C", "C"),
                SelectField("newCharacterPriorityTalent", "Magic or Resonance", "C", "A", "A", "C", "C", "D", "D"),
                SelectField("newCharacterPrioritySkills", "Skills", "D", "A", "A", "D", "D", "E", "E"),
                SelectField("newCharacterPriorityResources", "Resources", "E", "A", "A", "E", "E", "B", "B"),
                SelectField("newCharacterPriorityTalentChoice", "Talent Choice", "Mystic Adept", "Mundane", "Mundane", "Mystic Adept", "Mystic Adept", "Adept", "Adept"),
                new DesktopDialogField(
                    "newCharacterMetavariant",
                    "Metavariant",
                    runtimeState.SelectedMetavariant,
                    runtimeState.SelectedMetavariant,
                    InputType: "select",
                    Options: runtimeState.MetavariantOptions),
                new DesktopDialogField(
                    "newCharacterPrioritySkillChoice1",
                    "Skill Choice 1",
                    runtimeState.SkillChoice1.Value,
                    runtimeState.SkillChoice1.Value,
                    InputType: "select",
                    Options: runtimeState.SkillChoice1.Options),
                new DesktopDialogField(
                    "newCharacterPrioritySkillChoice2",
                    "Skill Choice 2",
                    runtimeState.SkillChoice2.Value,
                    runtimeState.SkillChoice2.Value,
                    InputType: "select",
                    Options: runtimeState.SkillChoice2.Options),
                new DesktopDialogField(
                    "newCharacterPriorityWorkflowCanCommit",
                    "Can Commit",
                    "false",
                    "false",
                    InputType: "text",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterPriorityWorkflowState",
                    "Runtime State",
                    PriorityWorkflowDialogRuntimeStateSerializer.Serialize(runtimeState),
                    string.Empty,
                    InputType: "text",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
            ],
            Actions:
            [
                new DesktopDialogAction("cancel", "Cancel"),
                new DesktopDialogAction("complete_new_character_workflow", "OK", true)
            ]);

        List<DialogFieldInputChange> inputChanges = [];

        using var context = CreateContext();
        IRenderedComponent<DialogHost> cut = context.Render<DialogHost>(parameters => parameters
            .Add(component => component.Dialog, dialog)
            .Add(component => component.FieldInputRequested, (Action<DialogFieldInputChange>)(change => inputChanges.Add(change))));

        Assert.IsNotNull(cut.Find("[data-priority-workflow]"));
        Assert.AreEqual("12", cut.Find("select[data-field-id='newCharacterMetatype']").GetAttribute("size"));
        StringAssert.Contains(cut.Markup, "Total priority value: 10 / 10");
        StringAssert.Contains(cut.Markup, "Run Faster p. 65");
        StringAssert.Contains(cut.Markup, "Low-Light Vision");
        Assert.IsTrue(cut.Find("#dialogFooter .action-btn.primary").HasAttribute("disabled"));

        cut.Find("select[data-field-id='newCharacterPrioritySkillChoice1']").Change("Counterspelling");

        Assert.AreEqual(1, inputChanges.Count);
        Assert.AreEqual("newCharacterPrioritySkillChoice1", inputChanges[0].FieldId);
        Assert.AreEqual("Counterspelling", inputChanges[0].Value);
    }

    private static DesktopDialogField SelectField(
        string id,
        string label,
        string value,
        params string[] optionPairs)
    {
        Assert.AreEqual(0, optionPairs.Length % 2, $"Select field {id} requires value/label pairs.");
        List<DesktopDialogFieldOption> options = [];
        for (int index = 0; index < optionPairs.Length; index += 2)
        {
            options.Add(new DesktopDialogFieldOption(optionPairs[index], optionPairs[index + 1]));
        }

        return new DesktopDialogField(
            id,
            label,
            value,
            value,
            InputType: "select",
            Options: options);
    }

    [TestMethod]
    public void StatusStrip_announces_status_via_shared_live_region_semantics()
    {
        using var context = CreateContext();
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

    private static FakeCharacterOverviewPresenter RegisterPreviewShellServices(BunitContext context)
    {
        CharacterWorkspaceId workspaceId = new("preview-ws");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Preview Runner",
            Alias: "PRV",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5,
            HasSavedWorkspace: true);
        WorkspaceSessionState session = new(
            ActiveWorkspaceId: workspaceId,
            OpenWorkspaces: [openWorkspace],
            RecentWorkspaceIds: [workspaceId]);
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = session,
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId
        };

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);
        ShellWorkspaceState shellWorkspace = new(
            Id: workspaceId,
            Name: openWorkspace.Name,
            Alias: openWorkspace.Alias,
            LastOpenedUtc: openWorkspace.LastOpenedUtc,
            RulesetId: openWorkspace.RulesetId);
        ShellState shellState = ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [shellWorkspace],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        };

        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(overviewState);

        context.Services.AddSingleton<ICharacterOverviewPresenter>(presenter);
        context.Services.AddSingleton<IShellPresenter>(new StaticShellPresenter(shellState));
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault());
        context.Services.AddSingleton<IRulesetPlugin, Sr5RulesetPlugin>();
        context.Services.AddSingleton<IRulesetPluginRegistry, RulesetPluginRegistry>();
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
        return presenter;
    }

    private sealed class StaticShellPresenter : IShellPresenter
    {
        public StaticShellPresenter(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task ToggleMenuAsync(string menuId, CancellationToken ct) => Task.CompletedTask;

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
