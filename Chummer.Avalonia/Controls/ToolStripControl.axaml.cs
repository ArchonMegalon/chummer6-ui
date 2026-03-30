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
        DesktopHomeButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.desktop_home", language);
        CampaignWorkspaceButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", language);
        UpdateStatusButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.update_status", language);
        InstallLinkingButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.link_copy", language);
        SupportButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.open_support", language);
        ReportIssueButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.report_issue", language);
        SettingsButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.settings", language);
        ImportFileButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_character_file", language);
        ImportRawButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_raw_xml", language);
        SaveButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.save_workspace", language);
        CloseWorkspaceButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.close_active_workspace", language);
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
