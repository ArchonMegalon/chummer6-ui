using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Chummer.Contracts.Presentation;
using System.Linq;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    private const string KeepLocalWorkSelectionId = "restore-decision-keep-local-work";
    private const string SaveLocalWorkSelectionId = "restore-decision-save-local-work";
    private const string ReviewCampaignWorkspaceSelectionId = "restore-decision-review-campaign-workspace";
    private const string OpenWorkspaceSupportSelectionId = "restore-decision-open-workspace-support";
    private const string DecisionOrderSummary = "Decision order: 1. keep local work visible, 2. save local work when available, 3. review Campaign Workspace, 4. open Workspace Support before accepting restore replacement.";
    private const string LocalAuthoritySummary = "Local authority: the desktop workspace remains the working copy until you choose Campaign Workspace review or Workspace Support; restore review never replaces local work by itself.";
    private const string ReplacementGuardSummary = "Restore replacement guard: there is no one-click accept; Campaign Workspace review or Workspace Support must be opened before a server restore can replace local desktop work.";
    private const string SupportHandoffSummary = "Support handoff: Workspace Support carries restore continuation, stale-state visibility, conflict choices, and the current local workspace anchor before any replacement.";
    private const string SaveAvailableDecisionSummary = "Review restore before replacing local work. Save first if needed.";
    private const string SaveUnavailableDecisionSummary = "Review restore before replacing local work. Keep local work or open support.";
    private const string SaveAvailableStatus = "Save local work is available before restore or conflict review changes the desktop state.";
    private const string SaveUnavailableStatus = "Save local work is unavailable because no dirty local workspace is active; keep local work, review Campaign Workspace, or open Workspace Support.";
    private const string KeepLocalStatus = "Kept local work visible; no restore, stale-state refresh, or conflict choice replaced desktop state.";
    private const string SaveRequestedStatus = "Save local work requested before any restore or conflict review changes desktop state.";
    private const string SavedLocalWorkStatus = "Local work saved before restore review; keep local work visible, review Campaign Workspace, or open Workspace Support before any replacement.";
    private const string ReviewCampaignWorkspaceStatus = "Opening Campaign Workspace to review restore continuation, stale state, and conflict choices before replacing local work.";
    private const string OpenWorkspaceSupportStatus = "Opening Workspace Support with restore continuation, stale-state, and conflict-choice context.";

    private readonly Dictionary<string, Button> _navigationTabButtons = new(StringComparer.Ordinal);
    private WorkspaceStripState _workspaceStripState = new("Workspace: none");
    private SummaryHeaderState _state = new(
        NavigationTabsHeading: string.Empty,
        NavigationTabs: [],
        ActiveTabId: null);

    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler? LoadDemoRunnerRequested;
    public event EventHandler? KeepLocalWorkRequested;
    public event EventHandler? SaveLocalWorkRequested;
    public event EventHandler? CampaignWorkspaceRequested;
    public event EventHandler? WorkspaceSupportRequested;

    public SummaryHeaderControl()
    {
        InitializeComponent();
        WorkspaceStripControl.LoadDemoRunnerRequested += WorkspaceStripControl_OnLoadDemoRunnerRequested;
        BuildRestoreActionButtons();
        ApplyAutomationProperties();
        SetWorkspaceStripState(_workspaceStripState);
        SetState(_state);
    }

    public void SetWorkspaceStripState(WorkspaceStripState state)
    {
        _workspaceStripState = state;
        WorkspaceStripControl.SetState(state);
        UpdateVisibility();
    }

    public void SetNavigationTabs(
        string navigationTabsHeading,
        NavigatorTabItem[] navigationTabs,
        string? activeTabId)
    {
        SetState(_state with
        {
            NavigationTabsHeading = navigationTabsHeading,
            NavigationTabs = navigationTabs,
            ActiveTabId = activeTabId
        });
    }

    public void SetState(SummaryHeaderState state)
    {
        _state = state;
        SetNavigationTabsInternal(state.NavigationTabsHeading, state.NavigationTabs, state.ActiveTabId);
        bool hasRecoveryContext = (
                !string.IsNullOrWhiteSpace(state.RestoreContinuitySummary)
                || !string.IsNullOrWhiteSpace(state.StaleStateSummary)
                || !string.IsNullOrWhiteSpace(state.ConflictChoiceSummary))
            && (
                state.CanSaveLocalWorkBeforeRestore
                || string.IsNullOrWhiteSpace(state.RestoreDecisionWorkspaceId)
                || !string.IsNullOrWhiteSpace(state.RestoreDecisionActionStatus)
                || !string.IsNullOrWhiteSpace(state.RestoreDecisionSelectionId));
        SetRestoreContinuityStatus(state, hasRecoveryContext);
        UpdateVisibility();
    }

    private void SetNavigationTabsInternal(
        string navigationTabsHeading,
        NavigatorTabItem[] navigationTabs,
        string? activeTabId)
    {
        NavigationTabsHeadingText.Text = navigationTabsHeading;
        NavigationTabsButtonsPanel.Children.Clear();
        _navigationTabButtons.Clear();

        foreach (NavigatorTabItem tab in navigationTabs.Where(static tab => tab.Enabled))
        {
            Button button = new()
            {
                Content = tab.Label,
                Tag = tab.Id,
                Margin = new Thickness(0d, 0d, 8d, 0d),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            button.Click += NavigationTabButton_OnClick;
            ToolTip.SetTip(button, tab.SectionId);
            if (string.Equals(tab.Id, activeTabId, StringComparison.Ordinal))
            {
                button.Classes.Add("selected");
            }

            _navigationTabButtons[tab.Id] = button;
            NavigationTabsButtonsPanel.Children.Add(button);
        }

        NavigationTabsPanel.IsVisible = NavigationTabsButtonsPanel.Children.Count > 0;
    }

    private void SetRestoreContinuityStatus(SummaryHeaderState state, bool hasRecoveryContext)
    {
        RestoreContinuityStatusText.Text = state.RestoreContinuitySummary ?? string.Empty;
        StaleStateStatusText.Text = state.StaleStateSummary ?? string.Empty;
        ConflictChoiceStatusText.Text = state.ConflictChoiceSummary ?? string.Empty;
        ToolTip.SetTip(
            RestoreContinuityStatusBorder,
            string.Join(
                "\n",
                new[]
                {
                    state.RestoreContinuitySummary,
                    state.StaleStateSummary,
                    state.ConflictChoiceSummary
                }.Where(static text => !string.IsNullOrWhiteSpace(text))));
        RestoreContinuityStatusBorder.IsVisible = false;
        RestoreContinuityStatusBorder.IsVisible = hasRecoveryContext;

        bool showActionPanel = hasRecoveryContext;
        RestoreContinuityActionPanel.IsVisible = showActionPanel;
        if (!showActionPanel)
        {
            KeepLocalWorkButton.Tag = null;
            SaveLocalWorkButton.Tag = null;
            ReviewCampaignWorkspaceButton.Tag = null;
            OpenWorkspaceSupportButton.Tag = null;
            RestoreContinuityActionStatusText.Text = string.Empty;
            ClearRestoreActionSelection();
            return;
        }

        KeepLocalWorkButton.Tag = KeepLocalWorkSelectionId;
        SaveLocalWorkButton.Tag = SaveLocalWorkSelectionId;
        ReviewCampaignWorkspaceButton.Tag = ReviewCampaignWorkspaceSelectionId;
        OpenWorkspaceSupportButton.Tag = OpenWorkspaceSupportSelectionId;
        SaveLocalWorkButton.IsEnabled = state.CanSaveLocalWorkBeforeRestore;
        RestoreContinuityDecisionText.Text = BuildRestoreContinuityDecisionSummary(state.CanSaveLocalWorkBeforeRestore);
        RestoreContinuityDecisionOrderText.Text = DecisionOrderSummary;
        RestoreContinuityLocalAuthorityText.Text = LocalAuthoritySummary;
        RestoreContinuityReplacementGuardText.Text = ReplacementGuardSummary;
        RestoreContinuitySupportHandoffText.Text = SupportHandoffSummary;
        RestoreContinuityActionStatusText.Text = state.RestoreDecisionActionStatus
            ?? (state.CanSaveLocalWorkBeforeRestore ? SaveAvailableStatus : SaveUnavailableStatus);
        ToolTip.SetTip(
            RestoreContinuityActionPanel,
            string.Join(
                "\n",
                new[]
                {
                    RestoreContinuityDecisionText.Text,
                    RestoreContinuityDecisionOrderText.Text,
                    RestoreContinuityLocalAuthorityText.Text,
                    RestoreContinuityReplacementGuardText.Text,
                    RestoreContinuitySupportHandoffText.Text,
                    RestoreContinuityActionStatusText.Text
                }.Where(static text => !string.IsNullOrWhiteSpace(text))));
        ApplyRestoreActionSelection(state.RestoreDecisionSelectionId);
    }

    private static string BuildRestoreContinuityDecisionSummary(bool canSaveLocalWorkBeforeRestore)
        => canSaveLocalWorkBeforeRestore
            ? SaveAvailableDecisionSummary
            : SaveUnavailableDecisionSummary;

    private void BuildRestoreActionButtons()
    {
        ToolTip.SetTip(KeepLocalWorkButton, "Keep local work visible before any restore review changes desktop state.");
        ToolTip.SetTip(SaveLocalWorkButton, "Save local work before reviewing restore or conflict choices.");
        ToolTip.SetTip(ReviewCampaignWorkspaceButton, "Review Campaign Workspace restore choices before any replacement.");
        ToolTip.SetTip(OpenWorkspaceSupportButton, "Open support with restore, stale-state, and conflict-choice context.");
    }

    private void ApplyAutomationProperties()
    {
        AutomationProperties.SetName(RestoreContinuityStatusBorder, "Restore continuity decision gate");
        AutomationProperties.SetHelpText(RestoreContinuityStatusBorder, "Primary Avalonia desktop route keeps restore continuation, stale state, and conflict choices visible before replacement.");
        AutomationProperties.SetName(RestoreContinuityStatusText, "Restore continuation status");
        AutomationProperties.SetName(StaleStateStatusText, "Stale state visibility status");
        AutomationProperties.SetName(ConflictChoiceStatusText, "Conflict choice status");
        AutomationProperties.SetName(RestoreContinuityDecisionText, "Restore decision guard");
        AutomationProperties.SetHelpText(RestoreContinuityDecisionText, "Chummer will not replace local work automatically; review the restore, stale-state, and conflict-choice posture first.");
        AutomationProperties.SetName(RestoreContinuityDecisionOrderText, "Restore decision order");
        AutomationProperties.SetHelpText(RestoreContinuityDecisionOrderText, "Use the visible restore choices in order: keep local, save when available, review Campaign Workspace, then open support.");
        AutomationProperties.SetName(RestoreContinuityLocalAuthorityText, "Restore local authority");
        AutomationProperties.SetHelpText(RestoreContinuityLocalAuthorityText, "The primary desktop route keeps local work authoritative until the user chooses a review or support action.");
        AutomationProperties.SetName(RestoreContinuityReplacementGuardText, "Restore replacement guard");
        AutomationProperties.SetHelpText(RestoreContinuityReplacementGuardText, "There is no automatic or one-click restore replacement path on the primary desktop route.");
        AutomationProperties.SetName(RestoreContinuitySupportHandoffText, "Restore support handoff");
        AutomationProperties.SetHelpText(RestoreContinuitySupportHandoffText, "Workspace Support receives restore, stale-state, conflict-choice, and local-anchor context before replacement.");
        AutomationProperties.SetName(KeepLocalWorkButton, "Keep local work");
        AutomationProperties.SetName(SaveLocalWorkButton, "Save local work before restore review");
        AutomationProperties.SetName(ReviewCampaignWorkspaceButton, "Review campaign workspace restore choices");
        AutomationProperties.SetName(OpenWorkspaceSupportButton, "Open workspace support for restore conflict");
        AutomationProperties.SetHelpText(OpenWorkspaceSupportButton, "Open support with restore, stale-state, and conflict-choice context.");
        AutomationProperties.SetName(RestoreContinuityActionStatusText, "Restore decision action status");
    }

    private void ApplyRestoreActionSelection(string? selectionId)
    {
        ClearRestoreActionSelection();
        Button? selectedButton = selectionId switch
        {
            KeepLocalWorkSelectionId => KeepLocalWorkButton,
            SaveLocalWorkSelectionId => SaveLocalWorkButton,
            ReviewCampaignWorkspaceSelectionId => ReviewCampaignWorkspaceButton,
            OpenWorkspaceSupportSelectionId => OpenWorkspaceSupportButton,
            _ => null
        };

        selectedButton?.Classes.Add("selected");
    }

    private void ClearRestoreActionSelection()
    {
        KeepLocalWorkButton.Classes.Remove("selected");
        SaveLocalWorkButton.Classes.Remove("selected");
        ReviewCampaignWorkspaceButton.Classes.Remove("selected");
        OpenWorkspaceSupportButton.Classes.Remove("selected");
    }

    private void NavigationTabButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tabId)
        {
            NavigationTabSelected?.Invoke(this, tabId);
        }
    }

    private void KeepLocalWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = KeepLocalStatus;
        ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
        ApplyRestoreActionSelection(KeepLocalWorkSelectionId);
        KeepLocalWorkRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveLocalWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!SaveLocalWorkButton.IsEnabled)
        {
            RestoreContinuityActionStatusText.Text = SaveUnavailableStatus;
            ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
            return;
        }

        RestoreContinuityActionStatusText.Text = SaveRequestedStatus;
        ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
        ApplyRestoreActionSelection(SaveLocalWorkSelectionId);
        SaveLocalWorkRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReviewCampaignWorkspaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = ReviewCampaignWorkspaceStatus;
        ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
        ApplyRestoreActionSelection(ReviewCampaignWorkspaceSelectionId);
        CampaignWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenWorkspaceSupportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RestoreContinuityActionStatusText.Text = OpenWorkspaceSupportStatus;
        ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
        ApplyRestoreActionSelection(OpenWorkspaceSupportSelectionId);
        WorkspaceSupportRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplySavedLocalWorkState()
    {
        RestoreContinuityActionStatusText.Text = SavedLocalWorkStatus;
        ToolTip.SetTip(RestoreContinuityActionPanel, RestoreContinuityActionStatusText.Text);
        ClearRestoreActionSelection();
    }

    private void WorkspaceStripControl_OnLoadDemoRunnerRequested(object? sender, EventArgs e)
        => LoadDemoRunnerRequested?.Invoke(this, EventArgs.Empty);

    private void UpdateVisibility()
    {
        SummaryHeaderState state = _state;
        bool showNavigation = state.HasVisibleContent || NavigationTabsPanel.IsVisible;
        bool showRestore = RestoreContinuityStatusBorder.IsVisible || RestoreContinuityActionPanel.IsVisible;
        bool showWorkspaceContext = WorkspaceStripControl.IsVisible;
        IsVisible = showNavigation || showRestore || showWorkspaceContext;
        Height = IsVisible ? double.NaN : 0d;
        RootBorder.IsVisible = IsVisible;
    }
}

public sealed record SummaryHeaderState(
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    bool HasVisibleContent = false,
    string? RuntimeSummary = null,
    string? RestoreContinuitySummary = null,
    string? StaleStateSummary = null,
    string? ConflictChoiceSummary = null,
    bool CanSaveLocalWorkBeforeRestore = false,
    string? RestoreDecisionWorkspaceId = null,
    string? RestoreDecisionActionStatus = null,
    string? RestoreDecisionSelectionId = null);
