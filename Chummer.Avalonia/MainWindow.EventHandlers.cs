using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private async void ToolStrip_OnImportRawRequested(object? sender, EventArgs e)
    {
        if (ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic)
        {
            MainWindowFeedbackCoordinator.ShowImportRawRequired(_controls.ToolStrip);
            return;
        }

        string importText = _controls.SectionHostInputText;
        if (string.IsNullOrWhiteSpace(importText))
        {
            MainWindowFeedbackCoordinator.ShowImportRawRequired(_controls.ToolStrip);
            return;
        }

        await RunUiActionAsync(
            () => _adapter.ImportAsync(Encoding.UTF8.GetBytes(importText), CancellationToken.None),
            "import debug XML");
    }

    private async void ToolStrip_OnImportFileRequested(object? sender, EventArgs e)
    {
        await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.OpenOnly);
    }

    private async void ToolStrip_OnLoadDemoRunnerRequested(object? sender, EventArgs e)
    {
        DesktopImportFileResult importFile = await MainWindowDesktopFileCoordinator.OpenBundledDemoRunnerAsync(CancellationToken.None);
        if (importFile.Outcome == DesktopFileOperationOutcome.Unavailable || importFile.Payload is null)
        {
            MainWindowFeedbackCoordinator.ShowBundledDemoRunnerUnavailable(_controls.ToolStrip);
            return;
        }

        MainWindowFeedbackCoordinator.ShowBundledDemoRunnerLoading(_controls.ToolStrip, importFile.SourceLabel);
        await RunUiActionAsync(
            () => _adapter.ImportAsync(importFile.Payload, CancellationToken.None),
            "load bundled demo runner");
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await _shellPresenter.InitializeAsync(CancellationToken.None);
                await _adapter.InitializeAsync(CancellationToken.None);
            },
            "initialize desktop shell");
    }

    private async void ToolStrip_OnSaveRequested(object? sender, EventArgs e)
    {
        if (sender is Controls.SummaryHeaderControl)
        {
            _transientStateCoordinator.RecordSaveLocalWorkDecision(ResolveActiveWorkspaceId());
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.SaveAsync(CancellationToken.None),
            "save workspace");
    }

    private async void ToolStrip_OnPrintRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync("print_character", CancellationToken.None),
            "print character");
    }

    private async void ToolStrip_OnCopyRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync("copy", CancellationToken.None),
            "copy workspace selection");
    }

    private async void ToolStrip_OnOpenForPrintingRequested(object? sender, EventArgs e)
    {
        await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.PrintAfterImport);
    }

    private async void ToolStrip_OnOpenForExportRequested(object? sender, EventArgs e)
    {
        await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.ExportAfterImport);
    }

    private async void ToolStrip_OnDesktopHomeRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopHomeWindow.ShowAsync(this, "avalonia");
                MainWindowFeedbackCoordinator.ShowDesktopHomeReviewed(_controls.ToolStrip);
            },
            "open desktop home");
    }

    private async void ToolStrip_OnCloseWorkspaceRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync("close_window", CancellationToken.None),
            "close workspace");
    }

    private async void ToolStrip_OnGmPrepRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, DesktopHeadId);
                MainWindowFeedbackCoordinator.ShowCampaignWorkspaceReviewed(_controls.ToolStrip);
            },
            "open GM prep packets");
    }

    private async void ToolStrip_OnRosterMovementRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, DesktopHeadId);
                MainWindowFeedbackCoordinator.ShowCampaignWorkspaceReviewed(_controls.ToolStrip);
            },
            "open roster movement");
    }

    private async void ToolStrip_OnRuleEnvironmentStudioRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopRuleEnvironmentStudioWindow.ShowAsync(this, DesktopHeadId, _adapter.State.LatestPortabilityActivity);
                MainWindowFeedbackCoordinator.ShowRuleEnvironmentStudioReviewed(_controls.ToolStrip);
            },
            "open rule environment studio");
    }

    private async void ToolStrip_OnCampaignWorkspaceRequested(object? sender, EventArgs e)
    {
        if (sender is Controls.SummaryHeaderControl)
        {
            _transientStateCoordinator.RecordCampaignWorkspaceDecision(ResolveActiveWorkspaceId());
        }

        await RunUiActionAsync(
            async () =>
            {
                await DesktopCampaignWorkspaceWindow.ShowAsync(this, "avalonia");
                MainWindowFeedbackCoordinator.ShowCampaignWorkspaceReviewed(_controls.ToolStrip);
            },
            "open campaign workspace");
    }

    private async void ToolStrip_OnUpdateStatusRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopUpdateWindow.ShowAsync(this, "avalonia");
                MainWindowFeedbackCoordinator.ShowUpdateReviewed(_controls.ToolStrip);
            },
            "open update status");
    }

    private async void ToolStrip_OnInstallLinkingRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopInstallLinkingWindow.ShowAsync(this, "avalonia");
                ApplyInstallLinkingChrome(DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia"));
                MainWindowFeedbackCoordinator.ShowInstallLinkingReviewed(_controls.ToolStrip);
            },
            "open install linking");
    }

    private async void ToolStrip_OnSupportRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopSupportWindow.ShowAsync(this, "avalonia");
                MainWindowFeedbackCoordinator.ShowSupportReviewed(_controls.ToolStrip);
            },
            "open support");
    }

    private async void ToolStrip_OnReportIssueRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            async () =>
            {
                await DesktopReportIssueWindow.ShowAsync(this, "avalonia");
                MainWindowFeedbackCoordinator.ShowReportIssueReviewed(_controls.ToolStrip);
            },
            "open report issue");
    }

    private async void ToolStrip_OnSettingsRequested(object? sender, EventArgs e)
    {
        await OpenDesktopCommandFromSurfaceAsync("global_settings", "open global settings");
        MainWindowFeedbackCoordinator.ShowSettingsReviewed(_controls.ToolStrip);
    }

    private async void SummaryHeader_OnRuntimeInspectorRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.OpenRuntimeInspectorAsync(CancellationToken.None),
            "open runtime inspector");
    }

    private void SummaryHeader_OnKeepLocalWorkRequested(object? sender, EventArgs e)
    {
        _transientStateCoordinator.RecordKeepLocalWorkDecision(ResolveActiveWorkspaceId());
        MainWindowFeedbackCoordinator.ShowLocalWorkspaceKept(_controls.ToolStrip);
    }

    private async void SummaryHeader_OnWorkspaceSupportRequested(object? sender, EventArgs e)
    {
        _transientStateCoordinator.RecordWorkspaceSupportDecision(ResolveActiveWorkspaceId());
        await RunUiActionAsync(
            async () =>
            {
                DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(DesktopHeadId);
                if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(installState, ResolveActiveSupportWorkspace()))
                {
                    MainWindowFeedbackCoordinator.ShowSupportReviewed(_controls.ToolStrip);
                    return;
                }

                await DesktopSupportWindow.ShowAsync(this, DesktopHeadId);
                MainWindowFeedbackCoordinator.ShowSupportReviewed(_controls.ToolStrip);
            },
            "open workspace support");
    }

    private string? ResolveActiveWorkspaceId()
        => _adapter.State.Session.ActiveWorkspaceId?.Value ?? _adapter.State.WorkspaceId?.Value;

    private WorkspaceListItem? ResolveActiveSupportWorkspace()
    {
        CharacterWorkspaceId? activeWorkspaceId = _adapter.State.Session.ActiveWorkspaceId ?? _adapter.State.WorkspaceId;
        OpenWorkspaceState? activeWorkspace = _adapter.State.Session.OpenWorkspaces
            .Concat(_adapter.State.OpenWorkspaces)
            .FirstOrDefault(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId?.Value, StringComparison.Ordinal));
        activeWorkspace ??= _adapter.State.Session.OpenWorkspaces
            .Concat(_adapter.State.OpenWorkspaces)
            .OrderByDescending(workspace => workspace.LastOpenedUtc)
            .FirstOrDefault();

        if (activeWorkspace is null)
        {
            return null;
        }

        CharacterFileSummary summary = new(
            Name: string.IsNullOrWhiteSpace(activeWorkspace.Name) ? activeWorkspace.Id.Value : activeWorkspace.Name,
            Alias: activeWorkspace.Alias,
            Metatype: string.Empty,
            BuildMethod: string.Empty,
            CreatedVersion: activeWorkspace.RulesetId,
            AppVersion: string.Empty,
            Karma: 0,
            Nuyen: 0,
            Created: true);
        return new WorkspaceListItem(
            Id: activeWorkspace.Id,
            Summary: summary,
            LastUpdatedUtc: activeWorkspace.LastOpenedUtc,
            RulesetId: activeWorkspace.RulesetId,
            HasSavedWorkspace: activeWorkspace.HasSavedWorkspace);
    }

    private async void MenuBar_OnMenuSelected(object? sender, string menuId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ToggleMenuAsync(menuId, CancellationToken.None),
            $"toggle menu '{menuId}'");
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool commandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shiftModifier = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool altModifier = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (!DesktopShortcutCatalog.TryResolveCommandId(
                e.Key.ToString(),
                commandModifier,
                shiftModifier,
                altModifier,
                out string commandId))
        {
            return;
        }

        e.Handled = true;
        if (await TryHandleMenuHostCommandAsync(commandId))
        {
            return;
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute hotkey command '{commandId}'");
    }
}
