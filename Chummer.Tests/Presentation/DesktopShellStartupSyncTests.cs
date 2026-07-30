#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Layout;
using Chummer.Blazor.Services;
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
    public void Startup_dialog_locks_shell_chrome_and_keyboard_shortcuts()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        StartupDialogOverviewPresenter presenter = new(CreateStartupOverviewState());
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState(), throwOnSync: true);
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        Assert.IsFalse(cut.Find(".menu-btn").HasAttribute("disabled"));
        Assert.IsFalse(cut.FindAll(".tool-btn").All(button => button.HasAttribute("disabled")));

        cut.Find("[data-startup-command='new_character']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(cut.Find("#dialogTitle"));
            Assert.IsTrue(cut.Find(".menu-btn").HasAttribute("disabled"));
            Assert.IsTrue(cut.FindAll(".tool-btn").All(button => button.HasAttribute("disabled")));
        });

        int executionCountAfterDialogOpen = presenter.ExecutionCount;
        cut.Find(".desktop-shell").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
        {
            Key = "n",
            CtrlKey = true
        });

        Assert.AreEqual(
            executionCountAfterDialogOpen,
            presenter.ExecutionCount,
            "Ctrl+N should not re-dispatch while a modal dialog is open.");
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
    }

    [TestMethod]
    public void DemoFixtureId_imports_seed_fixture_and_rewrites_preview_route_to_workspace_query()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/preview?fixture=blue&tab=tab-rules");

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>(parameters => parameters
            .Add(shell => shell.DemoFixtureId, "blue")
            .Add(shell => shell.DemoTabId, "tab-rules"));

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual(RulesetDefaults.Sr5, presenter.ImportedRulesetId);
            Assert.AreEqual("fixture-ws", presenter.State.WorkspaceId?.Value);
            Assert.AreEqual("tab-rules", presenter.SelectedTabId);
            StringAssert.EndsWith(navigation.Uri, "/preview?workspace=fixture-ws&tab=tab-rules");
        });
    }

    [TestMethod]
    public void DemoFixtureId_rewrites_workbench_route_and_preserves_tab_control_and_dialog_action_queries()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/workbench?fixture=blue&tab=tab-magician&control=spell_add&dialog_action=add");

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>(parameters => parameters
            .Add(shell => shell.DemoFixtureId, "blue")
            .Add(shell => shell.DemoTabId, "tab-magician")
            .Add(shell => shell.DemoUiControlId, "spell_add")
            .Add(shell => shell.DemoDialogActionId, "add"));

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual("tab-magician", presenter.SelectedTabId);
            Assert.AreEqual("spell_add", presenter.HandledUiControlId);
            Assert.AreEqual("add", presenter.ExecutedDialogActionId);
            StringAssert.EndsWith(
                navigation.Uri,
                "/workbench?workspace=fixture-ws&tab=tab-magician&control=spell_add&dialog_action=add");
        });
    }

    [TestMethod]
    public void DemoFixtureId_rewrites_preview_route_and_preserves_output_command_and_dialog_action_queries()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        EnsureBrowserFixtureAvailable("BLUE.chum5");

        FixtureImportingOverviewPresenter presenter = new(CreateStartupOverviewState());
        RecordingShellPresenter shellPresenter = new(CreateStartupShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/preview?fixture=blue&command=export_character&dialog_action=download");

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>(parameters => parameters
            .Add(shell => shell.DemoFixtureId, "blue")
            .Add(shell => shell.DemoStartupCommandId, "export_character")
            .Add(shell => shell.DemoDialogActionId, "download"));

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(presenter.ImportedContent);
            Assert.AreEqual("export_character", presenter.ExecutedCommandId);
            Assert.AreEqual("download", presenter.ExecutedDialogActionId);
            StringAssert.EndsWith(
                navigation.Uri,
                "/preview?workspace=fixture-ws&command=export_character&dialog_action=download");
        });
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter)
    {
        context.Services.AddSingleton(presenter);
        context.Services.AddSingleton(shellPresenter);
        context.Services.AddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
            HostedBuildPrivacyLifecycleCapabilities.Instance);
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault());
        context.Services.AddSingleton<IRulesetPlugin, Sr5RulesetPlugin>();
        context.Services.AddSingleton<IRulesetPluginRegistry, RulesetPluginRegistry>();
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
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
        NavigationTabDefinition rulesTab = new("tab-rules", "Rules", "rules", "character", true, true, RulesetDefaults.Sr5);
        NavigationTabDefinition magicianTab = new("tab-magician", "Magician", "spells", "character", true, true, RulesetDefaults.Sr5);

        return ShellState.Empty with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = [],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot, newCharacter],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab, rulesTab, magicianTab],
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

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            if (State.NavigationTabs.Any(tab => string.Equals(tab.Id, tabId, StringComparison.Ordinal)))
            {
                State = State with { ActiveTabId = tabId };
                StateChanged?.Invoke(this, EventArgs.Empty);
            }

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
        public int ExecutionCount { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;
        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandId = commandId;
            ExecutionCount++;
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
}
