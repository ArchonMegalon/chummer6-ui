using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Native SR6 creation wizard: classic Windows-desktop chrome (left step
/// navigation pane, content area, footer with &lt; Back / Next &gt; / Finish).
/// Bound to the Core SR6 creation foundation service through
/// <see cref="ISr6CreationFoundationInteractionPresenter"/>; the service remains
/// the sole read, preview, confirmation, and persistence authority.
/// </summary>
public partial class Sr6CreationWizardWindow : Window
{
    private readonly ISr6CreationFoundationInteractionPresenter _presenter;
    private readonly CharacterOverviewState _overview;

    private Sr6CreationFoundationInteractionState? _state;
    private Sr6CreationFoundationPreparedPreview? _preparedPreview;
    private string _lastOutcome = CharacterCreationFoundationOutcomes.Success;

    private int _activeStepIndex;
    private Guid _operationId = Guid.NewGuid();

    public Sr6CreationWizardWindow(
        ISr6CreationFoundationInteractionPresenter presenter,
        CharacterOverviewState overview)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _overview = overview ?? throw new ArgumentNullException(nameof(overview));
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LoadFromAuthority();
    }

    private void LoadFromAuthority()
    {
        Sr6CreationFoundationInteractionLoadResult result = _presenter.Load(_overview);
        _lastOutcome = result.Outcome;
        _state = result.State;
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || _state is null)
        {
            _activeStepIndex = 0;
            ShowBlockers(result.Blockers, result.Outcome);
            RenderSteps();
            RenderActiveStep();
            return;
        }

        RenderSteps();
        _activeStepIndex = Math.Min(_activeStepIndex, StepIds.Count - 1);
        RenderActiveStep();
    }

    // The build-method step comes first: priority ranks (or Point Buy pools)
    // are the budget source the foundation and attribute steps consume.
    // "finalize" is the one-way career-transition gate behind the explicit
    // confirmation step.
    private static readonly IReadOnlyList<string> StepIds = ["method", "foundation", "attributes", "confirm", "finalize"];

    private string ActiveStepId
        => StepIds[Math.Clamp(_activeStepIndex, 0, StepIds.Count - 1)];

    private void RenderSteps()
    {
        StepList.Children.Clear();
        string[] titles = ["Build Method", "Foundation", "Attributes", "Review & Confirm", "Finalize"];
        for (int i = 0; i < StepIds.Count; i++)
        {
            bool isActive = i == _activeStepIndex;
            var stepButton = new Button
            {
                Content = titles[i],
                Classes = { isActive ? "shell-step-active" : "shell-step" },
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
                IsEnabled = i <= _activeStepIndex,
                Tag = i,
            };
            stepButton.Click += (_, _) =>
            {
                if ((int)stepButton.Tag! <= _activeStepIndex)
                {
                    _activeStepIndex = (int)stepButton.Tag;
                    RenderActiveStep();
                }
            };
            StepList.Children.Add(stepButton);
        }

        WizardStepCounter.Text = $"Step {_activeStepIndex + 1} of {StepIds.Count}";
    }

    private void RenderActiveStep()
    {
        if (_state is null)
        {
            ActiveStepTitle.Text = "Character creation unavailable";
            StepStatusText.Text = FooterStatusText.Text = AuthorityStatusText.Text;
            StepContentHost.Content = null;
            BackButton.IsEnabled = NextButton.IsEnabled = FinishButton.IsVisible = false;
            return;
        }

        switch (ActiveStepId)
        {
            case "method":
                RenderMethodStep();
                break;
            case "finalize":
                RenderFinalizeStep();
                break;
            case "foundation":
                RenderFoundationStep();
                break;
            case "attributes":
                RenderAttributesStep();
                break;
            case "confirm":
                RenderConfirmStep();
                break;
        }

        RenderSteps();
    }

    private void RenderMethodStep()
    {
        ActiveStepTitle.Text = "Build Method";
        Sr6CreationFoundationState foundation = _state!.Foundation;
        var panel = new StackPanel { Spacing = 10 };

        if (IsPointBuy(foundation.BuildMethod))
        {
            RenderPointBuyStep(panel, foundation);
        }
        else
        {
            RenderPriorityStep(panel, foundation);
        }

        StepContentHost.Content = panel;
        UpdateFooterForDirtySelection();
    }

    private static bool IsPointBuy(string buildMethod)
        => string.Equals(buildMethod, Sr6CharacterCreationBuildMethods.PointBuy, StringComparison.Ordinal);

    private void RenderPriorityStep(StackPanel panel, Sr6CreationFoundationState foundation)
    {
        bool sumToTen = string.Equals(
            foundation.BuildMethod, Sr6CharacterCreationBuildMethods.SumToTen, StringComparison.Ordinal);
        panel.Children.Add(new TextBlock
        {
            Classes = { "shell-caption" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Text = sumToTen
                ? "Sum to Ten: assign a rank A-E to every category so the values sum to exactly ten (e.g. A+C+C+C+C)."
                : "Priority: assign the ranks A-E to the five categories; every rank is used exactly once.",
        });

        var combos = new List<(string CategoryId, ComboBox Box)>();
        foreach (string categoryId in PriorityCategoryIds)
        {
            var row = new Grid { ColumnDefinitions = new global::Avalonia.Controls.ColumnDefinitions("180,*"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = CategoryLabel(categoryId),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            });
            var box = new ComboBox
            {
                MinWidth = 120,
                SelectedItem = CurrentPriorityRank(categoryId),
            };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(box, $"sr6-priority-{categoryId}");
            foreach (string rank in PriorityRanks)
            {
                box.Items.Add(rank);
            }

            box.SelectionChanged += (_, _) =>
            {
                SetPriorityRank(categoryId, box.SelectedItem as string);
                _preparedPreview = null;
                UpdateFooterForDirtySelection();
            };
            Grid.SetColumn(box, 1);
            row.Children.Add(box);
            combos.Add((categoryId, box));
            panel.Children.Add(row);
        }

        _priorityComboBoxes = combos;
        GuidanceText.Text = sumToTen
            ? "Sum to Ten requires companion availability; the authority rejects the assignment when the draft does not carry the companion anchor."
            : "The heritage rank must be allowed for the metatype, and the talent rank must be allowed for the chosen talent. The preview validates both against the Core tables.";
    }

    private void RenderPointBuyStep(StackPanel panel, Sr6CreationFoundationState foundation)
    {
        if (foundation.PointBuyLimits is not { } limits)
        {
            panel.Children.Add(new TextBlock
            {
                Classes = { "shell-caption" },
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Text = "The rules authority has not published Point Buy limits for this draft yet.",
            });
            _pointBuyInputs = null;
            return;
        }

        panel.Children.Add(new TextBlock
        {
            Classes = { "shell-caption" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Text = $"Point Buy: you have {limits.CharacterPoints} character points. Free pools: {limits.FreeAttributePoints} attribute, {limits.FreeSkillPoints} skill, {limits.FreeAdjustmentPoints} adjustment. Buy additional pools with character points; resource units convert to nuyen.",
        });

        var inputs = new List<(string PoolId, NumericUpDown Amount)>();
        foreach (var pool in new (string Id, string Label, int Max)[]
        {
            ("attributes", "Additional attribute points", limits.MaximumAdditionalAttributePoints),
            ("skills", "Additional skill points", limits.MaximumAdditionalSkillPoints),
            ("adjustment", "Additional adjustment points", limits.MaximumAdditionalAdjustmentPoints),
            ("resources", "Resource units", limits.MaximumResourceUnits),
        })
        {
            var row = new Grid { ColumnDefinitions = new global::Avalonia.Controls.ColumnDefinitions("260,120"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = pool.Label,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            });
            var input = new NumericUpDown
            {
                Minimum = 0,
                Maximum = pool.Max,
                Value = CurrentPointBuyAmount(pool.Id),
                Increment = 1,
                MinWidth = 100,
            };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(input, $"sr6-pointbuy-{pool.Id}");
            input.ValueChanged += (_, _) =>
            {
                _preparedPreview = null;
                UpdateFooterForDirtySelection();
            };
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            inputs.Add((pool.Id, input));
            panel.Children.Add(row);
        }

        _pointBuyInputs = inputs;
        GuidanceText.Text = "Point Buy buys additional pools instead of assigning priority ranks; the preview checks the character-point budget on every call.";
    }

    private List<(string CategoryId, ComboBox Box)>? _priorityComboBoxes;
    private List<(string PoolId, NumericUpDown Amount)>? _pointBuyInputs;
    private Dictionary<string, string?> _priorityRanks = new();
    private Dictionary<string, int> _pointBuyAmounts = new();

    private static readonly IReadOnlyList<string> PriorityCategoryIds =
    [
        "heritage", "talent", "attributes", "skills", "resources",
    ];

    private static readonly IReadOnlyList<string> PriorityRanks = ["A", "B", "C", "D", "E"];

    private static string CategoryLabel(string categoryId)
        => categoryId switch
        {
            "heritage" => "Heritage (metatype)",
            "talent" => "Talent (magic or resonance)",
            "attributes" => "Attributes",
            "skills" => "Skills",
            "resources" => "Resources",
            _ => categoryId,
        };

    private string? CurrentPriorityRank(string categoryId)
        => _priorityRanks.TryGetValue(categoryId, out string? rank) ? rank : null;

    private void SetPriorityRank(string categoryId, string? rank)
    {
        if (rank is null)
        {
            _priorityRanks.Remove(categoryId);
            return;
        }

        _priorityRanks[categoryId] = rank;
    }

    private int CurrentPointBuyAmount(string poolId)
        => _pointBuyAmounts.TryGetValue(poolId, out int amount) ? amount : 0;

    private IReadOnlyList<Sr6CreationPriorityChoice> BuildPriorityAssignments()
        => _priorityRanks.Count == PriorityCategoryIds.Count
            ? PriorityCategoryIds.Select(id => new Sr6CreationPriorityChoice(id, _priorityRanks[id]!)).ToList()
            : [];

    private Sr6CreationPointBuySelection? BuildPointBuySelection()
        => _pointBuyInputs is not { } inputs ? null : new Sr6CreationPointBuySelection(
            (int)(inputs.FirstOrDefault(i => i.PoolId == "attributes").Amount.Value ?? 0),
            (int)(inputs.FirstOrDefault(i => i.PoolId == "skills").Amount.Value ?? 0),
            (int)(inputs.FirstOrDefault(i => i.PoolId == "adjustment").Amount.Value ?? 0),
            (int)(inputs.FirstOrDefault(i => i.PoolId == "resources").Amount.Value ?? 0));

    // ------------------------------------------------------------------
    // Finalize step: the one-way career-transition gate. The review comes
    // from the Core store (no client XML), losses must be acknowledged, and
    // the confirm carries an explicit confirmation + operation id.
    // ------------------------------------------------------------------
    private Sr6CreationFinalizationReview? _finalizationReview;
    private CheckBox? _lossAcknowledgement;
    private bool _finalizeBusy;

    private void RenderFinalizeStep()
    {
        ActiveStepTitle.Text = "Finalize";
        var panel = new StackPanel { Spacing = 12 };

        Sr6CreationFoundationBinding? binding = _state?.Foundation.Binding;
        if (binding is null || _overview.DisplayOwnerContext is not { } owner)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "The finalization review needs the bound draft and an owner context.",
            });
            StepContentHost.Content = panel;
            NextButton.IsEnabled = false;
            FinishButton.IsEnabled = false;
            FinishButton.IsVisible = false;
            return;
        }

        Sr6CreationFinalizationInteractionReviewResult reviewResult;
        try
        {
            reviewResult = _presenter.ReviewFinalization(_overview, binding);
        }
        catch (IOException)
        {
            // The store could not produce the review; treat as blockers below.
            reviewResult = new Sr6CreationFinalizationInteractionReviewResult(
                CharacterCreationFoundationOutcomes.Invalid,
                null,
                []);
        }

        if (reviewResult.Review is not { } review)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"The Core store could not produce the finalization review ({reviewResult.Outcome}).",
            });
            StepContentHost.Content = panel;
            NextButton.IsEnabled = false;
            FinishButton.IsEnabled = false;
            FinishButton.IsVisible = false;
            return;
        }

        _finalizationReview = review;
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Finalize converts the creation draft into a career character. This is one-way; the draft archive is retained by the Core store.",
        });

        foreach (Sr6CreationFinalizationLoss loss in review.Losses)
        {
            panel.Children.Add(new TextBlock { Text = $"· {loss.DomainId}/{loss.PoolId}: {loss.Amount}" });
        }

        foreach (string blocker in review.Blockers)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"· {blocker}",
                Classes = { "shell-caption" },
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (review.Losses.Count > 0)
        {
            var ack = new CheckBox { Content = "I accept the loss of the listed unspent balances." };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(ack, "sr6-finalize-accept-loss");
            ack.IsCheckedChanged += (_, _) => UpdateFooterForDirtySelection();
            _lossAcknowledgement = ack;
            panel.Children.Add(ack);
        }
        else
        {
            _lossAcknowledgement = null;
        }

        StepContentHost.Content = panel;
        GuidanceText.Text = review.CanFinalize
            ? "Confirming finalizes the draft and transitions the workspace to the career view."
            : "The Core authority lists blockers above; every one must clear before the career transition can run.";
        FinishButton.IsVisible = true;
        FinishButton.IsEnabled = review.CanFinalize && (review.Losses.Count == 0 || _lossAcknowledgement?.IsChecked == true);
        NextButton.IsEnabled = false;
        UpdateFooterForDirtySelection();
    }

    private void RenderFoundationStep()
    {
        ActiveStepTitle.Text = "Foundation - metatype and talent";
        Sr6CreationFoundationState foundation = _state!.Foundation;
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Classes = { "shell-caption" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Text = $"Build method: {foundation.BuildMethod}. Choose a metatype and a talent; the rules authority previews budgets before anything is applied.",
            // automation id set after construction
        });

        panel.Children.Add(new TextBlock { Text = "Metatype" });
        var metatypeList = new ListBox
        {
            ItemsSource = foundation.Metatypes.Select(m => m.Id).ToList(),
            SelectedItem = CurrentMetatypeId,
            // automation id set after construction
            MaxHeight = 160,
        };
        metatypeList.SelectionChanged += (_, _) =>
        {
            CurrentMetatypeId = metatypeList.SelectedItem as string;
            _preparedPreview = null;
            UpdateFooterForDirtySelection();
        };
        panel.Children.Add(metatypeList);

        panel.Children.Add(new TextBlock { Text = "Talent" });
        var talentList = new ListBox
        {
            ItemsSource = foundation.Talents.Select(t => t.Id).ToList(),
            SelectedItem = CurrentTalentId,
            // automation id set after construction
            MaxHeight = 160,
        };
        talentList.SelectionChanged += (_, _) =>
        {
            CurrentTalentId = talentList.SelectedItem as string;
            _preparedPreview = null;
            UpdateFooterForDirtySelection();
        };
        panel.Children.Add(talentList);

        StepContentHost.Content = panel;
        GuidanceText.Text = "Foundation choices set the metatype and talent. The preview applies the priority budget from the Core rules authority; nothing is written until you confirm on the final step.";
        UpdateFooterForDirtySelection();
    }

    private void RenderAttributesStep()
    {
        ActiveStepTitle.Text = "Attributes";
        Sr6CreationFoundationState foundation = _state!.Foundation;
        var panel = new StackPanel { Spacing = 10 };

        if (foundation.AttributeOptions is not { } options || options.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Classes = { "shell-caption" },
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Text = "The rules authority has not published attribute options for this foundation yet. Return to the foundation step and confirm a preview first.",
            });
            StepContentHost.Content = panel;
            NextButton.IsEnabled = _preparedPreview is not null;
            FinishButton.IsVisible = false;
            return;
        }

        panel.Children.Add(new TextBlock
        {
            Classes = { "shell-caption" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Text = "Distribute attribute points and adjustment points. The preview validates limits and the remaining budget on every call.",
        });

        var spends = new List<(string AttributeId, NumericUpDown Points, NumericUpDown Adjustment)>();
        foreach (Sr6CreationAttributeOption option in options)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*,*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = option.AttributeId,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var pointsInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = option.Maximum,
                Value = CurrentSpendFor(option.AttributeId).AttributePoints,
                Increment = 1,
                MinWidth = 80,
            };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(pointsInput, $"sr6-attribute-{option.AttributeId}-points");
            var adjustmentInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = option.Maximum,
                Value = CurrentSpendFor(option.AttributeId).AdjustmentPoints,
                Increment = 1,
                IsEnabled = option.AllowsAdjustmentPoints,
                MinWidth = 80,
            };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(adjustmentInput, $"sr6-attribute-{option.AttributeId}-adjustment");
            pointsInput.ValueChanged += (_, _) =>
            {
                _preparedPreview = null;
                UpdateFooterForDirtySelection();
            };
            adjustmentInput.ValueChanged += (_, _) =>
            {
                _preparedPreview = null;
                UpdateFooterForDirtySelection();
            };
            Grid.SetColumn(pointsInput, 1);
            Grid.SetColumn(adjustmentInput, 2);
            row.Children.Add(pointsInput);
            row.Children.Add(adjustmentInput);
            spends.Add((option.AttributeId, pointsInput, adjustmentInput));
            panel.Children.Add(row);
        }

        _attributeInputs = spends;
        StepContentHost.Content = panel;
        GuidanceText.Text = "Attribute points raise the base value; adjustment points move the final value within the metatype range. The preview rejects over-budget combinations without writing anything.";
        UpdateFooterForDirtySelection();
    }

    private List<(string AttributeId, NumericUpDown Points, NumericUpDown Adjustment)>? _attributeInputs;

    private void RenderConfirmStep()
    {
        ActiveStepTitle.Text = "Review & Confirm";
        var panel = new StackPanel { Spacing = 10 };

        if (_preparedPreview is not { } preview)
        {
            var noPreviewNote = new TextBlock
            {
            Classes = { "shell-caption" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Text = "No preview is held. Go back and let the rules authority build a preview from your selections before confirming.",
            };
            global::Avalonia.Automation.AutomationProperties.SetAutomationId(noPreviewNote, "sr6-confirm-no-preview");
        panel.Children.Add(noPreviewNote);
            StepContentHost.Content = panel;
            FinishButton.IsVisible = false;
            NextButton.IsEnabled = false;
            return;
        }

        var confirmSummary = new TextBlock
        {
            Text = "The preview below was minted by the rules authority. Confirming applies it to the draft as a committed decision.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(confirmSummary, "sr6-confirm-summary");
        panel.Children.Add(confirmSummary);
        var confirmDigest = new TextBlock
        {
            Classes = { "shell-caption" },
            Text = $"Preview digest: {preview.PreviewDigest}",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(confirmDigest, "sr6-confirm-digest");
        panel.Children.Add(confirmDigest);

        var confirmCheck = new CheckBox
        {
            Content = "I confirm this preview should be applied to the character draft.",
            IsChecked = _explicitlyConfirmed,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(confirmCheck, "sr6-confirm-checkbox");
        confirmCheck.IsCheckedChanged += (_, _) =>
        {
            _explicitlyConfirmed = confirmCheck.IsChecked == true;
            FinishButton.IsEnabled = _explicitlyConfirmed;
        };
        panel.Children.Add(confirmCheck);

        StepContentHost.Content = panel;
        GuidanceText.Text = "Confirmation is explicit and one-shot: the operation id is consumed on commit, a repeat returns an operation conflict rather than a duplicate write.";
        FinishButton.IsVisible = true;
        FinishButton.IsEnabled = _explicitlyConfirmed;
        NextButton.IsEnabled = false;
    }

    private bool _explicitlyConfirmed;

    private string? CurrentMetatypeId { get; set; }
    private string? CurrentTalentId { get; set; }

    private Sr6CreationAttributeSpend CurrentSpendFor(string attributeId)
        => _attributeInputs?.FirstOrDefault(a => a.AttributeId == attributeId) is { } entry
            ? new Sr6CreationAttributeSpend(attributeId, (int)entry.Points.Value, (int)entry.Adjustment.Value)
            : new Sr6CreationAttributeSpend(attributeId, 0, 0);

    private void UpdateFooterForDirtySelection()
    {
        FooterStatusText.Text = _preparedPreview is null
            ? "Selections changed - the wizard will request a fresh preview from the rules authority before you can confirm."
            : "Preview held for the current selection.";
    }

    private void ShowBlockers(IReadOnlyList<string> blockers, string outcome)
    {
        string detail = blockers.Count == 0
            ? outcome
            : string.Join(", ", blockers);
        AuthorityStatusText.Text = $"The rules authority returned '{outcome}' ({detail}).";
        FooterStatusText.Text = "Character creation is blocked until the authority reports success.";
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeStepIndex > 0)
        {
            _activeStepIndex--;
            RenderActiveStep();
        }
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeStepIndex >= StepIds.Count - 1)
        {
            return;
        }

        if (!EnsurePreviewHeld())
        {
            return;
        }

        _activeStepIndex++;
        RenderActiveStep();
    }

    private void FinishButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ActiveStepId == "finalize")
        {
            RunFinalization();
            return;
        }

        if (_preparedPreview is null || !_explicitlyConfirmed)
        {
            return;
        }

        Sr6CreationFoundationInteractionConfirmResult result = _presenter.Confirm(
            _overview,
            new Sr6CreationFoundationInteractionConfirmation(
                _preparedPreview,
                _preparedPreview.PreviewDigest,
                _operationId,
                ExplicitlyConfirmed: true));
        _lastOutcome = result.Outcome;
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.Commit is null)
        {
            _operationId = Guid.NewGuid();
            _explicitlyConfirmed = false;
            ShowBlockers(result.Blockers, result.Outcome);
            if (result.RefreshedState is not null)
            {
                _state = result.RefreshedState;
            }
            RenderActiveStep();
            return;
        }

        if (result.RefreshedState is not null)
        {
            _state = result.RefreshedState;
        }

        _preparedPreview = null;
        _explicitlyConfirmed = false;
        _operationId = Guid.NewGuid();
        FooterStatusText.Text = "Foundation committed. The final step reviews the whole draft and performs the one-way career transition.";
        _activeStepIndex = StepIds.ToList().IndexOf("finalize");
        RenderActiveStep();
    }

    private void RunFinalization()
    {
        if (_finalizeBusy || _finalizationReview is not { } review)
        {
            return;
        }

        if (!review.CanFinalize
            || (review.Losses.Count > 0 && _lossAcknowledgement?.IsChecked != true))
        {
            return;
        }

        _finalizeBusy = true;
        FinishButton.IsEnabled = false;
        try
        {
            Sr6CreationFinalizationInteractionConfirmResult result =
                _presenter.ConfirmFinalization(_overview, review, acceptUnspentLoss: review.Losses.Count > 0);
            if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Commit is null)
            {
                ShowBlockers(result.Blockers, result.Outcome);
                RenderFinalizeStep();
                return;
            }

            FooterStatusText.Text = "Finalized. The workspace transitions to the career view.";
            Finalized = true;
            Close(true);
        }
        catch (IOException ex)
        {
            AuthorityStatusText.Text = $"The Core store failed the finalization transaction ({ex.Message}).";
        }
        finally
        {
            _finalizeBusy = false;
            if (!Finalized)
            {
                FinishButton.IsEnabled = true;
            }
        }
    }

    public bool Finalized { get; private set; }

    private bool EnsurePreviewHeld()
    {
        if (_state is null)
        {
            return false;
        }

        if (_preparedPreview is not null)
        {
            return true;
        }

        var selection = new Sr6CreationFoundationSelection(
            CurrentMetatypeId ?? string.Empty,
            CurrentTalentId ?? string.Empty,
            BuildPriorityAssignments());
        selection = selection with
        {
            Attributes = BuildAttributeSelection(),
            PointBuy = IsPointBuy(_state!.Foundation.BuildMethod) ? BuildPointBuySelection() : null,
        };

        Sr6CreationFoundationInteractionPrepareResult result =
            _presenter.Prepare(_overview, selection);
        _lastOutcome = result.Outcome;
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.PreparedPreview is null)
        {
            ShowBlockers(result.Blockers, result.Outcome);
            return false;
        }

        _preparedPreview = result.PreparedPreview;
        _operationId = Guid.NewGuid();
        UpdateFooterForDirtySelection();
        return true;
    }

    private Sr6CreationAttributeSelection? BuildAttributeSelection()
    {
        if (_attributeInputs is not { } inputs || inputs.Count == 0)
        {
            return null;
        }

        return new Sr6CreationAttributeSelection(
            inputs.Select(a => new Sr6CreationAttributeSpend(
                a.AttributeId,
                (int)a.Points.Value,
                (int)a.Adjustment.Value)).ToList());
    }

    public bool Committed { get; private set; }

    private void Close(bool committed)
    {
        Committed = committed;
        Close();
    }
}