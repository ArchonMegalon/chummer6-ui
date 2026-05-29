using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chummer.Presentation.Overview;
using System.Collections.Generic;

namespace Chummer.Avalonia.Controls;

public partial class ClassicToolStrip : UserControl, IToolStripSurface
{
    private static readonly IReadOnlyDictionary<string, string> ButtonIconAssets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(SaveButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/disk.png",
        [nameof(PrintButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/printer.png",
        [nameof(CopyButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/page_copy.png",
        [nameof(DesktopHomeButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/user_add.png",
        [nameof(ImportFileButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/folder_page.png",
        [nameof(CloseWorkspaceButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/cancel.png",
        [nameof(OpenForPrintingButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/folder_print.png",
        [nameof(OpenForExportButton)] = "avares://Chummer.Avalonia/Assets/chummer5a-icons/folder_script_go.png",
    };

    private static readonly Dictionary<string, Bitmap> IconCache = new(StringComparer.Ordinal);

    public ClassicToolStrip()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyLabels();
        SetStatusText("State: idle");
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
        ApplyOptionalVisibility("OpenForExportButton", state.ShowOpenForExport);
        ApplyOptionalVisibility("GmPrepButton", state.ShowGmPrep);
        ApplyOptionalVisibility("RosterMovementButton", state.ShowRosterMovement);
        ApplyOptionalVisibility("CampaignWorkspaceButton", state.ShowCampaignWorkspace);
        ApplyOptionalVisibility("LoadDemoRunnerButton", state.ShowLoadDemoRunner);
    }

    public void SetStatusText(string statusText)
    {
        if (this.FindControl<TextBlock>("StatusText") is { } statusTextBlock)
        {
            statusTextBlock.Text = statusText;
        }

        if (this.FindControl<Border>("StatusTextBorder") is { } statusBorder)
        {
            statusBorder.IsVisible = !string.IsNullOrWhiteSpace(statusText)
            && !string.Equals(statusText.Trim(), "State: idle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(statusText.Trim(), "idle", StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ApplyLabels()
    {
        SetButtonLabel("ImportFileButton", "Open Character", "Open");
        SetButtonLabel("SaveButton", "Save Character", "Save");
        SetButtonLabel("PrintButton", "Print Character", "Print");
        SetButtonLabel("CopyButton", "Copy", "Copy");
        SetButtonLabel("OpenForPrintingButton", "Open Character for Printing", "Open Print");
        SetButtonLabel("OpenForExportButton", "Open Character for Export", "Open Export");
        SetButtonLabel("SettingsButton", "Global Settings", "Settings");
        SetButtonLabel("ImportRawButton", "Import Raw XML", "Raw XML");
        SetButtonLabel("DesktopHomeButton", "Desktop Home", "Home");
        SetButtonLabel("CampaignWorkspaceButton", "Campaign Workspace", "Campaign");
        SetButtonLabel("GmPrepButton", "Open GM Prep Packets", "GM Prep");
        SetButtonLabel("RosterMovementButton", "Open Roster Movement", "Roster");
        SetButtonLabel("RuleEnvironmentStudioButton", "Open Rule Environment Studio", "Rules");
        SetButtonLabel("UpdateStatusButton", "Review Update Status", "Update");
        SetButtonLabel("InstallLinkingButton", "Link This Copy", "Link Copy");
        SetButtonLabel("SupportButton", "Open Support", "Support");
        SetButtonLabel("ReportIssueButton", "Report Issue", "Issue");
        SetButtonLabel("LoadDemoRunnerButton", "Load Demo Runner", "Demo");
        SetButtonLabel("CloseWorkspaceButton", "Close Active Workspace", "Close");
    }

    private void SetButtonLabel(string buttonName, string label, string shortLabel)
    {
        if (this.FindControl<Button>(buttonName) is not { } button)
        {
            return;
        }

        if (TryCreateButtonIcon(button.Name, out Image? icon))
        {
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    icon!,
                    new TextBlock
                    {
                        Text = shortLabel,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }
        else
        {
            button.Content = shortLabel;
        }

        ToolTip.SetTip(button, label);
    }

    private static bool TryCreateButtonIcon(string? buttonName, out Image? icon)
    {
        icon = null;
        if (string.IsNullOrWhiteSpace(buttonName)
            || !ButtonIconAssets.TryGetValue(buttonName, out string? assetPath))
        {
            return false;
        }

        if (!IconCache.TryGetValue(assetPath, out Bitmap? bitmap))
        {
            bitmap = new Bitmap(AssetLoader.Open(new Uri(assetPath)));
            IconCache[assetPath] = bitmap;
        }

        icon = new Image
        {
            Source = bitmap,
            Width = 16,
            Height = 16,
            Classes = { "tool-button-icon" },
            VerticalAlignment = VerticalAlignment.Center
        };
        return true;
    }

    private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e) => ImportFileRequested?.Invoke(this, EventArgs.Empty);
    private void OpenForPrintingButton_OnClick(object? sender, RoutedEventArgs e) => OpenForPrintingRequested?.Invoke(this, EventArgs.Empty);
    private void OpenForExportButton_OnClick(object? sender, RoutedEventArgs e) => OpenForExportRequested?.Invoke(this, EventArgs.Empty);
    private void ImportRawButton_OnClick(object? sender, RoutedEventArgs e) => ImportRawRequested?.Invoke(this, EventArgs.Empty);
    private void SaveButton_OnClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void PrintButton_OnClick(object? sender, RoutedEventArgs e) => PrintRequested?.Invoke(this, EventArgs.Empty);
    private void CopyButton_OnClick(object? sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, EventArgs.Empty);
    private void DesktopHomeButton_OnClick(object? sender, RoutedEventArgs e) => DesktopHomeRequested?.Invoke(this, EventArgs.Empty);
    private void GmPrepButton_OnClick(object? sender, RoutedEventArgs e) => GmPrepRequested?.Invoke(this, EventArgs.Empty);
    private void RosterMovementButton_OnClick(object? sender, RoutedEventArgs e) => RosterMovementRequested?.Invoke(this, EventArgs.Empty);
    private void RuleEnvironmentStudioButton_OnClick(object? sender, RoutedEventArgs e) => RuleEnvironmentStudioRequested?.Invoke(this, EventArgs.Empty);
    private void CloseWorkspaceButton_OnClick(object? sender, RoutedEventArgs e) => CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    private void CampaignWorkspaceButton_OnClick(object? sender, RoutedEventArgs e) => CampaignWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    private void UpdateStatusButton_OnClick(object? sender, RoutedEventArgs e) => UpdateStatusRequested?.Invoke(this, EventArgs.Empty);
    private void InstallLinkingButton_OnClick(object? sender, RoutedEventArgs e) => InstallLinkingRequested?.Invoke(this, EventArgs.Empty);
    private void SupportButton_OnClick(object? sender, RoutedEventArgs e) => SupportRequested?.Invoke(this, EventArgs.Empty);
    private void ReportIssueButton_OnClick(object? sender, RoutedEventArgs e) => ReportIssueRequested?.Invoke(this, EventArgs.Empty);
    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void LoadDemoRunnerButton_OnClick(object? sender, RoutedEventArgs e) => LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);

    private void ApplyOptionalVisibility(string controlName, bool? isVisible)
    {
        if (!isVisible.HasValue)
        {
            return;
        }

        if (this.FindControl<Control>(controlName) is { } control)
        {
            control.IsVisible = isVisible.Value;
        }
    }
}
