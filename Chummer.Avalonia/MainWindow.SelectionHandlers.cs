using System.IO;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    internal static Func<MainWindow>? NewWindowFactoryForTesting { get; set; }
    internal static Action<MainWindow>? NewWindowCreatedForTesting { get; set; }

    private const string LegacyWikiUrl = "https://github.com/chummer5a/chummer5a/wiki/";
    private const string LegacyDiscordUrl = "https://discord.gg/mJB7st9";
    private const string LegacyIssueTrackerUrl = "https://github.com/chummer5a/chummer5a/issues/";

    private async void CommandDialogPane_OnCommandSelected(object? sender, string commandId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute command '{commandId}'");
    }

    private async void NavigatorPane_OnWorkspaceSelected(object? sender, string workspaceId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.SwitchWorkspaceAsync(workspaceId, CancellationToken.None),
            $"switch workspace '{workspaceId}'");
    }

    private async void NavigatorPane_OnNavigationTabSelected(object? sender, string tabId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.SelectTabAsync(tabId, CancellationToken.None),
            $"select tab '{tabId}'");
    }

    private async void NavigatorPane_OnSectionActionSelected(object? sender, string actionId)
    {
        if (!_transientStateCoordinator.TryResolveWorkspaceAction(actionId, out WorkspaceSurfaceActionDefinition? action)
            || action is null)
            return;

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteWorkspaceActionAsync(action, CancellationToken.None),
            $"execute workspace action '{actionId}'");
    }

    private async void NavigatorPane_OnWorkflowSurfaceSelected(object? sender, string actionId)
    {
        if (!_transientStateCoordinator.TryResolveWorkspaceAction(actionId, out WorkspaceSurfaceActionDefinition? action)
            || action is null)
            return;

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteWorkspaceActionAsync(action, CancellationToken.None),
            $"execute workflow surface '{actionId}'");
    }

    private async void SectionHost_OnQuickActionRequested(object? sender, string controlId)
    {
        await RunUiActionAsync(
            () => _interactionCoordinator.HandleUiControlAsync(controlId, CancellationToken.None),
            $"execute section quick action '{controlId}'");
    }

    private async void CommandDialogPane_OnDialogActionSelected(object? sender, string actionId)
    {
        if (await TryHandleDialogHostActionAsync(actionId))
        {
            return;
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteDialogActionAsync(actionId, CancellationToken.None),
            $"execute dialog action '{actionId}'");
    }

    private async void CommandDialogPane_OnDialogFieldValueChanged(object? sender, DialogFieldValueChangedEventArgs e)
    {
        await RunUiActionAsync(
            () => _adapter.UpdateDialogFieldAsync(e.FieldId, e.Value, CancellationToken.None),
            $"update dialog field '{e.FieldId}'");
    }

    private async void MenuBar_OnMenuCommandSelected(object? sender, string commandId)
    {
        if (await TryHandleMenuHostCommandAsync(commandId))
        {
            return;
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute menu command '{commandId}'");
    }

    private async Task<bool> TryHandleDialogHostActionAsync(string actionId)
    {
        DesktopDialogState? dialog = _adapter.State.ActiveDialog;
        if (dialog is null)
        {
            return false;
        }

        if (string.Equals(dialog.Id, "dialog.master_index", StringComparison.Ordinal)
            && string.Equals(actionId, "open_source", StringComparison.Ordinal))
        {
            string selectedSource = DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectedSource") ?? string.Empty;
            string sourcebook = DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCurrentSourcebook") ?? "selected sourcebook";

            await RunUiActionAsync(
                async () =>
                {
                    if (!string.IsNullOrWhiteSpace(selectedSource))
                    {
                        DesktopCrashRuntime.TryOpenPathInShell(selectedSource);
                    }

                    await _interactionCoordinator.ExecuteDialogActionAsync(actionId, CancellationToken.None);
                },
                $"open linked source for '{sourcebook}'");
            return true;
        }

        if (string.Equals(dialog.Id, "dialog.character_roster", StringComparison.Ordinal)
            && (string.Equals(actionId, "open_watch_file", StringComparison.Ordinal)
                || string.Equals(actionId, "open_roster_folder", StringComparison.Ordinal)
                || string.Equals(actionId, "open_portrait", StringComparison.Ordinal)))
        {
            string targetPath;
            if (string.Equals(actionId, "open_watch_file", StringComparison.Ordinal))
            {
                string rosterFolderPath = DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderPath") ?? string.Empty;
                string selectedWatchFile = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedWatchFile") ?? string.Empty;
                targetPath = string.IsNullOrWhiteSpace(rosterFolderPath) || string.IsNullOrWhiteSpace(selectedWatchFile)
                    ? string.Empty
                    : Path.Combine(rosterFolderPath, selectedWatchFile);
            }
            else
            {
                string fieldId = string.Equals(actionId, "open_roster_folder", StringComparison.Ordinal)
                    ? "rosterWatchFolderPath"
                    : "rosterPortraitPath";
                targetPath = DesktopDialogFieldValueParser.GetValue(dialog, fieldId) ?? string.Empty;
            }

            await RunUiActionAsync(
                async () =>
                {
                    if (!string.IsNullOrWhiteSpace(targetPath))
                    {
                        DesktopCrashRuntime.TryOpenPathInShell(targetPath);
                    }

                    await _interactionCoordinator.ExecuteDialogActionAsync(actionId, CancellationToken.None);
                },
                string.Equals(actionId, "open_watch_file", StringComparison.Ordinal)
                    ? "open roster watch file"
                    : string.Equals(actionId, "open_roster_folder", StringComparison.Ordinal)
                    ? "open roster folder"
                    : "open roster portrait");
            return true;
        }

        return false;
    }

    private async Task<bool> TryHandleMenuHostCommandAsync(string commandId)
    {
        switch (commandId)
        {
            case "new_window":
                await RunUiActionAsync(
                    () =>
                    {
                        MainWindow window = NewWindowFactoryForTesting?.Invoke() ?? new MainWindow();
                        NewWindowCreatedForTesting?.Invoke(window);
                        window.Show();
                        return Task.CompletedTask;
                    },
                    "open new window");
                return true;
            case "exit":
                await RunUiActionAsync(
                    () =>
                    {
                        Close();
                        return Task.CompletedTask;
                    },
                    "close desktop shell");
                return true;
            case "wiki":
                await OpenExternalMenuCommandAsync(commandId, LegacyWikiUrl);
                return true;
            case "discord":
                await OpenExternalMenuCommandAsync(commandId, LegacyDiscordUrl);
                return true;
            case "revision_history":
                await RunUiActionAsync(
                    () => DesktopVersionHistoryWindow.ShowAsync(this),
                    "open revision history");
                return true;
            case "dumpshock":
                await OpenExternalMenuCommandAsync(commandId, LegacyIssueTrackerUrl);
                return true;
            case "report_bug":
                await RunUiActionAsync(
                    () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
                    "open report bug dialog");
                return true;
            case "about":
                await RunUiActionAsync(
                    () => DesktopAboutWindow.ShowAsync(this),
                    "open about");
                return true;
            case "update":
                await RunUiActionAsync(
                    () => DesktopUpdateWindow.ShowAsync(this, DesktopHeadId),
                    "open update status");
                return true;
            case "open_character":
                await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.OpenOnly);
                return true;
            case "open_for_printing":
                await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.PrintAfterImport);
                return true;
            case "open_for_export":
                await OpenCharacterFromFilePickerAsync(DesktopOpenCharacterMode.ExportAfterImport);
                return true;
            default:
                return false;
        }
    }

    private Task OpenNativeReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, DesktopHeadId);

    private async Task OpenExternalMenuCommandAsync(string commandId, string target)
    {
        bool opened = false;
        await RunUiActionAsync(
            () =>
            {
                opened = DesktopCrashRuntime.TryOpenPathInShell(target);
                return Task.CompletedTask;
            },
            $"open external menu command '{commandId}'");

        if (opened)
        {
            return;
        }

        await RunUiActionAsync(
            () => _interactionCoordinator.ExecuteCommandAsync(commandId, CancellationToken.None),
            $"execute menu command '{commandId}'");
    }
}
