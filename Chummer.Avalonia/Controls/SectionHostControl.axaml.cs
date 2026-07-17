using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace Chummer.Avalonia.Controls;

public partial class SectionHostControl : UserControl
{
    private const string ExplainDrawerOpenRuleEnvironmentStudioActionId = "explain_drawer.open_rule_environment_studio";
    private const string ExplainDrawerOpenSourceAnchorActionId = "explain_drawer.open_source_anchor";
    private const string ExplainDrawerReviewBoundedFollowUpActionId = "explain_drawer.review_bounded_follow_up";
    internal static Func<string, bool>? ExplainDrawerSourceAnchorLauncherOverrideForTesting { get; set; }
    private IReadOnlyList<GearWorkbenchItem> _currentGearWorkbenchItems = Array.Empty<GearWorkbenchItem>();
    private BuildLabConceptIntakeState? _buildLab;
    private bool _suppressNavigationTabSelectionChanged;
    private bool _suppressSectionActionSelectionChanged;
    private ExplainDrawerContext? _currentExplainDrawerContext;
    private string? _activeRulesetId;

    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler<string>? SectionActionSelected;
    public event EventHandler<string>? QuickActionRequested;
    public event EventHandler<AttributeEditRequest>? AttributeEditRequested;

    public SectionHostControl()
    {
        InitializeComponent();
        DesktopShellTheme.ApplyShellTextInputTheme(SectionPreviewBox);
        DesktopShellTheme.ApplyShellTextInputTheme(XmlInputBox);
    }

    public string XmlInputText => XmlInputBox.Text ?? string.Empty;

    internal ExplainDrawerContext? GetCurrentExplainDrawerContext()
        => _currentExplainDrawerContext;

    private Control SectionReviewExpander => SectionReviewPanel;

    public void SetState(SectionHostState state)
    {
        _activeRulesetId = RulesetDefaults.NormalizeOptional(state.RulesetId);
        SetNavigationTabs(state.NavigationTabs, state.ActiveTabId);
        SetSectionActions(state.SectionActions, state.ActiveActionId);
        SetNotice(state.Notice);
        SetAttributeParityEditor(state.SectionId, state.PreviewJson);
        SetGearWorkbench(state.SectionId, state.PreviewJson, state.Rows, state.QuickActions);
        SetClassicCharacterSheet(state.SectionId, state.PreviewJson, state.Rows);
        SetSectionPreview(state.SectionId, state.PreviewJson, state.Rows);
        SetBuildLab(state.BuildLab);
        SetBrowseWorkspace(state.BrowseWorkspace);
        SetContactGraph(state.ContactGraph);
        SetDowntimePlanner(state.DowntimePlanner);
        SetNpcPersonaStudio(state.NpcPersonaStudio);
        SetSectionContext(state.SectionId, state.PreviewJson, state.Rows, state.QuickActions);
        SetSectionQuickActions(state.PreviewJson, state.QuickActions);
    }

    public void SetNavigationTabs(IReadOnlyList<NavigatorTabItem> navigationTabs, string? activeTabId)
    {
        NavigatorTabItem[] visibleTabs = navigationTabs
            .Where(tab => tab.Enabled)
            .ToArray();
        NavigatorTabItem[] renderedTabs = ReuseNavigationTabsIfUnchanged(visibleTabs);

        LoadedRunnerTabStripBorder.IsVisible = renderedTabs.Length > 0;
        _suppressNavigationTabSelectionChanged = true;
        try
        {
            LoadedRunnerTabStrip.ItemsSource = renderedTabs;
            LoadedRunnerTabStrip.SelectedItem = renderedTabs.FirstOrDefault(tab =>
                string.Equals(tab.Id, activeTabId, StringComparison.Ordinal));
        }
        finally
        {
            _suppressNavigationTabSelectionChanged = false;
        }
    }

    public void SetSectionActions(IReadOnlyList<NavigatorSectionActionItem> sectionActions, string? activeActionId)
    {
        NavigatorSectionActionItem[] visibleActions = sectionActions
            .Where(action => !string.IsNullOrWhiteSpace(action.Id))
            .ToArray();
        NavigatorSectionActionItem[] renderedActions = ReuseSectionActionsIfUnchanged(visibleActions);

        bool showSectionActions = renderedActions.Length > 1;
        SectionActionTabStripBorder.IsVisible = showSectionActions;
        _suppressSectionActionSelectionChanged = true;
        try
        {
            SectionActionTabStrip.ItemsSource = showSectionActions ? renderedActions : Array.Empty<NavigatorSectionActionItem>();
            SectionActionTabStrip.SelectedItem = showSectionActions
                ? renderedActions.FirstOrDefault(action => string.Equals(action.Id, activeActionId, StringComparison.Ordinal))
                : null;
        }
        finally
        {
            _suppressSectionActionSelectionChanged = false;
        }

        UpdateSectionRowsHeight();
    }

    private NavigatorTabItem[] ReuseNavigationTabsIfUnchanged(NavigatorTabItem[] nextTabs)
    {
        NavigatorTabItem[] currentTabs = LoadedRunnerTabStrip.Items
            .OfType<NavigatorTabItem>()
            .ToArray();
        return currentTabs.SequenceEqual(nextTabs)
            ? currentTabs
            : nextTabs;
    }

    private NavigatorSectionActionItem[] ReuseSectionActionsIfUnchanged(NavigatorSectionActionItem[] nextActions)
    {
        NavigatorSectionActionItem[] currentActions = SectionActionTabStrip.Items
            .OfType<NavigatorSectionActionItem>()
            .ToArray();
        return currentActions.SequenceEqual(nextActions)
            ? currentActions
            : nextActions;
    }

    public void SetNotice(string notice)
    {
        string normalizedNotice = notice?.Trim() ?? string.Empty;
        bool hideNotice = string.IsNullOrWhiteSpace(normalizedNotice)
            || string.Equals(normalizedNotice, "Ready.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedNotice, "Notice: Ready.", StringComparison.OrdinalIgnoreCase);
        if (hideNotice)
        {
            NoticeText.Text = string.Empty;
            NoticeBorder.IsVisible = false;
            return;
        }

        NoticeText.Text = normalizedNotice.StartsWith("Notice:", StringComparison.OrdinalIgnoreCase)
            ? normalizedNotice["Notice:".Length..].TrimStart()
            : normalizedNotice;
        NoticeBorder.IsVisible = true;
    }

    public void SetSectionPreview(string? sectionId, string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        if (!HasRenderableSectionSurface(sectionId, previewJson, rowArray))
        {
            SectionPreviewBox.Text = string.Empty;
            SectionReviewExpander.IsVisible = false;
            SectionRowsList.ItemsSource = null;
            SectionRowsBorder.IsVisible = false;
            return;
        }

        string previewText = BuildSectionPreviewText(sectionId, previewJson, rowArray, _activeRulesetId);
        SectionPreviewBox.Text = previewText;
        bool showingAttributeParityEditor = AttributeParityEditorBorder.IsVisible;
        SectionReviewPanel.IsVisible = false;
        SectionRowsList.ItemsSource = null;
        SectionRowsList.ItemsSource = BuildSectionRowViewItems(rowArray, _activeRulesetId);
        SectionRowsBorder.IsVisible = !showingAttributeParityEditor && !GearWorkbenchBorder.IsVisible;
    }

    public void SetSectionContext(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        if (!HasRenderableSectionSurface(sectionId, previewJson, rowArray, quickActions))
        {
            SectionContextBorder.IsVisible = false;
            SectionContextTitleText.Text = string.Empty;
            SectionContextSummaryText.Text = string.Empty;
            SectionContextTitleText.IsVisible = false;
            SectionContextSummaryText.IsVisible = false;
            UpdateSectionRowsHeight();
            return;
        }

        bool showContext = !ClassicCharacterSheetBorder.IsVisible
            && (!string.IsNullOrWhiteSpace(sectionId) || rowArray.Length > 0 || quickActions.Count > 0);

        SectionContextBorder.IsVisible = showContext;
        SectionContextTitleText.Text = showContext ? BuildSectionTitle(sectionId, previewJson) : string.Empty;
        SectionContextSummaryText.Text = showContext ? BuildSectionSummary(sectionId, previewJson, rowArray, quickActions, _activeRulesetId) : string.Empty;
        SectionContextTitleText.IsVisible = showContext && !string.IsNullOrWhiteSpace(SectionContextTitleText.Text);
        SectionContextSummaryText.IsVisible = showContext && !string.IsNullOrWhiteSpace(SectionContextSummaryText.Text);
        UpdateSectionRowsHeight();
    }

    public void SetClassicCharacterSheet(string? sectionId, string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        ClassicCharacterFactsPanel.Children.Clear();
        ClassicAttributeFactsPanel.Children.Clear();

        if (AttributeParityEditorBorder.IsVisible)
        {
            ClassicCharacterSheetBorder.IsVisible = false;
            UpdateSectionRowsHeight();
            return;
        }

        IReadOnlyList<ClassicSheetFactDisplayItem> summaryFacts = BuildCharacterSummaryFacts(previewJson);
        IReadOnlyList<ClassicSheetFactDisplayItem> attributeFacts = BuildCharacterAttributeFacts(previewJson, rows, _activeRulesetId);
        ClassicCharacterSummaryTitle.Text = BuildClassicSheetTitle(sectionId, previewJson);
        ClassicCharacterSummaryTitle.IsVisible = !string.IsNullOrWhiteSpace(ClassicCharacterSummaryTitle.Text);

        foreach (ClassicSheetFactDisplayItem fact in summaryFacts)
        {
            ClassicCharacterFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: false));
        }

