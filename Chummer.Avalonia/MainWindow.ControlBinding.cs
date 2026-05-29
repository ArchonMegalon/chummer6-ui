using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia;

internal static class MainWindowControlBinder
{
    private static readonly string UiKitShellChromeAdapterMarker = ShellChromeBoundary.RootClass;

    public static MainWindowControls Bind(
        ToolStripControl toolStrip,
        ClassicToolStrip classicToolStrip,
        SummaryHeaderControl summaryHeader,
        ShellMenuBarControl menuBar,
        ClassicMenuBar classicMenuBar,
        CharacterRosterControl characterRoster,
        NavigatorPaneControl navigatorPane,
        ClassicFormPortHostControl classicFormPortHost,
        SectionHostControl sectionHost,
        CommandDialogPaneControl commandDialogPane,
        CoachSidecarControl coachSidecar,
        StatusStripControl statusStrip,
        ClassicStatusStrip classicStatusStrip,
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
        AttachToolStripHandlers(toolStrip);
        AttachToolStripHandlers(classicToolStrip);
        AttachMenuBarHandlers(menuBar);
        AttachMenuBarHandlers(classicMenuBar);
        summaryHeader.NavigationTabSelected += onNavigationTabSelected;
        summaryHeader.LoadDemoRunnerRequested += onLoadDemoRunnerRequested;
        summaryHeader.KeepLocalWorkRequested += onKeepLocalWorkRequested;
        summaryHeader.SaveLocalWorkRequested += onSaveRequested;
        summaryHeader.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
        summaryHeader.WorkspaceSupportRequested += onWorkspaceSupportRequested;
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
        IToolStripSurface activeToolStrip = ClassicModePolicy.IsClassicDefault() ? classicToolStrip : toolStrip;
        IMenuBarSurface activeMenuBar = ClassicModePolicy.IsClassicDefault() ? classicMenuBar : menuBar;
        IStatusStripSurface activeStatusStrip = ClassicModePolicy.IsClassicDefault() ? classicStatusStrip : statusStrip;
        return new MainWindowControls(
            activeToolStrip,
            activeMenuBar,
            activeStatusStrip,
            toolStrip,
            classicToolStrip,
            summaryHeader,
            menuBar,
            classicMenuBar,
            characterRoster,
            navigatorPane,
            classicFormPortHost,
            sectionHost,
            commandDialogPane,
            coachSidecar,
            statusStrip,
            classicStatusStrip);

        void AttachToolStripHandlers(IToolStripSurface surface)
        {
            surface.ImportFileRequested += onImportFileRequested;
            surface.OpenForPrintingRequested += onOpenForPrintingRequested;
            surface.OpenForExportRequested += onOpenForExportRequested;
            surface.ImportRawRequested += onImportRawRequested;
            surface.SaveRequested += onSaveRequested;
            surface.PrintRequested += onPrintRequested;
            surface.CopyRequested += onCopyRequested;
            surface.DesktopHomeRequested += onDesktopHomeRequested;
            surface.GmPrepRequested += onGmPrepRequested;
            surface.RosterMovementRequested += onRosterMovementRequested;
            surface.RuleEnvironmentStudioRequested += onRuleEnvironmentStudioRequested;
            surface.CloseWorkspaceRequested += onCloseWorkspaceRequested;
            surface.CampaignWorkspaceRequested += onCampaignWorkspaceRequested;
            surface.UpdateStatusRequested += onUpdateStatusRequested;
            surface.InstallLinkingRequested += onInstallLinkingRequested;
            surface.SupportRequested += onSupportRequested;
            surface.ReportIssueRequested += onReportIssueRequested;
            surface.SettingsRequested += onSettingsRequested;
            surface.LoadDemoRunnerRequested += onLoadDemoRunnerRequested;
        }

        void AttachMenuBarHandlers(IMenuBarSurface surface)
        {
            surface.MenuSelected += onMenuSelected;
            surface.MenuCommandSelected += onMenuCommandSelected;
        }
    }
}

internal sealed record MainWindowControls(
    IToolStripSurface ToolStrip,
    IMenuBarSurface MenuBar,
    IStatusStripSurface StatusStrip,
    ToolStripControl ModernToolStrip,
    ClassicToolStrip ClassicToolStrip,
    SummaryHeaderControl SummaryHeader,
    ShellMenuBarControl ModernMenuBar,
    ClassicMenuBar ClassicMenuBar,
    CharacterRosterControl CharacterRoster,
    NavigatorPaneControl NavigatorPane,
    ClassicFormPortHostControl ClassicFormPortHost,
    SectionHostControl SectionHost,
    CommandDialogPaneControl CommandDialogPane,
    CoachSidecarControl CoachSidecar,
    StatusStripControl ModernStatusStrip,
    ClassicStatusStrip ClassicStatusStrip)
{
    public string SectionHostInputText => SectionHost.XmlInputText;

    public void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        ModernToolStrip.SetState(shellFrame.HeaderState.ToolStrip);
        ClassicToolStrip.SetState(shellFrame.HeaderState.ToolStrip);
        ModernMenuBar.SetState(shellFrame.HeaderState.MenuBar);
        ClassicMenuBar.SetState(shellFrame.HeaderState.MenuBar);
        SummaryHeader.SetWorkspaceStripState(shellFrame.ChromeState.WorkspaceStrip);
        SummaryHeader.SetState(shellFrame.ChromeState.SummaryHeader);
        ModernStatusStrip.SetState(shellFrame.ChromeState.StatusStrip);
        ClassicStatusStrip.SetState(shellFrame.ChromeState.StatusStrip);
        CharacterRoster.SetState(shellFrame.RosterPaneState);
        CommandDialogPane.SetState(shellFrame.CommandDialogPaneState);
        NavigatorPane.SetState(shellFrame.NavigatorPaneState);
        ClassicFormPortHost.SetState(shellFrame.SectionHostState, shellFrame.CommandDialogPaneState.SelectedCommandId);
        SectionHost.SetState(shellFrame.SectionHostState);
    }

    public void ApplyDesktopModeChrome(bool useClassicChrome)
    {
        ClassicMenuBar.IsVisible = useClassicChrome;
        ClassicToolStrip.IsVisible = useClassicChrome;
        ClassicStatusStrip.IsVisible = useClassicChrome;
        ModernMenuBar.IsVisible = !useClassicChrome;
        ModernToolStrip.IsVisible = !useClassicChrome;
        ModernStatusStrip.IsVisible = !useClassicChrome;
    }

    public void ApplyCoachSidecar(CoachSidecarPaneState state)
    {
        CoachSidecar.SetState(state);
    }
}
