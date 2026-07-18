#nullable enable annotations

using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class PublicPreviewSurfaceTests
{
    [TestMethod]
    public void Home_renders_truthful_public_navigation_and_browser_desktop_boundaries()
    {
        using var context = new BunitContext();
        IRenderedComponent<Home> cut = context.Render<Home>();

        StringAssert.Contains(cut.Markup, "Chummer Online for real dossier work.");
        StringAssert.Contains(cut.Markup, "This surface is the front door for the self-hosted Chummer portal:");
        StringAssert.Contains(cut.Markup, "Character workflows are live in the browser where parity is");
        StringAssert.Contains(cut.Markup, "Persistent dossier identity.");
        Assert.IsFalse(cut.Markup.Contains("Chummer Online for real runner work.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Stable dossier identity.", StringComparison.Ordinal));
        StringAssert.Contains(cut.Markup, "href=\"app?command=character_roster\"");
        StringAssert.Contains(cut.Markup, "href=\"showcase\"");
        StringAssert.Contains(cut.Markup, "href=\"/downloads/\"");
        StringAssert.Contains(cut.Markup, "href=\"/docs/\"");
        StringAssert.Contains(cut.Markup, "/app?command=character_roster</code>");
        StringAssert.Contains(cut.Markup, "/app</code>");
        StringAssert.Contains(cut.Markup, "/docs</code>");
        StringAssert.Contains(cut.Markup, "Desktop client remains authoritative for");
        StringAssert.Contains(cut.Markup, "NPC Persona Studio");
        Assert.IsNotNull(cut.Find("main.public-preview"));
        Assert.IsNotNull(cut.Find("#boundaries"));
        Assert.IsNotNull(cut.Find("#trust"));
    }

    [TestMethod]
    public void Portal_program_redirects_clean_public_app_route_to_hosted_blazor_app_and_preserves_query_string()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Portal", "Program.cs"));

        StringAssert.Contains(source, "app.MapGet(PortalRoutes.PublicApp, (HttpContext context) => Results.Redirect(BuildPublicAppRedirectUrl(options, context)));");
        StringAssert.Contains(source, "app.MapGet(PortalRoutes.PublicOnline, (HttpContext context) => Results.Redirect(BuildPublicOnlineRedirectUrl(options, context)));");
        StringAssert.Contains(source, "static string BuildPublicAppRedirectUrl(PortalOptions options, HttpContext context)");
        StringAssert.Contains(source, "return $\"{BuildBlazorAppUrl(options)}{context.Request.QueryString}\";");
        StringAssert.Contains(source, "static string BuildPublicOnlineRedirectUrl(PortalOptions options, HttpContext context)");
        StringAssert.Contains(source, "return $\"{BuildBlazorAppUrl(options)}{context.Request.QueryString}\";");
        StringAssert.Contains(source, "static string BuildBlazorAppUrl(PortalOptions options)");
        StringAssert.Contains(source, "=> BuildPublicUrl(options.BlazorUrl, PortalRoutes.BlazorAppSegment);");
        StringAssert.Contains(source, "public const string PublicApp = \"/app\";");
        StringAssert.Contains(source, "public const string PublicOnline = \"/online\";");
        StringAssert.Contains(source, "public const string BlazorApp = \"/blazor/app\";");
        StringAssert.Contains(source, "public static string PublicAppRoster => $\"{PublicApp}?command={CharacterRosterCommand}\";");
        Assert.IsFalse(source.Contains("app.MapGet(PortalRoutes.PublicAppSlash", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("app.MapGet(PortalRoutes.PublicOnlineSlash", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("public const string PublicAppSlash", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("public const string PublicOnlineSlash", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("static string BuildBlazorOnlineUrl(PortalOptions options)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("public const string BlazorOnline = \"/blazor/online\";", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Portal_program_keeps_clean_public_app_route_in_openapi_and_route_registry()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Portal", "Program.cs"));

        StringAssert.Contains(source, "const blazorAppMarker = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}' || route === '{{PortalRoutes.PublicOnline}}' ? ' data-openapi-chummer-app-route=\"true\"' : '';");
        StringAssert.Contains(source, "const routeFamily = route === '{{PortalRoutes.PublicApp}}' || route === '{{PortalRoutes.BlazorApp}}' || route === '{{PortalRoutes.PublicOnline}}'");
        StringAssert.Contains(source, "[PortalRoutes.PublicApp] = new");
        StringAssert.Contains(source, "summary = \"Open Chummer Online through the clean public /app route\"");
        StringAssert.Contains(source, "[PortalRoutes.BlazorApp] = new");
        StringAssert.Contains(source, "summary = \"Open the user-facing Chummer Online app\"");
        StringAssert.Contains(source, "[PortalRoutes.PublicOnline] = new");
        StringAssert.Contains(source, "summary = \"Open Chummer Online through the clean public /online alias\"");
        StringAssert.Contains(source, "route === '/blazor/'");
        StringAssert.Contains(source, "? 'Hosted browser entry'");
        StringAssert.Contains(source, "summary = \"Open the hosted Blazor browser entry that resolves into Chummer Online\"");
        Assert.IsFalse(source.Contains("[PortalRoutes.BlazorOnline] = new", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("summary = \"Open the user-facing Chummer Online app alias route\"", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("? 'Stable browser entry'", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("summary = \"Open the stable Blazor browser entry that resolves into Chummer Online\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Preview_renders_explicit_boundary_banner_around_desktop_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Preview Chummer Online workflows without changing the public route.");
        StringAssert.Contains(cut.Markup, "Browser-ready here:");
        StringAssert.Contains(cut.Markup, "Desktop-only still:");
        StringAssert.Contains(cut.Markup, "Published self-hosted Docker surface");
        StringAssert.Contains(cut.Markup, "Implicit owner sign-in");
        StringAssert.Contains(cut.Markup, "href=\"home\"");
        StringAssert.Contains(cut.Markup, "href=\"app\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench\"");
        StringAssert.Contains(cut.Markup, "href=\"showcase\"");
        StringAssert.Contains(cut.Markup, "href=\"/downloads/\"");
        StringAssert.Contains(cut.Markup, "href=\"/docs/\"");
        StringAssert.Contains(cut.Markup, "Open Build Lab");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;tab=tab-create");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;tab=tab-rules");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;tab=tab-technomancer");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;tab=tab-contacts");
        StringAssert.Contains(cut.Markup, "New runner");
        StringAssert.Contains(cut.Markup, "Open Dossier");
        StringAssert.Contains(cut.Markup, "Open Print Staging");
        StringAssert.Contains(cut.Markup, "Open Export Staging");
        StringAssert.Contains(cut.Markup, "Open Print Result");
        StringAssert.Contains(cut.Markup, "Open Export Result");
        StringAssert.Contains(cut.Markup, "Open Save Result");
        StringAssert.Contains(cut.Markup, "Open Save As Result");
        StringAssert.Contains(cut.Markup, "Open clean Origin Dossier route");
        StringAssert.Contains(cut.Markup, "preview?command=open_character");
        StringAssert.Contains(cut.Markup, "preview?command=open_for_printing");
        StringAssert.Contains(cut.Markup, "preview?command=open_for_export");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=print_character");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=export_character");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=export_character&amp;dialog_action=download");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=save_character");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=save_character_as");
        StringAssert.Contains(cut.Markup, "preview?fixture=blue&amp;command=save_character_as&amp;dialog_action=download");
        StringAssert.Contains(cut.Markup, "preview?command=new_character");
        StringAssert.Contains(cut.Markup, "preview?command=new_character_origin");
        Assert.IsFalse(cut.Markup.Contains(">Open Origin Dossier<", StringComparison.Ordinal));
        StringAssert.Contains(cut.Markup, "Seed Build Lab with a real SR5 dossier");
        StringAssert.Contains(cut.Markup, "Open the same seeded dossier on Rules");
        StringAssert.Contains(cut.Markup, "Drives the seeded dossier into the `Technomancer` lane with live section rendering and add actions.");
        StringAssert.Contains(cut.Markup, "Keep dossier context on Contacts");
        StringAssert.Contains(cut.Markup, "Moves a seeded browser dossier past startup into a living continuation tab.");
        StringAssert.Contains(cut.Markup, "Save a seeded dossier in the browser");
        StringAssert.Contains(cut.Markup, "Loads the published `BLUE` dossier and prepares a real print file instead of just opening an entry dialog.");
        StringAssert.Contains(cut.Markup, "Loads the published `BLUE` dossier and prepares a real export plus download action.");
        StringAssert.Contains(cut.Markup, "Loads the published `BLUE` dossier and saves it through the shared command.");
        StringAssert.Contains(cut.Markup, "Loads the published `BLUE` dossier and prepares the download through the shared save-as command.");
        Assert.IsFalse(cut.Markup.Contains("Seed Build Lab with a real SR5 runner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Open the same seeded runner on Rules", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Keep runner context on Contacts", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find(".browser-preview-status-grid"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='build-lab']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='technomancer']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='new-character']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='open-character']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='open-for-printing']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='open-for-export']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='print-character-result']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='export-character-result']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='save-character-result']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='save-character-as-result']"));
        Assert.IsNotNull(cut.Find("[data-preview-proof-card='origin-dossier']"));
        Assert.IsNotNull(cut.Find(".desktop-shell"));
    }

    [TestMethod]
    public void Workbench_route_keeps_dossier_copy_for_context_and_search_shortcuts()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(cut.Find("[data-workbench-context-actions='strip']"));
            Assert.IsNotNull(cut.Find("[data-workbench-search-filter='strip']"));
        });

        Assert.AreEqual(
            "Selection-sensitive dossier actions",
            cut.Find(".browser-workbench-context-actions-list").GetAttribute("aria-label"));
        StringAssert.Contains(cut.Markup, "Create from the selected dossier lane.");
        StringAssert.Contains(cut.Markup, "Return to the active dossier context.");
        StringAssert.Contains(cut.Markup, "Find, group, and organize existing dossiers.");
        StringAssert.Contains(cut.Markup, "Dense rows, short labels, maximum dossier context.");
        Assert.IsFalse(cut.Markup.Contains("maximum runner context", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Preview_component_css_keeps_status_cards_readable_when_light_card_surface_is_active()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string css = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "Components", "Pages", "Preview.razor.css"));

        AssertBlockContains(css, ".browser-preview-shell .browser-preview-status-card {", "color: #1f1d1a;");
        AssertBlockContains(css, ".browser-preview-shell .browser-preview-status-card strong {", "color: #1f1d1a;");
        AssertBlockContains(css, ".browser-preview-shell .browser-preview-status-card p {", "color: #4b5563;");
        AssertBlockContains(css, ".browser-preview-shell .browser-preview-status-card a {", "color: #1f1d1a;");
        AssertBlockContains(css, ".browser-preview-shell .browser-preview-status-card a:hover,", "background: #dbeaf6;");
    }

    [TestMethod]
    public void Preview_component_css_keeps_classic_frame_dossier_copy_in_sync()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string css = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "Components", "Pages", "Preview.razor.css"));

        StringAssert.Contains(css, "File   Edit   View   Dossier   Tools   Help\\A New runner  Open Dossier  Save  Print  Export  Character Roster  Build Lab");
        Assert.IsFalse(
            css.Contains("File   Edit   View   Runner   Tools   Help\\A New runner  Open Runner  Save  Print  Export  Character Roster  Build Lab", StringComparison.Ordinal),
            "The classic preview frame should stay aligned with dossier-facing shell copy.");
    }

    [TestMethod]
    public void Preview_rules_data_strip_uses_compatibility_command_routes_and_same_origin_help()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.IsNotNull(cut.Find("[data-workbench-data-packs='strip']"));
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=open_sourcebooks\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=open_errata\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=open_custom_data\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=update_data_packs\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=validate_data_scope\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=open_data_folder\"");
        StringAssert.Contains(cut.Markup, "href=\"/help\"");
    }

    [TestMethod]
    public void Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Chummer Online compatibility shell, running in the browser.");
        StringAssert.Contains(cut.Markup, "The clean public Chummer Online entry is");
        StringAssert.Contains(cut.Markup, "/app</code>.");
        StringAssert.Contains(cut.Markup, "Preview tools");
        StringAssert.Contains(cut.Markup, "Start a new runner");
        StringAssert.Contains(cut.Markup, "Import an existing dossier");
        StringAssert.Contains(cut.Markup, "Open or import a dossier.");
        StringAssert.Contains(cut.Markup, "Review dossier XML in the browser");
        StringAssert.Contains(cut.Markup, "Paste or stage Chummer dossier XML.");
        StringAssert.Contains(cut.Markup, "try a sample dossier");
        StringAssert.Contains(cut.Markup, "Sample dossier");
        StringAssert.Contains(cut.Markup, "Open a guided example dossier.");
        StringAssert.Contains(cut.Markup, "Keep recent dossiers one click away.");
        StringAssert.Contains(cut.Markup, "Keep a campaign roster pinned.");
        StringAssert.Contains(cut.Markup, "Surface published and preview channel posture.");
        StringAssert.Contains(cut.Markup, "Open a live seeded dossier");
        StringAssert.Contains(cut.Markup, "Continue PRV in Build Lab");
        StringAssert.Contains(cut.Markup, "Resume from restored session state");
        StringAssert.Contains(cut.Markup, "Resume PRV");
        StringAssert.Contains(cut.Markup, "saved");
        StringAssert.Contains(cut.Markup, "Continue restored dossier lanes");
        StringAssert.Contains(cut.Markup, "Resume PRV on profile");
        StringAssert.Contains(cut.Markup, "Resume PRV on rules");
        Assert.IsFalse(cut.Markup.Contains("Open a live seeded runner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Reserve a stable campaign roster.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Surface stable and preview channel posture.", StringComparison.Ordinal));
        StringAssert.Contains(cut.Markup, "Resume PRV on gear");
        StringAssert.Contains(cut.Markup, "Resume PRV on career log");
        StringAssert.Contains(cut.Markup, "Resume PRV on advanced");
        StringAssert.Contains(cut.Markup, "Add a contact for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep contact for PRV");
        StringAssert.Contains(cut.Markup, "Edit contact for PRV");
        StringAssert.Contains(cut.Markup, "Remove contact for PRV");
        StringAssert.Contains(cut.Markup, "Adjust contact connection for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep career entry for PRV");
        StringAssert.Contains(cut.Markup, "Add career entry for PRV");
        StringAssert.Contains(cut.Markup, "Apply career entry edit for PRV");
        StringAssert.Contains(cut.Markup, "Edit career entry for PRV");
        StringAssert.Contains(cut.Markup, "Remove and keep career entry result for PRV");
        StringAssert.Contains(cut.Markup, "Remove career entry for PRV");
        StringAssert.Contains(cut.Markup, "Save dossier notes for PRV");
        StringAssert.Contains(cut.Markup, "Edit dossier notes for PRV");
        StringAssert.Contains(cut.Markup, "Add SIN/license for PRV");
        StringAssert.Contains(cut.Markup, "Edit SIN/license for PRV");
        StringAssert.Contains(cut.Markup, "Remove SIN/license for PRV");
        StringAssert.Contains(cut.Markup, "Add weapon for PRV");
        StringAssert.Contains(cut.Markup, "Reload weapon for PRV");
        StringAssert.Contains(cut.Markup, "Review damage track for PRV");
        StringAssert.Contains(cut.Markup, "Move career entry up for PRV");
        StringAssert.Contains(cut.Markup, "Move career entry down for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep complex form for PRV");
        StringAssert.Contains(cut.Markup, "Add a complex form for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep initiation for PRV");
        StringAssert.Contains(cut.Markup, "Add initiation for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Add cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Edit cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Remove cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep spell for PRV");
        StringAssert.Contains(cut.Markup, "Add a spell for PRV");
        StringAssert.Contains(cut.Markup, "Add armor for PRV");
        StringAssert.Contains(cut.Markup, "Add skill for PRV");
        StringAssert.Contains(cut.Markup, "Add adept power for PRV");
        StringAssert.Contains(cut.Markup, "Add spirit for PRV");
        StringAssert.Contains(cut.Markup, "Add Matrix program for PRV");
        StringAssert.Contains(cut.Markup, "Add general magic item for PRV");
        StringAssert.Contains(cut.Markup, "Bind spirit for PRV");
        StringAssert.Contains(cut.Markup, "Show magic source for PRV");
        StringAssert.Contains(cut.Markup, "Remove magic item for PRV");
        StringAssert.Contains(cut.Markup, "Remove quality for PRV");
        StringAssert.Contains(cut.Markup, "Add vehicle for PRV");
        StringAssert.Contains(cut.Markup, "Edit vehicle for PRV");
        StringAssert.Contains(cut.Markup, "Remove vehicle for PRV");
        StringAssert.Contains(cut.Markup, "Add vehicle mod for PRV");
        StringAssert.Contains(cut.Markup, "Add gear for PRV");
        StringAssert.Contains(cut.Markup, "Edit gear for PRV");
        StringAssert.Contains(cut.Markup, "Remove gear for PRV");
        StringAssert.Contains(cut.Markup, "Add drug for PRV");
        StringAssert.Contains(cut.Markup, "Show gear source for PRV");
        StringAssert.Contains(cut.Markup, "Mount gear for PRV");
        StringAssert.Contains(cut.Markup, "Toggle gear free/paid for PRV");
        StringAssert.Contains(cut.Markup, "Remove drug for PRV");
        StringAssert.Contains(cut.Markup, "Specialize skill for PRV");
        StringAssert.Contains(cut.Markup, "Remove skill for PRV");
        StringAssert.Contains(cut.Markup, "Edit skill group for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep critter power for PRV");
        StringAssert.Contains(cut.Markup, "Add critter power for PRV");
        StringAssert.Contains(cut.Markup, "Open Runner Intelligence benchmarks for PRV");
        StringAssert.Contains(cut.Markup, "Model Increase Initiative and inventory what-if stack for PRV");
        StringAssert.Contains(cut.Markup, "Review Runner Intelligence privacy cohorts for PRV");
        StringAssert.Contains(cut.Markup, "Show source for PRV");
        StringAssert.Contains(cut.Markup, "Continue a recent dossier");
        StringAssert.Contains(cut.Markup, "Open Dossier");
        StringAssert.Contains(cut.Markup, "Continue dossier");
        StringAssert.Contains(cut.Markup, "Import dossier XML");
        StringAssert.Contains(cut.Markup, "Drag dossiers or folders to organize this roster.");
        StringAssert.Contains(cut.Markup, "Saved Dossiers");
        StringAssert.Contains(cut.Markup, "Active dossier");
        StringAssert.Contains(cut.Markup, "Sample dossier");
        StringAssert.Contains(cut.Markup, "Dossier sections");
        StringAssert.Contains(cut.Markup, "Dossier Identity");
        StringAssert.Contains(cut.Markup, "Unsaved local dossier");
        StringAssert.Contains(cut.Markup, "Dossier inspector");
        StringAssert.Contains(cut.Markup, "Unsaved dossier");
        StringAssert.Contains(cut.Markup, "Restored dossier: PRV");
        StringAssert.Contains(cut.Markup, "New runner, open dossiers, origins, and lists");
        StringAssert.Contains(cut.Markup, "Dense dossier sections and focused dialogs");
        StringAssert.Contains(cut.Markup, "Dossier tabs");
        StringAssert.Contains(cut.Markup, "Active dossier, build review, print/export, and recent import stay side by side.");
        StringAssert.Contains(cut.Markup, "Open another dossier.");
        StringAssert.Contains(cut.Markup, "Review the browser-safe dossier output handoff.");
        StringAssert.Contains(cut.Markup, "Continue dossier export/download work.");
        StringAssert.Contains(cut.Markup, "Bring existing dossiers across carefully.");
        StringAssert.Contains(cut.Markup, "Review imported changes before they touch the dossier.");
        StringAssert.Contains(cut.Markup, "Package the dossier for play.");
        StringAssert.Contains(cut.Markup, "Build table panels from this dossier.");
        StringAssert.Contains(cut.Markup, "Continue restored dossier lanes");
        StringAssert.Contains(cut.Markup, "Open the most recent restored dossier directly on the same browser lanes you would continue from in a desktop session.");
        StringAssert.Contains(cut.Markup, "Continue PRV on contacts");
        StringAssert.Contains(cut.Markup, "Continue PRV on profile");
        StringAssert.Contains(cut.Markup, "Continue PRV on rules");
        StringAssert.Contains(cut.Markup, "Continue PRV on gear");
        StringAssert.Contains(cut.Markup, "Continue PRV on advanced");
        StringAssert.Contains(cut.Markup, "Continue PRV for download");
        StringAssert.Contains(cut.Markup, "Continue PRV for export");
        StringAssert.Contains(cut.Markup, "Continue PRV for print");
        StringAssert.Contains(cut.Markup, "the clean public Chummer Online route remains /app and the hosted app path remains /blazor/app");
        Assert.IsFalse(cut.Markup.Contains("Review runner XML in the browser", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Paste or stage Chummer runner XML.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Bring existing runners across carefully.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Review imported changes before they touch the runner.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Package the runner for play.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Build table panels from this runner.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("try a sample runner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Sample runner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Open a guided example runner.", StringComparison.Ordinal));
        StringAssert.Contains(cut.Markup, "Review identity and profile");
        StringAssert.Contains(cut.Markup, "Review rules and references");
        StringAssert.Contains(cut.Markup, "Review loadout and gear");
        StringAssert.Contains(cut.Markup, "Open advanced build lanes");
        StringAssert.Contains(cut.Markup, "Prepare a browser download");
        StringAssert.Contains(cut.Markup, "Prepare an export package");
        StringAssert.Contains(cut.Markup, "Prepare a print preview");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=new_character\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?command=open_character\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-create\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws\"");
        StringAssert.Contains(cut.Markup, "data-workbench-recent-workspace=\"preview-ws\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-rules\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-skills\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-combat\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_edit\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_remove\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_connection\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=create_entry&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=create_entry\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=edit_entry&amp;dialog_action=apply\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=edit_entry\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=delete_entry&amp;dialog_action=delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=delete_entry\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info&amp;control=open_notes&amp;dialog_action=save\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info&amp;control=open_notes\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info&amp;control=identity_license_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info&amp;control=identity_license_edit\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info&amp;control=identity_license_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-combat&amp;control=combat_add_weapon\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-combat&amp;control=combat_reload\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-combat&amp;control=combat_damage_track\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=move_up\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-calendar&amp;control=move_down\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-technomancer&amp;control=complex_form_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-technomancer&amp;control=complex_form_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-skills&amp;control=skill_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-skills&amp;control=skill_specialize\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-skills&amp;control=skill_remove\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-skills&amp;control=skill_group\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-combat&amp;control=combat_add_armor\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-adept&amp;control=adept_power_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=spirit_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-technomancer&amp;control=matrix_program_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=magic_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=magic_bind\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=magic_source\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=magic_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-qualities&amp;control=quality_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=vehicle_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=vehicle_edit\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=vehicle_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=vehicle_mod_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=gear_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=gear_edit\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=gear_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=drug_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=gear_source\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=gear_mount\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=toggle_free_paid\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear&amp;control=drug_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-rules&amp;control=show_source\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?tab=tab-qualities&amp;control=quality_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-adept&amp;control=initiation_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-adept&amp;control=initiation_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_edit\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_delete\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=spell_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=spell_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-critter&amp;control=critter_power_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-critter&amp;control=critter_power_add\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-stats&amp;control=runner_benchmark\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-stats&amp;control=runner_what_if\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-stats&amp;control=runner_cohort_privacy\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-contacts\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-rules\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-gear\"");
        StringAssert.Contains(cut.Markup, "href=\"workbench?workspace=preview-ws&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=save_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=save_character_as\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=save_character_as&amp;dialog_action=download\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=export_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=export_character&amp;dialog_action=download\"");
        StringAssert.Contains(cut.Markup, "href=\"app?workspace=preview-ws&amp;command=print_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=save_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=save_character_as\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=export_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=print_character\"");
        StringAssert.Contains(cut.Markup, "href=\"app?fixture=blue&amp;command=print_preview\"");
        StringAssert.Contains(cut.Markup, "href=\"preview\"");
        Assert.IsFalse(cut.Markup.Contains("data-preview-proof-card=\"build-lab\"", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='new-character']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='open-character']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='seeded-build-lab']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='continue-recent']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='recent-work']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='restored-continuations']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='restored-actions']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='profile']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='rules']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='gear']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='technomancer']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='save-as']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='export']"));
        Assert.IsNotNull(cut.Find("[data-workbench-entry-card='print']"));
        Assert.IsNotNull(cut.Find(".desktop-shell"));
    }

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
        StringAssert.Contains(cut.Markup, "Find, group, and organize existing dossiers.");
        StringAssert.Contains(cut.Markup, "Kestrel");
        StringAssert.Contains(cut.Markup, "Street samurai");
        StringAssert.Contains(cut.Markup, "Rook");
        StringAssert.Contains(cut.Markup, "Decker");
        Assert.IsTrue(cut.Markup.Contains("chummer-online-app-shell", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find(".browser-app-roster"));
        Assert.IsNotNull(cut.Find("[data-app-route-classic-menu='true']"));
        Assert.AreEqual(6, cut.FindAll("[data-app-route-menu-root]").Count);
        Assert.AreEqual("File", cut.Find("[data-app-route-menu-root='file'] > button").TextContent.Trim());
        Assert.AreEqual("New runner", cut.Find("[data-app-route-menu-panel='file'] [data-app-route-menu-command='new_character']").TextContent.Trim());
        Assert.AreEqual("Open Dossier", cut.Find("[data-app-route-menu-panel='file'] [data-app-route-menu-command='open_character']").TextContent.Trim());
        Assert.AreEqual("Open sample dossier", cut.Find("[data-app-route-menu-panel='file'] [data-app-route-menu-command='open_example']").TextContent.Trim());
        Assert.AreEqual("Continue dossier", cut.Find("[data-app-route-menu-panel='special'] [data-app-route-menu-command='continue_recent']").TextContent.Trim());
        Assert.AreEqual("Compatibility route", cut.Find("[data-app-route-menu-panel='windows'] [data-app-route-menu-command='compatibility_route']").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-app-roster-actions").TextContent, "Open sample dossier");
        Assert.AreEqual("Dossier folders", cut.Find(".browser-app-roster-tree").GetAttribute("aria-label"));
        Assert.AreEqual("Organize dossiers", cut.Find(".browser-app-roster-toolbar").GetAttribute("aria-label"));
        Assert.AreEqual("Selected dossier", cut.Find(".browser-app-runner-panel").GetAttribute("aria-label"));
        Assert.AreEqual("Dossier summary", cut.Find(".browser-app-runner-stats").GetAttribute("aria-label"));
        Assert.AreEqual("Open dossier Kestrel", cut.Find(".browser-app-runner-actions").GetAttribute("aria-label"));
        Assert.IsNotNull(cut.Find(".browser-app-example-panel"));
        Assert.IsNotNull(cut.Find("[data-drop-target='runner-folder']"));
        Assert.IsFalse(cut.Markup.Contains("Open example", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("data-preview-proof-card=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-banner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-promoted-app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-frame--app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("desktop-shell", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-chummer-shell", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("new_character", "build-lab", "none", "none")]
    [DataRow("new_character_origin", "origin-dossier", "none", "none")]
    [DataRow("character_roster", "character-roster", "none", "none")]
    [DataRow("master_index", "master-index", "none", "none")]
    [DataRow("global_settings", "global-settings", "none", "none")]
    [DataRow("character_settings", "character-settings", "none", "none")]
    [DataRow("switch_ruleset", "switch-ruleset", "none", "none")]
    [DataRow("report_bug", "support", "none", "none")]
    [DataRow("about", "about", "none", "none")]
    [DataRow("runtime_inspector", "runtime-inspector", "none", "none")]
    [DataRow("auto_alice", "assistant", "none", "none")]
    [DataRow("translator", "translator", "none", "none")]
    [DataRow("xml_editor", "xml-editor", "none", "none")]
    [DataRow("hero_lab_importer", "hero-lab-importer", "none", "none")]
    [DataRow("open_character", "open-dossier", "none", "none")]
    [DataRow("save_character", "save", "save", "local-dossier")]
    [DataRow("save_character_as", "save", "save", "local-dossier")]
    [DataRow("open_for_printing", "print", "print", "print-view")]
    [DataRow("print_preview", "print", "print", "print-view")]
    [DataRow("print_character", "print", "print", "print-view")]
    [DataRow("open_for_export", "export", "export", "download-package")]
    [DataRow("export_character", "export", "export", "download-package")]
    public void Workbench_command_only_routes_publish_expected_shell_metadata_and_execute_startup_command(
        string commandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo($"/workbench?command={commandId}");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(commandId, presenter.ExecutedCommandId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(commandId.Replace('_', '-'), shell.GetAttribute("data-command"));
            Assert.AreEqual("none", shell.GetAttribute("data-tab"));
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, shell.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, shell.GetAttribute("data-output-target"));
        });
    }

    [DataTestMethod]
    [DataRow("/workbench?command=save_character", "Dossier save prepared.")]
    [DataRow("/workbench?command=save_character_as", "Browser dossier download prepared.")]
    [DataRow("/workbench?command=save_character_as&dialog_action=download", "Dossier download ready.")]
    [DataRow("/workbench?command=export_character", "Export package prepared.")]
    [DataRow("/workbench?command=export_character&dialog_action=download", "Export package download ready.")]
    [DataRow("/workbench?command=print_character", "Print preview prepared.")]
    [DataRow("/workbench?command=print_preview", "Print preview opened.")]
    public void Workbench_output_routes_render_committed_result_banner(
        string route,
        string expectedResultText)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ExecutedCommandId);
            var result = cut.Find("[data-workbench-committed-result]");
            Assert.AreEqual(expectedResultText, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });
    }

    [DataTestMethod]
    [DataRow("/workbench?command=save_character_as&dialog_action=download", "save", "Download Dossier", "Download Dossier shell", "from the compatibility route.")]
    [DataRow("/workbench?command=export_character&dialog_action=download", "export", "Download Export Package", "Download package shell", "from the compatibility route.")]
    [DataRow("/workbench?command=print_preview", "print", "Open Print Preview", "Print preview shell", "from the compatibility route.")]
    [DataRow("/workbench?command=open_for_export", "export", "Open Export Staging", "Export staging shell", "from the compatibility route.")]
    public void Workbench_output_routes_render_specific_chrome_copy_while_metadata_stays_category_level(
        string route,
        string expectedWorkflow,
        string expectedCommandLabel,
        string expectedFrameTitle,
        string expectedRouteSurfacePhrase)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ExecutedCommandId);
            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        string titlebarText = cut.Find(".classic-chummer-titlebar-title").TextContent.Trim();
        Assert.IsTrue(
            titlebarText.EndsWith(expectedCommandLabel, StringComparison.Ordinal),
            $"Classic titlebar should end with the specific output-state label '{expectedCommandLabel}', but was '{titlebarText}'.");

        string footerWorkflowText = cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim();
        Assert.AreEqual(expectedCommandLabel, footerWorkflowText);
        Assert.AreEqual(expectedCommandLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, expectedRouteSurfacePhrase);
    }

    [TestMethod]
    public void Workbench_master_index_command_renders_master_index_shell_copy()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?command=master_index");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("master_index", presenter.ExecutedCommandId);
            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("master-index", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Master Index", StringComparison.Ordinal));
        Assert.AreEqual("Master Index", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Master Index", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Master Index shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "shared Master Index workflow");
        StringAssert.Contains(cut.Markup, "rules, gear, qualities, spells, and references");
    }

    [DataTestMethod]
    [DataRow("/workbench?command=global_settings", "global_settings", "global-settings", "Global Settings", "Global Settings shell", "language, update posture, and dossier defaults")]
    [DataRow("/workbench?command=character_settings", "character_settings", "character-settings", "Character Settings", "Character Settings shell", "build defaults, karma ratio, house-rule posture, and notes")]
    [DataRow("/workbench?command=switch_ruleset", "switch_ruleset", "switch-ruleset", "Switch Ruleset", "Switch Ruleset shell", "preferred edition changes")]
    [DataRow("/workbench?command=report_bug", "report_bug", "support", "Support", "Support shell", "portal support and public GitHub issue paths")]
    [DataRow("/workbench?command=about", "about", "about", "About Chummer", "About Chummer shell", "runtime identity and preview posture")]
    [DataRow("/workbench?command=runtime_inspector", "runtime_inspector", "runtime-inspector", "Runtime Inspector", "Runtime Inspector shell", "resolved runtime, rule packs, and provider bindings")]
    [DataRow("/workbench?command=auto_alice", "auto_alice", "assistant", "Assistant", "Assistant shell", "build help, rules coaching, and explicit handoffs")]
    [DataRow("/workbench?command=translator", "translator", "translator", "Translator", "Translator shell", "language search and enabled overlays")]
    [DataRow("/workbench?command=xml_editor", "xml_editor", "xml-editor", "XML Editor", "XML Editor shell", "XML bridge and custom data posture")]
    [DataRow("/workbench?command=hero_lab_importer", "hero_lab_importer", "hero-lab-importer", "Hero Lab Importer", "Hero Lab Importer shell", "local XML intake and import-oracle posture")]
    [DataRow("/workbench?command=copy", "copy", "copy", "Copy", "Copy shell", "active editor copy posture stays inside the shared shell")]
    [DataRow("/workbench?command=paste", "paste", "paste", "Paste", "Paste shell", "active editor paste posture stays inside the shared shell")]
    [DataRow("/workbench?command=new_critter", "new_critter", "new-critter", "New Critter", "New Critter shell", "critter-first starter imports stay inside the shared shell")]
    [DataRow("/workbench?command=restart", "restart", "restart", "Restart", "Restart shell", "session reset and relaunch posture stay inside the shared shell")]
    [DataRow("/workbench?command=exit", "exit", "exit", "Exit", "Exit shell", "desktop-only exit posture stays inside the shared shell")]
    [DataRow("/workbench?command=close_window", "close_window", "close-window", "Close Window", "Close Window shell", "active-dossier close posture stays inside the shared shell")]
    [DataRow("/workbench?command=close_all", "close_all", "close-all", "Close All", "Close All shell", "session-wide close posture stays inside the shared shell")]
    [DataRow("/workbench?command=dice_roller", "dice_roller", "dice-roller", "Dice Roller", "Dice Roller shell", "roll method, threshold, and reroll posture")]
    [DataRow("/workbench?command=data_exporter", "data_exporter", "data-exporter", "Data Exporter", "Data Exporter shell", "export pipeline preview and payload handoff")]
    [DataRow("/workbench?command=print_setup", "print_setup", "print-setup", "Print Setup", "Print Setup shell", "print preferences stay inside the shared shell")]
    [DataRow("/workbench?command=print_multiple", "print_multiple", "print-multiple", "Print Multiple", "Print Multiple shell", "roster batch print posture stays inside the shared shell")]
    [DataRow("/workbench?command=update", "update", "update", "Update", "Update shell", "channel, pending installer, and support follow-through")]
    [DataRow("/workbench?command=new_window", "new_window", "new-window", "New Window", "New Window shell", "second-shell handoff stays inside the shared shell")]
    [DataRow("/workbench?command=wiki", "wiki", "wiki", "Wiki", "Wiki shell", "legacy documentation handoff stays inside the shared shell")]
    [DataRow("/workbench?command=discord", "discord", "discord", "Discord", "Discord shell", "community handoff stays inside the shared shell")]
    [DataRow("/workbench?command=show_login_video", "show_login_video", "login-video", "Login Video", "Login Video shell", "Matrix uplink handoff stays inside the shared shell")]
    [DataRow("/workbench?command=revision_history", "revision_history", "revision-history", "Revision History", "Revision History shell", "release notes and external history links stay inside the shared shell")]
    [DataRow("/workbench?command=dumpshock", "dumpshock", "issue-tracker", "Issue Tracker", "Issue Tracker shell", "issue-handling and external tracker context stay inside the shared shell")]
    [DataRow("/workbench?command=open_sourcebooks", "open_sourcebooks", "sourcebooks", "Sourcebooks", "Sourcebooks shell", "active book selection stays inside the shared shell")]
    [DataRow("/workbench?command=open_errata", "open_errata", "errata", "Errata", "Errata shell", "rules-update posture stays inside the shared shell")]
    [DataRow("/workbench?command=open_custom_data", "open_custom_data", "custom-data", "Custom Data", "Custom Data shell", "homebrew and local packs stay discoverable inside the shared shell")]
    [DataRow("/workbench?command=update_data_packs", "update_data_packs", "update-pack", "Update Pack", "Update Pack shell", "data refresh stays visible as an operator action inside the shared shell")]
    [DataRow("/workbench?command=validate_data_scope", "validate_data_scope", "validation-scope", "Validation Scope", "Validation Scope shell", "rules data stays connected to build readiness inside the shared shell")]
    [DataRow("/workbench?command=open_data_folder", "open_data_folder", "data-folder", "Data Folder", "Data Folder shell", "local and self-host rule-data paths stay visible inside the shared shell")]
    public void Workbench_tool_commands_render_specific_shell_copy(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedCommandLabel,
        string expectedFrameTitle,
        string expectedCopyFragment)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(expectedCommandId, presenter.ExecutedCommandId);
            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith(expectedCommandLabel, StringComparison.Ordinal));
        Assert.AreEqual(expectedCommandLabel, cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual(expectedCommandLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, expectedCopyFragment);
    }

    [DataTestMethod]
    [DataRow("/workbench?command=character_settings", "character_settings", "character-settings", "Character Settings")]
    [DataRow("/workbench?command=copy", "copy", "copy", "Copy")]
    [DataRow("/workbench?command=data_exporter", "data_exporter", "data-exporter", "Data Exporter")]
    public void Workbench_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual("blocked", shell.GetAttribute("data-startup-command-state"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsNull(presenter.ExecutedCommandId);
        Assert.AreEqual(expectedTitle, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual($"{expectedTitle} requires an open dossier", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "before Chummer Online can continue from the compatibility route");
        StringAssert.Contains(cut.Markup, "Open or restore a dossier first");
        Assert.IsFalse(
            cut.Markup.Contains("disabled in the current shell state.", StringComparison.Ordinal),
            "Blocked compatibility startup routes should stay on route-level guidance instead of surfacing a shell error for a command that cannot run yet.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear", "gear", "Gear", "Gear shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-rules", "rules", "Rules", "Rules shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-skills", "skills", "Skills", "Skills shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-cyberware", "cyberware", "Cyberware", "Cyberware shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-qualities", "qualities", "Qualities", "Qualities shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-adept", "adept", "Adept", "Adept shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-combat", "combat", "Combat", "Combat shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician", "magic", "Magic", "Magic shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-critter", "critter", "Critter", "Critter shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-stats", "stats", "Stats", "Stats shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-technomancer", "matrix", "Matrix", "Matrix shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-contacts", "contacts", "Contacts", "Contacts shell", "from the compatibility route.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar", "career", "Career", "Career shell", "from the compatibility route.")]
    public void Workbench_workspace_tab_routes_render_specific_workflow_shell_copy(
        string route,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedRouteSummaryFragment)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith(expectedWorkflowLabel, StringComparison.Ordinal));
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, expectedRouteSummaryFragment);
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-combat&control=combat_add_weapon", "combat_add_weapon", "combat", "Combat", "Combat shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=gear_add", "gear_add", "gear", "Gear", "Gear shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_add", "vehicle_add", "gear", "Gear", "Gear shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=drug_add", "drug_add", "gear", "Gear", "Gear shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-combat&control=combat_add_armor", "combat_add_armor", "combat", "Combat", "Combat shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-qualities&control=quality_add", "quality_add", "qualities", "Qualities", "Qualities shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-qualities&control=quality_delete", "quality_delete", "qualities", "Qualities", "Qualities shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-skills&control=skill_add", "skill_add", "skills", "Skills", "Skills shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-adept&control=adept_power_add", "adept_power_add", "adept", "Adept", "Adept shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=spirit_add", "spirit_add", "magic", "Magic", "Magic shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-technomancer&control=matrix_program_add", "matrix_program_add", "matrix", "Matrix", "Matrix shell")]
    public void Workbench_quick_action_control_routes_render_named_shells_and_handle_controls(
        string route,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith(expectedWorkflowLabel, StringComparison.Ordinal));
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-skills&control=skill_specialize", "skill_specialize")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-skills&control=skill_remove", "skill_remove")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-skills&control=skill_group", "skill_group")]
    public void Workbench_skill_control_routes_render_skills_shell_and_handle_controls(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("skills", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Skills", StringComparison.Ordinal));
        Assert.AreEqual("Skills", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Skills", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Skills shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-stats&control=runner_benchmark", "runner_benchmark")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-stats&control=runner_what_if", "runner_what_if")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-stats&control=runner_cohort_privacy", "runner_cohort_privacy")]
    public void Workbench_runner_intelligence_control_routes_render_stats_shell_and_handle_controls(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("stats", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Stats", StringComparison.Ordinal));
        Assert.AreEqual("Stats", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Stats", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Stats shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_rules_source_control_route_renders_rules_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-rules&control=show_source");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("show_source", presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("rules", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Rules", StringComparison.Ordinal));
        Assert.AreEqual("Rules", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Rules", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Rules shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_gear_toggle_control_route_renders_gear_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-gear&control=toggle_free_paid");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("toggle_free_paid", presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("gear", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Gear", StringComparison.Ordinal));
        Assert.AreEqual("Gear", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Gear", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Gear shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=gear_edit", "gear_edit")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=gear_delete", "gear_delete")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=gear_mount", "gear_mount")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=gear_source", "gear_source")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=drug_delete", "drug_delete")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_edit", "vehicle_edit")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_delete", "vehicle_delete")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_mod_add", "vehicle_mod_add")]
    public void Workbench_gear_inventory_control_routes_render_gear_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("gear", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Gear", StringComparison.Ordinal));
        Assert.AreEqual("Gear", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Gear", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Gear shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-combat&control=combat_reload", "combat_reload", "combat", "Combat", "Combat shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-combat&control=combat_damage_track", "combat_damage_track", "combat", "Combat", "Combat shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=magic_add", "magic_add", "magic", "Magic", "Magic shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=magic_bind", "magic_bind", "magic", "Magic", "Magic shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=magic_source", "magic_source", "magic", "Magic", "Magic shell")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=magic_delete", "magic_delete", "magic", "Magic", "Magic shell")]
    public void Workbench_combat_magic_control_routes_render_named_shells(
        string route,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith(expectedWorkflowLabel, StringComparison.Ordinal));
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_edit", "cyberware_edit")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_delete", "cyberware_delete")]
    public void Workbench_cyberware_control_routes_render_cyberware_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("cyberware", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Cyberware", StringComparison.Ordinal));
        Assert.AreEqual("Cyberware", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Cyberware", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Cyberware shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_edit", "contact_edit")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_remove", "contact_remove")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_connection", "contact_connection")]
    public void Workbench_contacts_control_routes_render_contacts_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("contacts", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Contacts", StringComparison.Ordinal));
        Assert.AreEqual("Contacts", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Contacts", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Contacts shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-info&control=identity_license_add", "identity_license_add")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-info&control=identity_license_edit", "identity_license_edit")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-info&control=identity_license_delete", "identity_license_delete")]
    public void Workbench_profile_identity_license_control_routes_render_profile_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("profile", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Profile", StringComparison.Ordinal));
        Assert.AreEqual("Profile", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_profile_notes_control_route_renders_profile_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-info&control=open_notes");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("profile", shell.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Profile", StringComparison.Ordinal));
        Assert.AreEqual("Profile", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_profile_notes_save_route_renders_profile_shell_and_committed_result()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-info&control=open_notes&dialog_action=save");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);
            Assert.AreEqual("save", presenter.ExecutedDialogActionId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("profile", shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual("Notes saved.", cut.Find("[data-workbench-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Profile", StringComparison.Ordinal));
        Assert.AreEqual("Profile", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_contact_add_dialog_action_route_renders_contacts_shell_and_committed_result()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("contacts", shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual("Contact 'Fixer' added.", cut.Find("[data-workbench-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Contacts", StringComparison.Ordinal));
        Assert.AreEqual("Contacts", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Contacts", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Contacts shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add", "spell_add", "magic", "Magic", "Magic shell", "Spell 'Stunbolt' added.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-critter&control=critter_power_add&dialog_action=add", "critter_power_add", "critter", "Critter", "Critter shell", "Critter power 'Natural Weapon' added.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add", "complex_form_add", "matrix", "Matrix", "Matrix shell", "Complex form 'Cleaner' added.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add", "initiation_add", "adept", "Adept", "Adept shell", "Initiation/submersion reward 'Masking' added.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add", "cyberware_add", "cyberware", "Cyberware", "Cyberware shell", "Cyberware 'Wired Reflexes 2' added.")]
    public void Workbench_support_add_dialog_action_routes_render_named_shells_and_committed_results(
        string route,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedCommittedResult)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual(expectedWorkflow, shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedCommittedResult, cut.Find("[data-workbench-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith(expectedWorkflowLabel, StringComparison.Ordinal));
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=create_entry&dialog_action=add", "create_entry", "add", "Entry 'New entry' added.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=edit_entry&dialog_action=apply", "edit_entry", "apply", "Entry renamed to 'Current Entry'.")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=delete_entry&dialog_action=delete", "delete_entry", "delete", "Entry 'Current Entry' removed.")]
    public void Workbench_career_dialog_action_routes_render_career_shell_and_committed_results(
        string route,
        string expectedControlId,
        string expectedDialogAction,
        string expectedCommittedResult)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.AreEqual(expectedDialogAction, presenter.ExecutedDialogActionId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("career", shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedCommittedResult, cut.Find("[data-workbench-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsTrue(cut.Find(".classic-chummer-titlebar-title").TextContent.Trim().EndsWith("Career", StringComparison.Ordinal));
        Assert.AreEqual("Career", cut.Find(".classic-chummer-status span:last-child em").TextContent.Trim());
        Assert.AreEqual("Career", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Career shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the compatibility route.");
    }

    [TestMethod]
    public void Workbench_character_roster_command_marks_roster_surface_as_active()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/workbench?command=character_roster");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("character_roster", presenter.ExecutedCommandId);

            var shell = cut.Find("section.classic-chummer-shell[data-route-segment='workbench']");
            Assert.AreEqual("character-roster", shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual("runner-active", shell.GetAttribute("data-roster-selected-node"));
            Assert.IsNotNull(cut.Find("aside.classic-chummer-roster.is-roster-command"));
        });
    }

    [DataTestMethod]
    [DataRow("new_character_origin")]
    [DataRow("open_character")]
    [DataRow("open_for_printing")]
    [DataRow("open_for_export")]
    [DataRow("print_character")]
    [DataRow("export_character")]
    [DataRow("save_character")]
    [DataRow("save_character_as")]
    public void Preview_command_query_bootstraps_shared_startup_command(string commandId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo($"/preview?command={commandId}");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() => Assert.AreEqual(commandId, presenter.ExecutedCommandId));
    }

    [DataTestMethod]
    [DataRow("/preview?fixture=blue&command=save_character", "save_character", "none")]
    [DataRow("/preview?fixture=blue&command=save_character_as", "save_character_as", "none")]
    [DataRow("/preview?fixture=blue&command=save_character_as&dialog_action=download", "save_character_as", "download")]
    [DataRow("/preview?fixture=blue&command=export_character", "export_character", "none")]
    [DataRow("/preview?fixture=blue&command=export_character&dialog_action=download", "export_character", "download")]
    [DataRow("/preview?fixture=blue&command=print_character", "print_character", "none")]
    [DataRow("/preview?fixture=blue&command=print_preview", "print_preview", "none")]
    public void Preview_fixture_output_query_bootstraps_shared_startup_command_and_dialog_action(
        string route,
        string expectedCommandId,
        string expectedDialogAction)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(expectedCommandId, presenter.ExecutedCommandId);
            Assert.AreEqual(expectedDialogAction == "none" ? null : expectedDialogAction, presenter.ExecutedDialogActionId);

            Assert.IsNotNull(cut.Find($".browser-preview-frame [data-chummer-app-startup-command='{expectedCommandId}']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });
    }

    [DataTestMethod]
    [DataRow("/preview?fixture=blue&command=save_character", "Dossier save prepared.")]
    [DataRow("/preview?fixture=blue&command=save_character_as", "Browser dossier download prepared.")]
    [DataRow("/preview?fixture=blue&command=save_character_as&dialog_action=download", "Dossier download ready.")]
    [DataRow("/preview?fixture=blue&command=export_character", "Export package prepared.")]
    [DataRow("/preview?fixture=blue&command=export_character&dialog_action=download", "Export package download ready.")]
    [DataRow("/preview?fixture=blue&command=print_character", "Print preview prepared.")]
    [DataRow("/preview?fixture=blue&command=print_preview", "Print preview opened.")]
    public void Preview_fixture_output_query_renders_committed_result_banner(
        string route,
        string expectedResultText)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ExecutedCommandId);
            var result = cut.Find("[data-preview-route-committed-result]");
            Assert.AreEqual(expectedResultText, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });
    }

    [DataTestMethod]
    [DataRow("/preview?fixture=blue&command=save_character_as&dialog_action=download", "Download Dossier", "Download Dossier shell", "browser dossier download continuation path", "from the preview tools route.")]
    [DataRow("/preview?fixture=blue&command=export_character&dialog_action=download", "Download Export Package", "Download package shell", "export package download continuation path", "from the preview tools route.")]
    [DataRow("/preview?fixture=blue&command=print_preview", "Open Print Preview", "Print preview shell", "print preview continuation path", "from the preview tools route.")]
    [DataRow("/preview?command=open_for_export", "Open Export Staging", "Export staging shell", "shared export staging workflow", "from the preview tools route.")]
    public void Preview_result_routes_render_specific_output_copy(
        string route,
        string expectedCommandLabel,
        string expectedFrameTitle,
        string expectedRouteSummaryFragment,
        string expectedRouteSurfacePhrase)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ExecutedCommandId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedCommandLabel);
        Assert.AreEqual(expectedCommandLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Markup, expectedRouteSummaryFragment);
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, expectedRouteSurfacePhrase);
    }

    [DataTestMethod]
    [DataRow("/preview?fixture=blue&tab=tab-gear", "Gear", "Gear shell", "from the preview tools route.")]
    [DataRow("/preview?fixture=blue&tab=tab-rules", "Rules", "Rules shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-skills", "Skills", "Skills shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-cyberware", "Cyberware", "Cyberware shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-qualities", "Qualities", "Qualities shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-adept", "Adept", "Adept shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-combat", "Combat", "Combat shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician", "Magic", "Magic shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-critter", "Critter", "Critter shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-stats", "Stats", "Stats shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-technomancer", "Matrix", "Matrix shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-contacts", "Contacts", "Contacts shell", "from the preview tools route.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar", "Career", "Career shell", "from the preview tools route.")]
    public void Preview_workspace_tab_routes_render_specific_workflow_shell_copy(
        string route,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedRouteSummaryFragment)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() => Assert.IsNotNull(cut.Find(".desktop-shell")));

        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, expectedRouteSummaryFragment);
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-combat&control=combat_add_weapon", "combat_add_weapon", "Combat", "Combat shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=gear_add", "gear_add", "Gear", "Gear shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=vehicle_add", "vehicle_add", "Gear", "Gear shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=drug_add", "drug_add", "Gear", "Gear shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-combat&control=combat_add_armor", "combat_add_armor", "Combat", "Combat shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-qualities&control=quality_add", "quality_add", "Qualities", "Qualities shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-qualities&control=quality_delete", "quality_delete", "Qualities", "Qualities shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar&control=move_up", "move_up", "Career", "Career shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar&control=move_down", "move_down", "Career", "Career shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-skills&control=skill_add", "skill_add", "Skills", "Skills shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-adept&control=adept_power_add", "adept_power_add", "Adept", "Adept shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=spirit_add", "spirit_add", "Magic", "Magic shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-technomancer&control=matrix_program_add", "matrix_program_add", "Matrix", "Matrix shell")]
    public void Preview_quick_action_control_routes_render_named_shells_and_handle_controls(
        string route,
        string expectedControlId,
        string expectedWorkflowLabel,
        string expectedFrameTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-skills&control=skill_specialize", "skill_specialize")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-skills&control=skill_remove", "skill_remove")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-skills&control=skill_group", "skill_group")]
    public void Preview_skill_control_routes_render_skills_shell_and_handle_controls(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Skills", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Skills shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar&control=create_entry&dialog_action=add", "create_entry", "add", "Entry 'New entry' added.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar&control=edit_entry&dialog_action=apply", "edit_entry", "apply", "Entry renamed to 'Current Entry'.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-calendar&control=delete_entry&dialog_action=delete", "delete_entry", "delete", "Entry 'Current Entry' removed.")]
    public void Preview_career_dialog_action_routes_render_career_shell_and_committed_results(
        string route,
        string expectedControlId,
        string expectedDialogAction,
        string expectedCommittedResult)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.AreEqual(expectedDialogAction, presenter.ExecutedDialogActionId);
            Assert.AreEqual(expectedCommittedResult, cut.Find("[data-preview-route-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Career", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Career shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-stats&control=runner_benchmark", "runner_benchmark")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-stats&control=runner_what_if", "runner_what_if")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-stats&control=runner_cohort_privacy", "runner_cohort_privacy")]
    public void Preview_runner_intelligence_control_routes_render_stats_shell_and_handle_controls(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Stats", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Stats shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Preview_rules_source_control_route_renders_rules_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/preview?workspace=preview-ws&tab=tab-rules&control=show_source");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("show_source", presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Rules", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Rules shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Preview_gear_toggle_control_route_renders_gear_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/preview?workspace=preview-ws&tab=tab-gear&control=toggle_free_paid");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("toggle_free_paid", presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Gear", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Gear shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=gear_edit", "gear_edit")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=gear_delete", "gear_delete")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=gear_mount", "gear_mount")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=gear_source", "gear_source")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=drug_delete", "drug_delete")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=vehicle_edit", "vehicle_edit")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=vehicle_delete", "vehicle_delete")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-gear&control=vehicle_mod_add", "vehicle_mod_add")]
    public void Preview_gear_inventory_control_routes_render_gear_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Gear", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Gear shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-combat&control=combat_reload", "combat_reload", "Combat", "Combat shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-combat&control=combat_damage_track", "combat_damage_track", "Combat", "Combat shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=magic_add", "magic_add", "Magic", "Magic shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=magic_bind", "magic_bind", "Magic", "Magic shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=magic_source", "magic_source", "Magic", "Magic shell")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=magic_delete", "magic_delete", "Magic", "Magic shell")]
    public void Preview_combat_magic_control_routes_render_named_shells(
        string route,
        string expectedControlId,
        string expectedWorkflowLabel,
        string expectedFrameTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-cyberware&control=cyberware_edit", "cyberware_edit")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-cyberware&control=cyberware_delete", "cyberware_delete")]
    public void Preview_cyberware_control_routes_render_cyberware_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Cyberware", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Cyberware shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-contacts&control=contact_edit", "contact_edit")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-contacts&control=contact_remove", "contact_remove")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-contacts&control=contact_connection", "contact_connection")]
    public void Preview_contacts_control_routes_render_contacts_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Contacts", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Contacts shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-info&control=identity_license_add", "identity_license_add")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-info&control=identity_license_edit", "identity_license_edit")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-info&control=identity_license_delete", "identity_license_delete")]
    public void Preview_profile_identity_license_control_routes_render_profile_shell(
        string route,
        string expectedControlId)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Preview_profile_notes_control_route_renders_profile_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/preview?workspace=preview-ws&tab=tab-info&control=open_notes");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Preview_profile_notes_save_route_renders_profile_shell_and_committed_result()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/preview?workspace=preview-ws&tab=tab-info&control=open_notes&dialog_action=save");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);
            Assert.AreEqual("save", presenter.ExecutedDialogActionId);
            Assert.AreEqual("Notes saved.", cut.Find("[data-preview-route-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Profile shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Preview_contact_add_dialog_action_route_renders_contacts_shell_and_committed_result()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/preview?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
            Assert.AreEqual("Contact 'Fixer' added.", cut.Find("[data-preview-route-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Contacts", cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual("Contacts shell", cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [DataTestMethod]
    [DataRow("/preview?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add", "spell_add", "Magic", "Magic shell", "Spell 'Stunbolt' added.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-critter&control=critter_power_add&dialog_action=add", "critter_power_add", "Critter", "Critter shell", "Critter power 'Natural Weapon' added.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add", "complex_form_add", "Matrix", "Matrix shell", "Complex form 'Cleaner' added.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add", "initiation_add", "Adept", "Adept shell", "Initiation/submersion reward 'Masking' added.")]
    [DataRow("/preview?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add", "cyberware_add", "Cyberware", "Cyberware shell", "Cyberware 'Wired Reflexes 2' added.")]
    public void Preview_support_add_dialog_action_routes_render_named_shells_and_committed_results(
        string route,
        string expectedControlId,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedCommittedResult)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
            Assert.AreEqual(expectedCommittedResult, cut.Find("[data-preview-route-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-preview-frame-kicker").TextContent.Trim());
        Assert.AreEqual(expectedFrameTitle, cut.Find(".browser-preview-frame-header h2").TextContent.Trim());
        StringAssert.Contains(cut.Find(".browser-preview-frame-header").TextContent, "from the preview tools route.");
    }

    [TestMethod]
    public void Workbench_workspace_query_bootstraps_shared_workspace_load()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() => Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value));
    }

    [TestMethod]
    public void Workbench_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [TestMethod]
    public void Workbench_advanced_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("complex_form_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_advanced_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("complex_form_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [TestMethod]
    public void Workbench_initiation_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-adept&control=initiation_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("initiation_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_initiation_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("initiation_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [TestMethod]
    public void Workbench_cyberware_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("cyberware_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_cyberware_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("cyberware_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [TestMethod]
    public void Workbench_spell_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-magician&control=spell_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("spell_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_spell_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("spell_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [TestMethod]
    public void Workbench_critter_power_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-critter&control=critter_power_add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("critter_power_add", presenter.HandledUiControlId);
        });
    }

    [TestMethod]
    public void Workbench_critter_power_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-critter&control=critter_power_add&dialog_action=add");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("critter_power_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
        });
    }

    [DataTestMethod]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=create_entry", "create_entry", null)]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=edit_entry", "edit_entry", null)]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=delete_entry", "delete_entry", null)]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=move_up", "move_up", null)]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=move_down", "move_down", null)]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=create_entry&dialog_action=add", "create_entry", "add")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=edit_entry&dialog_action=apply", "edit_entry", "apply")]
    [DataRow("/workbench?workspace=preview-ws&tab=tab-calendar&control=delete_entry&dialog_action=delete", "delete_entry", "delete")]
    public void Workbench_career_control_queries_bootstrap_shared_route_state(
        string route,
        string expectedControlId,
        string? expectedDialogAction)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(route);

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedControlId, presenter.HandledUiControlId);
            Assert.AreEqual(expectedDialogAction, presenter.ExecutedDialogActionId);
        });
    }

    private static FakeCharacterOverviewPresenter RegisterDesktopShellServices(BunitContext context, bool includeActiveWorkspace = true)
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);
        CharacterWorkspaceId workspaceId = new("preview-ws");
        FakeCharacterOverviewPresenter presenter = new();

        if (includeActiveWorkspace)
        {
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

            presenter.Publish(overviewState);
            RegisterDesktopShellServices(context, presenter, shellState);
            return presenter;
        }

        presenter.Publish(CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: []),
            Commands = [menuRoot]
        });
        RegisterDesktopShellServices(context, presenter, ShellState.Empty with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = [],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        });
        return presenter;
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        FakeCharacterOverviewPresenter presenter,
        ShellState shellState)
    {
        context.Services.AddSingleton<ICharacterOverviewPresenter>(presenter);
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
        context.Services.AddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
            HostedBuildPrivacyLifecycleCapabilities.Instance);
    }

    private static void AssertBlockContains(string source, string selector, string expectedSnippet)
    {
        int selectorIndex = source.IndexOf(selector, StringComparison.Ordinal);
        Assert.IsTrue(selectorIndex >= 0, $"Expected selector '{selector}' was not found.");

        int nextBraceIndex = source.IndexOf('}', selectorIndex);
        Assert.IsTrue(nextBraceIndex > selectorIndex, $"Expected selector '{selector}' to have a closing brace.");

        string block = source.Substring(selectorIndex, nextBraceIndex - selectorIndex + 1);
        StringAssert.Contains(block, expectedSnippet);
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
