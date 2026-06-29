#nullable enable annotations

using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.RunnerIntelligence;
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
        Assert.IsNotNull(cut.Find("[data-drop-target='runner-folder']"));
        Assert.IsFalse(cut.Markup.Contains("data-preview-proof-card=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-banner", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-promoted-app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("browser-preview-frame--app", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("desktop-shell", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("classic-chummer-shell", StringComparison.Ordinal));
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
