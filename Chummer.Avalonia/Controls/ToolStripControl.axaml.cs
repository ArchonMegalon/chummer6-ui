using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class ToolStripControl : UserControl
{
    public ToolStripControl()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public event EventHandler? ImportFileRequested;
    public event EventHandler? ImportRawRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? CloseWorkspaceRequested;
    public event EventHandler? DesktopHomeRequested;
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
    }

    public void SetStatusText(string statusText)
    {
        StatusText.Text = statusText;
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        foreach ((Button button, string label) in new (Button Button, string Label)[]
                 {
                     (DesktopHomeButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.desktop_home", language)),
                     (CampaignWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", language)),
                     (UpdateStatusButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.update_status", language)),
                     (InstallLinkingButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.link_copy", language)),
                     (SupportButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.open_support", language)),
                     (ReportIssueButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.report_issue", language)),
                     (SettingsButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.settings", language)),
                     (LoadDemoRunnerButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.load_demo_runner", language)),
                     (ImportFileButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_character_file", language)),
                     (ImportRawButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_raw_xml", language)),
                     (SaveButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.save_workspace", language)),
                     (CloseWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.close_active_workspace", language)),
                 })
        {
            button.Content = label;
        }

        StatusText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.status_idle", language);
    }

    private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImportFileRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DesktopHomeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DesktopHomeRequested?.Invoke(this, EventArgs.Empty);
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

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ToolStripState(string StatusText);
