using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell : IDisposable
{
    private CharacterOverviewStateBridge? _bridge;
    private const long MaxImportBytes = 8 * 1024 * 1024;
    private ElementReference _shellRoot;

    [Inject]
    public ICharacterOverviewPresenter Presenter { get; set; } = default!;

    [Inject]
    public ICommandAvailabilityEvaluator AvailabilityEvaluator { get; set; } = default!;

    [Inject]
    public IShellPresenter ShellPresenter { get; set; } = default!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    public IShellSurfaceResolver ShellSurfaceResolver { get; set; } = default!;

    private string RawImportXml { get; set; } = "<character><name>Demo</name><alias>Sample</alias><metatype>Human</metatype><buildmethod>Priority</buildmethod><created>True</created></character>";
    private string? ImportedFileName { get; set; }
    private string? ImportError { get; set; }
    private string LoadWorkspaceId { get; set; } = string.Empty;
    private string MetadataName { get; set; } = string.Empty;
    private string MetadataAlias { get; set; } = string.Empty;
    private string MetadataNotes { get; set; } = string.Empty;
    private string _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
    private long _lastDownloadVersionHandled;
    private long _lastExportVersionHandled;
    private long _lastPrintVersionHandled;
    private bool _isDisposed;
    private ShellSurfaceState _shellSurfaceState = ShellSurfaceState.Empty;

    private CharacterOverviewState State => _bridge?.Current ?? Presenter.State;
    private ShellState ShellState => ShellPresenter.State;

    private IEnumerable<AppCommandDefinition> HeadCommands =>
        _shellSurfaceState.Commands.Where(command => !string.Equals(command.Group, "menu", StringComparison.Ordinal));

    private IEnumerable<AppCommandDefinition> ToolStripCommands =>
        HeadCommands.Where(command => command.Group is "file" or "tools").Take(10);

    private IReadOnlyList<AppCommandDefinition> MenuRoots =>
        _shellSurfaceState.MenuRoots;

    private IReadOnlyList<NavigationTabDefinition> NavigationTabs =>
        _shellSurfaceState.NavigationTabs;

    private IReadOnlyList<WorkspaceSurfaceActionDefinition> ActiveWorkspaceActions =>
        _shellSurfaceState.WorkspaceActions;

    private IReadOnlyList<WorkflowSurfaceActionBinding> ActiveWorkflowSurfaceActions =>
        _shellSurfaceState.ActiveWorkflowSurfaceActions;

    private string ComplianceState =>
        ShellStatusTextFormatter.BuildComplianceState(_shellSurfaceState, State.Preferences);

    protected override async Task OnInitializedAsync()
    {
        ShellPresenter.StateChanged += OnShellStateChanged;
        await ShellPresenter.InitializeAsync(CancellationToken.None);

        _bridge = new CharacterOverviewStateBridge(Presenter, state =>
        {
            if (_isDisposed)
                return;

            RefreshShellSurfaceState();
            _ = InvokeAsync(() => RefreshCoachSidecarIfNeededAsync());
            _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
            _ = InvokeAsync(StateHasChanged);
        });
        await _bridge.InitializeAsync(CancellationToken.None);
        if (ShouldSyncShellWorkspaceContext(State, ShellState))
        {
            await SyncShellWorkspaceContextAsync();
        }

        RefreshShellSurfaceState();
        await RefreshCoachSidecarIfNeededAsync(force: true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _shellRoot.FocusAsync();
        }

        await DispatchPendingDownloadAsync();
        await DispatchPendingExportAsync();
        await DispatchPendingPrintAsync();
    }

    private void OnShellStateChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
            return;

        RefreshShellSurfaceState();
        _ = InvokeAsync(() => RefreshCoachSidecarIfNeededAsync());
        _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _isDisposed = true;
        ShellPresenter.StateChanged -= OnShellStateChanged;
        _bridge?.Dispose();
    }

    private Task SyncShellWorkspaceContextAsync()
    {
        CharacterWorkspaceId? activeWorkspaceId = State.Session.ActiveWorkspaceId ?? State.WorkspaceId;
        return ShellPresenter.SyncWorkspaceContextAsync(activeWorkspaceId, CancellationToken.None);
    }

    private void RefreshShellSurfaceState()
    {
        _shellSurfaceState = ShellSurfaceResolver.Resolve(State, ShellState);
    }

    internal static bool ShouldSyncShellWorkspaceContext(CharacterOverviewState overviewState, ShellState shellState)
    {
        CharacterWorkspaceId? activeWorkspaceId = overviewState.Session.ActiveWorkspaceId ?? overviewState.WorkspaceId;
        if (!WorkspaceIdsEqual(activeWorkspaceId, shellState.ActiveWorkspaceId))
        {
            return true;
        }

        IReadOnlyList<OpenWorkspaceState> sessionWorkspaces = overviewState.Session.OpenWorkspaces;
        if (sessionWorkspaces.Count != shellState.OpenWorkspaces.Count)
        {
            return true;
        }

        HashSet<string> shellWorkspaceIds = shellState.OpenWorkspaces
            .Select(workspace => workspace.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        return sessionWorkspaces.Any(workspace => !shellWorkspaceIds.Contains(workspace.Id.Value));
    }

    private static bool WorkspaceIdsEqual(CharacterWorkspaceId? left, CharacterWorkspaceId? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;

        return string.Equals(left.Value.Value, right.Value.Value, StringComparison.Ordinal);
    }
}
