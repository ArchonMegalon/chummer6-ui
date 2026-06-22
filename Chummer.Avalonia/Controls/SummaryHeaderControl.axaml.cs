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
    private const string DecisionOrderSummary = "Open Campaign Workspace or get workspace help when you need continuity help.";
    private const string LocalAuthoritySummary = "The desktop workspace remains the working copy until you choose Campaign Workspace review or workspace help.";
    private const string ReplacementGuardSummary = "Workspace changes stay explicit.";
    private const string SupportHandoffSummary = "Workspace help opens with the current local workspace context.";
    private const string SaveAvailableDecisionSummary = "Save first if needed before changing this desktop copy.";
    private const string SaveUnavailableDecisionSummary = "Keep local work or open support before changing this desktop copy.";
    private const string SaveAvailableStatus = "Save local work is available before changing this desktop copy.";
    private const string SaveUnavailableStatus = "Save local work is unavailable because no dirty local workspace is active.";
    private const string KeepLocalStatus = "Keep Local leaves this desktop copy in place.";
    private const string SaveRequestedStatus = "Saving local work before changing this desktop copy.";
    private const string SavedLocalWorkStatus = "Local work saved. Keep Local, review Campaign Workspace, or get workspace help before replacing anything.";
    private const string ReviewCampaignWorkspaceStatus = "Opening Campaign Workspace.";
    private const string OpenWorkspaceSupportStatus = "Opening workspace help with the current workspace context attached.";

    private readonly Dictionary<string, Button> _navigationTabButtons = new(StringComparer.Ordinal);
    private WorkspaceStripState _workspaceStripState = new("No character open");
    private SummaryHeaderState _state = new(
        NavigationTabsHeading: string.Empty,
        NavigationTabs: [],
        ActiveTabId: null);

    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler? LoadDemoRunnerRequested;
    public event EventHandler? StartOriginRequested;
    public event EventHandler? KeepLocalWorkRequested;
    public event EventHandler? SaveLocalWorkRequested;
    public event EventHandler? CampaignWorkspaceRequested;
    public event EventHandler? WorkspaceSupportRequested;

    public SummaryHeaderControl()
    {
        InitializeComponent();
        WorkspaceStripControl.LoadDemoRunnerRequested += WorkspaceStripControl_OnLoadDemoRunnerRequested;
        WorkspaceStripControl.StartOriginRequested += WorkspaceStripControl_OnStartOriginRequested;
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
        ToolTip.SetTip(KeepLocalWorkButton, "Keep Local keeps this desktop copy visible.");
        ToolTip.SetTip(SaveLocalWorkButton, "Save local work before changing this desktop copy.");
        ToolTip.SetTip(ReviewCampaignWorkspaceButton, "Open Campaign Workspace.");
        ToolTip.SetTip(OpenWorkspaceSupportButton, "Get workspace help with the current workspace context attached.");
    }

    private void ApplyAutomationProperties()
    {
        AutomationProperties.SetName(RestoreContinuityStatusBorder, "Workspace continuity status");
        AutomationProperties.SetHelpText(RestoreContinuityStatusBorder, "The desktop app keeps local work under explicit user control.");
        AutomationProperties.SetName(RestoreContinuityStatusText, "Restore continuation status");
        AutomationProperties.SetName(StaleStateStatusText, "Stale state visibility status");
        AutomationProperties.SetName(ConflictChoiceStatusText, "Workspace review status");
        AutomationProperties.SetName(RestoreContinuityDecisionText, "Workspace decision guard");
        AutomationProperties.SetHelpText(RestoreContinuityDecisionText, "Chummer keeps local work under explicit user control.");
        AutomationProperties.SetName(RestoreContinuityDecisionOrderText, "Workspace decision order");
        AutomationProperties.SetHelpText(RestoreContinuityDecisionOrderText, "Use the visible choices in order: keep local work visible, save local work when available, review Campaign Workspace, then get workspace help.");
        AutomationProperties.SetName(RestoreContinuityLocalAuthorityText, "Workspace local authority");
        AutomationProperties.SetHelpText(RestoreContinuityLocalAuthorityText, "Your local desktop copy stays authoritative until you choose Campaign Workspace review or workspace help.");
        AutomationProperties.SetName(RestoreContinuityReplacementGuardText, "Workspace change guard");
        AutomationProperties.SetHelpText(RestoreContinuityReplacementGuardText, "There is no automatic or one-click replacement from this desktop route.");
        AutomationProperties.SetName(RestoreContinuitySupportHandoffText, "Workspace support handoff");
        AutomationProperties.SetHelpText(RestoreContinuitySupportHandoffText, "Workspace help opens with the current local workspace context.");
        AutomationProperties.SetName(KeepLocalWorkButton, "Keep Local");
        AutomationProperties.SetName(SaveLocalWorkButton, "Save local work before workspace review");
        AutomationProperties.SetName(ReviewCampaignWorkspaceButton, "Review Campaign Workspace");
        AutomationProperties.SetName(OpenWorkspaceSupportButton, "Get workspace help");
        AutomationProperties.SetHelpText(OpenWorkspaceSupportButton, "Get workspace help with the current workspace context attached.");
        AutomationProperties.SetName(RestoreContinuityActionStatusText, "Workspace decision action status");
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

    private void WorkspaceStripControl_OnStartOriginRequested(object? sender, EventArgs e)
        => StartOriginRequested?.Invoke(this, EventArgs.Empty);

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
