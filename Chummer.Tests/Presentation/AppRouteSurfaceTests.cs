#nullable enable annotations

using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Application.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Chummer.Infrastructure.Owners;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AppRouteSurfaceTests
{
    [TestMethod]
    public void App_route_renders_character_roster_without_preview_scaffolding()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Character Roster");
        StringAssert.Contains(cut.Markup, "Your runners will appear here.");
        StringAssert.Contains(cut.Markup, "Kestrel");
        StringAssert.Contains(cut.Markup, "Street samurai");
        StringAssert.Contains(cut.Markup, "Rook");
        StringAssert.Contains(cut.Markup, "Decker");
        Assert.IsTrue(cut.Markup.Contains("chummer-online-app-shell", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find(".browser-app-roster"));
        Assert.IsNotNull(cut.Find(".browser-app-roster-tree"));
        Assert.IsNotNull(cut.Find(".browser-app-runner-panel"));
        Assert.IsNotNull(cut.Find(".browser-app-example-panel"));
        Assert.AreEqual(6, cut.FindAll("[data-app-menu-root]").Count);
        Assert.IsNotNull(cut.Find("[data-app-menu-root='file']"));
        Assert.IsNotNull(cut.Find("[data-app-menu-root='runner']"));
        Assert.IsNotNull(cut.Find("[data-app-menu-item='new-runner']"));
        Assert.IsNotNull(cut.Find("[data-app-menu-item='character-roster']"));
        Assert.AreEqual(7, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.IsNotNull(cut.Find("[data-app-toolstrip='classic']"));
        Assert.IsNotNull(cut.Find("[data-app-statusbar='true']"));
        Assert.IsNotNull(cut.Find("[data-drop-target='runner-folder']"));
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-create\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-gear\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-contacts\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-rules\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=save_character_as\"");
        Assert.IsFalse(cut.Markup.Contains("fixture=kestrel-samurai", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("fixture=rook-decker", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("fixture=mara-face", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("fixture=ash-mage", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("data-preview-proof-card=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-banner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-promoted-app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-frame--app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("desktop-shell", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-chummer-shell", StringComparison.Ordinal));
    }

    [TestMethod]
    public void App_route_character_roster_dash_alias_uses_roster_surface_without_startup_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=character-roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "data-route-surface=\"public-app\"");
        Assert.IsNotNull(cut.Find("[data-app-menu-root='runner']"));
        Assert.IsNotNull(cut.Find("[data-app-toolstrip='classic']"));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-frame--app", StringComparison.Ordinal));
        Assert.AreEqual("character-roster", cut.Find("section.browser-app-roster")?.GetAttribute("data-command"));
        Assert.AreEqual("Character Roster", cut.Find("h1")?.TextContent);
    }

    [TestMethod]
    public void App_route_new_character_opens_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=new_character");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("new_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("new-character", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("new_character", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "New runner");
        StringAssert.Contains(cut.Markup, "Build Lab shell");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains("Your runners will appear here.", StringComparison.Ordinal),
            "Explicit app-route startup commands must not silently fall back to the generic roster body.");
    }

    [TestMethod]
    public void App_route_origin_dossier_opens_story_first_shared_shell_without_roster_body()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=new_character_origin");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("new_character_origin", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("origin-dossier", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("new-character-origin", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("new_character_origin", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Origin Dossier", cut.Find(".browser-app-roster-head h1")?.TextContent);
        StringAssert.Contains(cut.Markup, "Start with the story-first dossier path, then move into standard character creation when the origin is ready.");
        StringAssert.Contains(cut.Markup, "Origin Dossier shell");
        StringAssert.Contains(cut.Markup, "Chummer Online is opening directly into Origin Dossier with the story-first path ready.");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
        Assert.IsFalse(
            cut.Markup.Contains("Your runners will appear here.", StringComparison.Ordinal),
            "Origin Dossier app-route startup should not fall back to the generic roster surface.");
    }

    [TestMethod]
    public void App_route_open_dossier_opens_shared_import_shell_without_roster_body()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=open_character");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("open_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("open-dossier", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("open-character", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("open_character", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Open Dossier", cut.Find(".browser-app-roster-head h1")?.TextContent);
        StringAssert.Contains(cut.Markup, "Open the shared import flow from Chummer Online instead of silently falling back to the roster.");
        StringAssert.Contains(cut.Markup, "Open Dossier shell");
        StringAssert.Contains(cut.Markup, "Chummer Online is opening directly into the shared import workflow so local dossiers can be selected from the app route.");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
        Assert.IsFalse(
            cut.Markup.Contains("Your runners will appear here.", StringComparison.Ordinal),
            "Open Dossier app-route startup should not fall back to the generic roster surface.");
    }

    [DataTestMethod]
    [DataRow(
        "open_for_printing",
        "print",
        "print-view",
        "open-for-printing",
        "Open for Printing",
        "Print shell",
        "Open the shared print workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the print continuation path from the app route.")]
    [DataRow(
        "print_character",
        "print",
        "print-view",
        "print-character",
        "Print Dossier",
        "Print shell",
        "Open the shared print workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the print continuation path from the app route.")]
    [DataRow(
        "open_for_export",
        "export",
        "download-package",
        "open-for-export",
        "Open for Export",
        "Export shell",
        "Open the shared export workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the export continuation path from the app route.")]
    [DataRow(
        "export_character",
        "export",
        "download-package",
        "export-character",
        "Export Dossier",
        "Export shell",
        "Open the shared export workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the export continuation path from the app route.")]
    [DataRow(
        "save_character",
        "save",
        "local-dossier",
        "save-character",
        "Save Runner",
        "Save Runner shell",
        "Run the shared runner-save workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the shared runner-save continuation from the app route.")]
    [DataRow(
        "save_character_as",
        "save",
        "local-dossier",
        "save-character-as",
        "Save Runner As",
        "Save Runner shell",
        "Run the shared runner-save workflow while keeping browser and desktop shell semantics aligned.",
        "Chummer Online is opening directly into the shared runner-save continuation from the app route.")]
    public void App_route_output_family_commands_open_shared_shell_without_roster_body(
        string commandId,
        string expectedWorkflow,
        string expectedOutputTarget,
        string expectedCommandToken,
        string expectedTitle,
        string expectedFrameTitle,
        string expectedSummary,
        string expectedRouteOpenSummary)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/app?command={commandId}");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(commandId, presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("requested", appSurface.GetAttribute("data-output-state"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedCommandToken, appSurface.GetAttribute("data-command"));
            Assert.AreEqual(commandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedTitle, cut.Find(".browser-app-roster-head h1")?.TextContent);
        StringAssert.Contains(cut.Markup, expectedSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteOpenSummary);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
        Assert.IsFalse(
            cut.Markup.Contains("Your runners will appear here.", StringComparison.Ordinal),
            "Output-family app-route startup commands should not fall back to the generic roster surface.");
    }

    [DataTestMethod]
    [DataRow(
        "master_index",
        "master-index",
        "Master Index",
        "Open the shared rules and source reference index from Chummer Online without falling back to the roster.",
        "Master Index shell",
        "Chummer Online is opening directly into the shared Master Index so rules and source references stay on the app route.")]
    [DataRow(
        "global_settings",
        "global-settings",
        "Global Settings",
        "Open the shared global settings workflow from Chummer Online without falling back to the roster.",
        "Global Settings shell",
        "Chummer Online is opening directly into shared global settings so preferences can be adjusted from the app route.")]
    public void App_route_utility_commands_open_shared_shell_without_roster_body(
        string commandId,
        string expectedWorkflow,
        string expectedTitle,
        string expectedSummary,
        string expectedFrameTitle,
        string expectedRouteOpenSummary)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/app?command={commandId}");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(commandId, presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("idle", appSurface.GetAttribute("data-output-state"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-command"));
            Assert.AreEqual(commandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedTitle, cut.Find(".browser-app-roster-head h1")?.TextContent);
        StringAssert.Contains(cut.Markup, expectedSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteOpenSummary);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
        Assert.IsFalse(
            cut.Markup.Contains("Your runners will appear here.", StringComparison.Ordinal),
            "Utility app-route startup commands should not fall back to the generic roster surface.");
    }

    [TestMethod]
    public void App_route_workspace_payload_uses_workflow_context_instead_of_startup_placeholder_copy()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("tab-contacts", presenter.SelectedTabId);
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("contacts", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("preview-ws", appSurface.GetAttribute("data-workspace"));
            Assert.AreEqual("tab-contacts", appSurface.GetAttribute("data-tab"));
            Assert.AreEqual("contact-add", appSurface.GetAttribute("data-control"));
            Assert.AreEqual("add", appSurface.GetAttribute("data-dialog-action"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Contacts", cut.Find(".browser-app-roster-head h1")?.TextContent.Trim());
        Assert.AreEqual("Contacts", cut.Find(".browser-preview-frame-kicker")?.TextContent.Trim());
        Assert.AreEqual("Contacts", cut.Find("p[data-chummer-app-startup-command='none'] strong")?.TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Contacts shell");
        StringAssert.Contains(cut.Markup, "Open the requested runner context directly in the shared Chummer Online shell.");
        Assert.IsFalse(
            cut.Markup.Contains("No startup command selected", StringComparison.Ordinal),
            "Workspace continuity routes should surface the active workflow instead of placeholder startup-command copy.");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
    }

    [TestMethod]
    public void App_route_fixture_payload_imports_seed_fixture_rewrites_to_workspace_query_and_uses_workflow_context_copy()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RegisterDesktopShellServices(context, presenter, CreateDefaultShellState());

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?fixture=blue&tab=tab-create");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual(RulesetDefaults.Sr5, presenter.ImportedRulesetId);
            Assert.AreEqual("tab-create", presenter.SelectedTabId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("fixture-ws", appSurface.GetAttribute("data-workspace"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-fixture"));
            Assert.AreEqual("tab-create", appSurface.GetAttribute("data-tab"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
            StringAssert.EndsWith(navigation.Uri, "/app?workspace=fixture-ws&tab=tab-create");
        });

        Assert.AreEqual("Build Lab", cut.Find(".browser-app-roster-head h1")?.TextContent.Trim());
        Assert.AreEqual("Build Lab", cut.Find(".browser-preview-frame-kicker")?.TextContent.Trim());
        Assert.AreEqual("Build Lab", cut.Find("p[data-chummer-app-startup-command='none'] strong")?.TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Build Lab shell");
        StringAssert.Contains(cut.Markup, "Open the shared Build Lab from Chummer Online so creation and continuation stay inside the app.");
        StringAssert.Contains(cut.Markup, "Chummer Online is opening directly into Build Lab so new-runner and live continuation links stay on the app route.");
        Assert.IsFalse(
            cut.Markup.Contains("No startup command selected", StringComparison.Ordinal),
            "Seeded fixture continuity routes should surface the active workflow instead of placeholder startup-command copy.");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-menu-root]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-toolstrip-action]").Count);
        Assert.AreEqual(0, cut.FindAll("[data-app-statusbar='true']").Count);
    }

    [TestMethod]
    public void App_route_fixture_rules_payload_uses_rules_workflow_context_copy()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RegisterDesktopShellServices(context, presenter, CreateDefaultShellState());

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?fixture=blue&tab=tab-rules");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual(RulesetDefaults.Sr5, presenter.ImportedRulesetId);
            Assert.AreEqual("tab-rules", presenter.SelectedTabId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("rules", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("fixture-ws", appSurface.GetAttribute("data-workspace"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-fixture"));
            Assert.AreEqual("tab-rules", appSurface.GetAttribute("data-tab"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
            StringAssert.EndsWith(navigation.Uri, "/app?workspace=fixture-ws&tab=tab-rules");
        });

        Assert.AreEqual("Rules", cut.Find(".browser-app-roster-head h1")?.TextContent.Trim());
        Assert.AreEqual("Rules", cut.Find(".browser-preview-frame-kicker")?.TextContent.Trim());
        Assert.AreEqual("Rules", cut.Find("p[data-chummer-app-startup-command='none'] strong")?.TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Rules shell");
        StringAssert.Contains(cut.Markup, "Open the shared rules-facing lane from Chummer Online so source and rules context stay on the app route.");
        StringAssert.Contains(cut.Markup, "Chummer Online is opening directly into the rules lane so source and rules context stay on the app route.");
        Assert.IsFalse(
            cut.Markup.Contains("Dossier shell", StringComparison.Ordinal),
            "Rules continuity routes should render dedicated Rules workflow copy instead of the generic dossier shell.");
    }

    [TestMethod]
    public void Online_alias_route_renders_roster_surface_with_alias_metadata_and_canonical_links()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        var appSurface = cut.Find("section.browser-app-roster");
        Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
        Assert.AreEqual("online", appSurface.GetAttribute("data-route-segment"));
        Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
        Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
        Assert.AreEqual("character-roster", appSurface.GetAttribute("data-command"));
        Assert.AreEqual(0, cut.FindAll("[data-app-route-shared-shell='true']").Count);
        Assert.IsFalse(cut.Markup.Contains("browser-preview-frame--app-route", StringComparison.Ordinal));
        Assert.AreEqual("app?command=character_roster", cut.Find("[data-app-menu-item='character-roster']").GetAttribute("href"));
        Assert.AreEqual("app?command=new_character", cut.Find("[data-app-menu-item='new-runner']").GetAttribute("href"));
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;tab=tab-rules\"");
        Assert.IsFalse(cut.Markup.Contains("online?fixture=blue", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("fixture=kestrel-samurai", StringComparison.Ordinal));
        Assert.AreEqual("Character Roster", cut.Find(".browser-app-roster-head h1")?.TextContent.Trim());
    }

    [TestMethod]
    public void Online_alias_route_fixture_payload_rewrites_to_alias_workspace_query_and_keeps_outbound_links_canonical()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RegisterDesktopShellServices(context, presenter, CreateDefaultShellState());

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?fixture=blue&tab=tab-create");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual(RulesetDefaults.Sr5, presenter.ImportedRulesetId);
            Assert.AreEqual("tab-create", presenter.SelectedTabId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-segment"));
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("fixture-ws", appSurface.GetAttribute("data-workspace"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-fixture"));
            Assert.AreEqual("tab-create", appSurface.GetAttribute("data-tab"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-command"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
            StringAssert.EndsWith(navigation.Uri, "/online?workspace=fixture-ws&tab=tab-create");
        });

        Assert.AreEqual("Build Lab", cut.Find(".browser-app-roster-head h1")?.TextContent.Trim());
        Assert.AreEqual("Build Lab", cut.Find(".browser-preview-frame-kicker")?.TextContent.Trim());
        Assert.AreEqual("app?command=new_character", cut.Find(".browser-app-roster-actions a").GetAttribute("href"));
        StringAssert.Contains(cut.Markup, "Build Lab shell");
        Assert.IsFalse(
            cut.Markup.Contains("No startup command selected", StringComparison.Ordinal),
            "Online alias continuity routes should surface the active workflow instead of placeholder startup-command copy.");
    }

    [TestMethod]
    public void App_root_workbench_fallback_keeps_new_runner_command_visible_on_new_character_route()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&command=new_character");

        IRenderedComponent<App> cut = context.Render<App>();

        var fallback = cut.Find("[data-ssr-workbench-fallback='true']");
        Assert.AreEqual("new_character", fallback.GetAttribute("data-command"));
        Assert.AreEqual("tab-create", fallback.GetAttribute("data-tab"));
        Assert.AreEqual(
            "workbench?workspace=preview-ws&tab=tab-create&command=new_character",
            cut.Find("[data-app-menu-item='new-runner']").GetAttribute("href"));
        Assert.AreEqual(1, cut.FindAll("section.desktop-dialog").Count);
    }

    private static FakeCharacterOverviewPresenter RegisterDesktopShellServices(BunitContext context)
    {
        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(CreateDefaultOverviewState());
        RegisterDesktopShellServices(context, presenter, CreateDefaultShellState());
        return presenter;
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        ICharacterOverviewPresenter presenter,
        ShellState shellState)
    {
        var hostEnvironment = new TestHostEnvironment();
        context.Services.AddSingleton<ICharacterOverviewPresenter>(presenter);
        context.Services.AddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
            HostedBuildPrivacyLifecycleCapabilities.Instance);
        context.Services.AddSingleton(new HostedBuildOwnerInvalidationTokenService(
            Options.Create(new HostedBuildOwnerInvalidationTokenOptions
            {
                AllowEphemeral = "true"
            }),
            hostEnvironment));
        context.Services.AddSingleton<IHostEnvironment>(hostEnvironment);
        context.Services.AddSingleton<IWebHostEnvironment>(hostEnvironment);
        context.Services.AddSingleton<IOwnerContextAccessor, LocalOwnerContextAccessor>();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton<IShellPresenter>(new StaticShellPresenter(shellState));
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault());
        context.Services.AddSingleton<IRunnerIntelligenceCalculator, RunnerIntelligenceCalculator>();
        context.Services.AddSingleton<IRunnerIntelligenceScenarioCatalog, RunnerIntelligenceScenarioCatalog>();
        context.Services.AddSingleton<BlazorRunnerIntelligencePreviewService>();
        context.Services.AddSingleton<IRulesetPlugin, Sr5RulesetPlugin>();
        context.Services.AddSingleton<IRulesetPluginRegistry, RulesetPluginRegistry>();
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
    }

    private static CharacterOverviewState CreateDefaultOverviewState()
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

        return CharacterOverviewState.Empty with
        {
            Session = session,
            OpenWorkspaces = [openWorkspace],
            WorkspaceId = workspaceId
        };
    }

    private static CharacterOverviewState CreateStartupOverviewState()
        => CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: [])
        };

    private static ShellState CreateDefaultShellState()
    {
        CharacterWorkspaceId workspaceId = new("preview-ws");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Preview Runner",
            Alias: "PRV",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5,
            HasSavedWorkspace: true);

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        IReadOnlyList<NavigationTabDefinition> navigationTabs =
            new CatalogOnlyRulesetShellCatalogResolver()
                .ResolveNavigationTabs(RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = navigationTabs
            .Single(tab => string.Equals(tab.Id, "tab-info", StringComparison.Ordinal));
        ShellWorkspaceState shellWorkspace = new(
            Id: workspaceId,
            Name: openWorkspace.Name,
            Alias: openWorkspace.Alias,
            LastOpenedUtc: openWorkspace.LastOpenedUtc,
            RulesetId: openWorkspace.RulesetId);

        return ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [shellWorkspace],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = navigationTabs,
            ActiveTabId = infoTab.Id
        };
    }

    private static void EnsureBrowserFixtureAvailable(string fileName)
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string sourcePath = Path.Combine(repoRoot, "Chummer.Tests", "TestFiles", fileName);
        string fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Directory.CreateDirectory(fixtureDirectory);

        string destinationPath = Path.Combine(fixtureDirectory, fileName);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (!File.Exists(destinationPath))
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }

                using FileStream stream = File.Open(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > 0)
                {
                    return;
                }
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
        }

        throw new IOException($"Browser demo fixture '{fileName}' could not be staged at '{destinationPath}'.");
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

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct) => Task.CompletedTask;

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FixtureImportingOverviewPresenter : ICharacterOverviewPresenter
    {
        public FixtureImportingOverviewPresenter(CharacterOverviewState state)
        {
            State = state;
        }

        public CharacterOverviewState State { get; private set; }
        public string? ImportedContent { get; private set; }
        public string? ImportedRulesetId { get; private set; }
        public string? ExecutedCommandId { get; private set; }
        public string? SelectedTabId { get; private set; }
        public string? HandledUiControlId { get; private set; }
        public string? ExecutedDialogActionId { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            ImportedContent = document.Content;
            ImportedRulesetId = document.RulesetId;

            CharacterWorkspaceId workspaceId = new("fixture-ws");
            OpenWorkspaceState openWorkspace = new(
                Id: workspaceId,
                Name: "Fixture Runner",
                Alias: "FIX",
                LastOpenedUtc: DateTimeOffset.UtcNow,
                RulesetId: document.RulesetId,
                HasSavedWorkspace: false);

            State = State with
            {
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: workspaceId,
                    OpenWorkspaces: [openWorkspace],
                    RecentWorkspaceIds: [workspaceId]),
                OpenWorkspaces = [openWorkspace],
                WorkspaceId = workspaceId
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandId = commandId;
            return Task.CompletedTask;
        }

        public Task HandleUiControlAsync(string controlId, CancellationToken ct)
        {
            HandledUiControlId = controlId;
            return Task.CompletedTask;
        }

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct) => Task.CompletedTask;
        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
        {
            ExecutedDialogActionId = actionId;
            return Task.CompletedTask;
        }

        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabId = tabId;
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ExportAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PrintAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public TestHostEnvironment()
        {
            WebRootFileProvider = new TestBuildPwaFileProvider();
        }

        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "Chummer.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }
    }

    private sealed class TestBuildPwaFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath)
            => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath)
        {
            string normalizedPath = subpath.TrimStart('/');
            return BuildPwaReleaseContract.AssetPaths.Contains(normalizedPath, StringComparer.Ordinal)
                ? new TestBuildPwaFileInfo(normalizedPath)
                : new NotFoundFileInfo(normalizedPath);
        }

        public Microsoft.Extensions.Primitives.IChangeToken Watch(string filter)
            => new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);
    }

    private sealed class TestBuildPwaFileInfo : IFileInfo
    {
        private readonly byte[] _content;

        public TestBuildPwaFileInfo(string name)
        {
            Name = name;
            _content = Encoding.UTF8.GetBytes($"test-build-pwa-asset:{name}");
        }

        public bool Exists => true;

        public long Length => _content.LongLength;

        public string? PhysicalPath => null;

        public string Name { get; }

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(_content, writable: false);
    }
}
