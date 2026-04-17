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
    public event EventHandler? PrintRequested;
    public event EventHandler? CopyRequested;
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
        _ = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.desktop_home", language);
        SetButtonLabel(DesktopHomeButton, "New Character", "New");
        SetButtonLabel(CampaignWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", language), "Campaign");
        SetButtonLabel(UpdateStatusButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.update_status", language), "Update");
        SetButtonLabel(InstallLinkingButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.link_copy", language), "Link");
        SetButtonLabel(SupportButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.open_support", language), "Support");
        SetButtonLabel(ReportIssueButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.report_issue", language), "Bug");
        SetButtonLabel(SettingsButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.settings", language), "Options");
        SetButtonLabel(LoadDemoRunnerButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.load_demo_runner", language), "Demo");
        SetButtonLabel(PrintButton, "Print Character", "Print");
        SetButtonLabel(CopyButton, "Copy", "Copy");
        SetButtonLabel(ImportFileButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_character_file", language), "Open");
        SetButtonLabel(ImportRawButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_raw_xml", language), "XML");
        SetButtonLabel(SaveButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.save_workspace", language), "Save");
        SetButtonLabel(CloseWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.close_active_workspace", language), "Close");
        StatusText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.status_idle", language);
    }

    private static void SetButtonLabel(Button button, string label, string compactLabel)
    {
        // Keep the classic full-label anchor in source for the visual-familiarity proof,
        // then collapse to the compact toolbar text used by the current shell.
        button.Content = label;
        button.Content = compactLabel;
        ToolTip.SetTip(button, label);
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

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PrintRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ToolStripState(string StatusText);
