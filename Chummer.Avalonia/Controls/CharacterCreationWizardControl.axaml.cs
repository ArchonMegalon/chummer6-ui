using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public sealed record CharacterCreationWizardStepRequest(string StepId);

public sealed record CharacterCreationWizardBuildGhostQuestion(
    string Question,
    CharacterCreationWizardBuildGhostContext Context);

public partial class CharacterCreationWizardControl : UserControl
{
    private CharacterCreationWizardDesktopState? _state;
    private bool _buildGhostPreferenceEnabled = true;
    private bool _buildGhostBusy;

    public CharacterCreationWizardControl()
    {
        InitializeComponent();
    }

    public event EventHandler<CharacterCreationWizardStepRequest>? StepRequested;
    public event EventHandler? ContinueRequested;
    public event EventHandler<CharacterCreationWizardBuildGhostQuestion>? BuildGhostQuestionSubmitted;
    public event EventHandler? RecoverCheckpointRequested;
    public event EventHandler? ExportCheckpointRequested;

    public void SetState(CharacterCreationWizardDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;

        CharacterCreationWizardDesktopStep active = state.Steps.Single(step => step.IsSelected);
        ActiveStepTitle.Text = active.Label;
        ResumeStatusText.Text = state.Resume.Restored
            ? "Navigation restored for this exact workspace revision."
            : state.Resume.InvalidationReason is { Length: > 0 } reason
                ? $"Saved navigation was invalidated: {reason}."
                : $"Workspace revision {state.WorkspaceRevision}.";
        AuthorityStatusText.Text = BuildAuthorityStatus(active);
        CompletionBlockerText.Text = state.CompletionBlockers.Count == 0
            ? "No completion blockers are currently projected."
            : $"Completion blockers: {string.Join(", ", state.CompletionBlockers)}";
        ContinueButton.IsEnabled = state.CanContinue;
        EditorLockText.IsVisible = !state.AdvancedEditorUnlocked;
        BuildGhostAuthorityText.Text = state.BuildGhostAvailable
            ? "Bound to the current runtime, workspace revision, and wizard snapshot."
            : "Build Ghost is pending authoritative runtime and wizard context.";

        RenderSteps(state.Steps);
        RenderBudgets(state.Budgets);
        RenderLegalOptions(state.LegalOptions);
        RefreshBuildGhostAvailability();
    }

    public void AppendBuildGhostAnswer(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        GhostConversation.Children.Add(new TextBlock
        {
            Text = $"{role}: {text.Trim()}",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Classes = { "shell-caption" }
        });
    }

    public void SetBuildGhostBusy(bool busy)
    {
        _buildGhostBusy = busy;
        RefreshBuildGhostAvailability();
    }

    public void SetBuildGhostEnabled(bool enabled)
    {
        _buildGhostPreferenceEnabled = enabled;
        RefreshBuildGhostAvailability();
        if (!enabled)
            GhostQuestionTextBox.Text = string.Empty;
    }

    private void RefreshBuildGhostAvailability()
    {
        bool canSend = _state is not null
                       && CharacterCreationWizardBuildGhostPolicy.CanSend(
                           _state,
                           _buildGhostPreferenceEnabled)
                       && !_buildGhostBusy;
        AskGhostButton.IsEnabled = canSend;
        GhostQuestionTextBox.IsEnabled = canSend;
    }

    public void FocusBuildGhostQuestion()
        => GhostQuestionTextBox.Focus();

    private void RenderSteps(IReadOnlyList<CharacterCreationWizardDesktopStep> steps)
    {
        StepList.Children.Clear();
        foreach (CharacterCreationWizardDesktopStep step in steps)
        {
            Button button = new()
            {
                Content = $"{step.Label}\n{step.Status}",
                Tag = step.StepId,
                IsEnabled = step.CanEnter && !step.IsSelected,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
            };
            button.Classes.Add("shell-action");
            if (!step.IsSelected)
                button.Classes.Add("quiet");
            AutomationProperties.SetAutomationId(button, $"creation-wizard-step-{step.StepId}");
            ToolTip.SetTip(button, BuildAuthorityStatus(step));
            button.Click += StepButton_OnClick;
            StepList.Children.Add(button);
        }
    }

