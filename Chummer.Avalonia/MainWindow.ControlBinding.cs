using Chummer.Avalonia.Controls;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia;

internal static class MainWindowControlBinder
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;

    public static MainWindowControls Bind(
        ToolStripControl toolStrip,
        SummaryHeaderControl summaryHeader,
        ShellMenuBarControl menuBar,
        NavigatorPaneControl navigatorPane,
        SectionHostControl sectionHost,
        CommandDialogPaneControl commandDialogPane,
        CoachSidecarControl coachSidecar,
        StatusStripControl statusStrip,
        EventHandler onImportFileRequested,
        EventHandler onImportRawRequested,
        EventHandler onSaveRequested,
        EventHandler onPrintRequested,
        EventHandler onCopyRequested,
        EventHandler onCloseWorkspaceRequested,
        EventHandler onDesktopHomeRequested,
        EventHandler onCampaignWorkspaceRequested,
        EventHandler onUpdateStatusRequested,
        EventHandler onInstallLinkingRequested,
        EventHandler onSupportRequested,
        EventHandler onReportIssueRequested,
        EventHandler onSettingsRequested,
        EventHandler onLoadDemoRunnerRequested,
        EventHandler onKeepLocalWorkRequested,
        EventHandler onWorkspaceSupportRequested,
        EventHandler<string> onMenuSelected,
        EventHandler<string> onWorkspaceSelected,
        EventHandler<string> onNavigationTabSelected,
        EventHandler<string> onSectionActionSelected,
        EventHandler<string> onWorkflowSurfaceSelected,
        EventHandler<string> onSectionQuickActionRequested,
        EventHandler onCoachLaunchCopyRequested,
        EventHandler<string> onCommandSelected,
        EventHandler<string> onDialogActionSelected,
        EventHandler<DialogFieldValueChangedEventArgs> onDialogFieldValueChanged,
        EventHandler<string> onMenuCommandSelected)
    {
        toolStrip.ImportFileRequested += onImportFileRequested;
        toolStrip.ImportRawRequested += onImportRawRequested;
        toolStrip.SaveRequested += onSaveRequested;
        toolStrip.PrintRequested += onPrintRequested;
        toolStrip.CopyRequested += onCopyRequested;
        toolStrip.CloseWorkspaceRequested += onCloseWorkspaceRequested;
        toolStrip.DesktopHomeRequested += onDesktopHomeRequested;
        toolStrip.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        toolStrip.UpdateStatusRequested += onUpdateStatusRequested;
        toolStrip.InstallLinkingRequested += onInstallLinkingRequested;
        toolStrip.SupportRequested += onSupportRequested;
        toolStrip.ReportIssueRequested += onReportIssueRequested;
        toolStrip.SettingsRequested += onSettingsRequested;
        toolStrip.LoadDemoRunnerRequested += onLoadDemoRunnerRequested;
        summaryHeader.NavigationTabSelected += onNavigationTabSelected;
        summaryHeader.KeepLocalWorkRequested += onKeepLocalWorkRequested;
        summaryHeader.SaveLocalWorkRequested += onSaveRequested;
        summaryHeader.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        summaryHeader.WorkspaceSupportRequested += onWorkspaceSupportRequested;
        menuBar.MenuSelected += onMenuSelected;
        navigatorPane.WorkspaceSelected += onWorkspaceSelected;
        navigatorPane.NavigationTabSelected += onNavigationTabSelected;
        navigatorPane.SectionActionSelected += onSectionActionSelected;
        navigatorPane.WorkflowSurfaceSelected += onWorkflowSurfaceSelected;
        sectionHost.QuickActionRequested += onSectionQuickActionRequested;
        coachSidecar.CopyLaunchRequested += onCoachLaunchCopyRequested;
        commandDialogPane.CommandSelected += onCommandSelected;
        commandDialogPane.DialogActionSelected += onDialogActionSelected;
        commandDialogPane.DialogFieldValueChanged += onDialogFieldValueChanged;
        menuBar.MenuCommandSelected += onMenuCommandSelected;

        return new MainWindowControls(
            toolStrip,
            summaryHeader,
            menuBar,
            navigatorPane,
            sectionHost,
            commandDialogPane,
            coachSidecar,
            statusStrip);
    }
}

internal sealed record MainWindowControls(
    ToolStripControl ToolStrip,
    SummaryHeaderControl SummaryHeader,
    ShellMenuBarControl MenuBar,
    NavigatorPaneControl NavigatorPane,
    SectionHostControl SectionHost,
    CommandDialogPaneControl CommandDialogPane,
    CoachSidecarControl CoachSidecar,
    StatusStripControl StatusStrip)
{
    public string SectionHostInputText => SectionHost.XmlInputText;

    public void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        ToolStrip.SetState(shellFrame.HeaderState.ToolStrip);
        MenuBar.SetState(shellFrame.HeaderState.MenuBar);
        SummaryHeader.SetState(shellFrame.ChromeState.SummaryHeader);
        StatusStrip.SetState(shellFrame.ChromeState.StatusStrip);
        CommandDialogPane.SetState(shellFrame.CommandDialogPaneState);
        NavigatorPane.SetState(shellFrame.NavigatorPaneState);
        SectionHost.SetState(shellFrame.SectionHostState);
    }

    public void ApplyCoachSidecar(CoachSidecarPaneState state)
    {
        CoachSidecar.SetState(state);
    }
}
