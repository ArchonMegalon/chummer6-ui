using Chummer.Avalonia.Controls;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia;

internal static class MainWindowControlBinder
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;

    public static MainWindowControls Bind(
        ToolStripControl toolStrip,
        WorkspaceStripControl workspaceStrip,
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
        EventHandler onCloseWorkspaceRequested,
        EventHandler onDesktopHomeRequested,
        EventHandler onCampaignWorkspaceRequested,
        EventHandler onInstallLinkingRequested,
        EventHandler onSupportRequested,
        EventHandler onRuntimeInspectorRequested,
        EventHandler<string> onMenuSelected,
        EventHandler<string> onWorkspaceSelected,
        EventHandler<string> onNavigationTabSelected,
        EventHandler<string> onSectionActionSelected,
        EventHandler<string> onWorkflowSurfaceSelected,
        EventHandler onCoachLaunchCopyRequested,
        EventHandler<string> onCommandSelected,
        EventHandler<string> onDialogActionSelected)
    {
        toolStrip.ImportFileRequested += onImportFileRequested;
        toolStrip.ImportRawRequested += onImportRawRequested;
        toolStrip.SaveRequested += onSaveRequested;
        toolStrip.CloseWorkspaceRequested += onCloseWorkspaceRequested;
        toolStrip.DesktopHomeRequested += onDesktopHomeRequested;
        toolStrip.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        toolStrip.InstallLinkingRequested += onInstallLinkingRequested;
        toolStrip.SupportRequested += onSupportRequested;
        summaryHeader.RuntimeInspectorRequested += onRuntimeInspectorRequested;
        menuBar.MenuSelected += onMenuSelected;
        navigatorPane.WorkspaceSelected += onWorkspaceSelected;
        navigatorPane.NavigationTabSelected += onNavigationTabSelected;
        navigatorPane.SectionActionSelected += onSectionActionSelected;
        navigatorPane.WorkflowSurfaceSelected += onWorkflowSurfaceSelected;
        coachSidecar.CopyLaunchRequested += onCoachLaunchCopyRequested;
        commandDialogPane.CommandSelected += onCommandSelected;
        commandDialogPane.DialogActionSelected += onDialogActionSelected;

        return new MainWindowControls(
            toolStrip,
            workspaceStrip,
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
    WorkspaceStripControl WorkspaceStrip,
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
        WorkspaceStrip.SetState(shellFrame.ChromeState.WorkspaceStrip);
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
