using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.Styling;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Presentation.Overview;
using Chummer.Contracts.Presentation;
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

public sealed record CharacterCreationLifestylePreviewRequested(
    CharacterCreationLifestyleMutationInput Input);

public sealed record CharacterCreationLifestyleConfirmRequested(
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

public sealed record CharacterCreationResourcesOptionClickRequested(
    CharacterCreationWizardDesktopOption Option);

public sealed record CharacterCreationResourcesConfirmRequested(
    string PreviewDigest);

public sealed record CharacterCreationQualitiesOptionClickRequested(
    CharacterCreationWizardDesktopOption Option, bool Remove);

public sealed record CharacterCreationMagicResonanceOptionClickRequested(
    CharacterCreationWizardDesktopOption Option);

public sealed record CharacterCreationJourneyOptionClickRequested(
    CharacterCreationWizardDesktopOption Option,
    IReadOnlyDictionary<string, string>? FollowUpValues = null);

public sealed record CharacterCreationJourneyConfirmRequested(
    string PreviewDigest);

public sealed record CharacterCreationJourneyFinishRequested;

public sealed record CharacterCreationJourneyFinishConfirmRequested(
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
    private CharacterCreationResourcesPreparedPreview? _resourcesPreview;
    private string? _resourcesMutationStatus;
    private string? _qualitiesMutationStatus;
    private bool _qualitiesMutationBusy;
    private string? _attributesMutationStatus;
    private bool _attributesMutationBusy;
    private string? _skillsMutationStatus;
    private bool _skillsMutationBusy;
    private HashSet<string> _selectedQualityIds = new(StringComparer.Ordinal);
    private bool _resourcesMutationBusy;
    private string? _foundationStatus;
    private bool _foundationMutationBusy;
    private CharacterCreationLifeModulePreparedPreview? _journeyPreview;
    private string? _journeyMutationStatus;
    private bool _journeyMutationBusy;
    private CharacterCreationLifeModuleFinishPreview? _journeyFinishPreview;

    // Journey follow-up drafts (audit ui019/T292): the follow-up inputs are
    // rebuilt on every busy/status re-render; without a draft map the
    // player's typing is lost whenever a preview or status line appears.
    // Drafts are scoped to the option they belong to so a re-render can
    // never substitute another option's values into a different card.
    private readonly Dictionary<(int Stage, string OptionId, string? VersionId), Dictionary<string, string>> _journeyFollowUpDrafts = [];
    private readonly Dictionary<(int Stage, string OptionId, string? VersionId), Dictionary<string, Func<string?>>> _journeyFollowUpReaders = [];
    private Border? _journeyPreviewPanel;

    public CharacterCreationLifeModulePreparedPreview? JourneyPreview => _journeyPreview;

    public CharacterCreationLifeModuleFinishPreview? JourneyFinishPreview => _journeyFinishPreview;
    private bool _contactMutationBusy;
    private CharacterCreationLifestylePreparedPreview? _lifestylePreview;
    private string? _lifestyleMutationStatus;
    private bool _lifestyleMutationBusy;
    private string? _boundLifestylesSnapshotDigest;
    // Draft-preservation state (audit win008): the wizard keeps the player's
    // entered values in typed drafts so a preview (valid, blocked, or failed)
    // re-renders the same editing surface with the same values, and a stable
    // minted identity survives the whole review cycle for new entities.
    private Dictionary<string, string>? _contactAddDraft;
    private Guid? _contactAddDraftIdentity;
    private readonly Dictionary<Guid, Dictionary<string, string>> _contactEditDrafts = [];
    // Field-level dirty tracking (audit win010): the saved values each edit
    // draft was captured from. A draft field equal to its base is "not changed
    // by the player" and must accept newer saved values on refresh instead of
    // resurrecting stale content.
    private readonly Dictionary<Guid, Dictionary<string, string>> _contactEditDraftBases = [];
    private CharacterCreationLifestyleConfiguration? _lifestyleCreateDraft;
    private Guid? _lifestyleCreateDraftIdentity;
    private readonly Dictionary<Guid, CharacterCreationLifestyleConfiguration> _lifestyleEditDrafts = [];
    // Saved values each lifestyle edit draft was captured from (audit win010).
    private readonly Dictionary<Guid, CharacterCreationLifestyleConfiguration> _lifestyleEditDraftBases = [];
    // Field-level merge conflicts (audit win010): the saved state and the
    // retained draft changed the same field differently. Rendered as a visible
    // note instead of silently preferring one side.
    private readonly Dictionary<Guid, string> _lifestyleMergeConflictNotes = [];
    private readonly Dictionary<Guid, string> _contactMergeConflictNotes = [];
    // Review binding (audit win008): the values at preview time. Confirmation
    // stays available only while the visible editors still match this baseline.
    private IReadOnlyDictionary<string, string>? _contactPreviewBaseline;
    private Guid? _contactPreviewBaselineId;
    private CharacterCreationLifestyleConfiguration? _lifestylePreviewBaseline;
    private Guid? _lifestylePreviewBaselineId;
    private bool _finalizeBusy;
    private string? _finalizeStatus;
    /// <summary>Latest authoritative finalization availability (from the
    /// wizard state's review gate). Single source of truth for the Finish
    /// button projection; mutation-busy transitions must not override it.</summary>
    private bool _finalizeAvailable;
    private IReadOnlyList<string> _finalizeBlockers = [];
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
    public event EventHandler<CharacterCreationLifestylePreviewRequested>? LifestylePreviewRequested;
    public event EventHandler<CharacterCreationLifestyleConfirmRequested>? LifestyleConfirmRequested;
    public event EventHandler<CharacterCreationFoundationOptionClickRequested>? FoundationOptionClickRequested;
    public event EventHandler<CharacterCreationResourcesOptionClickRequested>? ResourcesOptionClickRequested;
    public event EventHandler<CharacterCreationResourcesConfirmRequested>? ResourcesConfirmRequested;
    public event EventHandler<CharacterCreationQualitiesOptionClickRequested>? QualitiesOptionClickRequested;
    public event EventHandler? QualitiesEditRequested;

    /// <summary>Raised when the user asks to open the manual attribute allocation editor.</summary>
    public event EventHandler? AttributesEditRequested;
    public event EventHandler? SkillsEditRequested;
    public event EventHandler<CharacterCreationMagicResonanceOptionClickRequested>? MagicResonanceOptionClickRequested;

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

            var rowPanel = new StackPanel { Spacing = 1 };
            rowPanel.Children.Add(new TextBlock
            {
                Text = options[0].CategoryName,
                FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                FontSize = 12,
            });
            // UX-04: the rank cells must share the available width instead of a
            // fixed 92 DIP each — at 960 DIP / 150% scaling a fixed row cannot
            // fit all five ranks and the low ranks (e.g. E) clip out of reach.
            var ranksPanel = new global::Avalonia.Controls.Primitives.UniformGrid
            {
                Columns = options.Count,
            };
            foreach (var option in options)
            {
                var cellPanel = new StackPanel
                {
                    Spacing = 2,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                };
                var rankButton = new Button
                {
                    Content = option.Rank,
                    Tag = option,
                    HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                    Padding = new global::Avalonia.Thickness(4, 1),
                };
                AutomationProperties.SetAutomationId(
                    rankButton,
                    "creation-wizard-priority-" + category + "-" + option.Rank);
                rankButton.Click += PriorityRankButton_OnClick;
                _priorityRankButtons.Add(rankButton);
                cellPanel.Children.Add(rankButton);
                cellPanel.Children.Add(new TextBlock
                {
                    Text = option.Label,
                    FontSize = 10.5,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    MaxHeight = 28,
                    TextAlignment = global::Avalonia.Media.TextAlignment.Center,
                    Opacity = 0.85,
                });
                ranksPanel.Children.Add(cellPanel);
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

    public event EventHandler<CharacterCreationJourneyOptionClickRequested>? JourneyOptionClickRequested;

    public event EventHandler<CharacterCreationJourneyConfirmRequested>? JourneyConfirmRequested;

    public event EventHandler<CharacterCreationJourneyFinishRequested>? JourneyFinishRequested;

    public event EventHandler<CharacterCreationJourneyFinishConfirmRequested>? JourneyFinishConfirmRequested;
    public event EventHandler? FinalizeRequested;

    /// <summary>Raised when the user asks the Build Ghost to finish the character.</summary>
    public event EventHandler? FinishWithAiRequested;

    private bool _aiFinishBusy;

    /// <summary>
    /// Session-scoped feature gate for the Finish-with-AI affordance. Dark by
    /// default: the flow is still under active qualification, so every
    /// visibility write funnels through <see cref="RefreshFinishWithAiAvailability"/>
    /// and no setter can resurface the button while the gate is closed.
    /// </summary>
    private bool _finishWithAiFeatureEnabled;

    /// <summary>Last overview-projected finalization-read availability.</summary>
    private bool _finishWithAiReviewAvailable;

    /// <summary>
    /// Single projection choke point for the Finish-with-AI button. The feature
    /// gate is evaluated here and only here, so the busy/result writers cannot
    /// bypass it by writing visibility directly.
    /// </summary>
    private void RefreshFinishWithAiAvailability()
    {
        bool canActuate = _finishWithAiFeatureEnabled
            && _finishWithAiReviewAvailable
            && !_finalizeBusy
            && !_aiFinishBusy;
        FinishWithAiButton.IsVisible = canActuate;
        FinishWithAiButton.IsEnabled = canActuate;
    }

    /// <summary>
    /// Enables or disables the Finish-with-AI affordance for this session.
    /// Deliberately separate from <see cref="SetFinishWithAiAvailability"/>:
    /// the overview projection says when the wizard could offer the action,
    /// this gate says whether the still-qualifying flow may be offered at all.
    /// </summary>
    public void SetFinishWithAiFeatureEnabled(bool enabled)
    {
        _finishWithAiFeatureEnabled = enabled;
        RefreshFinishWithAiAvailability();
    }

    /// <summary>
    /// Projects overview finalization availability onto the Finish-with-AI
    /// affordance: it lives on the same finalization-read authority as Finish,
    /// and is hidden while any wizard mutation is in flight.
    /// </summary>
    public void SetFinishWithAiAvailability(bool canReview)
    {
        _finishWithAiReviewAvailable = canReview;
        RefreshFinishWithAiAvailability();
    }

    /// <summary>Shows the AI-finish surface as busy (button hidden while working).</summary>
    public void SetFinishWithAiBusy(bool busy)
    {
        _aiFinishBusy = busy;
        RefreshFinishWithAiAvailability();
        if (busy)
        {
            FinalizeStatusText.Text = "Build Ghost is preparing a missing-only proposal...";
        }
    }

    /// <summary>Reports the AI-finish outcome in the finalize status line.</summary>
    public bool IsFinishWithAiBusy => _aiFinishBusy;

    public void SetFinishWithAiResult(bool success, string message)
    {
        _finalizeStatus = message;
        FinalizeStatusText.Text = message;
        RefreshFinishWithAiAvailability();
    }


    /// <summary>
    /// Projects the overview state's finalization read into the wizard surface.
    /// The wizard snapshot itself is navigation/read-only (Core authority); the
    /// finalize affordance is driven entirely by the overview's authoritative
    /// finalization projection.
    /// </summary>
    public void SetFinalizeAvailability(bool canReview, IReadOnlyList<string> blockers)
    {
        ArgumentNullException.ThrowIfNull(blockers);
        _finalizeAvailable = canReview;
        _finalizeBlockers = blockers;
        RefreshFinalizeButton();
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
        // Audit 2026-11-03 P2: a rejected finalization withdraws availability
        // until the next authoritative gate update; the shared projection then
        // keeps the button hidden even when mutation-busy clears.
        _finalizeAvailable = false;
        RefreshFinalizeButton();
    }

    /// <summary>
    /// Single projection point for the Finish button: busy hides; otherwise
    /// the latest authoritative availability decides, and the status line
    /// keeps a rejection message once one exists.
    /// Audit 2026-11-03 P2.
    /// </summary>
    private void RefreshFinalizeButton()
    {
        var canReview = !_finalizeBusy && _finalizeAvailable;
        FinalizeButton.IsVisible = canReview;
        FinalizeButton.IsEnabled = canReview;
        if (_finalizeStatus is null)
        {
            FinalizeStatusText.Text = canReview
                ? "Whole-build review is available; finalizing applies the authoritative receipt and unlocks the advanced editor."
                : _finalizeBlockers.Count > 0
                    ? $"Finalization unavailable: {string.Join(", ", _finalizeBlockers)}"
                    : "Finalization is not available for this workspace revision.";
        }
    }

    public void SetFinalizeMutationBusy(bool busy)
    {
        _finalizeBusy = busy;
        // Both transitions use the same authoritative availability: entering
        // busy hides the action; leaving busy cannot revive a rejected review.
        RefreshFinalizeButton();
    }

    /// <summary>Read-only automation projection of the current wizard snapshot.</summary>
    public CharacterCreationWizardDesktopState? StateForAutomation() => _state;

    /// <summary>True when the prepared foundation preview can be confirmed.</summary>
    public bool FoundationPreviewConfirmable => _foundationPreview is { CanConfirm: true };

    public void SetState(CharacterCreationWizardDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // Draft lifecycle (audit win009): drafts and their minted identities are
        // bound to the character context, not to which wizard step is visible.
        // Step navigation (ContactsStep/LifestylesStep appearing or disappearing
        // while the same character revision stays bound) must preserve unsaved
        // input. A character revision change (another entry saved elsewhere)
        // also preserves the player's input: retained drafts become "unreviewed"
        // and require a fresh Core preview before they may be applied again.
        // Only a genuine character-context change (different workspace identity)
        // retires drafts, so unsaved input never transplants into another
        // character. Previews and review baselines are still retired whenever
        // the character revision or their step projection changes, because a
        // review describes the exact values it was prepared against.
        bool contextChanged = !string.Equals(
                                  state.WorkspaceId,
                                  _state?.WorkspaceId,
                                  StringComparison.Ordinal)
                              || state.WorkspaceRevision != _state?.WorkspaceRevision
                              || !string.Equals(
                                  state.SnapshotDigest,
                                  _state?.SnapshotDigest,
                                  StringComparison.Ordinal);
        bool ownerChanged = !string.Equals(
            state.WorkspaceId,
            _state?.WorkspaceId,
            StringComparison.Ordinal);
        HarvestJourneyFollowUpDrafts();
        if (ownerChanged)
            ClearJourneyPreviews();
        else if (contextChanged)
        {
            _journeyPreview = null;
            _journeyFinishPreview = null;
            _journeyMutationStatus = null;
        }
        if (!string.Equals(
                state.ContactsStep?.SnapshotDigest,
                _boundContactsSnapshotDigest,
                StringComparison.Ordinal)
            || contextChanged)
        {
            _contactPreview = null;
            _contactMutationStatus = null;
            _contactPreviewBaseline = null;
            _contactPreviewBaselineId = null;
            if (ownerChanged)
            {
                _contactAddDraft = null;
                _contactAddDraftIdentity = null;
                _contactEditDrafts.Clear();
                _contactEditDraftBases.Clear();
                _contactMergeConflictNotes.Clear();
                ContactsList.Children.Clear();
                _contactEditors.Clear();
            }
            _boundContactsSnapshotDigest = state.ContactsStep?.SnapshotDigest;
        }
        if (!string.Equals(
                state.LifestylesStep?.SnapshotDigest,
                _boundLifestylesSnapshotDigest,
                StringComparison.Ordinal)
            || contextChanged)
        {
            _lifestylePreview = null;
            _lifestyleMutationStatus = null;
            _lifestylePreviewBaseline = null;
            _lifestylePreviewBaselineId = null;
            if (ownerChanged)
            {
                _lifestyleCreateDraft = null;
                _lifestyleCreateDraftIdentity = null;
                _lifestyleEditDrafts.Clear();
                _lifestyleEditDraftBases.Clear();
                _lifestyleMergeConflictNotes.Clear();
                LifestylesList.Children.Clear();
            }
            _boundLifestylesSnapshotDigest = state.LifestylesStep?.SnapshotDigest;
        }
        // Harvest BEFORE rebinding _state: the harvest reads the currently bound
        // step projection to locate the live editors. If the new state drops the
        // step (navigation away), a harvest after the rebind would silently skip
        // and lose the player's typing (audit win009 navigation trigger).
        HarvestDraftsBeforeRender(contacts: true, lifestyles: true);
        // The live editors belong to the old projection. After harvesting them,
        // rebuild from the new projection without harvesting those controls twice.
        ContactsList.Children.Clear();
        _contactEditors.Clear();
        LifestylesList.Children.Clear();
        _state = state;

        CharacterCreationWizardDesktopStep active = state.Steps.Single(step => step.IsSelected);
        ActiveStepTitle.Text = active.Label;
        int stepIndex = state.Steps.ToList().FindIndex(step => object.ReferenceEquals(step, active));
        WizardStepCounter.Text = stepIndex >= 0
            ? $"Step {stepIndex + 1} of {state.Steps.Count}: {active.Label}"
            : active.Label;
        BackButton.IsEnabled = FindPreviousEnterableStep(state) is not null;
        _lastWizardState = state;
        RefreshStartingCashVisibility();
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
        RenderLifestyles(state.LifestylesStep);
        RefreshBuildGhostAvailability();
    }


    private CharacterCreationStartingNuyenSource? _startingCashSource;
    private int _startingCashTotal;
    private bool _startingCashRolled;

    /// <summary>
    /// Projects the authoritative starting-cash source and rolls the player's
    /// dice for them once per source binding (Chummer5a parity). The result is
    /// displayed on the finalize surface; Re-roll re-rolls fairly.
    /// </summary>
    public void SetStartingCashState(CharacterCreationStartingNuyenSource? source)
    {
        bool sourceChanged = !string.Equals(
            _startingCashSource?.AuthorityDigest,
            source?.AuthorityDigest,
            StringComparison.Ordinal);
        _startingCashSource = source;
        if (source is null)
        {
            _startingCashRolled = false;
            _startingCashTotal = 0;
            RefreshStartingCashVisibility();
            return;
        }
        if (sourceChanged || !_startingCashRolled)
        {
            _startingCashTotal = RollStartingCash(source);
            _startingCashRolled = true;
        }
        RefreshStartingCashVisibility();
        // Display shows the payable amount (dice sum x multiplier); the
        // choice payload in GetStartingCashChoice keeps submitting the raw
        // dice sum to Core, which applies the multiplier itself.
        StartingCashText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0}: rolled {1}d6 = {3}; x{2} = {4} nuyen. Re-roll if you prefer another roll.",
            source.Name,
            source.Dice,
            source.Multiplier,
            _startingCashTotal,
            (long)_startingCashTotal * source.Multiplier);
    }

    /// <summary>
    /// Projects StartingCashPanel visibility from the ACTIVE wizard step. The
    /// starting-cash roll belongs to the finalize surface (see
    /// <see cref="SetStartingCashState"/>); binding it on every overview bind
    /// regardless of step squeezes the center card's scroll viewport on the
    /// method step until priority rank buttons fall below the viewport clip
    /// and real pointer input cannot reach them (gen-10 acceptance finding:
    /// InputHitTest at the rank button's center returned the starting-cash
    /// heading). Hiding the panel on non-finalize steps restores the scroll
    /// row's space and genuine pointer reachability.
    /// </summary>
    private void RefreshStartingCashVisibility()
    {
        string? activeStepId = _lastWizardState?.Steps
            .FirstOrDefault(step => step.IsSelected)?.StepId;
        bool onFinalizeSurface = string.Equals(
            activeStepId,
            CharacterCreationWizardStepIds.Review,
            StringComparison.Ordinal);
        StartingCashPanel.IsVisible = onFinalizeSurface
            && _startingCashSource is not null
            && _startingCashRolled;
    }

    /// <summary>The rolled starting-cash choice, or null before the first roll.</summary>
    public CharacterCreationStartingCashChoice? GetStartingCashChoice()
    {
        if (_startingCashSource is null || !_startingCashRolled)
            return null;
        int max = _startingCashSource.Dice * 6;
        int total = Math.Clamp(_startingCashTotal, _startingCashSource.Dice, max);
        return new CharacterCreationStartingCashChoice(_startingCashSource.AuthorityDigest, total);
    }

    private static int RollStartingCash(CharacterCreationStartingNuyenSource source)
    {
        if (source.Dice <= 0)
            return 0;
        int total = 0;
        for (int i = 0; i < source.Dice; i++)
        {
            total += System.Random.Shared.Next(1, 7);
        }
        return total;
    }

    private void StartingCashRerollButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_startingCashSource is null)
            return;
        _startingCashTotal = RollStartingCash(_startingCashSource);
        SetStartingCashState(_startingCashSource);
    }
    private void FinalizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FinalizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FinishWithAiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_finishWithAiFeatureEnabled)
        {
            return;
        }

        FinishWithAiRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetContactPrepareResult(CharacterCreationContactsInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactPreview = result.PreparedPreview;
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        // Draft preservation: the draft store and the minted add identity are
        // deliberately NOT cleared here, so the rebuilt editing surface keeps
        // the player's values across valid, blocked and failed previews.
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
            // The mutation applied: only the saved entry's draft cycle is
            // complete (audit win009). Other entries keep their unsaved input;
            // a later character-context change retires them via SetState.
            RetireCompletedContactDraftCycle(result.PreparedPreview?.Edit.ContactId ?? Guid.Empty);
        }
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactReceiptLookupResult(
        CharacterCreationContactsInteractionReceiptLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null)
        {
            // A matching receipt proves the save committed. This is the same
            // verified completion as a direct confirmation, so the same draft
            // retirement applies (audit win010): the next create cycle must
            // mint a fresh identity instead of reusing the saved one. A missing
            // or mismatched receipt stays unresolved and retires nothing.
            _contactPreview = null;
            RetireCompletedContactDraftCycle(result.Receipt.ContactId);
        }
        RenderContacts(_state?.ContactsStep);
    }

    /// <summary>
    /// Retires the draft cycle of one verified-completed contact entry
    /// (direct confirmation or verified receipt recovery): the add/create
    /// draft identity if the saved entry was the minted create cycle, the
    /// entry's edit draft otherwise. Unrelated unsaved drafts survive.
    /// </summary>
    private void RetireCompletedContactDraftCycle(Guid savedContactId)
    {
        if (savedContactId != Guid.Empty && savedContactId == _contactAddDraftIdentity)
        {
            // Same harvest-before-clear ordering as the lifestyle retire: the
            // add identity must still be set during the harvest (no fresh
            // minting), and the clears below retire the completed cycle.
            HarvestDraftsBeforeRender(contacts: true, lifestyles: false);
            _contactAddDraft = null;
            _contactAddDraftIdentity = null;
            ContactsList.Children.Clear();
            _contactEditors.Clear();
        }
        else
        {
            _contactEditDrafts.Remove(savedContactId);
            _contactEditDraftBases.Remove(savedContactId);
        }
        _contactPreviewBaseline = null;
        _contactPreviewBaselineId = null;
    }

    public void SetLifestylePrepareResult(CharacterCreationLifestylesInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _lifestylePreview = result.PreparedPreview;
        _lifestyleMutationStatus = BuildLifestyleResultStatus(result.Outcome, result.Blockers);
        // Draft preservation: the draft store and the minted create identity are
        // deliberately NOT cleared here, so the rebuilt editing surface keeps
        // the player's values across valid, blocked and failed previews.
        RenderLifestyles(_state?.LifestylesStep);
    }

    public void SetLifestyleConfirmResult(CharacterCreationLifestylesInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _lifestyleMutationStatus = BuildLifestyleResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null
            && !result.Blockers.Contains(CharacterCreationLifestylesInteractionBlockers.ReceiptMismatch))
        {
            _lifestylePreview = null;
            // The mutation saved: only the saved entry's draft cycle is complete
            // (audit win009). Other entries keep their unsaved input; a later
            // character-context change retires them via SetState.
            RetireCompletedLifestyleDraftCycle(result.PreparedPreview?.Mutation.LifestyleId ?? Guid.Empty);
        }
        RenderLifestyles(_state?.LifestylesStep);
    }

    /// <summary>
    /// Retires the draft cycle of one verified-completed lifestyle entry
    /// (direct confirmation or verified receipt recovery): the create draft
    /// identity if the saved entry was the minted create cycle, the entry's
    /// edit draft otherwise. Unrelated unsaved drafts survive.
    /// </summary>
    private void RetireCompletedLifestyleDraftCycle(Guid savedLifestyleId)
    {
        if (savedLifestyleId != Guid.Empty && savedLifestyleId == _lifestyleCreateDraftIdentity)
        {
            // Harvest BEFORE clearing anything (audit win011 P2): the retire
            // runs while the old projection is still bound, so unrelated
            // unsaved edits visible right now must be captured into the draft
            // store before the controls (and their values) are destroyed. The
            // create identity must still be set during this harvest — the
            // create-form harvest branch keeps (never mints) it, and the
            // clears below then retire the completed cycle for good.
            HarvestDraftsBeforeRender(contacts: false, lifestyles: true);
            _lifestyleCreateDraft = null;
            _lifestyleCreateDraftIdentity = null;
            LifestylesList.Children.Clear();
        }
        else
        {
            _lifestyleEditDrafts.Remove(savedLifestyleId);
            _lifestyleEditDraftBases.Remove(savedLifestyleId);
        }
        _lifestylePreviewBaseline = null;
        _lifestylePreviewBaselineId = null;
    }

    /// <summary>
    /// Retires prepared previews and review baselines after a character revision
    /// changed, but preserves the player's unsaved drafts (audit win009): a save
    /// of one entry must not discard unrelated input in either domain. Drafts
    /// remain unreviewed; confirming them requires a fresh Core preview.
    /// </summary>
    public void RetireContactLifestylePreviews()
    {
        _contactPreview = null;
        _contactMutationStatus = null;
        _contactPreviewBaseline = null;
        _contactPreviewBaselineId = null;
        _lifestylePreview = null;
        _lifestyleMutationStatus = null;
        _lifestylePreviewBaseline = null;
        _lifestylePreviewBaselineId = null;
        RenderContacts(_state?.ContactsStep);
        RenderLifestyles(_state?.LifestylesStep);
    }

    public void ClearContactLifestylePreviews()
    {
        // This is the owner/workspace reset boundary. Do not redraw or harvest
        // the previous context's saved values while its replacement is loading.
        if (_state is not null)
            _state = _state with { ContactsStep = null, LifestylesStep = null };
        _contactAddDraft = null;
        _contactAddDraftIdentity = null;
        _contactEditDrafts.Clear();
        _contactEditDraftBases.Clear();
        _contactMergeConflictNotes.Clear();
        _contactPreviewBaseline = null;
        _contactPreviewBaselineId = null;
        _lifestyleCreateDraft = null;
        _lifestyleCreateDraftIdentity = null;
        _lifestyleEditDrafts.Clear();
        _lifestyleEditDraftBases.Clear();
        _lifestyleMergeConflictNotes.Clear();
        _lifestylePreviewBaseline = null;
        _lifestylePreviewBaselineId = null;
        ContactsList.Children.Clear();
        _contactEditors.Clear();
        LifestylesList.Children.Clear();
        _contactPreview = null;
        _contactMutationStatus = null;
        _lifestylePreview = null;
        _lifestyleMutationStatus = null;
        RenderContacts(_state?.ContactsStep);
        RenderLifestyles(_state?.LifestylesStep);
    }

    public void SetLifestyleReceiptLookupResult(CharacterCreationLifestylesInteractionReceiptLookupResult result)
    {
        _lifestyleMutationStatus = result.Receipt is not null
            ? "The saved lifestyle change was found. Reloading your character."
            : "The save outcome is still unknown. Review or confirm again to check the original save; no new change will be sent.";
        if (result.Receipt is not null)
        {
            // Verified completion via receipt recovery retires the completed
            // create cycle exactly like a direct confirmation (audit win010).
            _lifestylePreview = null;
            RetireCompletedLifestyleDraftCycle(result.Receipt.LifestyleId);
        }
        RenderLifestyles(_state?.LifestylesStep);
    }

    public void SetLifestyleMutationBusy(bool busy)
    {
        _lifestyleMutationBusy = busy;
        RenderLifestyles(_state?.LifestylesStep);
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

    public void SetJourneyPrepareResult(CharacterCreationLifeModuleJourneyInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _journeyPreview = result.PreparedPreview;
        _journeyMutationStatus = BuildJourneyResultStatus(result.Outcome, result.Blockers);
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetJourneyConfirmResult(CharacterCreationLifeModuleJourneyInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _journeyMutationStatus = BuildJourneyResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null
            && !result.Blockers.Contains(CharacterCreationLifeModuleJourneyInteractionBlockers.ReceiptMismatch))
        {
            _journeyPreview = null;
        }
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetJourneyFinishPrepareResult(CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _journeyFinishPreview = result.FinishPreview;
        _journeyMutationStatus = BuildJourneyResultStatus(result.Outcome, result.Blockers);
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetJourneyFinishConfirmResult(CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _journeyMutationStatus = BuildJourneyResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null
            && !result.Blockers.Contains(CharacterCreationLifeModuleJourneyInteractionBlockers.ReceiptMismatch))
        {
            _journeyFinishPreview = null;
        }
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetJourneyMutationBusy(bool busy)
    {
        _journeyMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetJourneyStatus(string message)
    {
        _journeyMutationStatus = message;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void ClearJourneyPreviews()
    {
        _journeyPreview = null;
        _journeyFinishPreview = null;
        _journeyMutationStatus = null;
        _journeyFollowUpDrafts.Clear();
        _journeyFollowUpReaders.Clear();
        _journeyPreviewPanel = null;
    }

    private static string BuildJourneyResultStatus(string outcome, IReadOnlyList<string> blockers)
    {
        string suffix = blockers.Count == 0 ? string.Empty : $" — {string.Join(", ", blockers)}";
        return $"Life module journey: {outcome}{suffix}";
    }

    public void SetResourcesPrepareResult(CharacterCreationResourcesInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _resourcesPreview = result.PreparedPreview;
        _resourcesMutationStatus = BuildResourcesResultStatus(result.Outcome, result.Blockers);
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetResourcesConfirmResult(CharacterCreationResourcesInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _resourcesMutationStatus = BuildResourcesResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null)
            _resourcesPreview = null;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetResourcesMutationBusy(bool busy)
    {
        _resourcesMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    /// <summary>
    /// Manual resources-step status line (gear-draft hold reporting): the
    /// zero-basket gear commit reports through the same legal option list as
    /// the other domains, so failures are visible instead of silently swallowed.
    /// </summary>
    public void ResourcesStatus(string message)
    {
        _resourcesMutationStatus = message;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    /// <summary>
    /// Manual Qualities/Magic-Resonance status line (WINDOWS_SCOPE 2026-11-03
    /// item P1): the manual option-card surfaces report through the same legal
    /// option list as the other domains, so failures are visible instead of
    /// silently swallowed.
    /// </summary>
    public void QualitiesStatus(string message)
    {
        _qualitiesMutationStatus = message;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SetQualitiesSelection(IReadOnlyList<string> selectedOptionIds)
    {
        _selectedQualityIds = selectedOptionIds.ToHashSet(StringComparer.Ordinal);
    }

    public void QualitiesMutationBusy(bool busy)
    {
        _qualitiesMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void AttributesStatus(string message)
    {
        _attributesMutationStatus = message;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SkillsStatus(string message)
    {
        _skillsMutationStatus = message;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void AttributesMutationBusy(bool busy)
    {
        _attributesMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    public void SkillsMutationBusy(bool busy)
    {
        _skillsMutationBusy = busy;
        RenderLegalOptions(_state?.LegalOptions ?? []);
    }

    private static string BuildResourcesResultStatus(string outcome, IReadOnlyList<string> blockers)
    {
        string suffix = blockers.Count == 0 ? string.Empty : $" (blocked: {string.Join(", ", blockers)})";
        return $"Resources interaction: {outcome}{suffix}";
    }

    private static string BuildFoundationResultStatus(string outcome, IReadOnlyList<string> blockers)
    {
        string suffix = blockers.Count == 0 ? string.Empty : $" (blocked: {string.Join(", ", blockers)})";
        return $"Foundation interaction: {outcome}{suffix}";
    }

    private void FoundationOptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CharacterCreationWizardDesktopOption option })
            return;
        if (string.Equals(
                _state?.ActiveStepId,
                CharacterCreationWizardStepIds.Resources,
                StringComparison.Ordinal))
        {
            ResourcesOptionClickRequested?.Invoke(this, new CharacterCreationResourcesOptionClickRequested(option));
            return;
        }
        if (string.Equals(
                _state?.ActiveStepId,
                CharacterCreationWizardStepIds.Qualities,
                StringComparison.Ordinal))
        {
            if (!_qualitiesMutationBusy)
                QualitiesOptionClickRequested?.Invoke(this, new CharacterCreationQualitiesOptionClickRequested(option, _selectedQualityIds.Contains(option.OptionId)));
            return;
        }
        if (string.Equals(
                _state?.ActiveStepId,
                CharacterCreationWizardStepIds.MagicResonance,
                StringComparison.Ordinal))
        {
            MagicResonanceOptionClickRequested?.Invoke(this, new CharacterCreationMagicResonanceOptionClickRequested(option));
            return;
        }
        if (_state?.JourneyStep is not null
            && string.Equals(
                _state?.ActiveStepId,
                CharacterCreationWizardStepIds.LifeModules,
                StringComparison.Ordinal))
        {
            JourneyOptionButton_OnClick(sender, e);
            return;
        }
        FoundationOptionClickRequested?.Invoke(this, new CharacterCreationFoundationOptionClickRequested(option));
    }

    private void ResourcesConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_resourcesPreview is { } preview)
            ResourcesConfirmRequested?.Invoke(this, new CharacterCreationResourcesConfirmRequested(preview.PreviewDigest));
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

    /// <summary>
    /// Clears the entire Build Ghost conversation transcript and the unsent
    /// question draft. Invoked by the window at an owner/workspace identity
    /// boundary so the previous context's advice and typed-but-unsubmitted
    /// question can never be presented as the current context's content
    /// (audit win015: pending replies were already isolated by turn
    /// invalidation, but content already on screen survived the switch).
    /// Turn invalidation itself stays in the window; this only resets the
    /// displayed state.
    /// </summary>
    public void ResetBuildGhostConversation()
    {
        GhostConversation.Children.Clear();
        GhostQuestionTextBox.Text = string.Empty;
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
        HarvestJourneyFollowUpDrafts();
        _journeyFollowUpReaders.Clear();
        _journeyPreviewPanel = null;
        LegalOptionList.Children.Clear();
        LegalOptionList.IsVisible = _state?.ContactsStep is null;
        StepContentHeading.Text = _state?.ContactsStep is null
            ? "Legal choices and prerequisites"
            : "Contacts and field authority";
        if (!LegalOptionList.IsVisible)
            return;
        bool qualities = _state?.ActiveStepId == CharacterCreationWizardStepIds.Qualities;
        bool journey = _state?.ActiveStepId == CharacterCreationWizardStepIds.LifeModules
            && _state?.JourneyStep is not null;
        if (journey)
            RenderJourneyStepHeader();
        if (_state?.ActiveStepId == CharacterCreationWizardStepIds.MagicResonance)
        {
            LegalOptionList.Children.Add(Caption("Magic/Resonance editing is not available in this Windows build yet. These choices cannot be applied here."));
            return;
        }
        if (qualities)
        {
            var edit = new Button { Content = "Edit selected qualities / confirm no qualities", IsEnabled = !_qualitiesMutationBusy };
            AutomationProperties.SetAutomationId(edit, "creation-wizard-edit-qualities");
            edit.Click += (_, _) => { if (!_qualitiesMutationBusy) QualitiesEditRequested?.Invoke(this, EventArgs.Empty); };
            LegalOptionList.Children.Add(edit);
            if (_qualitiesMutationBusy)
                LegalOptionList.Children.Add(new ProgressBar { IsIndeterminate = true });
            if (!string.IsNullOrWhiteSpace(_qualitiesMutationStatus))
                LegalOptionList.Children.Add(Caption(_qualitiesMutationStatus));
        }
        if (_state?.ActiveStepId == CharacterCreationWizardStepIds.Attributes)
        {
            string authorityBlocker = CharacterCreationWizardProjector.AttributesAuthorityUnavailable;
            bool attributesReady = _state is { } s
                && !s.CompletionBlockers.Contains(authorityBlocker)
                && !s.Budgets.Any(b => b.Blockers.Contains(authorityBlocker));
            var edit = new Button { Content = "Allocate attributes\u2026", IsEnabled = attributesReady && !_attributesMutationBusy };
            AutomationProperties.SetAutomationId(edit, "creation-wizard-allocate-attributes");
            edit.Click += (_, _) => { if (attributesReady && !_attributesMutationBusy) AttributesEditRequested?.Invoke(this, EventArgs.Empty); };
            LegalOptionList.Children.Add(edit);
            if (_attributesMutationBusy)
                LegalOptionList.Children.Add(new ProgressBar { IsIndeterminate = true });
            if (!string.IsNullOrWhiteSpace(_attributesMutationStatus))
                LegalOptionList.Children.Add(Caption(_attributesMutationStatus));
            if (attributesReady)
            {
                if (string.IsNullOrWhiteSpace(_attributesMutationStatus))
                    LegalOptionList.Children.Add(Caption(
                        "Attributes are ready to allocate. Open the editor to spend your available points."));
            }
            else
            {
                LegalOptionList.Children.Add(Caption(
                    "Attribute allocation opens once attributes are authoritative for this workspace."));
            }
        }
        if (_state?.ActiveStepId == CharacterCreationWizardStepIds.Skills)
        {
            string authorityBlocker = CharacterCreationSkillsBlockers.AuthorityUnavailable;
            bool skillsReady = _state is { } s
                && !s.CompletionBlockers.Contains(authorityBlocker)
                && !s.Budgets.Any(b => b.Blockers.Contains(authorityBlocker));
            var edit = new Button { Content = "Allocate skills\u2026", IsEnabled = skillsReady && !_skillsMutationBusy };
            AutomationProperties.SetAutomationId(edit, "creation-wizard-allocate-skills");
            edit.Click += (_, _) => { if (skillsReady && !_skillsMutationBusy) SkillsEditRequested?.Invoke(this, EventArgs.Empty); };
            LegalOptionList.Children.Add(edit);
            if (_skillsMutationBusy)
                LegalOptionList.Children.Add(new ProgressBar { IsIndeterminate = true });
            if (!string.IsNullOrWhiteSpace(_skillsMutationStatus))
                LegalOptionList.Children.Add(Caption(_skillsMutationStatus));
            if (skillsReady)
            {
                if (string.IsNullOrWhiteSpace(_skillsMutationStatus))
                    LegalOptionList.Children.Add(Caption(
                        "Skills are ready to allocate. Open the editor to spend your available points."));
            }
            else
            {
                LegalOptionList.Children.Add(Caption(
                    "Skill allocation opens once skills are authoritative for this workspace."));
            }
        }
        if (options.Count == 0)
        {
            bool dedicatedEditorShown = _state?.ActiveStepId
                is CharacterCreationWizardStepIds.Attributes
                or CharacterCreationWizardStepIds.Skills
                or CharacterCreationWizardStepIds.Qualities;
            if (!dedicatedEditorShown)
            {
                LegalOptionList.Children.Add(Caption(
                    "No authoritative choices are available for this step. Check the blockers rather than guessing."));
            }
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
                IsEnabled = qualities ? !_qualitiesMutationBusy && (option.IsEnabled || _selectedQualityIds.Contains(option.OptionId)) : option.IsEnabled,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = qualities ? $"{(_selectedQualityIds.Contains(option.OptionId) ? "Remove" : "Add")}: {option.Label}" : option.Label, Classes = { "shell-section-title" } },
                    Caption($"{prerequisite} · {costs}"),
                    Caption($"Source: {source}")
                }
            }
        };
            optionButton.Classes.Add("quiet");
            AutomationProperties.SetAutomationId(optionButton, $"creation-wizard-option-{option.OptionId}");
            if (journey)
            {
                var body = new StackPanel { Spacing = 4 };
                var readers = new Dictionary<string, Func<string?>>(StringComparer.Ordinal);
                var draftKey = (_state!.JourneyStep!.CurrentStageOrder, option.OptionId, option.VersionId);
                _journeyFollowUpDrafts.TryGetValue(draftKey, out Dictionary<string, string>? drafts);
                foreach (LifeModuleFollowUpPromptDto prompt in option.FollowUps)
                {
                    body.Children.Add(Caption(prompt.Label + (prompt.IsRequired ? " (required)" : "")));
                    if (prompt.Options.Count > 0)
                    {
                        var choice = new ComboBox
                        {
                            ItemsSource = prompt.Options.Where(o => o.IsEnabled).Select(o => new ComboBoxItem
                            { Content = o.Label, Tag = o.SourceValue }).ToArray(),
                            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
                        };
                        AutomationProperties.SetAutomationId(choice, $"creation-journey-input-{prompt.PromptId}");
                        if (drafts is not null && drafts.TryGetValue(prompt.PromptId, out string? draftChoice))
                        {
                            choice.SelectedItem = ((IEnumerable<ComboBoxItem>)choice.ItemsSource!)
                                .FirstOrDefault(item => string.Equals(item.Tag as string, draftChoice, StringComparison.Ordinal));
                        }
                        readers[prompt.PromptId] = () => (choice.SelectedItem as ComboBoxItem)?.Tag as string;
                        choice.SelectionChanged += (_, _) => InvalidateJourneyPreviewOnEdit();
                        body.Children.Add(choice);
                    }
                    else
                    {
                        var input = new TextBox { Watermark = prompt.Label };
                        AutomationProperties.SetAutomationId(input, $"creation-journey-input-{prompt.PromptId}");
                        if (drafts is not null && drafts.TryGetValue(prompt.PromptId, out string? draftText))
                            input.Text = draftText;
                        readers[prompt.PromptId] = () => input.Text;
                        input.PropertyChanged += (_, args) =>
                        {
                            if (args.Property == TextBox.TextProperty)
                                InvalidateJourneyPreviewOnEdit();
                        };
                        body.Children.Add(input);
                    }
                }
                // The readers survive the render so the next re-render can
                // harvest typing before the inputs are rebuilt.
                _journeyFollowUpReaders[draftKey] = readers;
                optionButton.IsEnabled = option.IsEnabled && !_journeyMutationBusy;
                optionButton.Click += (_, _) =>
                {
                    if (_journeyMutationBusy || !option.IsEnabled) return;
                    var values = readers.Select(pair => (pair.Key, Value: pair.Value()))
                        .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                        .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);
                    JourneyOptionClickRequested?.Invoke(this, new CharacterCreationJourneyOptionClickRequested(option, values));
                };
                body.Children.Add(optionButton);
                card.Child = body;
            }
            else
            {
                optionButton.Click += FoundationOptionButton_OnClick;
                card.Child = optionButton;
            }
            LegalOptionList.Children.Add(card);
        }

        RenderFoundationPreview(options);
        RenderResourcesPreview();
        if (journey)
            RenderJourneyPreviewPanel();
    }

    private void RenderJourneyStepHeader()
    {
        if (_state?.JourneyStep is not { } journey)
            return;
        LegalOptionList.Children.Add(Caption($"Life module stage {journey.CurrentStageOrder} of 5: {journey.CurrentStageLabel}"));
        if (_journeyMutationBusy)
            LegalOptionList.Children.Add(new ProgressBar { IsIndeterminate = true });
        if (!string.IsNullOrWhiteSpace(_journeyMutationStatus))
            LegalOptionList.Children.Add(Caption(_journeyMutationStatus));
        if (journey.SelectionFinished)
        {
            LegalOptionList.Children.Add(Caption(
                "Your life module selection is finished. The origin story follows in the character overview."));
            return;
        }
        if (journey.CanFinishSelection && journey.CurrentStageOrder == 5
            && _journeyFinishPreview is not { } finishPreview)
        {
            Button finishButton = new() { Content = "Review and finish life module selection\u2026", IsEnabled = !_journeyMutationBusy };
            AutomationProperties.SetAutomationId(finishButton, "creation-wizard-journey-finish");
            finishButton.Click += (_, finishArgs) =>
            {
                if (!_journeyMutationBusy)
                    JourneyFinishRequested?.Invoke(this, new CharacterCreationJourneyFinishRequested());
            };
            LegalOptionList.Children.Add(finishButton);
        }
        if (journey.CanFinishSelection && journey.CurrentStageOrder == 5
            && _journeyFinishPreview is { } finish)
        {
            Border finishCard = new() { Classes = { "shell-card", "subtle" }, Padding = new global::Avalonia.Thickness(9) };
            finishCard.Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Exact Core finish preview", Classes = { "shell-section-title" } },
                    Caption($"Source anchors: {finish.SourceAnchorIds.Count}"),
                    finish.CanConfirm && finish.Blockers.Count == 0
                        ? Caption("Selection complete; confirm to finish the journey against this exact revision.")
                        : Caption($"Blocked: {string.Join(", ", finish.Blockers)}")
                }
            };
            Button finishConfirm = new()
            {
                Content = "Confirm and finish selection",
                IsEnabled = finish.CanConfirm && finish.Blockers.Count == 0 && !_journeyMutationBusy
            };
            AutomationProperties.SetAutomationId(finishConfirm, "creation-wizard-journey-finish-confirm");
            finishConfirm.Click += (_, _) =>
            {
                if (_journeyFinishPreview is { } held
                    && held.CanConfirm && held.Blockers.Count == 0
                    && !_journeyMutationBusy)
                {
                    JourneyFinishConfirmRequested?.Invoke(this, new CharacterCreationJourneyFinishConfirmRequested(held.PreviewDigest));
                }
            };
            finishCard.Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    (finishCard.Child as StackPanel)!,
                    finishConfirm
                }
            };
            LegalOptionList.Children.Add(finishCard);
        }
    }


    /// <summary>
    /// Reads the live journey follow-up inputs (the per-option reader lambdas
    /// captured during the last card render) into the draft map. Called
    /// before every re-render of the legal-option cards while a journey step
    /// is active so typing survives busy/status re-renders.
    /// </summary>
    private void HarvestJourneyFollowUpDrafts()
    {
        foreach (var (key, readers) in _journeyFollowUpReaders)
        {
            Dictionary<string, string> captured = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Func<string?>> pair in readers)
            {
                if (pair.Value() is { } value)
                    captured[pair.Key] = value;
            }
            _journeyFollowUpDrafts[key] = captured;
        }
    }

    /// <summary>
    /// Clears the prepared preview when the player edits a follow-up input
    /// after a preview was prepared: the preview described the values at
    /// prepare time, so editing without invalidating would let the confirm
    /// gate commit a stale projection (audit ui019/T292).
    /// </summary>
    private void InvalidateJourneyPreviewOnEdit()
    {
        if (_journeyPreview is null)
            return;
        _journeyPreview = null;
        _journeyMutationStatus =
            "Life module preview cleared - adjust the follow-up answers and prepare again.";
        // Retire only the confirmation surface. Rebuilding every editor here
        // would discard the active control and interrupt typing after one key.
        if (_journeyPreviewPanel is { } panel)
            panel.Child = Caption(_journeyMutationStatus);
    }

    private void RenderJourneyPreviewPanel()
    {
        if (_journeyPreview is not { } preview)
            return;
        Border panel = new() { Classes = { "shell-card", "subtle" }, Padding = new global::Avalonia.Thickness(9) };
        StackPanel body = new() { Spacing = 4 };
        body.Children.Add(new TextBlock { Text = "Exact Core life module preview", Classes = { "shell-section-title" } });
        body.Children.Add(Caption($"Stage {preview.StageOrder}: {preview.Entry.StoryTemplate}"));
        foreach (LifeModuleEffectProjectionDto effect in preview.Entry.ProjectedEffects)
            body.Children.Add(Caption($"Effect {effect.EffectId} ({effect.Domain}): {effect.TargetId}"));
        foreach (LifeModuleRequirementProjectionDto requirement in preview.Entry.RequirementEvaluations)
            body.Children.Add(Caption($"Requirement {requirement.RequirementId}: {(requirement.IsMet ? "met" : "unmet")}"));
        body.Children.Add(Caption($"Karma before: {preview.BudgetBefore.Remaining:0} \u2022 after: {preview.BudgetAfter.Remaining:0}"));
        if (preview.AuthorityBlockers.Count > 0)
            body.Children.Add(Caption($"Authority: {string.Join(", ", preview.AuthorityBlockers)}"));
        body.Children.Add(preview.CanConfirm && preview.AuthorityBlockers.Count == 0
            ? Caption("Preview is legal; confirm to commit against this exact revision.")
            : Caption($"Blocked: {string.Join(", ", preview.AuthorityBlockers.Count > 0 ? preview.AuthorityBlockers : ["preview not confirmable"])}"));
        if (!string.IsNullOrWhiteSpace(_journeyMutationStatus))
            body.Children.Add(Caption(_journeyMutationStatus));
        Button confirm = new()
        {
            Content = "Confirm and apply life module",
            IsEnabled = preview.CanConfirm && preview.AuthorityBlockers.Count == 0 && !_journeyMutationBusy
        };
        AutomationProperties.SetAutomationId(confirm, "creation-wizard-journey-confirm");
        confirm.Click += JourneyConfirmButton_OnClick;
        body.Children.Add(confirm);
        panel.Child = body;
        _journeyPreviewPanel = panel;
        LegalOptionList.Children.Add(panel);
    }

    private void JourneyOptionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CharacterCreationWizardDesktopOption option })
            return;
        if (!option.IsEnabled)
            return;
        JourneyOptionClickRequested?.Invoke(this, new CharacterCreationJourneyOptionClickRequested(option));
    }

    private void JourneyConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_journeyPreview is { } preview)
            JourneyConfirmRequested?.Invoke(this, new CharacterCreationJourneyConfirmRequested(preview.PreviewDigest));
    }

    private void RenderResourcesPreview()
    {
        if (_state?.ActiveStepId != CharacterCreationWizardStepIds.Resources)
            return;
        if (!string.IsNullOrWhiteSpace(_resourcesMutationStatus))
            LegalOptionList.Children.Add(Caption(_resourcesMutationStatus));
        if (_resourcesPreview is null)
            return;
        CharacterCreationResourcesPreparedPreview prepared = _resourcesPreview;
        CharacterCreationResourcesBudget after = prepared.BudgetAfter;
        Border panel = new() { Classes = { "shell-card", "subtle" }, Padding = new global::Avalonia.Thickness(9) };
        panel.Child = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Exact Core resources preview", Classes = { "shell-section-title" } },
                Caption($"Total starting nuyen: {after.TotalStartingNuyen:0}¥ · remaining: {after.RemainingNuyen:0}¥"),
                prepared.Blockers.Count == 0
                    ? Caption("Preview is legal; confirm to commit against this exact revision.")
                    : Caption($"Blocked: {string.Join(", ", prepared.Blockers)}"),
            }
        };
        Button confirmButton = new()
        {
            Content = "Confirm resources",
            IsEnabled = prepared.CanConfirm && !_resourcesMutationBusy,
        };
        AutomationProperties.SetAutomationId(confirmButton, "creation-wizard-resources-confirm");
        confirmButton.Classes.Add("shell-action");
        confirmButton.Click += ResourcesConfirmButton_OnClick;

        LegalOptionList.Children.Add(panel);
        LegalOptionList.Children.Add(confirmButton);
    }

    private string BuildLifestyleResultStatus(string outcome, IReadOnlyList<string> blockers)
    {
        string status = outcome switch
        {
            "applied" => "Lifestyle change applied.",
            "replayed" => "Lifestyle change was already applied (replayed receipt).",
            "unavailable" => "The lifestyle result is unavailable. Check the saved outcome before trying another change.",
            "invalid" => "The lifestyle change was rejected as invalid.",
            "conflict" => "The lifestyle change conflicts with the current authority.",
            _ => $"Lifestyle change outcome: {outcome}."
        };
        return blockers is { Count: > 0 }
            ? $"{status} Blockers: {string.Join(", ", blockers)}"
            : status;
    }

    private void RenderLifestyles(CharacterCreationWizardDesktopLifestylesStep? step)
    {
        HarvestDraftsBeforeRender(contacts: false, lifestyles: true);
        LifestylesList.Children.Clear();
        LifestylesList.IsVisible = step is not null;
        if (step is null)
            return;

        string authority = step.CanEdit
            ? "Core can preview this exact revision; every change still requires explicit confirmation."
            : "Editing is blocked by Core authority for this exact revision.";
        LifestylesList.Children.Add(Caption(authority));
        if (step.Blockers.Count > 0)
            LifestylesList.Children.Add(Caption($"Blockers: {string.Join(", ", step.Blockers)}"));
        if (!string.IsNullOrWhiteSpace(_lifestyleMutationStatus))
            LifestylesList.Children.Add(Caption(_lifestyleMutationStatus));
        if (step.Lifestyles.Count == 0)
            LifestylesList.Children.Add(Caption(
                "No lifestyle is purchased yet. Use the add-lifestyle form below."));

        if (step.CanEdit
            && step.Authority.LifestyleOptions.Count > 0
            && !_lifestyleMutationBusy)
        {
            LifestylesList.Children.Add(BuildLifestyleCreateForm(step));
        }

        foreach (CharacterCreationWizardDesktopLifestyle lifestyle in step.Lifestyles)
        {
            LifestylesList.Children.Add(BuildLifestyleCard(lifestyle, step.CanEdit));
        }

        RenderLifestylePreview();
    }

    private Control BuildLifestyleCreateForm(CharacterCreationWizardDesktopLifestylesStep step)
    {
        StackPanel form = new() { Spacing = 3 };
        form.Children.Add(new TextBlock
        {
            Text = "Add a lifestyle",
            Classes = { "shell-section-title" }
        });
        form.Children.Add(Caption(
            $"Base-lifestyle options: {step.Authority.LifestyleOptions.Count}"
            + $", budget remaining: {step.Budget.Remaining.ToString("0.##", CultureInfo.InvariantCulture)}"
            + (step.Budget.Blockers.Count > 0
                ? $" · budget blockers: {string.Join(", ", step.Budget.Blockers)}"
                : string.Empty)));

        // Draft preservation: a pending create draft (kept across previews) seeds
        // the form so the player's typing and the minted identity survive.
        CharacterCreationLifestyleConfiguration? createDraft = _lifestyleCreateDraft;
        ComboBox basePicker = new()
        {
            MinWidth = 260,
            IsEnabled = !_lifestyleMutationBusy
        };
        foreach (CharacterCreationLifestyleCatalogOption option in step.Authority.LifestyleOptions
                     .Where(option => option.IsSelectable)
                     .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase))
        {
            basePicker.Items.Add(new ComboBoxItem
            {
                Content = $"{option.Name} ({option.BaseCost.ToString("0.##", CultureInfo.InvariantCulture)} per {option.DefaultIncrementId})",
                Tag = option
            });
        }
        int draftBaseIndex = createDraft is null
            ? -1
            : basePicker.Items.OfType<ComboBoxItem>().ToList().FindIndex(item =>
                item.Tag is CharacterCreationLifestyleCatalogOption draftOption
                && string.Equals(
                    draftOption.OptionId,
                    createDraft.BaseLifestyleOptionId,
                    StringComparison.Ordinal));
        if (draftBaseIndex >= 0)
            basePicker.SelectedIndex = draftBaseIndex;
        else if (basePicker.Items.Count > 0)
            basePicker.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(basePicker, "creation-wizard-lifestyle-create-base");
        form.Children.Add(Caption("Base lifestyle"));
        basePicker.SelectionChanged += (_, _) => RefreshPreviewBindingGates();
        form.Children.Add(basePicker);

        TextBox nameBox = new()
        {
            Text = createDraft?.Name ?? string.Empty,
            Watermark = "Lifestyle name",
            IsEnabled = !_lifestyleMutationBusy
        };
        AutomationProperties.SetAutomationId(nameBox, "creation-wizard-lifestyle-create-name");
        nameBox.TextChanged += (_, _) => RefreshPreviewBindingGates();
        form.Children.Add(Caption("Name"));
        form.Children.Add(nameBox);

        ComboBox stylePicker = new() { IsEnabled = !_lifestyleMutationBusy };
        foreach (string style in CharacterCreationLifestyleStyleIds.All)
        {
            stylePicker.Items.Add(new ComboBoxItem { Content = style, Tag = style });
        }
        stylePicker.SelectedIndex = Math.Max(0, CharacterCreationLifestyleStyleIds.All
            .ToList()
            .FindIndex(style => string.Equals(style, createDraft?.StyleId, StringComparison.Ordinal)));
        AutomationProperties.SetAutomationId(stylePicker, "creation-wizard-lifestyle-create-style");
        form.Children.Add(Caption("Style"));
        stylePicker.SelectionChanged += (_, _) => RefreshPreviewBindingGates();
        form.Children.Add(stylePicker);

        ComboBox incrementPicker = new() { IsEnabled = !_lifestyleMutationBusy };
        foreach (string increment in CharacterCreationLifestyleIncrementIds.All)
        {
            incrementPicker.Items.Add(new ComboBoxItem { Content = increment, Tag = increment });
        }
        incrementPicker.SelectedIndex = createDraft is null ? 2 : Math.Max(0, CharacterCreationLifestyleIncrementIds.All
            .ToList()
            .FindIndex(increment => string.Equals(increment, createDraft?.IncrementId, StringComparison.Ordinal)));
        AutomationProperties.SetAutomationId(incrementPicker, "creation-wizard-lifestyle-create-increment");
        form.Children.Add(Caption("Billing increment"));
        incrementPicker.SelectionChanged += (_, _) => RefreshPreviewBindingGates();
        form.Children.Add(incrementPicker);

        StackPanel numeric = new() { Spacing = 3 };
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-increments", "Increments", createDraft?.Increments ?? 1, 0, 36));
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-percentage", "Percentage", createDraft?.Percentage ?? 100, 0, 100));
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-roommates", "Roommates", createDraft?.Roommates ?? 0, 0, 10));
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-area", "Area", createDraft?.Area ?? 0, 0, 100));
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-comforts", "Comforts", createDraft?.Comforts ?? 0, 0, 100));
        numeric.Children.Add(BuildLifestyleNumericEditor(
            "creation-wizard-lifestyle-create-security", "Security", createDraft?.Security ?? 0, 0, 100));
        form.Children.Add(numeric);

        CheckBox splitCost = new()
        {
            Content = "Split cost with roommates",
            IsChecked = createDraft?.SplitCostWithRoommates == true,
            IsEnabled = !_lifestyleMutationBusy
        };
        AutomationProperties.SetAutomationId(splitCost, "creation-wizard-lifestyle-create-split");
        splitCost.IsCheckedChanged += (_, _) => RefreshPreviewBindingGates();
        form.Children.Add(splitCost);

        Button previewButton = new()
        {
            Content = "Preview new lifestyle",
            IsEnabled = true,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        previewButton.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(previewButton, "creation-wizard-lifestyle-create-preview");
        previewButton.Click += LifestyleCreatePreviewButton_OnClick;
        form.Children.Add(previewButton);

        Border card = new()
        {
            Classes = { "shell-card", "subtle" },
            Padding = new global::Avalonia.Thickness(9),
            Child = form
        };
        AutomationProperties.SetAutomationId(card, "creation-wizard-lifestyle-create");
        return card;
    }

    private Control BuildLifestyleNumericEditor(string automationId, string label, decimal defaultValue, decimal minimum, decimal maximum)
    {
        StackPanel row = new() { Spacing = 2 };
        row.Children.Add(Caption(label));
        NumericUpDown editor = new()
        {
            Value = defaultValue,
            Minimum = minimum,
            Maximum = maximum,
            AllowSpin = true,
            IsEnabled = !_lifestyleMutationBusy
        };
        AutomationProperties.SetAutomationId(editor, automationId);
        editor.ValueChanged += (_, _) => RefreshPreviewBindingGates();
        row.Children.Add(editor);
        return row;
    }

    private Control BuildLifestyleCard(
        CharacterCreationWizardDesktopLifestyle lifestyle,
        bool canEdit)
    {
        CharacterCreationLifestyleConfiguration configuration = lifestyle.Configuration;
        StackPanel fields = new() { Spacing = 3 };
        fields.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(configuration.Name)
                ? configuration.LifestyleId.ToString("D")
                : configuration.Name,
            Classes = { "shell-section-title" }
        });
        fields.Children.Add(Caption(
            $"{lifestyle.BaseLifestyleName} · {configuration.StyleId} · {configuration.IncrementId}"
            + $" · {lifestyle.Economics.TotalCost.ToString("0.##", CultureInfo.InvariantCulture)} nuyen"
            + $" · increments {configuration.Increments}"));
        if (lifestyle.Economics.Blockers.Count > 0)
            fields.Children.Add(Caption(
                $"Economics blockers: {string.Join(", ", lifestyle.Economics.Blockers)}"));
        fields.Children.Add(Caption(
            "Trust fund covered: " + (lifestyle.Economics.CoveredByTrustFund ? "yes" : "no")
            + $" · lifestyle points {lifestyle.Economics.LifestylePointsTotal}"));

        string prefix = $"creation-wizard-lifestyle-{configuration.LifestyleId:D}";
        CharacterCreationLifestyleConfiguration draft = MergeLifestyleDraft(
            configuration.LifestyleId,
            _lifestyleEditDrafts.TryGetValue(configuration.LifestyleId, out CharacterCreationLifestyleConfiguration? storedDraft)
                ? storedDraft
                : configuration,
            configuration);
        if (storedDraft is not null)
        {
            // Persist the merged view and roll the base forward to the saved
            // values this render was seeded from (audit win011 P2): typing
            // after this render must be compared against the CURRENT saved
            // configuration, never the pre-refresh base, or an edit made in a
            // previously untouched field would be classified "unchanged" and
            // silently reverted to the refreshed saved value.
            _lifestyleEditDrafts[configuration.LifestyleId] = draft;
            _lifestyleEditDraftBases[configuration.LifestyleId] = configuration;
        }
        if (_lifestyleMergeConflictNotes.Remove(configuration.LifestyleId, out string? conflictNote))
        {
            fields.Children.Add(Caption(conflictNote));
        }
        TextBox name = new() { Text = draft.Name, IsEnabled = canEdit && !_lifestyleMutationBusy };
        AutomationProperties.SetAutomationId(name, prefix + "-name");
        name.TextChanged += (_, _) => RefreshPreviewBindingGates();
        fields.Children.Add(Caption("Name"));
        fields.Children.Add(name);
        var editFields = new StackPanel { IsEnabled = canEdit && !_lifestyleMutationBusy, Spacing = 3 };
        editFields.Children.Add(BuildLifestyleNumericEditor(prefix + "-increments", "Billing increments", draft.Increments, 0, int.MaxValue));
        editFields.Children.Add(BuildLifestyleNumericEditor(prefix + "-percentage", "Cost percentage", draft.Percentage, Math.Min(0, draft.Percentage), decimal.MaxValue));
        editFields.Children.Add(BuildLifestyleNumericEditor(prefix + "-roommates", "Roommates", draft.Roommates, 0, int.MaxValue));
        CheckBox split = new() { Content = "Split cost with roommates", IsChecked = draft.SplitCostWithRoommates };
        AutomationProperties.SetAutomationId(split, prefix + "-split");
        split.IsCheckedChanged += (_, _) => RefreshPreviewBindingGates();
        editFields.Children.Add(split);
        fields.Children.Add(editFields);

        Button editButton = new()
        {
            Content = "Preview changes",
            Tag = lifestyle,
            IsEnabled = canEdit && !_lifestyleMutationBusy,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        editButton.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(
            editButton,
            $"creation-wizard-lifestyle-{configuration.LifestyleId:D}-preview");
        editButton.Click += LifestyleEditPreviewButton_OnClick;
        fields.Children.Add(editButton);

        Button deleteButton = new()
        {
            Content = "Delete lifestyle",
            Tag = lifestyle,
            IsEnabled = canEdit && !_lifestyleMutationBusy,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        deleteButton.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(
            deleteButton,
            $"creation-wizard-lifestyle-{configuration.LifestyleId:D}-delete");
        deleteButton.Click += LifestyleDeletePreviewButton_OnClick;
        fields.Children.Add(deleteButton);

        Border card = new()
        {
            Classes = { "shell-card", "subtle" },
            Padding = new global::Avalonia.Thickness(9),
            Child = fields
        };
        AutomationProperties.SetAutomationId(
            card,
            $"creation-wizard-lifestyle-{configuration.LifestyleId:D}");
        return card;
    }

    private static CharacterCreationLifestyleConfiguration BuildConfigurationFromForm(
        Guid lifestyleId,
        string baseLifestyleOptionId,
        string name,
        string styleId,
        string incrementId,
        int increments,
        decimal percentage,
        int roommates,
        int area,
        int comforts,
        int security,
        bool splitCost)
    {
        return new CharacterCreationLifestyleConfiguration(
            lifestyleId,
            baseLifestyleOptionId,
            name,
            styleId,
            incrementId,
            increments,
            percentage,
            roommates,
            splitCost,
            TrustFund: false,
            area,
            comforts,
            security,
            BonusLifestylePoints: 0,
            City: string.Empty,
            District: string.Empty,
            Borough: string.Empty,
            Qualities: []);
    }

    private void RenderLifestylePreview()
    {
        if (_lifestylePreview is not { } prepared)
            return;
        string text = $"Pending lifestyle change: {prepared.Mutation.MutationKind}.";
        if (prepared.Before is { } before)
            text += $" Before: {before.Configuration.Name}, {before.Configuration.Increments} increments, {before.Configuration.Percentage}% cost, {before.Configuration.Roommates} roommates, split cost: {before.Configuration.SplitCostWithRoommates}.";
        if (prepared.After is { } after)
        {
            text += $" {after.Configuration.Name} costs "
                + $"{after.Economics.TotalCost.ToString("0.##", CultureInfo.InvariantCulture)} nuyen.";
            text += $" After: {after.Configuration.Increments} increments, {after.Configuration.Percentage}% cost, {after.Configuration.Roommates} roommates, split cost: {after.Configuration.SplitCostWithRoommates}.";
        }
        text += $" Budget remaining: {prepared.BudgetBefore.Remaining:0.##} → {prepared.BudgetAfter.Remaining:0.##} nuyen.";
        bool lifestyleMatchesVisibleEditors = LifestylePreviewMatchesVisibleEditors(prepared);
        // Review-first layout: the player reads the affected entity, the changes
        // and the budget effect BEFORE the confirmation action is offered.
        LifestylesList.Children.Add(Caption(text));
        if (prepared.CanConfirm)
        {
            if (!lifestyleMatchesVisibleEditors)
            {
                LifestylesList.Children.Add(Caption(
                    "Values changed since this preview — preview again to confirm the new proposal."));
            }
            Button confirm = new()
            {
                Content = $"Confirm {prepared.Mutation.MutationKind}",
                IsEnabled = lifestyleMatchesVisibleEditors && !_lifestyleMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            confirm.Classes.Add("shell-action");
            AutomationProperties.SetAutomationId(confirm, "creation-wizard-lifestyle-confirm");
            confirm.Click += LifestyleConfirmButton_OnClick;
            LifestylesList.Children.Add(confirm);
        }
        else
        {
            LifestylesList.Children.Add(Caption(
                prepared.Blockers.Count > 0
                    ? $"Blocked: {string.Join(", ", prepared.Blockers)}"
                    : "This proposal cannot be confirmed yet."));
        }
    }

    private void LifestyleCreatePreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state?.LifestylesStep is not { CanEdit: true } step || _lifestyleMutationBusy)
            return;
        CharacterCreationLifestyleCatalogOption? option =
            (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-base")?.SelectedItem
                as ComboBoxItem)?.Tag as CharacterCreationLifestyleCatalogOption;
        if (option is null)
        {
            SetLifestylePrepareResult(new CharacterCreationLifestylesInteractionPrepareResult(
                "invalid", null, null, ["No base lifestyle option is selected."]));
            return;
        }

        string name = FindLifestyleEditor<TextBox>("creation-wizard-lifestyle-create-name")?.Text
            ?? string.Empty;
        string styleId = (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-style")?.SelectedItem
            as ComboBoxItem)?.Tag as string ?? CharacterCreationLifestyleStyleIds.Standard;
        string incrementId = (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-increment")?.SelectedItem
            as ComboBoxItem)?.Tag as string ?? CharacterCreationLifestyleIncrementIds.Month;
        int increments = ReadLifestyleNumeric("creation-wizard-lifestyle-create-increments", 1);
        decimal percentage = FindLifestyleEditor<NumericUpDown>("creation-wizard-lifestyle-create-percentage")?.Value ?? 100;
        int roommates = ReadLifestyleNumeric("creation-wizard-lifestyle-create-roommates", 0);
        int area = ReadLifestyleNumeric("creation-wizard-lifestyle-create-area", 0);
        int comforts = ReadLifestyleNumeric("creation-wizard-lifestyle-create-comforts", 0);
        int security = ReadLifestyleNumeric("creation-wizard-lifestyle-create-security", 0);
        bool splitCost = FindLifestyleEditor<CheckBox>("creation-wizard-lifestyle-create-split")?.IsChecked == true;

        // Draft preservation: reuse the identity minted for this create cycle so
        // repeated previews describe the same new lifestyle.
        if (_lifestyleCreateDraftIdentity is not { } mintedLifestyleId)
        {
            mintedLifestyleId = Guid.NewGuid();
            _lifestyleCreateDraftIdentity = mintedLifestyleId;
        }
        Guid lifestyleId = mintedLifestyleId;
        CharacterCreationLifestyleConfiguration configuration = BuildConfigurationFromForm(
            lifestyleId,
            option.OptionId,
            name,
            styleId,
            incrementId,
            increments,
            percentage,
            roommates,
            area,
            comforts,
            security,
            splitCost);
        _lifestyleCreateDraft = configuration;
        _lifestylePreviewBaseline = configuration;
        _lifestylePreviewBaselineId = configuration.LifestyleId;
        CharacterCreationLifestyleMutationInput input = new(
            CharacterCreationLifestyleMutationKinds.Create,
            configuration.LifestyleId,
            configuration);
        LifestylePreviewRequested?.Invoke(this, new CharacterCreationLifestylePreviewRequested(input));
    }

    private void LifestyleEditPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_lifestyleMutationBusy || _state?.LifestylesStep is not { CanEdit: true } step)
            return;
        if ((sender as Button)?.Tag is not CharacterCreationWizardDesktopLifestyle lifestyle)
            return;
        if (!step.Lifestyles.Contains(lifestyle)) return;
        string prefix = $"creation-wizard-lifestyle-{lifestyle.Configuration.LifestyleId:D}";
        CharacterCreationLifestyleConfiguration edited = lifestyle.Configuration with
        {
            Name = FindLifestyleEditor<TextBox>(prefix + "-name")?.Text ?? lifestyle.Configuration.Name,
            Increments = ReadLifestyleNumeric(prefix + "-increments", lifestyle.Configuration.Increments),
            Percentage = FindLifestyleEditor<NumericUpDown>(prefix + "-percentage")?.Value ?? lifestyle.Configuration.Percentage,
            Roommates = ReadLifestyleNumeric(prefix + "-roommates", lifestyle.Configuration.Roommates),
            SplitCostWithRoommates = FindLifestyleEditor<CheckBox>(prefix + "-split")?.IsChecked ?? lifestyle.Configuration.SplitCostWithRoommates
        };
        CharacterCreationLifestyleMutationInput input = new(
            CharacterCreationLifestyleMutationKinds.Edit,
            lifestyle.Configuration.LifestyleId,
            edited);
        // Draft preservation + review binding for the edit cycle. The base is
        // the saved configuration the editors were seeded from, so unchanged
        // fields keep accepting refreshed saved values (audit win010).
        _lifestyleEditDrafts[lifestyle.Configuration.LifestyleId] = edited;
        _lifestyleEditDraftBases[lifestyle.Configuration.LifestyleId] = lifestyle.Configuration;
        _lifestylePreviewBaseline = edited;
        _lifestylePreviewBaselineId = edited.LifestyleId;
        LifestylePreviewRequested?.Invoke(this, new CharacterCreationLifestylePreviewRequested(input));
    }

    private void LifestyleDeletePreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_lifestyleMutationBusy || _state?.LifestylesStep is not { CanEdit: true } step)
            return;
        if ((sender as Button)?.Tag is not CharacterCreationWizardDesktopLifestyle lifestyle)
            return;
        if (!step.Lifestyles.Contains(lifestyle)) return;
        CharacterCreationLifestyleMutationInput input = new(
            CharacterCreationLifestyleMutationKinds.Delete,
            lifestyle.Configuration.LifestyleId,
            null);
        _lifestyleEditDrafts.Remove(lifestyle.Configuration.LifestyleId);
        _lifestyleEditDraftBases.Remove(lifestyle.Configuration.LifestyleId);
        LifestylePreviewRequested?.Invoke(this, new CharacterCreationLifestylePreviewRequested(input));
    }

    private void LifestyleConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_lifestylePreview is not { CanConfirm: true } prepared || _lifestyleMutationBusy)
            return;
        if (!LifestylePreviewMatchesVisibleEditors(prepared))
            return;
        LifestyleConfirmRequested?.Invoke(this, new CharacterCreationLifestyleConfirmRequested(
            prepared.PreviewDigest));
    }

    /// <summary>Captures the current editor values for the given contact editor
    /// keys. Used by the draft store so previews never discard player input.</summary>
    private Dictionary<string, string> CaptureContactEditorValues(
        Guid contactId,
        IEnumerable<string> fieldIds)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string fieldId in fieldIds)
        {
            values[fieldId] = ReadContactEditor(contactId, fieldId);
        }
        return values;
    }

    /// <summary>Review-binding invariant: the values visibly being approved must
    /// be the values bound into the prepared operation. Compares the live editors
    /// against the baseline captured when the preview was requested.</summary>
    private bool ContactPreviewMatchesVisibleEditors(Guid contactId)
    {
        if (_contactPreviewBaseline is null || _contactPreviewBaselineId != contactId)
            return true;
        if (_contactPreview?.Edit.ChangeKind == CharacterCreationContactChangeKind.Remove)
            return true;
        Guid editorId = contactId == _contactAddDraftIdentity ? Guid.Empty : contactId;
        foreach ((string fieldId, string baselineValue) in _contactPreviewBaseline)
        {
            string liveValue = ReadContactEditor(editorId, fieldId);
            if (!string.Equals(liveValue, baselineValue, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>Review-binding invariant for lifestyles: compares the visible
    /// editors against the configuration captured when the preview was requested.
    /// A mismatch means the player changed values after previewing; confirmation
    /// must wait for a fresh preview of the new proposal.</summary>
    private bool LifestylePreviewMatchesVisibleEditors(
        CharacterCreationLifestylePreparedPreview prepared)
    {
        Guid lifestyleId = prepared.Mutation.LifestyleId;
        if (_lifestylePreviewBaseline is null || _lifestylePreviewBaselineId != lifestyleId)
            return true;
        CharacterCreationLifestyleConfiguration baseline = _lifestylePreviewBaseline;
        if (prepared.Mutation.MutationKind == CharacterCreationLifestyleMutationKinds.Delete)
            return true;

        if (FindLifestyleEditor<TextBox>($"creation-wizard-lifestyle-{lifestyleId:D}-name") is { } nameBox
            && !string.Equals(nameBox.Text, baseline.Name, StringComparison.Ordinal))
            return false;
        if (ReadLifestyleNumeric($"creation-wizard-lifestyle-{lifestyleId:D}-increments", baseline.Increments) != baseline.Increments)
            return false;
        if ((FindLifestyleEditor<NumericUpDown>($"creation-wizard-lifestyle-{lifestyleId:D}-percentage")?.Value
                ?? baseline.Percentage) != baseline.Percentage)
            return false;
        if (ReadLifestyleNumeric($"creation-wizard-lifestyle-{lifestyleId:D}-roommates", baseline.Roommates) != baseline.Roommates)
            return false;
        if ((FindLifestyleEditor<CheckBox>($"creation-wizard-lifestyle-{lifestyleId:D}-split")?.IsChecked
                ?? baseline.SplitCostWithRoommates) != baseline.SplitCostWithRoommates)
            return false;

        if (prepared.Mutation.MutationKind == CharacterCreationLifestyleMutationKinds.Create)
        {
            if (!string.Equals(FindLifestyleEditor<TextBox>("creation-wizard-lifestyle-create-name")?.Text ?? string.Empty,
                    baseline.Name, StringComparison.Ordinal))
                return false;
            if (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-base")?.SelectedItem
                    is not ComboBoxItem { Tag: CharacterCreationLifestyleCatalogOption option }
                || !string.Equals(option.OptionId, baseline.BaseLifestyleOptionId, StringComparison.Ordinal))
                return false;
            if (!string.Equals((FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-style")?.SelectedItem
                    as ComboBoxItem)?.Tag as string, baseline.StyleId, StringComparison.Ordinal)
                || !string.Equals((FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-increment")?.SelectedItem
                    as ComboBoxItem)?.Tag as string, baseline.IncrementId, StringComparison.Ordinal))
                return false;
            if (ReadLifestyleNumeric("creation-wizard-lifestyle-create-increments", baseline.Increments) != baseline.Increments)
                return false;
            if ((FindLifestyleEditor<NumericUpDown>("creation-wizard-lifestyle-create-percentage")?.Value
                    ?? baseline.Percentage) != baseline.Percentage)
                return false;
            if (ReadLifestyleNumeric("creation-wizard-lifestyle-create-roommates", baseline.Roommates) != baseline.Roommates)
                return false;
            if (ReadLifestyleNumeric("creation-wizard-lifestyle-create-area", baseline.Area) != baseline.Area)
                return false;
            if (ReadLifestyleNumeric("creation-wizard-lifestyle-create-comforts", baseline.Comforts) != baseline.Comforts)
                return false;
            if (ReadLifestyleNumeric("creation-wizard-lifestyle-create-security", baseline.Security) != baseline.Security)
                return false;
            if ((FindLifestyleEditor<CheckBox>("creation-wizard-lifestyle-create-split")?.IsChecked
                    ?? baseline.SplitCostWithRoommates) != baseline.SplitCostWithRoommates)
                return false;
        }
        return true;
    }

    /// <summary>Live re-evaluation of the review-binding gates (audit win008):
    /// when any contact or lifestyle editor value changes while a preview is
    /// pending, the confirm buttons are re-enabled or disabled so the values
    /// visibly approved stay equal to the values bound into the prepared
    /// operation. Contact confirm buttons carry a per-contact AutomationId;
    /// the lifestyle confirm button shares one id.</summary>
    private void RefreshPreviewBindingGates()
    {
        if (_contactPreview is { } contactPreview)
        {
            if (FindDescendantByAutomationId<Button>(
                    ContactsList,
                    $"creation-wizard-contact-{contactPreview.Edit.ContactId:D}-confirm") is { } contactConfirm)
            {
                bool contactMatches = contactPreview.RequiresExplicitConfirmation
                    && contactPreview.CanConfirm
                    && contactPreview.Blockers.Count == 0
                    && ContactPreviewMatchesVisibleEditors(contactPreview.Edit.ContactId);
                contactConfirm.IsEnabled = contactMatches && !_contactMutationBusy;
                contactConfirm.Opacity = contactMatches ? 1.0 : 0.55;
            }
        }

        if (_lifestylePreview is { } lifestylePreview)
        {
            if (FindDescendantByAutomationId<Button>(
                    LifestylesList, "creation-wizard-lifestyle-confirm") is { } lifestyleConfirm)
            {
                bool lifestyleMatches = LifestylePreviewMatchesVisibleEditors(lifestylePreview);
                lifestyleConfirm.IsEnabled = lifestyleMatches && !_lifestyleMutationBusy;
                lifestyleConfirm.Opacity = lifestyleMatches ? 1.0 : 0.55;
            }
        }
    }

    /// <summary>Finds a descendant control by AutomationId within the given
    /// visual root. Returns null when no matching control exists.</summary>
    private static T? FindDescendantByAutomationId<T>(global::Avalonia.Controls.Control root, string automationId)
        where T : global::Avalonia.Controls.Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(candidate => string.Equals(
                AutomationProperties.GetAutomationId(candidate),
                automationId,
                StringComparison.Ordinal));
    }

    /// <summary>Draft harvest (audit win008): before a re-render clears the
    /// editing surface, copy the CURRENT visible editor values into the typed
    /// draft store so edits made after a preview survive that re-render. Only
    /// editors bound to the currently rendered step are harvested, and only
    /// while the same snapshot era is still bound — so a snapshot change (which
    /// already cleared the drafts in SetState) can never re-seed stale values.
    /// The preview baseline is deliberately untouched: it records the values at
    /// preview time and must keep detecting post-preview edits.</summary>
    private void HarvestDraftsBeforeRender(bool contacts, bool lifestyles)
    {
        if (lifestyles && _state?.LifestylesStep is { CanEdit: true } lifestyleStep)
        {
            foreach (CharacterCreationWizardDesktopLifestyle lifestyle in lifestyleStep.Lifestyles)
            {
                Guid lifestyleId = lifestyle.Configuration.LifestyleId;
                string prefix = $"creation-wizard-lifestyle-{lifestyleId:D}";
                if (FindLifestyleEditor<TextBox>($"{prefix}-name") is not { } nameBox)
                    continue;
                CharacterCreationLifestyleConfiguration draft = lifestyle.Configuration with
                {
                    Name = nameBox.Text ?? string.Empty,
                    Increments = ReadLifestyleNumeric($"{prefix}-increments", lifestyle.Configuration.Increments),
                    Percentage = FindLifestyleEditor<NumericUpDown>($"{prefix}-percentage")?.Value
                        ?? lifestyle.Configuration.Percentage,
                    Roommates = ReadLifestyleNumeric($"{prefix}-roommates", lifestyle.Configuration.Roommates),
                    SplitCostWithRoommates = FindLifestyleEditor<CheckBox>($"{prefix}-split")?.IsChecked
                        ?? lifestyle.Configuration.SplitCostWithRoommates
                };
                if (_lifestyleEditDraftBases.TryGetValue(lifestyleId, out CharacterCreationLifestyleConfiguration? capturedBase))
                {
                    // An existing draft cycle: unchanged fields (editor value
                    // equals the captured base) keep the retained draft value
                    // merged with the latest saved values; only fields the
                    // player actually touched are updated from the editors.
                    // The merge input is the freshly captured editor state,
                    // not the previously stored draft: re-renders between edits
                    // must keep the newest typing (R48 gate regression).
                    CharacterCreationLifestyleConfiguration merged = MergeLifestyleDraft(
                        lifestyleId, draft, lifestyle.Configuration);
                    _lifestyleEditDrafts[lifestyleId] = merged;
                    _lifestyleEditDraftBases[lifestyleId] = lifestyle.Configuration;
                }
                else if (draft == lifestyle.Configuration)
                {
                    _lifestyleEditDrafts.Remove(lifestyleId);
                }
                else
                {
                    _lifestyleEditDrafts[lifestyleId] = draft;
                    _lifestyleEditDraftBases[lifestyleId] = lifestyle.Configuration;
                }
            }

            if (lifestyleStep.Authority.LifestyleOptions.Count > 0
                && FindLifestyleEditor<TextBox>("creation-wizard-lifestyle-create-name") is { } createName)
            {
                CharacterCreationLifestyleConfiguration? selected = null;
                if (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-base")?.SelectedItem
                    is ComboBoxItem { Tag: CharacterCreationLifestyleCatalogOption option })
                {
                    selected = new CharacterCreationLifestyleConfiguration(
                        _lifestyleCreateDraftIdentity ?? Guid.NewGuid(),
                        option.OptionId,
                        createName.Text ?? string.Empty,
                        (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-style")?.SelectedItem
                            as ComboBoxItem)?.Tag as string
                            ?? CharacterCreationLifestyleStyleIds.All[0],
                        (FindLifestyleEditor<ComboBox>("creation-wizard-lifestyle-create-increment")?.SelectedItem
                            as ComboBoxItem)?.Tag as string ?? option.DefaultIncrementId,
                        ReadLifestyleNumeric("creation-wizard-lifestyle-create-increments", 1),
                        FindLifestyleEditor<NumericUpDown>("creation-wizard-lifestyle-create-percentage")?.Value ?? 100m,
                        ReadLifestyleNumeric("creation-wizard-lifestyle-create-roommates", 0),
                        FindLifestyleEditor<CheckBox>("creation-wizard-lifestyle-create-split")?.IsChecked == true,
                        false,
                        ReadLifestyleNumeric("creation-wizard-lifestyle-create-area", 0),
                        ReadLifestyleNumeric("creation-wizard-lifestyle-create-comforts", 0),
                        ReadLifestyleNumeric("creation-wizard-lifestyle-create-security", 0),
                        0,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        []);
                }
                if (selected is not null)
                {
                    _lifestyleCreateDraftIdentity ??= selected.LifestyleId;
                    _lifestyleCreateDraft = selected;
                }
            }
        }

        if (contacts && _state?.ContactsStep is { CanEdit: true } contactsStep)
        {
            if (contactsStep.NewContactTemplate is { } template
                && FindContactEditor(Guid.Empty, CharacterCreationContactFieldIds.Name) is not null)
            {
                Dictionary<string, string> addDraft = CaptureContactEditorValues(
                    Guid.Empty, template.Fields.Select(field => field.FieldId));
                if (addDraft.Count > 0)
                {
                    _contactAddDraft = addDraft;
                    _contactAddDraftIdentity ??= Guid.NewGuid();
                }
            }

            foreach (CharacterCreationWizardDesktopContact contact in contactsStep.Contacts)
            {
                if (FindContactEditor(contact.ContactId, CharacterCreationContactFieldIds.Name) is null)
                    continue;
                Dictionary<string, string> draft = CaptureContactEditorValues(
                    contact.ContactId, contact.Fields.Select(field => field.FieldId));
                if (contact.Fields.All(field => string.Equals(
                        draft[field.FieldId], field.SerializedValue, StringComparison.Ordinal)))
                {
                    _contactEditDrafts.Remove(contact.ContactId);
                    _contactEditDraftBases.Remove(contact.ContactId);
                }
                else
                {
                    _contactEditDrafts[contact.ContactId] = draft;
                    _contactEditDraftBases[contact.ContactId] = CaptureSavedContactValues(contact);
                }
            }
        }
    }

    private Control? FindContactEditor(Guid contactId, string fieldId)
    {
        return _contactEditors.TryGetValue((contactId, fieldId), out Control? editor)
            ? editor
            : null;
    }

    /// <summary>
    /// Merges a retained lifestyle edit draft over the current saved
    /// configuration field by field (audit win010). A field the player did not
    /// change (draft equals the captured base) falls back to the latest saved
    /// value, so a refreshed projection is not reverted by stale draft content.
    /// A changed field keeps the player's edit. The merged configuration is
    /// what the editors show and what the next preview proposes.
    /// </summary>
    private CharacterCreationLifestyleConfiguration MergeLifestyleDraft(
        Guid lifestyleId,
        CharacterCreationLifestyleConfiguration editorDraft,
        CharacterCreationLifestyleConfiguration saved)
    {
        if (!_lifestyleEditDraftBases.TryGetValue(lifestyleId, out CharacterCreationLifestyleConfiguration? baseSnapshot))
        {
            return editorDraft;
        }
        List<string> conflicts = [];
        string mergedName = !string.Equals(editorDraft.Name, baseSnapshot.Name, StringComparison.Ordinal)
            ? editorDraft.Name
            : saved.Name;
        // Conflict: the player edited the field AND the saved state moved away
        // from the captured base. The local edit still wins, but the divergence
        // is reported instead of silently choosing a side (audit win010).
        if (!string.Equals(editorDraft.Name, baseSnapshot.Name, StringComparison.Ordinal)
            && !string.Equals(saved.Name, baseSnapshot.Name, StringComparison.Ordinal)
            && !string.Equals(editorDraft.Name, saved.Name, StringComparison.Ordinal))
            conflicts.Add($"name (saved '{saved.Name}' vs your edit '{editorDraft.Name}')");
        int mergedIncrements = editorDraft.Increments != baseSnapshot.Increments
            ? editorDraft.Increments
            : saved.Increments;
        if (editorDraft.Increments != baseSnapshot.Increments
            && saved.Increments != baseSnapshot.Increments
            && editorDraft.Increments != saved.Increments)
            conflicts.Add($"billing increments (saved {saved.Increments} vs your edit {editorDraft.Increments})");
        decimal mergedPercentage = editorDraft.Percentage != baseSnapshot.Percentage
            ? editorDraft.Percentage
            : saved.Percentage;
        if (editorDraft.Percentage != baseSnapshot.Percentage
            && saved.Percentage != baseSnapshot.Percentage
            && editorDraft.Percentage != saved.Percentage)
            conflicts.Add($"cost percentage (saved {saved.Percentage} vs your edit {editorDraft.Percentage})");
        int mergedRoommates = editorDraft.Roommates != baseSnapshot.Roommates
            ? editorDraft.Roommates
            : saved.Roommates;
        if (editorDraft.Roommates != baseSnapshot.Roommates
            && saved.Roommates != baseSnapshot.Roommates
            && editorDraft.Roommates != saved.Roommates)
            conflicts.Add($"roommates (saved {saved.Roommates} vs your edit {editorDraft.Roommates})");
        bool mergedSplit = editorDraft.SplitCostWithRoommates != baseSnapshot.SplitCostWithRoommates
            ? editorDraft.SplitCostWithRoommates
            : saved.SplitCostWithRoommates;
        if (editorDraft.SplitCostWithRoommates != baseSnapshot.SplitCostWithRoommates
            && saved.SplitCostWithRoommates != baseSnapshot.SplitCostWithRoommates
            && editorDraft.SplitCostWithRoommates != saved.SplitCostWithRoommates)
            conflicts.Add($"cost sharing (saved {(saved.SplitCostWithRoommates ? "on" : "off")} vs your edit {(editorDraft.SplitCostWithRoommates ? "on" : "off")})");
        if (conflicts.Count > 0)
            _lifestyleMergeConflictNotes[lifestyleId] =
                "This entry changed elsewhere while you were editing. Your edits are kept for: "
                + string.Join("; ", conflicts)
                + ". Review the changes before saving.";
        // Notes are consumed when the card renders them; the merge never
        // clears them, because the harvest transition (old base vs new saved
        // values) is the only point that can detect the conflict.
        return saved with
        {
            Name = mergedName,
            Increments = mergedIncrements,
            Percentage = mergedPercentage,
            Roommates = mergedRoommates,
            SplitCostWithRoommates = mergedSplit
        };
    }

    /// <summary>
    /// Captures the currently saved contact field values so the edit draft can
    /// later distinguish player edits from unchanged content (audit win010).
    /// </summary>
    private static Dictionary<string, string> CaptureSavedContactValues(
        CharacterCreationWizardDesktopContact contact)
        => contact.Fields.ToDictionary(
            field => field.FieldId,
            field => field.SerializedValue,
            StringComparer.Ordinal);

    private int ReadLifestyleNumeric(string automationId, int fallback)
    {
        decimal? value = FindLifestyleEditor<NumericUpDown>(automationId)?.Value;
        return value is null ? fallback : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private T? FindLifestyleEditor<T>(string automationId) where T : global::Avalonia.Controls.Control
    {
        return LifestylesList.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(candidate =>
                string.Equals(
                    AutomationProperties.GetAutomationId(candidate),
                    automationId,
                    StringComparison.Ordinal));
    }

    private void RenderContacts(CharacterCreationWizardDesktopContactsStep? contacts)
    {
        HarvestDraftsBeforeRender(contacts: true, lifestyles: false);
        ContactsList.Children.Clear();
        _contactEditors.Clear();
        ContactsList.IsVisible = contacts is not null;
        if (contacts is null)
        {
            // The step is merely not visible (audit win009): navigation away
            // keeps the unsaved drafts so returning restores the player's
            // input. Only SetState retires drafts on a real context change.
            _contactPreview = null;
            _contactPreviewBaseline = null;
            _contactPreviewBaselineId = null;
            return;
        }

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
                "No existing contact is projected yet. Use the add-contact form below."));
        }

        if (contacts.NewContactTemplate is { } template && contacts.CanEdit && !_contactMutationBusy)
        {
            StackPanel addFields = new() { Spacing = 3 };
            addFields.Children.Add(new TextBlock
            {
                Text = "Add a contact",
                Classes = { "shell-section-title" }
            });
            addFields.Children.Add(Caption(
                $"Core add-contact defaults · {template.ContactPointCost} contact points"
                + (template.CountsAgainstContactBudget ? " · counts against the contact budget" : string.Empty)
                + (template.CountsAgainstHighPlacesBudget ? " · counts against Friends in High Places" : string.Empty)));
            foreach (CharacterCreationContactFieldAuthority field in template.Fields)
            {
                // Draft preservation: the draft value (kept across previews) wins
                // over the template default so the player's typing survives.
                string draftValue = _contactAddDraft is not null
                    && _contactAddDraft.TryGetValue(field.FieldId, out string? draftFieldValue)
                    ? draftFieldValue
                    : field.SerializedValue;
                CharacterCreationWizardDesktopContactField projected = new(
                    field.FieldId,
                    field.Label,
                    field.ValueKind,
                    field.IsEditable,
                    draftValue,
                    field.Minimum,
                    field.Maximum,
                    field.LegalOptions,
                    field.Blockers,
                    field.SourceAnchorIds);
                StackPanel row = new() { Spacing = 2 };
                row.Children.Add(Caption(field.IsEditable ? field.Label : $"{field.Label} · locked"));
                row.Children.Add(BuildAddContactEditor(projected));
                addFields.Children.Add(row);
            }

            Button addPreview = new()
            {
                Content = "Preview new contact",
                IsEnabled = true,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            addPreview.Classes.Add("shell-action");
            AutomationProperties.SetAutomationId(addPreview, "creation-wizard-contact-add-preview");
            addPreview.Click += ContactAddPreviewButton_OnClick;
            addFields.Children.Add(addPreview);

            Border addCard = new()
            {
                Classes = { "shell-card", "subtle" },
                Padding = new global::Avalonia.Thickness(9),
                Child = addFields
            };
            AutomationProperties.SetAutomationId(addCard, "creation-wizard-contact-add");
            ContactsList.Children.Add(addCard);
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
            _contactMergeConflictNotes.Remove(contact.ContactId);
            fields.Children.Add(Caption(
                $"{contact.Role} · {contact.ContactPointCost} contact points"));
            foreach (CharacterCreationWizardDesktopContactField field in contact.Fields)
            {
                // Draft preservation with field-level merge (audit win010): a
                // field the player did not change (draft equals the captured
                // base) shows the latest saved value, so a refreshed projection
                // is not reverted by stale draft content. Changed fields keep
                // the player's typing.
                string draftValue = field.SerializedValue;
                if (_contactEditDrafts.TryGetValue(contact.ContactId, out Dictionary<string, string>? editDraft)
                    && editDraft.TryGetValue(field.FieldId, out string? editDraftValue)
                    && (!_contactEditDraftBases.TryGetValue(contact.ContactId, out Dictionary<string, string>? editBase)
                        || !editBase.TryGetValue(field.FieldId, out string? baseValue)
                        || !string.Equals(editDraftValue, baseValue, StringComparison.Ordinal)))
                {
                    draftValue = editDraftValue;
                    // The player changed this field; if the saved projection
                    // changed it too, differently, keep the player's edit but
                    // surface the conflict instead of silently overriding
                    // (audit win010).
                    if (editBase is not null
                        && (!editBase.TryGetValue(field.FieldId, out string? conflictBase)
                            || !string.Equals(field.SerializedValue, conflictBase, StringComparison.Ordinal))
                        && !string.Equals(editDraftValue, field.SerializedValue, StringComparison.Ordinal))
                    {
                        _contactMergeConflictNotes[contact.ContactId] =
                            (_contactMergeConflictNotes.TryGetValue(contact.ContactId, out string? existing)
                                ? existing + "; "
                                : "This entry changed elsewhere while you were editing. Your edits are kept for: ")
                            + $"{field.Label} (saved '{field.SerializedValue}' vs your edit '{editDraftValue}')";
                    }
                }
                CharacterCreationWizardDesktopContactField withDraft = new(
                    field.FieldId,
                    field.Label,
                    field.ValueKind,
                    field.IsEditable,
                    draftValue,
                    field.Minimum,
                    field.Maximum,
                    field.LegalOptions,
                    field.Blockers,
                    field.SourceAnchorIds);
                StackPanel row = new() { Spacing = 2 };
                row.Children.Add(Caption(field.IsEditable
                    ? field.Label
                    : $"{field.Label} · locked"));
                Control editor = BuildContactEditor(contact.ContactId, withDraft);
                row.Children.Add(editor);
                fields.Children.Add(row);
            }

            if (_contactMergeConflictNotes.Remove(contact.ContactId, out string? contactConflictNote))
                fields.Children.Add(Caption(contactConflictNote + ". Review the changes before saving."));

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
            Button remove = new()
            {
                Content = "Remove contact",
                Tag = contact.ContactId,
                IsEnabled = contact.CanDelete && contacts.CanEdit && !_contactMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            remove.Classes.Add("shell-action");
            AutomationProperties.SetAutomationId(
                remove,
                $"creation-wizard-contact-{contact.ContactId:D}-remove");
            remove.Click += ContactRemoveButton_OnClick;
            fields.Children.Add(remove);


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
        if (editor is TextBox contactTextBox)
        {
            contactTextBox.TextChanged += (_, _) => RefreshPreviewBindingGates();
        }
        else if (editor is ComboBox contactComboBox)
        {
            contactComboBox.SelectionChanged += (_, _) => RefreshPreviewBindingGates();
        }
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
                        && !_contactMutationBusy
                        && ContactPreviewMatchesVisibleEditors(preview.Edit.ContactId),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        confirm.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(
            confirm,
            $"creation-wizard-contact-{preview.Edit.ContactId:D}-confirm");
        confirm.Click += ContactConfirmButton_OnClick;
        // Review-binding feedback: when the player changed values after the
        // preview, the confirm button is disabled with an explicit reason.
        if (!ContactPreviewMatchesVisibleEditors(preview.Edit.ContactId))
        {
            body.Children.Add(Caption(
                "Values changed since this preview — preview again to confirm the new proposal."));
        }
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

    private Control BuildAddContactEditor(
        CharacterCreationWizardDesktopContactField field)
    {
        Control editor = BuildContactEditor(Guid.Empty, field);
        AutomationProperties.SetAutomationId(
            editor,
            $"creation-wizard-contact-add-field-{field.FieldId}");
        return editor;
    }

    private CharacterCreationContactEditInput? BuildAddContactInput(
        CharacterCreationContactProjection template,
        out string? error)
    {
        error = null;
        if (template is null)
        {
            error = "Core did not provide an add-contact template.";
            return null;
        }

        Dictionary<string, string> values = template.Fields.ToDictionary(
            static field => field.FieldId,
            field => ReadContactEditor(Guid.Empty, field.FieldId),
            StringComparer.Ordinal);
        CharacterCreationContactIdentity identity = new(
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
            values[CharacterCreationContactFieldIds.GroupName]);
        if (!int.TryParse(
                values[CharacterCreationContactFieldIds.Connection],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int connection))
        {
            error = "Connection is not a valid integer choice.";
            return null;
        }
        if (!int.TryParse(
                values[CharacterCreationContactFieldIds.Loyalty],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int loyalty))
        {
            error = "Loyalty is not a valid integer choice.";
            return null;
        }
        if (!bool.TryParse(values[CharacterCreationContactFieldIds.Group], out bool group)
            || !bool.TryParse(values[CharacterCreationContactFieldIds.Free], out bool free)
            || !bool.TryParse(values[CharacterCreationContactFieldIds.Family], out bool family)
            || !bool.TryParse(values[CharacterCreationContactFieldIds.Blackmail], out bool blackmail))
        {
            error = "Contact flags are not valid yes/no choices.";
            return null;
        }

        return new CharacterCreationContactEditInput(
            _contactAddDraftIdentity ??= Guid.NewGuid(),
            identity,
            connection,
            loyalty,
            group,
            free,
            family,
            blackmail)
        {
            ChangeKind = CharacterCreationContactChangeKind.Add
        };
    }

    private void ContactAddPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_contactMutationBusy || _state?.ContactsStep is not { CanEdit: true } contacts
            || contacts.NewContactTemplate is not { } template)
        {
            return;
        }

        CharacterCreationContactEditInput? input = BuildAddContactInput(template, out string? error);
        if (input is null)
        {
            _contactMutationStatus = error ?? "The new contact could not be projected.";
            RenderContacts(contacts);
            return;
        }
        // Draft preservation: remember exactly what the player reviewed and keep
        // the minted identity stable across the whole add-review cycle.
        _contactAddDraft = CaptureContactEditorValues(Guid.Empty, template.Fields.Select(field => field.FieldId));
        _contactAddDraftIdentity = input.ContactId;
        _contactPreviewBaseline = _contactAddDraft;
        _contactPreviewBaselineId = input.ContactId;
        _contactMutationStatus = null;
        ContactPreviewRequested?.Invoke(this, new CharacterCreationContactPreviewRequested(input));
    }

    private void ContactRemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_contactMutationBusy || _state?.ContactsStep is not { CanEdit: true } contacts
            || sender is not Button { Tag: Guid contactId }
            || contacts.Contacts.SingleOrDefault(contact => contact.ContactId == contactId)
               is not { CanDelete: true } contact)
        {
            return;
        }

        CharacterCreationContactEditInput input = new(contact.ContactId)
        {
            ChangeKind = CharacterCreationContactChangeKind.Remove
        };
        _contactMutationStatus = null;
        ContactPreviewRequested?.Invoke(this, new CharacterCreationContactPreviewRequested(input));
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
        if (_contactMutationBusy || _state?.ContactsStep is not { CanEdit: true } contacts
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
        // Draft preservation + review binding: remember what the player sees and
        // freeze the baseline the confirm gate compares against.
        _contactEditDrafts[contact.ContactId] = CaptureContactEditorValues(contact.ContactId, contact.Fields.Select(field => field.FieldId));
        _contactEditDraftBases[contact.ContactId] = CaptureSavedContactValues(contact);
        _contactPreviewBaseline = _contactEditDrafts[contact.ContactId];
        _contactPreviewBaselineId = contact.ContactId;
        ContactPreviewRequested?.Invoke(this, new CharacterCreationContactPreviewRequested(input));
    }

    private void ContactConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_contactMutationBusy || _contactPreview is not { } preview
            || !preview.RequiresExplicitConfirmation
            || !preview.CanConfirm
            || preview.Blockers.Count != 0)
        {
            return;
        }
        if (!ContactPreviewMatchesVisibleEditors(preview.Edit.ContactId))
            return;
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
