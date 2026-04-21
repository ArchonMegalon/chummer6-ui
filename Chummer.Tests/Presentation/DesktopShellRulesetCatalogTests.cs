#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Layout;
using Chummer.Contracts.AI;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopShellRulesetCatalogTests
{
    [TestMethod]
    public void Blazor_shell_root_route_is_owned_only_by_the_shell_anchor_page()
    {
        string homePath = SourcePath("Chummer.Blazor", "Components", "Pages", "Home.razor");
        string showcasePath = SourcePath("Chummer.Blazor", "Components", "Pages", "Showcase.razor");
        string legacyPath = SourcePath("Chummer.Blazor", "Pages", "Index.razor");

        string homeText = File.ReadAllText(homePath);
        string showcaseText = File.ReadAllText(showcasePath);
        string legacyText = File.ReadAllText(legacyPath);

        StringAssert.Contains(homeText, "@page \"/\"");
        StringAssert.Contains(homeText, "Desktop shell route anchor");
        StringAssert.Contains(showcaseText, "@page \"/showcase\"");
        Assert.IsFalse(legacyText.Contains("@page \"/\"", StringComparison.Ordinal));
        Assert.IsFalse(legacyText.Contains("@page \"/blazor\"", StringComparison.Ordinal));
        StringAssert.Contains(legacyText, "@page \"/legacy-console\"");
    }

    [DataTestMethod]
    [DataRow(RulesetDefaults.Sr4, "SR4 Characters", "Import SR4 Oracle File", "SR4 Preview Result", "SR4 Preview Commands")]
    [DataRow(RulesetDefaults.Sr5, "SR5 Characters", "Import SR5 Character File", "SR5 Editor Result", "SR5 Editor Commands")]
    [DataRow(RulesetDefaults.Sr6, "SR6 Characters", "Import SR6 Character File", "SR6 Preview Result", "SR6 Preview Commands")]
    public void DesktopShell_renders_ruleset_specific_flagship_posture_for_each_supported_lane(
        string rulesetId,
        string expectedDossiers,
        string expectedImportHeading,
        string expectedResultHeading,
        string expectedCommandHeading)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new($"ws-{rulesetId}");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: $"{rulesetId.ToUpperInvariant()} Runner",
            Alias: rulesetId.ToUpperInvariant(),
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: rulesetId);
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace], [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-info",
            IsBusy = false
        };
        ShellState shellState = CreateShellState(
            workspaceId,
            openWorkspace,
            rulesetId,
            runtimeTitle: $"{rulesetId.ToUpperInvariant()} Core",
            runtimeFingerprint: $"{rulesetId}-runtime-fp-001");

        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            FakeWorkbenchCoachApiClient.CreateDefault($"{rulesetId}-runtime-fp-001"),
            new CatalogOnlyRulesetPlugin(rulesetId));

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsFalse(cut.Markup.Contains("data-testid=\"desktop-flagship-marquee\"", StringComparison.Ordinal));
            Assert.AreEqual(0, cut.FindAll(".workbench-summary-copy").Count, "Desktop shell must keep the header tab-only in the compact single-runner posture.");
            Assert.AreEqual(0, cut.FindAll(".workbench-runtime-summary").Count, "Desktop shell must not burn header width on runtime copy in the compact single-runner posture.");
            Assert.IsFalse(cut.Markup.Contains(expectedDossiers, StringComparison.Ordinal), "Single-runner posture must not surface workspace dossiers on first paint.");
            StringAssert.Contains(cut.Markup, expectedImportHeading);
            StringAssert.Contains(cut.Markup, expectedResultHeading);
            StringAssert.Contains(cut.Markup, expectedCommandHeading);
            StringAssert.Contains(cut.Find("#complianceState").TextContent, "Ruleset:");
            StringAssert.Contains(cut.Find("#complianceState").TextContent, RulesetUiDirectiveCatalog.Resolve(rulesetId).DisplayName);
            StringAssert.Contains(cut.Find("#complianceState").TextContent, RulesetUiDirectiveCatalog.Resolve(rulesetId).FileExtension);
            StringAssert.Contains(cut.Find("#complianceState").TextContent, "Workflows: 1 defs / 1 surfaces");
            Assert.IsFalse(cut.Find("#complianceState").TextContent.Contains("preview/oracle-first", StringComparison.Ordinal));
            Assert.IsFalse(cut.Find("#complianceState").TextContent.Contains("primary/workbench", StringComparison.Ordinal));
            Assert.IsFalse(cut.Find("#complianceState").TextContent.Contains("beta/edge-first", StringComparison.Ordinal));
            Assert.IsFalse(cut.Find("#complianceState").TextContent.Contains("flagship", StringComparison.OrdinalIgnoreCase));
        });
    }

    [TestMethod]
    public void DesktopShell_uses_active_ruleset_plugin_catalogs_for_actions_and_workflow_surfaces()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-sr6");
        CharacterWorkspaceId secondaryWorkspaceId = new("ws-sr6-secondary");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "SR6 Runner",
            Alias: "SR6",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: "sr6");
        OpenWorkspaceState secondaryWorkspace = new(
            Id: secondaryWorkspaceId,
            Name: "SR6 Backup",
            Alias: "SR6B",
            LastOpenedUtc: DateTimeOffset.UtcNow.AddMinutes(-10),
            RulesetId: "sr6");

        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace, secondaryWorkspace], [workspaceId, secondaryWorkspaceId]),
            OpenWorkspaces = [openWorkspace, secondaryWorkspace],
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-info",
            IsBusy = false
        };

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, "sr6");
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, "sr6");
        WorkflowDefinition workflowDefinition = new(
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Title: "Career Workbench",
            SurfaceIds: ["workflow.surface.sr6"],
            RequiresOpenWorkspace: true);
        WorkflowSurfaceDefinition workflowSurface = new(
            SurfaceId: "workflow.surface.sr6",
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Kind: WorkflowSurfaceKinds.Workbench,
            RegionId: ShellRegionIds.SectionPane,
            LayoutToken: WorkflowLayoutTokens.CareerWorkbench,
            ActionIds: ["sr6.action.matrix"]);
        ShellWorkspaceState shellWorkspace = new(
            Id: workspaceId,
            Name: openWorkspace.Name,
            Alias: openWorkspace.Alias,
            LastOpenedUtc: openWorkspace.LastOpenedUtc,
            RulesetId: "sr6");
        ShellWorkspaceState secondaryShellWorkspace = new(
            Id: secondaryWorkspaceId,
            Name: secondaryWorkspace.Name,
            Alias: secondaryWorkspace.Alias,
            LastOpenedUtc: secondaryWorkspace.LastOpenedUtc,
            RulesetId: "sr6");
        ShellState shellState = ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [shellWorkspace, secondaryShellWorkspace],
            ActiveRulesetId = "sr6",
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id,
            WorkflowDefinitions = [workflowDefinition],
            WorkflowSurfaces = [workflowSurface],
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr6.core",
                Title: "SR6 Core",
                RulesetId: "sr6",
                RuntimeFingerprint: "sr6-runtime-fp-001",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0)
        };

        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            FakeWorkbenchCoachApiClient.CreateDefault("sr6-runtime-fp-001"),
            new Sr6CatalogPlugin());

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            IReadOnlyList<AngleSharp.Dom.IElement> actionButtons = cut.FindAll(".section-actions .action-button");
            IReadOnlyList<AngleSharp.Dom.IElement> workflowButtons = cut.FindAll(".controls .mini-btn");

            Assert.HasCount(1, actionButtons);
            Assert.HasCount(1, workflowButtons);
            StringAssert.Contains(actionButtons[0].TextContent, "SR6 Matrix Action");
            Assert.AreEqual("workflow.surface.sr6", workflowButtons[0].GetAttribute("data-workflow-surface"));
            StringAssert.Contains(workflowButtons[0].TextContent, "SR6 Matrix Action");
            Assert.AreEqual(0, cut.FindAll(".workbench-runtime-summary").Count);
            StringAssert.Contains(cut.Find("#complianceState").TextContent, "Ruleset: Shadowrun 6");
            StringAssert.Contains(cut.Find("#complianceState").TextContent, ".chum6");
            StringAssert.Contains(cut.Find("#complianceState").TextContent, "Workflows: 1 defs / 1 surfaces");
            Assert.IsFalse(cut.Markup.Contains("data-testid=\"desktop-flagship-marquee\"", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Starter and beta desk", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Shadowrun 6 guided starter cockpit", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("SR6 home cockpit foregrounds starter kits", StringComparison.Ordinal));
            StringAssert.Contains(cut.Markup, "SR6 Characters");
            StringAssert.Contains(cut.Markup, "SR6 Preview Tabs");
        });
    }

    [TestMethod]
    public void DesktopShell_summary_header_stays_tab_only_without_runtime_copy_or_inline_inspector_button()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        StaticOverviewPresenter presenter = new(CharacterOverviewState.Empty);
        ShellState shellState = ShellState.Empty with
        {
            ActiveRulesetId = "sr6",
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr6.core",
                Title: "SR6 Core",
                RulesetId: "sr6",
                RuntimeFingerprint: "sr6-runtime-fp-001",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0)
        };

        context.Services.AddSingleton<ICharacterOverviewPresenter>(presenter);
        context.Services.AddSingleton<IShellPresenter>(new StaticShellPresenter(shellState));
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault("sr6-runtime-fp-001"));
        IRulesetPlugin sr5Plugin = new Sr5RulesetPlugin();
        IRulesetPlugin sr6Plugin = new Sr6CatalogPlugin();
        context.Services.AddSingleton<IRulesetPlugin>(sr5Plugin);
        context.Services.AddSingleton<IRulesetPlugin>(sr6Plugin);
        context.Services.AddSingleton<IRulesetPluginRegistry>(new RulesetPluginRegistry([sr5Plugin, sr6Plugin]));
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(0, cut.FindAll("#summaryRuntimeInspect").Count);
            Assert.AreEqual(0, cut.FindAll(".workbench-runtime-summary").Count);
        });

        Assert.IsNull(presenter.ExecutedCommandId);
    }

    [TestMethod]
    public void DesktopShell_keeps_default_center_pane_free_of_metadata_and_payload_debug_chrome()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-sr5");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "SR5 Runner",
            Alias: "SR5",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5);
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace], [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId,
            ActiveSectionId = "skills",
            ActiveSectionJson = "{\"skills\":1}",
            ActiveSectionRows = [new SectionRowState("skills[0].name", "Pistols")]
        };
        ShellState shellState = CreateShellState(
            workspaceId,
            openWorkspace,
            RulesetDefaults.Sr5,
            runtimeTitle: "SR5 Core",
            runtimeFingerprint: "sr5-runtime-fp-001");

        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            FakeWorkbenchCoachApiClient.CreateDefault("sr5-runtime-fp-001"),
            new CatalogOnlyRulesetPlugin(RulesetDefaults.Sr5));

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Pistols");
            Assert.IsFalse(cut.Markup.Contains("panel metadata", StringComparison.Ordinal), "The default desktop shell must not mount the metadata slab above the workbench.");
            Assert.IsFalse(cut.Markup.Contains("section-payload", StringComparison.Ordinal), "The default desktop shell must not surface raw section payload dumps.");
            Assert.IsFalse(cut.Markup.Contains("{\"skills\":1}", StringComparison.Ordinal), "The default desktop shell must keep raw section JSON out of the visible workbench.");
        });
    }

    [TestMethod]
    public void DesktopShell_hides_workspace_left_pane_for_single_runner_posture()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-sr5");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "SR5 Runner",
            Alias: "SR5",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5);
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace], [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-info",
            IsBusy = false
        };
        ShellState shellState = CreateShellState(
            workspaceId,
            openWorkspace,
            RulesetDefaults.Sr5,
            runtimeTitle: "SR5 Core",
            runtimeFingerprint: "sr5-runtime-fp-compact");

        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            FakeWorkbenchCoachApiClient.CreateDefault("sr5-runtime-fp-compact"),
            new CatalogOnlyRulesetPlugin(RulesetDefaults.Sr5));

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(0, cut.FindAll(".left-pane").Count, "Single-runner posture must not render the workspace left pane.");
            Assert.AreEqual(0, cut.FindAll(".mdi-strip").Count, "Single-runner posture must not render MDI workspace chrome.");
            StringAssert.Contains(cut.Find(".workspace-layout").ClassName, "workspace-layout--without-left-pane");
            Assert.IsFalse(cut.Markup.Contains("SR5 Characters", StringComparison.Ordinal), "Single-runner posture must not spend first-paint copy on workspace headings.");
        });
    }

    [TestMethod]
    public void DesktopShell_restores_workspace_left_pane_for_multi_workspace_session()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId activeWorkspaceId = new("ws-sr5-primary");
        CharacterWorkspaceId secondaryWorkspaceId = new("ws-sr5-secondary");
        OpenWorkspaceState activeWorkspace = new(
            Id: activeWorkspaceId,
            Name: "SR5 Runner",
            Alias: "SR5",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5);
        OpenWorkspaceState secondaryWorkspace = new(
            Id: secondaryWorkspaceId,
            Name: "Backup Runner",
            Alias: "BR",
            LastOpenedUtc: DateTimeOffset.UtcNow.AddMinutes(-5),
            RulesetId: RulesetDefaults.Sr5);
        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(activeWorkspaceId, [activeWorkspace, secondaryWorkspace], [activeWorkspaceId, secondaryWorkspaceId]),
            OpenWorkspaces = [activeWorkspace, secondaryWorkspace],
            WorkspaceId = activeWorkspaceId,
            ActiveTabId = "tab-info",
            IsBusy = false
        };
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);
        WorkflowDefinition workflowDefinition = new(
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Title: "Career Workbench",
            SurfaceIds: ["workflow.surface.sr5"],
            RequiresOpenWorkspace: true);
        WorkflowSurfaceDefinition workflowSurface = new(
            SurfaceId: "workflow.surface.sr5",
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Kind: WorkflowSurfaceKinds.Workbench,
            RegionId: ShellRegionIds.SectionPane,
            LayoutToken: WorkflowLayoutTokens.CareerWorkbench,
            ActionIds: ["sr5.action.matrix"]);
        ShellState shellState = ShellState.Empty with
        {
            ActiveWorkspaceId = activeWorkspaceId,
            OpenWorkspaces =
            [
                new ShellWorkspaceState(activeWorkspaceId, activeWorkspace.Name, activeWorkspace.Alias, activeWorkspace.LastOpenedUtc, RulesetDefaults.Sr5),
                new ShellWorkspaceState(secondaryWorkspaceId, secondaryWorkspace.Name, secondaryWorkspace.Alias, secondaryWorkspace.LastOpenedUtc, RulesetDefaults.Sr5)
            ],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id,
            WorkflowDefinitions = [workflowDefinition],
            WorkflowSurfaces = [workflowSurface],
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr5.core",
                Title: "SR5 Core",
                RulesetId: RulesetDefaults.Sr5,
                RuntimeFingerprint: "sr5-runtime-fp-multi",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0)
        };

        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            FakeWorkbenchCoachApiClient.CreateDefault("sr5-runtime-fp-multi"),
            new CatalogOnlyRulesetPlugin(RulesetDefaults.Sr5));

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, cut.FindAll(".left-pane").Count, "Multi-workspace posture must restore the workspace left pane.");
            Assert.AreEqual(1, cut.FindAll(".mdi-strip").Count, "Multi-workspace posture must restore the MDI workspace strip.");
            StringAssert.Contains(cut.Find(".workspace-layout").ClassName, "workspace-layout--with-left-pane");
            StringAssert.Contains(cut.Markup, "SR5 Characters");
        });
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

    [TestMethod]
    public void DesktopShell_renders_coach_sidecar_for_active_runtime()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-sr6");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "SR6 Runner",
            Alias: "SR6",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: "sr6");

        CharacterOverviewState overviewState = CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(workspaceId, [openWorkspace], [workspaceId]),
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-info",
            IsBusy = false
        };

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, "sr6");
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, "sr6");
        ShellWorkspaceState shellWorkspace = new(
            Id: workspaceId,
            Name: openWorkspace.Name,
            Alias: openWorkspace.Alias,
            LastOpenedUtc: openWorkspace.LastOpenedUtc,
            RulesetId: "sr6");
        ShellState shellState = ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [shellWorkspace],
            ActiveRulesetId = "sr6",
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id,
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr6.core",
                Title: "SR6 Core",
                RulesetId: "sr6",
                RuntimeFingerprint: "sr6-runtime-fp-001",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0)
        };

        FakeWorkbenchCoachApiClient coachClient = FakeWorkbenchCoachApiClient.CreateDefault("sr6-runtime-fp-001");
        RegisterDesktopShellServices(
            context,
            overviewState,
            shellState,
            coachClient,
            new Sr6CatalogPlugin());

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();
        cut.Find("[data-testid=\"refresh-workbench-coach-sidecar\"]").Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Coach Sidecar");
            StringAssert.Contains(cut.Markup, "Grounded Guidance");
            StringAssert.Contains(cut.Markup, "Recent Coach Guidance");
            StringAssert.Contains(cut.Markup, "AI Magicx");
            StringAssert.Contains(cut.Markup, "Transport: ready · base yes · model yes · keys primary 1 / fallback 0 · route coach · binding primary / slot 0");
            StringAssert.Contains(cut.Markup, "cache hit");
            StringAssert.Contains(cut.Markup, "Keep the active runtime pinned before previewing Karma spend.");
            StringAssert.Contains(cut.Markup, "Signal's clean. Keep the deck on the grounded lane.");
            StringAssert.Contains(cut.Markup, "Budget snapshot: 18 / 400 chummer-ai-units");
            StringAssert.Contains(cut.Markup, "Structured summary: Preview Karma against the pinned runtime before you commit advancement changes.");
            StringAssert.Contains(cut.Markup, "Recommendations: 1 · Preview Karma spend");
            StringAssert.Contains(cut.Markup, "Evidence: 1 · Pinned runtime");
            StringAssert.Contains(cut.Markup, "Risks: 1 · Preview first");
            StringAssert.Contains(cut.Markup, "Sources: 1 sources / 1 action drafts");
            StringAssert.Contains(cut.Markup, "382 left / 5 burst");
            StringAssert.Contains(cut.Markup, "data-testid=\"open-workbench-coach-sidecar\"");
            StringAssert.Contains(cut.Markup, "data-testid=\"workbench-coach-provider-transport\"");
            StringAssert.Contains(cut.Markup, "data-testid=\"open-workbench-coach-thread\"");
            StringAssert.Contains(cut.Markup, "/coach/?routeType=coach&amp;conversationId=conv.workbench-coach-1&amp;runtimeFingerprint=sr6-runtime-fp-001&amp;workspaceId=ws-sr6");
            StringAssert.Contains(cut.Markup, "/coach/?routeType=coach&amp;runtimeFingerprint=sr6-runtime-fp-001&amp;workspaceId=ws-sr6");
        });

        Assert.AreEqual(1, coachClient.StatusCalls);
        Assert.AreEqual(1, coachClient.ProviderHealthCalls);
        Assert.AreEqual(1, coachClient.AuditCalls);
        Assert.AreEqual(AiRouteTypes.Coach, coachClient.LastAuditRouteType);
        Assert.AreEqual("sr6-runtime-fp-001", coachClient.LastRuntimeFingerprint);
        Assert.AreEqual(3, coachClient.LastMaxCount);
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        CharacterOverviewState overviewState,
        ShellState shellState,
        IWorkbenchCoachApiClient coachClient,
        IRulesetPlugin activeRulesetPlugin)
    {
        context.Services.AddSingleton<ICharacterOverviewPresenter>(new StaticOverviewPresenter(overviewState));
        context.Services.AddSingleton<IShellPresenter>(new StaticShellPresenter(shellState));
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton(coachClient);
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(coachClient);
        IRulesetPlugin sr5Plugin = new Sr5RulesetPlugin();
        context.Services.AddSingleton<IRulesetPlugin>(sr5Plugin);
        context.Services.AddSingleton<IRulesetPlugin>(activeRulesetPlugin);
        context.Services.AddSingleton<IRulesetPluginRegistry>(new RulesetPluginRegistry([sr5Plugin, activeRulesetPlugin]));
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
    }

    private static ShellState CreateShellState(
        CharacterWorkspaceId workspaceId,
        OpenWorkspaceState openWorkspace,
        string rulesetId,
        string runtimeTitle,
        string runtimeFingerprint)
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, rulesetId);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, rulesetId);
        WorkflowDefinition workflowDefinition = new(
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Title: "Career Workbench",
            SurfaceIds: [$"workflow.surface.{rulesetId}"],
            RequiresOpenWorkspace: true);
        WorkflowSurfaceDefinition workflowSurface = new(
            SurfaceId: $"workflow.surface.{rulesetId}",
            WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
            Kind: WorkflowSurfaceKinds.Workbench,
            RegionId: ShellRegionIds.SectionPane,
            LayoutToken: WorkflowLayoutTokens.CareerWorkbench,
            ActionIds: [$"{rulesetId}.action.matrix"]);
        ShellWorkspaceState shellWorkspace = new(
            Id: workspaceId,
            Name: openWorkspace.Name,
            Alias: openWorkspace.Alias,
            LastOpenedUtc: openWorkspace.LastOpenedUtc,
            RulesetId: rulesetId);

        return ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [shellWorkspace],
            ActiveRulesetId = rulesetId,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id,
            WorkflowDefinitions = [workflowDefinition],
            WorkflowSurfaces = [workflowSurface],
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: $"official.{rulesetId}.core",
                Title: runtimeTitle,
                RulesetId: rulesetId,
                RuntimeFingerprint: runtimeFingerprint,
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0)
        };
    }

    private sealed class StaticOverviewPresenter : ICharacterOverviewPresenter
    {
        public StaticOverviewPresenter(CharacterOverviewState state)
        {
            State = state;
        }

        public CharacterOverviewState State { get; private set; }
        public string? ExecutedCommandId { get; private set; }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;
        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandId = commandId;
            return Task.CompletedTask;
        }
        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;
        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
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

    private sealed class Sr6CatalogPlugin : IRulesetPlugin
    {
        public RulesetId Id { get; } = new("sr6");
        public string DisplayName => "SR6 test plugin";
        public IRulesetSerializer Serializer { get; } = new Sr6Serializer();
        public IRulesetShellDefinitionProvider ShellDefinitions { get; } = new Sr6ShellDefinitions();
        public IRulesetCatalogProvider Catalogs { get; } = new Sr6Catalogs();
        public IRulesetCapabilityDescriptorProvider CapabilityDescriptors { get; } = new Sr6CapabilityDescriptorProvider();
        public IRulesetCapabilityHost Capabilities { get; } = new Sr6CapabilityHost();
        public IRulesetRuleHost Rules { get; } = new RulesetRuleHostCapabilityAdapter(new Sr6CapabilityHost());
        public IRulesetScriptHost Scripts { get; } = new RulesetScriptHostCapabilityAdapter(new Sr6CapabilityHost());
    }

    private sealed class Sr6Serializer : IRulesetSerializer
    {
        public RulesetId RulesetId { get; } = new("sr6");
        public int SchemaVersion => 1;

        public WorkspacePayloadEnvelope Wrap(string payloadKind, string payload)
        {
            return new WorkspacePayloadEnvelope("sr6", SchemaVersion, payloadKind, payload);
        }
    }

    private sealed class Sr6ShellDefinitions : IRulesetShellDefinitionProvider
    {
        public IReadOnlyList<AppCommandDefinition> GetCommands() =>
        [
            new AppCommandDefinition("file", "menu.file", "menu", false, true, "sr6")
        ];

        public IReadOnlyList<NavigationTabDefinition> GetNavigationTabs() =>
        [
            new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, "sr6")
        ];
    }

    private sealed class Sr6Catalogs : IRulesetCatalogProvider
    {
        public IReadOnlyList<WorkspaceSurfaceActionDefinition> GetWorkspaceActions() =>
        [
            new WorkspaceSurfaceActionDefinition(
                Id: "sr6.action.matrix",
                Label: "SR6 Matrix Action",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Section,
                TargetId: "profile",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: "sr6")
        ];
    }

    private sealed class Sr6CapabilityHost : IRulesetCapabilityHost
    {
        public ValueTask<RulesetCapabilityInvocationResult> InvokeAsync(RulesetCapabilityInvocationRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RulesetCapabilityInvocationResult(
                true,
                new RulesetCapabilityValue(
                    RulesetCapabilityValueKinds.Object,
                    Properties: request.Arguments.ToDictionary(
                        static argument => argument.Name,
                        static argument => argument.Value,
                        StringComparer.Ordinal)),
                []));
        }
    }

    private sealed class Sr6CapabilityDescriptorProvider : IRulesetCapabilityDescriptorProvider
    {
        public IReadOnlyList<RulesetCapabilityDescriptor> GetCapabilityDescriptors() => [];
    }

    private sealed class CatalogOnlyRulesetPlugin : IRulesetPlugin
    {
        public CatalogOnlyRulesetPlugin(string rulesetId)
        {
            string normalizedRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
            Id = new RulesetId(normalizedRulesetId);
            DisplayName = normalizedRulesetId.ToUpperInvariant() + " test plugin";
            Serializer = new TestRulesetSerializer(normalizedRulesetId);
            ShellDefinitions = new TestRulesetShellDefinitions(normalizedRulesetId);
            Catalogs = new TestRulesetCatalogs(normalizedRulesetId);
            CapabilityDescriptors = new TestRulesetCapabilityDescriptorProvider();
            Capabilities = new TestRulesetCapabilityHost();
            Rules = new RulesetRuleHostCapabilityAdapter(Capabilities);
            Scripts = new RulesetScriptHostCapabilityAdapter(Capabilities);
        }

        public RulesetId Id { get; }
        public string DisplayName { get; }
        public IRulesetSerializer Serializer { get; }
        public IRulesetShellDefinitionProvider ShellDefinitions { get; }
        public IRulesetCatalogProvider Catalogs { get; }
        public IRulesetCapabilityDescriptorProvider CapabilityDescriptors { get; }
        public IRulesetCapabilityHost Capabilities { get; }
        public IRulesetRuleHost Rules { get; }
        public IRulesetScriptHost Scripts { get; }
    }

    private sealed class TestRulesetSerializer(string rulesetId) : IRulesetSerializer
    {
        public RulesetId RulesetId { get; } = new(rulesetId);
        public int SchemaVersion => 1;

        public WorkspacePayloadEnvelope Wrap(string payloadKind, string payload)
            => new(rulesetId, SchemaVersion, payloadKind, payload);
    }

    private sealed class TestRulesetShellDefinitions(string rulesetId) : IRulesetShellDefinitionProvider
    {
        public IReadOnlyList<AppCommandDefinition> GetCommands() =>
        [
            new AppCommandDefinition("file", "menu.file", "menu", false, true, rulesetId)
        ];

        public IReadOnlyList<NavigationTabDefinition> GetNavigationTabs() =>
        [
            new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, rulesetId)
        ];
    }

    private sealed class TestRulesetCatalogs(string rulesetId) : IRulesetCatalogProvider
    {
        public IReadOnlyList<WorkspaceSurfaceActionDefinition> GetWorkspaceActions() =>
        [
            new WorkspaceSurfaceActionDefinition(
                Id: $"{rulesetId}.action.matrix",
                Label: $"{rulesetId.ToUpperInvariant()} Matrix Action",
                TabId: "tab-info",
                Kind: WorkspaceSurfaceActionKind.Section,
                TargetId: "profile",
                RequiresOpenCharacter: true,
                EnabledByDefault: true,
                RulesetId: rulesetId)
        ];
    }

    private sealed class TestRulesetCapabilityHost : IRulesetCapabilityHost
    {
        public ValueTask<RulesetCapabilityInvocationResult> InvokeAsync(RulesetCapabilityInvocationRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RulesetCapabilityInvocationResult(
                true,
                new RulesetCapabilityValue(
                    RulesetCapabilityValueKinds.Object,
                    Properties: request.Arguments.ToDictionary(
                        static argument => argument.Name,
                        static argument => argument.Value,
                        StringComparer.Ordinal)),
                []));
        }
    }

    private sealed class TestRulesetCapabilityDescriptorProvider : IRulesetCapabilityDescriptorProvider
    {
        public IReadOnlyList<RulesetCapabilityDescriptor> GetCapabilityDescriptors() => [];
    }
}