    private void RenderBudgets(IReadOnlyList<CharacterCreationWizardDesktopBudget> budgets)
    {
        BudgetList.Children.Clear();
        if (budgets.Count == 0)
        {
            BudgetList.Children.Add(Caption("No authoritative budget is projected for this step."));
            return;
        }

        foreach (CharacterCreationWizardDesktopBudget budget in budgets)
        {
            string exactness = budget.IsExact ? "exact" : "authority pending";
            BudgetList.Children.Add(Caption(
                $"{budget.Label}: {budget.Remaining} / {budget.Total} {budget.Unit} remaining · {exactness}"));
        }
    }

    private void RenderLegalOptions(IReadOnlyList<CharacterCreationWizardDesktopOption> options)
    {
        LegalOptionList.Children.Clear();
        if (options.Count == 0)
        {
            LegalOptionList.Children.Add(Caption(
                "No authoritative choices are available for this step. Check the blockers rather than guessing."));
            return;
        }

        foreach (CharacterCreationWizardDesktopOption option in options)
        {
            string source = option.SourceId is { Length: > 0 }
                ? option.SourcePage is int page
                    ? $"{option.SourceId} p. {page}"
                    : option.SourceId
                : "source unavailable";
            string costs = option.Costs.Count == 0
                ? "No projected cost"
                : string.Join(", ", option.Costs.Select(cost => $"{cost.Delta} {cost.Unit}"));
            string prerequisite = option.IsEnabled
                ? "Legal now"
                : $"Blocked: {option.DisableReasonKey ?? "authority unavailable"}";
            Border card = new() { Classes = { "shell-card", "subtle" }, Padding = new global::Avalonia.Thickness(9) };
            card.Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = option.Label, Classes = { "shell-section-title" } },
                    Caption($"{prerequisite} · {costs}"),
                    Caption($"Source: {source}")
                }
            };
            AutomationProperties.SetAutomationId(card, $"creation-wizard-option-{option.OptionId}");
            LegalOptionList.Children.Add(card);
        }
    }

    private static TextBlock Caption(string text)
    {
        TextBlock block = new()
        {
            Text = text,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        };
        block.Classes.Add("shell-caption");
        return block;
    }

    private static string BuildAuthorityStatus(CharacterCreationWizardDesktopStep step)
    {
        List<string> status = [$"Status: {step.Status}"];
        if (step.Blockers.Count > 0)
            status.Add($"Blockers: {string.Join(", ", step.Blockers)}");
        if (step.Warnings.Count > 0)
            status.Add($"Warnings: {string.Join(", ", step.Warnings)}");
        return string.Join(" · ", status);
    }

    private void StepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stepId })
            StepRequested?.Invoke(this, new CharacterCreationWizardStepRequest(stepId));
    }

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
        => ContinueRequested?.Invoke(this, EventArgs.Empty);

    private void RecoverButton_OnClick(object? sender, RoutedEventArgs e)
        => RecoverCheckpointRequested?.Invoke(this, EventArgs.Empty);

    private void ExportButton_OnClick(object? sender, RoutedEventArgs e)
        => ExportCheckpointRequested?.Invoke(this, EventArgs.Empty);

    private void AskGhostButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state is null
            || !CharacterCreationWizardBuildGhostPolicy.CanSend(
                _state,
                _buildGhostPreferenceEnabled)
            || string.IsNullOrWhiteSpace(GhostQuestionTextBox.Text))
            return;

        string question = GhostQuestionTextBox.Text.Trim();
        GhostQuestionTextBox.Text = string.Empty;
        AppendBuildGhostAnswer("You", question);
        BuildGhostQuestionSubmitted?.Invoke(
            this,
            new CharacterCreationWizardBuildGhostQuestion(question, _state.BuildGhostContext));
    }

    private void WizardRoot_OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool commandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                               || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (commandModifier
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && e.Key == Key.G)
        {
            FocusBuildGhostQuestion();
            e.Handled = true;
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt) || _state is null)
            return;

        int selected = _state.Steps.ToList().FindIndex(static step => step.IsSelected);
        int delta = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (delta == 0)
            return;

        int candidate = selected + delta;
        while (candidate >= 0 && candidate < _state.Steps.Count)
        {
            CharacterCreationWizardDesktopStep target = _state.Steps[candidate];
            if (target.CanEnter)
            {
                StepRequested?.Invoke(this, new CharacterCreationWizardStepRequest(target.StepId));
                e.Handled = true;
                return;
            }

            candidate += delta;
        }
    }
}