        foreach (ClassicSheetFactDisplayItem fact in attributeFacts)
        {
            ClassicAttributeFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: true));
        }

        ClassicCharacterSheetBorder.IsVisible = summaryFacts.Count > 0 || attributeFacts.Count > 0;
        UpdateSectionRowsHeight();
    }

    public void SetGearWorkbench(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        string? normalizedSectionId = NormalizeSectionId(sectionId);
        bool shouldShow = normalizedSectionId is "gear" or "inventory";
        if (!shouldShow)
        {
            _currentGearWorkbenchItems = Array.Empty<GearWorkbenchItem>();
            GearWorkbenchBadgeStrip.Children.Clear();
            GearWorkbenchList.ItemsSource = null;
            GearWorkbenchDetailText.Text = string.Empty;
            GearWorkbenchBorder.IsVisible = false;
            UpdateSectionRowsHeight();
            return;
        }

        JsonObject? root = TryParseRootObject(previewJson);
        GearWorkbenchState workbench = BuildGearWorkbenchState(normalizedSectionId, root, rows.ToArray(), quickActions);
        _currentGearWorkbenchItems = workbench.Items;
        GearWorkbenchTitleText.Text = workbench.Title;
        GearWorkbenchSummaryText.Text = workbench.Summary;
        GearWorkbenchBadgeStrip.Children.Clear();
        foreach (Border badge in workbench.Badges)
        {
            GearWorkbenchBadgeStrip.Children.Add(badge);
        }

        GearWorkbenchList.ItemsSource = _currentGearWorkbenchItems;
        GearWorkbenchList.SelectedIndex = _currentGearWorkbenchItems.Count > 0 ? 0 : -1;
        GearWorkbenchDetailText.Text = _currentGearWorkbenchItems.FirstOrDefault()?.Detail
            ?? workbench.EmptyDetail;
        GearWorkbenchBorder.IsVisible = true;
        SectionRowsBorder.IsVisible = false;
        UpdateSectionRowsHeight();
    }

    public void SetSectionQuickActions(string previewJson, IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        _currentExplainDrawerContext = ReadExplainDrawerContext(TryParseRootObject(previewJson));
        IReadOnlyList<SectionQuickActionDisplayItem> renderedActions = BuildRenderedQuickActions(quickActions, _currentExplainDrawerContext);
        SectionQuickActionsHost.Children.Clear();

        foreach (SectionQuickActionDisplayItem quickAction in renderedActions)
        {
            SectionQuickActionsHost.Children.Add(CreateQuickActionButton(quickAction));
        }

        SectionQuickActionsBorder.IsVisible = renderedActions.Count > 0;
        UpdateSectionRowsHeight();
    }

    private void SetAttributeParityEditor(string? sectionId, string previewJson)
    {
        ApplyAttributeParityHeaderLabels(_activeRulesetId);
        AttributeParityRowsHost.Children.Clear();
        AttributeParityRowsHost.Spacing = 4d;
        if (!TryBuildAttributeParityRows(sectionId, previewJson, out AttributeParityRowState[] rows))
        {
            AttributeParityEditorBorder.IsVisible = false;
            return;
        }

        foreach (AttributeParityRowState row in rows)
        {
            AttributeParityRowsHost.Children.Add(CreateAttributeParityRow(row));
        }

        AttributeParityEditorBorder.IsVisible = rows.Length > 0;
    }

    private Control CreateAttributeParityRow(AttributeParityRowState row)
    {
        bool isSr6 = AttributeWorkbenchProjector.IsSr6Ruleset(_activeRulesetId);
        IBrush rowForeground = DesktopShellTheme.ResolveForegroundBrush();
        IBrush rowMutedForeground = DesktopShellTheme.ResolveTextMutedBrush();
        IBrush rowSurface = DesktopShellTheme.ResolveSurfaceBrush();
        IBrush rowSurfaceAlt = DesktopShellTheme.ResolveSurfaceAltBrush();
        IBrush rowBorder = DesktopShellTheme.ResolveBorderBrush();
        string rowLabel = BuildAttributeEditorRowLabel(row.AttributeName, _activeRulesetId);
        bool isEdgeAttribute = AttributeWorkbenchProjector.IsEdgeAttribute(row.AttributeName);
        bool careerMode = row.CareerMode;
        bool showImproveButton = isSr6 && careerMode;
        bool showBurnEdgeButton = isSr6 && careerMode && isEdgeAttribute;
        int metatypeMin = row.MetatypeMin;
        int metatypeMax = row.MetatypeMax;
        int metatypeAugMax = row.MetatypeAugMax;
        int availableKarma = row.AvailableKarma;
        Grid grid = new()
        {
            Name = $"AttributeParityRow_{ShortAttributeLabel(row.AttributeName)}",
            ColumnDefinitions = new ColumnDefinitions("*,128,128,72,120"),
            ColumnSpacing = 8,
            Margin = new Thickness(0d)
        };

        TextBlock nameLabel = new()
        {
            Name = $"AttributeParityRow_{ShortAttributeLabel(row.AttributeName)}_Label",
            Text = rowLabel,
            Foreground = rowForeground,
            FontWeight = isSr6 ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ToolTip.SetTip(nameLabel, row.AttributeName);
        Grid nameCell = new()
        {
            ColumnDefinitions = showImproveButton || showBurnEdgeButton ? new ColumnDefinitions("*,Auto") : new ColumnDefinitions("*"),
            ColumnSpacing = 6d,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        nameCell.Children.Add(nameLabel);
        StackPanel? actionHost = null;
        if (showImproveButton || showBurnEdgeButton)
        {
            actionHost = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6d,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(actionHost, 1);
            nameCell.Children.Add(actionHost);
        }

        int baseValue = Math.Clamp(
            row.BaseValue,
            Math.Max(0, metatypeMin),
            Math.Max(Math.Max(0, metatypeMin), Math.Max(0, row.PriorityMaximum)));
        int karmaValue = Math.Clamp(row.KarmaValue, 0, Math.Max(0, row.KarmaMaximum));
        int ResolveTotalCap() => Math.Max(metatypeMax, metatypeAugMax);
        int ResolveBaseMinimum() => Math.Max(0, metatypeMin);
        int ResolveBaseMaximum() => Math.Max(
            ResolveBaseMinimum(),
            Math.Min(Math.Max(0, row.PriorityMaximum), ResolveTotalCap() - Math.Max(0, karmaValue)));
        int ResolveKarmaMaximum() => Math.Max(
            0,
            Math.Min(Math.Max(0, row.KarmaMaximum), ResolveTotalCap() - Math.Max(ResolveBaseMinimum(), baseValue)));
        Func<int, int> setBaseStepperValue = static value => value;
        Func<int, int> setKarmaStepperValue = static value => value;
        Action<int> setBaseReadonlyValue = static _ => { };
        Action<int> setKarmaReadonlyValue = static _ => { };
        Action refreshBaseStepperBounds = static () => { };
        Action refreshKarmaStepperBounds = static () => { };
        bool suppressMirror = false;
        CancellationTokenSource? baseCommitCancellation = null;
        CancellationTokenSource? karmaCommitCancellation = null;
        int pendingBaseValue = baseValue;
        int pendingKarmaValue = karmaValue;

        TextBlock totalValueText = new()
        {
            Name = $"AttributeParityRow_{ShortAttributeLabel(row.AttributeName)}_Total",
            Text = BuildAttributeValueDisplay(baseValue + karmaValue, metatypeAugMax, _activeRulesetId),
            Foreground = rowForeground,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ToolTip.SetTip(totalValueText, isSr6 ? BuildAttributeBreakdownText(baseValue, karmaValue) : BuildAttributeTotalToolTip(row.AttributeName, _activeRulesetId));
        Grid.SetColumn(totalValueText, 3);
        grid.Children.Add(totalValueText);

        TextBlock limitsText = new()
        {
            Name = $"AttributeParityRow_{ShortAttributeLabel(row.AttributeName)}_Limits",
            Text = BuildAttributeLimitsDisplay(metatypeMin, metatypeMax, metatypeAugMax),
            Foreground = rowMutedForeground,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ToolTip.SetTip(limitsText, BuildAttributeLimitsToolTip(_activeRulesetId));
        Grid.SetColumn(limitsText, 4);
        grid.Children.Add(limitsText);

        Button? improveButton = null;
        if (showImproveButton)
        {
            improveButton = new Button
            {
                Name = $"AttributeImprove_{ShortAttributeLabel(row.AttributeName)}",
                Content = "Improve",
                MinWidth = 72,
                Height = 24,
                Padding = new Thickness(8d, 1d),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Foreground = rowForeground,
                Background = rowSurfaceAlt,
                BorderBrush = rowBorder,
                BorderThickness = new Thickness(1)
            };
            actionHost?.Children.Add(improveButton);
        }

        Button? burnEdgeButton = null;
        if (showBurnEdgeButton)
        {
            burnEdgeButton = new Button
            {
                Name = $"AttributeBurnEdge_{ShortAttributeLabel(row.AttributeName)}",
                Content = "Burn",
                MinWidth = 50,
                Height = 24,
                Padding = new Thickness(8d, 1d),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Foreground = rowForeground,
                Background = rowSurfaceAlt,
                BorderBrush = rowBorder,
                BorderThickness = new Thickness(1)
            };
            actionHost?.Children.Add(burnEdgeButton);
        }

        grid.Children.Add(nameCell);

        Control baseEditor = careerMode
            ? CreateReadonlyAttributeValueDisplay(
                $"AttributeBaseEditor_{ShortAttributeLabel(row.AttributeName)}",
                BuildAttributeStepperLabel(row.AttributeName, _activeRulesetId, "base"),
                baseValue.ToString(CultureInfo.InvariantCulture),
                out setBaseReadonlyValue)
            : CreateAttributeValueStepper(
                $"AttributeBaseEditor_{ShortAttributeLabel(row.AttributeName)}",
                BuildAttributeStepperLabel(row.AttributeName, _activeRulesetId, "base"),
                baseValue,
                ResolveBaseMinimum,
                ResolveBaseMaximum,
                row.BaseUnlocked,
                static next => next.ToString(CultureInfo.InvariantCulture),
                next =>
                {
                    baseValue = next;
                    MirrorCapPressure(baseChanged: true);
                    QueueCommit("base", row.BaseValue, () => pendingBaseValue, ref baseCommitCancellation);
                    QueueCommit("karma", row.KarmaValue, () => pendingKarmaValue, ref karmaCommitCancellation);
                },
                out setBaseStepperValue,
                out refreshBaseStepperBounds);
        Grid.SetColumn(baseEditor, 1);
        grid.Children.Add(baseEditor);

        Control karmaEditor = careerMode
            ? CreateReadonlyAttributeValueDisplay(
                $"AttributeKarmaEditor_{ShortAttributeLabel(row.AttributeName)}",
                BuildAttributeStepperLabel(row.AttributeName, _activeRulesetId, "karma"),
                karmaValue.ToString(CultureInfo.InvariantCulture),
                out setKarmaReadonlyValue)
            : CreateAttributeValueStepper(
                $"AttributeKarmaEditor_{ShortAttributeLabel(row.AttributeName)}",
                BuildAttributeStepperLabel(row.AttributeName, _activeRulesetId, "karma"),
                karmaValue,
                static () => 0,
                ResolveKarmaMaximum,
                enabled: true,
                static next => next.ToString(CultureInfo.InvariantCulture),
                next =>
                {
                    karmaValue = next;
                    MirrorCapPressure(baseChanged: false);
                    QueueCommit("base", row.BaseValue, () => pendingBaseValue, ref baseCommitCancellation);
                    QueueCommit("karma", row.KarmaValue, () => pendingKarmaValue, ref karmaCommitCancellation);
                },
                out setKarmaStepperValue,
                out refreshKarmaStepperBounds);
        Grid.SetColumn(karmaEditor, 2);
        grid.Children.Add(karmaEditor);

        Border rowBorderShell = new()
        {
            Background = row.BaseUnlocked && !careerMode ? rowSurface : rowSurfaceAlt,
            BorderBrush = rowBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6d, 4d),
            Child = grid
        };

        void RefreshLiveValue()
        {
            setBaseReadonlyValue(baseValue);
            setKarmaReadonlyValue(karmaValue);
            totalValueText.Text = BuildAttributeValueDisplay(baseValue + karmaValue, metatypeAugMax, _activeRulesetId);
            ToolTip.SetTip(totalValueText, isSr6 ? BuildAttributeBreakdownText(baseValue, karmaValue) : BuildAttributeTotalToolTip(row.AttributeName, _activeRulesetId));
            limitsText.Text = BuildAttributeLimitsDisplay(metatypeMin, metatypeMax, metatypeAugMax);
            RefreshImproveAvailability();
            RefreshBurnEdgeAvailability();
        }

        void MirrorCapPressure(bool baseChanged)
        {
            if (suppressMirror)
            {
                return;
            }

            suppressMirror = true;
            try
            {
                if (baseChanged)
                {
                    karmaValue = setKarmaStepperValue(karmaValue);
                    if (baseValue + karmaValue > ResolveTotalCap())
                    {
                        baseValue = setBaseStepperValue(baseValue);
                    }
                }
                else
                {
                    baseValue = setBaseStepperValue(baseValue);
                    if (baseValue + karmaValue > ResolveTotalCap())
                    {
                        karmaValue = setKarmaStepperValue(karmaValue);
                    }
                }
            }
            finally
            {
                refreshBaseStepperBounds();
                refreshKarmaStepperBounds();
                pendingBaseValue = baseValue;
                pendingKarmaValue = karmaValue;
                suppressMirror = false;
                RefreshLiveValue();
            }
        }

        void RefreshBurnEdgeAvailability()
        {
            if (burnEdgeButton is null)
            {
                return;
            }

            bool canBurnEdge = AttributeWorkbenchProjector.CanBurnEdge(new AttributeWorkbenchRow(
                row.AttributeName,
                rowLabel,
                ShortAttributeLabel(row.AttributeName),
                baseValue,
                karmaValue,
                baseValue + karmaValue,
                metatypeMin,
                metatypeMax,
                metatypeAugMax,
                row.PriorityMaximum,
                row.KarmaMaximum,
                row.BaseUnlocked,
                row.CareerMode,
                availableKarma,
                ComputeCareerAttributeUpgradeCost(baseValue + karmaValue, metatypeAugMax),
                row.CanCareerUpgrade));
            burnEdgeButton.IsEnabled = canBurnEdge;
            string helpText = canBurnEdge
                ? "Burn Edge"
                : "Edge is already exhausted.";
            ToolTip.SetTip(burnEdgeButton, helpText);
            global::Avalonia.Automation.AutomationProperties.SetName(burnEdgeButton, "Burn Edge");
            global::Avalonia.Automation.AutomationProperties.SetHelpText(burnEdgeButton, helpText);
        }

        void RefreshImproveAvailability()
        {
            if (improveButton is null)
            {
                return;
            }

            int improveCost = ComputeCareerAttributeUpgradeCost(baseValue + karmaValue, metatypeAugMax);
            bool canImprove = careerMode && improveCost > 0 && availableKarma >= improveCost;
            improveButton.IsEnabled = canImprove;
            string helpText = canImprove
                ? $"Improve {rowLabel} for {improveCost} Karma"
                : improveCost <= 0
                    ? $"{rowLabel} is already at its current ceiling."
                    : $"Need {improveCost} Karma to improve {rowLabel}.";
            ToolTip.SetTip(improveButton, helpText);
            global::Avalonia.Automation.AutomationProperties.SetName(improveButton, "Improve Attribute");
            global::Avalonia.Automation.AutomationProperties.SetHelpText(improveButton, helpText);
        }

        void CancelPendingAttributeCommits()
        {
            baseCommitCancellation?.Cancel();
            baseCommitCancellation?.Dispose();
            baseCommitCancellation = null;
            karmaCommitCancellation?.Cancel();
            karmaCommitCancellation?.Dispose();
            karmaCommitCancellation = null;
        }

        void QueueCommit(string bucket, int originalValue, Func<int> getPendingValue, ref CancellationTokenSource? cancellation)
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            _ = ScheduleCommitAsync(bucket, originalValue, getPendingValue, cancellation.Token);
        }

        async Task ScheduleCommitAsync(string bucket, int originalValue, Func<int> getPendingValue, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            int value = getPendingValue();
            if (cancellationToken.IsCancellationRequested || value == originalValue)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                AttributeEditRequested?.Invoke(this, new AttributeEditRequest(row.AttributeName, bucket, value));
            });
        }

        if (burnEdgeButton is not null)
        {
            burnEdgeButton.Click += (_, _) =>
            {
                if (!burnEdgeButton.IsEnabled)
                {
                    return;
                }

                CancelPendingAttributeCommits();
                suppressMirror = true;
                try
                {
                    if (karmaValue > 0)
                    {
                        karmaValue -= 1;
                    }
                    else if (baseValue > ResolveBaseMinimum())
                    {
                        baseValue -= 1;
                    }
                    else if (baseValue > 0 && metatypeMin > 0)
                    {
                        baseValue -= 1;
                        metatypeMin -= 1;
                    }

                    baseValue = setBaseStepperValue(baseValue);
                    karmaValue = setKarmaStepperValue(karmaValue);
                }
                finally
                {
                    refreshBaseStepperBounds();
                    refreshKarmaStepperBounds();
                    pendingBaseValue = baseValue;
                    pendingKarmaValue = karmaValue;
                    suppressMirror = false;
                    RefreshLiveValue();
                }

                AttributeEditRequested?.Invoke(this, new AttributeEditRequest(row.AttributeName, "burn", Math.Max(0, baseValue + karmaValue)));
            };
        }

        if (improveButton is not null)
        {
            improveButton.Click += (_, _) =>
            {
                int improveCost = ComputeCareerAttributeUpgradeCost(baseValue + karmaValue, metatypeAugMax);
                if (!improveButton.IsEnabled || improveCost <= 0)
                {
                    return;
                }

                availableKarma = Math.Max(0, availableKarma - improveCost);
                if (isEdgeAttribute && metatypeMin < 1 && baseValue == metatypeMin && karmaValue == 0)
                {
                    metatypeMin += 1;
                    baseValue += 1;
                }
                else
                {
                    karmaValue += 1;
                }

                pendingBaseValue = baseValue;
                pendingKarmaValue = karmaValue;
                RefreshLiveValue();
                AttributeEditRequested?.Invoke(this, new AttributeEditRequest(row.AttributeName, "improve", Math.Max(0, baseValue + karmaValue)));
            };
        }

        grid.DetachedFromVisualTree += (_, _) =>
        {
            CancelPendingAttributeCommits();
        };

        RefreshLiveValue();
        return rowBorderShell;
    }

    private static int ComputeCareerAttributeUpgradeCost(int currentValue, int totalMaximum)
    {
        if (currentValue >= totalMaximum)
        {
            return -1;
        }

        int nextRank = Math.Max(1, currentValue + 1);
        return nextRank * 5;
    }

    private static Control CreateAttributeValueStepper(
        string name,
        string accessibleName,
        int value,
        Func<int> minimumResolver,
        Func<int> maximumResolver,
        bool enabled,
        Func<int, string> valueFormatter,
        Action<int> valueChanged,
        out Func<int, int> setValue,
        out Action refreshBounds)
    {
        int initialMinimum = minimumResolver();
        int initialMaximum = Math.Max(initialMinimum, maximumResolver());
        int current = Math.Clamp(value, initialMinimum, initialMaximum);
        IBrush foreground = DesktopShellTheme.ResolveForegroundBrush();
        IBrush surface = DesktopShellTheme.ResolveSurfaceAltBrush();
        IBrush border = DesktopShellTheme.ResolveBorderBrush();
        Grid stepper = new()
        {
            Name = name,
            ColumnDefinitions = new ColumnDefinitions("28,10,*,10,28"),
            MinHeight = 26,
            Background = surface,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(stepper, $"{accessibleName}: {initialMinimum}-{initialMaximum}");
        global::Avalonia.Automation.AutomationProperties.SetName(stepper, accessibleName);
        global::Avalonia.Automation.AutomationProperties.SetHelpText(stepper, $"{accessibleName}. Use minus and plus to set a value from {initialMinimum} to {initialMaximum}.");

        Button decrement = CreateAttributeStepperButton("-", $"{name}_Decrease", enabled, foreground, surface, border, $"Decrease {accessibleName}");
        TextBlock valueText = new()
        {
            Name = $"{name}_Value",
            Text = valueFormatter(current),
            Foreground = foreground,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MinWidth = 42,
            Margin = new Thickness(4d, 0d)
        };
        ToolTip.SetTip(valueText, accessibleName);
        global::Avalonia.Automation.AutomationProperties.SetName(valueText, $"{accessibleName} value");
        Button increment = CreateAttributeStepperButton("+", $"{name}_Increase", enabled, foreground, surface, border, $"Increase {accessibleName}");

        Grid.SetColumn(decrement, 0);
        Grid.SetColumn(valueText, 2);
        Grid.SetColumn(increment, 4);
        stepper.Children.Add(decrement);
        stepper.Children.Add(valueText);
        stepper.Children.Add(increment);

        void ApplyValue(int next, bool emit)
        {
            int minimum = minimumResolver();
            int maximum = Math.Max(minimum, maximumResolver());
            int clamped = Math.Clamp(next, minimum, maximum);
            string formatted = valueFormatter(clamped);
            bool changed = clamped != current || !string.Equals(valueText.Text, formatted, StringComparison.Ordinal);

            current = clamped;
            valueText.Text = formatted;
            decrement.IsEnabled = enabled && current > minimum;
            increment.IsEnabled = enabled && current < maximum;
            ToolTip.SetTip(stepper, $"{accessibleName}: {minimum}-{maximum}");
            global::Avalonia.Automation.AutomationProperties.SetHelpText(stepper, $"{accessibleName}. Use minus and plus to set a value from {minimum} to {maximum}.");
            if (emit)
            {
                if (changed)
                {
                    valueChanged(current);
                }
            }
        }

        decrement.Click += (_, _) => ApplyValue(current - 1, emit: true);
        increment.Click += (_, _) => ApplyValue(current + 1, emit: true);
        setValue = next =>
        {
            ApplyValue(next, emit: false);
            return current;
        };
        refreshBounds = () => ApplyValue(current, emit: false);
        ApplyValue(current, emit: false);
        return stepper;
    }

    private static Control CreateReadonlyAttributeValueDisplay(
        string name,
        string accessibleName,
        string value,
        out Action<int> setValue)
    {
        IBrush foreground = DesktopShellTheme.ResolveForegroundBrush();
        IBrush surface = DesktopShellTheme.ResolveSurfaceAltBrush();
        IBrush border = DesktopShellTheme.ResolveBorderBrush();
        Border host = new()
        {
            Name = name,
            MinHeight = 26,
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8d, 2d)
        };
        ToolTip.SetTip(host, accessibleName);
        global::Avalonia.Automation.AutomationProperties.SetName(host, accessibleName);
        TextBlock valueText = new()
        {
            Name = $"{name}_Value",
            Text = value,
            Foreground = foreground,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        host.Child = valueText;
        setValue = next => valueText.Text = next.ToString(CultureInfo.InvariantCulture);
        return host;
    }

    private static Button CreateAttributeStepperButton(
        string label,
        string name,
        bool enabled,
        IBrush foreground,
        IBrush background,
        IBrush border,
        string accessibleName)
    {
        Button button = new()
        {
            Name = name,
            Content = label,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Foreground = foreground,
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            IsEnabled = enabled
        };
        ToolTip.SetTip(button, accessibleName);
        global::Avalonia.Automation.AutomationProperties.SetName(button, accessibleName);
        global::Avalonia.Automation.AutomationProperties.SetHelpText(button, accessibleName);
        return button;
    }

    private static bool TryBuildAttributeParityRows(string? sectionId, string previewJson, out AttributeParityRowState[] rows)
    {
        rows = AttributeWorkbenchProjector.BuildRows(sectionId, previewJson)
            .Select(static row => new AttributeParityRowState(
                AttributeName: row.AttributeName,
                BaseValue: row.BaseValue,
                KarmaValue: row.KarmaValue,
                MetatypeMin: row.MetatypeMin,
                MetatypeMax: row.MetatypeMax,
                MetatypeAugMax: row.MetatypeAugMax,
                PriorityMaximum: row.PriorityMaximum,
                KarmaMaximum: row.KarmaMaximum,
                BaseUnlocked: row.BaseUnlocked,
                CareerMode: row.CareerMode,
                AvailableKarma: row.AvailableKarma,
                UpgradeKarmaCost: row.UpgradeKarmaCost,
                CanCareerUpgrade: row.CanCareerUpgrade))
            .ToArray();
        return rows.Length > 0;
    }

    private void ApplyAttributeParityHeaderLabels(string? rulesetId)
    {
        bool isSr6 = AttributeWorkbenchProjector.IsSr6Ruleset(rulesetId);
        AttributeParityHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,128,128,72,120");
        AttributeParityHeaderGrid.ColumnSpacing = 8d;
        AttributeParityHeaderAttributeText.Text = "Attribute";
        AttributeParityHeaderStartText.Text = isSr6 ? "Base" : "Start";
        AttributeParityHeaderAddText.Text = isSr6 ? "Karma" : "Add";
        AttributeParityHeaderTotalText.Text = "Total";
        AttributeParityHeaderLimitsText.Text = "Limits";
        AttributeParityHeaderStartText.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
        AttributeParityHeaderAddText.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
        AttributeParityHeaderTotalText.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
        AttributeParityHeaderLimitsText.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
    }

    private static string BuildAttributeEditorRowLabel(string attributeName, string? rulesetId)
        => AttributeWorkbenchProjector.FormatDisplayLabel(attributeName, rulesetId);

    private static string BuildAttributeStepperLabel(string attributeName, string? rulesetId, string bucket)
    {
        string fullName = FormatAttributeFullName(attributeName);
        if (!IsSr6Ruleset(rulesetId))
        {
            return string.Equals(bucket, "base", StringComparison.Ordinal)
                ? $"{fullName} starting value"
                : $"{fullName} added value";
        }

        return string.Equals(bucket, "base", StringComparison.Ordinal)
            ? $"{fullName} base"
            : $"{fullName} karma";
    }

    private static string BuildAttributeBreakdownText(int baseValue, int karmaValue)
        => $"Base {baseValue} + Karma {karmaValue}";

    private static string BuildAttributeValueDisplay(int totalValue, int metatypeAugMax, string? rulesetId)
        => AttributeWorkbenchProjector.IsSr6Ruleset(rulesetId)
            ? totalValue.ToString(CultureInfo.InvariantCulture)
            : $"{totalValue} ({metatypeAugMax})";

    private static string BuildAttributeLimitsDisplay(AttributeParityRowState row, string? rulesetId)
        => BuildAttributeLimitsDisplay(row.MetatypeMin, row.MetatypeMax, row.MetatypeAugMax);

    private static string BuildAttributeLimitsDisplay(int metatypeMin, int metatypeMax, int metatypeAugMax)
        => $"{metatypeMin} / {metatypeMax} ({metatypeAugMax})";

    private static string BuildAttributeTotalToolTip(string attributeName, string? rulesetId)
    {
        string fullName = FormatAttributeFullName(attributeName);
        return AttributeWorkbenchProjector.IsSr6Ruleset(rulesetId)
            ? $"{fullName} current rating"
            : $"{fullName} total rating and augmented ceiling";
    }

    private static string BuildAttributeLimitsToolTip(string? rulesetId)
        => AttributeWorkbenchProjector.IsSr6Ruleset(rulesetId)
            ? "Minimum, natural maximum, and augmented maximum"
            : "Minimum, natural maximum, and augmented maximum";

    private static bool IsSr6Ruleset(string? rulesetId)
        => AttributeWorkbenchProjector.IsSr6Ruleset(rulesetId);

    private static int ReadInt(JsonObject source, string propertyName, int defaultValue)
    {
        string? value = ReadScalar(source, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static bool ReadBool(JsonObject source, string propertyName, bool defaultValue)
    {
        string? value = ReadScalar(source, propertyName);
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static IReadOnlyList<SectionQuickActionDisplayItem> BuildRenderedQuickActions(
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions,
        ExplainDrawerContext? explainContext)
    {
        List<SectionQuickActionDisplayItem> renderedActions = new(quickActions.Count + 3);
        renderedActions.AddRange(quickActions);

        if (explainContext is null)
        {
            return renderedActions;
        }

        if (!renderedActions.Any(static action => string.Equals(action.ControlId, ExplainDrawerOpenRuleEnvironmentStudioActionId, StringComparison.Ordinal)))
        {
            renderedActions.Add(new SectionQuickActionDisplayItem(
                ExplainDrawerOpenRuleEnvironmentStudioActionId,
                "Open Rule Environment Studio",
                renderedActions.Count == 0));
        }

        if (!string.IsNullOrWhiteSpace(explainContext.SourceLaunchTarget)
            && !renderedActions.Any(static action => string.Equals(action.ControlId, ExplainDrawerOpenSourceAnchorActionId, StringComparison.Ordinal)))
        {
            renderedActions.Add(new SectionQuickActionDisplayItem(
                ExplainDrawerOpenSourceAnchorActionId,
                "Open Source Anchor",
                false));
        }

        if (!string.IsNullOrWhiteSpace(explainContext.FollowUp)
            && !renderedActions.Any(static action => string.Equals(action.ControlId, ExplainDrawerReviewBoundedFollowUpActionId, StringComparison.Ordinal)))
        {
            renderedActions.Add(new SectionQuickActionDisplayItem(
                ExplainDrawerReviewBoundedFollowUpActionId,
                "Review Bounded Follow-up",
                false));
        }

        return renderedActions;
    }

    private void LoadedRunnerTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavigationTabSelectionChanged)
        {
            return;
        }

        if (sender is SelectingItemsControl { SelectedItem: NavigatorTabItem tab }
            && !string.IsNullOrWhiteSpace(tab.Id))
        {
            NavigationTabSelected?.Invoke(this, tab.Id);
        }
    }

    private void SectionActionTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSectionActionSelectionChanged)
        {
            return;
        }

        if (sender is SelectingItemsControl { SelectedItem: NavigatorSectionActionItem action }
            && !string.IsNullOrWhiteSpace(action.Id))
        {
            SectionActionSelected?.Invoke(this, action.Id);
        }
    }

    public void SetBuildLab(BuildLabConceptIntakeState? buildLab)
    {
        _buildLab = buildLab;
        BuildLabBorder.IsVisible = buildLab is not null;

        if (buildLab is null)
        {
            BuildLabSummaryText.Text = string.Empty;
            BuildLabSummaryText.IsVisible = false;
            BuildLabTrustReceiptPanel.Children.Clear();
            UpdateSectionRowsHeight();
            return;
        }

        BuildLabSummaryText.Text = $"{buildLab.Title} · {buildLab.RulesetId}/{buildLab.BuildMethod}";
        BuildLabSummaryText.IsVisible = true;
        SetBuildLabTrustReceiptSections(DesktopTrustReceiptText.BuildBuildLabSections(buildLab));
        UpdateSectionRowsHeight();
    }

    public void SetBrowseWorkspace(BrowseWorkspaceState? browseWorkspace)
    {
        // Chummer5a parity posture: remove synthetic browse-workspace scaffolding.
    }

    public void SetContactGraph(ContactRelationshipGraphState? contactGraph)
    {
        if (contactGraph is null)
        {
            return;
        }

        SectionContextSummaryText.Text = $"{contactGraph.Nodes.Count} contacts · {contactGraph.EdgeCount} links · {contactGraph.Obligations.Count} obligations";
        SectionContextSummaryText.IsVisible = true;
        SectionPreviewBox.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                BuildFactionStatusText(contactGraph),
                BuildHeatAndObligationText(contactGraph),
                BuildFavorRailText(contactGraph)
            }.Where(static section => !string.IsNullOrWhiteSpace(section)));
        SectionReviewPanel.IsVisible = !string.IsNullOrWhiteSpace(SectionPreviewBox.Text);
        UpdateSectionRowsHeight();
    }

    public void SetNpcPersonaStudio(NpcPersonaStudioState? npcPersonaStudio)
    {
        // Chummer5a parity posture: remove synthetic NPC-persona scaffolding.
    }

    public void SetDowntimePlanner(DowntimePlannerState? downtimePlanner)
    {
        // Chummer5a parity posture: remove synthetic downtime-planner scaffolding.
    }

    private void BrowseResultsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Synthetic browse-workspace routing removed for shell parity.
    }

    private static string BuildBrowseSummary(BrowseWorkspaceState browseWorkspace)
    {
        string title = string.IsNullOrWhiteSpace(browseWorkspace.DialogTitle) ? "Browse workspace" : browseWorkspace.DialogTitle!;
        return $"{title} · {browseWorkspace.TotalCount} results · {browseWorkspace.SortId}/{browseWorkspace.SortDirection}";
    }

    private static string BuildPresetLine(BrowseWorkspacePresetState preset)
    {
        string activeTag = preset.IsActive ? "[active] " : string.Empty;
        string scopeTag = preset.Shared ? "shared" : "local";
        return $"{activeTag}{preset.Label} ({scopeTag})";
    }

    private static string BuildFacetLine(BrowseWorkspaceFacetState facet)
    {
        string selectedOptions = facet.SelectedOptions.Count == 0
            ? "none selected"
            : string.Join(", ", facet.SelectedOptions.Select(option => option.Label));
        return $"{facet.Label}: {selectedOptions}";
    }

    private static string BuildResultLine(BrowseWorkspaceResultItemState result)
    {
        string columns = result.ColumnValues.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", result.ColumnValues.Select(pair => $"{pair.Key}: {pair.Value}"))}";
        string active = result.IsActive ? "[active] " : string.Empty;
        string selectable = result.IsSelectable ? string.Empty : " · unavailable";
        return $"{active}{result.Title}{columns}{selectable}";
    }

    private static string BuildDetailText(BrowseItemDetail? detail)
    {
        if (detail is null)
            return "Select a browse result to inspect its current detail payload.";

        IEnumerable<string> lines = detail.SummaryLines.Count == 0
            ? Array.Empty<string>()
            : detail.SummaryLines;
        string summary = string.Join(Environment.NewLine, lines);
        string explain = string.IsNullOrWhiteSpace(detail.ExplainEntryId)
            ? string.Empty
            : $"{Environment.NewLine}Explain: {detail.ExplainEntryId}";
        return $"{detail.Title}{Environment.NewLine}{summary}{explain}".Trim();
    }

    private static string BuildBuildLabProvenance(BuildLabConceptIntakeState buildLab)
    {
        List<string> lines = [];
        if (buildLab.ProvenanceBadges.Count > 0)
        {
            lines.Add(string.Join(" · ", buildLab.ProvenanceBadges.Select(badge => badge.Label)));
        }

        if (!string.IsNullOrWhiteSpace(buildLab.ExplainEntryId))
        {
            lines.Add($"Explain: {buildLab.ExplainEntryId}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.SourceDocumentId))
        {
            lines.Add($"Source: {buildLab.SourceDocumentId}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.NextSafeAction))
        {
            lines.Add($"Next safe action: {buildLab.NextSafeAction}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.RuntimeCompatibilitySummary))
        {
            lines.Add($"Runtime: {buildLab.RuntimeCompatibilitySummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.CampaignFitSummary))
        {
            lines.Add($"Campaign fit: {buildLab.CampaignFitSummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.SupportClosureSummary))
        {
            lines.Add($"Support: {buildLab.SupportClosureSummary}");
        }

        if (buildLab.Watchouts is { Count: > 0 })
        {
            lines.Add($"Watchouts: {string.Join(" | ", buildLab.Watchouts.Take(3))}");
        }

        if (HasBuildBlockerReceipt(buildLab))
        {
            // m104: avalonia_build_blocker_receipts
            lines.Add($"Build blocker details: {BuildBuildBlockerBadge(buildLab)}");
            lines.Add($"Explanation: {BuildBuildBlockerExplainReceipt(buildLab)}");
            lines.Add($"Rule environment: {buildLab.RulesetId} / {buildLab.BuildMethod}");
            lines.Add($"Environment change: {BuildBuildBlockerBefore(buildLab)} -> {BuildBuildBlockerAfter(buildLab)}");
            lines.Add($"Before: {BuildBuildBlockerBefore(buildLab)}");
            lines.Add($"After: {BuildBuildBlockerAfter(buildLab)}");
            lines.Add($"Support reuse: {BuildBuildBlockerSupport(buildLab)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void SetBuildLabTrustReceiptSections(IReadOnlyList<DesktopTrustReceiptSection> sections)
    {
        BuildLabTrustReceiptPanel.Children.Clear();
        if (sections.Count == 0)
        {
            return;
        }

        BuildLabTrustReceiptPanel.Children.Add(new TextBlock
        {
            Text = "Build explanation and environment details",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700")
        });
        BuildLabTrustReceiptPanel.Children.Add(DesktopExplainCompanionLauncher.CreateLaunchButton(
            this,
            new DesktopExplainCompanionRequest(
                Title: "Build Lab details",
                SurfaceId: "build_explain:artifact_launch",
                SurfaceLabel: "Desktop Build Lab comparison and blocker details",
                Sections: sections,
                SurfaceFamilyId: "build_explain:artifact_launch",
                RulesetId: _buildLab?.RulesetId,
                WorkspaceId: _buildLab?.WorkspaceId),
            "OpenBuildLabExplainCompanionButton"));

        if (_buildLab is not null)
        {
            BuildLabTrustReceiptPanel.Children.Add(new TextBlock
            {
                Text = $"Build comparison: {BuildBuildCompareCompanionBadge(_buildLab)}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700")
            });

            if (HasBuildBlockerReceipt(_buildLab))
            {
                BuildLabTrustReceiptPanel.Children.Add(new TextBlock
                {
                    Text = $"Build blocker details: {BuildBuildBlockerBadge(_buildLab)}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700")
                });
            }
        }

        foreach (DesktopTrustReceiptSection section in sections)
        {
            StackPanel sectionPanel = new()
            {
                Spacing = 4
            };

            sectionPanel.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700")
            });

            foreach (string line in section.Lines)
            {
                sectionPanel.Children.Add(new TextBlock
                {
                    Text = $"- {line}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700")
                });
            }

            ToolTip.SetTip(sectionPanel, string.Join(Environment.NewLine, new[] { section.Title }.Concat(section.Lines)));
            BuildLabTrustReceiptPanel.Children.Add(sectionPanel);
        }
    }

    private static bool HasBuildBlockerReceipt(BuildLabConceptIntakeState buildLab)
        => !buildLab.CanContinue
            || !string.IsNullOrWhiteSpace(buildLab.RuntimeCompatibilitySummary)
            || !string.IsNullOrWhiteSpace(buildLab.SupportClosureSummary)
            || buildLab.Watchouts is { Count: > 0 }
            || buildLab.Variants.Any(variant => variant.Warnings.Count > 0);

    private static string BuildBuildBlockerBadge(BuildLabConceptIntakeState buildLab)
    {
        int warningCount = buildLab.Variants.Sum(static variant => variant.Warnings.Count)
            + (buildLab.Watchouts?.Count ?? 0);
        return warningCount == 0 && buildLab.CanContinue ? "ready" : $"{warningCount} blocker signal(s)";
    }

    private static string BuildBuildCompareCompanionBadge(BuildLabConceptIntakeState buildLab)
    {
        int variantCount = buildLab.Variants.Count;
        string leadVariant = buildLab.Variants.FirstOrDefault()?.Label ?? "no variant";
        return variantCount == 0
            ? "pending variant comparison"
            : $"{variantCount} variant option(s); lead {leadVariant}";
    }

    private static string BuildBuildBlockerBefore(BuildLabConceptIntakeState buildLab)
    {
        string summary = FirstNonBlank(
            buildLab.RuntimeCompatibilitySummary,
            buildLab.CampaignFitSummary,
            buildLab.Variants
                .SelectMany(static variant => variant.Warnings)
                .Select(static warning => warning.Detail)
                .FirstOrDefault(),
            buildLab.Watchouts?.FirstOrDefault());

        return string.IsNullOrWhiteSpace(summary) ? "No blocker was emitted before this build decision." : summary;
    }

    private static string BuildBuildBlockerAfter(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(buildLab.NextSafeAction, buildLab.SupportClosureSummary, buildLab.CanContinue ? "Build can continue with the current setup." : "Resolve the blocker before continuing.");

    private static string BuildBuildBlockerExplainReceipt(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(
            buildLab.ExplainEntryId,
            buildLab.SourceDocumentId,
            $"{buildLab.RulesetId}/{buildLab.BuildMethod} blocker details");

    private static string BuildBuildBlockerSupport(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(buildLab.SupportClosureSummary, string.IsNullOrWhiteSpace(buildLab.ExplainEntryId) ? "Support can cite the visible blocker details." : $"Support can cite explanation {buildLab.ExplainEntryId}.");

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string BuildVariantLine(BuildLabVariantProjection variant)
    {
        string metrics = variant.Metrics.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", variant.Metrics.Select(metric => $"{metric.Label}: {metric.Value}"))}";
        string warnings = variant.Warnings.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", variant.Warnings.Select(warning => warning.Label))}";
        return $"{variant.Label} ({variant.TableFit}){metrics}{warnings}";
    }

    private static string BuildCoverageText(BuildLabConceptIntakeState buildLab)
    {
        List<string> lines = [];
        AppendTeamCoverageLines(lines, buildLab.TeamCoverage);

        int optimizerReadyVariants = 0;
        foreach (BuildLabVariantProjection variant in buildLab.Variants)
        {
            List<string> signals = [];

            string coverageMetrics = string.Join(
                " · ",
                variant.Metrics
                    .Where(IsCoverageMetric)
                    .Select(metric => $"{metric.Label}: {metric.Value}"));
            if (!string.IsNullOrWhiteSpace(coverageMetrics))
            {
                signals.Add(coverageMetrics);
            }

            if (variant.OverlapBadges.Count > 0)
            {
                signals.Add($"Overlap: {string.Join(" | ", variant.OverlapBadges.Select(badge => badge.Label))}");
            }

            if (signals.Count == 0)
            {
                continue;
            }

            optimizerReadyVariants++;
            lines.Add($"{variant.Label}: {string.Join(" · ", signals)}");
        }

        foreach (BuildLabProgressionTimeline timeline in buildLab.ProgressionTimelines)
        {
            BuildLabProgressionStep? strongestCoverageStep = null;
            foreach (BuildLabProgressionStep step in timeline.Steps)
            {
                if (step.Outcomes.Any(IsCoverageMetric))
                {
                    strongestCoverageStep = step;
                }
            }

            if (strongestCoverageStep is null)
            {
                lines.Add($"{timeline.Title}: {timeline.Steps.Count} checkpoint(s) keep the planner ready for handoff.");
                continue;
            }

            string coverageOutcomes = string.Join(
                " | ",
                strongestCoverageStep.Outcomes
                    .Where(IsCoverageMetric)
                    .Select(metric => $"{metric.Label}: {metric.Value}"));
            lines.Add($"{timeline.Title}: strongest coverage checkpoint at {strongestCoverageStep.KarmaTarget} Karma · {coverageOutcomes}");
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        lines.Insert(0, $"Planner + team coverage · {optimizerReadyVariants} optimizer-ready variant(s) · {buildLab.ProgressionTimelines.Count} progression timeline(s)");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendTeamCoverageLines(List<string> lines, BuildLabTeamCoverageProjection? teamCoverage)
    {
        if (teamCoverage is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.CoverageSummary))
        {
            lines.Add($"Coverage: {teamCoverage.CoverageSummary}");
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.RolePressureSummary))
        {
            lines.Add($"Role pressure: {teamCoverage.RolePressureSummary}");
        }

        if (teamCoverage.CoveredRoleTags is { Count: > 0 })
        {
            lines.Add($"Covered roles: {FormatRoleTags(teamCoverage.CoveredRoleTags)}");
        }

        if (teamCoverage.MissingRoleTags.Count > 0)
        {
            lines.Add($"Missing roles: {FormatRoleTags(teamCoverage.MissingRoleTags)}");
        }

        if (teamCoverage.DuplicateRoleTags is { Count: > 0 })
        {
            lines.Add($"Duplicate roles: {FormatRoleTags(teamCoverage.DuplicateRoleTags)}");
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.ExplainEntryId))
        {
            lines.Add($"Explain: {teamCoverage.ExplainEntryId}");
        }
    }

    private static string BuildTimelineText(BuildLabConceptIntakeState buildLab)
    {
        if (buildLab.ProgressionTimelines.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = [];
        foreach (BuildLabProgressionTimeline timeline in buildLab.ProgressionTimelines)
        {
            lines.Add($"{timeline.Title} · {timeline.Summary}");
            foreach (BuildLabProgressionStep step in timeline.Steps)
            {
                string outcomes = step.Outcomes.Count == 0
                    ? string.Empty
                    : $" · {string.Join(" · ", step.Outcomes.Select(metric => $"{metric.Label}: {metric.Value}"))}";
                lines.Add($"  {step.KarmaTarget} Karma: {step.Label} · {step.Summary}{outcomes}");
            }

            if (!string.IsNullOrWhiteSpace(timeline.SourceDocumentId))
            {
                lines.Add($"  Source: {timeline.SourceDocumentId}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsCoverageMetric(BuildLabVariantMetric metric)
    {
        return metric.Label.Contains("coverage", StringComparison.OrdinalIgnoreCase)
            || metric.Label.Contains("role", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRoleTags(IEnumerable<string> roleTags)
        => string.Join(" | ", roleTags.Select(FormatRoleTag));

    private static string FormatRoleTag(string roleTag)
    {
        string normalized = roleTag.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? roleTag
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static string BuildExportPayloadText(BuildLabConceptIntakeState buildLab)
    {
        if (buildLab.ExportPayloads.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = [];
        foreach (BuildLabExportPayload payload in buildLab.ExportPayloads)
        {
            lines.Add($"{payload.Title} · {payload.PayloadKind}");
            foreach (BuildLabExportField field in payload.Fields)
            {
                lines.Add($"  {field.Label}: {field.Value}");
            }

            if (!string.IsNullOrWhiteSpace(payload.QueryText))
            {
                lines.Add($"  Query: {payload.QueryText}");
            }

            if (!string.IsNullOrWhiteSpace(payload.SourceDocumentId))
            {
                lines.Add($"  Source: {payload.SourceDocumentId}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildExportTargetLine(BuildLabConceptIntakeState buildLab, BuildLabExportTarget target)
    {
        BuildLabExportPayload? payload = buildLab.ExportPayloads
            .FirstOrDefault(candidate => string.Equals(candidate.PayloadId, target.PayloadId, StringComparison.Ordinal));
        string payloadLine = payload is null ? string.Empty : $" · Payload: {payload.Title}";
        return $"{target.Label} ({target.TargetKind}) · {target.WorkflowId}{payloadLine}";
    }

    private static string BuildActionLine(BuildLabConceptIntakeState buildLab, BuildLabActionDescriptor action)
    {
        BuildLabExportTarget? target = buildLab.ExportTargets
            .FirstOrDefault(candidate => string.Equals(candidate.TargetId, action.TargetId, StringComparison.Ordinal));
        string label = target is null ? action.Label : $"{action.Label} -> {target.Label}";
        return action.Enabled ? label : $"{label} (disabled)";
    }

    private static string BuildFactionStatusText(ContactRelationshipGraphState contactGraph)
    {
        if (contactGraph.Factions.Count == 0)
        {
            return string.Empty;
        }

        IEnumerable<string> lines = contactGraph.Factions
            .Select(faction => $"{faction.Name}: {faction.Status} (contacts {faction.ContactCount}, heat {faction.AverageHeat})");
        return "Faction status rail" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string BuildHeatAndObligationText(ContactRelationshipGraphState contactGraph)
    {
        List<string> lines = [];
        lines.Add("Heat rail");
        foreach (ContactRelationshipHeatState heat in contactGraph.HeatRails.Take(3))
        {
            lines.Add($"{heat.Subject}: {heat.Heat} ({heat.Status})");
        }

        lines.Add(string.Empty);
        lines.Add("Obligation rail");
        if (contactGraph.Obligations.Count == 0)
        {
            lines.Add("No active obligations.");
        }
        else
        {
            lines.AddRange(contactGraph.Obligations.Select(obligation => $"{obligation.Subject}: {obligation.Summary} ({obligation.Severity})"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildFavorRailText(ContactRelationshipGraphState contactGraph)
    {
        if (contactGraph.UnresolvedFavors.Count == 0)
        {
            return "Unresolved favor rail" + Environment.NewLine + "No unresolved favors.";
        }

        IEnumerable<string> lines = contactGraph.UnresolvedFavors
            .Select(favor => $"{favor.Subject}: {favor.Summary}{(favor.Overdue ? " (overdue)" : string.Empty)}");
        return "Unresolved favor rail" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string BuildPersonaSummary(NpcPersonaStudioState personaStudio)
    {
        string defaultPersona = string.IsNullOrWhiteSpace(personaStudio.DefaultPersonaId)
            ? "none"
            : personaStudio.DefaultPersonaId;
        return $"NPC Persona Studio · default {defaultPersona} · selected {personaStudio.SelectedPersonaId}";
    }

    private static string BuildPersonaLine(NpcPersonaDescriptorState persona, string selectedPersonaId)
    {
        string selectedTag = string.Equals(persona.PersonaId, selectedPersonaId, StringComparison.Ordinal) ? "[selected] " : string.Empty;
        string evidenceTag = persona.EvidenceFirst ? "evidence-first" : "balanced";
        return $"{selectedTag}{persona.Label} ({persona.PersonaId}) · {evidenceTag} · {persona.ApprovalState}";
    }

    private static string BuildPolicyLine(NpcPersonaRoutePolicyState policy)
    {
        string provider = string.IsNullOrWhiteSpace(policy.PrimaryProviderId) ? "provider:none" : $"provider:{policy.PrimaryProviderId}";
        string routeClass = string.IsNullOrWhiteSpace(policy.RouteClassId) ? "class:none" : $"class:{policy.RouteClassId}";
        string tools = policy.AllowedToolIds.Count == 0 ? "tools:none" : $"tools:{string.Join(",", policy.AllowedToolIds)}";
        string persona = string.IsNullOrWhiteSpace(policy.PersonaId) ? "persona:none" : $"persona:{policy.PersonaId}";
        return $"{policy.RouteType} · {routeClass} · {provider} · {persona} · {policy.ApprovalState} · {tools}";
    }

    private static string BuildApprovalSummary(NpcPersonaStudioState personaStudio)
    {
        string draft = personaStudio.HasDraftPolicies ? "draft policies present" : "no draft policies";
        string approved = personaStudio.HasApprovedPolicies ? "approved policies present" : "no approved policies";
        return $"{draft}{Environment.NewLine}{approved}";
    }

    private static string BuildDowntimePlannerSummary(DowntimePlannerState downtimePlanner)
    {
        return $"Downtime planner · {downtimePlanner.PlannerLanes.Count} lanes · {downtimePlanner.ScheduleItems.Count} scheduled items";
    }

    private static string BuildDowntimeCalendarText(DowntimePlannerState downtimePlanner)
    {
        if (downtimePlanner.CalendarDays.Count == 0)
        {
            return "Calendar view" + Environment.NewLine + "No calendar entries.";
        }

        IEnumerable<string> lines = downtimePlanner.CalendarDays
            .Select(day => $"{day.Date}: {day.ItemCount} items · {day.Summary}");
        return "Calendar view" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private Button CreateQuickActionButton(SectionQuickActionDisplayItem quickAction)
    {
        Button button = new()
        {
            Name = $"SectionQuickAction_{quickAction.ControlId}",
            Content = quickAction.Label,
            Tag = quickAction.ControlId,
            IsVisible = true,
            Margin = new Thickness(0d, 0d, 8d, 6d)
        };
        button.Classes.Add("shell-action");
        button.Classes.Add(quickAction.IsPrimary ? "primary" : "quiet");
        button.Click += SectionQuickActionButton_OnClick;
        return button;
    }

    private void SectionQuickActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string controlId })
        {
            if (string.Equals(controlId, ExplainDrawerOpenSourceAnchorActionId, StringComparison.Ordinal)
                && TryOpenExplainDrawerSourceAnchor())
            {
                return;
            }

            QuickActionRequested?.Invoke(this, controlId);
        }
    }

    private bool TryOpenExplainDrawerSourceAnchor()
    {
        string? target = _currentExplainDrawerContext?.SourceLaunchTarget;
        return !string.IsNullOrWhiteSpace(target)
            && (ExplainDrawerSourceAnchorLauncherOverrideForTesting?.Invoke(target)
                ?? DesktopCrashRuntime.TryOpenPathInShell(target));
    }

    private static IReadOnlyList<ClassicSheetFactDisplayItem> BuildCharacterSummaryFacts(string previewJson)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        if (root is null)
        {
            return Array.Empty<ClassicSheetFactDisplayItem>();
        }

        List<ClassicSheetFactDisplayItem> facts = [];
        AppendFact(facts, "Alias", ReadString(root, "alias"));
        AppendFact(facts, "Metatype", ReadString(root, "metatype"));
        AppendFact(facts, "Concept", ReadString(root, "concept"));
        AppendFact(facts, "Role", ReadString(root, "role"));
        AppendFact(facts, "Build", FirstNonBlank(
            ReadString(root, "buildMethod"),
            ReadString(root, "buildmethod"),
            ReadString(root, "priority")));
        AppendFact(facts, "Ruleset", FirstNonBlank(
            ReadString(root, "gameEdition"),
            ReadString(root, "ruleset"))?.ToUpperInvariant());
        AppendFact(facts, "Karma", ReadScalar(root, "karma"));
        AppendFact(facts, "Nuyen", ReadScalar(root, "nuyen"));
        AppendFact(facts, "Street Cred", ReadScalar(root, "streetCred"));
        AppendFact(facts, "Notoriety", ReadScalar(root, "notoriety"));
        AppendFact(facts, "Public Awareness", ReadScalar(root, "publicAwareness"));
        AppendFact(facts, "Physical", BuildTrackSummary(root, "physicalTrack", "physicalFilled"));
        AppendFact(facts, "Stun", BuildTrackSummary(root, "stunTrack", "stunFilled"));
        AppendFact(facts, "Counterspelling", ReadScalar(root, "currentCounterspellingDice"));

        JsonObject? combat = ReadObject(root, "combat");
        if (combat is not null)
        {
            AppendFact(facts, "Init", ReadString(combat, "initiative"));
            AppendFact(facts, "Armor", ReadScalar(combat, "armor"));
            AppendFact(facts, "Essence", ReadScalar(combat, "essence"));
        }

        return facts.Take(6).ToArray();
    }

    private static string? BuildTrackSummary(JsonObject root, string totalPropertyName, string filledPropertyName)
    {
        string? total = ReadScalar(root, totalPropertyName);
        string? filled = ReadScalar(root, filledPropertyName);
        if (string.IsNullOrWhiteSpace(total) && string.IsNullOrWhiteSpace(filled))
        {
            return null;
        }

        return $"{filled ?? "0"} / {total ?? "0"}";
    }

    private static IReadOnlyList<ClassicSheetFactDisplayItem> BuildCharacterAttributeFacts(
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        string? rulesetId)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        List<ClassicSheetFactDisplayItem> facts = [];

        if (ReadArray(root, "attributes") is { Count: > 0 } attributeArray)
        {
            foreach (JsonNode? node in attributeArray)
            {
                if (node is not JsonObject attribute)
                {
                    continue;
                }

                string name = FirstNonBlank(
                    ReadString(attribute, "name"),
                    ReadString(attribute, "label"));
                string value = FirstNonBlank(
                    ReadScalar(attribute, "totalValue"),
                    ReadScalar(attribute, "baseValue"),
                    ReadScalar(attribute, "value"),
                    ReadScalar(attribute, "base"));
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(FormatCompactAttributeLabel(name, rulesetId), value));
                }
            }
        }

        if (facts.Count == 0 && ReadObject(root, "attributes") is { } attributesObject)
        {
            foreach (string key in new[] { "Body", "Agility", "Reaction", "Strength", "Willpower", "Logic", "Intuition", "Charisma", "Edge", "Magic", "Resonance" })
            {
                string? value = ReadScalar(attributesObject, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(FormatCompactAttributeLabel(key, rulesetId), value));
                }
            }
        }

        if (facts.Count == 0)
        {
            foreach (SectionRowDisplayItem row in rows)
            {
                if (!row.Path.StartsWith("attributes.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string attributeName = row.Path["attributes.".Length..];
                if (!string.IsNullOrWhiteSpace(row.DisplayValue))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(FormatCompactAttributeLabel(attributeName, rulesetId), row.DisplayValue));
                }
            }
        }

        return facts;
    }

    private static JsonObject? TryParseRootObject(string previewJson)
    {
        if (string.IsNullOrWhiteSpace(previewJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(previewJson) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildSectionTitle(string? sectionId, string previewJson)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        string? previewSection = root is not null ? FirstNonBlank(ReadString(root, "section"), ReadString(root, "sectionId")) : null;
        string rawSection = string.IsNullOrWhiteSpace(sectionId) ? previewSection ?? "Section" : sectionId;
        return rawSection.Trim().ToLowerInvariant() switch
        {
            "profile" => "Profile",
            "cyberwares" => "Cyberware",
            "attributedetails" => "Attributes",
            "conditionmonitor" => "Condition Monitor",
            "complexforms" => "Complex Forms",
            "enemies" => "Enemies",
            "karmasummary" => "Karma Summary",
            "pets" => "Pets & Cohorts",
            "spelldefense" => "Spell Defense",
            "sprites" => "Sprites",
            "initiationgrades" => "Initiation & Submersion",
            "mentorspirits" => "Mentor Spirits",
            "progress" => "Karma Journal",
            _ => FormatSectionName(rawSection)
        };
    }

    private static string BuildSectionSummary(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions,
        string? rulesetId)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        List<string> parts = [];
        JsonObject? root = TryParseRootObject(previewJson);
        string title = BuildSectionTitle(sectionId, previewJson);
        int? recordedCount = ReadCount(root);

        if (string.Equals(sectionId, "validate", StringComparison.OrdinalIgnoreCase)
            && root is not null
            && TryGetPropertyValueIgnoreCase(root, "isValid", out _))
        {
            bool isValid = IsTruthy(root, "isValid");
            int issueCount = ReadArray(root, "issues")?.Count ?? 0;
            parts.Add(isValid ? "Character valid" : "Character needs attention");
            parts.Add(issueCount switch
            {
                0 => "No validation issues",
                1 => "1 validation issue",
                _ => $"{issueCount} validation issues"
            });
            return string.Join("  •  ", parts);
        }

        if (IsSr6Ruleset(rulesetId) && AttributeWorkbenchProjector.IsAttributeSection(sectionId))
        {
            IReadOnlyList<AttributeWorkbenchRow> attributeRows = AttributeWorkbenchProjector.BuildRows(sectionId, previewJson);
            if (attributeRows.Count > 0)
            {
                parts.Add(attributeRows.Count == 1 ? "1 attribute ready" : $"{attributeRows.Count} attributes ready");
                AttributeWorkbenchRow leadAttribute = attributeRows[0];
                parts.Add($"{leadAttribute.DisplayName} {leadAttribute.TotalValue}");

                if (quickActions.Count > 0)
                {
                    string actionSummary = string.Join(", ", quickActions.Take(2).Select(action => action.Label));
                    if (quickActions.Count > 2)
                    {
                        actionSummary = $"{actionSummary}, +{quickActions.Count - 2} more";
                    }

                    parts.Add($"Actions: {actionSummary}");
                }

                if (TryBuildExplainDrawerSummary(root) is { } sr6ExplainSummary)
                {
                    parts.Add(sr6ExplainSummary);
                }

                return string.Join("  •  ", parts);
            }
        }

        if (recordedCount is > 0)
        {
            parts.Add(recordedCount == 1 ? "1 visible entry" : $"{recordedCount} visible entries");
        }
        else if (rowArray.Length > 0)
        {
            parts.Add(rowArray.Length == 1 ? "1 visible entry" : $"{rowArray.Length} visible entries");
        }

        if (rowArray.Length > 0)
        {
            string leadPath = rowArray[0].GetDisplayPath(rulesetId).Trim();
            string leadValue = rowArray[0].DisplayValue.Trim();
            if (!string.IsNullOrWhiteSpace(leadValue) || !string.IsNullOrWhiteSpace(leadPath))
            {
                parts.Add(string.IsNullOrWhiteSpace(leadValue) ? leadPath : $"{leadPath}: {leadValue}");
            }
        }

        if (quickActions.Count > 0)
        {
            string actionSummary = string.Join(", ", quickActions.Take(2).Select(action => action.Label));
            if (quickActions.Count > 2)
            {
                actionSummary = $"{actionSummary}, +{quickActions.Count - 2} more";
            }

            parts.Add($"Actions: {actionSummary}");
        }

        if (TryBuildExplainDrawerSummary(root) is { } explainSummary)
        {
            parts.Add(explainSummary);
        }

        return parts.Count == 0
            ? BuildEmptySectionSummary(sectionId, title, quickActions)
            : string.Join("  •  ", parts);
    }

    private static string FormatSectionName(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return "Section";
        }

        string normalized = sectionName.Replace('_', ' ').Replace('-', ' ').Trim();
        normalized = InsertWordBoundaries(normalized);
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string InsertWordBoundaries(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        StringBuilder builder = new(token.Length + 4);
        for (int i = 0; i < token.Length; i++)
        {
            char current = token[i];
            if (i > 0
                && char.IsUpper(current)
                && !char.IsWhiteSpace(token[i - 1])
                && !char.IsUpper(token[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static void AppendFact(List<ClassicSheetFactDisplayItem> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new ClassicSheetFactDisplayItem(label, value));
        }
    }

    private static string? ReadString(JsonObject source, string propertyName)
        => TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static string? ReadScalar(JsonObject source, string propertyName)
        => TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static JsonObject? ReadObject(JsonObject? source, string propertyName)
        => source is not null
            && TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? node as JsonObject
            : null;

    private static JsonNode? ReadNode(JsonObject? source, string propertyName)
        => source is not null
            && TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? node
            : null;

    private static JsonArray? ReadArray(JsonObject? source, string propertyName)
        => source is not null
            && TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? node as JsonArray
            : null;

    private static bool IsTruthy(JsonObject source, string propertyName)
    {
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node))
        {
            return false;
        }

        string normalized = SanitizeJsonValue(node)?.Trim() ?? string.Empty;
        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPropertyValueIgnoreCase(JsonObject source, string propertyName, out JsonNode? node)
    {
        foreach ((string key, JsonNode? value) in source)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                node = value;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static int? ReadCount(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        foreach (string key in new[]
                 {
                     "count",
                     "gearCount",
                     "weaponCount",
                     "armorCount",
                     "cyberwareCount",
                     "vehicleCount",
                     "knowledgeCount"
                 })
        {
            if (int.TryParse(ReadScalar(root, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                return count;
            }
        }

        return null;
    }

    private static string BuildClassicSheetTitle(
        string? sectionId,
        string previewJson)
    {
        string title = BuildSectionTitle(sectionId, previewJson);
        return string.IsNullOrWhiteSpace(title) ? "Section" : title;
    }

    private static string BuildSectionPreviewText(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        string? rulesetId)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        List<string> lines = [];
        string title = BuildSectionTitle(sectionId, previewJson);

        if (!string.IsNullOrWhiteSpace(title))
        {
            lines.Add(title);
        }

        AppendPreviewScalarLine(lines, "Name", root, "name");
        AppendPreviewScalarLine(lines, "Alias", root, "alias");
        AppendPreviewScalarLine(lines, "Metatype", root, "metatype");
        AppendPreviewScalarLine(lines, "Concept", root, "concept");
        AppendPreviewScalarLine(lines, "Build Method", root, "buildMethod", "buildmethod");
        AppendPreviewScalarLine(lines, "Ruleset", root, "gameEdition", "ruleset");
        AppendPreviewScalarLine(lines, "Karma", root, "karma");
        AppendPreviewScalarLine(lines, "Nuyen", root, "nuyen");
        AppendPreviewScalarLine(lines, "Street Cred", root, "streetCred");
        AppendPreviewScalarLine(lines, "Notoriety", root, "notoriety");
        AppendPreviewScalarLine(lines, "Public Awareness", root, "publicAwareness");

        if (ReadObject(root, "combat") is { } combat)
        {
            AppendPreviewScalarLine(lines, "Initiative", combat, "initiative");
            AppendPreviewScalarLine(lines, "Armor", combat, "armor");
            AppendPreviewScalarLine(lines, "Essence", combat, "essence");
        }

        if (lines.Count > 1 && rowArray.Length > 0)
        {
            lines.Add(string.Empty);
        }

        if (rowArray.Length > 0)
        {
            foreach (SectionRowDisplayItem row in rowArray.Take(10))
            {
                string label = row.GetDisplayPath(rulesetId).Trim();
                string value = row.DisplayValue.Trim();
                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                lines.Add(string.IsNullOrWhiteSpace(value)
                    ? label
                    : $"{label}: {value}");
            }

            if (rowArray.Length > 10)
            {
                lines.Add($"+{rowArray.Length - 10} more entries");
            }
        }
        else if (lines.Count == 1)
        {
            lines.Add(BuildEmptySectionReviewLine(sectionId));
        }

        AppendExplainDrawerLines(lines, root);

        string normalizedPayload = previewJson.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPayload))
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Payload");
            lines.Add(normalizedPayload);
        }

        return string.Join(Environment.NewLine, lines.Where(static line => line is not null)).Trim();
    }

    private static IReadOnlyList<SectionRowDisplayViewItem> BuildSectionRowViewItems(
        IEnumerable<SectionRowDisplayItem> rows,
        string? rulesetId)
        => rows
            .Select(row => new SectionRowDisplayViewItem(row.GetDisplayPath(rulesetId), row.DisplayValue, row.Path, row.Value))
            .ToArray();

    private static string FormatCompactAttributeLabel(string attributeName, string? rulesetId)
        => IsSr6Ruleset(rulesetId)
            ? FormatAttributeFullName(attributeName)
            : ShortAttributeLabel(attributeName);

    private static string BuildSectionPreviewHeader(string? sectionId, string previewJson)
    {
        string title = BuildSectionTitle(sectionId, previewJson);
        return string.IsNullOrWhiteSpace(title) ? "Section" : title;
    }

    private static string BuildEmptySectionSummary(
        string? sectionId,
        string title,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        string? primaryActionLabel = quickActions
            .FirstOrDefault(action => action.IsPrimary)?.Label
            ?? quickActions.FirstOrDefault()?.Label;
        string emptySummary = NormalizeSectionId(sectionId) switch
        {
            "attributes" or "attributedetails" => "No attribute values are recorded yet.",
            "skills" => "No active or knowledge skills are recorded yet.",
            "qualities" => "No positive or negative qualities are recorded yet.",
            "contacts" => "No contacts are recorded yet.",
            "relationships" => "No relationships are recorded yet.",
            "enemies" => "No enemies are recorded yet.",
            "gear" or "inventory" => "No carried gear is recorded yet.",
            "weapons" => "No weapons are recorded yet.",
            "armors" => "No armor pieces are recorded yet.",
            "cyberwares" => "No cyberware or bioware is recorded yet.",
            "vehicles" => "No vehicles are recorded yet.",
            "pets" => "No pets or cohorts are recorded yet.",
            "spells" => "No spells are recorded yet.",
            "spelldefense" => "No spell-defense values are recorded yet.",
            "powers" => "No adept powers are recorded yet.",
            "complexforms" => "No complex forms or programs are recorded yet.",
            "sprites" => "No sprites are recorded yet.",
            "drugs" => "No drugs or consumables are recorded yet.",
            "progress" or "calendar" => "No journal entries are recorded yet.",
            "initiationgrades" => "No initiation or submersion grades are recorded yet.",
            "profile" => "Runner identity details are still blank.",
            "rules" => "Rules and service selections are still blank.",
            _ => $"{title} is ready."
        };

        if (!string.IsNullOrWhiteSpace(primaryActionLabel))
        {
            return $"{emptySummary} Use {primaryActionLabel}.";
        }

        return emptySummary;
    }

    private static bool HasRenderableSectionSurface(
        string? sectionId,
        string previewJson,
        IReadOnlyList<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem>? quickActions = null)
    {
        if (!string.IsNullOrWhiteSpace(sectionId) || rows.Count > 0 || (quickActions?.Count ?? 0) > 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(previewJson))
        {
            return false;
        }

        JsonObject? root = TryParseRootObject(previewJson);
        return root is not null && root.Count > 0;
    }

    private static string BuildEmptySectionReviewLine(string? sectionId)
    {
        return NormalizeSectionId(sectionId) switch
        {
            "attributes" or "attributedetails" => "No attribute values are recorded yet.",
            "skills" => "No skills are recorded yet.",
            "qualities" => "No qualities are recorded yet.",
            "contacts" => "No contacts are recorded yet.",
            "relationships" => "No relationships are recorded yet.",
            "enemies" => "No enemies are recorded yet.",
            "gear" or "inventory" => "No gear entries are recorded yet.",
            "weapons" => "No weapons are recorded yet.",
            "armors" => "No armor entries are recorded yet.",
            "cyberwares" => "No cyberware entries are recorded yet.",
            "vehicles" => "No vehicles are recorded yet.",
            "pets" => "No pets or cohorts are recorded yet.",
            "spells" => "No spells are recorded yet.",
            "spelldefense" => "No spell-defense values are recorded yet.",
            "powers" => "No adept powers are recorded yet.",
            "complexforms" => "No complex forms are recorded yet.",
            "sprites" => "No sprites are recorded yet.",
            "drugs" => "No consumables are recorded yet.",
            "progress" or "calendar" => "No karma journal entries are recorded yet.",
            "initiationgrades" => "No initiation entries are recorded yet.",
            "profile" => "No profile details are recorded yet.",
            "rules" => "No ruleset selections are recorded yet.",
            _ => "No recorded entries yet."
        };
    }

    private static void AppendExplainDrawerLines(List<string> lines, JsonObject? root)
    {
        ExplainDrawerContext? explainContext = ReadExplainDrawerContext(root);
        if (explainContext is null)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add("Explain drawer");
        lines.Add($"Explain packet: {explainContext.ExplainPacket}");

        if (!string.IsNullOrWhiteSpace(explainContext.SourceAnchor))
        {
            lines.Add($"Source anchor: {explainContext.SourceAnchor}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.SourceLaunch))
        {
            lines.Add($"Source launch: {explainContext.SourceLaunch}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.StaleState))
        {
            lines.Add($"Stale state: {explainContext.StaleState}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.FollowUp))
        {
            lines.Add($"Follow-up: {explainContext.FollowUp}");
        }
    }

    private static string? TryBuildExplainDrawerSummary(JsonObject? root)
    {
        ExplainDrawerContext? explainContext = ReadExplainDrawerContext(root);
        if (explainContext is null)
        {
            return null;
        }

        List<string> parts = [$"Explain: {explainContext.ExplainPacket}"];
        if (!string.IsNullOrWhiteSpace(explainContext.SourceAnchor))
        {
            parts.Add($"Source anchor: {explainContext.SourceAnchor}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.SourceLaunch))
        {
            parts.Add($"Source launch: {explainContext.SourceLaunch}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.StaleState))
        {
            parts.Add($"Stale state: {explainContext.StaleState}");
        }

        if (!string.IsNullOrWhiteSpace(explainContext.FollowUp))
        {
            parts.Add($"Follow-up: {explainContext.FollowUp}");
        }

        return string.Join(" · ", parts);
    }

    private static ExplainDrawerContext? ReadExplainDrawerContext(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        JsonObject? explainNode = ReadObject(root, "explain")
            ?? ReadObject(root, "explanation")
            ?? ReadObject(root, "explanationPacket")
            ?? ReadObject(root, "explanation_packet")
            ?? FindFirstExplainableItem(root);
        if (explainNode is null)
        {
            return null;
        }

        string explainPacket = FirstNonBlank(
            ReadString(explainNode, "packet_id"),
            ReadString(explainNode, "packetId"),
            ReadString(explainNode, "explainEntryId"),
            ReadString(explainNode, "explanationPacketId"),
            ReadString(explainNode, "packetId"),
            ReadString(explainNode, "explainPacket"),
            ReadString(explainNode, "value_ref"),
            ReadString(explainNode, "valueRef"));
        if (string.IsNullOrWhiteSpace(explainPacket))
        {
            return null;
        }

        string sourceAnchor = FirstNonBlank(
            ReadString(explainNode, "sourceAnchor"),
            ReadString(explainNode, "sourceAnchorId"),
            ReadString(explainNode, "sourceDocumentId"),
            ReadString(explainNode, "rulebookPage"),
            ReadString(explainNode, "rulebookAnchor"),
            TryBuildSourceAnchorSummary(explainNode));
        string sourceLaunch = FirstNonBlank(
            ReadString(explainNode, "sourceAnchorLaunchSummary"),
            ReadString(explainNode, "sourceLaunchSummary"),
            ReadString(explainNode, "localRulebookLaunchSummary"),
            TryBuildSourceLaunchSummary(explainNode));
        string sourceLaunchTarget = TryBuildSourceLaunchTarget(explainNode) ?? string.Empty;
        string staleState = FirstNonBlank(
            ReadString(explainNode, "staleSnapshotSummary"),
            ReadString(explainNode, "staleStateSummary"),
            ReadString(explainNode, "staleSummary"),
            ReadString(explainNode, "staleSnapshotPosture"),
            TryBuildStaleSnapshotSummary(explainNode));
        string followUp = FirstNonBlank(
            ReadString(explainNode, "followUpSummary"),
            ReadString(explainNode, "boundedFollowUpSummary"),
            ReadString(explainNode, "counterfactualSummary"),
            ReadString(explainNode, "nextSafeAction"),
            TryBuildCounterfactualFollowUpSummary(explainNode));

        return new ExplainDrawerContext(explainPacket, sourceAnchor, sourceLaunch, sourceLaunchTarget, staleState, followUp);
    }

    private static JsonObject? FindFirstExplainableItem(JsonObject root)
    {
        foreach ((string _, JsonNode? value) in root)
        {
            if (value is JsonObject obj
                && HasExplainDrawerFields(obj))
            {
                return obj;
            }

            if (value is JsonObject nestedObject
                && FindFirstExplainableItem(nestedObject) is { } nestedMatch)
            {
                return nestedMatch;
            }

            if (value is not JsonArray array)
            {
                continue;
            }

            foreach (JsonNode? item in array)
            {
                if (item is JsonObject itemObject
                    && HasExplainDrawerFields(itemObject))
                {
                    return itemObject;
                }

                if (item is JsonObject nestedItemObject
                    && FindFirstExplainableItem(nestedItemObject) is { } nestedItemMatch)
                {
                    return nestedItemMatch;
                }
            }
        }

        return null;
    }

    private static bool HasExplainDrawerFields(JsonObject obj)
        => obj.Count > 0
            && (
                !string.IsNullOrWhiteSpace(ReadString(obj, "packet_id"))
                || !string.IsNullOrWhiteSpace(ReadString(obj, "value_ref"))
                || !string.IsNullOrWhiteSpace(ReadString(obj, "sourceAnchor"))
                || !string.IsNullOrWhiteSpace(ReadString(obj, "followUpSummary"))
                || !string.IsNullOrWhiteSpace(ReadString(obj, "boundedFollowUpSummary"))
                || ReadNode(obj, "source_anchors") is not null
                || ReadNode(obj, "counterfactual_actions") is not null
                || HasLegacyExplainDrawerFields(obj));

    private static bool HasLegacyExplainDrawerFields(JsonObject obj)
        => !string.IsNullOrWhiteSpace(ReadString(obj, "explainEntryId"))
            || !string.IsNullOrWhiteSpace(ReadString(obj, "explanationPacketId"))
            || !string.IsNullOrWhiteSpace(ReadString(obj, "packetId"))
            || !string.IsNullOrWhiteSpace(ReadString(obj, "explainPacket"));

    private static string? TryBuildSourceAnchorSummary(JsonObject explainNode)
    {
        JsonNode? anchorNode = ReadNode(explainNode, "source_anchors")
            ?? ReadNode(explainNode, "sourceAnchors")
            ?? ReadNode(explainNode, "sourceAnchor")
            ?? ReadNode(explainNode, "primarySourceAnchor");
        return anchorNode switch
        {
            JsonObject anchorObject => FormatSourceAnchorSummary(anchorObject),
            JsonArray anchorArray => anchorArray
                .OfType<JsonObject>()
                .Select(FormatSourceAnchorSummary)
                .FirstOrDefault(static summary => !string.IsNullOrWhiteSpace(summary)),
            _ => null
        };
    }

    private static string? FormatSourceAnchorSummary(JsonObject anchorObject)
    {
        string book = FirstNonBlank(
            ReadString(anchorObject, "book"),
            ReadString(anchorObject, "sourceBook"),
            ReadString(anchorObject, "title"),
            ReadString(anchorObject, "sourceTitle"),
            ReadString(anchorObject, "documentId"));
        string page = FirstNonBlank(
            ReadString(anchorObject, "page"),
            ReadString(anchorObject, "pageNumber"),
            ReadString(anchorObject, "rulebookPage"));
        string section = FirstNonBlank(
            ReadString(anchorObject, "section"),
            ReadString(anchorObject, "sectionHint"),
            ReadString(anchorObject, "ruleId"),
            ReadString(anchorObject, "anchorId"),
            ReadString(anchorObject, "id"));

        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(book))
        {
            parts.Add(string.IsNullOrWhiteSpace(page) ? book : $"{book} p. {page}");
        }

        if (!string.IsNullOrWhiteSpace(section) && !string.Equals(section, book, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(section);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string? TryBuildSourceLaunchSummary(JsonObject explainNode)
    {
        JsonNode? anchorNode = ReadNode(explainNode, "source_anchors")
            ?? ReadNode(explainNode, "sourceAnchors")
            ?? ReadNode(explainNode, "sourceAnchor")
            ?? ReadNode(explainNode, "primarySourceAnchor");
        JsonObject? anchorObject = anchorNode switch
        {
            JsonObject obj => obj,
            JsonArray array => array.OfType<JsonObject>().FirstOrDefault(),
            _ => null
        };
        if (anchorObject is null)
        {
            return null;
        }

        string explicitLaunch = FirstNonBlank(
            ReadString(anchorObject, "sourceAnchorLaunchSummary"),
            ReadString(anchorObject, "sourceLaunchSummary"),
            ReadString(anchorObject, "localRulebookLaunchSummary"),
            ReadString(anchorObject, "local_rulebook_launch_summary"),
            ReadString(anchorObject, "openLocalRulebookSummary"));
        if (!string.IsNullOrWhiteSpace(explicitLaunch))
        {
            return explicitLaunch;
        }

        bool hasLocalBinding = IsTruthy(anchorObject, "localBindingAvailable")
            || IsTruthy(anchorObject, "local_binding_available")
            || IsTruthy(anchorObject, "isLocallyBound")
            || !string.IsNullOrWhiteSpace(ReadString(anchorObject, "localPdfPath"));
        if (hasLocalBinding)
        {
            return "Open the bound local rulebook anchor from this desktop route.";
        }

        if (!string.IsNullOrWhiteSpace(ReadString(anchorObject, "referenceUrl"))
            || !string.IsNullOrWhiteSpace(ReadString(anchorObject, "uri")))
        {
            return "Open the cited source anchor from this desktop route.";
        }

        return null;
    }

    private static string? TryBuildSourceLaunchTarget(JsonObject explainNode)
    {
        JsonNode? anchorNode = ReadNode(explainNode, "source_anchors")
            ?? ReadNode(explainNode, "sourceAnchors")
            ?? ReadNode(explainNode, "sourceAnchor")
            ?? ReadNode(explainNode, "primarySourceAnchor");
        JsonObject? anchorObject = anchorNode switch
        {
            JsonObject obj => obj,
            JsonArray array => array.OfType<JsonObject>().FirstOrDefault(),
            _ => null
        };
        if (anchorObject is null)
        {
            return null;
        }

        return FirstNonBlank(
            ReadString(anchorObject, "localPdfPath"),
            ReadString(anchorObject, "referenceUrl"),
            ReadString(anchorObject, "uri"));
    }

    private static string? TryBuildStaleSnapshotSummary(JsonObject explainNode)
    {
        JsonNode? staleNode = ReadNode(explainNode, "stale_if_snapshot_changes")
            ?? ReadNode(explainNode, "staleIfSnapshotChanges")
            ?? ReadNode(explainNode, "staleSnapshot");
        if (staleNode is JsonValue)
        {
            return SanitizeJsonValue(staleNode);
        }

        if (staleNode is not JsonObject staleObject)
        {
            return null;
        }

        string explicitSummary = FirstNonBlank(
            ReadString(staleObject, "summary"),
            ReadString(staleObject, "reason"),
            ReadString(staleObject, "message"),
            ReadString(staleObject, "posture"));
        if (!string.IsNullOrWhiteSpace(explicitSummary))
        {
            return explicitSummary;
        }

        string packetSnapshot = FirstNonBlank(
            ReadString(staleObject, "snapshot_ref"),
            ReadString(staleObject, "snapshotRef"),
            ReadString(staleObject, "packetSnapshotRef"));
        string currentSnapshot = FirstNonBlank(
            ReadString(staleObject, "current_snapshot_ref"),
            ReadString(staleObject, "currentSnapshotRef"),
            ReadString(staleObject, "activeSnapshotRef"));
        if (!string.IsNullOrWhiteSpace(packetSnapshot) && !string.IsNullOrWhiteSpace(currentSnapshot))
        {
            return $"Packet snapshot {packetSnapshot} no longer matches current snapshot {currentSnapshot}. Refresh before trusting this value.";
        }

        return string.IsNullOrWhiteSpace(packetSnapshot)
            ? null
            : $"Refresh before trusting this value after the snapshot changes ({packetSnapshot}).";
    }

    private static string? TryBuildCounterfactualFollowUpSummary(JsonObject explainNode)
    {
        JsonNode? followUpNode = ReadNode(explainNode, "counterfactual_actions")
            ?? ReadNode(explainNode, "counterfactualActions")
            ?? ReadNode(explainNode, "followUpActions");
        return followUpNode switch
        {
            JsonValue value => SanitizeJsonValue(value),
            JsonObject obj => FormatCounterfactualActionSummary(obj),
            JsonArray array => string.Join(
                " ; ",
                array.OfType<JsonObject>()
                    .Select(FormatCounterfactualActionSummary)
                    .Where(static summary => !string.IsNullOrWhiteSpace(summary))
                    .Take(2)),
            _ => null
        };
    }

    private static string? FormatCounterfactualActionSummary(JsonObject actionObject)
        => FirstNonBlank(
            ReadString(actionObject, "summary"),
            ReadString(actionObject, "label"),
            ReadString(actionObject, "title"),
            ReadString(actionObject, "question"),
            ReadString(actionObject, "description"),
            ReadString(actionObject, "action"));

    private static string? NormalizeSectionId(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return null;
        }

        return sectionId.Trim().ToLowerInvariant();
    }

    private static void AppendPreviewScalarLine(
        List<string> lines,
        string label,
        JsonObject? source,
        params string[] propertyNames)
    {
        if (source is null)
        {
            return;
        }

        string value = FirstNonBlank(propertyNames.Select(propertyName => ReadScalar(source, propertyName)).ToArray());
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    private static string SanitizeJsonValue(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        string raw = node.ToJsonString();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            return raw[1..^1];
        }

        return raw;
    }

    private static string ShortAttributeLabel(string attributeName)
        => AttributeWorkbenchProjector.FormatCompactLabel(attributeName);

    private static string FormatAttributeFullName(string attributeName)
        => AttributeWorkbenchProjector.FormatFullLabel(attributeName);

    private static Control CreateClassicFactCard(ClassicSheetFactDisplayItem fact, bool emphasizeValue)
    {
        Border card = new()
        {
            Margin = new Thickness(0d, 0d, 4d, 4d),
            Padding = emphasizeValue ? new Thickness(3d, 2d) : new Thickness(4d, 3d),
            MinWidth = emphasizeValue ? 38d : 76d,
            MinHeight = emphasizeValue ? 28d : 32d,
            Background = emphasizeValue
                ? DesktopShellTheme.ResolveSelectionToolbarBrush()
                : DesktopShellTheme.ResolveSelectionPanelBrush(),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1d)
        };

        StackPanel stack = new()
        {
            Spacing = 0d
        };
        stack.Children.Add(new TextBlock
        {
            Text = fact.Label,
            IsVisible = false,
            FontSize = emphasizeValue ? 8d : 9d,
            FontWeight = FontWeight.Medium,
            TextAlignment = emphasizeValue ? TextAlignment.Center : TextAlignment.Left
        });
        stack.Children.Add(new TextBlock
        {
            Text = fact.Value,
            FontSize = emphasizeValue ? 15d : 12d,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = emphasizeValue ? TextAlignment.Center : TextAlignment.Left
        });
        card.Child = stack;
        ToolTip.SetTip(card, $"{fact.Label}: {fact.Value}");
        return card;
    }

    private void GearWorkbenchList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GearWorkbenchList.SelectedItem is GearWorkbenchItem item)
        {
            GearWorkbenchDetailText.Text = item.Detail;
        }
        else if (_currentGearWorkbenchItems.Count > 0)
        {
            GearWorkbenchDetailText.Text = _currentGearWorkbenchItems[0].Detail;
        }
        else
        {
            GearWorkbenchDetailText.Text = "Select an inventory entry to inspect its current loadout details.";
        }
    }

    private GearWorkbenchState BuildGearWorkbenchState(
        string? normalizedSectionId,
        JsonObject? root,
        IReadOnlyList<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        string title = normalizedSectionId == "inventory" ? "Inventory" : "Gear";
        string? primaryActionLabel = quickActions.FirstOrDefault(static action => action.IsPrimary)?.Label
            ?? quickActions.FirstOrDefault()?.Label;
        IReadOnlyList<GearWorkbenchItem> items = normalizedSectionId == "inventory"
            ? BuildInventoryWorkbenchItems(root, rows)
            : BuildGearWorkbenchItems(root, rows);

        string summary = items.Count == 0
            ? BuildEmptySectionSummary(normalizedSectionId, title, quickActions)
            : primaryActionLabel is null
                ? $"{items.Count} visible loadout entries. Select an entry to inspect details."
                : $"{items.Count} visible loadout entries. Use {primaryActionLabel} to extend the current loadout.";

        string emptyDetail = primaryActionLabel is null
            ? "No inventory entries are currently visible."
            : $"No inventory entries are currently visible. Use {primaryActionLabel}.";

        List<Border> badges = [];
        if (normalizedSectionId == "inventory")
        {
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Gear", ReadCountLabel(root, "gearCount", rows, "gear")), emphasizeValue: false));
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Weapons", ReadCountLabel(root, "weaponCount", rows, "weapons")), emphasizeValue: false));
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Armor", ReadCountLabel(root, "armorCount", rows, "armors")), emphasizeValue: false));
        }
        else
        {
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Entries", items.Count.ToString(CultureInfo.InvariantCulture)), emphasizeValue: false));
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Nuyen", root is null ? "Unknown" : ReadScalar(root, "nuyen") ?? "Unknown"), emphasizeValue: false));
            badges.Add((Border)CreateClassicFactCard(new ClassicSheetFactDisplayItem("Action", primaryActionLabel ?? "Inspect"), emphasizeValue: false));
        }

        return new GearWorkbenchState(title, summary, items, badges, emptyDetail);
    }

    private IReadOnlyList<GearWorkbenchItem> BuildGearWorkbenchItems(JsonObject? root, IReadOnlyList<SectionRowDisplayItem> rows)
    {
        JsonArray? gearArray = ReadArray(root, "gear");
        if (gearArray is not null)
        {
            List<GearWorkbenchItem> items = [];
            for (int index = 0; index < gearArray.Count; index++)
            {
                if (gearArray[index] is not JsonObject entry)
                {
                    continue;
                }

                string? label = ReadScalar(entry, "name");
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = $"Gear {index + 1}";
                }

                string summary = BuildJoinedSummary(
                    BuildScalarFact(ReadScalar(entry, "rating"), "Rating"),
                    BuildScalarFact(ReadScalar(entry, "quantity"), "Qty"),
                    ReadScalar(entry, "location") ?? string.Empty);
                string detail = BuildJoinedLines(
                    BuildDetailLine("Name", label),
                    BuildDetailLine("Rating", ReadScalar(entry, "rating")),
                    BuildDetailLine("Quantity", ReadScalar(entry, "quantity")),
                    BuildDetailLine("Location", ReadScalar(entry, "location")),
                    BuildDetailLine("Source", ReadScalar(entry, "source")),
                    BuildDetailLine("Availability", ReadScalar(entry, "availability")));
                items.Add(new GearWorkbenchItem(label, string.IsNullOrWhiteSpace(summary) ? "Runner gear entry" : summary, string.IsNullOrWhiteSpace(detail) ? label : detail));
            }

            if (items.Count > 0)
            {
                return items;
            }
        }

        return rows
            .Where(row => row.Path.Contains("gear", StringComparison.OrdinalIgnoreCase))
            .Select(static row => new GearWorkbenchItem(row.DisplayPath, row.DisplayValue, $"{row.DisplayPath}{Environment.NewLine}{row.DisplayValue}"))
            .ToArray();
    }

    private IReadOnlyList<GearWorkbenchItem> BuildInventoryWorkbenchItems(JsonObject? root, IReadOnlyList<SectionRowDisplayItem> rows)
    {
        List<GearWorkbenchItem> items = [];
        AppendInventoryItems(items, root, "gear", "Gear", rows);
        AppendInventoryItems(items, root, "weapons", "Weapons", rows);
        AppendInventoryItems(items, root, "armors", "Armor", rows);
        AppendInventoryItems(items, root, "cyberwares", "Cyberware", rows);
        AppendInventoryItems(items, root, "vehicles", "Vehicles", rows);
        return items;
    }

    private void AppendInventoryItems(
        List<GearWorkbenchItem> items,
        JsonObject? root,
        string propertyName,
        string title,
        IReadOnlyList<SectionRowDisplayItem> rows)
    {
        JsonArray? array = ReadArray(root, propertyName);
        if (array is not null)
        {
            for (int index = 0; index < array.Count; index++)
            {
                if (array[index] is not JsonObject entry)
                {
                    continue;
                }

                string? label = ReadScalar(entry, "name");
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = $"{title} {index + 1}";
                }

                string summary = BuildJoinedSummary(
                    BuildScalarFact(ReadScalar(entry, "rating"), "Rating"),
                    BuildScalarFact(ReadScalar(entry, "quantity"), "Qty"),
                    ReadScalar(entry, "location") ?? string.Empty);
                string detail = BuildJoinedLines(
                    BuildDetailLine("Type", title),
                    BuildDetailLine("Name", label),
                    BuildDetailLine("Rating", ReadScalar(entry, "rating")),
                    BuildDetailLine("Quantity", ReadScalar(entry, "quantity")),
                    BuildDetailLine("Location", ReadScalar(entry, "location")));
                items.Add(new GearWorkbenchItem(label, string.IsNullOrWhiteSpace(summary) ? title : summary, string.IsNullOrWhiteSpace(detail) ? label : detail));
            }
        }

        foreach (SectionRowDisplayItem row in rows.Where(row => row.Path.StartsWith(propertyName, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(new GearWorkbenchItem(
                row.DisplayPath,
                row.DisplayValue,
                $"{title}{Environment.NewLine}{row.DisplayPath}: {row.DisplayValue}"));
        }
    }

    private static string ReadCountLabel(JsonObject? root, string propertyName, IReadOnlyList<SectionRowDisplayItem> rows, string prefix)
    {
        string? scalar = root is null ? null : ReadScalar(root, propertyName);
        if (!string.IsNullOrWhiteSpace(scalar))
        {
            return scalar;
        }

        int count = rows.Count(row => row.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return count.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildScalarFact(string? value, string label)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label} {value}";

    private static string BuildDetailLine(string label, string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value}";

    private static string BuildJoinedSummary(params string[] segments)
        => string.Join(" · ", segments.Where(static segment => !string.IsNullOrWhiteSpace(segment)));

    private static string BuildJoinedLines(params string[] lines)
        => string.Join(Environment.NewLine, lines.Where(static line => !string.IsNullOrWhiteSpace(line)));

    private void UpdateSectionRowsHeight()
    {
        if (AttributeParityEditorBorder.IsVisible)
        {
            SectionRowsList.MinHeight = 132d;
            SectionRowsList.MaxHeight = 360d;
            return;
        }

        bool denseChromeVisible = ClassicCharacterSheetBorder.IsVisible
            || GearWorkbenchBorder.IsVisible
            || SectionContextBorder.IsVisible
            || SectionActionTabStripBorder.IsVisible
            || SectionQuickActionsBorder.IsVisible;
        double rowHeight = denseChromeVisible ? 176d : 212d;
        if (SectionReviewPanel.IsVisible)
        {
            rowHeight -= 12d;
        }

        if (SectionQuickActionsBorder.IsVisible)
        {
            rowHeight -= 20d;
        }

        SectionRowsList.MinHeight = Math.Max(132d, Math.Min(rowHeight, 260d));
        SectionRowsList.MaxHeight = Math.Max(220d, rowHeight);
    }
}

public sealed record SectionHostState(
    string? SectionId,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    NavigatorSectionActionItem[] SectionActions,
    string? ActiveActionId,
    string Notice,
    string PreviewJson,
    SectionRowDisplayItem[] Rows,
    SectionQuickActionDisplayItem[] QuickActions,
    BuildLabConceptIntakeState? BuildLab,
    BrowseWorkspaceState? BrowseWorkspace,
    ContactRelationshipGraphState? ContactGraph,
    DowntimePlannerState? DowntimePlanner,
    NpcPersonaStudioState? NpcPersonaStudio,
    string? RulesetId = null);

internal sealed record SectionRowDisplayViewItem(string DisplayPath, string DisplayValue, string Path, string Value)
{
    public override string ToString()
        => $"{Path} = {Value}";
}

internal sealed record AttributeParityRowState(
    string AttributeName,
    int BaseValue,
    int KarmaValue,
    int MetatypeMin,
    int MetatypeMax,
    int MetatypeAugMax,
    int PriorityMaximum,
    int KarmaMaximum,
    bool BaseUnlocked,
    bool CareerMode,
    int AvailableKarma,
    int UpgradeKarmaCost,
    bool CanCareerUpgrade);

public sealed record SectionRowDisplayItem(string Path, string Value)
{
    public string DisplayPath => BuildDisplayPath(Path);
    public string DisplayValue => BuildDisplayValue(Path, Value);

    public string GetDisplayPath(string? rulesetId)
        => BuildDisplayPath(Path, rulesetId);

    public override string ToString()
    {
        return $"{Path} = {Value}";
    }

    private static string BuildDisplayPath(string path, string? rulesetId = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(value)";
        }

        string[] segments = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return "(value)";
        }

        string section = segments[0];
        string leaf = segments[^1];
        string bareLeaf = RemoveIndexer(leaf);
        string bareSection = RemoveIndexer(section);
        if (string.Equals(bareLeaf, "isvalid", StringComparison.OrdinalIgnoreCase))
        {
            return "Status";
        }

        if (string.Equals(section, "attributes", StringComparison.OrdinalIgnoreCase))
        {
            return FormatAttributeLabel(bareLeaf, rulesetId);
        }

        if (string.Equals(section, "combat", StringComparison.OrdinalIgnoreCase))
        {
            string combatKey = bareLeaf.Trim().ToLowerInvariant();
            return combatKey switch
            {
                "initiative" => "Init",
                "armor" => "Armor",
                "essence" => "Essence",
                _ => FormatDesktopLabel(leaf)
            };
        }

        if (string.Equals(bareLeaf, bareSection, StringComparison.OrdinalIgnoreCase))
        {
            return FormatCollectionLabel(bareSection, leaf);
        }

        return FormatDesktopLabel(leaf);
    }

    private static string BuildDisplayValue(string path, string value)
    {
        string displayValue = SanitizeValue(value);
        string leaf = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;
        string bareLeaf = RemoveIndexer(leaf);

        if (string.Equals(bareLeaf, "isvalid", StringComparison.OrdinalIgnoreCase))
        {
            return displayValue.Trim().ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => "Valid",
                "false" or "0" or "no" => "Needs attention",
                _ => displayValue
            };
        }

        if (string.Equals(bareLeaf, "issues", StringComparison.OrdinalIgnoreCase)
            && string.Equals(displayValue, "No entries", StringComparison.OrdinalIgnoreCase))
        {
            return "None";
        }

        return displayValue;
    }

    private static string SanitizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
    }

    private static string FormatDesktopLabel(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "(value)";
        }

        string normalized = token.Trim();
        int? ordinal = null;
        int bracketIndex = normalized.IndexOf('[');
        if (bracketIndex >= 0)
        {
            int closingBracketIndex = normalized.IndexOf(']', bracketIndex + 1);
            if (closingBracketIndex > bracketIndex + 1
                && int.TryParse(normalized[(bracketIndex + 1)..closingBracketIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex))
            {
                ordinal = parsedIndex + 1;
            }

            normalized = normalized[..bracketIndex];
        }

        normalized = normalized.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "(value)";
        }

        normalized = InsertWordBoundaries(normalized);
        string label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
        return ordinal is int index ? $"{label} {index}" : label;
    }

    private static string FormatCollectionLabel(string section, string token)
    {
        string normalizedSection = section.Trim().ToLowerInvariant() switch
        {
            "attributes" => "attribute",
            "skills" => "skill",
            "qualities" => "quality",
            "gear" => "gear",
            "weapons" => "weapon",
            "armors" => "armor",
            "cyberwares" => "cyberware",
            "vehicles" => "vehicle",
            "contacts" => "contact",
            "spells" => "spell",
            "powers" => "power",
            "drugs" => "drug",
            "aiprograms" => "program",
            "expenses" => "expense",
            "improvements" => "improvement",
            "complexforms" => "complex form",
            "sprites" => "sprite",
            "initiationgrades" => "initiation grade",
            "mentorspirits" => "mentor spirit",
            "progress" => "entry",
            "calendar" => "entry",
            _ => RemoveIndexer(token).Replace('_', ' ').Replace('-', ' ')
        };

        string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedSection);
        int bracketIndex = token.IndexOf('[');
        if (bracketIndex >= 0)
        {
            int closingBracketIndex = token.IndexOf(']', bracketIndex + 1);
            if (closingBracketIndex > bracketIndex + 1
                && int.TryParse(token[(bracketIndex + 1)..closingBracketIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return $"{title} {index + 1}";
            }
        }

        return title;
    }

    private static string FormatAttributeLabel(string attributeName, string? rulesetId)
        => IsSr6Ruleset(rulesetId)
            ? attributeName.Trim().ToLowerInvariant() switch
            {
                "body" => "Body",
                "agility" => "Agility",
                "reaction" => "Reaction",
                "strength" => "Strength",
                "willpower" => "Willpower",
                "logic" => "Logic",
                "intuition" => "Intuition",
                "charisma" => "Charisma",
                "edge" => "Edge",
                "magic" => "Magic",
                "resonance" => "Resonance",
                _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(attributeName.Trim().ToLowerInvariant())
            }
            : attributeName.Trim().ToLowerInvariant() switch
            {
                "body" => "BOD",
                "agility" => "AGI",
                "reaction" => "REA",
                "strength" => "STR",
                "willpower" => "WIL",
                "logic" => "LOG",
                "intuition" => "INT",
                "charisma" => "CHA",
                "edge" => "EDG",
                "magic" => "MAG",
                "resonance" => "RES",
                _ => attributeName.Length <= 3 ? attributeName.ToUpperInvariant() : attributeName[..Math.Min(3, attributeName.Length)].ToUpperInvariant()
            };

    private static bool IsSr6Ruleset(string? rulesetId)
        => string.Equals(RulesetDefaults.NormalizeOptional(rulesetId), RulesetDefaults.Sr6, StringComparison.Ordinal);

    private static string RemoveIndexer(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        int bracketIndex = token.IndexOf('[');
        return bracketIndex >= 0 ? token[..bracketIndex] : token;
    }

    private static string InsertWordBoundaries(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        StringBuilder builder = new(token.Length + 4);
        for (int i = 0; i < token.Length; i++)
        {
            char current = token[i];
            if (i > 0
                && char.IsUpper(current)
                && !char.IsWhiteSpace(token[i - 1])
                && !char.IsUpper(token[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}

public sealed record BrowseResultDisplayItem(string ItemId, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}

internal sealed record ExplainDrawerContext(
    string ExplainPacket,
    string? SourceAnchor,
    string? SourceLaunch,
    string? SourceLaunchTarget,
    string? StaleState,
    string? FollowUp);

public sealed record SectionQuickActionDisplayItem(string ControlId, string Label, bool IsPrimary);

public sealed record ClassicSheetFactDisplayItem(string Label, string Value);

internal sealed record GearWorkbenchItem(string Label, string Summary, string Detail);

internal sealed record GearWorkbenchState(
    string Title,
    string Summary,
    IReadOnlyList<GearWorkbenchItem> Items,
    IReadOnlyList<Border> Badges,
    string EmptyDetail);
