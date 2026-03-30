using Avalonia.Controls;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Presentation.UiKit;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

public partial class MainWindow : Window
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;
    private readonly IShellPresenter _shellPresenter;
    private readonly ICommandAvailabilityEvaluator _commandAvailabilityEvaluator;
    private readonly IShellSurfaceResolver _shellSurfaceResolver;
    private readonly IAvaloniaCoachSidecarClient _coachSidecarClient;
    private readonly CharacterOverviewViewModelAdapter _adapter;
    private readonly MainWindowActionExecutionCoordinator _actionExecutionCoordinator;
    private readonly MainWindowInteractionCoordinator _interactionCoordinator;
    private readonly MainWindowLifecycleCoordinator _lifecycleCoordinator;
    private readonly MainWindowTransientStateCoordinator _transientStateCoordinator;
    private readonly MainWindowControls _controls;

    public MainWindow()
        : this(
            ResolveService<ICharacterOverviewPresenter>(),
            ResolveService<IShellPresenter>(),
            ResolveService<ICommandAvailabilityEvaluator>(),
            ResolveService<IShellSurfaceResolver>(),
            ResolveService<IAvaloniaCoachSidecarClient>(),
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
    {
        InitializeComponent();
        Title = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.window_title",
            DesktopLocalizationCatalog.GetCurrentLanguage());

        _shellPresenter = shellPresenter;
        _commandAvailabilityEvaluator = commandAvailabilityEvaluator;
        _shellSurfaceResolver = shellSurfaceResolver;
        _coachSidecarClient = coachSidecarClient;
        _adapter = adapter;
        _actionExecutionCoordinator = new MainWindowActionExecutionCoordinator(
            adapter,
            shellPresenter,
            ApplyUiActionFailure);
        _interactionCoordinator = new MainWindowInteractionCoordinator(presenter, shellPresenter, adapter);
        _transientStateCoordinator = new MainWindowTransientStateCoordinator();

        _controls = MainWindowControlBinder.Bind(
            toolStrip: ToolStripControl,
            workspaceStrip: WorkspaceStripControl,
            summaryHeader: SummaryHeaderControl,
            menuBar: ShellMenuBarControl,
            navigatorPane: NavigatorPaneControl,
            sectionHost: SectionHostControl,
            commandDialogPane: CommandDialogPaneControl,
            coachSidecar: CoachSidecarControl,
            statusStrip: StatusStripControl,
            onImportFileRequested: ToolStrip_OnImportFileRequested,
            onImportRawRequested: ToolStrip_OnImportRawRequested,
            onSaveRequested: ToolStrip_OnSaveRequested,
            onCloseWorkspaceRequested: ToolStrip_OnCloseWorkspaceRequested,
            onDesktopHomeRequested: ToolStrip_OnDesktopHomeRequested,
            onInstallLinkingRequested: ToolStrip_OnInstallLinkingRequested,
            onSupportRequested: ToolStrip_OnSupportRequested,
            onRuntimeInspectorRequested: SummaryHeader_OnRuntimeInspectorRequested,
            onMenuSelected: MenuBar_OnMenuSelected,
            onWorkspaceSelected: NavigatorPane_OnWorkspaceSelected,
            onNavigationTabSelected: NavigatorPane_OnNavigationTabSelected,
            onSectionActionSelected: NavigatorPane_OnSectionActionSelected,
            onWorkflowSurfaceSelected: NavigatorPane_OnWorkflowSurfaceSelected,
            onCoachLaunchCopyRequested: CoachSidecar_OnCopyLaunchRequested,
            onCommandSelected: CommandDialogPane_OnCommandSelected,
            onDialogActionSelected: CommandDialogPane_OnDialogActionSelected);
        _lifecycleCoordinator = new MainWindowLifecycleCoordinator(
            this,
            adapter,
            shellPresenter,
            RefreshState,
            OnOpened);
        _lifecycleCoordinator.Attach();

        RefreshState();
    }

    private static T ResolveService<T>()
        where T : notnull
    {
        IServiceProvider services = App.Services
            ?? throw new InvalidOperationException("Avalonia services are not initialized. Use DI startup to construct MainWindow.");
        return services.GetRequiredService<T>();
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifecycleCoordinator.Detach(_transientStateCoordinator.DetachDialogWindow());
        base.OnClosed(e);
    }
}
