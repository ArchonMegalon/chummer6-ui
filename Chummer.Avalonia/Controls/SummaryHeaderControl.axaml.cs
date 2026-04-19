using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Contracts.Presentation;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    private const string PrimaryDesktopRouteStatus = "Primary route: Avalonia desktop keeps restore continuation, stale state, and conflict choices visible before any replacement.";
    private const string SaveAvailableStatus = "Save local work is available before restore or conflict review changes the desktop state.";
    private const string SaveUnavailableStatus = "Save local work is unavailable because no dirty local workspace is active; keep local work, review Campaign Workspace, or open Workspace Support.";
    private const string KeepLocalWorkStatus = "Kept local work visible; no restore, stale-state refresh, or conflict choice replaced desktop state.";
    private const string SaveLocalWorkRequestedStatus = "Save local work requested before any restore or conflict review changes desktop state.";
    private const string CampaignWorkspaceRequestedStatus = "Opening Campaign Workspace to review restore continuation, stale state, and conflict choices before replacing local work.";
    private const string WorkspaceSupportRequestedStatus = "Opening Workspace Support with restore continuation, stale-state, and conflict-choice context.";
    private const string RestoreContinuityDecisionOrder = "Decision order: 1. keep local work visible, 2. save local work when available, 3. review Campaign Workspace, 4. open Workspace Support before accepting restore replacement.";
    private const string RestoreContinuityLocalAuthority = "Local authority: the desktop workspace remains the working copy until you choose Campaign Workspace review or Workspace Support; restore review never replaces local work by itself.";
    private const string RestoreContinuityReplacementGuard = "Restore replacement guard: there is no one-click accept; Campaign Workspace review or Workspace Support must be opened before a server restore can replace local desktop work.";
    private const string RestoreContinuitySupportHandoff = "Support handoff: Workspace Support carries restore continuation, stale-state visibility, conflict choices, and the current local workspace anchor before any replacement.";
    private bool _suppressTabSelectionChanged;

    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler? KeepLocalWorkRequested;
    public event EventHandler? SaveLocalWorkRequested;
    public event EventHandler? CampaignWorkspaceRequested;
    public event EventHandler? WorkspaceSupportRequested;

    public SummaryHeaderControl()
    {
        InitializeComponent();
        ApplyRestoreContinuityAutomationText();
    }

    public void SetState(SummaryHeaderState state)
    {
        SetNavigationTabs(state.NavigationTabsHeading, state.NavigationTabs, state.ActiveTabId);
        SetRestoreContinuityStatus(
            state.RestoreContinuitySummary,
            state.StaleStateSummary,
            state.ConflictChoiceSummary,
            state.CanSaveLocalWorkBeforeRestore);
    }

    public void SetNavigationTabs(
        string heading,
        IReadOnlyList<NavigatorTabItem> navigationTabs,
        string? activeTabId)
    {
        LoadedRunnerTabStripHeading.Text = string.IsNullOrWhiteSpace(heading) ? "Runner Tabs" : heading;

        NavigatorTabItem[] visibleTabs = navigationTabs
            .Where(tab => tab.Enabled)
            .ToArray();
        LoadedRunnerTabStripBorder.IsVisible = visibleTabs.Length > 0;
        _suppressTabSelectionChanged = true;
        try
        {
            LoadedRunnerTabStrip.ItemsSource = visibleTabs;
            LoadedRunnerTabStrip.SelectedItem = visibleTabs.FirstOrDefault(tab => string.Equals(tab.Id, activeTabId, StringComparison.Ordinal))
                ?? visibleTabs.FirstOrDefault();
        }
        finally
        {
            _suppressTabSelectionChanged = false;
        }
    }

    private void LoadedRunnerTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabSelectionChanged)
        {
            return;
        }

        if (LoadedRunnerTabStrip.SelectedItem is NavigatorTabItem { Id.Length: > 0 } tab)
        {
            NavigationTabSelected?.Invoke(this, tab.Id);
        }
    }

    private void SetRestoreContinuityStatus(
        string? restoreContinuitySummary,
        string? staleStateSummary,
        string? conflictChoiceSummary,
        bool canSaveLocalWorkBeforeRestore)
    {
        RestoreContinuityStatusText.Text = restoreContinuitySummary ?? string.Empty;
        StaleStateStatusText.Text = staleStateSummary ?? string.Empty;
        ConflictChoiceStatusText.Text = conflictChoiceSummary ?? string.Empty;
        RestoreContinuityStatusBorder.IsVisible =
            !string.IsNullOrWhiteSpace(restoreContinuitySummary)
            || !string.IsNullOrWhiteSpace(staleStateSummary)
            || !string.IsNullOrWhiteSpace(conflictChoiceSummary);
        RestoreContinuityActionPanel.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        RestoreContinuityDecisionText.Text = BuildRestoreContinuityDecisionSummary(canSaveLocalWorkBeforeRestore);
        RestoreContinuityDecisionText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        RestoreContinuityDecisionOrderText.Text = RestoreContinuityDecisionOrder;
        RestoreContinuityDecisionOrderText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        RestoreContinuityLocalAuthorityText.Text = RestoreContinuityLocalAuthority;
        RestoreContinuityLocalAuthorityText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        RestoreContinuityReplacementGuardText.Text = RestoreContinuityReplacementGuard;
        RestoreContinuityReplacementGuardText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        RestoreContinuitySupportHandoffText.Text = RestoreContinuitySupportHandoff;
        RestoreContinuitySupportHandoffText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        KeepLocalWorkButton.Tag = "restore-decision-keep-local-work";
        ToolTip.SetTip(KeepLocalWorkButton, "Keep local work visible and do not apply a restore packet.");
        SaveLocalWorkButton.IsEnabled = canSaveLocalWorkBeforeRestore;
        SaveLocalWorkButton.Tag = "restore-decision-save-local-work";
        AutomationProperties.SetHelpText(SaveLocalWorkButton, canSaveLocalWorkBeforeRestore
            ? "Save the dirty local workspace before restore or conflict review."
            : "No dirty local workspace is active, so there is nothing to save before review.");
        ToolTip.SetTip(SaveLocalWorkButton, canSaveLocalWorkBeforeRestore
            ? "Save the dirty local workspace before opening restore or conflict review."
            : "No dirty local workspace is active, so there is nothing to save before review.");
        ReviewCampaignWorkspaceButton.Tag = "restore-decision-review-campaign-workspace";
        ToolTip.SetTip(ReviewCampaignWorkspaceButton, "Review campaign continuity, devices, and stale-state posture before accepting restore.");
        OpenWorkspaceSupportButton.Tag = "restore-decision-open-workspace-support";
        ToolTip.SetTip(OpenWorkspaceSupportButton, "Open workspace support with restore, stale-state, and conflict-choice context.");
        RestoreContinuityActionStatusText.Text = canSaveLocalWorkBeforeRestore
            ? SaveAvailableStatus
            : SaveUnavailableStatus;
        RestoreContinuityActionStatusText.IsVisible = RestoreContinuityStatusBorder.IsVisible;
        ClearRestoreDecisionSelection();
    }

    private void ApplyRestoreContinuityAutomationText()
    {
        AutomationProperties.SetName(RestoreContinuityStatusBorder, "Restore continuity decision gate");
        AutomationProperties.SetHelpText(
            RestoreContinuityStatusBorder,
            "Primary Avalonia desktop route keeps restore continuation, stale state, and conflict choices visible before replacement.");
        AutomationProperties.SetName(RestoreContinuityStatusText, "Restore continuation status");
        AutomationProperties.SetName(StaleStateStatusText, "Stale state visibility status");
        AutomationProperties.SetName(ConflictChoiceStatusText, "Conflict choice status");
        AutomationProperties.SetName(RestoreContinuityDecisionText, "Restore decision guard");
        AutomationProperties.SetHelpText(
            RestoreContinuityDecisionText,
            "Chummer will not replace local work automatically; review the restore, stale-state, and conflict-choice posture first.");
        AutomationProperties.SetName(RestoreContinuityDecisionOrderText, "Restore decision order");
        AutomationProperties.SetHelpText(
            RestoreContinuityDecisionOrderText,
            "Use the visible restore choices in order: keep local, save when available, review Campaign Workspace, then open support.");
        AutomationProperties.SetName(RestoreContinuityLocalAuthorityText, "Restore local authority");
        AutomationProperties.SetHelpText(
            RestoreContinuityLocalAuthorityText,
            "The primary desktop route keeps local work authoritative until the user chooses a review or support action.");
        AutomationProperties.SetName(RestoreContinuityReplacementGuardText, "Restore replacement guard");
        AutomationProperties.SetHelpText(
            RestoreContinuityReplacementGuardText,
            "There is no automatic or one-click restore replacement path on the primary desktop route.");
        AutomationProperties.SetName(RestoreContinuitySupportHandoffText, "Restore support handoff");
        AutomationProperties.SetHelpText(
            RestoreContinuitySupportHandoffText,
            "Workspace Support receives restore, stale-state, conflict-choice, and local-anchor context before replacement.");
        AutomationProperties.SetName(KeepLocalWorkButton, "Keep local work");
        AutomationProperties.SetHelpText(KeepLocalWorkButton, "Keep the local workspace visible and skip restore replacement.");
        AutomationProperties.SetName(SaveLocalWorkButton, "Save local work before restore review");
        AutomationProperties.SetHelpText(SaveLocalWorkButton, "Save local work before restore or conflict review when a dirty local workspace is active.");
        AutomationProperties.SetName(ReviewCampaignWorkspaceButton, "Review campaign workspace restore choices");
        AutomationProperties.SetHelpText(ReviewCampaignWorkspaceButton, "Open campaign workspace continuity before accepting restore.");
        AutomationProperties.SetName(OpenWorkspaceSupportButton, "Open workspace support for restore conflict");
        AutomationProperties.SetHelpText(OpenWorkspaceSupportButton, "Open support with restore, stale-state, and conflict-choice context.");
        AutomationProperties.SetName(RestoreContinuityActionStatusText, "Restore decision action status");
    }

    private static string BuildRestoreContinuityDecisionSummary(bool canSaveLocalWorkBeforeRestore)
        => canSaveLocalWorkBeforeRestore
            ? $"{PrimaryDesktopRouteStatus} Decision gate: Chummer will not replace local work automatically; save local work before reviewing a server restore."
            : $"{PrimaryDesktopRouteStatus} Decision gate: Chummer will not replace local work automatically; keep local work, review Campaign Workspace, or open Workspace Support.";

    private void ClearRestoreDecisionSelection()
    {
        KeepLocalWorkButton.Classes.Remove("selected");
        SaveLocalWorkButton.Classes.Remove("selected");
        ReviewCampaignWorkspaceButton.Classes.Remove("selected");
        OpenWorkspaceSupportButton.Classes.Remove("selected");
    }

    private void MarkRestoreDecisionSelected(Button selectedButton)
    {
        ClearRestoreDecisionSelection();
        selectedButton.Classes.Add("selected");
    }

    private void KeepLocalWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = KeepLocalWorkStatus;
        MarkRestoreDecisionSelected(KeepLocalWorkButton);
        KeepLocalWorkRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveLocalWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!SaveLocalWorkButton.IsEnabled)
        {
            RestoreContinuityActionStatusText.Text = SaveUnavailableStatus;
            ClearRestoreDecisionSelection();
            return;
        }

        RestoreContinuityActionStatusText.Text = SaveLocalWorkRequestedStatus;
        MarkRestoreDecisionSelected(SaveLocalWorkButton);
        SaveLocalWorkRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReviewCampaignWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = CampaignWorkspaceRequestedStatus;
        MarkRestoreDecisionSelected(ReviewCampaignWorkspaceButton);
        CampaignWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenWorkspaceSupportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = WorkspaceSupportRequestedStatus;
        MarkRestoreDecisionSelected(OpenWorkspaceSupportButton);
        WorkspaceSupportRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record SummaryHeaderState(
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    string? RuntimeSummary = null,
    string? RestoreContinuitySummary = null,
    string? StaleStateSummary = null,
    string? ConflictChoiceSummary = null,
    bool CanSaveLocalWorkBeforeRestore = false);
