using Avalonia.Controls;
using Chummer.Contracts.Presentation;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Presentation.UiKit;
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
    private readonly CharacterOverviewViewModelAdapter _adapter;
    private readonly MainWindowActionExecutionCoordinator _actionExecutionCoordinator;
    private readonly MainWindowInteractionCoordinator _interactionCoordinator;
    private readonly MainWindowLifecycleCoordinator _lifecycleCoordinator;
    private readonly MainWindowTransientStateCoordinator _transientStateCoordinator;
    private readonly MainWindowControls _controls;
    private DesktopPreferenceState _persistedPreferences;

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
        _persistedPreferences = DesktopPreferenceRuntime.LoadOrCreateState(DesktopHeadId);
        DesktopPreferenceStateRuntime.SetCurrent(_persistedPreferences);
        DesktopLocalizationCatalog.SetCurrentLanguageOverride(_persistedPreferences.Language);
        InitializeComponent();
        Title = DesktopLocalizationCatalog.GetRequiredString(
            "desktop.shell.window_title",
            _persistedPreferences.Language);

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
            onCampaignWorkspaceRequested: ToolStrip_OnCampaignWorkspaceRequested,
            onUpdateStatusRequested: ToolStrip_OnUpdateStatusRequested,
            onInstallLinkingRequested: ToolStrip_OnInstallLinkingRequested,
            onSupportRequested: ToolStrip_OnSupportRequested,
            onReportIssueRequested: ToolStrip_OnReportIssueRequested,
            onSettingsRequested: ToolStrip_OnSettingsRequested,
            onLoadDemoRunnerRequested: ToolStrip_OnLoadDemoRunnerRequested,
            onRuntimeInspectorRequested: SummaryHeader_OnRuntimeInspectorRequested,
            onMenuSelected: MenuBar_OnMenuSelected,
            onWorkspaceSelected: NavigatorPane_OnWorkspaceSelected,
            onNavigationTabSelected: NavigatorPane_OnNavigationTabSelected,
            onSectionActionSelected: NavigatorPane_OnSectionActionSelected,
            onWorkflowSurfaceSelected: NavigatorPane_OnWorkflowSurfaceSelected,
            onSectionQuickActionRequested: SectionHost_OnQuickActionRequested,
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
