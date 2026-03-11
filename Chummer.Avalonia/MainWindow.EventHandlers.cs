using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Chummer.Contracts.Workspaces;
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
