#nullable enable annotations

using Bunit;
using Chummer.Application.Owners;
using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Services;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AppRouteSurfaceTests
{
    private const string CharacterRosterLandingSummary = "Find, group, and organize existing dossiers.";

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
        StringAssert.Contains(cut.Markup, CharacterRosterLandingSummary);
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

    [TestMethod]
    public void Hosted_blazor_workbench_origin_route_renders_clean_origin_dossier_result_cta()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=new_character_origin");

        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "data-ssr-workbench-fallback=\"true\"");
        StringAssert.Contains(markup, "data-command=\"new_character_origin\"");
        StringAssert.Contains(markup, "data-active-workflow=\"origin-dossier\"");
        StringAssert.Contains(markup, "Continue Origin Dossier on the clean route.");
        StringAssert.Contains(markup, "href=\"/app?command=new_character_origin\"");
        StringAssert.Contains(markup, "Open clean Origin Dossier route");
        Assert.IsFalse(markup.Contains("Continue this workflow on Chummer Online", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_route_renders_ssr_fallback_shell_and_bootstrap_script()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=new_character_origin");

        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "data-ssr-workbench-fallback=\"true\"");
        StringAssert.Contains(markup, "data-command=\"new_character_origin\"");
        StringAssert.Contains(markup, "data-tab=\"none\"");
        StringAssert.Contains(markup, "data-active-workflow=\"origin-dossier\"");
        StringAssert.Contains(markup, "data-output-workflow=\"none\"");
        StringAssert.Contains(markup, "data-output-state=\"idle\"");
        StringAssert.Contains(markup, "data-output-target=\"none\"");
        StringAssert.Contains(markup, "data-route-family=\"compatibility\"");
        StringAssert.Contains(markup, "data-route-segment=\"workbench\"");
        StringAssert.Contains(markup, "data-route-surface=\"compatibility\"");
        StringAssert.Contains(markup, "data-route-alias=\"none\"");
        StringAssert.Contains(markup, "data-active-runner=\"BLUE\"");
        StringAssert.Contains(markup, "data-roster-selected-node=\"none\"");
        StringAssert.Contains(markup, "data-dossier-state=\"origin-draft\"");
        StringAssert.Contains(markup, "data-dossier-storage=\"local\"");
        StringAssert.Contains(markup, "data-validation-state=\"review-required\"");
        StringAssert.Contains(markup, "data-privacy-mode=\"local-first\"");
        StringAssert.Contains(markup, "data-analytics-scope=\"route-workflow-only\"");
        StringAssert.Contains(markup, "data-hosting-mode=\"hosted-or-self-hosted\"");
        StringAssert.Contains(markup, "data-deployment-target=\"compatibility\"");
        StringAssert.Contains(markup, "data-self-hostable=\"true\"");
        StringAssert.Contains(markup, "data-container-target=\"docker\"");
        StringAssert.Contains(markup, "data-auth-gate=\"none\"");
        StringAssert.Contains(markup, "data-session-state=\"local-preview\"");
        StringAssert.Contains(markup, "data-login-target=\"none\"");
        StringAssert.Contains(markup, "data-auth-return-policy=\"none\"");
        StringAssert.Contains(markup, "data-client-kind=\"web-desktop\"");
        StringAssert.Contains(markup, "data-parity-target=\"desktop-client\"");
        StringAssert.Contains(markup, "data-calculation-owner=\"shared-chummer-core\"");
        StringAssert.Contains(markup, "data-statistics-runtime=\"reusable-by-avalonia\"");
        StringAssert.Contains(markup, "data-character-statistics=\"enabled\"");
        StringAssert.Contains(markup, "data-statistics-scope=\"anonymized-build-comparisons\"");
        StringAssert.Contains(markup, "data-recommendation-mode=\"explainable-local-inputs\"");
        StringAssert.Contains(markup, "data-recommendation-inputs=\"spells-inventory-drugs-gear-qualities\"");
        StringAssert.Contains(markup, "data-risk-model=\"damage-threshold-probability\"");
        StringAssert.Contains(markup, "data-calculation-boundary=\"shared-engine-only\"");
        StringAssert.Contains(markup, "data-result-consumer=\"blazor-renders-shared-results\"");
        StringAssert.Contains(markup, "data-origin-wizard=\"true\"");
        StringAssert.Contains(markup, ">Origin Dossier<");
        StringAssert.Contains(markup, "Start the story-first dossier path for BLUE.");
        StringAssert.Contains(markup, "Pick only the basics, then build the story. Advanced controls are optional.");
        StringAssert.Contains(markup, "Continue Origin Dossier on the clean route.");
        StringAssert.Contains(markup, "href=\"/app?command=new_character_origin\"");
        StringAssert.Contains(markup, "Open clean Origin Dossier route");
        Assert.IsFalse(markup.Contains("Create the story first. Review it, then continue to a guided build if you want mechanics.", StringComparison.Ordinal));
        StringAssert.Contains(markup, "data-result-route=\"origin-dossier\">/app?command=new_character_origin</code>");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-create&amp;command=new_character\"");
        StringAssert.Contains(markup, "Resume BLUE on SIN/license review");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-info&amp;control=identity_license_edit\"");
        StringAssert.Contains(markup, "window.chummerWorkbenchFallback.observe();");
        StringAssert.Contains(markup, "document.querySelector('[data-ssr-workbench-fallback=\"true\"]')");
        StringAssert.Contains(markup, "section.classic-chummer-shell:not([data-ssr-workbench-fallback])");
        StringAssert.Contains(markup, "const serviceWorkerScript = '/blazor/service-worker.js';");
        StringAssert.Contains(markup, "<footer class=\"classic-chummer-status\" aria-label=\"Classic Chummer status\">");
        StringAssert.Contains(markup, "<strong>Dirty</strong><em>Unsaved dossier</em>");
        StringAssert.Contains(markup, "<strong>Workflow</strong><em>Origin Dossier</em>");
    }

    [TestMethod]
    public void Hosted_blazor_workbench_fallback_restored_actions_preserve_tab_control_and_dialog_routes()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=new_character_origin");

        StringAssert.Contains(markup, "Add and keep contact for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-contacts&amp;control=contact_add&amp;dialog_action=add\"");
        StringAssert.Contains(markup, "Add and keep career entry for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-calendar&amp;control=create_entry&amp;dialog_action=add\"");
        StringAssert.Contains(markup, "Save dossier notes for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-info&amp;control=open_notes&amp;dialog_action=save\"");
        StringAssert.Contains(markup, "Save BLUE in browser");
        StringAssert.Contains(markup, "href=\"/app?workspace=blue-workspace&amp;command=save_character\"");
        StringAssert.Contains(markup, "Download BLUE from browser");
        StringAssert.Contains(markup, "href=\"/app?workspace=blue-workspace&amp;command=save_character_as&amp;dialog_action=download\"");
        StringAssert.Contains(markup, "Download BLUE export package");
        StringAssert.Contains(markup, "href=\"/app?workspace=blue-workspace&amp;command=export_character&amp;dialog_action=download\"");
        StringAssert.Contains(markup, "Add armor for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-combat&amp;control=combat_add_armor\"");
        StringAssert.Contains(markup, "Add Matrix program for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-technomancer&amp;control=matrix_program_add\"");
        StringAssert.Contains(markup, "Remove vehicle for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-gear&amp;control=vehicle_delete\"");
        StringAssert.Contains(markup, "Model Increase Initiative and inventory what-if stack for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-stats&amp;control=runner_what_if\"");
        StringAssert.Contains(markup, "Show source for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-rules&amp;control=show_source\"");
        StringAssert.Contains(markup, "Add general magic item for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-magician&amp;control=magic_add\"");
        StringAssert.Contains(markup, "Remove quality for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-qualities&amp;control=quality_delete\"");
        StringAssert.Contains(markup, "Remove drug for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-gear&amp;control=drug_delete\"");
        StringAssert.Contains(markup, "Add and keep spell for BLUE");
        StringAssert.Contains(markup, "href=\"workbench?workspace=blue-workspace&amp;tab=tab-magician&amp;control=spell_add&amp;dialog_action=add\"");
    }

    [TestMethod]
    public void Hosted_blazor_app_route_renders_blazor_base_href_with_static_assets_and_no_ssr_fallback()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/app?command=character_roster");

        StringAssert.Contains(markup, "<html lang=\"en\">");
        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "href=\"/blazor/manifest.webmanifest\"");
        StringAssert.Contains(markup, "href=\"/blazor/icons/chummer-pwa.svg\"");
        StringAssert.Contains(markup, "href=\"/blazor/app.css\"");
        StringAssert.Contains(markup, "src=\"/blazor/_framework/blazor.web.js\"");
        StringAssert.Contains(markup, "const serviceWorkerScript = '/blazor/service-worker.js';");
        StringAssert.Contains(markup, "const serviceWorkerScope = '/blazor/';");
        StringAssert.Contains(markup, "Character Roster");
        Assert.IsFalse(
            markup.Contains(
                "<section id=\"chummer-online-app\" class=\"classic-chummer-shell\" tabindex=\"-1\" aria-label=\"Chummer Online classic desktop shell\" data-ssr-workbench-fallback=\"true\"",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Clean_public_app_route_uses_root_base_href_while_preserving_hosted_asset_paths()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/app?command=character_roster");

        Assert.AreEqual("/", InvokeString(app, "BuildBaseHref"));
        AssertReleaseAssetHref(
            "/blazor/_framework/blazor.web.js",
            InvokeString(app, "BuildStaticAssetHref", "_framework/blazor.web.js"));
        AssertReleaseAssetHref(
            "/blazor/app.css",
            InvokeString(app, "BuildStaticAssetHref", "app.css"));
        Assert.AreEqual("/blazor/", InvokeString(app, "BuildServiceWorkerScope"));
    }

    [TestMethod]
    public void Hosted_blazor_app_route_keeps_blazor_base_href()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/app?command=character_roster");

        Assert.AreEqual("/blazor/", InvokeString(app, "BuildBaseHref"));
        AssertReleaseAssetHref(
            "/blazor/manifest.webmanifest",
            InvokeString(app, "BuildStaticAssetHref", "manifest.webmanifest"));
        Assert.AreEqual("/blazor/", InvokeString(app, "BuildServiceWorkerScope"));
    }

    [TestMethod]
    public void Clean_online_alias_route_uses_root_base_href_while_preserving_hosted_asset_paths()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/online?command=character_roster");

        Assert.AreEqual("/", InvokeString(app, "BuildBaseHref"));
        AssertReleaseAssetHref(
            "/blazor/_framework/blazor.web.js",
            InvokeString(app, "BuildStaticAssetHref", "_framework/blazor.web.js"));
        AssertReleaseAssetHref(
            "/blazor/service-worker.js",
            InvokeString(app, "BuildStaticAssetHref", "service-worker.js"));
        Assert.AreEqual("/blazor/", InvokeString(app, "BuildServiceWorkerScope"));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_route_keeps_blazor_base_href()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?workspace=preview-ws");

        Assert.AreEqual("/blazor/", InvokeString(app, "BuildBaseHref"));
        AssertReleaseAssetHref(
            "/blazor/_framework/blazor.web.js",
            InvokeString(app, "BuildStaticAssetHref", "_framework/blazor.web.js"));
        Assert.AreEqual("/blazor/", InvokeString(app, "BuildServiceWorkerScope"));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_fallback_uses_normalized_runner_label_for_custom_runner_urls()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&control=gear_edit");

        StringAssert.Contains(markup, "data-workspace=\"storm-ops\"");
        StringAssert.Contains(markup, "data-fixture=\"alpha-beta\"");
        StringAssert.Contains(markup, "data-active-runner=\"Ghost-One\"");
        StringAssert.Contains(markup, "Chummer Online - Ghost-One - Gear");
        StringAssert.Contains(markup, "Resume Ghost-One");
        StringAssert.Contains(markup, "Resume Ghost-One on SIN/license review");
        StringAssert.Contains(markup, "href=\"workbench?workspace=storm-ops&amp;fixture=alpha-beta&amp;runner=Ghost-One&amp;tab=tab-info&amp;control=identity_license_edit\"");
        StringAssert.Contains(markup, "Save Ghost-One in browser");
        StringAssert.Contains(markup, "href=\"/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;command=save_character\"");
        StringAssert.Contains(markup, "Add gear for Ghost-One");
        StringAssert.Contains(markup, "href=\"workbench?workspace=storm-ops&amp;fixture=alpha-beta&amp;runner=Ghost-One&amp;tab=tab-gear&amp;control=gear_add\"");
        StringAssert.Contains(markup, "Download Ghost-One export package");
        StringAssert.Contains(markup, "href=\"/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;command=export_character&amp;dialog_action=download\"");
        Assert.IsFalse(markup.Contains(">Resume BLUE", StringComparison.Ordinal));
        Assert.IsFalse(markup.Contains(" for BLUE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_output_fallback_uses_custom_runner_copy_without_polluting_clean_app_href()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&command=save_character");

        StringAssert.Contains(markup, "data-active-runner=\"Ghost-One\"");
        StringAssert.Contains(markup, "data-fixture=\"alpha-beta\"");
        StringAssert.Contains(markup, "Save dossier workflow is ready for Ghost-One.");
        StringAssert.Contains(markup, "Continue dossier save workflow for Ghost-One.");
        StringAssert.Contains(markup, "href=\"/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;command=save_character\"");
        StringAssert.Contains(markup, "data-result-route=\"save\">/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;command=save_character</code>");
        Assert.IsFalse(markup.Contains("href=\"/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;runner=Ghost-One", StringComparison.Ordinal));
        Assert.IsFalse(markup.Contains("Continue dossier save workflow for BLUE", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("save_character_as", "save", "Download Dossier", "Dossier download ready for Ghost-One.", "Dossier download ready: Ghost-One.chum6", "/app?fixture=alpha-beta&workspace=storm-ops&command=save_character_as&dialog_action=download")]
    [DataRow("export_character", "export", "Download Export Package", "Export package download ready for Ghost-One.", "Export package download ready: Ghost-One", "/app?fixture=alpha-beta&workspace=storm-ops&command=export_character&dialog_action=download")]
    public void Hosted_blazor_workbench_output_dialog_action_fallback_preserves_clean_app_action_continuation(
        string commandId,
        string expectedWorkflow,
        string expectedTitle,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&command={commandId}&dialog_action=download");

        StringAssert.Contains(markup, $"data-command=\"{commandId}\"");
        StringAssert.Contains(markup, "data-dialog-action=\"download\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, $"data-output-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, "data-output-state=\"requested\"");
        StringAssert.Contains(markup, $"Chummer Online - Ghost-One - {expectedTitle}");
        StringAssert.Contains(markup, $"<h2>{expectedTitle}</h2>");
        StringAssert.Contains(markup, expectedSummary);
        StringAssert.Contains(markup, expectedResultText);
        StringAssert.Contains(markup, $"href=\"{expectedResultRouteHref.Replace("&", "&amp;", StringComparison.Ordinal)}\"");
        StringAssert.Contains(markup, $"data-result-route=\"{expectedWorkflow}\">{expectedResultRouteHref}</code>".Replace("&", "&amp;", StringComparison.Ordinal));
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
        Assert.IsFalse(markup.Contains("href=\"/app?fixture=alpha-beta&amp;workspace=storm-ops&amp;runner=Ghost-One", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_open_dossier_route_renders_open_dossier_workflow_metadata()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=open_character");

        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "data-command=\"open_character\"");
        StringAssert.Contains(markup, "data-tab=\"none\"");
        StringAssert.Contains(markup, "data-active-workflow=\"open-dossier\"");
        StringAssert.Contains(markup, ">Open Dossier<");
        StringAssert.Contains(markup, "Open a local runner dossier.");
    }

    [TestMethod]
    public void Hosted_blazor_workbench_save_as_route_renders_save_workflow_result_without_dialog_fallback()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=save_character_as");

        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "data-command=\"save_character_as\"");
        StringAssert.Contains(markup, "data-tab=\"none\"");
        StringAssert.Contains(markup, "data-active-workflow=\"save\"");
        StringAssert.Contains(markup, "data-output-workflow=\"save\"");
        StringAssert.Contains(markup, "data-output-state=\"requested\"");
        StringAssert.Contains(markup, "data-output-target=\"local-dossier\"");
        StringAssert.Contains(markup, "Chummer Online - BLUE - Save Dossier As");
        StringAssert.Contains(markup, "<h2>Save Dossier As</h2>");
        StringAssert.Contains(markup, "Browser dossier download prepared for BLUE.");
        StringAssert.Contains(markup, "data-result-panel-kind=\"save\"");
        StringAssert.Contains(markup, "Download prepared: BLUE.chum6");
        StringAssert.Contains(markup, "href=\"/app?workspace=blue-workspace&amp;command=save_character_as\"");
        StringAssert.Contains(markup, "Continue this workflow on Chummer Online");
        StringAssert.Contains(markup, "data-result-route=\"save\">/app?workspace=blue-workspace&amp;command=save_character_as</code>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_save_route_renders_save_workflow_result_without_dialog_fallback()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=save_character");

        StringAssert.Contains(markup, "<base href=\"/blazor/\" />");
        StringAssert.Contains(markup, "data-command=\"save_character\"");
        StringAssert.Contains(markup, "data-tab=\"none\"");
        StringAssert.Contains(markup, "data-active-workflow=\"save\"");
        StringAssert.Contains(markup, "data-output-workflow=\"save\"");
        StringAssert.Contains(markup, "data-output-state=\"requested\"");
        StringAssert.Contains(markup, "data-output-target=\"local-dossier\"");
        StringAssert.Contains(markup, "Chummer Online - BLUE - Save Dossier");
        StringAssert.Contains(markup, "<h2>Save Dossier</h2>");
        StringAssert.Contains(markup, "Save dossier workflow is ready for BLUE.");
        StringAssert.Contains(markup, "data-result-panel-kind=\"save\"");
        StringAssert.Contains(markup, "Continue dossier save workflow for BLUE.");
        StringAssert.Contains(markup, "href=\"/app?workspace=blue-workspace&amp;command=save_character\"");
        StringAssert.Contains(markup, "Continue this workflow on Chummer Online");
        StringAssert.Contains(markup, "data-result-route=\"save\">/app?workspace=blue-workspace&amp;command=save_character</code>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("new_character", "build-lab", "Continue Build Lab on Chummer Online.", "/app?command=new_character", "New runner", "Build Lab")]
    [DataRow("character_roster", "character-roster", "Continue Character Roster on Chummer Online.", "/app?command=character_roster", "Character Roster", "Character Roster")]
    [DataRow("master_index", "master-index", "Continue Master Index on Chummer Online.", "/app?command=master_index", "Master Index", "Master Index")]
    [DataRow("global_settings", "global-settings", "Continue Global Settings on Chummer Online.", "/app?command=global_settings", "Global Settings", "Global Settings")]
    [DataRow("character_settings", "character-settings", "Continue Character Settings on Chummer Online.", "/app?command=character_settings", "Character Settings", "Character Settings")]
    [DataRow("switch_ruleset", "switch-ruleset", "Continue Switch Ruleset on Chummer Online.", "/app?command=switch_ruleset", "Switch Ruleset", "Switch Ruleset")]
    [DataRow("report_bug", "support", "Continue Support on Chummer Online.", "/app?command=report_bug", "Support and bug reporting", "Support")]
    [DataRow("about", "about", "Continue About Chummer on Chummer Online.", "/app?command=about", "About Chummer", "About Chummer")]
    [DataRow("runtime_inspector", "runtime-inspector", "Continue Runtime Inspector on Chummer Online.", "/app?command=runtime_inspector", "Runtime Inspector", "Runtime Inspector")]
    [DataRow("auto_alice", "assistant", "Continue Assistant on Chummer Online.", "/app?command=auto_alice", "Auto ALICE", "Assistant")]
    [DataRow("translator", "translator", "Continue Translator on Chummer Online.", "/app?command=translator", "Translator", "Translator")]
    [DataRow("xml_editor", "xml-editor", "Continue XML Editor on Chummer Online.", "/app?command=xml_editor", "XML Editor", "XML Editor")]
    [DataRow("hero_lab_importer", "hero-lab-importer", "Continue Hero Lab Importer on Chummer Online.", "/app?command=hero_lab_importer", "Hero Lab Importer", "Hero Lab Importer")]
    [DataRow("dice_roller", "dice-roller", "Continue Dice Roller on Chummer Online.", "/app?command=dice_roller", "Dice Roller", "Dice Roller")]
    [DataRow("data_exporter", "data-exporter", "Continue Data Exporter on Chummer Online.", "/app?command=data_exporter", "Data Exporter", "Data Exporter")]
    [DataRow("print_setup", "print-setup", "Continue Print Setup on Chummer Online.", "/app?command=print_setup", "Print Setup", "Print Setup")]
    [DataRow("print_multiple", "print-multiple", "Continue Print Multiple on Chummer Online.", "/app?command=print_multiple", "Print Multiple", "Print Multiple")]
    [DataRow("update", "update", "Continue Update on Chummer Online.", "/app?command=update", "Check for Updates", "Update")]
    [DataRow("new_window", "new-window", "Continue New Window on Chummer Online.", "/app?command=new_window", "New Window", "New Window")]
    [DataRow("wiki", "wiki", "Continue Wiki on Chummer Online.", "/app?command=wiki", "Wiki", "Wiki")]
    [DataRow("discord", "discord", "Continue Discord on Chummer Online.", "/app?command=discord", "Discord", "Discord")]
    [DataRow("show_login_video", "login-video", "Continue Login Video on Chummer Online.", "/app?command=show_login_video", "Show Login Video", "Login Video")]
    [DataRow("revision_history", "revision-history", "Continue Revision History on Chummer Online.", "/app?command=revision_history", "Revision History", "Revision History")]
    [DataRow("dumpshock", "issue-tracker", "Continue Issue Tracker on Chummer Online.", "/app?command=dumpshock", "Issue Tracker", "Issue Tracker")]
    [DataRow("open_character", "open-dossier", "Continue local dossier import while BLUE stays loaded.", "/app?workspace=blue-workspace&command=open_character", "Open Dossier", "Open Dossier")]
    [DataRow("open_for_printing", "print", "Continue print preview setup for BLUE.", "/app?workspace=blue-workspace&command=open_for_printing", "Open Print Staging", "Open Print Staging")]
    [DataRow("open_for_export", "export", "Continue export dossier setup for BLUE.", "/app?workspace=blue-workspace&command=open_for_export", "Open Export Staging", "Open Export Staging")]
    public void Hosted_blazor_workbench_supported_dialog_routes_render_clean_app_continuations_while_preserving_dialog_fallback(
        string commandId,
        string expectedWorkflow,
        string expectedResultText,
        string expectedResultRouteHref,
        string expectedDialogTitle,
        string expectedWorkflowLabel)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?command={commandId}");

        StringAssert.Contains(markup, $"data-command=\"{commandId}\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, $"Chummer Online - BLUE - {expectedWorkflowLabel}");
        StringAssert.Contains(markup, expectedResultText);
        StringAssert.Contains(markup, "Continue this workflow on Chummer Online");
        StringAssert.Contains(markup, $"href=\"{expectedResultRouteHref.Replace("&", "&amp;", StringComparison.Ordinal)}\"");
        StringAssert.Contains(markup, $"data-result-route=\"{expectedWorkflow}\">{expectedResultRouteHref}</code>".Replace("&", "&amp;", StringComparison.Ordinal));
        StringAssert.Contains(markup, "<section class=\"desktop-dialog\"");
        StringAssert.Contains(markup, $">{expectedDialogTitle}<");
        StringAssert.Contains(markup, $"<strong>Workflow</strong><em>{expectedWorkflowLabel}</em>");
    }

    [DataTestMethod]
    [DataRow("print_character", "print", "Prepare Print Preview", "Print preview prepared for BLUE.", "Print preview prepared: BLUE", "/app?workspace=blue-workspace&command=print_character")]
    [DataRow("print_preview", "print", "Open Print Preview", "Inspect the print preview for BLUE.", "Print preview prepared: BLUE", "/app?workspace=blue-workspace&command=print_preview")]
    [DataRow("export_character", "export", "Prepare Export Package", "Export dossier prepared for BLUE.", "Export Dossier prepared: BLUE", "/app?workspace=blue-workspace&command=export_character")]
    public void Hosted_blazor_workbench_output_routes_render_specific_visible_chrome_without_dialog_fallback(
        string commandId,
        string expectedWorkflow,
        string expectedTitle,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?command={commandId}");

        StringAssert.Contains(markup, $"data-command=\"{commandId}\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, $"Chummer Online - BLUE - {expectedTitle}");
        StringAssert.Contains(markup, $"<h2>{expectedTitle}</h2>");
        StringAssert.Contains(markup, expectedSummary);
        StringAssert.Contains(markup, expectedResultText);
        StringAssert.Contains(markup, $"href=\"{expectedResultRouteHref.Replace("&", "&amp;", StringComparison.Ordinal)}\"");
        StringAssert.Contains(markup, $"<strong>Workflow</strong><em>{expectedTitle}</em>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("copy", "copy", "Copy", "Keep the active editor copy relay visible without dropping back to generic shell chrome.", "Continue Copy on Chummer Online.", "/app?workspace=blue-workspace&command=copy")]
    [DataRow("paste", "paste", "Paste", "Keep the active editor paste relay visible without dropping back to generic shell chrome.", "Continue Paste on Chummer Online.", "/app?workspace=blue-workspace&command=paste")]
    [DataRow("new_critter", "new-critter", "New Critter", "Start a critter-first starter import without leaving the shared shell.", "Continue New Critter on Chummer Online.", "/app?command=new_critter")]
    [DataRow("restart", "restart", "Restart", "Reset open dossiers so the next launch can start clean.", "Continue Restart on Chummer Online.", "/app?workspace=blue-workspace&command=restart")]
    [DataRow("exit", "exit", "Exit", "Keep desktop-only exit posture visible without dropping back to generic shell chrome.", "Continue Exit on Chummer Online.", "/app?command=exit")]
    [DataRow("close_window", "close-window", "Close Window", "Close the active dossier window without dropping back to generic shell chrome.", "Continue Close Window on Chummer Online.", "/app?workspace=blue-workspace&command=close_window")]
    [DataRow("close_all", "close-all", "Close All", "Close all open dossier windows without dropping back to generic shell chrome.", "Continue Close All on Chummer Online.", "/app?workspace=blue-workspace&command=close_all")]
    public void Hosted_blazor_workbench_action_routes_render_specific_visible_chrome_without_dialog_fallback(
        string commandId,
        string expectedWorkflow,
        string expectedTitle,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?command={commandId}");

        StringAssert.Contains(markup, $"data-command=\"{commandId}\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, $"Chummer Online - BLUE - {expectedTitle}");
        StringAssert.Contains(markup, $"<h2>{expectedTitle}</h2>");
        StringAssert.Contains(markup, expectedSummary);
        StringAssert.Contains(markup, expectedResultText);
        StringAssert.Contains(markup, $"href=\"{expectedResultRouteHref.Replace("&", "&amp;", StringComparison.Ordinal)}\"");
        StringAssert.Contains(markup, $"data-result-route=\"{expectedWorkflow}\">{expectedResultRouteHref}</code>".Replace("&", "&amp;", StringComparison.Ordinal));
        StringAssert.Contains(markup, $"<strong>Workflow</strong><em>{expectedTitle}</em>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("open_sourcebooks", "sourcebooks", "Sourcebooks", "Review the active book selection posture.", "Continue Sourcebooks on Chummer Online.", "/app?command=open_sourcebooks")]
    [DataRow("open_errata", "errata", "Errata", "Reserve visible rules-update context.", "Continue Errata on Chummer Online.", "/app?command=open_errata")]
    [DataRow("open_custom_data", "custom-data", "Custom Data", "Keep homebrew and local packs discoverable.", "Continue Custom Data on Chummer Online.", "/app?command=open_custom_data")]
    [DataRow("update_data_packs", "update-pack", "Update Pack", "Show data refresh as an operator action.", "Continue Update Pack on Chummer Online.", "/app?command=update_data_packs")]
    [DataRow("validate_data_scope", "validation-scope", "Validation Scope", "Connect rules data to build readiness.", "Continue Validation Scope on Chummer Online.", "/app?command=validate_data_scope")]
    [DataRow("open_data_folder", "data-folder", "Data Folder", "Keep local and self-host paths visible.", "Continue Data Folder on Chummer Online.", "/app?command=open_data_folder")]
    public void Hosted_blazor_workbench_data_pack_routes_render_specific_visible_chrome_without_dialog_fallback(
        string commandId,
        string expectedWorkflow,
        string expectedTitle,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?command={commandId}");

        StringAssert.Contains(markup, $"data-command=\"{commandId}\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, $"Chummer Online - BLUE - {expectedTitle}");
        StringAssert.Contains(markup, $"<h2>{expectedTitle}</h2>");
        StringAssert.Contains(markup, expectedSummary);
        StringAssert.Contains(markup, expectedResultText);
        StringAssert.Contains(markup, $"href=\"{expectedResultRouteHref.Replace("&", "&amp;", StringComparison.Ordinal)}\"");
        StringAssert.Contains(markup, $"data-result-route=\"{expectedWorkflow}\">{expectedResultRouteHref}</code>".Replace("&", "&amp;", StringComparison.Ordinal));
        StringAssert.Contains(markup, $"<strong>Workflow</strong><em>{expectedTitle}</em>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hosted_blazor_workbench_new_character_route_uses_build_lab_classic_chrome_while_preserving_new_runner_dialog()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?command=new_character");

        StringAssert.Contains(markup, "data-command=\"new_character\"");
        StringAssert.Contains(markup, "data-active-workflow=\"build-lab\"");
        StringAssert.Contains(markup, "data-output-workflow=\"none\"");
        StringAssert.Contains(markup, "data-output-state=\"idle\"");
        StringAssert.Contains(markup, "data-output-target=\"none\"");
        StringAssert.Contains(markup, "data-route-family=\"compatibility\"");
        StringAssert.Contains(markup, "data-route-surface=\"compatibility\"");
        StringAssert.Contains(markup, "data-route-alias=\"none\"");
        StringAssert.Contains(markup, "Chummer Online - BLUE - Build Lab");
        StringAssert.Contains(markup, "<h2>Build Lab</h2>");
        StringAssert.Contains(markup, "<strong>Workflow</strong><em>Build Lab</em>");
        StringAssert.Contains(markup, "<section class=\"desktop-dialog\"");
        StringAssert.Contains(markup, ">New runner<");
        StringAssert.Contains(markup, "Continue Build Lab on Chummer Online.");
    }

    [TestMethod]
    public void Hosted_blazor_workbench_control_dialog_route_uses_workflow_classic_chrome_while_preserving_dialog_title()
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/blazor/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&control=complex_form_add");

        StringAssert.Contains(markup, "data-control=\"complex_form_add\"");
        StringAssert.Contains(markup, "data-active-workflow=\"matrix\"");
        StringAssert.Contains(markup, "data-output-workflow=\"none\"");
        StringAssert.Contains(markup, "data-output-state=\"idle\"");
        StringAssert.Contains(markup, "data-output-target=\"none\"");
        StringAssert.Contains(markup, "Chummer Online - Ghost-One - Matrix");
        StringAssert.Contains(markup, "<h2>Matrix</h2>");
        StringAssert.Contains(markup, "<strong>Workflow</strong><em>Matrix</em>");
        StringAssert.Contains(markup, "<section class=\"desktop-dialog\"");
        StringAssert.Contains(markup, ">Add Complex Form<");
    }

    [DataTestMethod]
    [DataRow("contact_add", "add", "contacts", "Contact 'Fixer' added.")]
    [DataRow("critter_power_add", "add", "critter", "Critter power 'Natural Weapon' added.")]
    public void Hosted_blazor_workbench_committed_result_fallback_renders_supported_result_banner(
        string controlId,
        string dialogAction,
        string expectedWorkflow,
        string expectedCommittedResult)
    {
        string markup = RenderAppMarkup(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/blazor/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&control={controlId}&dialog_action={dialogAction}");

        StringAssert.Contains(markup, $"data-control=\"{controlId}\"");
        StringAssert.Contains(markup, $"data-dialog-action=\"{dialogAction}\"");
        StringAssert.Contains(markup, $"data-active-workflow=\"{expectedWorkflow}\"");
        StringAssert.Contains(markup, "data-workbench-committed-result=");
        StringAssert.Contains(markup, $"<strong>{expectedCommittedResult}</strong>");
        Assert.IsFalse(markup.Contains("<section class=\"desktop-dialog\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workbench_fallback_normalizes_query_tokens_and_emits_sanitized_relative_hrefs()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/workbench?workspace=storm ops&runner=Ghost/One&fixture=alpha beta&control=complex_form_add");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the complex-form route.");

        Assert.AreEqual("storm-ops", GetPropertyValue<string>(fallback, "Workspace"));
        Assert.AreEqual("Ghost-One", GetPropertyValue<string>(fallback, "Runner"));
        Assert.AreEqual("alpha-beta", GetPropertyValue<string>(fallback, "Fixture"));
        Assert.AreEqual("tab-technomancer", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual("Matrix", GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "SelectedRosterNode"));
        Assert.AreEqual("unsaved", GetPropertyValue<string>(fallback, "DossierState"));

        Assert.AreEqual(
            "workbench?workspace=storm-ops&tab=tab-adept&control=initiation_add&dialog_action=add",
            InvokeStaticString(
                "BuildWorkbenchHref",
                "storm ops",
                "tab-adept",
                null,
                "initiation_add",
                "add",
                null));
        Assert.AreEqual(
            "workbench?workspace=storm-ops&runner=Ghost-One&tab=tab-adept&control=initiation_add&dialog_action=add",
            InvokeStaticString(
                "BuildWorkbenchHref",
                "storm ops",
                "tab-adept",
                null,
                "initiation_add",
                "add",
                "Ghost/One"));
        Assert.AreEqual(
            "/app?workspace=storm-ops&command=save_character_as",
            InvokeStaticString(
                "BuildPublicAppHref",
                "storm ops",
                null,
                "save_character_as"));
        Assert.AreEqual(
            "/app?workspace=storm-ops&command=save_character_as&dialog_action=download",
            InvokeStaticString(
                "BuildPublicAppHref",
                "storm ops",
                null,
                "save_character_as",
                "download"));
        Assert.AreEqual(
            "/app?fixture=alpha-beta&workspace=storm-ops&command=save_character_as&dialog_action=download",
            InvokeStaticString(
                "BuildPublicAppHref",
                "alpha beta",
                "storm ops",
                null,
                "save_character_as",
                "download"));
    }

    [TestMethod]
    public void Workbench_origin_dossier_query_builds_story_first_fallback_dialog()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/workbench?command=new_character_origin");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the origin-dossier compatibility route.");
        object dialog = GetPropertyValue<object>(fallback, "Dialog")
            ?? throw new AssertFailedException("Expected the origin-dossier compatibility route to expose a dialog payload.");

        Assert.AreEqual("new_character_origin", GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual("origin-dossier", GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("Origin Dossier", GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual("Origin Dossier", GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual("Start the story-first dossier path for BLUE.", GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual("Continue Origin Dossier on the clean route.", GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual("/app?command=new_character_origin", GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "CommittedResult"));
        Assert.AreEqual("Origin Dossier", GetPropertyValue<string>(dialog, "Title"));
        Assert.IsTrue(GetPropertyValue<bool>(dialog, "IsOriginWizard"));
        CollectionAssert.Contains(
            (System.Collections.ICollection?)GetPropertyValue<object>(dialog, "Lines")
                ?? throw new AssertFailedException("Expected origin dialog lines to be present."),
            "Pick only the basics, then build the story. Advanced controls are optional.");
        CollectionAssert.Contains(
            (System.Collections.ICollection?)GetPropertyValue<object>(dialog, "Lines")
                ?? throw new AssertFailedException("Expected origin dialog lines to be present."),
            "Story Preview");
        CollectionAssert.DoesNotContain(
            (System.Collections.ICollection?)GetPropertyValue<object>(dialog, "Lines")
                ?? throw new AssertFailedException("Expected origin dialog lines to be present."),
            "Create the story first. Review it, then continue to a guided build if you want mechanics.");
    }

    [DataTestMethod]
    [DataRow("create_entry", "add", "tab-calendar", "career", "Entry 'New entry' added.")]
    [DataRow("edit_entry", "apply", "tab-calendar", "career", "Entry renamed to 'Current Entry'.")]
    [DataRow("delete_entry", "delete", "tab-calendar", "career", "Entry 'Current Entry' removed.")]
    [DataRow("open_notes", "save", "tab-info", "profile", "Notes saved.")]
    [DataRow("contact_add", "add", "tab-contacts", "contacts", "Contact 'Fixer' added.")]
    [DataRow("complex_form_add", "add", "tab-technomancer", "matrix", "Complex form 'Cleaner' added.")]
    [DataRow("initiation_add", "add", "tab-adept", "adept", "Initiation/submersion reward 'Masking' added.")]
    [DataRow("cyberware_add", "add", "tab-cyberware", "cyberware", "Cyberware 'Wired Reflexes 2' added.")]
    [DataRow("spell_add", "add", "tab-magician", "magic", "Spell 'Stunbolt' added.")]
    [DataRow("critter_power_add", "add", "tab-critter", "critter", "Critter power 'Natural Weapon' added.")]
    public void Workbench_committed_result_query_prefers_result_banner_over_dialog_payload(
        string controlId,
        string dialogAction,
        string expectedTab,
        string expectedWorkflow,
        string expectedCommittedResult)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?workspace=preview-ws&control={controlId}&dialog_action={dialogAction}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the committed-result route.");

        Assert.AreEqual(controlId, GetPropertyValue<string>(fallback, "Control"));
        Assert.AreEqual(dialogAction, GetPropertyValue<string>(fallback, "DialogAction"));
        Assert.AreEqual(expectedTab, GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual(expectedCommittedResult, GetPropertyValue<string>(fallback, "CommittedResult"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "Dialog"));
    }

    [TestMethod]
    public void Workbench_new_character_query_defaults_to_blue_build_lab_identity()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/workbench?command=new_character");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the new-runner route.");

        Assert.AreEqual("new_character", GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual("build-lab", GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("blue-workspace", GetPropertyValue<string>(fallback, "Workspace"));
        Assert.AreEqual("blue", GetPropertyValue<string>(fallback, "Fixture"));
        Assert.AreEqual("BLUE", GetPropertyValue<string>(fallback, "Runner"));
        Assert.AreEqual("Build Lab", GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual("Build Lab", GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual("Continue Build Lab for BLUE.", GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual("Continue Build Lab on Chummer Online.", GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual("/app?command=new_character", GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "SelectedRosterNode"));
        Assert.AreEqual("unsaved", GetPropertyValue<string>(fallback, "DossierState"));
        Assert.AreEqual("local", GetPropertyValue<string>(fallback, "DossierStorage"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "RouteFamily"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "RouteSurface"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "RouteAlias"));
        Assert.AreEqual("review-required", GetPropertyValue<string>(fallback, "ValidationState"));
        Assert.AreEqual("local-first", GetPropertyValue<string>(fallback, "PrivacyMode"));
        Assert.AreEqual("route-workflow-only", GetPropertyValue<string>(fallback, "AnalyticsScope"));
        Assert.AreEqual("hosted-or-self-hosted", GetPropertyValue<string>(fallback, "HostingMode"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "DeploymentTarget"));
        Assert.AreEqual("true", GetPropertyValue<string>(fallback, "SelfHostable"));
        Assert.AreEqual("docker", GetPropertyValue<string>(fallback, "ContainerTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "AuthGate"));
        Assert.AreEqual("local-preview", GetPropertyValue<string>(fallback, "SessionState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "LoginTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "AuthReturnPolicy"));
        Assert.AreEqual("web-desktop", GetPropertyValue<string>(fallback, "ClientKind"));
        Assert.AreEqual("desktop-client", GetPropertyValue<string>(fallback, "ParityTarget"));
        Assert.AreEqual("shared-chummer-core", GetPropertyValue<string>(fallback, "CalculationOwner"));
        Assert.AreEqual("reusable-by-avalonia", GetPropertyValue<string>(fallback, "StatisticsRuntime"));
        Assert.AreEqual("enabled", GetPropertyValue<string>(fallback, "CharacterStatistics"));
        Assert.AreEqual("anonymized-build-comparisons", GetPropertyValue<string>(fallback, "StatisticsScope"));
        Assert.AreEqual("explainable-local-inputs", GetPropertyValue<string>(fallback, "RecommendationMode"));
        Assert.AreEqual("spells-inventory-drugs-gear-qualities", GetPropertyValue<string>(fallback, "RecommendationInputs"));
        Assert.AreEqual("damage-threshold-probability", GetPropertyValue<string>(fallback, "RiskModel"));
        Assert.AreEqual("shared-engine-only", GetPropertyValue<string>(fallback, "CalculationBoundary"));
        Assert.AreEqual("blazor-renders-shared-results", GetPropertyValue<string>(fallback, "ResultConsumer"));
    }

    [TestMethod]
    public void Workbench_character_roster_query_uses_character_roster_workflow_identity()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/workbench?command=character_roster");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the character-roster route.");
        object dialog = GetPropertyValue<object>(fallback, "Dialog")
            ?? throw new AssertFailedException("Expected the character-roster route to expose a dialog payload.");

        Assert.AreEqual("character_roster", GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual("character-roster", GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual("runner-active", GetPropertyValue<string>(fallback, "SelectedRosterNode"));
        Assert.AreEqual("unsaved", GetPropertyValue<string>(fallback, "DossierState"));
        Assert.AreEqual("Character Roster", GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual("Character Roster", GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual("Group dossiers into your own folders.", GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual("Continue Character Roster on Chummer Online.", GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual("/app?command=character_roster", GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.AreEqual("Character Roster", GetPropertyValue<string>(dialog, "Title"));
        CollectionAssert.Contains(
            (System.Collections.ICollection?)GetPropertyValue<object>(dialog, "Lines")
                ?? throw new AssertFailedException("Expected character roster dialog lines to be present."),
            "Dossier Status");
    }

    [TestMethod]
    public void Workbench_master_index_query_uses_master_index_workflow_identity()
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: "https://chummer.run/workbench?command=master_index");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException("Expected a workbench fallback for the master-index route.");
        object dialog = GetPropertyValue<object>(fallback, "Dialog")
            ?? throw new AssertFailedException("Expected the master-index route to expose a dialog payload.");

        Assert.AreEqual("master_index", GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual("master-index", GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual("Master Index", GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual("Master Index", GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual("Search rules, gear, qualities, spells, and references.", GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual("Continue Master Index on Chummer Online.", GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual("/app?command=master_index", GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.AreEqual("Master Index", GetPropertyValue<string>(dialog, "Title"));
    }

    [DataTestMethod]
    [DataRow("global_settings", "global-settings", "Global Settings", "Update language, UI scale, and dossier defaults.", "Continue Global Settings on Chummer Online.", "/app?command=global_settings")]
    [DataRow("character_settings", "character-settings", "Character Settings", "Edit the current character-setting defaults used when creating and validating dossiers.", "Continue Character Settings on Chummer Online.", "/app?command=character_settings")]
    [DataRow("switch_ruleset", "switch-ruleset", "Switch Ruleset", "Set the preferred ruleset used when no workspace is active.", "Continue Switch Ruleset on Chummer Online.", "/app?command=switch_ruleset")]
    [DataRow("report_bug", "support", "Support and bug reporting", "Open tracked support, guest support, or the public GitHub issue form.", "Continue Support on Chummer Online.", "/app?command=report_bug")]
    [DataRow("about", "about", "About Chummer", "Review the shared preview/runtime identity path.", "Continue About Chummer on Chummer Online.", "/app?command=about")]
    [DataRow("runtime_inspector", "runtime-inspector", "Runtime Inspector", "Inspect the resolved runtime, rule packs, and provider bindings.", "Continue Runtime Inspector on Chummer Online.", "/app?command=runtime_inspector")]
    [DataRow("auto_alice", "assistant", "Auto ALICE", "Plan a build, explain rules posture, or prepare an explicit handoff without mutating the runner silently.", "Continue Assistant on Chummer Online.", "/app?command=auto_alice")]
    [DataRow("translator", "translator", "Translator", "Search languages and review enabled overlays.", "Continue Translator on Chummer Online.", "/app?command=translator")]
    [DataRow("xml_editor", "xml-editor", "XML Editor", "Review XML bridge and custom data posture before editing local payloads.", "Continue XML Editor on Chummer Online.", "/app?command=xml_editor")]
    [DataRow("hero_lab_importer", "hero-lab-importer", "Hero Lab Importer", "Import Hero Lab XML while import-oracle posture stays visible.", "Continue Hero Lab Importer on Chummer Online.", "/app?command=hero_lab_importer")]
    [DataRow("dice_roller", "dice-roller", "Dice Roller", "Choose a roll method, threshold, and reroll options.", "Continue Dice Roller on Chummer Online.", "/app?command=dice_roller")]
    [DataRow("data_exporter", "data-exporter", "Data Exporter", "Export pipeline is routed through API tool endpoints.", "Continue Data Exporter on Chummer Online.", "/app?command=data_exporter")]
    [DataRow("print_setup", "print-setup", "Print Setup", "Printer setup is delegated to host/browser print capabilities.", "Continue Print Setup on Chummer Online.", "/app?command=print_setup")]
    [DataRow("print_multiple", "print-multiple", "Print Multiple", "Batch print is available through roster and print endpoints.", "Continue Print Multiple on Chummer Online.", "/app?command=print_multiple")]
    [DataRow("update", "update", "Check for Updates", "See where this copy gets updates, how it behaves when a newer build is available, and where support picks up if an update needs help.", "Continue Update on Chummer Online.", "/app?command=update")]
    [DataRow("new_window", "new-window", "New Window", "Open a second shell instance from your platform runtime.", "Continue New Window on Chummer Online.", "/app?command=new_window")]
    [DataRow("wiki", "wiki", "Wiki", "Use the legacy wiki as an external reference without displacing the current view.", "Continue Wiki on Chummer Online.", "/app?command=wiki")]
    [DataRow("discord", "discord", "Discord", "Community chat opens in the browser instead of replacing the desktop view.", "Continue Discord on Chummer Online.", "/app?command=discord")]
    [DataRow("show_login_video", "login-video", "Show Login Video", "The Avalonia desktop host opens the Matrix uplink login video on demand, including after the install is already linked.", "Continue Login Video on Chummer Online.", "/app?command=show_login_video")]
    [DataRow("revision_history", "revision-history", "Revision History", "Release notes open as an external help surface.", "Continue Revision History on Chummer Online.", "/app?command=revision_history")]
    [DataRow("dumpshock", "issue-tracker", "Issue Tracker", "The Chummer6 issue tracker opens externally and stays outside the desktop view.", "Continue Issue Tracker on Chummer Online.", "/app?command=dumpshock")]
    public void Workbench_tool_command_queries_publish_specific_tool_workflow_identity(
        string commandId,
        string expectedWorkflow,
        string expectedHeading,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?command={commandId}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{commandId}'.");
        object dialog = GetPropertyValue<object>(fallback, "Dialog")
            ?? throw new AssertFailedException($"Expected the '{commandId}' route to expose a dialog payload.");

        Assert.AreEqual(commandId, GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual(expectedSummary, GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual(expectedResultText, GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual(expectedResultRouteHref, GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(dialog, "Title"));
    }

    [DataTestMethod]
    [DataRow("open_sourcebooks", "sourcebooks", "Sourcebooks", "Review the active book selection posture.", "Continue Sourcebooks on Chummer Online.", "/app?command=open_sourcebooks")]
    [DataRow("open_errata", "errata", "Errata", "Reserve visible rules-update context.", "Continue Errata on Chummer Online.", "/app?command=open_errata")]
    [DataRow("open_custom_data", "custom-data", "Custom Data", "Keep homebrew and local packs discoverable.", "Continue Custom Data on Chummer Online.", "/app?command=open_custom_data")]
    [DataRow("update_data_packs", "update-pack", "Update Pack", "Show data refresh as an operator action.", "Continue Update Pack on Chummer Online.", "/app?command=update_data_packs")]
    [DataRow("validate_data_scope", "validation-scope", "Validation Scope", "Connect rules data to build readiness.", "Continue Validation Scope on Chummer Online.", "/app?command=validate_data_scope")]
    [DataRow("open_data_folder", "data-folder", "Data Folder", "Keep local and self-host paths visible.", "Continue Data Folder on Chummer Online.", "/app?command=open_data_folder")]
    public void Workbench_data_pack_command_queries_publish_expected_workflow_identity_without_dialog_payload(
        string commandId,
        string expectedWorkflow,
        string expectedHeading,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?command={commandId}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{commandId}'.");

        Assert.AreEqual(commandId, GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual(expectedSummary, GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual(expectedResultText, GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual(expectedResultRouteHref, GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "Dialog"));
    }

    [DataTestMethod]
    [DataRow("copy", "copy", "Copy", "Keep the active editor copy relay visible without dropping back to generic shell chrome.", "Continue Copy on Chummer Online.", "/app?workspace=blue-workspace&command=copy")]
    [DataRow("paste", "paste", "Paste", "Keep the active editor paste relay visible without dropping back to generic shell chrome.", "Continue Paste on Chummer Online.", "/app?workspace=blue-workspace&command=paste")]
    [DataRow("new_critter", "new-critter", "New Critter", "Start a critter-first starter import without leaving the shared shell.", "Continue New Critter on Chummer Online.", "/app?command=new_critter")]
    [DataRow("restart", "restart", "Restart", "Reset open dossiers so the next launch can start clean.", "Continue Restart on Chummer Online.", "/app?workspace=blue-workspace&command=restart")]
    [DataRow("exit", "exit", "Exit", "Keep desktop-only exit posture visible without dropping back to generic shell chrome.", "Continue Exit on Chummer Online.", "/app?command=exit")]
    [DataRow("close_window", "close-window", "Close Window", "Close the active dossier window without dropping back to generic shell chrome.", "Continue Close Window on Chummer Online.", "/app?workspace=blue-workspace&command=close_window")]
    [DataRow("close_all", "close-all", "Close All", "Close all open dossier windows without dropping back to generic shell chrome.", "Continue Close All on Chummer Online.", "/app?workspace=blue-workspace&command=close_all")]
    public void Workbench_action_command_queries_publish_expected_workflow_identity_without_dialog_payload(
        string commandId,
        string expectedWorkflow,
        string expectedHeading,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?command={commandId}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{commandId}'.");

        Assert.AreEqual(commandId, GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual("idle", GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual(expectedSummary, GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual(expectedResultText, GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual(expectedResultRouteHref, GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "Dialog"));
    }

    [DataTestMethod]
    [DataRow("open_character", "open-dossier", "Open Dossier", "Open a local runner dossier.", "Continue local dossier import while BLUE stays loaded.", "/app?workspace=blue-workspace&command=open_character")]
    [DataRow("save_character", "save", "Save Dossier", "Save dossier workflow is ready for BLUE.", "Continue dossier save workflow for BLUE.", "/app?workspace=blue-workspace&command=save_character")]
    [DataRow("save_character_as", "save", "Save Dossier As", "Browser dossier download prepared for BLUE.", "Download prepared: BLUE.chum6", "/app?workspace=blue-workspace&command=save_character_as")]
    [DataRow("open_for_printing", "print", "Open Print Staging", "Prepare a print preview for BLUE.", "Continue print preview setup for BLUE.", "/app?workspace=blue-workspace&command=open_for_printing")]
    [DataRow("print_preview", "print", "Open Print Preview", "Inspect the print preview for BLUE.", "Print preview prepared: BLUE", "/app?workspace=blue-workspace&command=print_preview")]
    [DataRow("print_character", "print", "Prepare Print Preview", "Print preview prepared for BLUE.", "Print preview prepared: BLUE", "/app?workspace=blue-workspace&command=print_character")]
    [DataRow("open_for_export", "export", "Open Export Staging", "Prepare an export dossier for BLUE.", "Continue export dossier setup for BLUE.", "/app?workspace=blue-workspace&command=open_for_export")]
    [DataRow("export_character", "export", "Prepare Export Package", "Export dossier prepared for BLUE.", "Export Dossier prepared: BLUE", "/app?workspace=blue-workspace&command=export_character")]
    public void Workbench_output_command_queries_publish_expected_workflow_identity(
        string commandId,
        string expectedWorkflow,
        string expectedHeading,
        string expectedSummary,
        string? expectedResultText,
        string? expectedResultRouteHref)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?command={commandId}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{commandId}'.");

        Assert.AreEqual(commandId, GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual(expectedSummary, GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual(expectedResultText, GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual(expectedResultRouteHref, GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "CommittedResult"));
    }

    [DataTestMethod]
    [DataRow("save_character_as", "download", "save", "Download Dossier", "Dossier download ready for BLUE.", "Dossier download ready: BLUE.chum6", "/app?workspace=blue-workspace&command=save_character_as&dialog_action=download")]
    [DataRow("export_character", "download", "export", "Download Export Package", "Export package download ready for BLUE.", "Export package download ready: BLUE", "/app?workspace=blue-workspace&command=export_character&dialog_action=download")]
    public void Workbench_output_dialog_action_queries_publish_specific_download_heading_and_continuation(
        string commandId,
        string dialogAction,
        string expectedWorkflow,
        string expectedHeading,
        string expectedSummary,
        string expectedResultText,
        string expectedResultRouteHref)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?command={commandId}&dialog_action={dialogAction}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{commandId}' with '{dialogAction}'.");

        Assert.AreEqual(commandId, GetPropertyValue<string>(fallback, "Command"));
        Assert.AreEqual(dialogAction, GetPropertyValue<string>(fallback, "DialogAction"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual(expectedSummary, GetPropertyValue<string>(fallback, "SectionSummary"));
        Assert.AreEqual(expectedResultText, GetPropertyValue<string>(fallback, "ResultText"));
        Assert.AreEqual(expectedResultRouteHref, GetPropertyValue<string>(fallback, "ResultRouteHref"));
        Assert.IsNull(GetPropertyValue<object>(fallback, "CommittedResult"));
    }

    [DataTestMethod]
    [DataRow("contact_add", "tab-contacts", "contacts", "Contacts")]
    [DataRow("gear_edit", "tab-gear", "gear", "Gear")]
    [DataRow("combat_reload", "tab-combat", "combat", "Combat")]
    [DataRow("move_down", "tab-calendar", "career", "Career")]
    [DataRow("open_notes", "tab-info", "profile", "Profile")]
    [DataRow("show_source", "tab-rules", "rules", "Rules")]
    [DataRow("runner_benchmark", "tab-stats", "stats", "Stats")]
    [DataRow("skill_group", "tab-skills", "skills", "Skills")]
    [DataRow("cyberware_delete", "tab-cyberware", "cyberware", "Cyberware")]
    [DataRow("quality_delete", "tab-qualities", "qualities", "Qualities")]
    [DataRow("adept_power_add", "tab-adept", "adept", "Adept")]
    [DataRow("magic_delete", "tab-magician", "magic", "Magic")]
    [DataRow("critter_power_add", "tab-critter", "critter", "Critter")]
    [DataRow("matrix_program_add", "tab-technomancer", "matrix", "Matrix")]
    public void Workbench_control_only_queries_infer_interactive_shell_identity(
        string controlId,
        string expectedTab,
        string expectedWorkflow,
        string expectedHeading)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: $"https://chummer.run/workbench?workspace=preview-ws&control={controlId}");

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{controlId}'.");

        Assert.AreEqual(controlId, GetPropertyValue<string>(fallback, "Control"));
        Assert.AreEqual(expectedTab, GetPropertyValue<string>(fallback, "Tab"));
        Assert.AreEqual(expectedWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "Title"));
        Assert.AreEqual(expectedHeading, GetPropertyValue<string>(fallback, "SectionHeading"));
        Assert.AreEqual("BLUE", GetPropertyValue<string>(fallback, "Runner"));
    }

    [DataTestMethod]
    [DataRow("https://chummer.run/workbench?command=open_character", "open-dossier", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=character_roster", "character-roster", "none", "idle", "none", "runner-active", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=master_index", "master-index", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=global_settings", "global-settings", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=character_settings", "character-settings", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=switch_ruleset", "switch-ruleset", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=report_bug", "support", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=about", "about", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=runtime_inspector", "runtime-inspector", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=auto_alice", "assistant", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=translator", "translator", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=xml_editor", "xml-editor", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=hero_lab_importer", "hero-lab-importer", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=dice_roller", "dice-roller", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=data_exporter", "data-exporter", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=print_setup", "print-setup", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=print_multiple", "print-multiple", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=update", "update", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=new_window", "new-window", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=wiki", "wiki", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=discord", "discord", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=show_login_video", "login-video", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=revision_history", "revision-history", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=dumpshock", "issue-tracker", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=copy", "copy", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=paste", "paste", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=new_critter", "new-critter", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=restart", "restart", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=exit", "exit", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=close_window", "close-window", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=close_all", "close-all", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=open_sourcebooks", "sourcebooks", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=open_errata", "errata", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=open_custom_data", "custom-data", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=update_data_packs", "update-pack", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=validate_data_scope", "validation-scope", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=open_data_folder", "data-folder", "none", "idle", "none", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=new_character_origin", "origin-dossier", "none", "idle", "none", "none", "origin-draft")]
    [DataRow("https://chummer.run/workbench?command=save_character_as&dialog_action=download", "save", "save", "requested", "local-dossier", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?command=export_character", "export", "export", "requested", "download-package", "none", "unsaved")]
    [DataRow("https://chummer.run/workbench?workspace=preview-ws&control=complex_form_add", "matrix", "none", "idle", "none", "none", "unsaved")]
    public void Workbench_fallback_query_metadata_matches_compatibility_route_contract(
        string uri,
        string expectedActiveWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputState,
        string expectedOutputTarget,
        string expectedSelectedRosterNode,
        string expectedDossierState)
    {
        App app = CreateApp(
            baseUri: "https://chummer.run/blazor/",
            uri: uri);

        object fallback = InvokeObject(app, "BuildWorkbenchFallback")
            ?? throw new AssertFailedException($"Expected a workbench fallback for '{uri}'.");

        Assert.AreEqual(expectedActiveWorkflow, GetPropertyValue<string>(fallback, "ActiveWorkflow"));
        Assert.AreEqual(expectedOutputWorkflow, GetPropertyValue<string>(fallback, "OutputWorkflow"));
        Assert.AreEqual(expectedOutputState, GetPropertyValue<string>(fallback, "OutputState"));
        Assert.AreEqual(expectedOutputTarget, GetPropertyValue<string>(fallback, "OutputTarget"));
        Assert.AreEqual(expectedSelectedRosterNode, GetPropertyValue<string>(fallback, "SelectedRosterNode"));
        Assert.AreEqual(expectedDossierState, GetPropertyValue<string>(fallback, "DossierState"));
        Assert.AreEqual("local", GetPropertyValue<string>(fallback, "DossierStorage"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "RouteFamily"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "RouteSurface"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "RouteAlias"));
        Assert.AreEqual("review-required", GetPropertyValue<string>(fallback, "ValidationState"));
        Assert.AreEqual("local-first", GetPropertyValue<string>(fallback, "PrivacyMode"));
        Assert.AreEqual("route-workflow-only", GetPropertyValue<string>(fallback, "AnalyticsScope"));
        Assert.AreEqual("hosted-or-self-hosted", GetPropertyValue<string>(fallback, "HostingMode"));
        Assert.AreEqual("compatibility", GetPropertyValue<string>(fallback, "DeploymentTarget"));
        Assert.AreEqual("true", GetPropertyValue<string>(fallback, "SelfHostable"));
        Assert.AreEqual("docker", GetPropertyValue<string>(fallback, "ContainerTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "AuthGate"));
        Assert.AreEqual("local-preview", GetPropertyValue<string>(fallback, "SessionState"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "LoginTarget"));
        Assert.AreEqual("none", GetPropertyValue<string>(fallback, "AuthReturnPolicy"));
        Assert.AreEqual("web-desktop", GetPropertyValue<string>(fallback, "ClientKind"));
        Assert.AreEqual("desktop-client", GetPropertyValue<string>(fallback, "ParityTarget"));
        Assert.AreEqual("shared-chummer-core", GetPropertyValue<string>(fallback, "CalculationOwner"));
        Assert.AreEqual("reusable-by-avalonia", GetPropertyValue<string>(fallback, "StatisticsRuntime"));
        Assert.AreEqual("enabled", GetPropertyValue<string>(fallback, "CharacterStatistics"));
        Assert.AreEqual("anonymized-build-comparisons", GetPropertyValue<string>(fallback, "StatisticsScope"));
        Assert.AreEqual("explainable-local-inputs", GetPropertyValue<string>(fallback, "RecommendationMode"));
        Assert.AreEqual("spells-inventory-drugs-gear-qualities", GetPropertyValue<string>(fallback, "RecommendationInputs"));
        Assert.AreEqual("damage-threshold-probability", GetPropertyValue<string>(fallback, "RiskModel"));
        Assert.AreEqual("shared-engine-only", GetPropertyValue<string>(fallback, "CalculationBoundary"));
        Assert.AreEqual("blazor-renders-shared-results", GetPropertyValue<string>(fallback, "ResultConsumer"));
    }

    [TestMethod]
    public void App_route_classic_menu_opens_on_click_and_closes_when_focus_moves_back_to_the_surface()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.AreEqual("false", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));

        cut.Find("[data-app-route-menu-trigger='file']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));
            StringAssert.Contains(cut.Find("[data-app-route-menu-root='file']").ClassName, "is-open");
        });

        cut.Find(".browser-app-runner-panel").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("false", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-app-route-menu-root='file']").ClassName.Contains("is-open", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void App_route_classic_menu_closes_when_escape_is_pressed()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.Find("[data-app-route-menu-trigger='file']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));
        });

        cut.Find("[data-app-route-classic-menu='true']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("false", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-app-route-menu-root='file']").ClassName.Contains("is-open", StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void App_route_classic_menu_closes_when_route_context_changes()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.Find("[data-app-route-menu-trigger='file']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-app-route-menu-trigger='file']").GetAttribute("aria-expanded"));
            StringAssert.Contains(cut.Find("[data-app-route-menu-root='file']").ClassName, "is-open");
        });

        navigation.NavigateTo("/app?command=new_character_origin");

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(0, cut.FindAll("[data-app-route-menu-trigger='file']").Count);
            Assert.AreEqual(0, cut.FindAll("[data-app-route-menu-root='file']").Count);
            StringAssert.Contains(cut.Markup, "Origin Dossier");
            StringAssert.Contains(cut.Markup, "data-origin-dossier-startup=\"true\"");
        });
    }

    [TestMethod]
    public void Workbench_route_change_does_not_restore_the_removed_classic_menu()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.AreEqual(0, cut.FindAll("[data-classic-menu-trigger]").Count);
        Assert.AreEqual(0, cut.FindAll("nav.classic-chummer-menu button").Count);

        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-gear");

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(0, cut.FindAll("[data-classic-menu-trigger]").Count);
            Assert.AreEqual(0, cut.FindAll("nav.classic-chummer-menu button").Count);
        });
    }

    [TestMethod]
    public void Workbench_route_has_no_classic_menu_escape_trap()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.AreEqual(0, cut.FindAll("[data-classic-menu-trigger]").Count);
        Assert.AreEqual(0, cut.FindAll("nav.classic-chummer-menu button").Count);
    }

    [DataTestMethod]
    [DataRow("new_character", "build-lab", "none", "none", "Build Lab")]
    [DataRow("global_settings", "global-settings", "none", "none", "Global Settings")]
    [DataRow("character_settings", "character-settings", "none", "none", "Character Settings")]
    [DataRow("switch_ruleset", "switch-ruleset", "none", "none", "Switch Ruleset")]
    [DataRow("report_bug", "support", "none", "none", "Support")]
    [DataRow("about", "about", "none", "none", "About Chummer")]
    [DataRow("runtime_inspector", "runtime-inspector", "none", "none", "Runtime Inspector")]
    [DataRow("auto_alice", "assistant", "none", "none", "Assistant")]
    [DataRow("translator", "translator", "none", "none", "Translator")]
    [DataRow("xml_editor", "xml-editor", "none", "none", "XML Editor")]
    [DataRow("hero_lab_importer", "hero-lab-importer", "none", "none", "Hero Lab Importer")]
    [DataRow("copy", "copy", "none", "none", "Copy")]
    [DataRow("paste", "paste", "none", "none", "Paste")]
    [DataRow("new_critter", "new-critter", "none", "none", "New Critter")]
    [DataRow("restart", "restart", "none", "none", "Restart")]
    [DataRow("exit", "exit", "none", "none", "Exit")]
    [DataRow("close_window", "close-window", "none", "none", "Close Window")]
    [DataRow("close_all", "close-all", "none", "none", "Close All")]
    [DataRow("dice_roller", "dice-roller", "none", "none", "Dice Roller")]
    [DataRow("data_exporter", "data-exporter", "none", "none", "Data Exporter")]
    [DataRow("print_setup", "print-setup", "none", "none", "Print Setup")]
    [DataRow("print_multiple", "print-multiple", "none", "none", "Print Multiple")]
    [DataRow("update", "update", "none", "none", "Update")]
    [DataRow("new_window", "new-window", "none", "none", "New Window")]
    [DataRow("wiki", "wiki", "none", "none", "Wiki")]
    [DataRow("discord", "discord", "none", "none", "Discord")]
    [DataRow("show_login_video", "login-video", "none", "none", "Login Video")]
    [DataRow("revision_history", "revision-history", "none", "none", "Revision History")]
    [DataRow("dumpshock", "issue-tracker", "none", "none", "Issue Tracker")]
    [DataRow("open_sourcebooks", "sourcebooks", "none", "none", "Sourcebooks")]
    [DataRow("open_errata", "errata", "none", "none", "Errata")]
    [DataRow("open_custom_data", "custom-data", "none", "none", "Custom Data")]
    [DataRow("update_data_packs", "update-pack", "none", "none", "Update Pack")]
    [DataRow("validate_data_scope", "validation-scope", "none", "none", "Validation Scope")]
    [DataRow("open_data_folder", "data-folder", "none", "none", "Data Folder")]
    [DataRow("open_character", "open-dossier", "none", "none", "Open Dossier")]
    [DataRow("save_character", "save", "save", "local-dossier", "Save")]
    [DataRow("save_character_as", "save", "save", "local-dossier", "Save Dossier As")]
    [DataRow("open_for_printing", "print", "print", "print-view", "Print")]
    [DataRow("print_preview", "print", "print", "print-view", "Print")]
    [DataRow("print_character", "print", "print", "print-view", "Print")]
    [DataRow("open_for_export", "export", "export", "download-package", "Export")]
    [DataRow("export_character", "export", "export", "download-package", "Export")]
    public void App_route_command_workflows_render_shared_shell_without_falling_back_to_roster(
        string commandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle)
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
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(commandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Explicit app-route command workflows must not silently fall back to the generic roster body.");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&command=save_character", "save_character", "save", "save", "local-dossier", "Save", "none")]
    [DataRow("/app?workspace=preview-ws&command=save_character_as", "save_character_as", "save", "save", "local-dossier", "Save Dossier As", "none")]
    [DataRow("/app?workspace=preview-ws&command=save_character_as&dialog_action=download", "save_character_as", "save", "save", "local-dossier", "Download", "download")]
    [DataRow("/app?workspace=preview-ws&command=open_for_printing", "open_for_printing", "print", "print", "print-view", "Print", "none")]
    [DataRow("/app?workspace=preview-ws&command=print_character", "print_character", "print", "print", "print-view", "Print", "none")]
    [DataRow("/app?workspace=preview-ws&command=open_for_export", "open_for_export", "export", "export", "download-package", "Export", "none")]
    [DataRow("/app?workspace=preview-ws&command=export_character", "export_character", "export", "export", "download-package", "Export", "none")]
    [DataRow("/app?workspace=preview-ws&command=export_character&dialog_action=download", "export_character", "export", "export", "download-package", "Export", "download")]
    public void App_route_workspace_output_query_loads_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle,
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
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedCommandId, presenter.ExecutedCommandId);
            Assert.AreEqual(expectedDialogAction == "none" ? null : expectedDialogAction, presenter.ExecutedDialogActionId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedDialogAction, appSurface.GetAttribute("data-dialog-action"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace-bound output continuations on /app must not collapse back to the generic roster landing surface.");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&command=save_character", "Dossier save prepared.")]
    [DataRow("/app?workspace=preview-ws&command=save_character_as", "Browser dossier download prepared.")]
    [DataRow("/app?workspace=preview-ws&command=save_character_as&dialog_action=download", "Dossier download ready.")]
    [DataRow("/app?workspace=preview-ws&command=export_character", "Export package prepared.")]
    [DataRow("/app?workspace=preview-ws&command=export_character&dialog_action=download", "Export package download ready.")]
    [DataRow("/app?workspace=preview-ws&command=print_character", "Print preview prepared.")]
    [DataRow("/app?workspace=preview-ws&command=print_preview", "Print preview opened.")]
    public void App_route_output_queries_render_committed_result_banner(
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
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            var result = cut.Find("[data-app-route-committed-result]");
            Assert.AreEqual(expectedResultText, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&command=save_character_as&dialog_action=download", "Download Dossier", "Finish the dossier download handoff.", "The dossier download is ready on the clean public route so the final handoff stays inside the shared shell.", "Download Dossier shell", "browser dossier download continuation path")]
    [DataRow("/app?workspace=preview-ws&command=export_character&dialog_action=download", "Download Export Package", "Finish the export package handoff.", "The export package download is ready on the clean public route so the final handoff stays inside the shared shell.", "Download package shell", "export package download continuation path")]
    [DataRow("/app?workspace=preview-ws&command=print_preview", "Open Print Preview", "Open the shared print preview.", "The print preview opens directly on the clean public route so browser output review stays inside the shared shell.", "Print preview shell", "print preview continuation path")]
    [DataRow("/app?workspace=preview-ws&command=open_for_export", "Open Export Staging", "Open the shared export staging path.", "The shared export staging dialog stays attached to the clean public route before an export package is prepared.", "Export staging shell", "shared export staging workflow")]
    public void App_route_output_queries_render_specific_output_copy(
        string route,
        string expectedCommandLabel,
        string expectedPanelTitle,
        string expectedPanelSummary,
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
            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual(expectedCommandLabel, appSurface.GetAttribute("aria-label"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedCommandLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedCommandLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, expectedCommandLabel);
        StringAssert.Contains(cut.Markup, expectedPanelTitle);
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteSummaryFragment);
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Return to Character Roster");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Open Build Lab");
        Assert.IsFalse(cut.Markup.Contains("Return to roster", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("/online?workspace=preview-ws&command=save_character_as&dialog_action=download", "Download Dossier", "Finish the dossier download handoff.", "The dossier download is ready on the clean public /online alias so the final handoff stays inside the shared shell.", "Download Dossier shell", "browser dossier download continuation path", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&command=export_character&dialog_action=download", "Download Export Package", "Finish the export package handoff.", "The export package download is ready on the clean public /online alias so the final handoff stays inside the shared shell.", "Download package shell", "export package download continuation path", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&command=print_preview", "Open Print Preview", "Open the shared print preview.", "The print preview opens directly on the clean public /online alias so browser output review stays inside the shared shell.", "Print preview shell", "print preview continuation path", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&command=open_for_export", "Open Export Staging", "Open the shared export staging path.", "The shared export staging dialog stays attached to the clean public /online alias before an export package is prepared.", "Export staging shell", "shared export staging workflow", "from the clean public /online alias.")]
    public void Online_alias_output_queries_render_specific_output_copy(
        string route,
        string expectedCommandLabel,
        string expectedPanelTitle,
        string expectedPanelSummary,
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
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual(expectedCommandLabel, appSurface.GetAttribute("aria-label"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedCommandLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedCommandLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, expectedCommandLabel);
        StringAssert.Contains(cut.Markup, expectedPanelTitle);
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteSummaryFragment);
        StringAssert.Contains(cut.Markup, expectedRouteSurfacePhrase);
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Return to Character Roster");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Open Build Lab");
        Assert.IsFalse(cut.Markup.Contains("Return to roster", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("/app?fixture=blue&command=save_character", "save_character", "save", "save", "local-dossier", "Save", "none")]
    [DataRow("/app?fixture=blue&command=save_character_as", "save_character_as", "save", "save", "local-dossier", "Save Dossier As", "none")]
    [DataRow("/app?fixture=blue&command=save_character_as&dialog_action=download", "save_character_as", "save", "save", "local-dossier", "Download", "download")]
    [DataRow("/app?fixture=blue&command=export_character", "export_character", "export", "export", "download-package", "Export", "none")]
    [DataRow("/app?fixture=blue&command=export_character&dialog_action=download", "export_character", "export", "export", "download-package", "Export", "download")]
    [DataRow("/app?fixture=blue&command=print_character", "print_character", "print", "print", "print-view", "Print", "none")]
    [DataRow("/app?fixture=blue&command=print_preview", "print_preview", "print", "print", "print-view", "Print", "none")]
    public void App_route_fixture_output_query_loads_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle,
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

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedDialogAction, appSurface.GetAttribute("data-dialog-action"));
            Assert.AreEqual(expectedCommandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.AreEqual("blue", appSurface.GetAttribute("data-fixture"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Fixture-driven `/app` output continuations must not collapse back to the generic roster landing surface.");
    }

    [TestMethod]
    public void App_route_workspace_open_dossier_query_loads_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?workspace=preview-ws&command=open_character");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("open-dossier", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-dialog-action"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Open Dossier");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace-bound open-dossier continuations on /app must not collapse back to the generic roster landing surface.");
    }

    [TestMethod]
    public void App_route_workspace_build_lab_query_loads_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?workspace=preview-ws&tab=tab-create");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='app']");
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.AreEqual("tab-create", appSurface.GetAttribute("data-tab"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Build Lab");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace build-lab continuations on /app must not collapse back to the roster landing surface.");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear", "app", "gear", "Gear", "Continue the shared Gear shell.", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.", "Gear shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-rules", "app", "rules", "Rules", "Continue the shared Rules shell.", "The requested dossier context now opens directly into the Rules shell on the clean public route instead of dropping back to the roster landing surface.", "Rules shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-skills", "app", "skills", "Skills", "Continue the shared Skills shell.", "The requested dossier context now opens directly into the Skills shell on the clean public route instead of dropping back to the roster landing surface.", "Skills shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-cyberware", "app", "cyberware", "Cyberware", "Continue the shared Cyberware shell.", "The requested dossier context now opens directly into the Cyberware shell on the clean public route instead of dropping back to the roster landing surface.", "Cyberware shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-qualities", "app", "qualities", "Qualities", "Continue the shared Qualities shell.", "The requested dossier context now opens directly into the Qualities shell on the clean public route instead of dropping back to the roster landing surface.", "Qualities shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-adept", "app", "adept", "Adept", "Continue the shared Adept shell.", "The requested dossier context now opens directly into the Adept shell on the clean public route instead of dropping back to the roster landing surface.", "Adept shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-combat", "app", "combat", "Combat", "Continue the shared Combat shell.", "The requested dossier context now opens directly into the Combat shell on the clean public route instead of dropping back to the roster landing surface.", "Combat shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician", "app", "magic", "Magic", "Continue the shared Magic shell.", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.", "Magic shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-critter", "app", "critter", "Critter", "Continue the shared Critter shell.", "The requested dossier context now opens directly into the Critter shell on the clean public route instead of dropping back to the roster landing surface.", "Critter shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-stats", "app", "stats", "Stats", "Continue the shared Stats shell.", "The requested dossier context now opens directly into the Stats shell on the clean public route instead of dropping back to the roster landing surface.", "Stats shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-technomancer", "app", "matrix", "Matrix", "Continue the shared Matrix shell.", "The requested dossier context now opens directly into the Matrix shell on the clean public route instead of dropping back to the roster landing surface.", "Matrix shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-contacts", "app", "contacts", "Contacts", "Continue the shared Contacts shell.", "The requested dossier context now opens directly into the Contacts shell on the clean public route instead of dropping back to the roster landing surface.", "Contacts shell", "from the clean public route.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar", "app", "career", "Career", "Continue the shared Career shell.", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.", "Career shell", "from the clean public route.")]
    public void App_route_workspace_tab_queries_render_specific_workflow_shell_copy(
        string route,
        string expectedRouteSegment,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedPanelTitle,
        string expectedPanelSummary,
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
            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, expectedPanelTitle);
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteSummaryFragment);
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-stats&control=runner_benchmark", "app", "runner_benchmark", "The requested dossier context now opens directly into the Stats shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-stats&control=runner_benchmark", "online", "runner_benchmark", "The requested dossier context now opens directly into the Stats shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-stats&control=runner_what_if", "app", "runner_what_if", "The requested dossier context now opens directly into the Stats shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-stats&control=runner_what_if", "online", "runner_what_if", "The requested dossier context now opens directly into the Stats shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-stats&control=runner_cohort_privacy", "app", "runner_cohort_privacy", "The requested dossier context now opens directly into the Stats shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-stats&control=runner_cohort_privacy", "online", "runner_cohort_privacy", "The requested dossier context now opens directly into the Stats shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_runner_intelligence_control_routes_render_stats_shells_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("stats", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Stats", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Stats", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Stats shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Stats shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add", "app", "spell_add", "magic", "Magic", "Magic shell", "Spell 'Stunbolt' added.", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add", "online", "spell_add", "magic", "Magic", "Magic shell", "Spell 'Stunbolt' added.", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-critter&control=critter_power_add&dialog_action=add", "app", "critter_power_add", "critter", "Critter", "Critter shell", "Critter power 'Natural Weapon' added.", "The requested dossier context now opens directly into the Critter shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-critter&control=critter_power_add&dialog_action=add", "online", "critter_power_add", "critter", "Critter", "Critter shell", "Critter power 'Natural Weapon' added.", "The requested dossier context now opens directly into the Critter shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add", "app", "complex_form_add", "matrix", "Matrix", "Matrix shell", "Complex form 'Cleaner' added.", "The requested dossier context now opens directly into the Matrix shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add", "online", "complex_form_add", "matrix", "Matrix", "Matrix shell", "Complex form 'Cleaner' added.", "The requested dossier context now opens directly into the Matrix shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add", "app", "initiation_add", "adept", "Adept", "Adept shell", "Initiation/submersion reward 'Masking' added.", "The requested dossier context now opens directly into the Adept shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add", "online", "initiation_add", "adept", "Adept", "Adept shell", "Initiation/submersion reward 'Masking' added.", "The requested dossier context now opens directly into the Adept shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add", "app", "cyberware_add", "cyberware", "Cyberware", "Cyberware shell", "Cyberware 'Wired Reflexes 2' added.", "The requested dossier context now opens directly into the Cyberware shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add", "online", "cyberware_add", "cyberware", "Cyberware", "Cyberware shell", "Cyberware 'Wired Reflexes 2' added.", "The requested dossier context now opens directly into the Cyberware shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_support_add_dialog_action_routes_render_named_shells_and_committed_results(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedCommittedResult,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));

            var result = cut.Find("[data-app-route-committed-result]");
            Assert.AreEqual(expectedCommittedResult, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, $"Continue the shared {expectedWorkflowLabel} shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-combat&control=combat_add_weapon", "app", "combat_add_weapon", "combat", "Combat", "Combat shell", "The requested dossier context now opens directly into the Combat shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-combat&control=combat_add_weapon", "online", "combat_add_weapon", "combat", "Combat", "Combat shell", "The requested dossier context now opens directly into the Combat shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=gear_add", "app", "gear_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=gear_add", "online", "gear_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=vehicle_add", "app", "vehicle_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=vehicle_add", "online", "vehicle_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=drug_add", "app", "drug_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=drug_add", "online", "drug_add", "gear", "Gear", "Gear shell", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-combat&control=combat_add_armor", "app", "combat_add_armor", "combat", "Combat", "Combat shell", "The requested dossier context now opens directly into the Combat shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-combat&control=combat_add_armor", "online", "combat_add_armor", "combat", "Combat", "Combat shell", "The requested dossier context now opens directly into the Combat shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-qualities&control=quality_add", "app", "quality_add", "qualities", "Qualities", "Qualities shell", "The requested dossier context now opens directly into the Qualities shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-qualities&control=quality_add", "online", "quality_add", "qualities", "Qualities", "Qualities shell", "The requested dossier context now opens directly into the Qualities shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-qualities&control=quality_delete", "app", "quality_delete", "qualities", "Qualities", "Qualities shell", "The requested dossier context now opens directly into the Qualities shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-qualities&control=quality_delete", "online", "quality_delete", "qualities", "Qualities", "Qualities shell", "The requested dossier context now opens directly into the Qualities shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar&control=move_up", "app", "move_up", "career", "Career", "Career shell", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar&control=move_up", "online", "move_up", "career", "Career", "Career shell", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar&control=move_down", "app", "move_down", "career", "Career", "Career shell", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar&control=move_down", "online", "move_down", "career", "Career", "Career shell", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-skills&control=skill_add", "app", "skill_add", "skills", "Skills", "Skills shell", "The requested dossier context now opens directly into the Skills shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-skills&control=skill_add", "online", "skill_add", "skills", "Skills", "Skills shell", "The requested dossier context now opens directly into the Skills shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-adept&control=adept_power_add", "app", "adept_power_add", "adept", "Adept", "Adept shell", "The requested dossier context now opens directly into the Adept shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-adept&control=adept_power_add", "online", "adept_power_add", "adept", "Adept", "Adept shell", "The requested dossier context now opens directly into the Adept shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=spirit_add", "app", "spirit_add", "magic", "Magic", "Magic shell", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=spirit_add", "online", "spirit_add", "magic", "Magic", "Magic shell", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-technomancer&control=matrix_program_add", "app", "matrix_program_add", "matrix", "Matrix", "Matrix shell", "The requested dossier context now opens directly into the Matrix shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-technomancer&control=matrix_program_add", "online", "matrix_program_add", "matrix", "Matrix", "Matrix shell", "The requested dossier context now opens directly into the Matrix shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_quick_action_control_routes_render_named_shells_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, $"Continue the shared {expectedWorkflowLabel} shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-skills&control=skill_specialize", "app", "skill_specialize", "The requested dossier context now opens directly into the Skills shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-skills&control=skill_specialize", "online", "skill_specialize", "The requested dossier context now opens directly into the Skills shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-skills&control=skill_remove", "app", "skill_remove", "The requested dossier context now opens directly into the Skills shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-skills&control=skill_remove", "online", "skill_remove", "The requested dossier context now opens directly into the Skills shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-skills&control=skill_group", "app", "skill_group", "The requested dossier context now opens directly into the Skills shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-skills&control=skill_group", "online", "skill_group", "The requested dossier context now opens directly into the Skills shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_skill_control_routes_render_skills_shells_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("skills", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Skills", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Skills", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Skills shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Skills shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar&control=create_entry&dialog_action=add", "app", "create_entry", "add", "Entry 'New entry' added.", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar&control=create_entry&dialog_action=add", "online", "create_entry", "add", "Entry 'New entry' added.", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar&control=edit_entry&dialog_action=apply", "app", "edit_entry", "apply", "Entry renamed to 'Current Entry'.", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar&control=edit_entry&dialog_action=apply", "online", "edit_entry", "apply", "Entry renamed to 'Current Entry'.", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-calendar&control=delete_entry&dialog_action=delete", "app", "delete_entry", "delete", "Entry 'Current Entry' removed.", "The requested dossier context now opens directly into the Career shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar&control=delete_entry&dialog_action=delete", "online", "delete_entry", "delete", "Entry 'Current Entry' removed.", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_career_dialog_action_routes_render_career_shell_and_committed_result(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedDialogAction,
        string expectedCommittedResult,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("career", appSurface.GetAttribute("data-active-workflow"));

            var result = cut.Find("[data-app-route-committed-result]");
            Assert.AreEqual(expectedCommittedResult, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Career", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Career", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Career shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Career shell");
    }

    [TestMethod]
    public void App_origin_dossier_command_opens_dossier_builder_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/app?command=new_character_origin");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Origin Dossier");
        StringAssert.Contains(cut.Markup, "Start the story-first dossier path.");
        StringAssert.Contains(cut.Markup, "data-chummer-app-startup-command=\"new_character_origin\"");
        StringAssert.Contains(cut.Markup, "data-browser-shell-command=\"new-character-origin\"");
        StringAssert.Contains(cut.Markup, "data-origin-dossier-route=\"app\"");
        StringAssert.Contains(cut.Markup, "data-origin-dossier-shared-shell=\"true\"");
        StringAssert.Contains(cut.Markup, "href=\"app?command=new_character_origin\"");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Use standard character creation");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Return to Character Roster");
        Assert.IsFalse(cut.Markup.Contains("Start the story-first character path.", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find("[data-startup-command='new_character_origin']"));
        Assert.IsNotNull(cut.Find(".browser-app-origin-panel"));
        Assert.IsNotNull(cut.Find(".desktop-shell"));
        Assert.AreEqual("new_character_origin", presenter.ExecutedCommandId);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The Origin Dossier app deep link must not silently fall back to the generic roster body.");
    }

    [DataTestMethod]
    [DataRow("/app?command=global_settings", "app", "none", "app", "global_settings", "global-settings", "Global Settings", "language, update posture, and dossier defaults")]
    [DataRow("/online?command=global_settings", "online", "online", "online-alias", "global_settings", "global-settings", "Global Settings", "language, update posture, and dossier defaults")]
    [DataRow("/app?command=character_settings", "app", "none", "app", "character_settings", "character-settings", "Character Settings", "build defaults, karma ratio, house-rule posture, and notes")]
    [DataRow("/online?command=character_settings", "online", "online", "online-alias", "character_settings", "character-settings", "Character Settings", "build defaults, karma ratio, house-rule posture, and notes")]
    [DataRow("/app?command=switch_ruleset", "app", "none", "app", "switch_ruleset", "switch-ruleset", "Switch Ruleset", "preferred edition changes")]
    [DataRow("/online?command=switch_ruleset", "online", "online", "online-alias", "switch_ruleset", "switch-ruleset", "Switch Ruleset", "preferred edition changes")]
    [DataRow("/app?command=report_bug", "app", "none", "app", "report_bug", "support", "Support", "portal support and public GitHub issue paths")]
    [DataRow("/online?command=report_bug", "online", "online", "online-alias", "report_bug", "support", "Support", "portal support and public GitHub issue paths")]
    [DataRow("/app?command=about", "app", "none", "app", "about", "about", "About Chummer", "runtime identity and preview posture")]
    [DataRow("/online?command=about", "online", "online", "online-alias", "about", "about", "About Chummer", "runtime identity and preview posture")]
    [DataRow("/app?command=runtime_inspector", "app", "none", "app", "runtime_inspector", "runtime-inspector", "Runtime Inspector", "resolved runtime, rule packs, and provider bindings")]
    [DataRow("/online?command=runtime_inspector", "online", "online", "online-alias", "runtime_inspector", "runtime-inspector", "Runtime Inspector", "resolved runtime, rule packs, and provider bindings")]
    [DataRow("/app?command=auto_alice", "app", "none", "app", "auto_alice", "assistant", "Assistant", "build help, rules coaching, and explicit handoffs")]
    [DataRow("/online?command=auto_alice", "online", "online", "online-alias", "auto_alice", "assistant", "Assistant", "build help, rules coaching, and explicit handoffs")]
    [DataRow("/app?command=translator", "app", "none", "app", "translator", "translator", "Translator", "language search and enabled overlays")]
    [DataRow("/online?command=translator", "online", "online", "online-alias", "translator", "translator", "Translator", "language search and enabled overlays")]
    [DataRow("/app?command=xml_editor", "app", "none", "app", "xml_editor", "xml-editor", "XML Editor", "XML bridge status and custom data posture")]
    [DataRow("/online?command=xml_editor", "online", "online", "online-alias", "xml_editor", "xml-editor", "XML Editor", "XML bridge status and custom data posture")]
    [DataRow("/app?command=hero_lab_importer", "app", "none", "app", "hero_lab_importer", "hero-lab-importer", "Hero Lab Importer", "local XML intake and import-oracle posture")]
    [DataRow("/online?command=hero_lab_importer", "online", "online", "online-alias", "hero_lab_importer", "hero-lab-importer", "Hero Lab Importer", "local XML intake and import-oracle posture")]
    [DataRow("/app?command=copy", "app", "none", "app", "copy", "copy", "Copy", "active editor copy posture stays inside Chummer Online")]
    [DataRow("/online?command=copy", "online", "online", "online-alias", "copy", "copy", "Copy", "active editor copy posture stays inside Chummer Online")]
    [DataRow("/app?command=paste", "app", "none", "app", "paste", "paste", "Paste", "active editor paste posture stays inside Chummer Online")]
    [DataRow("/online?command=paste", "online", "online", "online-alias", "paste", "paste", "Paste", "active editor paste posture stays inside Chummer Online")]
    [DataRow("/app?command=new_critter", "app", "none", "app", "new_critter", "new-critter", "New Critter", "critter-first starter imports stay inside Chummer Online")]
    [DataRow("/online?command=new_critter", "online", "online", "online-alias", "new_critter", "new-critter", "New Critter", "critter-first starter imports stay inside Chummer Online")]
    [DataRow("/app?command=restart", "app", "none", "app", "restart", "restart", "Restart", "session reset and relaunch posture stay inside Chummer Online")]
    [DataRow("/online?command=restart", "online", "online", "online-alias", "restart", "restart", "Restart", "session reset and relaunch posture stay inside Chummer Online")]
    [DataRow("/app?command=exit", "app", "none", "app", "exit", "exit", "Exit", "desktop-only exit posture stays inside Chummer Online")]
    [DataRow("/online?command=exit", "online", "online", "online-alias", "exit", "exit", "Exit", "desktop-only exit posture stays inside Chummer Online")]
    [DataRow("/app?command=close_window", "app", "none", "app", "close_window", "close-window", "Close Window", "active-dossier close posture stays inside Chummer Online")]
    [DataRow("/online?command=close_window", "online", "online", "online-alias", "close_window", "close-window", "Close Window", "active-dossier close posture stays inside Chummer Online")]
    [DataRow("/app?command=close_all", "app", "none", "app", "close_all", "close-all", "Close All", "session-wide close posture stays inside Chummer Online")]
    [DataRow("/online?command=close_all", "online", "online", "online-alias", "close_all", "close-all", "Close All", "session-wide close posture stays inside Chummer Online")]
    [DataRow("/app?command=dice_roller", "app", "none", "app", "dice_roller", "dice-roller", "Dice Roller", "roll method, threshold, and reroll posture stay inside Chummer Online")]
    [DataRow("/online?command=dice_roller", "online", "online", "online-alias", "dice_roller", "dice-roller", "Dice Roller", "roll method, threshold, and reroll posture stay inside Chummer Online")]
    [DataRow("/app?command=data_exporter", "app", "none", "app", "data_exporter", "data-exporter", "Data Exporter", "export pipeline preview and payload handoff stay inside Chummer Online")]
    [DataRow("/online?command=data_exporter", "online", "online", "online-alias", "data_exporter", "data-exporter", "Data Exporter", "export pipeline preview and payload handoff stay inside Chummer Online")]
    [DataRow("/app?command=print_setup", "app", "none", "app", "print_setup", "print-setup", "Print Setup", "print preferences stay inside Chummer Online before host print takes over")]
    [DataRow("/online?command=print_setup", "online", "online", "online-alias", "print_setup", "print-setup", "Print Setup", "print preferences stay inside Chummer Online before host print takes over")]
    [DataRow("/app?command=print_multiple", "app", "none", "app", "print_multiple", "print-multiple", "Print Multiple", "roster batch print posture stays inside Chummer Online")]
    [DataRow("/online?command=print_multiple", "online", "online", "online-alias", "print_multiple", "print-multiple", "Print Multiple", "roster batch print posture stays inside Chummer Online")]
    [DataRow("/app?command=update", "app", "none", "app", "update", "update", "Update", "channel, pending installer, and support follow-through stay inside Chummer Online")]
    [DataRow("/online?command=update", "online", "online", "online-alias", "update", "update", "Update", "channel, pending installer, and support follow-through stay inside Chummer Online")]
    [DataRow("/app?command=new_window", "app", "none", "app", "new_window", "new-window", "New Window", "second-shell handoff stays inside Chummer Online before the host opens another window")]
    [DataRow("/online?command=new_window", "online", "online", "online-alias", "new_window", "new-window", "New Window", "second-shell handoff stays inside Chummer Online before the host opens another window")]
    [DataRow("/app?command=wiki", "app", "none", "app", "wiki", "wiki", "Wiki", "legacy documentation handoff stays inside Chummer Online before the external reference opens")]
    [DataRow("/online?command=wiki", "online", "online", "online-alias", "wiki", "wiki", "Wiki", "legacy documentation handoff stays inside Chummer Online before the external reference opens")]
    [DataRow("/app?command=discord", "app", "none", "app", "discord", "discord", "Discord", "community handoff stays inside Chummer Online before the external chat opens")]
    [DataRow("/online?command=discord", "online", "online", "online-alias", "discord", "discord", "Discord", "community handoff stays inside Chummer Online before the external chat opens")]
    [DataRow("/app?command=show_login_video", "app", "none", "app", "show_login_video", "login-video", "Login Video", "Matrix uplink handoff stays inside Chummer Online before the help surface opens")]
    [DataRow("/online?command=show_login_video", "online", "online", "online-alias", "show_login_video", "login-video", "Login Video", "Matrix uplink handoff stays inside Chummer Online before the help surface opens")]
    [DataRow("/app?command=revision_history", "app", "none", "app", "revision_history", "revision-history", "Revision History", "release notes and external history links stay inside Chummer Online before the browser opens them")]
    [DataRow("/online?command=revision_history", "online", "online", "online-alias", "revision_history", "revision-history", "Revision History", "release notes and external history links stay inside Chummer Online before the browser opens them")]
    [DataRow("/app?command=dumpshock", "app", "none", "app", "dumpshock", "issue-tracker", "Issue Tracker", "issue-handling and external tracker context stay inside Chummer Online before the browser opens the tracker")]
    [DataRow("/online?command=dumpshock", "online", "online", "online-alias", "dumpshock", "issue-tracker", "Issue Tracker", "issue-handling and external tracker context stay inside Chummer Online before the browser opens the tracker")]
    [DataRow("/app?command=open_sourcebooks", "app", "none", "app", "open_sourcebooks", "sourcebooks", "Sourcebooks", "active book selection stays inside Chummer Online")]
    [DataRow("/online?command=open_sourcebooks", "online", "online", "online-alias", "open_sourcebooks", "sourcebooks", "Sourcebooks", "active book selection stays inside Chummer Online")]
    [DataRow("/app?command=open_errata", "app", "none", "app", "open_errata", "errata", "Errata", "rules-update posture stays inside Chummer Online")]
    [DataRow("/online?command=open_errata", "online", "online", "online-alias", "open_errata", "errata", "Errata", "rules-update posture stays inside Chummer Online")]
    [DataRow("/app?command=open_custom_data", "app", "none", "app", "open_custom_data", "custom-data", "Custom Data", "homebrew and local packs stay discoverable inside Chummer Online")]
    [DataRow("/online?command=open_custom_data", "online", "online", "online-alias", "open_custom_data", "custom-data", "Custom Data", "homebrew and local packs stay discoverable inside Chummer Online")]
    [DataRow("/app?command=update_data_packs", "app", "none", "app", "update_data_packs", "update-pack", "Update Pack", "data refresh stays visible as an operator action inside Chummer Online")]
    [DataRow("/online?command=update_data_packs", "online", "online", "online-alias", "update_data_packs", "update-pack", "Update Pack", "data refresh stays visible as an operator action inside Chummer Online")]
    [DataRow("/app?command=validate_data_scope", "app", "none", "app", "validate_data_scope", "validation-scope", "Validation Scope", "rules data stays connected to build readiness inside Chummer Online")]
    [DataRow("/online?command=validate_data_scope", "online", "online", "online-alias", "validate_data_scope", "validation-scope", "Validation Scope", "rules data stays connected to build readiness inside Chummer Online")]
    [DataRow("/app?command=open_data_folder", "app", "none", "app", "open_data_folder", "data-folder", "Data Folder", "local and self-host rule-data paths stay visible inside Chummer Online")]
    [DataRow("/online?command=open_data_folder", "online", "online", "online-alias", "open_data_folder", "data-folder", "Data Folder", "local and self-host rule-data paths stay visible inside Chummer Online")]
    public void Public_tool_commands_open_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedRouteSegment,
        string expectedRouteAlias,
        string expectedRouteFamily,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedTitle,
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual(expectedRouteAlias, appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual(expectedRouteFamily, appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedCommandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.IsTrue(
            cut.Markup.Contains($"Open the shared {expectedTitle}.", StringComparison.Ordinal)
            || cut.Markup.Contains($"Open the shared {expectedTitle} relay.", StringComparison.Ordinal),
            $"Expected shared-shell startup copy for '{expectedTitle}'.");
        StringAssert.Contains(cut.Markup, $"{expectedTitle} shell");
        StringAssert.Contains(cut.Markup, expectedCopyFragment);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            $"The public {expectedTitle} startup route must not silently fall back to the generic roster body.");
    }

    [DataTestMethod]
    [DataRow("/app?command=character_settings", "app", "none", "app", "character_settings", "Character Settings", "character-settings")]
    [DataRow("/online?command=character_settings", "online", "online", "online-alias", "character_settings", "Character Settings", "character-settings")]
    [DataRow("/app?command=copy", "app", "none", "app", "copy", "Copy", "copy")]
    [DataRow("/online?command=copy", "online", "online", "online-alias", "copy", "Copy", "copy")]
    [DataRow("/app?command=data_exporter", "app", "none", "app", "data_exporter", "Data Exporter", "data-exporter")]
    [DataRow("/online?command=data_exporter", "online", "online", "online-alias", "data_exporter", "Data Exporter", "data-exporter")]
    public void Public_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command(
        string route,
        string expectedRouteSegment,
        string expectedRouteAlias,
        string expectedRouteFamily,
        string expectedCommandId,
        string expectedTitle,
        string expectedWorkflow)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual(expectedRouteAlias, appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual(expectedRouteFamily, appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedCommandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.AreEqual("blocked", appSurface.GetAttribute("data-startup-command-state"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.IsNull(presenter.ExecutedCommandId);
        StringAssert.Contains(cut.Markup, $"{expectedTitle} needs an open dossier.");
        StringAssert.Contains(cut.Markup, $"{expectedTitle} requires an open dossier");
        StringAssert.Contains(cut.Markup, "Open or restore a dossier first");
        Assert.IsFalse(
            cut.Markup.Contains("disabled in the current shell state.", StringComparison.Ordinal),
            "Blocked startup routes should stay on route-level guidance instead of surfacing a shell error for a command that cannot run yet.");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
    }

    [DataTestMethod]
    [DataRow("/app?command=master_index", "app", "none", "app", "Master Index", "the clean public route")]
    [DataRow("/online?command=master_index", "online", "online", "online-alias", "Master Index", "the clean public /online alias")]
    public void Public_master_index_command_opens_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedRouteSegment,
        string expectedRouteAlias,
        string expectedRouteFamily,
        string expectedTitle,
        string expectedRoutePhrase)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(route);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("master_index", presenter.ExecutedCommandId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual(expectedRouteAlias, appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual(expectedRouteFamily, appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("master-index", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual("master_index", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        StringAssert.Contains(cut.Markup, "Open the shared Master Index.");
        StringAssert.Contains(cut.Markup, "Master Index shell");
        StringAssert.Contains(cut.Markup, expectedRoutePhrase);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The public Master Index startup route must not silently fall back to the generic roster body.");
    }

    [TestMethod]
    public void Online_alias_route_renders_character_roster_as_the_same_public_app_surface()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?command=character_roster");

        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Character Roster");
        StringAssert.Contains(cut.Markup, CharacterRosterLandingSummary);
        StringAssert.Contains(cut.Markup, "Kestrel");
        Assert.IsTrue(cut.Markup.Contains("chummer-online-app-shell", StringComparison.Ordinal));
        Assert.AreEqual("/online", new Uri(navigation.Uri).AbsolutePath);
        StringAssert.Contains(navigation.Uri, "command=character_roster", StringComparison.Ordinal);
        StringAssert.Contains(cut.Markup, "data-canonical-route=\"app\"");
        StringAssert.Contains(cut.Markup, "data-route-alias=\"online\"");
        StringAssert.Contains(cut.Markup, "data-route-family=\"online-alias\"");
        Assert.IsNotNull(cut.Find(".browser-app-roster"));
        Assert.AreEqual("character_roster", cut.Find(".browser-app-roster").GetAttribute("data-chummer-app-startup-command"));
        Assert.AreEqual("character-roster", cut.Find(".browser-app-roster").GetAttribute("data-browser-shell-command"));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-banner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-promoted-app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("desktop-shell", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-chummer-shell", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Online_alias_open_dossier_command_opens_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?command=open_character");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("open_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("open-dossier", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Open Dossier");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The `/online` open-dossier alias must not silently fall back to the generic roster body.");
    }

    [TestMethod]
    public void Online_alias_build_lab_command_opens_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?command=new_character");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("new_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Build Lab");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The `/online` Build Lab alias must not silently fall back to the generic roster body.");
    }

    [DataTestMethod]
    [DataRow("save_character", "save", "save", "local-dossier", "Save")]
    [DataRow("save_character_as", "save", "save", "local-dossier", "Save Dossier As")]
    [DataRow("open_for_printing", "print", "print", "print-view", "Print")]
    [DataRow("print_preview", "print", "print", "print-view", "Print")]
    [DataRow("print_character", "print", "print", "print-view", "Print")]
    [DataRow("open_for_export", "export", "export", "download-package", "Export")]
    [DataRow("export_character", "export", "export", "download-package", "Export")]
    public void Online_alias_output_command_workflows_open_shared_shell_without_falling_back_to_roster(
        string commandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/online?command={commandId}");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(commandId, presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(commandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The `/online` shared-shell command aliases must not silently fall back to the generic roster body.");
    }

    [DataTestMethod]
    [DataRow("/online?workspace=preview-ws&command=save_character", "save_character", "save", "save", "local-dossier", "Save", "none")]
    [DataRow("/online?workspace=preview-ws&command=save_character_as", "save_character_as", "save", "save", "local-dossier", "Save Dossier As", "none")]
    [DataRow("/online?workspace=preview-ws&command=save_character_as&dialog_action=download", "save_character_as", "save", "save", "local-dossier", "Download", "download")]
    [DataRow("/online?workspace=preview-ws&command=open_for_printing", "open_for_printing", "print", "print", "print-view", "Print", "none")]
    [DataRow("/online?workspace=preview-ws&command=print_character", "print_character", "print", "print", "print-view", "Print", "none")]
    [DataRow("/online?workspace=preview-ws&command=open_for_export", "open_for_export", "export", "export", "download-package", "Export", "none")]
    [DataRow("/online?workspace=preview-ws&command=export_character", "export_character", "export", "export", "download-package", "Export", "none")]
    [DataRow("/online?workspace=preview-ws&command=export_character&dialog_action=download", "export_character", "export", "export", "download-package", "Export", "download")]
    public void Online_alias_workspace_output_query_loads_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle,
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
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual(expectedCommandId, presenter.ExecutedCommandId);
            Assert.AreEqual(expectedDialogAction == "none" ? null : expectedDialogAction, presenter.ExecutedDialogActionId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedDialogAction, appSurface.GetAttribute("data-dialog-action"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace-bound `/online` output continuations must not collapse back to the generic roster landing surface.");
    }

    [DataTestMethod]
    [DataRow("/online?workspace=preview-ws&command=save_character", "Dossier save prepared.")]
    [DataRow("/online?workspace=preview-ws&command=save_character_as", "Browser dossier download prepared.")]
    [DataRow("/online?workspace=preview-ws&command=save_character_as&dialog_action=download", "Dossier download ready.")]
    [DataRow("/online?workspace=preview-ws&command=export_character", "Export package prepared.")]
    [DataRow("/online?workspace=preview-ws&command=export_character&dialog_action=download", "Export package download ready.")]
    [DataRow("/online?workspace=preview-ws&command=print_character", "Print preview prepared.")]
    [DataRow("/online?workspace=preview-ws&command=print_preview", "Print preview opened.")]
    public void Online_alias_output_queries_render_committed_result_banner(
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
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            var result = cut.Find("[data-app-route-committed-result]");
            Assert.AreEqual(expectedResultText, result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });
    }

    [DataTestMethod]
    [DataRow("/online?fixture=blue&command=save_character", "save_character", "save", "save", "local-dossier", "Save", "none")]
    [DataRow("/online?fixture=blue&command=save_character_as", "save_character_as", "save", "save", "local-dossier", "Save Dossier As", "none")]
    [DataRow("/online?fixture=blue&command=save_character_as&dialog_action=download", "save_character_as", "save", "save", "local-dossier", "Download", "download")]
    [DataRow("/online?fixture=blue&command=export_character", "export_character", "export", "export", "download-package", "Export", "none")]
    [DataRow("/online?fixture=blue&command=export_character&dialog_action=download", "export_character", "export", "export", "download-package", "Export", "download")]
    [DataRow("/online?fixture=blue&command=print_character", "print_character", "print", "print", "print-view", "Print", "none")]
    [DataRow("/online?fixture=blue&command=print_preview", "print_preview", "print", "print", "print-view", "Print", "none")]
    public void Online_alias_fixture_output_query_loads_shared_shell_without_falling_back_to_roster(
        string route,
        string expectedCommandId,
        string expectedWorkflow,
        string expectedOutputWorkflow,
        string expectedOutputTarget,
        string expectedTitle,
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

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual(expectedOutputWorkflow, appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual(expectedOutputTarget, appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual(expectedDialogAction, appSurface.GetAttribute("data-dialog-action"));
            Assert.AreEqual(expectedCommandId, appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.AreEqual("blue", appSurface.GetAttribute("data-fixture"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, expectedTitle);
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Fixture-driven `/online` output continuations must not collapse back to the generic roster landing surface.");
    }

    [TestMethod]
    public void Online_alias_workspace_open_dossier_query_loads_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?workspace=preview-ws&command=open_character");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
            Assert.AreEqual("open_character", presenter.ExecutedCommandId);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("open-dossier", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-dialog-action"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Open Dossier");
        StringAssert.Contains(cut.Markup, "The shared import dialog opens from the clean public /online alias");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace-bound `/online` open-dossier continuations must not collapse back to the generic roster landing surface.");
    }

    [TestMethod]
    public void Online_alias_workspace_build_lab_query_loads_shared_shell_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context, includeActiveWorkspace: false);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?workspace=preview-ws&tab=tab-create");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);

            var appSurface = cut.Find("section.browser-app-roster[data-route-segment='online']");
            Assert.AreEqual("app", appSurface.GetAttribute("data-canonical-route"));
            Assert.AreEqual("online", appSurface.GetAttribute("data-route-alias"));
            Assert.AreEqual("online-alias", appSurface.GetAttribute("data-route-family"));
            Assert.AreEqual("build-lab", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-workflow"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-output-target"));
            Assert.AreEqual("none", appSurface.GetAttribute("data-chummer-app-startup-command"));
            Assert.AreEqual("tab-create", appSurface.GetAttribute("data-tab"));
            Assert.IsNotNull(cut.Find("[data-app-route-shared-shell='true']"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        StringAssert.Contains(cut.Markup, "Build Lab");
        StringAssert.Contains(cut.Markup, "Build Lab stays on the clean public /online alias");
        Assert.AreEqual(0, cut.FindAll(".browser-app-roster-tree").Count);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "Workspace build-lab continuations on `/online` must not collapse back to the roster landing surface.");
    }

    [DataTestMethod]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear", "online", "gear", "Gear", "Continue the shared Gear shell.", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Gear shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-rules", "online", "rules", "Rules", "Continue the shared Rules shell.", "The requested dossier context now opens directly into the Rules shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Rules shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-skills", "online", "skills", "Skills", "Continue the shared Skills shell.", "The requested dossier context now opens directly into the Skills shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Skills shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-cyberware", "online", "cyberware", "Cyberware", "Continue the shared Cyberware shell.", "The requested dossier context now opens directly into the Cyberware shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Cyberware shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-qualities", "online", "qualities", "Qualities", "Continue the shared Qualities shell.", "The requested dossier context now opens directly into the Qualities shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Qualities shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-adept", "online", "adept", "Adept", "Continue the shared Adept shell.", "The requested dossier context now opens directly into the Adept shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Adept shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-combat", "online", "combat", "Combat", "Continue the shared Combat shell.", "The requested dossier context now opens directly into the Combat shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Combat shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician", "online", "magic", "Magic", "Continue the shared Magic shell.", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Magic shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-critter", "online", "critter", "Critter", "Continue the shared Critter shell.", "The requested dossier context now opens directly into the Critter shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Critter shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-stats", "online", "stats", "Stats", "Continue the shared Stats shell.", "The requested dossier context now opens directly into the Stats shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Stats shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-technomancer", "online", "matrix", "Matrix", "Continue the shared Matrix shell.", "The requested dossier context now opens directly into the Matrix shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Matrix shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-contacts", "online", "contacts", "Contacts", "Continue the shared Contacts shell.", "The requested dossier context now opens directly into the Contacts shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Contacts shell", "from the clean public /online alias.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-calendar", "online", "career", "Career", "Continue the shared Career shell.", "The requested dossier context now opens directly into the Career shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Career shell", "from the clean public /online alias.")]
    public void Online_alias_workspace_tab_queries_render_specific_workflow_shell_copy(
        string route,
        string expectedRouteSegment,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedPanelTitle,
        string expectedPanelSummary,
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
            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, expectedPanelTitle);
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
        StringAssert.Contains(cut.Markup, expectedRouteSummaryFragment);
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-rules&control=show_source", "app", "The requested dossier context now opens directly into the Rules shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-rules&control=show_source", "online", "The requested dossier context now opens directly into the Rules shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_rules_source_control_routes_render_rules_shell_and_handle_control(
        string route,
        string expectedRouteSegment,
        string expectedPanelSummary)
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
            Assert.AreEqual("show_source", presenter.HandledUiControlId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("rules", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Rules", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Rules", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Rules shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Rules shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=toggle_free_paid", "app", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=toggle_free_paid", "online", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_gear_toggle_control_routes_render_gear_shell_and_handle_control(
        string route,
        string expectedRouteSegment,
        string expectedPanelSummary)
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
            Assert.AreEqual("toggle_free_paid", presenter.HandledUiControlId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("gear", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Gear", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Gear", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Gear shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Gear shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=gear_edit", "app", "gear_edit", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=gear_edit", "online", "gear_edit", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=gear_delete", "app", "gear_delete", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=gear_delete", "online", "gear_delete", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=gear_mount", "app", "gear_mount", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=gear_mount", "online", "gear_mount", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=gear_source", "app", "gear_source", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=gear_source", "online", "gear_source", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=drug_delete", "app", "drug_delete", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=drug_delete", "online", "drug_delete", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=vehicle_edit", "app", "vehicle_edit", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=vehicle_edit", "online", "vehicle_edit", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=vehicle_delete", "app", "vehicle_delete", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=vehicle_delete", "online", "vehicle_delete", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-gear&control=vehicle_mod_add", "app", "vehicle_mod_add", "The requested dossier context now opens directly into the Gear shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-gear&control=vehicle_mod_add", "online", "vehicle_mod_add", "The requested dossier context now opens directly into the Gear shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_gear_inventory_control_routes_render_gear_shell_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("gear", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Gear", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Gear", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Gear shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Gear shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-combat&control=combat_reload", "app", "combat_reload", "combat", "Combat", "The requested dossier context now opens directly into the Combat shell on the clean public route instead of dropping back to the roster landing surface.", "Combat shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-combat&control=combat_reload", "online", "combat_reload", "combat", "Combat", "The requested dossier context now opens directly into the Combat shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Combat shell")]
    [DataRow("/app?workspace=preview-ws&tab=tab-combat&control=combat_damage_track", "app", "combat_damage_track", "combat", "Combat", "The requested dossier context now opens directly into the Combat shell on the clean public route instead of dropping back to the roster landing surface.", "Combat shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-combat&control=combat_damage_track", "online", "combat_damage_track", "combat", "Combat", "The requested dossier context now opens directly into the Combat shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Combat shell")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=magic_add", "app", "magic_add", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=magic_add", "online", "magic_add", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=magic_bind", "app", "magic_bind", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=magic_bind", "online", "magic_bind", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=magic_source", "app", "magic_source", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=magic_source", "online", "magic_source", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/app?workspace=preview-ws&tab=tab-magician&control=magic_delete", "app", "magic_delete", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public route instead of dropping back to the roster landing surface.", "Magic shell")]
    [DataRow("/online?workspace=preview-ws&tab=tab-magician&control=magic_delete", "online", "magic_delete", "magic", "Magic", "The requested dossier context now opens directly into the Magic shell on the clean public /online alias instead of dropping back to the roster landing surface.", "Magic shell")]
    public void Public_app_combat_magic_control_routes_render_named_shells_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedWorkflow,
        string expectedWorkflowLabel,
        string expectedPanelSummary,
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual(expectedWorkflow, appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, $"Continue the shared {expectedWorkflowLabel} shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-cyberware&control=cyberware_edit", "app", "cyberware_edit", "The requested dossier context now opens directly into the Cyberware shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-cyberware&control=cyberware_edit", "online", "cyberware_edit", "The requested dossier context now opens directly into the Cyberware shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-cyberware&control=cyberware_delete", "app", "cyberware_delete", "The requested dossier context now opens directly into the Cyberware shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-cyberware&control=cyberware_delete", "online", "cyberware_delete", "The requested dossier context now opens directly into the Cyberware shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_cyberware_control_routes_render_cyberware_shell_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("cyberware", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Cyberware", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Cyberware", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Cyberware shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Cyberware shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-contacts&control=contact_edit", "app", "contact_edit", "The requested dossier context now opens directly into the Contacts shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-contacts&control=contact_edit", "online", "contact_edit", "The requested dossier context now opens directly into the Contacts shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-contacts&control=contact_remove", "app", "contact_remove", "The requested dossier context now opens directly into the Contacts shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-contacts&control=contact_remove", "online", "contact_remove", "The requested dossier context now opens directly into the Contacts shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-contacts&control=contact_connection", "app", "contact_connection", "The requested dossier context now opens directly into the Contacts shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-contacts&control=contact_connection", "online", "contact_connection", "The requested dossier context now opens directly into the Contacts shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_contacts_control_routes_render_contacts_shell_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("contacts", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Contacts", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Contacts", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Contacts shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Contacts shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-info&control=identity_license_add", "app", "identity_license_add", "The requested dossier context now opens directly into the Profile shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-info&control=identity_license_add", "online", "identity_license_add", "The requested dossier context now opens directly into the Profile shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-info&control=identity_license_edit", "app", "identity_license_edit", "The requested dossier context now opens directly into the Profile shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-info&control=identity_license_edit", "online", "identity_license_edit", "The requested dossier context now opens directly into the Profile shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    [DataRow("/app?workspace=preview-ws&tab=tab-info&control=identity_license_delete", "app", "identity_license_delete", "The requested dossier context now opens directly into the Profile shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-info&control=identity_license_delete", "online", "identity_license_delete", "The requested dossier context now opens directly into the Profile shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_profile_identity_license_control_routes_render_profile_shell_and_handle_controls(
        string route,
        string expectedRouteSegment,
        string expectedControlId,
        string expectedPanelSummary)
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

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("profile", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Profile shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Profile shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-info&control=open_notes", "app", "The requested dossier context now opens directly into the Profile shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-info&control=open_notes", "online", "The requested dossier context now opens directly into the Profile shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_profile_notes_control_routes_render_profile_shell_and_handle_control(
        string route,
        string expectedRouteSegment,
        string expectedPanelSummary)
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
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("profile", appSurface.GetAttribute("data-active-workflow"));
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Profile shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Profile shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-info&control=open_notes&dialog_action=save", "app", "The requested dossier context now opens directly into the Profile shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-info&control=open_notes&dialog_action=save", "online", "The requested dossier context now opens directly into the Profile shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_profile_notes_save_routes_render_profile_shell_and_committed_result(
        string route,
        string expectedRouteSegment,
        string expectedPanelSummary)
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
            Assert.AreEqual("open_notes", presenter.HandledUiControlId);
            Assert.AreEqual("save", presenter.ExecutedDialogActionId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("profile", appSurface.GetAttribute("data-active-workflow"));
            Assert.AreEqual("Notes saved.", cut.Find("[data-app-route-committed-result]").TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual("Profile", cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual("Profile", cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Profile shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, "Profile shell");
    }

    [DataTestMethod]
    [DataRow("/app?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add", "app", "Contacts", "Contacts shell", "The requested dossier context now opens directly into the Contacts shell on the clean public route instead of dropping back to the roster landing surface.")]
    [DataRow("/online?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add", "online", "Contacts", "Contacts shell", "The requested dossier context now opens directly into the Contacts shell on the clean public /online alias instead of dropping back to the roster landing surface.")]
    public void Public_app_contact_add_dialog_action_routes_render_contacts_shell_and_committed_result(
        string route,
        string expectedRouteSegment,
        string expectedWorkflowLabel,
        string expectedFrameTitle,
        string expectedPanelSummary)
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
            Assert.AreEqual("contact_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);

            var appSurface = cut.Find($"section.browser-app-roster[data-route-segment='{expectedRouteSegment}']");
            Assert.AreEqual("contacts", appSurface.GetAttribute("data-active-workflow"));

            var result = cut.Find("[data-app-route-committed-result]");
            Assert.AreEqual("Contact 'Fixer' added.", result.TextContent.Trim());
            Assert.IsNotNull(cut.Find(".desktop-shell"));
        });

        Assert.AreEqual(expectedWorkflowLabel, cut.Find("section.browser-app-roster h1").TextContent.Trim());
        Assert.AreEqual(expectedWorkflowLabel, cut.Find(".browser-app-startup-panel span").TextContent.Trim());
        StringAssert.Contains(cut.Markup, "Continue the shared Contacts shell.");
        StringAssert.Contains(cut.Markup, expectedPanelSummary);
        StringAssert.Contains(cut.Markup, expectedFrameTitle);
    }

    [TestMethod]
    public void Online_alias_origin_dossier_command_opens_dossier_builder_without_falling_back_to_roster()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/online?command=new_character_origin");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        Assert.AreEqual("/online", new Uri(navigation.Uri).AbsolutePath);
        StringAssert.Contains(navigation.Uri, "command=new_character_origin", StringComparison.Ordinal);
        StringAssert.Contains(cut.Markup, "Origin Dossier");
        StringAssert.Contains(cut.Markup, "Start the story-first dossier path.");
        StringAssert.Contains(cut.Markup, "data-chummer-app-startup-command=\"new_character_origin\"");
        StringAssert.Contains(cut.Markup, "data-browser-shell-command=\"new-character-origin\"");
        StringAssert.Contains(cut.Markup, "data-canonical-route=\"app\"");
        StringAssert.Contains(cut.Markup, "data-route-alias=\"online\"");
        StringAssert.Contains(cut.Markup, "data-route-family=\"online-alias\"");
        StringAssert.Contains(cut.Markup, "data-origin-dossier-route=\"app\"");
        StringAssert.Contains(cut.Markup, "data-origin-dossier-shared-shell=\"true\"");
        StringAssert.Contains(cut.Markup, "href=\"app?command=new_character_origin\"");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Use standard character creation");
        StringAssert.Contains(cut.Find(".browser-app-origin-actions").TextContent, "Return to Character Roster");
        Assert.IsFalse(cut.Markup.Contains("Start the story-first character path.", StringComparison.Ordinal));
        Assert.IsNotNull(cut.Find("[data-startup-command='new_character_origin']"));
        Assert.IsNotNull(cut.Find(".browser-app-origin-panel"));
        Assert.IsNotNull(cut.Find(".desktop-shell"));
        Assert.AreEqual("new_character_origin", presenter.ExecutedCommandId);
        Assert.IsFalse(
            cut.Markup.Contains(CharacterRosterLandingSummary, StringComparison.Ordinal),
            "The `/online` Origin Dossier alias must not silently fall back to the generic roster body.");
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
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64"] =
                    Convert.ToBase64String(Convert.FromHexString(
                        "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"))
            })
            .Build();
        context.Services.AddSingleton(configuration);
        var environment = new TestHostEnvironment();
        context.Services.AddSingleton<IHostEnvironment>(environment);
        context.Services.AddSingleton<IWebHostEnvironment>(environment);
        context.Services.AddHostedBuildOwnerInvalidationTokens(configuration);
        context.Services.AddSingleton<IOwnerContextAccessor>(
            new FixedOwnerContextAccessor(new OwnerScope("app-route-surface-test-owner")));
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

    private static string RenderAppMarkup(string baseUri, string uri)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.RemoveAll<NavigationManager>();
        context.Services.AddSingleton<NavigationManager>(new FixedNavigationManager(baseUri, uri));
        RegisterDesktopShellServices(context);
        return NormalizeRenderedMarkup(context.Render<App>().Markup);
    }

    private static string NormalizeRenderedMarkup(string markup)
    {
        string normalized = Regex.Replace(
            markup,
            @"\s+b-[a-z0-9]+(?=[\s/>])",
            string.Empty,
            RegexOptions.CultureInvariant);

        return Regex.Replace(
            normalized,
            @"\?build=[0-9a-f]{64}",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static void AssertReleaseAssetHref(string expectedPath, string actual)
    {
        const string revisionPrefix = "?build=";
        string expectedPrefix = expectedPath + revisionPrefix;
        Assert.IsTrue(
            actual.StartsWith(expectedPrefix, StringComparison.Ordinal),
            $"Release asset href '{actual}' must start with '{expectedPrefix}'.");
        Assert.IsTrue(
            BuildPwaReleaseContract.IsValidContentRevision(actual[expectedPrefix.Length..]),
            $"Release asset href '{actual}' must end with a canonical content revision.");
    }

    private static App CreateApp(string baseUri, string uri)
    {
        App app = new();
        SetInjectedProperty(app, "Navigation", new FixedNavigationManager(baseUri, uri));
        SetInjectedProperty(app, "Configuration", new ConfigurationBuilder().Build());
        SetInjectedProperty(app, "BuildPwaEnvironment", new TestHostEnvironment());
        return app;
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Could not find injected property '{propertyName}'.");

        property.SetValue(target, value);
    }

    private static string InvokeString(App app, string methodName, params object[]? args)
    {
        MethodInfo method = typeof(App).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Could not find method '{methodName}'.");

        return (string?)method.Invoke(app, args) ?? string.Empty;
    }

    private static string InvokeStaticString(string methodName, params object?[]? args)
    {
        MethodInfo? method = null;
        int argumentCount = args?.Length ?? 0;
        foreach (MethodInfo candidate in typeof(App).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (candidate.Name == methodName && candidate.GetParameters().Length == argumentCount)
            {
                method = candidate;
                break;
            }
        }

        if (method is null)
            throw new AssertFailedException($"Could not find static method '{methodName}' with {argumentCount} argument(s).");

        return (string?)method.Invoke(null, args) ?? string.Empty;
    }

    private static object? InvokeObject(App app, string methodName, params object[]? args)
    {
        MethodInfo method = typeof(App).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Could not find method '{methodName}'.");

        return method.Invoke(app, args);
    }

    private static T? GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Could not find property '{propertyName}' on '{target.GetType().Name}'.");

        object? value = property.GetValue(target);
        if (value is null)
            return default;

        if (value is T typedValue)
            return typedValue;

        throw new AssertFailedException(
            $"Property '{propertyName}' on '{target.GetType().Name}' was '{value.GetType().Name}', not '{typeof(T).Name}'.");
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
        public Task ApplyOriginDossierEditAsync(OriginDossierEditRequest request, CancellationToken ct) => Task.CompletedTask;
        public Task ApplyCollectionMutationAsync(WorkspaceCollectionMutationRequest request, CancellationToken ct) => Task.CompletedTask;
        public Task ApplyConditionMonitorEditAsync(ConditionMonitorEditRequest request, CancellationToken ct) => Task.CompletedTask;

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
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(AppRouteSurfaceTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new TestReleaseAssetFileProvider();
    }

    private sealed class FixedOwnerContextAccessor(OwnerScope owner) : IOwnerContextAccessor
    {
        public OwnerScope Current => owner;
    }

    private sealed class TestReleaseAssetFileProvider : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath)
        {
            string normalized = subpath.TrimStart('/');
            return BuildPwaReleaseContract.AssetPaths.Contains(normalized, StringComparer.Ordinal)
                ? new TestReleaseAssetFileInfo(normalized)
                : new NotFoundFileInfo(normalized);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
            => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class TestReleaseAssetFileInfo(string name) : IFileInfo
    {
        private readonly byte[] _content = System.Text.Encoding.UTF8.GetBytes(name);

        public bool Exists => true;

        public long Length => _content.LongLength;

        public string? PhysicalPath => null;

        public string Name => Path.GetFileName(name);

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(_content, writable: false);
    }

    private sealed class FixedNavigationManager : NavigationManager
    {
        public FixedNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, BuildUriContainedByBaseUri(baseUri, uri));
            OverrideNavigationState(baseUri, uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }

        private static string BuildUriContainedByBaseUri(string baseUri, string uri)
        {
            Uri requestedUri = new(uri, UriKind.Absolute);
            if (uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
                return requestedUri.ToString();

            Uri hostedBaseUri = new(baseUri, UriKind.Absolute);
            string relativePathAndQuery = $"{requestedUri.PathAndQuery}{requestedUri.Fragment}".TrimStart('/');
            return new Uri(hostedBaseUri, relativePathAndQuery).ToString();
        }

        private void OverrideNavigationState(string baseUri, string uri)
        {
            SetNavigationStateField("_baseUri", new Uri(baseUri, UriKind.Absolute), baseUri);
            SetNavigationStateField("_uri", new Uri(uri, UriKind.Absolute), uri);
        }

        private void SetNavigationStateField(string fieldName, Uri uriValue, string stringValue)
        {
            FieldInfo field = typeof(NavigationManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException($"Could not override NavigationManager field '{fieldName}'.");

            object value = field.FieldType == typeof(Uri)
                ? uriValue
                : field.FieldType == typeof(string)
                    ? stringValue
                    : throw new AssertFailedException($"NavigationManager field '{fieldName}' has unsupported type '{field.FieldType}'.");

            field.SetValue(this, value);
        }
    }
}
