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
        DesktopImportFileResult importFile = await MainWindowDesktopFileCoordinator.OpenImportFileAsync(
            StorageProvider,
            CancellationToken.None);
        if (importFile.Outcome == DesktopFileOperationOutcome.Unavailable)
        {
            MainWindowFeedbackCoordinator.ShowImportFileUnavailable(_controls.ToolStrip);
            return;
        }

        if (importFile.Outcome != DesktopFileOperationOutcome.Completed || importFile.Payload is null)
        {
            return;
        }

        await RunUiActionAsync(
            () => _adapter.ImportAsync(importFile.Payload, CancellationToken.None),
            "import character file");
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
            "copy character data");
    }

    private async void ToolStrip_OnDesktopHomeRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync("new_character", CancellationToken.None),
            "create new character");
    }

    private async void ToolStrip_OnCampaignWorkspaceRequested(object? sender, EventArgs e)
    {
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
        await RunUiActionAsync(
            async () =>
            {
                await _interactionCoordinator.ExecuteCommandAsync("global_settings", CancellationToken.None);
                MainWindowFeedbackCoordinator.ShowSettingsReviewed(_controls.ToolStrip);
            },
            "open global settings");
    }

    private async void ToolStrip_OnCloseWorkspaceRequested(object? sender, EventArgs e)
    {
        if (!_interactionCoordinator.TryGetActiveWorkspaceId(_adapter.State, out CharacterWorkspaceId activeWorkspaceId))
        {
            MainWindowFeedbackCoordinator.ShowNoActiveWorkspace(_controls.ToolStrip);
            return;
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.CloseWorkspaceAsync(activeWorkspaceId, CancellationToken.None),
            "close workspace");
    }

    private async void SummaryHeader_OnRuntimeInspectorRequested(object? sender, EventArgs e)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.OpenRuntimeInspectorAsync(CancellationToken.None),
            "open runtime inspector");
    }

    private void SummaryHeader_OnKeepLocalWorkRequested(object? sender, EventArgs e)
    {
        MainWindowFeedbackCoordinator.ShowLocalWorkspaceKept(_controls.ToolStrip);
    }

    private async void SummaryHeader_OnWorkspaceSupportRequested(object? sender, EventArgs e)
    {
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
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute hotkey command '{commandId}'");
    }
}
