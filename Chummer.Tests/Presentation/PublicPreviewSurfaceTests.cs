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

        StringAssert.Contains(cut.Markup, "Chummer in the browser, with clear scope and no desktop cosplay.");
        StringAssert.Contains(cut.Markup, "This surface is the public evaluation lane for Chummer's workbench, rules guidance, and campaign-safe output review.");
        StringAssert.Contains(cut.Markup, "It is deliberately not the full desktop client.");
        StringAssert.Contains(cut.Markup, "href=\"/preview\"");
        StringAssert.Contains(cut.Markup, "href=\"/showcase\"");
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

        StringAssert.Contains(cut.Markup, "Browser-safe shell preview");
        StringAssert.Contains(cut.Markup, "Boundary:");
        StringAssert.Contains(cut.Markup, "guided browser preview");
        StringAssert.Contains(cut.Markup, "not as a claim of full desktop parity");
        StringAssert.Contains(cut.Markup, "href=\"/\"");
        StringAssert.Contains(cut.Markup, "href=\"/showcase\"");
        Assert.IsNotNull(cut.Find(".desktop-shell"));
    }

    private static void RegisterDesktopShellServices(BunitContext context)
    {
        CharacterWorkspaceId workspaceId = new("preview-ws");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Preview Runner",
            Alias: "PRV",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: RulesetDefaults.Sr5);
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
