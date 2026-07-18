#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Layout;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopShellStartupSyncTests
{
    [TestMethod]
    public void OnInitializedAsync_skips_sync_when_states_are_aligned()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-1");
        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(CreateOverviewState(workspaceId));
        RecordingShellPresenter shellPresenter = new(CreateShellState(workspaceId));
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        context.Render<DesktopShell>();

        Assert.AreEqual(1, shellPresenter.InitializeCalls);
        Assert.AreEqual(0, shellPresenter.SyncWorkspaceContextCalls);
    }

    [TestMethod]
    public void OnInitializedAsync_syncs_when_states_are_misaligned()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId presenterWorkspaceId = new("ws-2");
        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(CreateOverviewState(presenterWorkspaceId));
        RecordingShellPresenter shellPresenter = new(CreateShellState(new CharacterWorkspaceId("ws-1")));
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        context.Render<DesktopShell>();

        Assert.AreEqual(1, shellPresenter.InitializeCalls);
        Assert.AreEqual(1, shellPresenter.SyncWorkspaceContextCalls);
        Assert.AreEqual("ws-2", shellPresenter.LastSyncedWorkspaceId?.Value);
    }

    [TestMethod]
    public void ExecuteCommandAsync_keeps_startup_dialog_commands_off_the_workspace_sync_path()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        StartupDialogOverviewPresenter presenter = new(CreateStartupOverviewState());
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState(), throwOnSync: true);
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.Find("[data-startup-command='new_character']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(cut.Find("#dialogTitle"));
            Assert.AreEqual("New Character", cut.Find("#dialogTitle").TextContent.Trim());
        });

        Assert.AreEqual(0, shellPresenter.SyncWorkspaceContextCalls);
        Assert.AreEqual("new_character", presenter.ExecutedCommandId);
    }

    [TestMethod]
    public void ExecuteCommandFromSurfaceAsync_honors_shared_startup_command_availability_before_forwarding_to_overview_presenter()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition xmlEditor = new("xml_editor", "XML Editor", "tools", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition dataExporter = new("data_exporter", "Data Exporter", "tools", true, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);

        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: []),
            Commands = [menuRoot, xmlEditor, dataExporter]
        });

        RecordingShellPresenter shellPresenter = new(ShellState.Empty with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = [],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot, xmlEditor, dataExporter],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        });
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.Instance.ExecuteCommandFromSurfaceAsync("data_exporter").GetAwaiter().GetResult();
        Assert.IsNull(presenter.ExecutedCommandId);

        cut.Instance.ExecuteCommandFromSurfaceAsync("xml_editor").GetAwaiter().GetResult();

        CollectionAssert.AreEqual(new[] { "data_exporter", "xml_editor" }, shellPresenter.ExecutedCommandIds.ToArray());
        Assert.AreEqual("xml_editor", presenter.ExecutedCommandId);
        Assert.AreEqual(0, shellPresenter.SyncWorkspaceContextCalls);
    }

    [TestMethod]
    public void DemoWorkspaceId_loads_non_legacy_workspace_without_importing_seed_fixture()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        FakeCharacterOverviewPresenter presenter = new();
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        context.Render<DesktopShell>(parameters => parameters.Add(shell => shell.DemoWorkspaceId, "preview-ws"));

        Assert.AreEqual("preview-ws", presenter.LoadedWorkspaceId?.Value);
        Assert.IsNull(presenter.ImportedContent);
        Assert.IsNull(presenter.ImportedRulesetId);
    }

    [TestMethod]
    public void DemoWorkspaceId_legacy_seed_alias_skips_backend_load_and_warns()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        FakeCharacterOverviewPresenter presenter = new();
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>(
            parameters => parameters.Add(shell => shell.DemoWorkspaceId, "ws-1"));

        Assert.IsNull(presenter.LoadedWorkspaceId);
        Assert.IsNull(presenter.ImportedContent);
        StringAssert.Contains(cut.Markup, "data-demo-workspace-route-warning");
        StringAssert.Contains(cut.Markup, "legacy sample workspace link");
        var appRecoveryLink = cut.Find("[data-demo-workspace-route-recovery='app']");
        var workbenchRecoveryLink = cut.Find("[data-demo-workspace-route-recovery='workbench']");
        Assert.AreEqual("/app?fixture=blue&tab=tab-create", appRecoveryLink.GetAttribute("href"));
        Assert.AreEqual("/workbench?fixture=blue&tab=tab-create", workbenchRecoveryLink.GetAttribute("href"));
        StringAssert.Contains(appRecoveryLink.TextContent, "Open seeded Build Lab on Chummer Online");
        StringAssert.Contains(workbenchRecoveryLink.TextContent, "Open seeded compatibility shell");
    }

    [TestMethod]
    public void DesktopShell_origin_dossier_notice_renders_actionable_clean_route_affordance()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("preview-ws");
        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(CreateOverviewState(workspaceId) with
        {
            Notice = "Origin Dossier link: /app?command=new_character_origin&ruleset=sr5&alias=Cipher"
        });
        RecordingShellPresenter shellPresenter = new(CreateShellState(workspaceId));
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        var notice = cut.Find("[data-shell-notice-kind='origin-dossier-link']");
        var link = cut.Find("[data-shell-notice-link='origin-dossier']");
        var route = cut.Find("[data-shell-notice-route='origin-dossier']");

        StringAssert.Contains(notice.TextContent, "Origin Dossier link:");
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr5&alias=Cipher", link.GetAttribute("href"));
        StringAssert.Contains(link.TextContent, "Open clean Origin Dossier route");
        Assert.IsFalse(cut.Markup.Contains("Open Origin Dossier on Chummer Online", StringComparison.Ordinal));
        Assert.AreEqual("/app?command=new_character_origin&ruleset=sr5&alias=Cipher", route.TextContent.Trim());
    }

    [TestMethod]
    public void DesktopShell_publishes_classic_browser_execution_metadata_on_root_element()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        CharacterWorkspaceId workspaceId = new("ws-1");
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Nova Runner",
            Alias: "NOVA",
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
            WorkspaceId = workspaceId,
            ActiveTabId = "tab-create"
        };

        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition createTab = new("tab-create", "Create", "spark", "character", true, true, RulesetDefaults.Sr5);
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
            NavigationTabs = [createTab],
            ActiveTabId = createTab.Id
        };

        FakeCharacterOverviewPresenter presenter = new();
        presenter.Publish(overviewState);
        RecordingShellPresenter shellPresenter = new(shellState);
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blazor/workbench?workspace=ws-1&tab=tab-create");

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.WaitForAssertion(() =>
        {
            var shell = cut.Find("section.desktop-shell.classic-desktop-shell");
            Assert.AreEqual("tab-create", shell.GetAttribute("data-tab"));
            Assert.AreEqual("sr5", shell.GetAttribute("data-ruleset"));
            Assert.AreEqual("build-lab", shell.GetAttribute("data-active-workflow"));
            Assert.AreEqual("workbench", shell.GetAttribute("data-route-segment"));
            Assert.AreEqual("NOVA", shell.GetAttribute("data-active-runner"));
            Assert.AreEqual("NOVA", shell.GetAttribute("data-legacy-runner"));
        });
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter)
    {
        context.Services.AddSingleton(presenter);
        context.Services.AddSingleton(shellPresenter);
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault());
        context.Services.AddSingleton<IRulesetPlugin, Sr5RulesetPlugin>();
        context.Services.AddSingleton<IRulesetPluginRegistry, RulesetPluginRegistry>();
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
        context.Services.AddSingleton<Chummer.Blazor.Services.IWorkspacePrivacyLifecycleCapabilities>(
            Chummer.Blazor.Services.HostedBuildPrivacyLifecycleCapabilities.Instance);
    }

    private static CharacterOverviewState CreateOverviewState(CharacterWorkspaceId workspaceId)
    {
        OpenWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Runner",
            Alias: "RUN",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: "sr5");
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

    private static ShellState CreateShellState(CharacterWorkspaceId workspaceId)
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);
        ShellWorkspaceState openWorkspace = new(
            Id: workspaceId,
            Name: "Runner",
            Alias: "RUN",
            LastOpenedUtc: DateTimeOffset.UtcNow,
            RulesetId: "sr5");

        return ShellState.Empty with
        {
            ActiveWorkspaceId = workspaceId,
            OpenWorkspaces = [openWorkspace],
            ActiveRulesetId = "sr5",
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        };
    }

    private static CharacterOverviewState CreateStartupOverviewState()
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition openCharacter = new("open_character", "Open Runner", "file", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition newCharacter = new("new_character", "New Character", "file", false, true, RulesetDefaults.Sr5);

        return CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: []),
            Commands = [menuRoot, openCharacter, newCharacter]
        };
    }

    private static ShellState CreateStartupShellState()
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        AppCommandDefinition newCharacter = new("new_character", "New Character", "file", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);

        return ShellState.Empty with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = [],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot, newCharacter],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        };
    }

    private sealed class RecordingShellPresenter : IShellPresenter
    {
        private readonly bool _throwOnSync;

        public RecordingShellPresenter(ShellState state, bool throwOnSync = false)
        {
            State = state;
            _throwOnSync = throwOnSync;
        }

        public ShellState State { get; private set; }
        public int InitializeCalls { get; private set; }
        public int SyncWorkspaceContextCalls { get; private set; }
        public CharacterWorkspaceId? LastSyncedWorkspaceId { get; private set; }
        public List<string> ExecutedCommandIds { get; } = [];

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            SyncWorkspaceContextCalls++;
            if (_throwOnSync)
            {
                throw new InvalidOperationException("Workspace sync should not run for startup dialog commands.");
            }

            LastSyncedWorkspaceId = activeWorkspaceId;
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StartupDialogOverviewPresenter : ICharacterOverviewPresenter
    {
        public StartupDialogOverviewPresenter(CharacterOverviewState state)
        {
            State = state;
        }

        public CharacterOverviewState State { get; private set; }
        public string? ExecutedCommandId { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;
        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandId = commandId;
            State = State with
            {
                LastCommandId = commandId,
                ActiveDialog = new DesktopDialogState(
                    "dialog.new_character",
                    "New Character",
                    "Create a new runner.",
                    [],
                    [new DesktopDialogAction("cancel", "Cancel")])
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct) => Task.CompletedTask;
        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;
        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ExportAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PrintAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
