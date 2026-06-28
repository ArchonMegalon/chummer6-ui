#nullable enable annotations

using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Pages;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        StringAssert.Contains(cut.Markup, "Chummer&#x27;s public browser workbench, grounded in the real product.");
        StringAssert.Contains(cut.Markup, "This surface is the public landing lane for the self-hosted Chummer portal");
        StringAssert.Contains(cut.Markup, "Character workflows are live in the browser where parity is actually implemented");
        StringAssert.Contains(cut.Markup, "href=\"/workbench\"");
        StringAssert.Contains(cut.Markup, "href=\"/showcase\"");
        StringAssert.Contains(cut.Markup, "href=\"/downloads/\"");
        StringAssert.Contains(cut.Markup, "href=\"/docs/\"");
        StringAssert.Contains(cut.Markup, "/workbench</code>");
        StringAssert.Contains(cut.Markup, "/preview</code>");
        StringAssert.Contains(cut.Markup, "Desktop client remains authoritative for");
        StringAssert.Contains(cut.Markup, "NPC Persona Studio");
        Assert.IsNotNull(cut.Find("main.public-preview"));
        Assert.IsNotNull(cut.Find("#boundaries"));
        Assert.IsNotNull(cut.Find("#proof"));
    }

    [TestMethod]
    public void Preview_renders_explicit_boundary_banner_around_desktop_shell()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "Chummer&#x27;s shared workbench shell, running in the browser.");
        StringAssert.Contains(cut.Markup, "Browser-owned here:");
        StringAssert.Contains(cut.Markup, "Desktop-owned still:");
        StringAssert.Contains(cut.Markup, "Published self-hosted Docker surface");
        StringAssert.Contains(cut.Markup, "Implicit owner sign-in");
        StringAssert.Contains(cut.Markup, "href=\"/\"");
        StringAssert.Contains(cut.Markup, "href=\"/showcase\"");
        StringAssert.Contains(cut.Markup, "href=\"/downloads/\"");
        StringAssert.Contains(cut.Markup, "href=\"/docs/\"");
        StringAssert.Contains(cut.Markup, "Open Build Lab");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;tab=tab-create");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;tab=tab-rules");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;tab=tab-technomancer");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;tab=tab-contacts");
        StringAssert.Contains(cut.Markup, "New runner");
        StringAssert.Contains(cut.Markup, "Open Runner");
        StringAssert.Contains(cut.Markup, "Open for Printing");
        StringAssert.Contains(cut.Markup, "Open for Export");
        StringAssert.Contains(cut.Markup, "Open Print Result");
        StringAssert.Contains(cut.Markup, "Open Export Result");
        StringAssert.Contains(cut.Markup, "Open Save Result");
        StringAssert.Contains(cut.Markup, "Open Save As Result");
        StringAssert.Contains(cut.Markup, "Open Origin Dossier");
        StringAssert.Contains(cut.Markup, "/preview?command=open_character");
        StringAssert.Contains(cut.Markup, "/preview?command=open_for_printing");
        StringAssert.Contains(cut.Markup, "/preview?command=open_for_export");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;command=print_character");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;command=export_character");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;command=save_character");
        StringAssert.Contains(cut.Markup, "/preview?fixture=blue&amp;command=save_character_as");
        StringAssert.Contains(cut.Markup, "/preview?command=new_character");
        StringAssert.Contains(cut.Markup, "/preview?command=new_character_origin");
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
    public void Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterDesktopShellServices(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench");
        IRenderedComponent<Preview> cut = context.Render<Preview>();

        StringAssert.Contains(cut.Markup, "This is the product-shaped browser workbench entrypoint");
        StringAssert.Contains(cut.Markup, "Open preview proof shelf");
        StringAssert.Contains(cut.Markup, "Start a new runner");
        StringAssert.Contains(cut.Markup, "Import an existing runner");
        StringAssert.Contains(cut.Markup, "Open a live seeded workspace");
        StringAssert.Contains(cut.Markup, "Continue PRV in build lab");
        StringAssert.Contains(cut.Markup, "Resume from restored session state");
        StringAssert.Contains(cut.Markup, "Resume PRV");
        StringAssert.Contains(cut.Markup, "saved");
        StringAssert.Contains(cut.Markup, "Continue restored runner lanes");
        StringAssert.Contains(cut.Markup, "Resume PRV on profile");
        StringAssert.Contains(cut.Markup, "Resume PRV on rules");
        StringAssert.Contains(cut.Markup, "Resume PRV on gear");
        StringAssert.Contains(cut.Markup, "Resume PRV on advanced");
        StringAssert.Contains(cut.Markup, "Add a contact for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep contact for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep complex form for PRV");
        StringAssert.Contains(cut.Markup, "Add a complex form for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep initiation for PRV");
        StringAssert.Contains(cut.Markup, "Add initiation for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Add cyberware for PRV");
        StringAssert.Contains(cut.Markup, "Add and keep spell for PRV");
        StringAssert.Contains(cut.Markup, "Add a spell for PRV");
        StringAssert.Contains(cut.Markup, "Continue a recent runner");
        StringAssert.Contains(cut.Markup, "Continue PRV on contacts");
        StringAssert.Contains(cut.Markup, "Continue PRV on profile");
        StringAssert.Contains(cut.Markup, "Continue PRV on rules");
        StringAssert.Contains(cut.Markup, "Continue PRV on gear");
        StringAssert.Contains(cut.Markup, "Continue PRV on advanced");
        StringAssert.Contains(cut.Markup, "Continue PRV for download");
        StringAssert.Contains(cut.Markup, "Continue PRV for export");
        StringAssert.Contains(cut.Markup, "Continue PRV for print");
        StringAssert.Contains(cut.Markup, "Review identity and profile");
        StringAssert.Contains(cut.Markup, "Review rules and references");
        StringAssert.Contains(cut.Markup, "Review loadout and gear");
        StringAssert.Contains(cut.Markup, "Open advanced build lanes");
        StringAssert.Contains(cut.Markup, "Prepare a browser download");
        StringAssert.Contains(cut.Markup, "Prepare an export package");
        StringAssert.Contains(cut.Markup, "Prepare a print preview");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?command=new_character\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?command=open_character\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-create\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws\"");
        StringAssert.Contains(cut.Markup, "data-workbench-recent-workspace=\"preview-ws\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-rules\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-gear\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-contacts&amp;control=contact_add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-technomancer&amp;control=complex_form_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-technomancer&amp;control=complex_form_add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-adept&amp;control=initiation_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-adept&amp;control=initiation_add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-cyberware&amp;control=cyberware_add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=spell_add&amp;dialog_action=add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-magician&amp;control=spell_add\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-contacts\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-info\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-rules\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-gear\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;tab=tab-technomancer\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;command=save_character_as\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;command=export_character\"");
        StringAssert.Contains(cut.Markup, "href=\"/workbench?workspace=preview-ws&amp;command=print_character\"");
        StringAssert.Contains(cut.Markup, "href=\"/preview\"");
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

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Command, commandId));

        Assert.AreEqual(commandId, presenter.ExecutedCommandId);
    }

    [TestMethod]
    public void Workbench_workspace_query_bootstraps_shared_workspace_load()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
    }

    [TestMethod]
    public void Workbench_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-contacts")
            .Add(component => component.Control, "contact_add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("contact_add", presenter.HandledUiControlId);
    }

    [TestMethod]
    public void Workbench_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-contacts")
            .Add(component => component.Control, "contact_add")
            .Add(component => component.DialogAction, "add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("contact_add", presenter.HandledUiControlId);
        Assert.AreEqual("add", presenter.ExecutedDialogActionId);
    }

    [TestMethod]
    public void Workbench_advanced_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-technomancer")
            .Add(component => component.Control, "complex_form_add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("complex_form_add", presenter.HandledUiControlId);
    }

    [TestMethod]
    public void Workbench_advanced_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-technomancer")
            .Add(component => component.Control, "complex_form_add")
            .Add(component => component.DialogAction, "add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("complex_form_add", presenter.HandledUiControlId);
        Assert.AreEqual("add", presenter.ExecutedDialogActionId);
    }

    [TestMethod]
    public void Workbench_initiation_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-adept&control=initiation_add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-adept")
            .Add(component => component.Control, "initiation_add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("initiation_add", presenter.HandledUiControlId);
    }

    [TestMethod]
    public void Workbench_initiation_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-adept&control=initiation_add&dialog_action=add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-adept")
            .Add(component => component.Control, "initiation_add")
            .Add(component => component.DialogAction, "add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("initiation_add", presenter.HandledUiControlId);
        Assert.AreEqual("add", presenter.ExecutedDialogActionId);
    }

    [TestMethod]
    public void Workbench_cyberware_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-cyberware")
            .Add(component => component.Control, "cyberware_add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("cyberware_add", presenter.HandledUiControlId);
    }

    [TestMethod]
    public void Workbench_cyberware_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_add&dialog_action=add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-cyberware")
            .Add(component => component.Control, "cyberware_add")
            .Add(component => component.DialogAction, "add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("cyberware_add", presenter.HandledUiControlId);
        Assert.AreEqual("add", presenter.ExecutedDialogActionId);
    }

    [TestMethod]
    public void Workbench_spell_control_query_bootstraps_shared_ui_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-magician&control=spell_add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-magician")
            .Add(component => component.Control, "spell_add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("spell_add", presenter.HandledUiControlId);
    }

    [TestMethod]
    public void Workbench_spell_dialog_action_query_bootstraps_shared_dialog_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        FakeCharacterOverviewPresenter presenter = RegisterDesktopShellServices(context);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add");

        context.Render<Preview>(parameters => parameters
            .Add(component => component.Workspace, "preview-ws")
            .Add(component => component.Tab, "tab-magician")
            .Add(component => component.Control, "spell_add")
            .Add(component => component.DialogAction, "add"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.AreEqual("spell_add", presenter.HandledUiControlId);
        Assert.AreEqual("add", presenter.ExecutedDialogActionId);
    }

    private static FakeCharacterOverviewPresenter RegisterDesktopShellServices(BunitContext context)
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
