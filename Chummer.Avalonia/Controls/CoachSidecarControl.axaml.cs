using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.Avalonia.Controls;

public partial class CoachSidecarControl : UserControl
{
    public event EventHandler? OpenLaunchRequested;
    public event EventHandler? CopyLaunchRequested;

    public CoachSidecarControl()
    {
        InitializeComponent();
    }

    public void SetState(CoachSidecarPaneState state)
    {
        CoachLaunchStatusText.Text = string.IsNullOrWhiteSpace(state.LaunchStatusMessage)
            ? "Open Coach in your browser, or copy the link if you need to hand it off."
            : state.LaunchStatusMessage;
        OpenCoachLaunchButton.IsEnabled = !string.IsNullOrWhiteSpace(state.LaunchUri);
        CopyCoachLaunchButton.IsEnabled = !string.IsNullOrWhiteSpace(state.LaunchUri);
        CoachErrorText.Text = string.IsNullOrWhiteSpace(state.ErrorMessage)
            ? string.Empty
            : $"Error: {state.ErrorMessage}";
        ToolTip.SetTip(
            this,
            string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Status: {state.Status}",
                    $"Prompt policy: {state.PromptPolicy}",
                    $"Budget: {state.BudgetSummary}",
                    $"Workspace: {state.WorkspaceId}",
                    $"Runtime: {state.RuntimeFingerprint}",
                    state.LaunchUri,
                    CoachLaunchStatusText.Text,
                    CoachErrorText.Text
                }.Where(static line => !string.IsNullOrWhiteSpace(line))));
    }

    private void OpenCoachLaunchButton_OnClick(object? sender, RoutedEventArgs e)
        => OpenLaunchRequested?.Invoke(this, EventArgs.Empty);

    private void CopyCoachLaunchButton_OnClick(object? sender, RoutedEventArgs e)
        => CopyLaunchRequested?.Invoke(this, EventArgs.Empty);
}

public sealed record CoachSidecarPaneState(
    string Status,
    string PromptPolicy,
    string BudgetSummary,
    string WorkspaceId,
    string RuntimeFingerprint,
    string LaunchUri,
    string? LaunchStatusMessage,
    string? ErrorMessage,
    CoachProviderDisplayItem[] Providers,
    CoachAuditDisplayItem[] Audits)
{
    public static CoachSidecarPaneState Empty { get; } = new(
        Status: "unloaded",
        PromptPolicy: "Policy not loaded yet",
        BudgetSummary: "Budget not loaded yet",
        WorkspaceId: "No workspace attached",
        RuntimeFingerprint: "No runtime fingerprint yet",
        LaunchUri: string.Empty,
        LaunchStatusMessage: null,
        ErrorMessage: null,
        Providers: [],
        Audits: []);
}

public sealed record CoachProviderDisplayItem(
    string DisplayName,
    string ProviderId,
    string AdapterKind,
    string CircuitState,
    string TransportSummary,
    string CredentialSummary,
    string BindingSummary,
    string LastSuccess,
    string LastFailure);

public sealed record CoachAuditDisplayItem(
    string ConversationId,
    string RuntimeFingerprint,
    string LaunchUri,
    string Summary,
    string? FlavorLine,
    string BudgetSummary,
    string StructuredSummary,
    string RecommendationSummary,
    string EvidenceSummary,
    string RiskSummary,
    string SourceSummary,
    string CacheStatus,
    string RouteDecision,
    string Coverage,
    string Updated);
