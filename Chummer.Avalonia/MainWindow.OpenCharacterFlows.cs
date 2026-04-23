using Chummer.Contracts.Workspaces;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private async Task OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode openMode)
    {
        string pickerTitle = GetOpenCharacterFilePickerTitle(openMode);
        DesktopImportFileResult importFile = await MainWindowDesktopFileCoordinator.OpenImportFileAsync(
            StorageProvider,
            pickerTitle,
            CancellationToken.None);
        if (importFile.Outcome == DesktopFileOperationOutcome.Unavailable)
        {
            MainWindowFeedbackCoordinator.ShowImportFileUnavailable(_controls.ToolStrip);
            return;
        }

        if (importFile.Outcome == DesktopFileOperationOutcome.Cancelled)
        {
            MainWindowFeedbackCoordinator.ShowImportFileCancelled(_controls.ToolStrip, pickerTitle);
            return;
        }

        if (importFile.Outcome != DesktopFileOperationOutcome.Completed || importFile.Payload is null)
        {
            return;
        }

        CharacterWorkspaceId? previousWorkspaceId = _adapter.State.Session.ActiveWorkspaceId ?? _adapter.State.WorkspaceId;
        await RunUiActionAsync(
            async () =>
            {
                await _interactionCoordinator.ImportAsync(importFile.Payload, CancellationToken.None);

                if (!TryResolveImportedWorkspaceId(previousWorkspaceId, out _))
                {
                    return;
                }

                switch (openMode)
                {
                    case DesktopOpenCharacterMode.OpenOnly:
                        break;
                    case DesktopOpenCharacterMode.PrintAfterImport:
                        await _interactionCoordinator.PrintAsync(CancellationToken.None);
                        break;
                    case DesktopOpenCharacterMode.ExportAfterImport:
                        await _interactionCoordinator.ExportAsync(CancellationToken.None);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(openMode), openMode, null);
                }
            },
            openMode switch
            {
                DesktopOpenCharacterMode.OpenOnly => "open character file",
                DesktopOpenCharacterMode.PrintAfterImport => "open character for printing",
                DesktopOpenCharacterMode.ExportAfterImport => "open character for export",
                _ => "open character file"
            });
    }

    private static string GetOpenCharacterFilePickerTitle(DesktopOpenCharacterMode openMode)
        => openMode switch
        {
            DesktopOpenCharacterMode.OpenOnly => "Open Character File",
            DesktopOpenCharacterMode.PrintAfterImport => "Open Character for Printing",
            DesktopOpenCharacterMode.ExportAfterImport => "Open Character for Export",
            _ => "Open Character File"
        };

    private bool TryResolveImportedWorkspaceId(
        CharacterWorkspaceId? previousWorkspaceId,
        out CharacterWorkspaceId importedWorkspaceId)
    {
        CharacterWorkspaceId? currentWorkspaceId = _adapter.State.Session.ActiveWorkspaceId ?? _adapter.State.WorkspaceId;
        if (currentWorkspaceId is null)
        {
            importedWorkspaceId = default;
            return false;
        }

        if (previousWorkspaceId is not null && currentWorkspaceId.Value.Equals(previousWorkspaceId.Value))
        {
            importedWorkspaceId = default;
            return false;
        }

        importedWorkspaceId = currentWorkspaceId.Value;
        return true;
    }
}

internal enum DesktopOpenCharacterMode
{
    OpenOnly,
    PrintAfterImport,
    ExportAfterImport
}
