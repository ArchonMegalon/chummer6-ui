using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell : IDisposable
{
    private const string SaveCharacterCommandId = "save_character";
    private const string PrintCharacterCommandId = "print_character";
    private const string CopyCommandId = "copy";
    private const string NewCharacterCommandId = "new_character";
    private const string OpenCharacterCommandId = "open_character";
    private const string CloseWindowCommandId = "close_window";
    private const string LegacySeededWorkspaceAlias = "ws-1";
    private const string LegacySeededWorkspaceAliasWarning =
        "The legacy sample workspace link 'ws-1' is stale. Use a seeded recovery route below to mint a fresh workspace link.";
    private const string LegacySeededWorkspaceRecoveryAppHref = "/app?fixture=blue&tab=tab-create";
    private const string LegacySeededWorkspaceRecoveryWorkbenchHref = "/workbench?fixture=blue&tab=tab-create";

    private static readonly string[] PreferredToolStripCommandOrder =
    [
        SaveCharacterCommandId,
        PrintCharacterCommandId,
        CopyCommandId,
        NewCharacterCommandId,
        OpenCharacterCommandId,
        CloseWindowCommandId
    ];

    private CharacterOverviewStateBridge? _bridge;
    private const long MaxImportBytes = 8 * 1024 * 1024;
    private ElementReference _shellRoot;
    private readonly CancellationTokenSource _componentLifetime = new();

    [Inject]
    public ICharacterOverviewPresenter Presenter { get; set; } = default!;

    [Inject]
    public ICommandAvailabilityEvaluator AvailabilityEvaluator { get; set; } = default!;

    [Inject]
    public IShellPresenter ShellPresenter { get; set; } = default!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IShellSurfaceResolver ShellSurfaceResolver { get; set; } = default!;

    [Inject]
    public IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public DesktopInstallLinkingStartupContext? InstallLinkingStartupContext { get; set; }

    [Parameter]
    public string? DemoFixtureId { get; set; }

    [Parameter]
    public string? DemoWorkspaceId { get; set; }

    [Parameter]
    public string? DemoTabId { get; set; }

    [Parameter]
    public string? DemoStartupCommandId { get; set; }

    [Parameter]
    public string? DemoUiControlId { get; set; }

    [Parameter]
    public string? DemoDialogActionId { get; set; }

    private DesktopInstallLinkingStartupContext? EffectiveInstallLinkingStartupContext =>
        InstallLinkingStartupContext
        ?? Services.GetService(typeof(DesktopInstallLinkingStartupContext)) as DesktopInstallLinkingStartupContext;

    private string RawImportXml { get; set; } = "<character><name>Demo</name><alias>Sample</alias><metatype>Human</metatype><buildmethod>Priority</buildmethod><created>True</created></character>";
    private string? ImportedFileName { get; set; }
    private string? ImportError { get; set; }
    private string LoadWorkspaceId { get; set; } = string.Empty;
    private string MetadataName { get; set; } = string.Empty;
    private string MetadataAlias { get; set; } = string.Empty;
    private string MetadataNotes { get; set; } = string.Empty;
    private string _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
    private long _lastDownloadVersionHandled;
    private long _lastRecoveryExportVersionHandled;
    private long _lastExportVersionHandled;
    private long _lastPrintVersionHandled;
    private bool _isDisposed;
    private bool _demoBootstrapCompleted;
    private string? _lastDemoBootstrapKey;
    private string? _bootstrappedDemoFixtureId;
    private string? _demoWorkspaceRouteWarning;
    private bool _originWizardAdvancedControlsOpen;
    private ShellSurfaceState _shellSurfaceState = ShellSurfaceState.Empty;

    private CharacterOverviewState State => _bridge?.Current ?? Presenter.State;
    private ShellState ShellState => ShellPresenter.State;
    private string CurrentLanguage => DesktopLocalizationCatalog.NormalizeOrDefault(State.Preferences.Language);
    private bool ShowInstallClaimGate =>
        EffectiveInstallLinkingStartupContext is not null
        && EffectiveInstallLinkingStartupContext.ShouldPrompt
        && !DesktopInstallLinkingRuntime.IsClaimed(EffectiveInstallLinkingStartupContext.State);
    private DesktopInstallLinkingState InstallLinkingState =>
        EffectiveInstallLinkingStartupContext?.State
        ?? DesktopInstallLinkingRuntime.LoadOrCreateState("blazor-desktop");

    private IEnumerable<AppCommandDefinition> HeadCommands =>
        _shellSurfaceState.Commands.Where(command => !string.Equals(command.Group, "menu", StringComparison.Ordinal));

    private IEnumerable<AppCommandDefinition> ToolStripCommands =>
        ResolveToolStripCommands();

    private bool ShowLeftPane =>
        _shellSurfaceState.ActiveWorkspaceId is not null
        && _shellSurfaceState.OpenWorkspaces.Count > 1;

    private bool UseResponsiveBuildWorkspace =>
        (_shellSurfaceState.ActiveWorkspaceId is not null || State.WorkspaceId is not null)
        && NavigationTabs.Count > 0;

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

    private string DemoWorkspaceRecoveryAppHref => LegacySeededWorkspaceRecoveryAppHref;

    private string DemoWorkspaceRecoveryWorkbenchHref => LegacySeededWorkspaceRecoveryWorkbenchHref;

    private string? OriginDossierNoticeHref =>
        TryGetOriginDossierNoticeHref(State.Notice, out string? href) ? href : null;

    private WorkspaceDownloadReceipt? DownloadNoticeReceipt =>
        HasNoticePrefix(State.Notice, "Download prepared:") ? State.PendingDownload : null;

    private WorkspaceExportReceipt? ExportNoticeReceipt =>
        HasNoticePrefix(State.Notice, "Portable export ready:")
        || HasNoticePrefix(State.Notice, "Export prepared:")
            ? State.PendingExport
            : null;

    private WorkspacePrintReceipt? PrintNoticeReceipt =>
        HasNoticePrefix(State.Notice, "Print preview prepared:") ? State.PendingPrint : null;

    private string ClassicShellActiveTabId =>
        _shellSurfaceState.ActiveTabId
        ?? State.ActiveTabId
        ?? string.Empty;

    private string ClassicShellRulesetId =>
        RulesetDefaults.NormalizeOptional(_shellSurfaceState.ActiveRulesetId)
        ?? RulesetDefaults.NormalizeOptional(State.Rules?.GameEdition)
        ?? RulesetDefaults.NormalizeOptional(ResolveClassicShellActiveWorkspace()?.RulesetId)
        ?? string.Empty;

    private string ClassicShellActiveWorkflowId
    {
        get
        {
            if (string.Equals(ClassicShellActiveTabId, "tab-create", StringComparison.Ordinal))
            {
                return "build-lab";
            }

            string? activeActionId = State.ActiveActionId;
            WorkflowSurfaceActionBinding? activeWorkflow = _shellSurfaceState.ActiveWorkflowSurfaceActions.FirstOrDefault(action =>
                !string.IsNullOrWhiteSpace(activeActionId)
                && string.Equals(action.ActionId, activeActionId, StringComparison.Ordinal));
            return activeWorkflow?.WorkflowId ?? string.Empty;
        }
    }

    private string ClassicShellRouteSegment
    {
        get
        {
            string route = Navigation.ToBaseRelativePath(Navigation.Uri);
            int queryOrFragmentIndex = route.IndexOfAny(['?', '#']);
            if (queryOrFragmentIndex >= 0)
            {
                route = route[..queryOrFragmentIndex];
            }

            route = route.Trim('/');
            if (route.StartsWith("blazor/", StringComparison.OrdinalIgnoreCase))
            {
                route = route["blazor/".Length..];
            }

            if (string.IsNullOrWhiteSpace(route))
            {
                return "home";
            }

            int nestedSegmentIndex = route.IndexOf('/');
            string segment = nestedSegmentIndex >= 0 ? route[..nestedSegmentIndex] : route;
            return segment.ToLowerInvariant();
        }
    }

    private string ClassicShellActiveRunner
    {
        get
        {
            OpenWorkspaceState? activeWorkspace = ResolveClassicShellActiveWorkspace();
            return State.Profile?.Alias
                ?? activeWorkspace?.Alias
                ?? State.Profile?.Name
                ?? activeWorkspace?.Name
                ?? string.Empty;
        }
    }

    private OpenWorkspaceState? ResolveClassicShellActiveWorkspace()
    {
        CharacterWorkspaceId? activeWorkspaceId = _shellSurfaceState.ActiveWorkspaceId
            ?? State.Session.ActiveWorkspaceId
            ?? State.WorkspaceId;
        if (activeWorkspaceId is null)
        {
            return null;
        }

        return _shellSurfaceState.OpenWorkspaces
                   .FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId))
               ?? State.OpenWorkspaces
                   .FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId))
               ?? State.Session.OpenWorkspaces
                   .FirstOrDefault(workspace => WorkspaceIdsEqual(workspace.Id, activeWorkspaceId));
    }

    protected override async Task OnInitializedAsync()
    {
        ShellPresenter.StateChanged += OnShellStateChanged;
        await ShellPresenter.InitializeAsync(_componentLifetime.Token);

        _bridge = new CharacterOverviewStateBridge(Presenter, state =>
        {
            if (_isDisposed)
                return;

            SyncOriginWizardDialogUiState();
            RefreshShellSurfaceState();
            _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
            _ = InvokeAsync(StateHasChanged);
        });
        await _bridge.InitializeAsync(_componentLifetime.Token);
        SyncOriginWizardDialogUiState(clearWhenDialogClosed: true);
        if (ShouldSyncShellWorkspaceContext(State, ShellState))
        {
            await SyncShellWorkspaceContextAsync();
        }

        RefreshShellSurfaceState();
        await TryBootstrapDemoWorkspaceAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await DispatchPendingRecoveryExportAsync();
        await DispatchPendingDownloadAsync();
        await DispatchPendingExportAsync();
        await DispatchPendingPrintAsync();
    }

    private void SyncOriginWizardDialogUiState(bool clearWhenDialogClosed = false)
    {
        DesktopDialogState? activeDialog = State.ActiveDialog;
        if (activeDialog is null)
        {
            if (clearWhenDialogClosed)
            {
                _originWizardAdvancedControlsOpen = false;
            }

            return;
        }

        if (!string.Equals(activeDialog.Id, "dialog.new_character.origin_wizard", StringComparison.Ordinal))
        {
            _originWizardAdvancedControlsOpen = false;
        }
    }

    private void OnShellStateChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
            return;

        RefreshShellSurfaceState();
        _lastUiUtc = DateTimeOffset.UtcNow.ToString("u");
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _componentLifetime.Cancel();
        ShellPresenter.StateChanged -= OnShellStateChanged;
        _bridge?.Dispose();
        _componentLifetime.Dispose();
    }

    private Task SyncShellWorkspaceContextAsync()
    {
        CharacterWorkspaceId? activeWorkspaceId = State.Session.ActiveWorkspaceId ?? State.WorkspaceId;
        return ShellPresenter.SyncWorkspaceContextAsync(activeWorkspaceId, _componentLifetime.Token);
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

    private IEnumerable<AppCommandDefinition> ResolveToolStripCommands()
    {
        Dictionary<string, AppCommandDefinition> commandsById = HeadCommands
            .ToDictionary(command => command.Id, StringComparer.Ordinal);

        foreach (string commandId in PreferredToolStripCommandOrder)
        {
            if (commandsById.TryGetValue(commandId, out AppCommandDefinition? command))
            {
                yield return command;
            }
        }
    }

    private string BuildInstallClaimHref()
        => DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri(
            DesktopInstallLinkingRuntime.BuildClaimPortalRelativePathForInstall(InstallLinkingState));

    private string BuildInstallSupportHref()
        => DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri(
            DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForInstall(InstallLinkingState));

    private string BuildInstallDownloadsHref()
        => DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/downloads");

    private string BuildInstallOriginDossierHref()
        => DesktopInstallLinkingRuntime.BuildOriginDossierPortalAbsoluteUri();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await TryBootstrapDemoWorkspaceAsync();
    }

    private async Task TryBootstrapDemoWorkspaceAsync()
    {
        if (_bridge is null)
            return;

        string? fixtureId = NormalizeDemoFixtureId(DemoFixtureId);
        string? workspaceId = NormalizeOptionalToken(DemoWorkspaceId);
        string? tabId = NormalizeOptionalToken(DemoTabId);
        string? commandId = NormalizeDemoStartupCommandId(DemoStartupCommandId);
        string? controlId = NormalizeOptionalToken(DemoUiControlId);
        string? dialogActionId = NormalizeOptionalToken(DemoDialogActionId);
        string? demoKey =
            fixtureId is null && workspaceId is null && tabId is null && commandId is null && controlId is null && dialogActionId is null
                ? null
                : $"{fixtureId ?? string.Empty}|{workspaceId ?? string.Empty}|{tabId ?? string.Empty}|{commandId ?? string.Empty}|{controlId ?? string.Empty}|{dialogActionId ?? string.Empty}";
        if (demoKey is null)
            return;

        if (_demoBootstrapCompleted && string.Equals(_lastDemoBootstrapKey, demoKey, StringComparison.Ordinal))
        {
            return;
        }

        _demoWorkspaceRouteWarning = null;

        if (workspaceId is not null
            && fixtureId is null
            && IsLegacySeededWorkspaceAlias(workspaceId))
        {
            _demoWorkspaceRouteWarning = LegacySeededWorkspaceAliasWarning;
            _demoBootstrapCompleted = true;
            _lastDemoBootstrapKey = demoKey;
            RefreshShellSurfaceState();
            return;
        }

        if (workspaceId is not null
            && fixtureId is null
            && (State.WorkspaceId is null || !string.Equals(State.WorkspaceId.Value.Value, workspaceId, StringComparison.Ordinal)))
        {
            await LoadWorkspaceAsync(workspaceId);
        }
        else if (fixtureId is not null
            && (!string.Equals(_bootstrappedDemoFixtureId, fixtureId, StringComparison.Ordinal)
                || State.WorkspaceId is null
                || State.Session.OpenWorkspaces.Count == 0))
        {
            string fixturePath = ResolveDemoFixturePath(fixtureId!);
            string xml = await File.ReadAllTextAsync(fixturePath, Encoding.UTF8);
            await Presenter.ImportAsync(
                new WorkspaceImportDocument(xml, RulesetDefaults.Sr5, WorkspaceDocumentFormat.NativeXml),
                CancellationToken.None);
            await SyncShellWorkspaceContextAsync();
            SyncMetadataDraftFromState();
            _bootstrappedDemoFixtureId = fixtureId;
            RewriteFixtureRouteToWorkspaceRoute();
        }

        if (tabId is not null && State.WorkspaceId is not null && !string.Equals(State.ActiveTabId, tabId, StringComparison.Ordinal))
        {
            await _bridge.SelectTabAsync(tabId, CancellationToken.None);
            await SyncShellWorkspaceContextAsync();
        }

        if (controlId is not null)
        {
            await HandleUiControlAsync(controlId);
        }

        if (commandId is not null)
        {
            await ExecuteCommandAsync(commandId);
        }

        if (dialogActionId is not null)
        {
            await ExecuteDialogActionAsync(dialogActionId);
        }

        _demoBootstrapCompleted = true;
        _lastDemoBootstrapKey = demoKey;
        RefreshShellSurfaceState();
    }

    private static string? NormalizeDemoFixtureId(string? fixtureId)
    {
        string? normalized = NormalizeOptionalToken(fixtureId);
        if (normalized is null)
            return null;

        return string.Equals(normalized, "blue", StringComparison.OrdinalIgnoreCase)
            ? "BLUE"
            : null;
    }

    private static string ResolveDemoFixturePath(string fixtureId)
    {
        string fileName = fixtureId switch
        {
            "BLUE" => "BLUE.chum5",
            _ => throw new InvalidOperationException($"Unknown browser demo fixture '{fixtureId}'.")
        };

        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", fileName)
        ];

        foreach (string fixturePath in candidatePaths)
        {
            if (File.Exists(fixturePath))
                return fixturePath;
        }

        string fixturePathHint = string.Join(", ", candidatePaths);
        throw new FileNotFoundException(
            $"Browser demo fixture '{fileName}' was not found in any known output location.",
            fixturePathHint);
    }

    private static string? NormalizeOptionalToken(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool IsLegacySeededWorkspaceAlias(string workspaceId)
        => string.Equals(workspaceId, LegacySeededWorkspaceAlias, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetOriginDossierNoticeHref(string? notice, out string? href)
    {
        const string prefix = "Origin Dossier link:";
        if (!string.IsNullOrWhiteSpace(notice)
            && notice.StartsWith(prefix, StringComparison.Ordinal))
        {
            string candidate = notice[prefix.Length..].Trim();
            if (candidate.StartsWith("/app?command=new_character_origin", StringComparison.Ordinal))
            {
                href = candidate;
                return true;
            }
        }

        href = null;
        return false;
    }

    private static bool HasNoticePrefix(string? notice, string prefix)
        => !string.IsNullOrWhiteSpace(notice)
            && notice.StartsWith(prefix, StringComparison.Ordinal);

    private static string? NormalizeDemoStartupCommandId(string? commandId)
    {
        string? normalized = NormalizeOptionalToken(commandId);
        return normalized is not null && OverviewCommandPolicy.IsKnownSharedCommand(normalized)
            ? normalized
            : null;
    }

    private void RewriteFixtureRouteToWorkspaceRoute()
    {
        if (State.WorkspaceId is not { } workspaceId)
            return;

        List<string> query = [$"workspace={Uri.EscapeDataString(workspaceId.Value)}"];

        string? tabId = NormalizeOptionalToken(DemoTabId);
        string? commandId = NormalizeDemoStartupCommandId(DemoStartupCommandId);
        string? controlId = NormalizeOptionalToken(DemoUiControlId);
        string? dialogActionId = NormalizeOptionalToken(DemoDialogActionId);

        if (tabId is not null)
        {
            query.Add($"tab={Uri.EscapeDataString(tabId)}");
        }

        if (commandId is not null)
        {
            query.Add($"command={Uri.EscapeDataString(commandId)}");
        }

        if (controlId is not null)
        {
            query.Add($"control={Uri.EscapeDataString(controlId)}");
        }

        if (dialogActionId is not null)
        {
            query.Add($"dialog_action={Uri.EscapeDataString(dialogActionId)}");
        }

        string currentRoute = Navigation.ToBaseRelativePath(Navigation.Uri);
        int queryOrFragmentIndex = currentRoute.IndexOfAny(['?', '#']);
        if (queryOrFragmentIndex >= 0)
        {
            currentRoute = currentRoute[..queryOrFragmentIndex];
        }

        string target = $"{currentRoute}?{string.Join("&", query)}";
        if (string.Equals(Navigation.ToBaseRelativePath(Navigation.Uri), target, StringComparison.Ordinal))
            return;

        Navigation.NavigateTo(target, replace: true);
    }
}
