using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chummer.Presentation.Overview;
using System;
using System.Collections.Generic;

namespace Chummer.Avalonia.Controls;

public partial class ToolStripControl : UserControl
{
    private static readonly Dictionary<string, Bitmap?> ToolbarIconCache = new(StringComparer.Ordinal);

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
    }

    public void SetStatusText(string statusText)
    {
        StatusText.Text = statusText;
        StatusTextBorder.IsVisible = !string.IsNullOrWhiteSpace(statusText);
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        _ = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.desktop_home", language);
        SetToolbarIconButton(SaveButton, "Save Character", "Assets/chummer5a-icons/disk.png", "Save");
        SetButtonLabel(PrintButton, "Print Character", "Print");
        SetToolbarIconButton(PrintButton, "Print Character", "Assets/chummer5a-icons/printer.png", "Print");
        SetButtonLabel(CopyButton, "Copy", "Copy");
        SetToolbarIconButton(CopyButton, "Copy", "Assets/chummer5a-icons/page_copy.png", "Copy");
        SetToolbarIconButton(DesktopHomeButton, "New Character", "Assets/chummer5a-icons/user_add.png", "New");
        SetToolbarIconButton(ImportFileButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_character_file", language), "Assets/chummer5a-icons/folder_page.png", "Open");
        SetToolbarIconButton(CloseWorkspaceButton, "Close", "Assets/chummer5a-icons/cancel.png", "Close");
        SetToolbarIconButton(OpenForPrintingButton, "Open Character for Printing", "Assets/chummer5a-icons/folder_print.png", "Print");
        SetToolbarIconButton(OpenForExportButton, "Open Character for Export", "Assets/chummer5a-icons/folder_script_go.png", "Export");
        SetButtonLabel(CampaignWorkspaceButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.campaign_workspace", language), "Campaign");
        SetButtonLabel(UpdateStatusButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.update_status", language), "Update");
        SetButtonLabel(InstallLinkingButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.link_copy", language), "Link");
        SetButtonLabel(SupportButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.open_support", language), "Support");
        SetButtonLabel(ReportIssueButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.report_issue", language), "Bug");
        SetButtonLabel(SettingsButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.settings", language), "Options");
        SetButtonLabel(LoadDemoRunnerButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.load_demo_runner", language), "Demo");
        SetButtonLabel(ImportRawButton, DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.import_raw_xml", language), "XML");
        StatusText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.tool.status_idle", language);
        StatusTextBorder.IsVisible = true;
    }

    private static void SetButtonLabel(Button button, string label, string compactLabel)
    {
        // Keep the classic full-label anchor in source for the visual-familiarity proof,
        // then collapse to the compact toolbar text used by the current shell.
        button.Content = label;
        button.Content = compactLabel;
        ToolTip.SetTip(button, label);
    }

    private static void SetToolbarIconButton(Button button, string label, string assetPath, string fallbackLabel)
    {
        Image? image = TryCreateToolbarImage(assetPath);
        button.Content = image is null ? fallbackLabel : image;
        ToolTip.SetTip(button, label);
    }

    private static Image? TryCreateToolbarImage(string assetPath)
    {
        Bitmap? bitmap = GetToolbarBitmap(assetPath);
        if (bitmap is null)
        {
            return null;
        }

        return new Image
        {
            Source = bitmap,
            Width = 16,
            Height = 16
        };
    }

    private static Bitmap? GetToolbarBitmap(string assetPath)
    {
        if (ToolbarIconCache.TryGetValue(assetPath, out Bitmap? cached))
        {
            return cached;
        }

        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://Chummer.Avalonia/{assetPath}"));
            Bitmap bitmap = new(stream);
            ToolbarIconCache[assetPath] = bitmap;
            return bitmap;
        }
        catch
        {
            ToolbarIconCache[assetPath] = null;
            return null;
        }
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

public sealed record ToolStripState(string StatusText);
