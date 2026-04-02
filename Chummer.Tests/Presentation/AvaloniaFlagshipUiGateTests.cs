#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Contracts.AI;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaFlagshipUiGateTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("FileMenuButton");

            harness.WaitUntil(() =>
            {
                Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return host is not null && host.Children.Count > 0;
            });

            Panel menuHost = harness.FindControl<Panel>("MenuCommandsHost");
            string[] visibleCommands = menuHost.Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.Contains(visibleCommands, "open character");
            CollectionAssert.Contains(visibleCommands, "save character");
        });
    }

    [TestMethod]
    public void Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("SettingsButton");

            harness.WaitUntil(() =>
            {
                TextBlock? title = harness.FindControlOrDefault<TextBlock>("DialogTitleText");
                Panel? fields = harness.FindControlOrDefault<Panel>("DialogFieldsHost");
                Panel? actions = harness.FindControlOrDefault<Panel>("DialogActionsHost");
                return string.Equals(title?.Text, "Global Settings", StringComparison.Ordinal)
                    && fields is not null
                    && fields.Children.Count > 0
                    && actions is not null
                    && actions.Children.OfType<Button>().Any();
            });

            Panel fieldsHost = harness.FindControl<Panel>("DialogFieldsHost");
            Panel actionsHost = harness.FindControl<Panel>("DialogActionsHost");

            Assert.IsTrue(fieldsHost.Children.OfType<Control>().Any());
            Assert.IsTrue(actionsHost.Children.OfType<Button>().Any(button =>
                string.Equals(button.Content?.ToString(), "Save", StringComparison.OrdinalIgnoreCase)));

            harness.Click("FileMenuButton");
            harness.WaitUntil(() =>
            {
                Panel? menuHost = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                return menuHost is not null && menuHost.Children.Count > 0;
            });
        });
    }

    [TestMethod]
    public void Load_demo_runner_button_dispatches_import_when_fixture_is_available()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.Presenter.ImportCalls > 0);
                Assert.IsTrue(harness.Presenter.LastImportedDocument is not null);
                StringAssert.Contains(harness.Presenter.LastImportedDocument!.Content, "<character");
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Keyboard_shortcuts_resolve_to_the_same_shell_commands()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            harness.PressKey(Key.S, RawInputModifiers.Control);
            harness.WaitUntil(() =>
                string.Equals(harness.ShellPresenter.State.LastCommandId, "save_character", StringComparison.Ordinal)
                && string.Equals(harness.Presenter.State.LastCommandId, "save_character", StringComparison.Ordinal));

            harness.PressKey(Key.G, RawInputModifiers.Control);
            harness.WaitUntil(() =>
                string.Equals(harness.ShellPresenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                && string.Equals(harness.Presenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                && string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void Desktop_shell_preserves_chummer5a_familiarity_cues()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Panel menuPanel = harness.FindControl<Panel>("MenuBarPanel");
            string[] menuLabels = menuPanel.Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "File",
                    "Edit",
                    "Special",
                    "Tools",
                    "Windows",
                    "Help"
                },
                menuLabels);

            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control contentRegion = harness.FindControl<Control>("ContentRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");
            ProgressBar progressBar = harness.FindControl<ProgressBar>("WorkbenchProgressBar");

            Point menuTop = harness.TranslateToWindow(harness.FindControl<Control>("MenuBarRegion"));
            Point toolTop = harness.TranslateToWindow(toolStripRegion);
            Point contentTop = harness.TranslateToWindow(contentRegion);
            Point statusTop = harness.TranslateToWindow(statusStripRegion);

            Assert.IsTrue(toolTop.Y > menuTop.Y);
            Assert.IsTrue(contentTop.Y > toolTop.Y);
            Assert.IsTrue(statusTop.Y > contentTop.Y);
            Assert.IsTrue(progressBar.Value >= 100d);
        });
    }

    [TestMethod]
    public void Visual_review_evidence_is_published_for_light_and_dark_shell_states()
    {
        string screenshotDirectory = ResolveScreenshotDirectory();
        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, recursive: true);
        }

        Directory.CreateDirectory(screenshotDirectory);

        string[] expectedFiles =
        [
            "01-initial-shell-light.png",
            "02-menu-open-light.png",
            "03-settings-open-light.png",
            "04-loaded-runner-light.png",
            "05-dense-section-light.png",
            "06-dense-section-dark.png"
        ];

        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, byte[]> screenshots = WithHarness(harness =>
            {
                Dictionary<string, byte[]> captured = new(StringComparer.Ordinal);

                harness.WaitForReady();

                harness.SetTheme(ThemeVariant.Light);
                captured[expectedFiles[0]] = harness.CaptureScreenshotBytes();

                harness.Click("FileMenuButton");
                harness.WaitUntil(() =>
                {
                    Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                    return host is not null && host.Children.Count > 0;
                });
                captured[expectedFiles[1]] = harness.CaptureScreenshotBytes();

                harness.Click("FileMenuButton");
                harness.WaitUntil(() =>
                {
                    Panel? host = harness.FindControlOrDefault<Panel>("MenuCommandsHost");
                    return host is not null && host.Children.Count == 0;
                });

                harness.PressKey(Key.G, RawInputModifiers.Control);
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                captured[expectedFiles[2]] = harness.CaptureScreenshotBytes();

                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.Presenter.ImportCalls > 0);
                captured[expectedFiles[3]] = harness.CaptureScreenshotBytes();
                captured[expectedFiles[4]] = harness.CaptureScreenshotBytes();

                harness.SetTheme(ThemeVariant.Dark);
                captured[expectedFiles[5]] = harness.CaptureScreenshotBytes();

                return captured;
            });

            foreach ((string fileName, byte[] pngBytes) in screenshots)
            {
                File.WriteAllBytes(Path.Combine(screenshotDirectory, fileName), pngBytes);
            }
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        foreach (string fileName in expectedFiles)
        {
            string fullPath = Path.Combine(screenshotDirectory, fileName);
            Assert.IsTrue(File.Exists(fullPath), $"Expected screenshot evidence '{fileName}' was not created.");

            FileInfo fileInfo = new(fullPath);
            Assert.IsTrue(fileInfo.Length > 0, $"Screenshot evidence '{fileName}' is empty.");
        }
    }

    private static void WithHarness(Action<FlagshipUiHarness> assertion)
    {
        WithHarness<bool>(harness =>
        {
            assertion(harness);
            return true;
        });
    }

    private static TResult WithHarness<TResult>(Func<FlagshipUiHarness, TResult> assertion)
    {
        EnsureHeadlessPlatform();
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
        return session.Dispatch(() =>
            {
                using FlagshipUiHarness harness = new();
                return assertion(harness);
            },
            CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return;
            }

            _headlessInitialized = true;
        }
    }

    private sealed class FlagshipHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                });
        }
    }

    private static string FindTestFilePath(string fileName)
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Tests", "TestFiles", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", fileName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", fileName),
            Path.Combine("/src", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer-presentation/Chummer.Tests/TestFiles", fileName)
        };

        string? match = candidates.FirstOrDefault(path => File.Exists(path));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate test file.", fileName);
        }

        return match;
    }

    private static string ResolveScreenshotDirectory()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CHUMMER_UI_GATE_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                ".codex-studio",
                "out",
                "test-ui-flagship-gate",
                Guid.NewGuid().ToString("N")));
    }

    private sealed class FlagshipUiHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;
        private readonly RecordingCharacterOverviewPresenter _presenter;

        public FlagshipUiHarness()
        {
            _presenter = new RecordingCharacterOverviewPresenter();
            _adapter = new CharacterOverviewViewModelAdapter(_presenter);
            ShellPresenter = new RecordingShellPresenter(CreateShellState());
            var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
            var pluginRegistry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            var shellCatalogResolver = new RulesetShellCatalogResolverService(pluginRegistry);
            Window = new MainWindow(
                _presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public MainWindow Window { get; }
        public RecordingCharacterOverviewPresenter Presenter => _presenter;
        public RecordingShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() => ShellPresenter.InitializeCalls > 0 && _presenter.InitializeCalls > 0);
        }

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public Point TranslateToWindow(Control control)
        {
            Point? translated = control.TranslatePoint(default, Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{control.Name ?? control.GetType().Name}' to window coordinates.");
            return translated!.Value;
        }

        public void ClickDialogAction(string actionId)
        {
            Button actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Dialog action '{actionId}' was not found.");

            Point? translated = actionButton.TranslatePoint(
                new Point(actionButton.Bounds.Width / 2d, actionButton.Bounds.Height / 2d),
                Window);
            Assert.IsNotNull(translated, $"Unable to translate dialog action '{actionId}' to window coordinates.");

            Point location = translated!.Value;
            Window.MouseMove(location, RawInputModifiers.None);
            Window.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void InvokeDialogAction(string actionId)
        {
            Button actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Dialog action '{actionId}' was not found.");

            actionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers);
            Pump();
        }

        public void SetTheme(ThemeVariant themeVariant)
        {
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = themeVariant;
            }

            Window.RequestedThemeVariant = themeVariant;
            Window.InvalidateVisual();
            Pump();
        }

        public byte[] CaptureScreenshotBytes()
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Pump();
            using var bitmap = Window.CaptureRenderedFrame();
            if (bitmap is null)
            {
                throw new AssertFailedException("No rendered frame was available for screenshot capture.");
            }

            using MemoryStream output = new();
            bitmap.Save(output);
            return output.ToArray();
        }

        public T FindControl<T>(string name)
            where T : Control
        {
            return FindControlOrDefault<T>(name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found.");
        }

        public T? FindControlOrDefault<T>(string name)
            where T : Control
        {
            return Window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 2000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return;
                }

                Pump();
            }

            Assert.Fail("Timed out waiting for UI condition.");
        }

        private static void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        private IEnumerable<Button> DialogActionButtons()
        {
            Panel actionsHost = FindControl<Panel>("DialogActionsHost");
            return actionsHost.Children.OfType<Button>();
        }

        public void Dispose()
        {
            Window.Close();
            _adapter.Dispose();
        }
    }

    private sealed class RecordingCharacterOverviewPresenter : ICharacterOverviewPresenter
    {
        private CharacterOverviewState _state = CharacterOverviewState.Empty;

        public CharacterOverviewState State => _state;
        public event EventHandler? StateChanged;

        public int InitializeCalls { get; private set; }
        public int ImportCalls { get; private set; }
        public WorkspaceImportDocument? LastImportedDocument { get; private set; }

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            Publish(_state);
            return Task.CompletedTask;
        }

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            ImportCalls++;
            LastImportedDocument = document;

            CharacterWorkspaceId workspaceId = new("demo-runner");
            OpenWorkspaceState workspace = new(
                Id: workspaceId,
                Name: "Soma",
                Alias: "Demo",
                LastOpenedUtc: DateTimeOffset.UtcNow,
                RulesetId: RulesetDefaults.Sr5);

            Publish(_state with
            {
                WorkspaceId = workspaceId,
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: workspaceId,
                    OpenWorkspaces: [workspace],
                    RecentWorkspaceIds: [workspaceId]),
                OpenWorkspaces = [workspace],
                Profile = new CharacterProfileSection(
                    "Soma",
                    "Demo",
                    "QA",
                    "Human",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Street Sam",
                    "Runner demo",
                    string.Empty,
                    "6.0",
                    "6.0",
                    "Priority",
                    "Standard",
                    Created: true,
                    Adept: false,
                    Magician: false,
                    Technomancer: false,
                    AI: false,
                    MainMugshotIndex: 0,
                    MugshotCount: 0),
                ActiveSectionJson = """
{
  "name": "Soma",
  "ruleset": "sr5",
  "metatype": "Human",
  "priority": "Standard",
  "role": "Street Sam",
  "attributes": {
    "Body": 5,
    "Agility": 7,
    "Reaction": 6,
    "Strength": 4,
    "Willpower": 3,
    "Logic": 3
  },
  "combat": {
    "initiative": "11 + 2d6",
    "armor": 12,
    "essence": 5.34
  }
}
""",
                ActiveSectionRows =
                [
                    new SectionRowState("attributes.body", "5"),
                    new SectionRowState("attributes.agility", "7"),
                    new SectionRowState("attributes.reaction", "6"),
                    new SectionRowState("skills.firearms[0]", "Automatics 6"),
                    new SectionRowState("skills.stealth[0]", "Sneaking 5"),
                    new SectionRowState("gear.weapons[0]", "Ares Alpha"),
                    new SectionRowState("gear.armor[0]", "Armor Jacket"),
                    new SectionRowState("cyberware[0]", "Wired Reflexes 2"),
                    new SectionRowState("contacts[0]", "Fixer (Loyalty 4 / Connection 5)"),
                    new SectionRowState("notes.runner_goal", "Ready for a flagship shell smoke pass")
                ],
                HasSavedWorkspace = false,
                Error = null
            });

            return Task.CompletedTask;
        }

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;
        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            if (string.Equals(commandId, "global_settings", StringComparison.Ordinal))
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    ActiveDialog = new DesktopDialogState(
                        Id: "dialog.global_settings",
                        Title: "Global Settings",
                        Message: "Adjust shell scale, theme, and language.",
                        Fields:
                        [
                            new DesktopDialogField("globalUiScale", "UI Scale (%)", "100", "100", InputType: "number"),
                            new DesktopDialogField("globalTheme", "Theme", "dark-steel", "dark-steel"),
                            new DesktopDialogField("globalCompactMode", "Compact Mode", "true", "false", InputType: "checkbox")
                        ],
                        Actions:
                        [
                            new DesktopDialogAction("save", "Save", IsPrimary: true),
                            new DesktopDialogAction("cancel", "Cancel")
                        ]),
                    Error = null
                });
            }
            else
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
            DesktopDialogState? dialog = _state.ActiveDialog;
            if (dialog is null)
            {
                return Task.CompletedTask;
            }

            Publish(_state with
            {
                ActiveDialog = dialog with
                {
                    Fields = dialog.Fields
                        .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                            ? field with { Value = value ?? string.Empty }
                            : field)
                        .ToArray()
                }
            });

            return Task.CompletedTask;
        }

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
        {
            if (string.Equals(actionId, "cancel", StringComparison.Ordinal)
                || string.Equals(actionId, "save", StringComparison.Ordinal))
            {
                Publish(_state with
                {
                    ActiveDialog = null,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task CloseDialogAsync(CancellationToken ct)
        {
            Publish(_state with { ActiveDialog = null, Error = null });
            return Task.CompletedTask;
        }

        private void Publish(CharacterOverviewState state)
        {
            _state = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
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
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
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
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
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
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
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
    }

    private static ShellState CreateShellState()
    {
        AppCommandDefinition[] commands =
        [
            new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5),
            new("edit", "menu.edit", "menu", false, true, RulesetDefaults.Sr5),
            new("special", "menu.special", "menu", false, true, RulesetDefaults.Sr5),
            new("tools", "menu.tools", "menu", false, true, RulesetDefaults.Sr5),
            new("windows", "menu.windows", "menu", false, true, RulesetDefaults.Sr5),
            new("help", "menu.help", "menu", false, true, RulesetDefaults.Sr5),
            new("open_character", "command.open_character", "file", false, true, RulesetDefaults.Sr5),
            new("save_character", "command.save_character", "file", true, true, RulesetDefaults.Sr5),
            new("global_settings", "command.global_settings", "tools", false, true, RulesetDefaults.Sr5),
            new("about", "command.about", "help", false, true, RulesetDefaults.Sr5)
        ];

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
