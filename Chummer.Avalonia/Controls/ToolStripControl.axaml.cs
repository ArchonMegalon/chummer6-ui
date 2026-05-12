using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;
using System;

namespace Chummer.Avalonia.Controls;

public partial class ToolStripControl : UserControl
{
    public ToolStripControl()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public event EventHandler? ImportFileRequested;
    public event EventHandler? OpenForPrintingRequested;
    public event EventHandler? OpenForExportRequested;
    public event EventHandler? ImportRawRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? PrintRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? DesktopHomeRequested;
    public event EventHandler? GmPrepRequested;
    public event EventHandler? RosterMovementRequested;
    public event EventHandler? RuleEnvironmentStudioRequested;
    public event EventHandler? CloseWorkspaceRequested;
    public event EventHandler? CampaignWorkspaceRequested;
    public event EventHandler? UpdateStatusRequested;
    public event EventHandler? InstallLinkingRequested;
    public event EventHandler? SupportRequested;
    public event EventHandler? ReportIssueRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? LoadDemoRunnerRequested;

    public void SetState(ToolStripState state)
    {
        SetStatusText(state.StatusText);
        ApplyVisibility(OpenForExportButton, state.ShowOpenForExport);
        ApplyVisibility(GmPrepButton, state.ShowGmPrep);
        ApplyVisibility(RosterMovementButton, state.ShowRosterMovement);
        ApplyVisibility(CampaignWorkspaceButton, state.ShowCampaignWorkspace);
        ApplyVisibility(LoadDemoRunnerButton, state.ShowLoadDemoRunner);
    }

    public void SetStatusText(string statusText)
    {
        StatusText.Text = statusText;
        StatusTextBorder.IsVisible = !string.IsNullOrWhiteSpace(statusText);
    }

    private static void ApplyVisibility(Control control, bool? isVisible)
    {
        if (!isVisible.HasValue)
        {
            return;
        }

        control.IsVisible = isVisible.Value;
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        _ = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.desktop_home", language);
        SetButtonLabel(SaveButton, "Save Workspace");
        SetButtonLabel(PrintButton, "Print Character", "Print");
        SetButtonLabel(CopyButton, "Copy", "Copy");
        SetButtonLabel(DesktopHomeButton, "Desktop Home");
        SetButtonLabel(ImportFileButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_character_file", language));
        SetButtonLabel(CloseWorkspaceButton, "Close Active Workspace");
        SetButtonLabel(OpenForPrintingButton, "Open Character for Printing");
        SetButtonLabel(OpenForExportButton, "Open Character for Export");
        SetButtonLabel(GmPrepButton, "Open GM Prep Packets");
        SetButtonLabel(RosterMovementButton, "Review Roster Movement");
        SetButtonLabel(RuleEnvironmentStudioButton, "Open Rule Environment Studio");
        SetButtonLabel(CampaignWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", language));
        SetButtonLabel(UpdateStatusButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.update_status", language));
        SetButtonLabel(InstallLinkingButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.link_copy", language));
        SetButtonLabel(SupportButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.open_support", language));
        SetButtonLabel(ReportIssueButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.report_issue", language));
        SetButtonLabel(SettingsButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.settings", language));
        SetButtonLabel(LoadDemoRunnerButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.load_demo_runner", language));
        SetButtonLabel(ImportRawButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_raw_xml", language));
        StatusText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.status_idle", language);
        StatusTextBorder.IsVisible = true;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        SetButtonLabel(button, label, label);
    }

    private static void SetButtonLabel(Button button, string label, string shortLabel)
    {
        _ = shortLabel;
        button.Content = label;
        ToolTip.SetTip(button, label);
    }

    private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImportFileRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PrintRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DesktopHomeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopHomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GmPrepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GmPrepRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RosterMovementButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RosterMovementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RuleEnvironmentStudioButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RuleEnvironmentStudioRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenForPrintingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenForPrintingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenForExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenForExportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CampaignWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CampaignWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateStatusButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateStatusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InstallLinkingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InstallLinkingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SupportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SupportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReportIssueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ReportIssueRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LoadDemoRunnerButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ImportRawButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImportRawRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ToolStripState(
    string StatusText,
    bool? ShowOpenForExport = null,
    bool? ShowGmPrep = null,
    bool? ShowRosterMovement = null,
    bool? ShowCampaignWorkspace = null,
    bool? ShowLoadDemoRunner = null);
