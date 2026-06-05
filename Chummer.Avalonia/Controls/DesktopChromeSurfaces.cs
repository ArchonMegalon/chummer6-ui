using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public interface IToolStripSurface
{
    event EventHandler? ImportFileRequested;
    event EventHandler? OpenForPrintingRequested;
    event EventHandler? OpenForExportRequested;
    event EventHandler? ImportRawRequested;
    event EventHandler? SaveRequested;
    event EventHandler? PrintRequested;
    event EventHandler? CopyRequested;
    event EventHandler? DesktopHomeRequested;
    event EventHandler? HorizonsRequested;
    event EventHandler? GmPrepRequested;
    event EventHandler? RosterMovementRequested;
    event EventHandler? RuleEnvironmentStudioRequested;
    event EventHandler? CloseWorkspaceRequested;
    event EventHandler? CampaignWorkspaceRequested;
    event EventHandler? UpdateStatusRequested;
    event EventHandler? InstallLinkingRequested;
    event EventHandler? SupportRequested;
    event EventHandler? ReportIssueRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? LoadDemoRunnerRequested;

    void SetState(ToolStripState state);
    void SetStatusText(string statusText);
}

public interface IMenuBarSurface
{
    event EventHandler<string>? MenuSelected;
    event EventHandler<string>? MenuCommandSelected;

    void SetState(MenuBarState state);
}

public interface IStatusStripSurface
{
    void SetState(StatusStripState state);
}
