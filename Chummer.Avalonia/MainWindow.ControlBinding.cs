using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia;

internal static class MainWindowControlBinder
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;

    public static MainWindowControls Bind(
        ToolStripControl toolStrip,
        SummaryHeaderControl summaryHeader,
        ShellMenuBarControl menuBar,
        CharacterRosterControl characterRoster,
        NavigatorPaneControl navigatorPane,
        ClassicFormPortHostControl classicFormPortHost,
        SectionHostControl sectionHost,
        CommandDialogPaneControl commandDialogPane,
        CoachSidecarControl coachSidecar,
        StatusStripControl statusStrip,
        EventHandler onImportFileRequested,
        EventHandler onOpenForPrintingRequested,
        EventHandler onOpenForExportRequested,
        EventHandler onImportRawRequested,
        EventHandler onSaveRequested,
        EventHandler onPrintRequested,
        EventHandler onCopyRequested,
        EventHandler onDesktopHomeRequested,
        EventHandler onGmPrepRequested,
        EventHandler onRosterMovementRequested,
        EventHandler onRuleEnvironmentStudioRequested,
        EventHandler onCloseWorkspaceRequested,
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
        EventHandler<string> onRosterWorkspaceSelected,
        EventHandler<string> onWorkspaceSelected,
        EventHandler<string> onNavigationTabSelected,
        EventHandler<string> onSectionActionSelected,
        EventHandler<string> onWorkflowSurfaceSelected,
        EventHandler<string> onSectionQuickActionRequested,
        EventHandler<AttributeEditRequest> onSectionAttributeEditRequested,
        EventHandler onCoachLaunchOpenRequested,
        EventHandler onCoachLaunchCopyRequested,
        EventHandler<string> onCommandSelected,
        EventHandler<string> onDialogActionSelected,
        EventHandler<DialogFieldValueChangedEventArgs> onDialogFieldValueChanged,
        EventHandler<string> onMenuCommandSelected)
    {
        toolStrip.ImportFileRequested += onImportFileRequested;
        toolStrip.OpenForPrintingRequested += onOpenForPrintingRequested;
        toolStrip.OpenForExportRequested += onOpenForExportRequested;
        toolStrip.ImportRawRequested += onImportRawRequested;
        toolStrip.SaveRequested += onSaveRequested;
        toolStrip.PrintRequested += onPrintRequested;
        toolStrip.CopyRequested += onCopyRequested;
        toolStrip.DesktopHomeRequested += onDesktopHomeRequested;
        toolStrip.GmPrepRequested += onGmPrepRequested;
        toolStrip.RosterMovementRequested += onRosterMovementRequested;
        toolStrip.RuleEnvironmentStudioRequested += onRuleEnvironmentStudioRequested;
        toolStrip.CloseWorkspaceRequested += onCloseWorkspaceRequested;
        toolStrip.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        toolStrip.UpdateStatusRequested += onUpdateStatusRequested;
        toolStrip.InstallLinkingRequested += onInstallLinkingRequested;
        toolStrip.SupportRequested += onSupportRequested;
        toolStrip.ReportIssueRequested += onReportIssueRequested;
        toolStrip.SettingsRequested += onSettingsRequested;
        toolStrip.LoadDemoRunnerRequested += onLoadDemoRunnerRequested;
        summaryHeader.NavigationTabSelected += onNavigationTabSelected;
        summaryHeader.LoadDemoRunnerRequested += onLoadDemoRunnerRequested;
        summaryHeader.KeepLocalWorkRequested += onKeepLocalWorkRequested;
        summaryHeader.SaveLocalWorkRequested += onSaveRequested;
        summaryHeader.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        summaryHeader.WorkspaceSupportRequested += onWorkspaceSupportRequested;
        menuBar.MenuSelected += onMenuSelected;
        characterRoster.SelectionChanged += (_, args) => onRosterWorkspaceSelected(characterRoster, args.SelectedNode.Id);
        navigatorPane.WorkspaceSelected += onWorkspaceSelected;
        navigatorPane.NavigationTabSelected += onNavigationTabSelected;
        navigatorPane.SectionActionSelected += onSectionActionSelected;
        navigatorPane.WorkflowSurfaceSelected += onWorkflowSurfaceSelected;
        sectionHost.NavigationTabSelected += onNavigationTabSelected;
        sectionHost.SectionActionSelected += onSectionActionSelected;
        sectionHost.QuickActionRequested += onSectionQuickActionRequested;
        sectionHost.AttributeEditRequested += onSectionAttributeEditRequested;
        coachSidecar.OpenLaunchRequested += onCoachLaunchOpenRequested;
        coachSidecar.CopyLaunchRequested += onCoachLaunchCopyRequested;
        commandDialogPane.CommandSelected += onCommandSelected;
        commandDialogPane.DialogActionSelected += onDialogActionSelected;
        commandDialogPane.DialogFieldValueChanged += onDialogFieldValueChanged;
        menuBar.MenuCommandSelected += onMenuCommandSelected;

        return new MainWindowControls(
            toolStrip,
            summaryHeader,
            menuBar,
            characterRoster,
            navigatorPane,
            classicFormPortHost,
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
    CharacterRosterControl CharacterRoster,
    NavigatorPaneControl NavigatorPane,
    ClassicFormPortHostControl ClassicFormPortHost,
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
        SummaryHeader.SetWorkspaceStripState(shellFrame.ChromeState.WorkspaceStrip);
        SummaryHeader.SetState(shellFrame.ChromeState.SummaryHeader);
        StatusStrip.SetState(shellFrame.ChromeState.StatusStrip);
        CharacterRoster.SetState(shellFrame.RosterPaneState);
        CommandDialogPane.SetState(shellFrame.CommandDialogPaneState);
        NavigatorPane.SetState(shellFrame.NavigatorPaneState);
        ClassicFormPortHost.SetState(shellFrame.SectionHostState, shellFrame.CommandDialogPaneState.SelectedCommandId);
        SectionHost.SetState(shellFrame.SectionHostState);
    }

    public void ApplyCoachSidecar(CoachSidecarPaneState state)
    {
        CoachSidecar.SetState(state);
    }
}
