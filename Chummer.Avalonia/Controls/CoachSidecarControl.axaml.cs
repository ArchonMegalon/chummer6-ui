using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.Avalonia.Controls;

public partial class CoachSidecarControl : UserControl
{
    public event EventHandler? CopyLaunchRequested;

    public CoachSidecarControl()
    {
        InitializeComponent();
    }

    public void SetState(CoachSidecarPaneState state)
    {
        CoachStatusText.Text = $"Status: {state.Status}";
        CoachPromptPolicyText.Text = $"Prompt Policy: {state.PromptPolicy}";
        CoachBudgetText.Text = $"Coach Budget: {state.BudgetSummary}";
        CoachWorkspaceText.Text = $"Workspace: {state.WorkspaceId}";
        CoachRuntimeText.Text = $"Runtime: {state.RuntimeFingerprint}";
        CoachLaunchUriTextBox.Text = state.LaunchUri;
        CoachLaunchStatusText.Text = string.IsNullOrWhiteSpace(state.LaunchStatusMessage)
            ? "Copy the scoped /coach link to continue in the dedicated Coach head."
            : state.LaunchStatusMessage;
        CopyCoachLaunchButton.IsEnabled = !string.IsNullOrWhiteSpace(state.LaunchUri)
            && !string.Equals(state.LaunchUri, "n/a", StringComparison.Ordinal);
        CoachErrorText.Text = string.IsNullOrWhiteSpace(state.ErrorMessage)
            ? "(none)"
            : $"Error: {state.ErrorMessage}";
        ProviderHealthList.ItemsSource = state.Providers;
        AuditList.ItemsSource = state.Audits;
    }

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
        PromptPolicy: "n/a",
        BudgetSummary: "n/a",
        WorkspaceId: "n/a",
        RuntimeFingerprint: "n/a",
        LaunchUri: "n/a",
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
    string LastFailure)
{
    public override string ToString()
        => $"{DisplayName} [{CircuitState}] · {ProviderId} · {AdapterKind} · transport {TransportSummary} · keys {CredentialSummary} · {BindingSummary} · success {LastSuccess} · failure {LastFailure}";
}

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
    string Updated)
{
    public override string ToString()
        => $"{ConversationId} · {RuntimeFingerprint}"
            + (string.Equals(LaunchUri, "n/a", StringComparison.Ordinal) ? string.Empty : $" · launch {LaunchUri}")
            + $" · {CacheStatus} · {Summary}"
            + (string.IsNullOrWhiteSpace(FlavorLine) ? string.Empty : $" · flavor {FlavorLine}")
            + (string.Equals(BudgetSummary, "n/a", StringComparison.Ordinal) ? string.Empty : $" · budget {BudgetSummary}")
            + (string.Equals(StructuredSummary, "none", StringComparison.Ordinal) ? string.Empty : $" · structured {StructuredSummary}")
            + (string.Equals(RecommendationSummary, "none", StringComparison.Ordinal) ? string.Empty : $" · recommendations {RecommendationSummary}")
            + (string.Equals(EvidenceSummary, "none", StringComparison.Ordinal) ? string.Empty : $" · evidence {EvidenceSummary}")
            + (string.Equals(RiskSummary, "none", StringComparison.Ordinal) ? string.Empty : $" · risks {RiskSummary}")
            + (string.Equals(SourceSummary, "0 sources / 0 action drafts", StringComparison.Ordinal) ? string.Empty : $" · sources {SourceSummary}")
            + $" · {RouteDecision} · {Coverage} · updated {Updated}";
}
