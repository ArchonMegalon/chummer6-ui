using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Contracts.AI;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Avalonia;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaHeadlessSmokeTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;

    [TestMethod]
    public async Task Avalonia_headless_import_edit_switch_save_smoke()
    {
        FakeCharacterOverviewPresenter presenter = new();
        using CharacterOverviewViewModelAdapter adapter = new(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(Encoding.UTF8.GetBytes("<character />"), CancellationToken.None);

        CharacterWorkspaceId workspaceA = new("headless-workspace-a");
        CharacterWorkspaceId workspaceB = new("headless-workspace-b");
        await adapter.SwitchWorkspaceAsync(workspaceA, CancellationToken.None);
        await adapter.SwitchWorkspaceAsync(workspaceB, CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Headless Runner", "HR1", "headless smoke"), CancellationToken.None);
        await presenter.SaveAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.InitializeCalls);
        Assert.AreEqual(1, presenter.SaveCalls);
        Assert.IsNotNull(presenter.ImportedContent);
        Assert.AreEqual(workspaceB.Value, presenter.SwitchedWorkspaceId?.Value);
        Assert.AreEqual("Headless Runner", presenter.UpdatedMetadata?.Name);
        Assert.AreEqual("HR1", presenter.UpdatedMetadata?.Alias);
    }

    [TestMethod]
    public void Avalonia_headless_main_window_menu_command_smoke()
    {
        EnsureHeadlessPlatform();

        using HeadlessMainWindowHarness harness = new();
        harness.WaitForReady();

        MenuItem fileMenuButton = harness.FindMenuButton("FileMenuButton");
        fileMenuButton.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        harness.Pump();

        CollectionAssert.Contains(harness.ShellPresenter.ToggledMenuIds, "file");

        MenuItem newCharacterCommand = harness.FindMenuCommand("new_character");
        Assert.IsTrue(newCharacterCommand.IsEnabled, "new_character should be executable from the visible menu surface.");
        newCharacterCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        harness.Pump();

        CollectionAssert.Contains(harness.ShellPresenter.ExecutedCommandIds, "new_character");

    }

    [TestMethod]
    public void Avalonia_headless_platform_bootstrap_reference()
    {
        EnsureHeadlessPlatform();
        Assert.IsTrue(_headlessInitialized);
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
                return;

            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont()
                .SetupWithoutStarting();
            _headlessInitialized = true;
        }
    }

    private sealed class HeadlessMainWindowHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;
        private readonly FakeCharacterOverviewPresenter _presenter;

        public HeadlessMainWindowHarness()
        {
            _presenter = new FakeCharacterOverviewPresenter();
            _adapter = new CharacterOverviewViewModelAdapter(_presenter);
            ShellPresenter = new RecordingShellPresenter(CreateShellState());
            DefaultCommandAvailabilityEvaluator availabilityEvaluator = new();
            RulesetPluginRegistry pluginRegistry = new([new Sr5RulesetPlugin()]);
            RulesetShellCatalogResolverService shellCatalogResolver = new(pluginRegistry);
            Window = new MainWindow(
                _presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Pump();
        }

        public MainWindow Window { get; }
        public RecordingShellPresenter ShellPresenter { get; }

        public void WaitForReady()
            => WaitUntil(() =>
                ShellPresenter.InitializeCalls > 0
                && _presenter.InitializeCalls > 0
                && Window.IsVisible
                && Window.Bounds.Width > 0d
                && Window.Bounds.Height > 0d);

        public MenuItem FindMenuButton(string name)
            => ResolveActiveMenuSurface().FindControl<MenuItem>(name)
                ?? throw new InvalidOperationException($"Menu button '{name}' was not found on the active menu surface.");

        public MenuItem FindMenuCommand(string commandId)
        {
            MenuItem fileMenuButton = FindMenuButton("FileMenuButton");
            return fileMenuButton.Items.OfType<MenuItem>()
                .Single(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal));
        }

        private Control ResolveActiveMenuSurface()
            => Window.ControlsForAutomation.MenuBar as Control
                ?? throw new InvalidOperationException("Active menu surface does not expose a control.");

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            Window.Close();
            Pump();
            _adapter.Dispose();
        }

        private static void WaitUntil(Func<bool> predicate, int maxIterations = 20)
        {
            for (int attempt = 0; attempt < maxIterations; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return;
                }
            }

            Assert.Fail("Timed out while waiting for the Avalonia headless main-window harness to settle.");
        }
    }

    private sealed class RecordingShellPresenter : IShellPresenter
    {
        public RecordingShellPresenter(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }
        public int InitializeCalls { get; private set; }
        public List<string> ExecutedCommandIds { get; } = [];
        public List<string> SelectedTabIds { get; } = [];
        public List<string> ToggledMenuIds { get; } = [];
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            State = State with
            {
                LastCommandId = commandId,
                OpenMenuId = null,
                Notice = $"Command '{commandId}' dispatched."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabIds.Add(tabId);
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
            ToggledMenuIds.Add(menuId);
            State = State with
            {
                OpenMenuId = string.Equals(State.OpenMenuId, menuId, StringComparison.Ordinal) ? null : menuId,
                Notice = $"Menu '{menuId}' opened."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            ShellWorkspaceState[] openWorkspaces = activeWorkspaceId is null
                ? []
                : [
                    new ShellWorkspaceState(
                        activeWorkspaceId.Value,
                        "Soma",
                        "Demo",
                        DateTimeOffset.UtcNow,
                        RulesetDefaults.Sr5,
                        HasSavedWorkspace: false)
                ];
            State = State with
            {
                ActiveWorkspaceId = activeWorkspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveTabId = activeWorkspaceId is null ? State.ActiveTabId : "tab-info",
                Notice = activeWorkspaceId is null ? "Ready." : $"Restored {openWorkspaces.Length} workspace(s)."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCoachSidecarClient : IAvaloniaCoachSidecarClient
    {
        public Task<AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
            string routeType,
            string? runtimeFingerprint = null,
            int maxCount = 3,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendCoachTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendBuildTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Failure(0, "disabled"));
    }

    private static ShellState CreateShellState()
    {
        AppCommandDefinition[] commands = new CatalogOnlyRulesetShellCatalogResolver()
            .ResolveCommands(RulesetDefaults.Sr5)
            .ToArray();

        return ShellState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            PreferredRulesetId = RulesetDefaults.Sr5,
            Commands = commands,
            MenuRoots = commands.Where(command => string.Equals(command.Group, "menu", StringComparison.Ordinal)).ToArray(),
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-info", "Info", "summary", "character", true, true, RulesetDefaults.Sr5)
            ],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
    }
}
