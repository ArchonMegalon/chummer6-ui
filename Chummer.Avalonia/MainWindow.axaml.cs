using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Chummer.Contracts.Presentation;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Presentation.UiKit;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

public partial class MainWindow : Window
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;
    private const string DesktopHeadId = "avalonia";
    private readonly IShellPresenter _shellPresenter;
    private readonly ICommandAvailabilityEvaluator _commandAvailabilityEvaluator;
    private readonly IShellSurfaceResolver _shellSurfaceResolver;
    private readonly IAvaloniaCoachSidecarClient _coachSidecarClient;
    private readonly DesktopAnalyticsClient _desktopAnalyticsClient;
    private readonly CharacterOverviewViewModelAdapter _adapter;
    private readonly MainWindowActionExecutionCoordinator _actionExecutionCoordinator;
    private readonly MainWindowInteractionCoordinator _interactionCoordinator;
    private readonly MainWindowLifecycleCoordinator _lifecycleCoordinator;
    private readonly MainWindowTransientStateCoordinator _transientStateCoordinator;
    private readonly MainWindowControls _controls;
    private DesktopPreferenceState _persistedPreferences;
    private DesktopInstallLinkingState _installLinkingState;

    public MainWindow()
        : this(
            ResolveService<ICharacterOverviewPresenter>(),
            ResolveService<IShellPresenter>(),
            ResolveService<ICommandAvailabilityEvaluator>(),
            ResolveService<IShellSurfaceResolver>(),
            ResolveService<IAvaloniaCoachSidecarClient>(),
            ResolveService<DesktopAnalyticsClient>(),
            ResolveService<CharacterOverviewViewModelAdapter>())
    {
    }

    public MainWindow(
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator,
        IShellSurfaceResolver shellSurfaceResolver,
        IAvaloniaCoachSidecarClient coachSidecarClient,
        CharacterOverviewViewModelAdapter adapter)
        : this(
            presenter,
            shellPresenter,
            commandAvailabilityEvaluator,
            shellSurfaceResolver,
            coachSidecarClient,
            new DesktopAnalyticsClient(App.Services?.GetService<HttpClient>() ?? new HttpClient { BaseAddress = new Uri("https://chummer.run/") }),
            adapter)
    {
    }

    public MainWindow(
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter,
        ICommandAvailabilityEvaluator commandAvailabilityEvaluator,
        IShellSurfaceResolver shellSurfaceResolver,
        IAvaloniaCoachSidecarClient coachSidecarClient,
        DesktopAnalyticsClient desktopAnalyticsClient,
        CharacterOverviewViewModelAdapter adapter)
    {
        _persistedPreferences = DesktopPreferenceRuntime.LoadOrCreateState(DesktopHeadId);
        DesktopPreferenceStateRuntime.SetCurrent(_persistedPreferences);
        DesktopLocalizationCatalog.SetCurrentLanguageOverride(_persistedPreferences.Language);
        _installLinkingState = DesktopInstallLinkingRuntime.LoadOrCreateState(DesktopHeadId);
        InitializeComponent();
        ApplyInstallLinkingChrome(_installLinkingState);
        TryApplyWindowIcon();

        _shellPresenter = shellPresenter;
        _commandAvailabilityEvaluator = commandAvailabilityEvaluator;
        _shellSurfaceResolver = shellSurfaceResolver;
        _coachSidecarClient = coachSidecarClient;
        _desktopAnalyticsClient = desktopAnalyticsClient;
        _adapter = adapter;
        _actionExecutionCoordinator = new MainWindowActionExecutionCoordinator(
            adapter,
            shellPresenter,
            ApplyUiActionFailure);
        _interactionCoordinator = new MainWindowInteractionCoordinator(presenter, shellPresenter, adapter);
        _transientStateCoordinator = new MainWindowTransientStateCoordinator();

        _controls = MainWindowControlBinder.Bind(
            toolStrip: ToolStripControl,
            classicToolStrip: ClassicToolStripControl,
            summaryHeader: SummaryHeaderControl,
            menuBar: ShellMenuBarControl,
            classicMenuBar: ClassicMenuBarControl,
            characterRoster: CharacterRosterControl,
            navigatorPane: NavigatorPaneControl,
            classicFormPortHost: ClassicFormPortHostControl,
            sectionHost: SectionHostControl,
            commandDialogPane: CommandDialogPaneControl,
            coachSidecar: CoachSidecarControl,
            statusStrip: StatusStripControl,
            classicStatusStrip: ClassicStatusStripControl,
            onImportFileRequested: ToolStrip_OnImportFileRequested,
            onOpenForPrintingRequested: ToolStrip_OnOpenForPrintingRequested,
            onOpenForExportRequested: ToolStrip_OnOpenForExportRequested,
            onImportRawRequested: ToolStrip_OnImportRawRequested,
            onAutoAliceRequested: ToolStrip_OnAutoAliceRequested,
            onSaveRequested: ToolStrip_OnSaveRequested,
            onPrintRequested: ToolStrip_OnPrintRequested,
            onCopyRequested: ToolStrip_OnCopyRequested,
            onDesktopHomeRequested: ToolStrip_OnDesktopHomeRequested,
            onHorizonsRequested: ToolStrip_OnHorizonsRequested,
            onGmPrepRequested: ToolStrip_OnGmPrepRequested,
            onRosterMovementRequested: ToolStrip_OnRosterMovementRequested,
            onRuleEnvironmentStudioRequested: ToolStrip_OnRuleEnvironmentStudioRequested,
            onCloseWorkspaceRequested: ToolStrip_OnCloseWorkspaceRequested,
            onCampaignWorkspaceRequested: ToolStrip_OnCampaignWorkspaceRequested,
            onUpdateStatusRequested: ToolStrip_OnUpdateStatusRequested,
            onInstallLinkingRequested: ToolStrip_OnInstallLinkingRequested,
            onSupportRequested: ToolStrip_OnSupportRequested,
            onReportIssueRequested: ToolStrip_OnReportIssueRequested,
            onSettingsRequested: ToolStrip_OnSettingsRequested,
            onLoadDemoRunnerRequested: ToolStrip_OnLoadDemoRunnerRequested,
            onStartOriginRequested: ToolStrip_OnStartOriginRequested,
            onKeepLocalWorkRequested: SummaryHeader_OnKeepLocalWorkRequested,
            onWorkspaceSupportRequested: SummaryHeader_OnWorkspaceSupportRequested,
            onMenuSelected: MenuBar_OnMenuSelected,
            onRosterWorkspaceSelected: NavigatorPane_OnWorkspaceSelected,
            onWorkspaceSelected: NavigatorPane_OnWorkspaceSelected,
            onNavigationTabSelected: NavigatorPane_OnNavigationTabSelected,
            onSectionActionSelected: NavigatorPane_OnSectionActionSelected,
            onWorkflowSurfaceSelected: NavigatorPane_OnWorkflowSurfaceSelected,
            onSectionQuickActionRequested: SectionHost_OnQuickActionRequested,
            onSectionAttributeEditRequested: SectionHost_OnAttributeEditRequested,
            onCoachLaunchOpenRequested: CoachSidecar_OnOpenLaunchRequested,
            onCoachLaunchCopyRequested: CoachSidecar_OnCopyLaunchRequested,
            onCommandSelected: CommandDialogPane_OnCommandSelected,
            onDialogActionSelected: CommandDialogPane_OnDialogActionSelected,
            onDialogFieldValueChanged: CommandDialogPane_OnDialogFieldValueChanged,
            onMenuCommandSelected: MenuBar_OnMenuCommandSelected);
        _lifecycleCoordinator = new MainWindowLifecycleCoordinator(
            this,
            adapter,
            shellPresenter,
            RefreshState,
            OnOpened);
        _lifecycleCoordinator.Attach();
        _controls.ApplyDesktopModeChrome(ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic);

        RefreshState();
    }

    private Task TrackDesktopShellEventAsync(
        string eventName,
        string surface,
        IReadOnlyDictionary<string, string?>? properties = null,
        CancellationToken ct = default)
        => _desktopAnalyticsClient.TrackShellEventAsync(DesktopHeadId, eventName, surface, properties, ct);

    private static T ResolveService<T>()
        where T : notnull
    {
        IServiceProvider services = App.Services
            ?? throw new InvalidOperationException("Avalonia services are not initialized. Use DI startup to construct MainWindow.");
        return services.GetRequiredService<T>();
    }

    private void TryApplyWindowIcon()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Chummer.Avalonia/Assets/chummer.ico"));
            Icon = new WindowIcon(stream);
        }
        catch
        {
            string fallbackIconPath = Path.Combine(AppContext.BaseDirectory, "chummer.ico");
            if (File.Exists(fallbackIconPath))
            {
                try
                {
                    Icon = new WindowIcon(fallbackIconPath);
                }
                catch
                {
                }
            }

            // Keep startup resilient if the icon payload is unavailable in a local dev build.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifecycleCoordinator.Detach(_transientStateCoordinator.DetachDialogWindow());
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            Window_OnKeyDown(this, e);
        }
    }

    internal void ApplyInstallLinkingChrome(DesktopInstallLinkingState state)
    {
        _installLinkingState = state;
        string shellTitle = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.window_title",
            _persistedPreferences.Language);
        string claimTitle = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.install_link.title",
            _persistedPreferences.Language);
        Title = DesktopInstallLinkingRuntime.BuildShellWindowTitle(shellTitle, claimTitle, state);
    }

    internal DesktopDialogWindow? PeekDialogWindowForTesting()
        => _transientStateCoordinator.PeekDialogWindowForTesting();

    internal MainWindowControls ControlsForAutomation => _controls;

    internal CharacterOverviewState SnapshotStateForAutomation() => _adapter.State;

    internal Task SaveWorkspaceForAutomationAsync(CancellationToken ct)
        => _interactionCoordinator.SaveAsync(ct);

    internal byte[] CaptureScreenshotBytesForAutomation()
    {
        TopLevel? dialogWindow = PeekDialogWindowForTesting();
        PixelSize pixelSize = new(
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));

        for (int attempt = 0; attempt < 3; attempt++)
        {
            dialogWindow?.InvalidateMeasure();
            dialogWindow?.InvalidateArrange();
            dialogWindow?.InvalidateVisual();
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
            Measure(new Size(pixelSize.Width, pixelSize.Height));
            Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
            Dispatcher.UIThread.RunJobs();
        }

        using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
        bitmap.Render(this);
        if (dialogWindow is not null)
        {
            bitmap.Render(dialogWindow);
        }

        using MemoryStream output = new();
        bitmap.Save(output);
        return output.ToArray();
    }
}
