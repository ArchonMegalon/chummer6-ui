using Avalonia.Automation;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using System.Globalization;

namespace Chummer.Avalonia.Controls;

public sealed record CharacterCreationWizardStepRequest(string StepId);

public sealed record CharacterCreationWizardBuildGhostQuestion(
    string Question,
    CharacterCreationWizardBuildGhostContext Context);

public sealed record CharacterCreationContactPreviewRequested(
    CharacterCreationContactEditInput Input);

public sealed record CharacterCreationContactConfirmRequested(
    string PreviewDigest);

public sealed record CharacterCreationFoundationOptionClickRequested(
    CharacterCreationWizardDesktopOption Option);

public sealed record CharacterCreationFoundationPreviewApplied(
    string Outcome,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationFoundationConfirmRequested(
    string PreviewDigest);

public partial class CharacterCreationWizardControl : UserControl
{
    private static readonly string[] s_IdentityFieldIds =
    [
        CharacterCreationContactFieldIds.Name,
        CharacterCreationContactFieldIds.Role,
        CharacterCreationContactFieldIds.Location,
        CharacterCreationContactFieldIds.Notes,
        CharacterCreationContactFieldIds.CustomName,
        CharacterCreationContactFieldIds.Metatype,
        CharacterCreationContactFieldIds.Gender,
        CharacterCreationContactFieldIds.Age,
        CharacterCreationContactFieldIds.ContactType,
        CharacterCreationContactFieldIds.PreferredPayment,
        CharacterCreationContactFieldIds.HobbiesVice,
        CharacterCreationContactFieldIds.PersonalLife,
        CharacterCreationContactFieldIds.GroupName
    ];

    private CharacterCreationWizardDesktopState? _state;
    private readonly Dictionary<(Guid ContactId, string FieldId), Control> _contactEditors = [];
    private CharacterCreationContactPreparedPreview? _contactPreview;
    private string? _boundContactsSnapshotDigest;
    private string? _contactMutationStatus;
    private CharacterCreationFoundationPreparedPreview? _foundationPreview;
    private string? _foundationStatus;
    private bool _foundationMutationBusy;
    private bool _contactMutationBusy;
    private bool _finalizeBusy;
    private string? _finalizeStatus;
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
    public event EventHandler<CharacterCreationContactPreviewRequested>? ContactPreviewRequested;
    public event EventHandler<CharacterCreationContactConfirmRequested>? ContactConfirmRequested;
    public event EventHandler<CharacterCreationFoundationOptionClickRequested>? FoundationOptionClickRequested;

    /// <summary>Raised whenever the traveler adjusts a Priority rank selection.</summary>
    public sealed record CharacterCreationPrioritySelectionChanged(
        IReadOnlyDictionary<string, string> Selections);

    public event EventHandler<CharacterCreationPrioritySelectionChanged>? PrioritySelectionChanged;

    /// <summary>Raised when the traveler confirms the Priority assignment draft.</summary>
    public event EventHandler? PriorityConfirmRequested;

    private CharacterCreationPrerequisiteState? _prerequisiteState;
    private CharacterCreationWizardDesktopState? _lastWizardState;
    private readonly Dictionary<string, string> _prioritySelections = new(StringComparer.Ordinal);
    private readonly List<Button> _priorityRankButtons = [];
    private readonly List<Button> _priorityHeritageChoiceButtons = [];
    private readonly List<Button> _priorityTalentChoiceButtons = [];
    private string? _priorityHeritageSelectionId;
    private string? _priorityTalentSelectionId;
    private bool _priorityMutationBusy;

    /// <summary>True once the authoritative prerequisite draft is recorded.</summary>
    public bool IsPriorityConfirmed => _prerequisiteState?.PendingDraft is not null;

    /// <summary>
    /// Binds the authoritative prerequisite (Priority table) snapshot. A null
    /// state hides the surface; a confirmed draft renders read-only assignments.
    /// </summary>
    public void SetPrerequisiteState(CharacterCreationPrerequisiteState? state)
    {
        _prerequisiteState = state;
        _prioritySelections.Clear();
        _priorityHeritageSelectionId = null;
        _priorityTalentSelectionId = null;
        _priorityHeritageChoiceButtons.Clear();
        _priorityTalentChoiceButtons.Clear();
        LastPriorityMutationStatus = null;
        if (state?.PendingDraft is { } draft)
        {
            foreach (var assignment in draft.Assignments)
            {
                _prioritySelections[assignment.CategoryId] = assignment.Rank;
            }
        }

        RenderPrioritySurface();
    }

    /// <summary>Current per-category rank selections (category id to rank letter).</summary>
    public IReadOnlyDictionary<string, string> GetPrioritySelections()
        => new Dictionary<string, string>(_prioritySelections, StringComparer.Ordinal);

    /// <summary>Selected heritage choice for the draft, if any.</summary>
    public string? GetPriorityHeritageSelectionId() => _priorityHeritageSelectionId;

    /// <summary>Selected talent choice for the draft, if any.</summary>
    public string? GetPriorityTalentSelectionId() => _priorityTalentSelectionId;

    /// <summary>Heritage (metatype/metavariant) choices for the assigned heritage rank.</summary>
    public IReadOnlyList<(string SelectionId, string Label, bool IsEnabled)> GetPriorityHeritageChoices()
    {
        CharacterCreationPrerequisiteState? state = _prerequisiteState;
        if (state is null)
        {
            return [];
        }

        string? assignedRank = _prioritySelections.GetValueOrDefault(
            CharacterCreationPriorityCategoryIds.Heritage);
        if (string.IsNullOrEmpty(assignedRank))
        {
            return [];
        }

        CharacterCreationPriorityOptionProjection? option = state.Authority.Options
            .FirstOrDefault(item => string.Equals(item.CategoryId,
                CharacterCreationPriorityCategoryIds.Heritage, StringComparison.Ordinal)
                && string.Equals(item.Rank, assignedRank, StringComparison.Ordinal));
        if (option is null)
        {
            return [];
        }

        return option.HeritageOptions
            .Select(item => (item.SelectionId, BuildHeritageChoiceLabel(item), item.IsEnabled))
            .ToArray();
    }

    private static string BuildHeritageChoiceLabel(
        CharacterCreationPriorityHeritageOptionProjection item)
        => item.MetavariantName is { Length: > 0 } metavariant
            ? $"{item.MetatypeName} ({metavariant})"
            : item.MetatypeName;

    /// <summary>Talent choices for the assigned talent rank.</summary>
    public IReadOnlyList<(string SelectionId, string Label, bool IsEnabled)> GetPriorityTalentChoices()
    {
        CharacterCreationPrerequisiteState? state = _prerequisiteState;
        if (state is null)
        {
            return [];
        }

        string? assignedRank = _prioritySelections.GetValueOrDefault(
            CharacterCreationPriorityCategoryIds.Talent);
        if (string.IsNullOrEmpty(assignedRank))
        {
            return [];
        }

        CharacterCreationPriorityOptionProjection? option = state.Authority.Options
            .FirstOrDefault(item => string.Equals(item.CategoryId,
                CharacterCreationPriorityCategoryIds.Talent, StringComparison.Ordinal)
                && string.Equals(item.Rank, assignedRank, StringComparison.Ordinal));
        if (option is null)
        {
            return [];
        }

        return option.TalentOptions
            .Select(item => (item.SelectionId, BuildTalentChoiceLabel(item), item.IsEnabled))
            .ToArray();
    }

    private static string BuildTalentChoiceLabel(
        CharacterCreationPriorityTalentOptionProjection item)
        => item.Name;

    private void RenderPrioritySurface()
    {
        _priorityRankButtons.Clear();
        string? activeStepId = _lastWizardState?.Steps
            .FirstOrDefault(step => step.IsSelected)?.StepId;
        CharacterCreationPrerequisiteState? state = _prerequisiteState;
        bool show = string.Equals(activeStepId, "method", StringComparison.Ordinal) && state is not null;
        PriorityPanel.IsVisible = show;
        if (!show || state is null)
        {
            PriorityConfirmButton.IsEnabled = false;
            PriorityGridHost.Children.Clear();
            return;
        }

        PriorityGridHost.Children.Clear();
        if (state.PendingDraft is { } draft)
        {
            PriorityStatusText.Text = "Priority assignment confirmed for this workspace revision.";
            foreach (var assignment in draft.Assignments.OrderBy(a => a.Order))
            {
                PriorityGridHost.Children.Add(new TextBlock
                {
                    Text = assignment.CategoryId + ": rank " + assignment.Rank,
                });
            }

            PriorityConfirmButton.IsVisible = false;
            PriorityConfirmButton.IsEnabled = false;
            return;
        }

        PriorityConfirmButton.IsVisible = true;
        if (!state.Authority.IsAuthoritative)
        {
            PriorityStatusText.Text = "Priority authority is unavailable: "
                + string.Join(", ", state.Authority.Blockers);
            PriorityConfirmButton.IsEnabled = false;
            return;
        }

        PriorityStatusText.Text = "Assign exactly one priority rank per category; each rank can be used only once.";
        foreach (string category in CharacterCreationPriorityCategoryIds.Ordered)
        {
            List<CharacterCreationPriorityOptionProjection> options = state.Authority.Options
                .Where(o => string.Equals(o.CategoryId, category, StringComparison.Ordinal))
                .OrderBy(o => o.Rank, StringComparer.Ordinal)
                .ToList();
            if (options.Count == 0)
            {
                continue;
            }

            var rowPanel = new StackPanel { Spacing = 4 };
            rowPanel.Children.Add(new TextBlock { Text = options[0].CategoryName });
            var ranksPanel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
            };
            foreach (var option in options)
            {
                var rankButton = new Button
                {
                    Content = option.Rank + " - " + option.Label,
                    Tag = option,
                };
                AutomationProperties.SetAutomationId(
                    rankButton,
                    "creation-wizard-priority-" + category + "-" + option.Rank);
                rankButton.Click += PriorityRankButton_OnClick;
                _priorityRankButtons.Add(rankButton);
                ranksPanel.Children.Add(rankButton);
            }

            rowPanel.Children.Add(ranksPanel);
            PriorityGridHost.Children.Add(rowPanel);
        }

        RenderPriorityHeritageChoices();
        RenderPriorityTalentChoices();
        RefreshPrioritySelectionVisuals();
    }

    private void RenderPriorityHeritageChoices()
    {
        _priorityHeritageChoiceButtons.Clear();
        PriorityHeritageChoicesHost.Children.Clear();
        bool show = _prerequisiteState is { PendingDraft: null }
            && _prerequisiteState.Authority.IsAuthoritative
            && _prioritySelections.ContainsKey(CharacterCreationPriorityCategoryIds.Heritage);
        PriorityHeritageChoicesSection.IsVisible = show;
        if (!show)
        {
            return;
        }

        PriorityHeritageChoicesStatus.Text = "Select the metatype for heritage rank "
            + _prioritySelections.GetValueOrDefault(CharacterCreationPriorityCategoryIds.Heritage) + ".";
        foreach ((string selectionId, string label, bool isEnabled) in GetPriorityHeritageChoices())
        {
            var choiceButton = new Button { Content = label, IsEnabled = isEnabled, Tag = selectionId };
            AutomationProperties.SetAutomationId(
                choiceButton,
                "creation-wizard-priority-heritage-choice-" + selectionId);
            choiceButton.Click += HeritageChoiceButton_OnClick;
            _priorityHeritageChoiceButtons.Add(choiceButton);
            PriorityHeritageChoicesHost.Children.Add(choiceButton);
        }
    }

    private void RenderPriorityTalentChoices()
    {
        _priorityTalentChoiceButtons.Clear();
        PriorityTalentChoicesHost.Children.Clear();
        bool show = _prerequisiteState is { PendingDraft: null }
            && _prerequisiteState.Authority.IsAuthoritative
            && _prioritySelections.ContainsKey(CharacterCreationPriorityCategoryIds.Talent);
        PriorityTalentChoicesSection.IsVisible = show;
        if (!show)
        {
            return;
        }

        PriorityTalentChoicesStatus.Text = "Select the talent for talent rank "
            + _prioritySelections.GetValueOrDefault(CharacterCreationPriorityCategoryIds.Talent) + ".";
        foreach ((string selectionId, string label, bool isEnabled) in GetPriorityTalentChoices())
        {
            var choiceButton = new Button { Content = label, IsEnabled = isEnabled, Tag = selectionId };
            AutomationProperties.SetAutomationId(
                choiceButton,
                "creation-wizard-priority-talent-choice-" + selectionId);
            choiceButton.Click += TalentChoiceButton_OnClick;
            _priorityTalentChoiceButtons.Add(choiceButton);
            PriorityTalentChoicesHost.Children.Add(choiceButton);
        }
    }

    private void PriorityRankButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CharacterCreationPriorityOptionProjection option })
        {
            return;
        }

        if (string.Equals(
                _prioritySelections.GetValueOrDefault(option.CategoryId),
                option.Rank,
                StringComparison.Ordinal))
        {
            _prioritySelections.Remove(option.CategoryId);
        }
        else
        {
            _prioritySelections[option.CategoryId] = option.Rank;
        }

        RefreshPrioritySelectionVisuals();
        // Any rank change invalidates a previously picked heritage/talent
        // choice and must re-render the choice pickers for the new ranks.
        _priorityHeritageSelectionId = null;
        _priorityTalentSelectionId = null;
        RenderPriorityHeritageChoices();
        RenderPriorityTalentChoices();
        PrioritySelectionChanged?.Invoke(
            this,
            new CharacterCreationPrioritySelectionChanged(GetPrioritySelections()));
    }

    private void HeritageChoiceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string selectionId })
        {
            return;
        }

        _priorityHeritageSelectionId = string.Equals(
            _priorityHeritageSelectionId,
            selectionId,
            StringComparison.Ordinal)
            ? null : selectionId;
        RefreshPrioritySelectionVisuals();
    }

    private void TalentChoiceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string selectionId })
        {
            return;
        }

        _priorityTalentSelectionId = string.Equals(
            _priorityTalentSelectionId,
        selectionId,
            StringComparison.Ordinal)
            ? null : selectionId;
        RefreshPrioritySelectionVisuals();
    }

    private void RefreshPrioritySelectionVisuals()
    {
        foreach (Button button in _priorityRankButtons)
        {
            if (button.Tag is not CharacterCreationPriorityOptionProjection option)
            {
                continue;
            }

            bool selected = string.Equals(
                _prioritySelections.GetValueOrDefault(option.CategoryId),
                option.Rank,
                StringComparison.Ordinal);
            if (selected)
            {
                button.Classes.Add("selected");
            }
            else
            {
                button.Classes.Remove("selected");
            }
        }

        bool allAssigned = CharacterCreationPriorityCategoryIds.Ordered
            .All(category => _prioritySelections.ContainsKey(category));
        PriorityConfirmButton.IsEnabled = allAssigned && !_priorityMutationBusy;

        foreach (Button heritageButton in _priorityHeritageChoiceButtons)
        {
            bool heritageChosen = heritageButton.Tag is string heritageId
                && string.Equals(heritageId, _priorityHeritageSelectionId, StringComparison.Ordinal);
            if (heritageChosen)
            {
                heritageButton.Classes.Add("selected");
            }
            else
            {
                heritageButton.Classes.Remove("selected");
            }
        }

        foreach (Button talentButton in _priorityTalentChoiceButtons)
        {
            bool talentChosen = talentButton.Tag is string talentId
                && string.Equals(talentId, _priorityTalentSelectionId, StringComparison.Ordinal);
            if (talentChosen)
            {
                talentButton.Classes.Add("selected");
            }
            else
            {
                talentButton.Classes.Remove("selected");
            }
        }
    }

    private void PriorityConfirmButton_OnClick(object? sender, RoutedEventArgs e)
        => PriorityConfirmRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Last diagnostic status reported by the Priority confirm flow;
    /// observable by automation to explain why an effect did not land.</summary>
    public string? LastPriorityMutationStatus { get; private set; }

    /// <summary>Records a diagnostic status for the Priority confirm flow.</summary>
    public void ReportPriorityMutationStatus(string status)
        => LastPriorityMutationStatus = status;

    /// <summary>Toggles the mutation lock applied while a Priority confirm is
    /// in flight; the confirm affordance re-enables through the busy flag.</summary>
    public void SetPriorityMutationBusy(bool busy)
    {
        _priorityMutationBusy = busy;
        RefreshPrioritySelectionVisuals();
    }
    public event EventHandler<CharacterCreationFoundationConfirmRequested>? FoundationConfirmRequested;
    public event EventHandler? FinalizeRequested;

    /// <summary>
    /// Projects the overview state's finalization read into the wizard surface.
    /// The wizard snapshot itself is navigation/read-only (Core authority); the
    /// finalize affordance is driven entirely by the overview's authoritative
    /// finalization projection.
    /// </summary>
    public void SetFinalizeAvailability(bool canReview, IReadOnlyList<string> blockers)
    {
        ArgumentNullException.ThrowIfNull(blockers);
        FinalizeButton.IsVisible = canReview && !_finalizeBusy;
        FinalizeButton.IsEnabled = canReview && !_finalizeBusy;
        FinalizeStatusText.Text = _finalizeStatus
            ?? (canReview
                ? "Whole-build review is available; finalizing applies the authoritative receipt and unlocks the advanced editor."
                : blockers.Count > 0
                    ? $"Finalization unavailable: {string.Join(", ", blockers)}"
                    : "Finalization is not available for this workspace revision.");
    }

    public void SetFinalizeResult(string outcome, IReadOnlyList<string> blockers)
    {
        ArgumentNullException.ThrowIfNull(blockers);
        _finalizeStatus = outcome is CharacterCreationFinalizationOutcomes.Applied
                or CharacterCreationFinalizationOutcomes.Replayed
            ? $"Character finalized ({outcome}); receipt digest anchors this exact workspace revision."
            : blockers.Count > 0
                ? $"Finalization failed ({outcome}): {string.Join(", ", blockers)}"
                : $"Finalization failed ({outcome}).";
        FinalizeStatusText.Text = _finalizeStatus;
        FinalizeButton.IsVisible = false;
        FinalizeButton.IsEnabled = false;
    }

    public void SetFinalizeMutationBusy(bool busy)
    {
        _finalizeBusy = busy;
        if (busy)
        {
            FinalizeButton.IsVisible = false;
            FinalizeButton.IsEnabled = false;
        }
        else
        {
            bool canReview = _state is not null;
            FinalizeButton.IsVisible = canReview;
            FinalizeButton.IsEnabled = canReview;
        }
    }

    /// <summary>Read-only automation projection of the current wizard snapshot.</summary>
    public CharacterCreationWizardDesktopState? StateForAutomation() => _state;

    /// <summary>True when the prepared foundation preview can be confirmed.</summary>
    public bool FoundationPreviewConfirmable => _foundationPreview is { CanConfirm: true };

    public void SetState(CharacterCreationWizardDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ContactsStep is { } contacts
            && !string.Equals(
                contacts.SnapshotDigest,
                _boundContactsSnapshotDigest,
                StringComparison.Ordinal))
        {
            _contactPreview = null;
            _contactMutationStatus = null;
            _boundContactsSnapshotDigest = contacts.SnapshotDigest;
        }
        _state = state;

        CharacterCreationWizardDesktopStep active = state.Steps.Single(step => step.IsSelected);
        ActiveStepTitle.Text = active.Label;
        int stepIndex = state.Steps.ToList().FindIndex(step => object.ReferenceEquals(step, active));
        WizardStepCounter.Text = stepIndex >= 0
            ? $"Step {stepIndex + 1} of {state.Steps.Count}: {active.Label}"
            : active.Label;
        BackButton.IsEnabled = FindPreviousEnterableStep(state) is not null;
        _lastWizardState = state;
        RenderPrioritySurface();
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
        RenderContacts(state.ContactsStep);
        RefreshBuildGhostAvailability();
    }

    private void FinalizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FinalizeRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetContactPrepareResult(CharacterCreationContactsInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactPreview = result.PreparedPreview;
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactConfirmResult(CharacterCreationContactsInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        if (result.Outcome is CharacterCreationContactOutcomes.Applied
            or CharacterCreationContactOutcomes.Replayed)
        {
            _contactPreview = null;
        }
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactReceiptLookupResult(
        CharacterCreationContactsInteractionReceiptLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null)
            _contactPreview = null;
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactMutationBusy(bool busy)
    {
        _contactMutationBusy = busy;
        RenderContacts(_state?.ContactsStep);
    }

    public void SetFoundationPrepareResult(CharacterCreationFoundationInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _foundationPreview = result.PreparedPreview;
        _foundationStatus = BuildFoundationResultStatus(result.Outcome, result.Blockers);
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetFoundationConfirmResult(CharacterCreationFoundationInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _foundationStatus = BuildFoundationResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null)
            _foundationPreview = null;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetFoundationMutationBusy(bool busy)
    {
        _foundationMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    private static string BuildFoundationResultStatus(string outcome, IReadOnlyList<string> blockers)
    {
        string suffix = blockers.Count == 0 ? string.Empty : $" (blocked: {string.Join(", ", blockers)})";
        return $"Foundation interaction: {outcome}{suffix}";
    }

    private void FoundationOptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CharacterCreationWizardDesktopOption option })
            FoundationOptionClickRequested?.Invoke(this, new CharacterCreationFoundationOptionClickRequested(option));
    }

    private void FoundationConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_foundationPreview is { } preview)
            FoundationConfirmRequested?.Invoke(this, new CharacterCreationFoundationConfirmRequested(preview.PreviewDigest));
    }

    private void RenderFoundationPreview(IReadOnlyList<CharacterCreationWizardDesktopOption> options)
    {
        if (_foundationPreview is not { } preview)
            return;

        StackPanel body = new() { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = "Review exact Core foundation preview",
            Classes = { "shell-section-title" }
        });
        body.Children.Add(Caption($"Metatype: {preview.RequestedMetatype}"));
        if (preview.Nationality is { } nationality)
            body.Children.Add(Caption($"Nationality: {nationality.Name}"));
        body.Children.Add(Caption(
            $"Life modules: {preview.LifeModuleBudgetBefore.Remaining} → {preview.LifeModuleBudgetAfter.Remaining} remaining"));
        foreach (CharacterCreationFoundationDiffEntry diff in preview.Diff)
            body.Children.Add(Caption($"{diff.DiffId} [{diff.Domain}]: {diff.BeforeValue ?? "-"} → {diff.AfterValue ?? "-"}"));
        if (preview.AuthorityBlockers.Count > 0)
            body.Children.Add(Caption($"Blocked: {string.Join(", ", preview.AuthorityBlockers)}"));
        if (!string.IsNullOrWhiteSpace(_foundationStatus))
            body.Children.Add(Caption(_foundationStatus));

        Button confirm = new()
        {
            Content = "Confirm and apply foundation",
            IsEnabled = preview.RequiresExplicitConfirmation
                        && preview.CanConfirm
                        && preview.AuthorityBlockers.Count == 0
                        && !_foundationMutationBusy,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        confirm.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(confirm, "creation-wizard-foundation-confirm");
        confirm.Click += FoundationConfirmButton_OnClick;
        body.Children.Add(confirm);

        Border card = new()
        {
            Classes = { "shell-card" },
            Padding = new global::Avalonia.Thickness(9),
            Child = body
        };
        AutomationProperties.SetAutomationId(card, "creation-wizard-foundation-preview");
        LegalOptionList.Children.Insert(0, card);
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
        LegalOptionList.IsVisible = _state?.ContactsStep is null;
        StepContentHeading.Text = _state?.ContactsStep is null
            ? "Legal choices and prerequisites"
            : "Contacts and field authority";
        if (!LegalOptionList.IsVisible)
            return;
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
            Button optionButton = new()
            {
                Tag = option,
                IsEnabled = option.IsEnabled,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = option.Label, Classes = { "shell-section-title" } },
                    Caption($"{prerequisite} · {costs}"),
                    Caption($"Source: {source}")
                }
            }
        };
            optionButton.Classes.Add("quiet");
            AutomationProperties.SetAutomationId(optionButton, $"creation-wizard-option-{option.OptionId}");
            optionButton.Click += FoundationOptionButton_OnClick;
            card.Child = optionButton;
            LegalOptionList.Children.Add(card);
        }

        RenderFoundationPreview(options);
    }

    private void RenderContacts(CharacterCreationWizardDesktopContactsStep? contacts)
    {
        ContactsList.Children.Clear();
        _contactEditors.Clear();
        ContactsList.IsVisible = contacts is not null;
        if (contacts is null)
            return;

        string authority = contacts.CanEdit
            ? "Core can preview this exact revision; every change still requires explicit confirmation."
            : "Editing is blocked by Core authority for this exact revision.";
        ContactsList.Children.Add(Caption(authority));
        if (contacts.Blockers.Count > 0)
            ContactsList.Children.Add(Caption($"Blockers: {string.Join(", ", contacts.Blockers)}"));
        if (!string.IsNullOrWhiteSpace(_contactMutationStatus))
            ContactsList.Children.Add(Caption(_contactMutationStatus));
        if (contacts.Contacts.Count == 0)
        {
            ContactsList.Children.Add(Caption(
                "No existing contact is projected. Add-contact and Lifestyle authority are not available in this slice."));
            return;
        }

        foreach (CharacterCreationWizardDesktopContact contact in contacts.Contacts)
        {
            string title = string.IsNullOrWhiteSpace(contact.Name)
                ? contact.ContactId.ToString("D")
                : contact.Name;
            StackPanel fields = new() { Spacing = 3 };
            fields.Children.Add(new TextBlock
            {
                Text = title,
                Classes = { "shell-section-title" }
            });
            fields.Children.Add(Caption(
                $"{contact.Role} · {contact.ContactPointCost} contact points"));
            foreach (CharacterCreationWizardDesktopContactField field in contact.Fields)
            {
                StackPanel row = new() { Spacing = 2 };
                row.Children.Add(Caption(field.IsEditable
                    ? field.Label
                    : $"{field.Label} · locked"));
                Control editor = BuildContactEditor(contact.ContactId, field);
                row.Children.Add(editor);
                fields.Children.Add(row);
            }

            Button preview = new()
            {
                Content = "Preview changes",
                Tag = contact.ContactId,
                IsEnabled = contacts.CanEdit && !_contactMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            preview.Classes.Add("shell-action");
            AutomationProperties.SetAutomationId(
                preview,
                $"creation-wizard-contact-{contact.ContactId:D}-preview");
            preview.Click += ContactPreviewButton_OnClick;
            fields.Children.Add(preview);

            Border card = new()
            {
                Classes = { "shell-card", "subtle" },
                Padding = new global::Avalonia.Thickness(9),
                Child = fields
            };
            AutomationProperties.SetAutomationId(
                card,
                $"creation-wizard-contact-{contact.ContactId:D}");
            ContactsList.Children.Add(card);
        }

        RenderContactPreview();
    }

    private Control BuildContactEditor(
        Guid contactId,
        CharacterCreationWizardDesktopContactField field)
    {
        Control editor;
        if (string.Equals(field.ValueKind, CharacterCreationContactValueKinds.Text, StringComparison.Ordinal))
        {
            editor = new TextBox
            {
                Text = field.SerializedValue,
                IsReadOnly = !field.IsEditable,
                IsEnabled = !_contactMutationBusy,
                MaxLength = field.Maximum ?? int.MaxValue,
                AcceptsReturn = string.Equals(
                    field.FieldId,
                    CharacterCreationContactFieldIds.Notes,
                    StringComparison.Ordinal),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
            };
        }
        else
        {
            List<ComboBoxItem> items = field.LegalOptions.Select(option => new ComboBoxItem
            {
                Content = option.Label,
                Tag = option.SerializedValue,
                IsEnabled = option.IsEnabled && field.IsEditable
            }).ToList();
            ComboBoxItem? selected = items.SingleOrDefault(item => string.Equals(
                item.Tag as string,
                field.SerializedValue,
                StringComparison.OrdinalIgnoreCase));
            editor = new ComboBox
            {
                ItemsSource = items,
                SelectedItem = selected,
                IsEnabled = field.IsEditable && !_contactMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
            };
        }

        AutomationProperties.SetAutomationId(
            editor,
            $"creation-wizard-contact-{contactId:D}-field-{field.FieldId}");
        _contactEditors[(contactId, field.FieldId)] = editor;
        return editor;
    }

    private void RenderContactPreview()
    {
        if (_contactPreview is not { } preview)
            return;

        StackPanel body = new() { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = "Review exact Core preview",
            Classes = { "shell-section-title" }
        });
        body.Children.Add(Caption(
            $"Contacts: {preview.ContactBudgetBefore.Remaining} → {preview.ContactBudgetAfter.Remaining} remaining"));
        body.Children.Add(Caption(
            $"Friends in High Places: {preview.HighPlacesBudgetBefore.Remaining} → {preview.HighPlacesBudgetAfter.Remaining} remaining"));
        foreach (CharacterCreationContactWriteOperation operation in preview.WritePlan.Operations
                     .OrderBy(static operation => operation.Order))
        {
            body.Children.Add(Caption(
                $"{operation.Order}. {operation.FieldId}: {operation.BeforeValue} → {operation.AfterValue}"));
        }
        if (preview.Blockers.Count > 0)
            body.Children.Add(Caption($"Blocked: {string.Join(", ", preview.Blockers)}"));

        Button confirm = new()
        {
            Content = "Confirm and apply",
            IsEnabled = preview.RequiresExplicitConfirmation
                        && preview.CanConfirm
                        && preview.Blockers.Count == 0
                        && !_contactMutationBusy,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        confirm.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(
            confirm,
            $"creation-wizard-contact-{preview.Edit.ContactId:D}-confirm");
        confirm.Click += ContactConfirmButton_OnClick;
        body.Children.Add(confirm);

        Border card = new()
        {
            Classes = { "shell-card" },
            Padding = new global::Avalonia.Thickness(9),
            Child = body
        };
        AutomationProperties.SetAutomationId(
            card,
            $"creation-wizard-contact-{preview.Edit.ContactId:D}-write-plan");
        ContactsList.Children.Add(card);
    }

    private CharacterCreationContactEditInput? BuildContactInput(
        CharacterCreationWizardDesktopContact contact,
        out string? error)
    {
        string? localError = null;
        Dictionary<string, string> values = contact.Fields.ToDictionary(
            static field => field.FieldId,
            field => ReadContactEditor(contact.ContactId, field.FieldId),
            StringComparer.Ordinal);
        bool identityChanged = s_IdentityFieldIds.Any(fieldId =>
            !string.Equals(
                values[fieldId],
                contact.Fields.Single(field => field.FieldId == fieldId).SerializedValue,
                StringComparison.Ordinal));
        CharacterCreationContactIdentity? identity = identityChanged
            ? new CharacterCreationContactIdentity(
                values[CharacterCreationContactFieldIds.Name],
                values[CharacterCreationContactFieldIds.Role],
                values[CharacterCreationContactFieldIds.Location],
                values[CharacterCreationContactFieldIds.Notes],
                values[CharacterCreationContactFieldIds.CustomName],
                values[CharacterCreationContactFieldIds.Metatype],
                values[CharacterCreationContactFieldIds.Gender],
                values[CharacterCreationContactFieldIds.Age],
                values[CharacterCreationContactFieldIds.ContactType],
                values[CharacterCreationContactFieldIds.PreferredPayment],
                values[CharacterCreationContactFieldIds.HobbiesVice],
                values[CharacterCreationContactFieldIds.PersonalLife],
                values[CharacterCreationContactFieldIds.GroupName])
            : null;

        int? connection = ChangedInt(CharacterCreationContactFieldIds.Connection);
        int? loyalty = ChangedInt(CharacterCreationContactFieldIds.Loyalty);
        bool? group = ChangedBool(CharacterCreationContactFieldIds.Group);
        bool? free = ChangedBool(CharacterCreationContactFieldIds.Free);
        bool? family = ChangedBool(CharacterCreationContactFieldIds.Family);
        bool? blackmail = ChangedBool(CharacterCreationContactFieldIds.Blackmail);
        if (localError is not null)
        {
            error = localError;
            return null;
        }
        error = null;
        return new CharacterCreationContactEditInput(
            contact.ContactId,
            identity,
            connection,
            loyalty,
            group,
            free,
            family,
            blackmail);

        int? ChangedInt(string fieldId)
        {
            CharacterCreationWizardDesktopContactField field = contact.Fields.Single(item => item.FieldId == fieldId);
            string value = values[fieldId];
            if (string.Equals(value, field.SerializedValue, StringComparison.Ordinal))
                return null;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;
            localError = $"{field.Label} is not a valid integer choice.";
            return null;
        }

        bool? ChangedBool(string fieldId)
        {
            CharacterCreationWizardDesktopContactField field = contact.Fields.Single(item => item.FieldId == fieldId);
            string value = values[fieldId];
            if (string.Equals(value, field.SerializedValue, StringComparison.OrdinalIgnoreCase))
                return null;
            if (bool.TryParse(value, out bool parsed))
                return parsed;
            localError = $"{field.Label} is not a valid yes/no choice.";
            return null;
        }
    }

    private string ReadContactEditor(Guid contactId, string fieldId)
    {
        if (!_contactEditors.TryGetValue((contactId, fieldId), out Control? editor))
            return string.Empty;
        return editor switch
        {
            TextBox text => text.Text ?? string.Empty,
            ComboBox { SelectedItem: ComboBoxItem { Tag: string value } } => value,
            _ => string.Empty
        };
    }

    private static string BuildContactResultStatus(
        string outcome,
        IReadOnlyList<string> blockers)
        => blockers.Count == 0
            ? $"Contact operation: {outcome}."
            : $"Contact operation: {outcome} · {string.Join(", ", blockers)}";

    private void ContactPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state?.ContactsStep is not { } contacts
            || sender is not Button { Tag: Guid contactId }
            || contacts.Contacts.SingleOrDefault(contact => contact.ContactId == contactId)
               is not { } contact)
        {
            return;
        }

        CharacterCreationContactEditInput? input = BuildContactInput(contact, out string? error);
        if (input is null)
        {
            _contactMutationStatus = error ?? "The contact edit could not be projected.";
            RenderContacts(contacts);
            return;
        }
        ContactPreviewRequested?.Invoke(this, new CharacterCreationContactPreviewRequested(input));
    }

    private void ContactConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_contactPreview is not { } preview
            || !preview.RequiresExplicitConfirmation
            || !preview.CanConfirm
            || preview.Blockers.Count != 0)
        {
            return;
        }
        ContactConfirmRequested?.Invoke(
            this,
            new CharacterCreationContactConfirmRequested(preview.PreviewDigest));
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

    private CharacterCreationWizardDesktopStep? FindPreviousEnterableStep(
        CharacterCreationWizardDesktopState state)
    {
        int selected = state.Steps.ToList().FindIndex(static step => step.IsSelected);
        for (int candidate = selected - 1; candidate >= 0; candidate--)
        {
            if (state.Steps[candidate].CanEnter)
            {
                return state.Steps[candidate];
            }
        }

        return null;
    }

    private void BackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state is null)
        {
            return;
        }

        CharacterCreationWizardDesktopStep? previous = FindPreviousEnterableStep(_state);
        if (previous is not null)
        {
            StepRequested?.Invoke(this, new CharacterCreationWizardStepRequest(previous.StepId));
        }
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
