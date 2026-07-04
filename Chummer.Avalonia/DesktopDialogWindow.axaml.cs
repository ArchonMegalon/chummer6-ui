using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Chummer.Avalonia;

public partial class DesktopDialogWindow : Window
{
    private const string OriginWizardDialogId = "dialog.new_character.origin_wizard";
    private const string OriginWizardAdvancedStoryControlsExpanderName = "OriginDossierStandaloneAdvancedStoryControlsExpander";
    private static readonly TimeSpan[] DelayedOriginWizardComboRestoreDelays =
    [
        TimeSpan.FromMilliseconds(48),
        TimeSpan.FromMilliseconds(96)
    ];
    private static readonly TimeSpan OriginWizardTransientRefreshCloseGrace = TimeSpan.FromMilliseconds(300);
    private static readonly string UiKitAccessibilityAdapterMarker = AccessibilityPrimitiveBoundary.RootClass;
    private CharacterOverviewViewModelAdapter? _adapter;
    private readonly TextBlock _dialogTitleText;
    private readonly TextBlock _dialogMessageText;
    private readonly ContentControl _dialogTrustReceiptPanel;
    private readonly ScrollViewer _dialogScrollViewer;
    private readonly StackPanel _dialogFieldsPanel;
    private readonly Border _dialogActionsBorder;
    private readonly StackPanel _dialogActionsPanel;
    private IReadOnlyList<DesktopDialogField> _boundDialogFields = Array.Empty<DesktopDialogField>();
    private string? _preferredFocusControlName;
    private int? _preferredFocusSelectionStart;
    private bool _originWizardAdvancedStoryControlsExpanded;
    private Vector? _preferredDialogScrollAnchor;
    private int _preferredDialogScrollAnchorVersion;
    private (string ControlName, double OffsetY)? _preferredDialogViewportAnchor;
    private int _preferredDialogViewportAnchorVersion;
    private (string ControlName, double OffsetY)? _preferredDialogInteractionAnchor;
    private int _preferredDialogInteractionAnchorVersion;
    private bool _suppressProgrammaticComboFocusAnchorCapture;
    private bool _skipPreferredFocusRestoreOnNextBind;
    private bool _suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh;
    private int _dialogBindVersion;
    private bool _originWizardTransientRefreshPending;
    private DateTimeOffset _originWizardTransientRefreshPendingAtUtc;
    private int _originWizardTransientRefreshCloseDeferralVersion;
    private bool _suppressCloseNotification;
    private bool _suppressDialogUpdates;

    public DesktopDialogWindow()
    {
        InitializeComponent();

        _dialogTitleText = this.FindControl<TextBlock>("DialogTitleText")!;
        _dialogMessageText = this.FindControl<TextBlock>("DialogMessageText")!;
        _dialogTrustReceiptPanel = this.FindControl<ContentControl>("DialogTrustReceiptPanel")!;
        _dialogScrollViewer = this.FindControl<ScrollViewer>("DialogScrollViewer")!;
        _dialogFieldsPanel = this.FindControl<StackPanel>("DialogFieldsPanel")!;
        _dialogActionsBorder = this.FindControl<Border>("DialogActionsBorder")!;
        _dialogActionsPanel = this.FindControl<StackPanel>("DialogActionsPanel")!;
        Closing += OnClosing;
        Opened += OnOpened;
    }

    public DesktopDialogWindow(CharacterOverviewViewModelAdapter adapter)
        : this()
    {
        _adapter = adapter;
    }

    public string? BoundDialogId { get; private set; }

    internal byte[] CaptureScreenshotBytesForAutomation()
    {
        PixelSize pixelSize = new(
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));

        for (int attempt = 0; attempt < 3; attempt++)
        {
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
            Measure(new Size(pixelSize.Width, pixelSize.Height));
            Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
            Dispatcher.UIThread.RunJobs();
        }

        using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
        bitmap.Render(this);
        using MemoryStream output = new();
        bitmap.Save(output);
        return output.ToArray();
    }

    public void AttachAdapter(CharacterOverviewViewModelAdapter adapter)
    {
        _adapter = adapter;
    }

    public void BindDialog(DesktopDialogState dialog)
    {
        _dialogBindVersion++;
        _suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh = false;
        if (string.Equals(dialog.Id, OriginWizardDialogId, StringComparison.Ordinal))
        {
            ClearPendingOriginWizardTransientRefresh();
        }

        CapturePreferredFocusState();
        CaptureTransientDialogState();
        bool preservedOriginWizardAdvancedStoryControlsExpanded = _originWizardAdvancedStoryControlsExpanded;
        string? previousDialogId = BoundDialogId;
        Vector? preservedScrollOffset = CapturePreferredScrollOffset(dialog.Id);
        (string ControlName, double OffsetY)? preservedViewportAnchor = CapturePreferredDialogViewportAnchorSnapshot(dialog.Id);
        (string ControlName, double OffsetY)? preservedInteractionAnchor = CapturePreferredDialogInteractionAnchorSnapshot(dialog.Id);
        bool preserveInteractionContext = string.Equals(dialog.Id, previousDialogId, StringComparison.Ordinal);
        bool skipPreferredFocusRestore = preserveInteractionContext && _skipPreferredFocusRestoreOnNextBind;
        _skipPreferredFocusRestoreOnNextBind = false;
        BoundDialogId = dialog.Id;
        if (!string.Equals(BoundDialogId, previousDialogId, StringComparison.Ordinal)
            && !string.Equals(BoundDialogId, OriginWizardDialogId, StringComparison.Ordinal))
        {
            _originWizardAdvancedStoryControlsExpanded = false;
        }

        _boundDialogFields = dialog.Fields;
        if (!preserveInteractionContext)
        {
            ApplyDialogSizing(dialog.Id);
        }

        Title = dialog.Title;
        _dialogTitleText.Text = dialog.Title;
        string visibleMessage = SuppressDialogBanner(dialog.Id) ? string.Empty : dialog.Message ?? string.Empty;
        _dialogMessageText.Text = visibleMessage;
        _dialogTitleText.IsVisible = false;
        _dialogMessageText.IsVisible = false;
        string trustReceiptText = DesktopTrustReceiptText.BuildDialogReceipt(dialog);
        _dialogTrustReceiptPanel.Content = DesktopTrustPanelFactory.CreateDialogPanel(dialog, trustReceiptText);
        _dialogTrustReceiptPanel.IsVisible = false;
        ToolTip.SetTip(_dialogFieldsPanel, null);

        BuildFields(dialog.Fields);
        RestoreTransientDialogState(dialog.Id, preservedOriginWizardAdvancedStoryControlsExpanded);
        BuildActions(dialog.Actions);
        RefreshDialogVisuals();
        PrimePreferredScrollOffsetForDialogRebind(dialog.Id, preservedScrollOffset, preservedViewportAnchor, preservedInteractionAnchor);
        RestorePreferredScrollOffset(dialog.Id, preservedScrollOffset, preservedViewportAnchor, preservedInteractionAnchor);
        if (IsVisible)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!skipPreferredFocusRestore)
                {
                    FocusPreferredControlDuringRestore(allowFallback: !preserveInteractionContext);
                }

                RestorePreferredScrollOffset(dialog.Id, preservedScrollOffset, preservedViewportAnchor, preservedInteractionAnchor);
            }, DispatcherPriority.Input);
        }
    }

    public void CloseFromPresenter()
    {
        if (!IsVisible)
            return;

        _suppressCloseNotification = true;
        try
        {
            Close();
        }
        finally
        {
            _suppressCloseNotification = false;
        }
    }

    private void BuildFields(IReadOnlyList<DesktopDialogField> fields)
    {
        _suppressDialogUpdates = true;
        _dialogFieldsPanel.Children.Clear();
        if (TryBuildLegacyParityDialog(fields))
        {
            _suppressDialogUpdates = false;
            return;
        }

        DesktopDialogField[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .Where(ShouldRenderField)
            .ToArray();
        for (int index = 0; index < visibleFields.Length; index++)
        {
            DesktopDialogField field = visibleFields[index];
            if (string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Left, StringComparison.Ordinal)
                && index + 1 < visibleFields.Length
                && string.Equals(visibleFields[index + 1].LayoutSlot, DesktopDialogFieldLayoutSlots.Right, StringComparison.Ordinal))
            {
                _dialogFieldsPanel.Children.Add(CreateSplitFieldRow(field, visibleFields[index + 1]));
                index++;
                continue;
            }

            _dialogFieldsPanel.Children.Add(CreateStandaloneFieldRow(field));
        }

        _suppressDialogUpdates = false;
    }

    private bool TryBuildLegacyParityDialog(IReadOnlyList<DesktopDialogField> fields)
    {
        if (string.Equals(BoundDialogId, "dialog.global_settings", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyGlobalSettingsPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.new_character", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyNewCharacterPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.new_character.origin_wizard", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyOriginWizardPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.new_character.origin_build", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyOriginBuildPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.new_character.priority_workflow", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyPriorityWorkflowPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.new_character.karma_workflow", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyKarmaWorkflowPane(fields));
            return true;
        }

        if (TryBuildLegacySelectionAddDialog(fields, out Control? selectionAddPane))
        {
            _dialogFieldsPanel.Children.Add(selectionAddPane!);
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.character_settings", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyCharacterSettingsPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.dice_roller", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyDiceRollerPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.switch_ruleset", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacySwitchRulesetPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.master_index", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyMasterIndexPane(fields));
            return true;
        }

        if (string.Equals(BoundDialogId, "dialog.character_roster", StringComparison.Ordinal))
        {
            _dialogFieldsPanel.Children.Add(CreateLegacyCharacterRosterPane(fields));
            return true;
        }

        return false;
    }

    private bool TryBuildLegacySelectionAddDialog(IReadOnlyList<DesktopDialogField> fields, out Control? pane)
    {
        pane = null;
        if (string.IsNullOrWhiteSpace(BoundDialogId)
            || !BoundDialogId.StartsWith("dialog.ui.", StringComparison.Ordinal))
        {
            return false;
        }

        DesktopDialogField[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .Where(ShouldRenderField)
            .ToArray();
        if (visibleFields.Length == 0)
        {
            return false;
        }

        pane = CreateLegacySelectionAddPane(visibleFields);
        return true;
    }

    private Control CreateLegacySelectionAddPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField? navigationField = fields.FirstOrDefault(static field =>
            string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal));
        DesktopDialogField? candidateField = fields.FirstOrDefault(static field =>
            field.Id.Contains("CandidateList", StringComparison.Ordinal)
            && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal));
        DesktopDialogField? browseGridField = fields.FirstOrDefault(static field =>
            field.Id.Contains("BrowseGrid", StringComparison.Ordinal)
            && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal));

        HashSet<string> reservedIds = fields
            .Where(static field =>
                string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal)
                || field.Id.Contains("CandidateList", StringComparison.Ordinal)
                || field.Id.Contains("BrowseGrid", StringComparison.Ordinal))
            .Select(static field => field.Id)
            .ToHashSet(StringComparer.Ordinal);

        DesktopDialogField[] topFields = fields
            .Where(field => !reservedIds.Contains(field.Id))
            .Where(field => !field.IsReadOnly
                || string.Equals(field.InputType, "select", StringComparison.Ordinal)
                || string.Equals(field.InputType, "number", StringComparison.Ordinal)
                || string.Equals(field.InputType, "checkbox", StringComparison.Ordinal)
                || string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Detail, StringComparison.Ordinal))
            .Where(field => !IsSupportSummaryField(field))
            .ToArray();

        DesktopDialogField[] rightDetails = fields
            .Where(field => !reservedIds.Contains(field.Id))
            .Where(field => string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Right, StringComparison.Ordinal)
                || string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal)
                || string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Detail, StringComparison.Ordinal))
            .Where(field => !field.Id.Contains("SelectionTrail", StringComparison.Ordinal)
                && !field.Id.Contains("FilterSummary", StringComparison.Ordinal)
                && !field.Id.Contains("ResultCommands", StringComparison.Ordinal)
                && !field.Id.Contains("CategoryCommands", StringComparison.Ordinal))
            .Where(field => !topFields.Any(top => string.Equals(top.Id, field.Id, StringComparison.Ordinal)))
            .ToArray();

        StackPanel shell = new()
        {
            Spacing = 10
        };

        if (topFields.Length > 0)
        {
            shell.Children.Add(CreateClassicSelectionToolbar(
                BuildSelectionTopFieldRows(topFields).ToArray()));
        }

        Grid body = new()
        {
            ColumnDefinitions = new ColumnDefinitions("1.05*,0.95*"),
            ColumnSpacing = 14
        };

        StackPanel leftColumn = new()
        {
            Spacing = 10
        };
        if (navigationField is not null)
        {
            leftColumn.Children.Add(CreateSelectionSurfaceCard(ResolveSelectionNavigationTitle(navigationField), CreateLegacySelectionCategoryTreePanel(navigationField, fields), minHeight: 112));
        }

        if (candidateField is not null)
        {
            leftColumn.Children.Add(CreateSelectionSurfaceCard("Available", CreateLegacySelectionCandidatePanel(candidateField, fields), minHeight: 248));
        }

        if (leftColumn.Children.Count > 0)
        {
            Grid.SetColumn(leftColumn, 0);
            body.Children.Add(leftColumn);
        }

        StackPanel rightColumn = new()
        {
            Spacing = 10
        };

        if (browseGridField is not null)
        {
            rightColumn.Children.Add(CreateSelectionSurfaceCard(browseGridField.Label, CreateFieldControl(browseGridField), minHeight: 132));
        }

        foreach (DesktopDialogField detailField in rightDetails)
        {
            rightColumn.Children.Add(CreateSelectionSurfaceCard(detailField.Label, CreateFieldControl(detailField), minHeight: ResolveSelectionPanelMinHeight(detailField)));
        }

        if (rightColumn.Children.Count > 0)
        {
            Grid.SetColumn(rightColumn, 1);
            body.Children.Add(rightColumn);
        }

        if (body.Children.Count > 0)
        {
            shell.Children.Add(body);
        }

        return shell;
    }

    private static string ResolveSelectionNavigationTitle(DesktopDialogField field)
    {
        if (!string.Equals(field.Label, "Navigation", StringComparison.Ordinal))
        {
            return field.Label;
        }

        return field.Id.Contains("CategoryTree", StringComparison.Ordinal)
            ? "Categories"
            : "Current selection";
    }

    private IEnumerable<Control> BuildSelectionTopFieldRows(IReadOnlyList<DesktopDialogField> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            DesktopDialogField current = fields[index];
            if (index + 1 < fields.Count && CanPairSelectionTopField(current, fields[index + 1]))
            {
                yield return CreateSplitFieldRow(current, fields[index + 1]);
                index++;
                continue;
            }

            yield return CreateStandaloneFieldRow(current);
        }
    }

    private static bool CanPairSelectionTopField(DesktopDialogField left, DesktopDialogField right)
    {
        return !left.IsMultiline
            && !right.IsMultiline
            && !string.Equals(left.InputType, "checkbox", StringComparison.Ordinal)
            && !string.Equals(right.InputType, "checkbox", StringComparison.Ordinal);
    }

    private Control CreateLegacySelectionCandidatePanel(
        DesktopDialogField candidateField,
        IReadOnlyList<DesktopDialogField> allFields)
    {
        IReadOnlyList<SelectionCandidateItem> items = ParseSelectionCandidateItems(candidateField.Value);
        string? primaryFieldId = ResolveSelectionPrimaryFieldId(candidateField, allFields);
        string? selectedName = primaryFieldId is null
            ? items.FirstOrDefault(static item => item.IsSelected)?.Value
            : allFields.FirstOrDefault(field => string.Equals(field.Id, primaryFieldId, StringComparison.Ordinal))?.Value;

        ListBox listBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(candidateField.Id),
            ItemsSource = items,
            MinHeight = 248
        };
        ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate = new FuncDataTemplate<SelectionCandidateItem>((item, _) => BuildClassicSelectionCandidateRow(item));
        listBox.SelectionChanged += (_, _) =>
        {
            if (_suppressDialogUpdates)
            {
                return;
            }

            if (primaryFieldId is null || listBox.SelectedItem is not SelectionCandidateItem selectedItem)
            {
                return;
            }

            QueueDialogFieldUpdate(primaryFieldId, selectedItem.Value);
        };

        SelectionCandidateItem? selectedItem = items.FirstOrDefault(item =>
            string.Equals(item.Value, selectedName, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(static item => item.IsSelected)
            ?? items.FirstOrDefault();
        if (selectedItem is not null)
        {
            listBox.SelectedItem = selectedItem;
        }

        ApplyAccessibility(listBox, candidateField.AccessibleName, candidateField.ToolTip, candidateField.HelpText);

        StackPanel shell = new()
        {
            Spacing = 8
        };
        shell.Children.Add(CreateClassicSelectionSectionHeader(candidateField.Label, items.Count));
        shell.Children.Add(listBox);
        return shell;
    }

    private Control CreateLegacySelectionCategoryTreePanel(
        DesktopDialogField categoryTreeField,
        IReadOnlyList<DesktopDialogField> allFields)
    {
        string? categoryFieldId = ResolveSelectionCategoryFieldId(categoryTreeField, allFields);
        string? selectedName = ResolveSelectionCategoryValue(categoryTreeField, allFields, categoryFieldId);
        IReadOnlyList<SelectionCandidateItem> items = BuildSelectionCategoryItems(categoryTreeField, allFields, categoryFieldId);

        ListBox listBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(categoryTreeField.Id),
            ItemsSource = items,
            MinHeight = 112,
            Cursor = categoryFieldId is null ? null : new Cursor(StandardCursorType.Hand)
        };
        ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate = new FuncDataTemplate<SelectionCandidateItem>((item, _) => BuildClassicSelectionCandidateRow(item));
        listBox.SelectionChanged += (_, _) =>
        {
            if (_suppressDialogUpdates || categoryFieldId is null || listBox.SelectedItem is not SelectionCandidateItem selectedItem)
            {
                return;
            }

            QueueDialogFieldUpdate(categoryFieldId, selectedItem.Value);
        };

        SelectionCandidateItem? selectedItem = items.FirstOrDefault(item =>
            string.Equals(item.Value, selectedName, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(static item => item.IsSelected)
            ?? items.FirstOrDefault();
        if (selectedItem is not null)
        {
            listBox.SelectedItem = selectedItem;
        }

        ApplyAccessibility(listBox, categoryTreeField.AccessibleName, categoryTreeField.ToolTip, categoryTreeField.HelpText);
        return listBox;
    }

    private static IReadOnlyList<SelectionCandidateItem> BuildSelectionCategoryItems(
        DesktopDialogField categoryTreeField,
        IReadOnlyList<DesktopDialogField> allFields,
        string? categoryFieldId)
    {
        DesktopDialogField? categoryField = allFields.FirstOrDefault(field =>
            string.Equals(field.Id, categoryFieldId, StringComparison.Ordinal));
        DesktopDialogFieldOption[] options = categoryField?.Options?
            .DistinctBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        if (options.Length > 0)
        {
            return options
                .Where(static option => !string.IsNullOrWhiteSpace(option.Value))
                .Select(option => new SelectionCandidateItem(
                    option.Value,
                    option.Label,
                    string.Equals(option.Value, categoryField?.Value, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        return ParseSelectionTreeBranchItems(categoryTreeField.Value);
    }

    private static Border CreateClassicSelectionToolbar(params Control[] children)
    {
        StackPanel body = new()
        {
            Spacing = 6
        };
        foreach (Control child in children)
        {
            body.Children.Add(child);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSelectionToolbarBrush(),
            CornerRadius = default,
            Padding = new Thickness(6, 5),
            Child = body
        };
    }

    private static Border CreateSelectionSurfaceCard(string? title, Control content, double minHeight)
    {
        StackPanel shell = new()
        {
            Spacing = string.IsNullOrWhiteSpace(title) ? 0 : 5
        };
        if (!string.IsNullOrWhiteSpace(title))
        {
            shell.Children.Add(CreateClassicSelectionTitle(title));
        }
        shell.Children.Add(content);
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSelectionPanelBrush(),
            CornerRadius = default,
            Padding = new Thickness(6, 5),
            MinHeight = minHeight,
            Child = shell
        };
    }

    private static Control CreateClassicSelectionTitle(string title)
    {
        return new Border
        {
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 4),
            Child = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            }
        };
    }

    private static Control CreateClassicSelectionSectionHeader(string label, int count)
    {
        Grid header = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };

        TextBlock title = new()
        {
            Text = label,
            FontWeight = FontWeight.SemiBold
        };
        TextBlock badge = new()
        {
            Text = count.ToString(CultureInfo.InvariantCulture),
            FontSize = 12,
            Foreground = DesktopShellTheme.ResolveTextMutedBrush(),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(badge, 1);
        header.Children.Add(title);
        header.Children.Add(badge);

        return new Border
        {
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 4),
            Child = header
        };
    }

    private static Control BuildClassicSelectionCandidateRow(SelectionCandidateItem? item)
    {
        SplitSelectionCandidateDisplayText(item?.DisplayText ?? string.Empty, out string title, out string meta);

        StackPanel body = new()
        {
            Spacing = string.IsNullOrWhiteSpace(meta) ? 0 : 2,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "shell-option-label" }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(meta))
        {
            body.Children.Add(CreateOptionMetaText(meta));
        }

        return new Border
        {
            Padding = new Thickness(5, 4),
            Margin = new Thickness(0),
            Child = body
        };
    }

    private static void SplitSelectionCandidateDisplayText(string displayText, out string title, out string meta)
    {
        string[] segments = displayText
            .Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        title = segments.FirstOrDefault() ?? string.Empty;
        meta = segments.Length > 1
            ? string.Join("  |  ", segments.Skip(1))
            : string.Empty;
    }

    private static bool IsSupportSummaryField(DesktopDialogField field)
    {
        return string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Snippet, StringComparison.Ordinal)
            || field.Id.Contains("SelectionDetails", StringComparison.Ordinal)
            || field.Id.Contains("SelectionTrail", StringComparison.Ordinal)
            || field.Id.Contains("FilterSummary", StringComparison.Ordinal)
            || field.Id.Contains("ResultCommands", StringComparison.Ordinal)
            || field.Id.Contains("CategoryCommands", StringComparison.Ordinal)
            || field.Id.Contains("LiveRecalc", StringComparison.Ordinal)
            || field.Id.Contains("BrowseGrid", StringComparison.Ordinal);
    }

    private static IReadOnlyList<SelectionCandidateItem> ParseSelectionCandidateItems(string rawValue)
    {
        List<SelectionCandidateItem> items = [];
        foreach (string line in rawValue.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool isSelected = line.StartsWith(">", StringComparison.Ordinal);
            string normalized = line.TrimStart('>', '*', '-', ' ').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            items.Add(new SelectionCandidateItem(
                Value: ExtractSelectionCandidateName(normalized),
                DisplayText: normalized,
                IsSelected: isSelected));
        }

        return items;
    }

    private static IReadOnlyList<SelectionCandidateItem> ParseSelectionTreeBranchItems(string rawValue)
    {
        (int Level, string Value, bool IsSelected)[] nodes = rawValue
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseSelectionTreeNode)
            .Where(static node => node is not null)
            .Select(static node => node!.Value)
            .ToArray();

        if (nodes.Length == 0)
        {
            return Array.Empty<SelectionCandidateItem>();
        }

        int leafLevel = nodes.Max(static node => node.Level);
        return nodes
            .Where(node => node.Level == leafLevel)
            .Select(node => new SelectionCandidateItem(node.Value, node.Value, node.IsSelected))
            .ToArray();
    }

    private static (int Level, string Value, bool IsSelected)? ParseSelectionTreeNode(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            return null;
        }

        string valueLine = line.TrimEnd();
        int branchIndex = valueLine.IndexOf('├');
        if (branchIndex < 0)
        {
            branchIndex = valueLine.IndexOf('└');
        }

        if (branchIndex < 0)
        {
            return null;
        }

        bool isSelected = valueLine.Contains('>', StringComparison.Ordinal);
        string value = valueLine[(branchIndex + 2)..]
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : (branchIndex, value, isSelected);
    }

    private static string ExtractSelectionCandidateName(string displayText)
    {
        int separatorIndex = displayText.IndexOf(" · ", StringComparison.Ordinal);
        return separatorIndex > 0
            ? displayText[..separatorIndex].Trim()
            : displayText.Trim();
    }

    private static string? ResolveSelectionPrimaryFieldId(
        DesktopDialogField candidateField,
        IReadOnlyList<DesktopDialogField> allFields)
    {
        string prefix = candidateField.Id.EndsWith("CandidateList", StringComparison.Ordinal)
            ? candidateField.Id[..^"CandidateList".Length]
            : candidateField.Id;
        string[] preferredSuffixes =
        [
            "Name",
            "Reward",
            "Power",
            "Program",
            "Skill",
            "Spell",
            "Form",
            "Vehicle",
            "Weapon",
            "Armor",
            "Quality"
        ];

        foreach (string suffix in preferredSuffixes)
        {
            string preferredId = prefix + suffix;
            if (allFields.Any(field => string.Equals(field.Id, preferredId, StringComparison.Ordinal)))
            {
                return preferredId;
            }
        }

        return allFields
            .FirstOrDefault(field =>
                field.Id.StartsWith(prefix, StringComparison.Ordinal)
                && !field.IsReadOnly
                && !field.Id.Contains("Search", StringComparison.Ordinal)
                && !field.Id.Contains("Category", StringComparison.Ordinal)
                && !field.Id.Contains("BookFilter", StringComparison.Ordinal)
                && !field.Id.Contains("Sections", StringComparison.Ordinal)
                && !field.Id.Contains("Show", StringComparison.Ordinal)
                && !field.Id.Contains("Hide", StringComparison.Ordinal))?.Id;
    }

    private static string? ResolveSelectionCategoryFieldId(
        DesktopDialogField categoryTreeField,
        IReadOnlyList<DesktopDialogField> allFields)
    {
        string prefix = categoryTreeField.Id.EndsWith("CategoryTree", StringComparison.Ordinal)
            ? categoryTreeField.Id[..^"CategoryTree".Length]
            : categoryTreeField.Id;
        string[] preferredSuffixes =
        [
            "Category",
            "Type",
            "Family",
            "Track"
        ];

        foreach (string suffix in preferredSuffixes)
        {
            string preferredId = prefix + suffix;
            if (allFields.Any(field => string.Equals(field.Id, preferredId, StringComparison.Ordinal) && !field.IsReadOnly))
            {
                return preferredId;
            }
        }

        return null;
    }

    private static string? ResolveSelectionCategoryValue(
        DesktopDialogField categoryTreeField,
        IReadOnlyList<DesktopDialogField> allFields,
        string? categoryFieldId)
    {
        string prefix = categoryTreeField.Id.EndsWith("CategoryTree", StringComparison.Ordinal)
            ? categoryTreeField.Id[..^"CategoryTree".Length]
            : categoryTreeField.Id;
        DesktopDialogField? selectedBranchField = allFields.FirstOrDefault(field =>
            string.Equals(field.Id, prefix + "SelectedBranch", StringComparison.Ordinal));
        return selectedBranchField?.Value
            ?? allFields.FirstOrDefault(field => string.Equals(field.Id, categoryFieldId, StringComparison.Ordinal))?.Value;
    }

    private static double ResolveSelectionPanelMinHeight(DesktopDialogField field)
    {
        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal))
        {
            return 120d;
        }

        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Snippet, StringComparison.Ordinal))
        {
            return 68d;
        }

        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Narrative, StringComparison.Ordinal))
        {
            return 92d;
        }

        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal))
        {
            return 92d;
        }

        return field.IsMultiline ? 96d : 60d;
    }

    private sealed record SelectionCandidateItem(
        string Value,
        string DisplayText,
        bool IsSelected);

    private static bool SuppressDialogBanner(string? dialogId)
    {
        return string.Equals(dialogId, "dialog.new_character", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.dice_roller", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.global_settings", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.character_settings", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.switch_ruleset", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.master_index", StringComparison.Ordinal)
            || string.Equals(dialogId, "dialog.character_roster", StringComparison.Ordinal);
    }

    private Control CreateLegacyGlobalSettingsPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField themeField = FindRequiredField(fields, "globalTheme");
        DesktopDialogField uiScaleField = FindRequiredField(fields, "globalUiScale");
        DesktopDialogField languageField = FindRequiredField(fields, "globalLanguage");
        DesktopDialogField sheetLanguageField = FindRequiredField(fields, "globalSheetLanguage");
        DesktopDialogField compactModeField = FindRequiredField(fields, "globalCompactMode");
        DesktopDialogField characterPriorityField = FindRequiredField(fields, "globalCharacterPriority");
        DesktopDialogField updateModeField = FindRequiredField(fields, "globalUpdateMode");
        DesktopDialogField preferNightlyField = FindRequiredField(fields, "globalPreferNightlyBuilds");
        DesktopDialogField rosterPathField = FindRequiredField(fields, "globalCharacterRosterPath");
        DesktopDialogField hideMasterIndexField = FindRequiredField(fields, "globalHideMasterIndex");
        DesktopDialogField analyticsOptOutField = FindRequiredField(fields, "globalAnalyticsOptOut");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Global Options",
            CreateLegacyGlobalAppearanceRow(themeField, uiScaleField),
            CreateLegacySettingsPairRow(languageField, sheetLanguageField),
            CreateLegacySettingsPairRow(characterPriorityField, compactModeField),
            CreateLegacySettingsPairRow(updateModeField, preferNightlyField),
            CreateLegacySettingsPairRow(hideMasterIndexField, analyticsOptOutField),
            CreateLegacyRosterPathRow(rosterPathField)));

        shell.Children.Add(CreateLegacyHorizonWorkbenchPane());

        return shell;
    }

    private Control CreateLegacyHorizonWorkbenchPane()
    {
        var preferences = DesktopPreferenceRuntime.LoadOrCreateState("avalonia");
        StackPanel content = new()
        {
            Spacing = 10
        };

        content.Children.Add(new TextBlock
        {
            Text = "Desktop Tools",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "Open the main Chummer tools from desktop settings. Karma Forge includes package browsing, tracked packages, and direct create-package intake.",
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(CreateKarmaForgeWorkbenchRow());

        foreach (DesktopHorizonWorkbenchEntry entry in DesktopHorizonWorkbenchCatalog.ListEntries()
                     .Where(static item => !string.Equals(item.Id, "karma_forge", StringComparison.Ordinal))
                     .Where(item => !OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(item.Id, preferences)))
        {
            content.Children.Add(CreateHorizonWorkbenchRow(entry));
        }

        return CreateLegacyFieldGroup("Tools", content);
    }

    private Control CreateKarmaForgeWorkbenchRow()
    {
        IReadOnlyList<DesktopHorizonRouteOption> targets = DesktopHorizonWorkbenchCatalog.ListKarmaForgeTargets();
        ComboBox targetCombo = new()
        {
            MinWidth = 220,
            ItemsSource = targets,
            SelectedIndex = 0,
            ItemTemplate = new FuncDataTemplate<DesktopHorizonRouteOption>((option, _) =>
                new TextBlock
                {
                    Text = option.Label,
                    TextWrapping = TextWrapping.Wrap
                })
        };
        ApplyShellComboBoxTheme(targetCombo);

        Button openSelectedButton = CreateHorizonRouteButton(
            "Open",
            () => DesktopHorizonWorkbenchLauncher.OpenKarmaForgeAsync(this, "avalonia"));

        Button openSelectedRouteButton = CreateHorizonRouteButton(
            "Open selected route",
            () =>
            {
                if (targetCombo.SelectedItem is DesktopHorizonRouteOption selected)
                {
                    DesktopInstallLinkingRuntime.TryOpenRelativePortal(selected.RelativeHref);
                }
            });

        Button createPackageButton = CreateHorizonRouteButton(
            "Create new package",
            () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/participate/karma-forge#karma-forge-intake"));

        Button openTrackedPackagesButton = CreateHorizonRouteButton(
            "Open my packages",
            () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/packages"));

        return CreateHorizonWorkbenchCard(
            "Karma Forge",
            "Browse package candidates, open the signed-in package shelf, and jump straight into a new package intake.",
            targetCombo,
            openSelectedButton,
            openSelectedRouteButton,
            createPackageButton,
            openTrackedPackagesButton);
    }

    private Control CreateHorizonWorkbenchRow(DesktopHorizonWorkbenchEntry entry)
    {
        List<Control> actions =
        [
            CreateHorizonRouteButton("Open", () => DesktopHorizonWorkbenchLauncher.OpenAsync(this, "avalonia", entry)),
            CreateHorizonRouteButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref))
        ];

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateHorizonRouteButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref)));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateHorizonRouteButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref)));
        }

        return CreateHorizonWorkbenchCard(entry.Title, entry.Summary, null, actions.ToArray());
    }

    private static Control CreateHorizonWorkbenchCard(
        string title,
        string summary,
        Control? leadControl,
        params Control[] actions)
    {
        StackPanel card = new()
        {
            Spacing = 8
        };

        card.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        card.Children.Add(new TextBlock
        {
            Text = summary,
            TextWrapping = TextWrapping.Wrap
        });

        if (leadControl is not null)
        {
            card.Children.Add(leadControl);
        }

        WrapPanel actionsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };
        foreach (Control action in actions)
        {
            actionsPanel.Children.Add(action);
        }

        card.Children.Add(actionsPanel);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#020617"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = card
        };
    }

    private static Button CreateHorizonRouteButton(string label, Action onClick)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 128,
            Margin = new Thickness(0, 0, 8, 8)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Control CreateLegacyGlobalAppearanceRow(DesktopDialogField themeField, DesktopDialogField uiScaleField)
    {
        Grid row = new()
        {
            ColumnDefinitions = new ColumnDefinitions("156,*,156,*"),
            ColumnSpacing = 8
        };

        TextBlock themeLabel = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(themeField.Id),
            Text = themeField.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ApplyAccessibility(themeLabel, themeField.AccessibleName, themeField.ToolTip, themeField.HelpText);
        row.Children.Add(themeLabel);

        ComboBox themeCombo = BuildSelectComboBox(themeField, minWidth: 180);
        themeCombo.Name = DesktopDialogAccessibility.BuildFieldInputName(themeField.Id);
        ApplyAccessibility(themeCombo, themeField.AccessibleName, themeField.ToolTip, themeField.HelpText);
        Grid.SetColumn(themeCombo, 1);
        row.Children.Add(themeCombo);

        TextBlock uiScaleLabel = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(uiScaleField.Id),
            Text = uiScaleField.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ApplyAccessibility(uiScaleLabel, uiScaleField.AccessibleName, uiScaleField.ToolTip, uiScaleField.HelpText);
        Grid.SetColumn(uiScaleLabel, 2);
        row.Children.Add(uiScaleLabel);

        TextBox uiScaleTextBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(uiScaleField.Id),
            Text = uiScaleField.Value,
            IsReadOnly = uiScaleField.IsReadOnly,
            MinHeight = 24
        };
        ApplyTextBoxAccessibility(uiScaleTextBox, uiScaleField.AccessibleName, uiScaleField.ToolTip, uiScaleField.HelpText);
        if (!uiScaleField.IsReadOnly)
        {
            uiScaleTextBox.TextChanged += (_, _) =>
            {
                string nextValue = uiScaleTextBox.Text ?? string.Empty;
                if (!string.Equals(nextValue, uiScaleField.Value, StringComparison.Ordinal))
                {
                    QueueDialogFieldUpdate(uiScaleField.Id, nextValue);
                }
            };
        }

        Grid.SetColumn(uiScaleTextBox, 3);
        row.Children.Add(uiScaleTextBox);

        return row;
    }

    private Control CreateLegacySettingsPairRow(DesktopDialogField left, DesktopDialogField? right)
    {
        Grid row = new()
        {
            ColumnDefinitions = new ColumnDefinitions(right is null ? "156,*" : "156,*,156,*"),
            ColumnSpacing = 8,
            RowDefinitions = new RowDefinitions("Auto")
        };

        row.Children.Add(CreateLegacySettingsLabel(left, 0));
        row.Children.Add(CreateLegacySettingsInput(left, 1));

        if (right is not null)
        {
            row.Children.Add(CreateLegacySettingsLabel(right, 2));
            row.Children.Add(CreateLegacySettingsInput(right, 3));
        }

        return row;
    }

    private static Control CreateLegacySettingsLabel(DesktopDialogField field, int column)
    {
        Control label;
        if (string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
        {
            label = new TextBlock
            {
                Text = field.Label,
                Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
        }
        else
        {
            label = new TextBlock
            {
                Text = field.Label,
                Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = field.IsMultiline ? global::Avalonia.Layout.VerticalAlignment.Top : global::Avalonia.Layout.VerticalAlignment.Center
            };
        }

        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        Grid.SetColumn(label, column);
        return label;
    }

    private Control CreateLegacySettingsInput(DesktopDialogField field, int column)
    {
        Control input = CreateFieldControl(field);
        input.Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id);
        ApplyAccessibility(input, field.AccessibleName, field.ToolTip, field.HelpText);
        Grid.SetColumn(input, column);
        return input;
    }

    private Control CreateLegacyRosterPathRow(DesktopDialogField field)
    {
        Grid row = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldContainerName(field.Id),
            ColumnDefinitions = new ColumnDefinitions("156,*,Auto,Auto"),
            ColumnSpacing = 8
        };

        TextBlock label = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
            Text = field.Label,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(label);

        TextBox textBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
            Text = field.Value,
            IsReadOnly = field.IsReadOnly
        };
        ApplyTextBoxAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
        if (!field.IsReadOnly)
        {
            textBox.TextChanged += (_, _) =>
            {
                string nextValue = textBox.Text ?? string.Empty;
                if (!string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    QueueDialogFieldUpdate(field.Id, nextValue);
                }
            };
        }

        Grid.SetColumn(textBox, 1);
        row.Children.Add(textBox);

        Button browseButton = new()
        {
            Name = $"{field.Id}BrowseButton",
            Content = "Browse...",
            MinWidth = 88,
            IsEnabled = !field.IsReadOnly
        };
        ApplyAccessibility(
            browseButton,
            $"{field.Label} browse",
            $"Browse for {field.Label}.",
            $"Open the host folder picker and update {field.Label}.");
        browseButton.Click += async (_, _) =>
        {
            if (field.IsReadOnly)
            {
                return;
            }

            string? selectedPath = await MainWindowDesktopFileCoordinator.OpenFolderAsync(
                StorageProvider,
                "Select Character Roster Folder",
                CancellationToken.None);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || string.Equals(selectedPath, textBox.Text ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            textBox.Text = selectedPath;
            QueueDialogFieldUpdate(field.Id, selectedPath);
        };
        Grid.SetColumn(browseButton, 2);
        row.Children.Add(browseButton);

        Button clearButton = new()
        {
            Name = $"{field.Id}ClearButton",
            Content = "Clear",
            MinWidth = 72,
            IsEnabled = !field.IsReadOnly
        };
        ApplyAccessibility(
            clearButton,
            $"{field.Label} clear",
            $"Clear {field.Label}.",
            $"Remove the current {field.Label} value.");
        clearButton.Click += (_, _) =>
        {
            if (field.IsReadOnly)
            {
                return;
            }

            if (string.IsNullOrEmpty(textBox.Text))
            {
                return;
            }

            textBox.Text = string.Empty;
            QueueDialogFieldUpdate(field.Id, string.Empty);
        };
        Grid.SetColumn(clearButton, 3);
        row.Children.Add(clearButton);

        return row;
    }

    private Control CreateLegacyNewCharacterPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField nameField = FindRequiredField(fields, "newCharacterName");
        DesktopDialogField aliasField = FindRequiredField(fields, "newCharacterAlias");
        DesktopDialogField rulesetField = FindRequiredField(fields, "newCharacterRulesetId");
        DesktopDialogField buildMethodField = FindRequiredField(fields, "newCharacterBuildMethod");
        DesktopDialogField houseRulesField = FindRequiredField(fields, "newCharacterHouseRulesEnabled");

        StackPanel shell = new()
        {
            Spacing = 10
        };

        Grid settingRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("146,*,Auto"),
            ColumnSpacing = 10
        };
        TextBlock settingLabel = new()
        {
            Text = "Build method:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Name = DesktopDialogAccessibility.BuildFieldLabelName("newCharacterBuildMethod")
        };
        settingRow.Children.Add(settingLabel);

        ComboBox buildMethodCombo = BuildSelectComboBox(buildMethodField, minWidth: 220);
        buildMethodCombo.Name = DesktopDialogAccessibility.BuildFieldInputName("newCharacterBuildMethod");
        Grid.SetColumn(buildMethodCombo, 1);
        settingRow.Children.Add(buildMethodCombo);

        Button modifyButton = new()
        {
            Name = "newCharacterModifyButton",
            Content = "Options",
            MinWidth = 88
        };
        Control optionsPanel = CreateLegacyFieldGroup(
            "Options",
            CreateStandaloneFieldRow(houseRulesField));
        optionsPanel.Name = "newCharacterOptionsPanel";
        optionsPanel.IsVisible = false;
        ApplyAccessibility(
            modifyButton,
            "Show character options",
            "Show character options.",
            "Show house-rule options without closing this build dialog.");
        modifyButton.Click += (_, _) =>
        {
            optionsPanel.IsVisible = !optionsPanel.IsVisible;
        };
        Grid.SetColumn(modifyButton, 2);
        settingRow.Children.Add(modifyButton);

        Grid rulesetRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("146,*"),
            ColumnSpacing = 10
        };
        TextBlock rulesetLabel = new()
        {
            Text = "Ruleset:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Name = DesktopDialogAccessibility.BuildFieldLabelName("newCharacterRulesetId")
        };
        rulesetRow.Children.Add(rulesetLabel);

        ComboBox rulesetCombo = BuildSelectComboBox(rulesetField, minWidth: 180);
        rulesetCombo.Name = DesktopDialogAccessibility.BuildFieldInputName("newCharacterRulesetId");
        Grid.SetColumn(rulesetCombo, 1);
        rulesetRow.Children.Add(rulesetCombo);

        shell.Children.Add(CreateLegacyFieldGroup(
            "New Runner",
            CreateLegacyGroupLead("Choose the ruleset, build method, and runner name before the character opens."),
            settingRow,
            rulesetRow,
            CreateSplitFieldRow(nameField, aliasField)));
        shell.Children.Add(optionsPanel);

        return shell;
    }

    private Control CreateLegacyOriginWizardPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField nameField = FindRequiredField(fields, "newCharacterName");
        DesktopDialogField aliasField = FindRequiredField(fields, "newCharacterAlias");
        DesktopDialogField rulesetField = FindRequiredField(fields, "newCharacterRulesetId");
        DesktopDialogField archetypeField = FindRequiredField(fields, "newCharacterOriginArchetypeIntent");
        DesktopDialogField buildPreferenceField = FindRequiredField(fields, "newCharacterOriginBuildPreference");
        DesktopDialogField metatypePreferenceField = FindRequiredField(fields, "newCharacterOriginMetatypePreference");
        DesktopDialogField backgroundField = FindRequiredField(fields, "newCharacterOriginBackground");
        DesktopDialogField turningPointField = FindRequiredField(fields, "newCharacterOriginTurningPoint");
        DesktopDialogField trainingPathField = FindRequiredField(fields, "newCharacterOriginTrainingPath");
        DesktopDialogField pressureCostField = FindRequiredField(fields, "newCharacterOriginPressureCost");
        DesktopDialogField upgradeExposureField = FindRequiredField(fields, "newCharacterOriginUpgradeExposure");
        DesktopDialogField motivationField = FindRequiredField(fields, "newCharacterOriginMotivation");
        DesktopDialogField toneField = FindRequiredField(fields, "newCharacterOriginTone");
        DesktopDialogField gmPresetField = FindRequiredField(fields, "newCharacterOriginGmConstraintPreset");
        DesktopDialogField gmRequirementsField = FindRequiredField(fields, "newCharacterOriginGmRequirements");
        DesktopDialogField summaryField = FindRequiredField(fields, "newCharacterOriginSummary");
        DesktopDialogField buildMethodField = FindRequiredField(fields, "newCharacterOriginBuildMethod");
        DesktopDialogField metatypeField = FindRequiredField(fields, "newCharacterOriginMetatype");
        DesktopDialogField qualityFocusField = FindRequiredField(fields, "newCharacterOriginQualityFocus");
        DesktopDialogField pathSummaryField = FindRequiredField(fields, "newCharacterOriginPathSummary");
        DesktopDialogField gmSummaryField = FindRequiredField(fields, "newCharacterOriginGmRequirementSummary");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Story",
            CreateLegacyGroupLead("Pick only the basics, then build the story. Advanced controls are optional."),
            CreateSplitFieldRow(metatypePreferenceField, archetypeField),
            CreateOriginSummaryStrip(
                ("Metatype", metatypeField.Value),
                ("Archetype", archetypeField.Value),
                ("Path", pathSummaryField.Value))));

        Grid lifePathGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        StackPanel leftPath = new()
        {
            Spacing = 8,
            Children =
            {
                CreateStandaloneFieldRow(backgroundField),
                CreateStandaloneFieldRow(trainingPathField),
                CreateStandaloneFieldRow(pressureCostField)
            }
        };
        StackPanel rightPath = new()
        {
            Spacing = 8,
            Children =
            {
                CreateStandaloneFieldRow(turningPointField),
                CreateStandaloneFieldRow(upgradeExposureField),
                CreateStandaloneFieldRow(motivationField),
                CreateStandaloneFieldRow(toneField)
            }
        };
        lifePathGrid.Children.Add(leftPath);
        Grid.SetColumn(rightPath, 1);
        lifePathGrid.Children.Add(rightPath);

        Expander advancedStoryControls = new()
        {
            Name = OriginWizardAdvancedStoryControlsExpanderName,
            Header = "Advanced story controls",
            IsExpanded = _originWizardAdvancedStoryControlsExpanded,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    CreateLegacyFieldGroup(
                        "Runner",
                        CreateLegacyGroupLead("Optional identity and rules context for the story packet."),
                        CreateSplitFieldRow(nameField, aliasField),
                        CreateSplitFieldRow(rulesetField, buildPreferenceField)),
                    CreateLegacyFieldGroup(
                        "Life Path",
                        CreateLegacyGroupLead("Optional life-module-style steering: where the runner came from, what broke, how they trained, and what still costs them."),
                        lifePathGrid),
                    CreateLegacyFieldGroup(
                        "GM Steering",
                        CreateLegacyGroupLead("Optional table permissions or requirements. These guide the story and build handoff; they do not edit a sheet by themselves."),
                        CreateSplitFieldRow(gmPresetField, gmRequirementsField),
                        CreateOriginSummaryStrip(("Applied GM Constraint", gmSummaryField.Value), ("Pressure", qualityFocusField.Value)))
                }
            }
        };
        int advancedStoryControlsBindVersion = _dialogBindVersion;
        advancedStoryControls.Expanded += (_, _) =>
        {
            if (_suppressDialogUpdates || advancedStoryControlsBindVersion != _dialogBindVersion)
            {
                return;
            }

            _originWizardAdvancedStoryControlsExpanded = true;
        };
        advancedStoryControls.Collapsed += (_, _) =>
        {
            if (_suppressDialogUpdates
                || advancedStoryControlsBindVersion != _dialogBindVersion
                || _suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh)
            {
                return;
            }

            _originWizardAdvancedStoryControlsExpanded = false;
        };
        shell.Children.Add(advancedStoryControls);

        shell.Children.Add(CreateLegacySummaryCard(
            "Story Preview",
            "Review the story seed first. Mechanics can follow after the story is accepted.",
            CreateNarrativePanel(summaryField.Value, minHeight: 120, maxHeight: 240)));

        return shell;
    }

    private Control CreateLegacyOriginBuildPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField bookField = FindRequiredField(fields, "newCharacterOriginBookPreview");
        DesktopDialogField storyField = FindRequiredField(fields, "newCharacterOriginStory");
        DesktopDialogField buildLogicField = FindRequiredField(fields, "newCharacterOriginBuildLogic");
        DesktopDialogField implicationsField = FindRequiredField(fields, "newCharacterOriginImplications");
        DesktopDialogField rulesetField = FindRequiredField(fields, "newCharacterWorkflowRulesetId");
        DesktopDialogField methodField = FindRequiredField(fields, "newCharacterWorkflowBuildMethod");
        DesktopDialogField aliasField = FindRequiredField(fields, "newCharacterWorkflowAlias");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Build Handoff",
            CreateOriginSummaryStrip(
                ("Runner", aliasField.Value),
                ("Ruleset", rulesetField.Value.ToUpperInvariant()),
                ("Method", methodField.Value))));

        shell.Children.Add(CreateLegacySummaryCard(
            "Book Preview",
            "Read this first. Character creation starts after the story feels right.",
            CreateFieldControl(bookField)));

        shell.Children.Add(CreateLegacySummaryCard(
            "Story",
            "Alice can use this text later; it does not change the sheet by itself.",
            CreateNarrativePanel(storyField.Value, minHeight: 144, maxHeight: 260)));

        Grid supportGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        supportGrid.Children.Add(CreateLegacySummaryCard(
            "Build Translation",
            "The handoff translates the story into a normal guided character-creation path.",
            CreateFieldControl(buildLogicField)));
        Border constraintsCard = CreateLegacySummaryCard(
            "Constraints",
            "GM grants and requirements stay visible before opening chargen.",
            CreateFieldControl(implicationsField));
        Grid.SetColumn(constraintsCard, 1);
        supportGrid.Children.Add(constraintsCard);
        shell.Children.Add(supportGrid);
        return shell;
    }

    private static Control CreateOriginSummaryStrip(params (string Label, string Value)[] metrics)
    {
        WrapPanel panel = new()
        {
            Orientation = Orientation.Horizontal
        };

        foreach ((string label, string value) in metrics)
        {
            panel.Children.Add(new Border
            {
                Classes = { "shell-panel" },
                Padding = new Thickness(8, 5),
                Margin = new Thickness(0, 0, 8, 8),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = label,
                            Classes = { "shell-kicker" }
                        },
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(value) ? "Pending" : value,
                            Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            });
        }

        return panel;
    }

    private Control CreateLegacyPriorityWorkflowPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField categoryField = FindRequiredField(fields, "newCharacterMetatypeCategory");
        DesktopDialogField metatypeField = FindRequiredField(fields, "newCharacterMetatype");
        DesktopDialogField heritageField = FindRequiredField(fields, "newCharacterPriorityHeritage");
        DesktopDialogField attributesField = FindRequiredField(fields, "newCharacterPriorityAttributes");
        DesktopDialogField talentPriorityField = FindRequiredField(fields, "newCharacterPriorityTalent");
        DesktopDialogField skillsField = FindRequiredField(fields, "newCharacterPrioritySkills");
        DesktopDialogField resourcesField = FindRequiredField(fields, "newCharacterPriorityResources");
        DesktopDialogField talentChoiceField = FindRequiredField(fields, "newCharacterPriorityTalentChoice");
        DesktopDialogField metavariantField = FindOptionalField(fields, "newCharacterMetavariant")
            ?? new DesktopDialogField("newCharacterMetavariant", "Metavariant", string.Empty, string.Empty, InputType: "select");
        DesktopDialogField skillChoice1Field = FindOptionalField(fields, "newCharacterPrioritySkillChoice1")
            ?? new DesktopDialogField("newCharacterPrioritySkillChoice1", "Skill Choice 1", string.Empty, string.Empty, InputType: "select");
        DesktopDialogField skillChoice2Field = FindOptionalField(fields, "newCharacterPrioritySkillChoice2")
            ?? new DesktopDialogField("newCharacterPrioritySkillChoice2", "Skill Choice 2", string.Empty, string.Empty, InputType: "select");
        DesktopDialogField skillChoice3Field = FindOptionalField(fields, "newCharacterPrioritySkillChoice3")
            ?? new DesktopDialogField("newCharacterPrioritySkillChoice3", "Skill Choice 3", string.Empty, string.Empty, InputType: "select");
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            FindOptionalField(fields, "newCharacterPriorityWorkflowState")?.Value);

        static TextBlock CreateRowLabel(string text, string? name = null) => new()
        {
            Name = name,
            Text = text,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        ComboBox BuildRuntimeCombo(DesktopDialogField field, double minWidth = 160d)
        {
            ComboBox combo = BuildSelectComboBox(field, minWidth);
            combo.Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id);
            ApplyAccessibility(combo, field.AccessibleName, field.ToolTip, field.HelpText);
            return combo;
        }

        Grid topMatrix = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,132,Auto,132,Auto,160"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        topMatrix.Children.Add(CreateRowLabel("Metatype:", DesktopDialogAccessibility.BuildFieldLabelName(heritageField.Id)));
        ComboBox heritageCombo = BuildRuntimeCombo(heritageField, 120);
        Grid.SetColumn(heritageCombo, 1);
        topMatrix.Children.Add(heritageCombo);

        TextBlock attributesLabel = CreateRowLabel("Attributes:", DesktopDialogAccessibility.BuildFieldLabelName(attributesField.Id));
        Grid.SetColumn(attributesLabel, 2);
        topMatrix.Children.Add(attributesLabel);
        ComboBox attributesCombo = BuildRuntimeCombo(attributesField, 120);
        Grid.SetColumn(attributesCombo, 3);
        topMatrix.Children.Add(attributesCombo);

        TextBlock magicLabel = CreateRowLabel("Magic or Resonance:", DesktopDialogAccessibility.BuildFieldLabelName(talentPriorityField.Id));
        Grid.SetColumn(magicLabel, 4);
        topMatrix.Children.Add(magicLabel);
        ComboBox talentPriorityCombo = BuildRuntimeCombo(talentPriorityField, 150);
        Grid.SetColumn(talentPriorityCombo, 5);
        topMatrix.Children.Add(talentPriorityCombo);

        TextBlock skillsLabel = CreateRowLabel("Skills:", DesktopDialogAccessibility.BuildFieldLabelName(skillsField.Id));
        Grid.SetRow(skillsLabel, 1);
        topMatrix.Children.Add(skillsLabel);
        ComboBox skillsCombo = BuildRuntimeCombo(skillsField, 120);
        Grid.SetColumn(skillsCombo, 1);
        Grid.SetRow(skillsCombo, 1);
        topMatrix.Children.Add(skillsCombo);

        TextBlock resourcesLabel = CreateRowLabel("Resources:", DesktopDialogAccessibility.BuildFieldLabelName(resourcesField.Id));
        Grid.SetColumn(resourcesLabel, 2);
        Grid.SetRow(resourcesLabel, 1);
        topMatrix.Children.Add(resourcesLabel);
        ComboBox resourcesCombo = BuildRuntimeCombo(resourcesField, 120);
        Grid.SetColumn(resourcesCombo, 3);
        Grid.SetRow(resourcesCombo, 1);
        topMatrix.Children.Add(resourcesCombo);

        TextBlock talentChoiceLabel = CreateRowLabel("Talent Choice:", DesktopDialogAccessibility.BuildFieldLabelName(talentChoiceField.Id));
        Grid.SetColumn(talentChoiceLabel, 4);
        Grid.SetRow(talentChoiceLabel, 1);
        topMatrix.Children.Add(talentChoiceLabel);
        ComboBox talentChoiceCombo = BuildRuntimeCombo(talentChoiceField, 150);
        Grid.SetColumn(talentChoiceCombo, 5);
        Grid.SetRow(talentChoiceCombo, 1);
        topMatrix.Children.Add(talentChoiceCombo);

        TextBlock sumToTenLabel = new()
        {
            Name = "newCharacterPrioritySumToTenLabel",
            Text = runtimeState.SumToTenLabel,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResolveThemeBrush("ChummerShellInfoBrush", "#173A6C"),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            IsVisible = !string.IsNullOrWhiteSpace(runtimeState.SumToTenLabel)
        };
        Grid.SetColumn(sumToTenLabel, 5);
        Grid.SetRow(sumToTenLabel, 2);
        topMatrix.Children.Add(sumToTenLabel);

        Border topMatrixBorder = new()
        {
            Child = CreateLegacySummaryCard(
                "Priority Allocation",
                "Assign the five priority lanes, then confirm the metatype and choice-dependent follow-through.",
                topMatrix)
        };

        ComboBox categoryCombo = BuildRuntimeCombo(categoryField, 180);
        ListBox metatypeList = BuildSelectListBox(metatypeField);
        metatypeList.Name = DesktopDialogAccessibility.BuildFieldInputName(metatypeField.Id);
        metatypeList.MinHeight = 280;
        ApplyAccessibility(metatypeList, metatypeField.AccessibleName, metatypeField.ToolTip, metatypeField.HelpText);
        metatypeList.DoubleTapped += async (_, _) =>
        {
            if (_adapter is null)
            {
                return;
            }

            await ExecuteSafeAsync(
                () => _adapter.ExecuteDialogActionAsync("complete_new_character_workflow", CancellationToken.None),
                "complete new character workflow");
        };

        StackPanel browseLane = new()
        {
            Spacing = 8,
            Children =
            {
                CreateRowLabel("Filter:", DesktopDialogAccessibility.BuildFieldLabelName(categoryField.Id)),
                categoryCombo,
                CreateRowLabel("Metatypes:", "newCharacterMetatypeListLabel"),
                metatypeList
            }
        };

        Border browseLaneBorder = new()
        {
            Child = CreateLegacySummaryCard(
                "Browse Metatypes",
                "Use category first, then choose the concrete metatype from the list.",
                browseLane)
        };

        Grid rightFactsGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6
        };
        AddLabeledValueRow(
            rightFactsGrid,
            0,
            "Metavariant:",
            BuildRuntimeCombo(metavariantField with { Options = runtimeState.MetavariantOptions }, 180));
        AddLabeledValueRow(rightFactsGrid, 1, "Karma:", new TextBlock { Text = runtimeState.MetatypeKarma });
        AddLabeledValueRow(rightFactsGrid, 2, "Special Attributes:", new TextBlock { Text = runtimeState.SpecialAttributes });
        AddLabeledValueRow(rightFactsGrid, 3, "Source:", new TextBlock { Text = runtimeState.Source, TextWrapping = TextWrapping.Wrap });

        Grid inspectAttributesGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            ColumnSpacing = 8,
            RowSpacing = 4
        };
        for (int index = 0; index < runtimeState.InspectAttributes.Count; index++)
        {
            PriorityWorkflowInspectAttributeState attribute = runtimeState.InspectAttributes[index];
            int row = index / 2;
            while (inspectAttributesGrid.RowDefinitions.Count <= row)
            {
                inspectAttributesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            int column = (index % 2) * 2;
            TextBlock label = CreateRowLabel(attribute.Label);
            Grid.SetRow(label, row);
            Grid.SetColumn(label, column);
            inspectAttributesGrid.Children.Add(label);

            TextBlock value = new()
            {
                Text = attribute.Value,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            Grid.SetRow(value, row);
            Grid.SetColumn(value, column + 1);
            inspectAttributesGrid.Children.Add(value);
        }

        ListBox qualitiesList = new()
        {
            Name = "newCharacterPriorityQualitiesList",
            ItemsSource = runtimeState.Qualities.Count == 0 ? new[] { "No innate qualities" } : runtimeState.Qualities,
            MinHeight = 96
        };
        ApplyShellListBoxTheme(qualitiesList);
        qualitiesList.ItemTemplate = new FuncDataTemplate<string>((line, _) =>
            CreateOptionText(line ?? string.Empty, TextWrapping.Wrap));

        StackPanel inspectLane = new()
        {
            Spacing = 10,
            Children =
            {
                rightFactsGrid,
                new Border
                {
                    Classes = { "shell-panel", "subtle" },
                    Padding = new Thickness(8),
                    Child = inspectAttributesGrid
                },
                CreateRowLabel("Qualities:"),
                qualitiesList
            }
        };

        Border inspectLaneBorder = new()
        {
            Child = CreateLegacySummaryCard(
                "Selection Detail",
                "Review karma cost, special attributes, source, and inherited qualities before confirming.",
                inspectLane)
        };

        Grid centerGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("300,*"),
            ColumnSpacing = 12
        };
        centerGrid.Children.Add(browseLaneBorder);
        Grid.SetColumn(inspectLaneBorder, 1);
        centerGrid.Children.Add(inspectLaneBorder);

        StackPanel skillChoiceLane = new()
        {
            Spacing = 6,
            IsVisible = skillChoice1Field.Options is { Count: > 0 } || skillChoice2Field.Options is { Count: > 0 } || skillChoice3Field.Options is { Count: > 0 }
        };
        if (!string.IsNullOrWhiteSpace(runtimeState.SkillSelectionLabel))
        {
            skillChoiceLane.Children.Add(CreateRowLabel(runtimeState.SkillSelectionLabel, "newCharacterPrioritySkillSelectionLabel"));
        }

        if (runtimeState.SkillChoice1.Visible)
        {
            skillChoiceLane.Children.Add(BuildRuntimeCombo(skillChoice1Field with { Options = runtimeState.SkillChoice1.Options }, 220));
        }

        if (runtimeState.SkillChoice2.Visible)
        {
            skillChoiceLane.Children.Add(BuildRuntimeCombo(skillChoice2Field with { Options = runtimeState.SkillChoice2.Options }, 220));
        }

        if (runtimeState.SkillChoice3.Visible)
        {
            skillChoiceLane.Children.Add(BuildRuntimeCombo(skillChoice3Field with { Options = runtimeState.SkillChoice3.Options }, 220));
        }

        Border skillChoiceBorder = new()
        {
            Child = CreateLegacySummaryCard(
                "Choice Follow-Through",
                "Complete the skill choices unlocked by the selected priorities.",
                skillChoiceLane),
            IsVisible = skillChoiceLane.Children.Count > 0
        };

        StackPanel shell = new()
        {
            Spacing = 14,
            Children =
            {
                topMatrixBorder,
                centerGrid
            }
        };

        if (skillChoiceBorder.IsVisible)
        {
            shell.Children.Add(skillChoiceBorder);
        }

        return shell;
    }

    private Control CreateLegacyKarmaWorkflowPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField categoryField = FindRequiredField(fields, "newCharacterMetatypeCategory");
        DesktopDialogField metatypeField = FindRequiredField(fields, "newCharacterMetatype");
        DesktopDialogField summaryField = FindRequiredField(fields, "newCharacterKarmaWorkflowSummary");

        static TextBlock CreateRowLabel(string text, string? name = null) => new()
        {
            Name = name,
            Text = text,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        ComboBox categoryCombo = BuildSelectComboBox(categoryField, 180);
        categoryCombo.Name = DesktopDialogAccessibility.BuildFieldInputName(categoryField.Id);
        ApplyAccessibility(categoryCombo, categoryField.AccessibleName, categoryField.ToolTip, categoryField.HelpText);

        ComboBox metatypeCombo = BuildSelectComboBox(metatypeField, 180);
        metatypeCombo.Name = DesktopDialogAccessibility.BuildFieldInputName(metatypeField.Id);
        ApplyAccessibility(metatypeCombo, metatypeField.AccessibleName, metatypeField.ToolTip, metatypeField.HelpText);

        Grid selectorGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,200,Auto,200"),
            ColumnSpacing = 14,
            RowSpacing = 10
        };
        selectorGrid.Children.Add(CreateRowLabel("Show Metatypes:", DesktopDialogAccessibility.BuildFieldLabelName(categoryField.Id)));
        Grid.SetColumn(categoryCombo, 1);
        selectorGrid.Children.Add(categoryCombo);

        TextBlock metatypeLabel = CreateRowLabel("Metatype:", DesktopDialogAccessibility.BuildFieldLabelName(metatypeField.Id));
        Grid.SetColumn(metatypeLabel, 2);
        selectorGrid.Children.Add(metatypeLabel);
        Grid.SetColumn(metatypeCombo, 3);
        selectorGrid.Children.Add(metatypeCombo);

        Border selectorBorder = new()
        {
            Child = CreateLegacySummaryCard(
                "Metatype Selection",
                "Pick which metatypes to show, then choose one.",
                selectorGrid)
        };

        TextBlock summaryText = new()
        {
            Name = "newCharacterKarmaWorkflowSummaryText",
            Text = summaryField.Value,
            TextWrapping = TextWrapping.Wrap
        };

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                selectorBorder,
                new Border
                {
                    BorderThickness = new Thickness(1),
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                    Background = DesktopShellTheme.ResolveSurfaceBrush(),
                    CornerRadius = default,
                    Padding = new Thickness(8, 6),
                    Child = summaryText
                }
            }
        };
    }

    private Control CreateLegacyCharacterSettingsPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField priorityField = FindRequiredField(fields, "characterPriority");
        DesktopDialogField karmaRatioField = FindRequiredField(fields, "characterKarmaNuyen");
        DesktopDialogField houseRulesField = FindRequiredField(fields, "characterHouseRulesEnabled");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Character Defaults",
            CreateSplitFieldRow(priorityField, karmaRatioField),
            CreateStandaloneFieldRow(houseRulesField)));

        return shell;
    }

    private Control CreateLegacyDiceRollerPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField methodField = FindRequiredField(fields, "diceMethod");
        DesktopDialogField diceCountField = FindRequiredField(fields, "diceCount");
        DesktopDialogField thresholdField = FindRequiredField(fields, "diceThreshold");
        DesktopDialogField gremlinsField = FindRequiredField(fields, "diceGremlins");
        DesktopDialogField ruleOf6Field = FindRequiredField(fields, "diceRuleOf6");
        DesktopDialogField cinematicGameplayField = FindRequiredField(fields, "diceCinematicGameplay");
        DesktopDialogField rushJobField = FindRequiredField(fields, "diceRushJob");
        DesktopDialogField bubbleDieField = FindRequiredField(fields, "diceBubbleDie");
        DesktopDialogField variableGlitchField = FindRequiredField(fields, "diceVariableGlitch");
        DesktopDialogField resultsSummaryField = FindRequiredField(fields, "diceResultsSummary");
        DesktopDialogField resultsListField = FindRequiredField(fields, "diceResultsList");

        Grid shell = new()
        {
            ColumnDefinitions = new ColumnDefinitions("132,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            ColumnSpacing = 12,
            RowSpacing = 10
        };

        Grid topBar = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,56,Auto,*,220,Auto,Auto"),
            ColumnSpacing = 8
        };
        Grid.SetColumnSpan(topBar, 2);

        TextBlock rollLabel = new()
        {
            Text = "Roll",
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        topBar.Children.Add(rollLabel);

        NumericUpDown diceCountEditor = BuildLegacyInlineNumericUpDown(diceCountField, width: 56);
        Grid.SetColumn(diceCountEditor, 1);
        topBar.Children.Add(diceCountEditor);

        TextBlock d6Label = new()
        {
            Text = "D6",
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(d6Label, 2);
        topBar.Children.Add(d6Label);

        ComboBox methodCombo = BuildSelectComboBox(methodField, minWidth: 220);
        methodCombo.Name = DesktopDialogAccessibility.BuildFieldInputName(methodField.Id);
        ApplyAccessibility(methodCombo, methodField.AccessibleName, methodField.ToolTip, methodField.HelpText);
        Grid.SetColumn(methodCombo, 4);
        topBar.Children.Add(methodCombo);

        Button rollButton = CreateLegacyActionButton("roll", "Roll", isPrimary: true);
        Grid.SetColumn(rollButton, 5);
        topBar.Children.Add(rollButton);

        Button rerollButton = CreateLegacyActionButton("reroll_misses", "Re-Roll Misses");
        Grid.SetColumn(rerollButton, 6);
        topBar.Children.Add(rerollButton);

        Grid.SetRow(topBar, 0);
        shell.Children.Add(topBar);

        ListBox resultsList = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(resultsListField.Id),
            ItemsSource = SplitLines(resultsListField.Value),
            MinHeight = 360
        };
        ApplyShellListBoxTheme(resultsList);
        resultsList.ItemTemplate = new FuncDataTemplate<string>((line, _) =>
            CreateOptionText(line ?? string.Empty, TextWrapping.Wrap));
        ApplyAccessibility(resultsList, resultsListField.AccessibleName, resultsListField.ToolTip, resultsListField.HelpText);
        Grid.SetColumn(resultsList, 0);
        Grid.SetRow(resultsList, 1);
        shell.Children.Add(resultsList);

        Grid rightPane = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            RowSpacing = 6
        };
        Grid.SetColumn(rightPane, 1);
        Grid.SetRow(rightPane, 1);

        AddCheckboxRow(rightPane, 0, BuildLegacyInlineCheckBox(ruleOf6Field, "using Rule of 6"));
        AddCheckboxRow(rightPane, 1, BuildLegacyInlineCheckBox(cinematicGameplayField, "Hit on 4, 5, or 6"));
        AddCheckboxRow(rightPane, 2, BuildLegacyInlineCheckBox(rushJobField, "Rushed Job (Glitch on 1 or 2)"));
        AddCheckboxRow(rightPane, 3, BuildLegacyInlineCheckBox(bubbleDieField, "Bubble Die (Fix Even Dicepool Glitch Chances)"));
        AddCheckboxRow(rightPane, 4, BuildLegacyInlineCheckBox(variableGlitchField, "Glitch on More 1's than Hits, Not Half Dicepool"));
        AddLabeledValueRow(rightPane, 5, "Threshold:", BuildLegacyInlineNumericUpDown(thresholdField, width: 64));
        AddLabeledValueRow(rightPane, 6, "Gremlins:", BuildLegacyInlineNumericUpDown(gremlinsField, width: 64));

        Grid resultsPane = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 6
        };
        Grid.SetRow(resultsPane, 7);
        Grid.SetColumnSpan(resultsPane, 2);

        TextBlock resultsLabel = new()
        {
            Text = "Results:",
            FontWeight = FontWeight.SemiBold
        };
        resultsPane.Children.Add(resultsLabel);

        Border resultsSummaryBorder = new()
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(6, 4),
            Child = new TextBlock
            {
                Name = DesktopDialogAccessibility.BuildFieldInputName(resultsSummaryField.Id),
                Text = resultsSummaryField.Value,
                TextWrapping = TextWrapping.Wrap
            }
        };
        ApplyAccessibility(resultsSummaryBorder, resultsSummaryField.AccessibleName, resultsSummaryField.ToolTip, resultsSummaryField.HelpText);
        Grid.SetRow(resultsSummaryBorder, 1);
        resultsPane.Children.Add(resultsSummaryBorder);

        rightPane.Children.Add(resultsPane);
        shell.Children.Add(rightPane);
        return shell;
    }

    private Control CreateLegacySwitchRulesetPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField rulesetField = FindRequiredField(fields, "preferredRulesetId");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Startup Ruleset",
            CreateStandaloneFieldRow(rulesetField)));

        return shell;
    }

    private static Control CreateLegacyFieldGroup(string title, params Control[] children)
    {
        StackPanel body = new()
        {
            Spacing = 8
        };
        body.Children.Add(new TextBlock
        {
            Text = title,
            Classes = { "shell-section-title" }
        });
        foreach (Control child in children)
        {
            body.Children.Add(child);
        }

        return new Border
        {
            Classes = { "shell-panel", "subtle" },
            Padding = new Thickness(12),
            Child = body
        };
    }

    private static Control CreateLegacyGroupLead(string text)
    {
        return new TextBlock
        {
            Text = text,
            Classes = { "shell-caption" }
        };
    }

    private static Border CreateLegacySummaryCard(string title, string summary, Control content)
    {
        string normalizedTitle = title.Replace(" ", string.Empty, StringComparison.Ordinal);
        return new Border
        {
            Name = $"Legacy{normalizedTitle}SummaryCard",
            Classes = { "shell-panel", "accent" },
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Name = $"Legacy{normalizedTitle}SummaryTitle",
                        Text = title,
                        Classes = { "shell-section-title" }
                    },
                    new TextBlock
                    {
                        Name = $"Legacy{normalizedTitle}SummaryText",
                        Text = summary,
                        Classes = { "shell-caption" }
                    },
                    content
                }
            }
        };
    }

    private static Control CreateSummaryMetricStrip(params Control[] metrics)
    {
        WrapPanel panel = new()
        {
            Orientation = Orientation.Horizontal
        };

        foreach (Control metric in metrics)
        {
            panel.Children.Add(metric);
        }

        return panel;
    }

    private static Control CreateSummaryMetric(string label, string value)
    {
        return new Border
        {
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            BorderBrush = ResolveThemeBrush("ChummerShellBorderStrongBrush", "#93A0B2"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 0, 8, 8),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        Classes = { "shell-caption" }
                    },
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(value) ? "(none)" : value,
                        Classes = { "shell-metric-value" }
                    }
                }
            }
        };
    }

    private Control CreateLegacyMasterIndexPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField dataFileField = FindRequiredField(fields, "masterIndexFileSelection");
        DesktopDialogField entriesField = FindRequiredField(fields, "masterIndexActiveResultKey");
        DesktopDialogField searchField = FindRequiredField(fields, "masterIndexSearch");
        DesktopDialogField sourcebookField = FindRequiredField(fields, "masterIndexCurrentSourcebook");
        DesktopDialogField linkedSourceField = FindRequiredField(fields, "masterIndexSelectedSource");
        DesktopDialogField dataRootField = FindRequiredField(fields, "masterIndexDataRoot");
        DesktopDialogField notesField = FindRequiredField(fields, "masterIndexSnippetPreview");
        DesktopDialogField settingsField = FindRequiredField(fields, "masterIndexSettingsSummary");
        bool sourceAvailable = !string.IsNullOrWhiteSpace(sourcebookField.Value);
        bool linkedSourceAvailable = !string.IsNullOrWhiteSpace(linkedSourceField.Value);
        bool notesAvailable = !string.IsNullOrWhiteSpace(notesField.Value);

        Grid shell = new()
        {
            ColumnDefinitions = new ColumnDefinitions("1*,1*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 10
        };

        Grid left = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 8
        };
        Grid.SetColumn(left, 0);
        Grid.SetRow(left, 0);

        Grid fileRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8
        };
        fileRow.Children.Add(new TextBlock
        {
            Text = "Data File:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        ComboBox fileCombo = BuildSelectComboBox(dataFileField, minWidth: 220);
        fileCombo.Name = DesktopDialogAccessibility.BuildFieldInputName(dataFileField.Id);
        ApplyAccessibility(fileCombo, dataFileField.AccessibleName, dataFileField.ToolTip, dataFileField.HelpText);
        Grid.SetColumn(fileCombo, 1);
        fileRow.Children.Add(fileCombo);
        left.Children.Add(fileRow);

        ListBox entriesList = BuildSelectListBox(entriesField);
        entriesList.Name = DesktopDialogAccessibility.BuildFieldInputName(entriesField.Id);
        ApplyAccessibility(entriesList, entriesField.AccessibleName, entriesField.ToolTip, entriesField.HelpText);
        Grid.SetRow(entriesList, 1);
        left.Children.Add(entriesList);

        Grid right = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 8
        };
        Grid.SetColumn(right, 1);
        Grid.SetRow(right, 0);

        Grid searchRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8
        };
        searchRow.Children.Add(new TextBlock
        {
            Text = "Search:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        TextBox searchBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(searchField.Id),
            Text = searchField.Value,
            Focusable = true
        };
        ApplyTextBoxAccessibility(searchBox, searchField.AccessibleName, searchField.ToolTip, searchField.HelpText);
        if (string.IsNullOrWhiteSpace(_preferredFocusControlName))
        {
            _preferredFocusControlName = searchBox.Name;
        }
        if (string.Equals(_preferredFocusControlName, searchBox.Name, StringComparison.Ordinal))
        {
            searchBox.AttachedToVisualTree += (_, _) => RestorePreferredTextBoxFocus(searchBox);
        }
        searchBox.TextChanged += (_, _) =>
        {
            string nextValue = searchBox.Text ?? string.Empty;
            if (!string.Equals(nextValue, searchField.Value, StringComparison.Ordinal))
            {
                QueueDialogFieldUpdate(searchField.Id, nextValue);
            }
        };
        Grid.SetColumn(searchBox, 1);
        searchRow.Children.Add(searchBox);
        right.Children.Add(searchRow);

        Grid sourceRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };
        sourceRow.Children.Add(new TextBlock
        {
            Text = "Source:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            IsVisible = sourceAvailable
        });
        TextBlock sourceValueText = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(sourcebookField.Id),
            Text = sourcebookField.Value,
            Cursor = linkedSourceAvailable ? new Cursor(StandardCursorType.Hand) : default,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = sourceAvailable
        };
        ApplyAccessibility(sourceValueText, sourcebookField.AccessibleName, sourcebookField.ToolTip, sourcebookField.HelpText);
        if (linkedSourceAvailable)
        {
            sourceValueText.TextDecorations = TextDecorations.Underline;
            sourceValueText.Foreground = ResolveThemeBrush("ChummerShellInfoBrush", "#173A6C");
            sourceValueText.PointerPressed += async (_, _) =>
            {
                await ExecuteSafeAsync(
                    () => _adapter!.ExecuteDialogActionAsync("open_source", CancellationToken.None),
                    "execute action 'open_source'");
            };
        }

        Grid.SetColumn(sourceValueText, 1);
        sourceRow.Children.Add(sourceValueText);
        if (linkedSourceAvailable)
        {
            TextBlock sourceReminderText = new()
            {
                Text = "<- Click to Open Linked PDF",
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                IsVisible = false
            };
            Grid.SetColumn(sourceReminderText, 2);
            sourceRow.Children.Add(sourceReminderText);
        }
        else
        {
            Grid.SetColumnSpan(sourceValueText, 2);
        }
        Grid.SetRow(sourceRow, 1);
        right.Children.Add(sourceRow);

        Grid dataRootRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            IsVisible = !string.IsNullOrWhiteSpace(dataRootField.Value)
        };
        dataRootRow.Children.Add(new TextBlock
        {
            Text = "Data Root:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        TextBlock dataRootValueText = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(dataRootField.Id),
            Text = dataRootField.Value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ApplyAccessibility(dataRootValueText, dataRootField.AccessibleName, dataRootField.ToolTip, dataRootField.HelpText);
        Grid.SetColumn(dataRootValueText, 1);
        dataRootRow.Children.Add(dataRootValueText);
        Grid.SetRow(dataRootRow, 2);
        right.Children.Add(dataRootRow);

        TextBox notesBox = new()
        {
            Text = notesField.Value,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 280,
            IsVisible = notesAvailable
        };
        ApplyTextBoxAccessibility(notesBox, notesField.AccessibleName, notesField.ToolTip, notesField.HelpText);
        Grid.SetRow(notesBox, 3);
        right.Children.Add(notesBox);

        Grid settingsRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };
        settingsRow.Children.Add(new TextBlock
        {
            Text = "Setting:",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        ComboBox settingsCombo = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(settingsField.Id),
            ItemsSource = new[]
            {
                new DesktopDialogFieldOption(settingsField.Value, settingsField.Value)
            },
            SelectedIndex = 0,
            IsEnabled = false,
            MinWidth = 260
        };
        ApplyShellComboBoxTheme(settingsCombo);
        ApplyAccessibility(settingsCombo, settingsField.AccessibleName, settingsField.ToolTip, settingsField.HelpText);
        settingsCombo.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            CreateOptionText(option?.Label ?? string.Empty, TextWrapping.Wrap));
        Grid.SetColumn(settingsCombo, 1);
        settingsRow.Children.Add(settingsCombo);
        Button modifySettingsButton = new()
        {
            Name = "masterIndexSettingsModifyButton",
            Content = "Open settings",
            MinWidth = 88
        };
        ApplyAccessibility(
            modifySettingsButton,
            "Modify master index setting",
            "Open Character Settings.",
            "Open the legacy Character Settings dialog.");
        modifySettingsButton.Click += async (_, _) =>
        {
            await ExecuteSafeAsync(
                () => _adapter!.ExecuteCommandAsync("character_settings", CancellationToken.None),
                "execute command 'character_settings'");
        };
        Grid.SetColumn(modifySettingsButton, 2);
        settingsRow.Children.Add(modifySettingsButton);
        Grid.SetColumnSpan(settingsRow, 2);
        Grid.SetRow(settingsRow, 1);
        shell.Children.Add(settingsRow);

        shell.Children.Add(left);
        shell.Children.Add(right);
        return shell;
    }

    private Control CreateLegacyCharacterRosterPane(IReadOnlyList<DesktopDialogField> fields)
    {
        DesktopDialogField snapshotField = FindRequiredField(fields, "rosterSnapshot");
        DesktopDialogField selectedRunnerField = FindRequiredField(fields, "rosterSelectedRunnerId");
        DesktopDialogField selectedWatchFileField = FindRequiredField(fields, "rosterSelectedWatchFile");
        DesktopDialogField summaryField = FindRequiredField(fields, "rosterSelectedRunner");
        DesktopDialogField mugshotField = FindRequiredField(fields, "rosterMugshot");
        DesktopDialogField statusField = FindRequiredField(fields, "rosterSelectedRunnerStatus");
        DesktopDialogField backgroundField = FindRequiredField(fields, "rosterSelectedRunnerBackground");
        DesktopDialogField notesField = FindRequiredField(fields, "rosterSelectedRunnerNotes");

        RosterDialogSnapshotDisplay snapshot = JsonSerializer.Deserialize<RosterDialogSnapshotDisplay>(snapshotField.Value)
            ?? new RosterDialogSnapshotDisplay(string.Empty, string.Empty, string.Empty, [], []);

        Grid shell = new()
        {
            ColumnDefinitions = new ColumnDefinitions("40*,60*"),
            ColumnSpacing = 12
        };

        StackPanel left = new()
        {
            Spacing = 8
        };
        Grid.SetColumn(left, 0);

        TreeView rosterTree = BuildRosterTree(snapshot, selectedRunnerField.Value, selectedWatchFileField.Value);
        rosterTree.Name = DesktopDialogAccessibility.BuildFieldInputName(selectedRunnerField.Id);
        ApplyAccessibility(rosterTree, selectedRunnerField.AccessibleName, selectedRunnerField.ToolTip, selectedRunnerField.HelpText);
        rosterTree.MinHeight = 420;
        rosterTree.DoubleTapped += async (_, _) =>
        {
            if (rosterTree.SelectedItem is not RosterTreeItem node)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(node.RunnerId))
            {
                await ExecuteSafeAsync(
                    () => _adapter!.ExecuteDialogActionAsync("open_runner", CancellationToken.None),
                    "execute action 'open_runner'");
            }
            else if (!string.IsNullOrWhiteSpace(node.WatchFile))
            {
                await ExecuteSafeAsync(
                    () => _adapter!.ExecuteDialogActionAsync("open_watch_file", CancellationToken.None),
                    "execute action 'open_watch_file'");
            }
        };
        left.Children.Add(rosterTree);

        Grid.SetColumn(left, 0);
        shell.Children.Add(left);

        Grid right = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 8
        };
        Grid.SetColumn(right, 1);

        Grid summaryRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,300"),
            ColumnSpacing = 12
        };
        summaryRow.Children.Add(CreateLegacyRosterSummaryPanel(summaryField.Value));
        Control mugshotPanel = CreateRosterMugshotPanel(mugshotField.Value);
        Grid.SetColumn(mugshotPanel, 1);
        summaryRow.Children.Add(mugshotPanel);
        right.Children.Add(summaryRow);

        TextBlock statusText = new()
        {
            Text = statusField.Value,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        Grid.SetRow(statusText, 1);
        right.Children.Add(statusText);

        string description = ReadRosterValue(backgroundField.Value, "Description:", string.Empty);
        string concept = ReadRosterValue(backgroundField.Value, "Concept:", string.Empty);
        string background = ReadRosterValue(backgroundField.Value, "Background:", string.Empty);
        string characterNotes = ReadRosterValue(notesField.Value, "Character Notes:", string.Empty);
        string gameNotes = BuildGameNotes(notesField.Value);

        TabItem[] detailTabs =
        [
            new() { Header = "Description", Content = CreateLegacyReadOnlyTextBox(description) },
            new() { Header = "Concept", Content = CreateLegacyReadOnlyTextBox(concept) },
            new() { Header = "Background", Content = CreateLegacyReadOnlyTextBox(background) },
            new() { Header = "Character Notes", Content = CreateLegacyReadOnlyTextBox(characterNotes) },
            new() { Header = "Game Notes", Content = CreateLegacyReadOnlyTextBox(gameNotes) }
        ];
        TabControl detailTabsControl = new()
        {
            Name = "rosterDetailTabs",
            ItemsSource = detailTabs
        };
        ApplyAccessibility(
            detailTabsControl,
            "Character roster detail tabs",
            "Review Description, Concept, Background, Character Notes, and Game Notes.",
            "Switch between the legacy Character Roster detail tabs.");
        Grid.SetRow(detailTabsControl, 2);
        right.Children.Add(detailTabsControl);

        shell.Children.Add(right);
        return shell;
    }

    private Control CreateStandaloneFieldRow(DesktopDialogField field)
    {
        return CreateFieldPane(field);
    }

    private Control CreateSplitFieldRow(DesktopDialogField left, DesktopDialogField right)
    {
        Grid row = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8
        };
        Control leftPane = CreateFieldPane(left);
        Control rightPane = CreateFieldPane(right);
        Grid.SetColumn(leftPane, 0);
        Grid.SetColumn(rightPane, 1);
        row.Children.Add(leftPane);
        row.Children.Add(rightPane);
        return row;
    }

    private Control CreateFieldPane(DesktopDialogField field)
    {
        if (string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
        {
            CheckBox checkBox = new()
            {
                Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
                Content = field.Label,
                IsChecked = ParseCheckbox(field.Value),
                IsEnabled = !field.IsReadOnly,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
            };
            ApplyAccessibility(checkBox, field.AccessibleName, field.ToolTip, field.HelpText);
            return checkBox.Also(checkBox =>
            {
                if (!field.IsReadOnly)
                {
                    checkBox.IsCheckedChanged += (_, _) =>
                    {
                        string nextValue = checkBox.IsChecked == true ? "true" : "false";
                        if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                        {
                            return;
                        }

                        QueueDialogFieldUpdate(field.Id, nextValue);
                    };
                }
            });
        }

        Grid row = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldContainerName(field.Id),
            ColumnDefinitions = new ColumnDefinitions("*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 0,
            RowSpacing = 6
        };

        TextBlock label = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
            Text = field.Label,
            VerticalAlignment = field.IsMultiline ? global::Avalonia.Layout.VerticalAlignment.Top : global::Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(label);

        Control fieldControl = CreateFieldControl(field);
        fieldControl.Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id);
        ApplyAccessibility(fieldControl, field.AccessibleName, field.ToolTip, field.HelpText);
        Grid.SetColumn(fieldControl, 0);
        Grid.SetRow(fieldControl, 1);

        row.Children.Add(fieldControl);
        return row;
    }

    private static bool ShouldRenderField(DesktopDialogField field)
    {
        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tabs, StringComparison.Ordinal))
        {
            // Chummer5a parity posture: do not render synthetic dialog tab strips.
            return false;
        }

        return true;
    }

    private Control CreateFieldControl(DesktopDialogField field)
    {
        if (string.Equals(field.InputType, "select", StringComparison.Ordinal))
        {
            DesktopDialogFieldOption[] options = (field.Options ?? [])
                .DistinctBy(option => option.Value, StringComparer.Ordinal)
                .ToArray();
            ComboBox comboBox = new()
            {
                ItemsSource = options,
                SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, field.Value, StringComparison.Ordinal)),
                IsEnabled = !field.IsReadOnly,
                MinWidth = 180
            };
            int comboBoxBindVersion = _dialogBindVersion;
            ApplyShellComboBoxTheme(comboBox);
            PrepareComboBoxForDialogStatePreservation(comboBox, comboBoxBindVersion);
            comboBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
                CreateComboBoxOptionText(option?.Label ?? string.Empty));
            if (!field.IsReadOnly)
            {
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (_suppressDialogUpdates || comboBoxBindVersion != _dialogBindVersion)
                    {
                        return;
                    }

                    if (comboBox.SelectedItem is not DesktopDialogFieldOption selectedOption)
                    {
                        return;
                    }

                    if (string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    QueueDialogFieldUpdate(field.Id, selectedOption.Value, comboBox);
                };
            }

            ApplyAccessibility(comboBox, field.AccessibleName, field.ToolTip, field.HelpText);
            return comboBox;
        }

        if (field.IsReadOnly && !string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Default, StringComparison.Ordinal))
        {
            Control visualControl;
            if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal))
            {
                visualControl = CreateStructuredTextPanel(field.Value, useMonospace: true, minHeight: 160);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal))
            {
                visualControl = CreateStructuredTextPanel(field.Value, useMonospace: false, minHeight: 160);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tabs, StringComparison.Ordinal))
            {
                visualControl = CreateTabsPanel(field.Value);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Image, StringComparison.Ordinal))
            {
                visualControl = CreateImagePlaceholderPanel(field.Value);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Book, StringComparison.Ordinal))
            {
                visualControl = CreateBookPreviewPanel(field.Value);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal))
            {
                visualControl = CreateGridPanel(field.Value);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Narrative, StringComparison.Ordinal))
            {
                visualControl = CreateNarrativePanel(field.Value);
            }
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Snippet, StringComparison.Ordinal))
            {
                visualControl = CreateSnippetPanel(field.Value);
            }
            else
            {
                visualControl = CreateSnippetPanel(field.Value);
            }

            ApplyAccessibility(visualControl, field.AccessibleName, field.ToolTip, field.HelpText);
            return visualControl;
        }

        if (field.IsReadOnly && field.IsMultiline && !string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Default, StringComparison.Ordinal))
        {
            TextBlock textBlock = new()
            {
                Text = field.Value,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap
            };
            if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal))
            {
                textBlock.FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace");
            }

            Border panel = new()
            {
                BorderThickness = new Thickness(1),
                BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                Background = DesktopShellTheme.ResolveSurfaceBrush(),
                Padding = new Thickness(6, 4),
                MinHeight = string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal)
                    || string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal)
                    ? 160
                    : 124,
                Child = textBlock
            };
            ApplyAccessibility(panel, field.AccessibleName, field.ToolTip, field.HelpText);
            return panel;
        }

        TextBox textBox = new()
        {
            Text = field.Value,
            IsReadOnly = field.IsReadOnly,
            AcceptsReturn = field.IsMultiline,
            TextWrapping = field.IsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = field.IsMultiline
                ? string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Detail, StringComparison.Ordinal) ? 136 : 104
                : 24
        };
        if (!field.IsReadOnly)
        {
            textBox.TextChanged += (_, _) =>
            {
                string nextValue = textBox.Text ?? string.Empty;
                if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    return;
                }

                QueueDialogFieldUpdate(field.Id, nextValue);
            };
        }

        ApplyTextBoxAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
        return textBox;
    }

    private static Control CreateTabsPanel(string value)
    {
        WrapPanel tabs = new()
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal
        };

        foreach (string line in value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            tabs.Children.Add(new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                Background = DesktopShellTheme.ResolveSurfaceBrush(),
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(8, 3),
                Child = new TextBlock { Text = line }
            });
        }

        return tabs;
    }

    private static Control CreateImagePlaceholderPanel(string value)
    {
        string[] lines = value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? portraitSource = lines
            .FirstOrDefault(line => line.StartsWith("Portrait Source | ", StringComparison.Ordinal))
            ?.Substring("Portrait Source | ".Length)
            .Trim();
        string? previewLabel = lines.Length > 0 ? lines[0] : null;
        StackPanel panel = new()
        {
            Spacing = 4
        };
        Control previewControl;
        if (!string.IsNullOrWhiteSpace(portraitSource) && File.Exists(portraitSource))
        {
            try
            {
                previewControl = new Image
                {
                    Source = new Bitmap(portraitSource),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    MaxHeight = 220
                };
            }
            catch
            {
                previewControl = CreateMugshotFallback(previewLabel);
            }
        }
        else
        {
            previewControl = CreateMugshotFallback(previewLabel);
        }

        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            MinHeight = 136,
            Child = previewControl
        });

        if (lines.Length > 1)
        {
            panel.Children.Add(new TextBlock
            {
                Text = string.Join(Environment.NewLine, lines.Skip(1)),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return panel;
    }

    private static Control CreateRosterMugshotPanel(string value)
    {
        string portraitSource = value.Trim();
        Control previewControl;
        if (!string.IsNullOrWhiteSpace(portraitSource) && File.Exists(portraitSource))
        {
            try
            {
                previewControl = new Image
                {
                    Source = new Bitmap(portraitSource),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    MaxHeight = 220
                };
            }
            catch
            {
                previewControl = new Panel();
            }
        }
        else
        {
            previewControl = new Panel();
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            MinHeight = 136,
            Child = previewControl
        };
    }

    private static Control CreateMugshotFallback(string? previewLabel)
    {
        if (!string.IsNullOrWhiteSpace(previewLabel))
        {
            return new TextBlock
            {
                Text = previewLabel,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
        }

        return new Panel();
    }

    private static Control CreateGridPanel(string value, bool hideEmptyRows = false)
    {
        StackPanel rows = new()
        {
            Spacing = 3
        };

        foreach ((string key, string data) in ParseGridRows(value))
        {
            if (hideEmptyRows && string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            Grid row = new()
            {
                ColumnDefinitions = new ColumnDefinitions("156,*"),
                ColumnSpacing = 8
            };
            TextBlock keyText = new()
            {
                Text = key,
                FontWeight = FontWeight.SemiBold
            };
            TextBlock valueText = new()
            {
                Text = data,
                TextWrapping = TextWrapping.Wrap
            };
            ToolTip.SetTip(valueText, key);
            Grid.SetColumn(keyText, 0);
            Grid.SetColumn(valueText, 1);
            row.Children.Add(keyText);
            row.Children.Add(valueText);
            rows.Children.Add(row);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(6, 4),
            Child = rows
        };
    }

    private static Control CreateLegacyRosterSummaryPanel(string value)
    {
        Dictionary<string, string> rows = ParseGridRows(value)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        Grid grid = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 6,
            RowSpacing = 4
        };

        string fileName = ReadLegacyRosterSummaryValue(rows, "File Path");
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(fileName);
        }

        AddLegacyRosterSummaryRow(grid, 0, "Character Name:", ReadLegacyRosterSummaryValue(rows, "Character Name"));
        AddLegacyRosterSummaryRow(grid, 1, "Alias:", ReadLegacyRosterSummaryValue(rows, "Alias"));
        AddLegacyRosterSummaryRow(grid, 2, "Player:", ReadLegacyRosterSummaryValue(rows, "Player Name"));
        AddLegacyRosterSummaryRow(grid, 3, "Metatype:", ReadLegacyRosterSummaryValue(rows, "Metatype"));
        AddLegacyRosterSummaryRow(grid, 4, "Career Karma:", ReadLegacyRosterSummaryValue(rows, "Career Karma"));
        AddLegacyRosterSummaryRow(grid, 5, "Essence:", ReadLegacyRosterSummaryValue(rows, "Essence"));
        AddLegacyRosterSummaryRow(grid, 6, "File Name:", fileName);
        AddLegacyRosterSummaryRow(grid, 7, "Settings File:", ReadLegacyRosterSummaryValue(rows, "Settings"));

        return grid;
    }

    private static Control CreateSnippetPanel(string value)
    {
        return new Border
        {
            Name = "SnippetReadOnlyTextPanel",
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(6, 4),
            Child = new TextBlock
            {
                Text = value,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control CreateNarrativePanel(string value, double minHeight = 144, double maxHeight = 320)
    {
        string[] paragraphs = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] bodyParagraphs = paragraphs.Length == 0 ? [value.Trim()] : paragraphs;

        StackPanel narrative = new()
        {
            Spacing = 12
        };

        foreach (string paragraph in bodyParagraphs.Where(static paragraph => !string.IsNullOrWhiteSpace(paragraph)))
        {
            narrative.Children.Add(new TextBlock
            {
                Name = "OriginNarrativeParagraphText",
                Text = paragraph,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                FontSize = 14
            });
        }

        return new Border
        {
            Name = "OriginNarrativePreviewPanel",
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(16, 14),
            MinHeight = minHeight,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = maxHeight,
                Content = narrative
            }
        };
    }

    private static Control CreateBookPreviewPanel(string value)
    {
        string[] paragraphs = value
            .Split([Environment.NewLine + Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string title = paragraphs.FirstOrDefault() ?? "Origin Dossier";
        string[] bodyParagraphs = paragraphs.Skip(1).DefaultIfEmpty(value).ToArray();

        StackPanel book = new()
        {
            Spacing = 10
        };
        book.Children.Add(new TextBlock
        {
            Name = "OriginBookPreviewTitleText",
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = DesktopShellTheme.ResolveForegroundBrush(),
            TextWrapping = TextWrapping.Wrap
        });

        foreach (string paragraph in bodyParagraphs)
        {
            book.Children.Add(new TextBlock
            {
                Name = "OriginBookPreviewBodyText",
                Text = paragraph,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });
        }

        return new Border
        {
            Name = "OriginBookPreviewPanel",
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(18, 14),
            MinHeight = 300,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 420,
                Content = book
            }
        };
    }

    private static Control CreateStructuredTextPanel(string value, bool useMonospace, double minHeight)
    {
        return new Border
        {
            Name = "StructuredReadOnlyTextPanel",
            BorderThickness = new Thickness(1),
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
            Padding = new Thickness(6, 4),
            MinHeight = minHeight,
            Child = new TextBlock
            {
                Text = value,
                Foreground = DesktopShellTheme.ResolveForegroundBrush(),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = useMonospace ? new FontFamily("Consolas, Menlo, Monaco, monospace") : FontFamily.Default
            }
        };
    }

    private static IEnumerable<(string Key, string Value)> ParseGridRows(string value)
    {
        foreach (string line in value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                yield return (parts[0], parts[1]);
            }
            else
            {
                yield return (line, string.Empty);
            }
        }
    }

    private static void AddLegacyRosterSummaryRow(Grid grid, int rowIndex, string label, string value)
    {
        TextBlock labelText = new()
        {
            Text = label,
            IsVisible = false,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetRow(labelText, rowIndex);
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        TextBlock valueText = new()
        {
            Text = string.IsNullOrWhiteSpace(value) ? "[None]" : value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetRow(valueText, rowIndex);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
    }

    private static string ReadLegacyRosterSummaryValue(
        IReadOnlyDictionary<string, string> rows,
        string key)
    {
        return rows.TryGetValue(key, out string? value)
            ? value
            : string.Empty;
    }

    private Button CreateLegacyActionButton(string actionId, string label, bool isPrimary = false)
    {
        DesktopDialogAction action = new(actionId, label, isPrimary);

        Button button = new()
        {
            Name = DesktopDialogAccessibility.BuildActionName(actionId),
            Content = label,
            Tag = actionId,
            MinWidth = 88,
            Classes = { "shell-action", isPrimary ? "primary" : "quiet" }
        };
        if (isPrimary)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        ApplyAccessibility(button, action.AccessibleName, action.ToolTip, action.HelpText);
        button.Click += async (_, _) =>
        {
            if (_adapter is null)
                return;

            await ExecuteSafeAsync(
                () => _adapter.ExecuteDialogActionAsync(actionId, CancellationToken.None),
                $"execute action '{actionId}'");
        };
        return button;
    }

    private CheckBox BuildLegacyInlineCheckBox(DesktopDialogField field, string? textOverride = null)
    {
        CheckBox checkBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
            Content = string.Empty,
            IsChecked = ParseCheckbox(field.Value),
            IsEnabled = !field.IsReadOnly
        };
        ApplyAccessibility(checkBox, field.AccessibleName, field.ToolTip, field.HelpText);
        if (!field.IsReadOnly)
        {
            checkBox.IsCheckedChanged += (_, _) =>
            {
                string nextValue = checkBox.IsChecked == true ? "true" : "false";
                if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    return;
                }

                QueueDialogFieldUpdate(field.Id, nextValue);
            };
        }

        return checkBox;
    }

    private TextBox BuildLegacyInlineTextBox(DesktopDialogField field, double width)
    {
        TextBox textBox = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
            Text = field.Value,
            IsReadOnly = field.IsReadOnly,
            Width = width
        };
        ApplyTextBoxAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
        if (!field.IsReadOnly)
        {
            textBox.TextChanged += (_, _) =>
            {
                string nextValue = textBox.Text ?? string.Empty;
                if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    return;
                }

                QueueDialogFieldUpdate(field.Id, nextValue);
            };
        }

        return textBox;
    }

    private NumericUpDown BuildLegacyInlineNumericUpDown(DesktopDialogField field, double width)
    {
        decimal value = decimal.TryParse(field.Value, out decimal parsedValue) ? parsedValue : 0m;
        NumericUpDown numericUpDown = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
            Value = value,
            IsReadOnly = field.IsReadOnly,
            Width = width,
            Minimum = 0,
            Increment = 1
        };
        ApplyAccessibility(numericUpDown, field.AccessibleName, field.ToolTip, field.HelpText);
        DesktopShellTheme.ApplyShellNumericUpDownTheme(numericUpDown);
        if (!field.IsReadOnly)
        {
            numericUpDown.ValueChanged += (_, _) =>
            {
                string nextValue = Convert.ToInt32(numericUpDown.Value ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    return;
                }

                QueueDialogFieldUpdate(field.Id, nextValue);
            };
        }

        return numericUpDown;
    }

    private static string[] SplitLines(string value)
    {
        string[] lines = value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? ["No rolls yet."] : lines;
    }

    private static void AddCheckboxRow(Grid grid, int rowIndex, CheckBox checkBox)
    {
        Grid.SetRow(checkBox, rowIndex);
        Grid.SetColumn(checkBox, 0);
        Grid.SetColumnSpan(checkBox, 2);
        grid.Children.Add(checkBox);
    }

    private static void AddLabeledValueRow(Grid grid, int rowIndex, string label, Control valueControl)
    {
        TextBlock labelText = new()
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            Foreground = DesktopShellTheme.ResolveForegroundBrush(),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetRow(labelText, rowIndex);
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);
        ToolTip.SetTip(valueControl, label);

        if (valueControl is TextBlock valueText)
        {
            valueText.Foreground = DesktopShellTheme.ResolveForegroundBrush();
            valueText.FontWeight = FontWeight.SemiBold;
            valueText.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        }

        Grid.SetRow(valueControl, rowIndex);
        Grid.SetColumn(valueControl, 1);
        grid.Children.Add(valueControl);
    }

    private static void ApplyAccessibility(Control control, string accessibleName, string toolTip, string helpText)
    {
        AutomationProperties.SetName(control, accessibleName);
        AutomationProperties.SetHelpText(control, helpText);
        ToolTip.SetTip(control, toolTip);
    }

    private static void ApplyTextBoxAccessibility(TextBox textBox, string accessibleName, string toolTip, string helpText)
    {
        DesktopShellTheme.ApplyShellTextInputTheme(textBox);
        ApplyAccessibility(textBox, accessibleName, toolTip, helpText);
        ToolTip.SetTip(textBox, null);
    }

    private void BuildActions(IReadOnlyList<DesktopDialogAction> actions)
    {
        _dialogActionsPanel.Children.Clear();
        IEnumerable<DesktopDialogAction> visibleActions = actions;
        if (string.Equals(BoundDialogId, "dialog.master_index", StringComparison.Ordinal)
            || string.Equals(BoundDialogId, "dialog.character_roster", StringComparison.Ordinal)
            || string.Equals(BoundDialogId, "dialog.dice_roller", StringComparison.Ordinal))
        {
            visibleActions = [];
        }
        else if (string.Equals(BoundDialogId, "dialog.global_settings", StringComparison.Ordinal))
        {
            visibleActions = actions.Where(action => !string.Equals(action.Id, "apply", StringComparison.Ordinal));
        }

        DesktopDialogAction[] visibleActionArray = visibleActions.ToArray();
        _dialogActionsBorder.IsVisible = visibleActionArray.Length > 0;

        foreach (DesktopDialogAction action in visibleActionArray)
        {
            bool isEnabled = true;
            if (string.Equals(BoundDialogId, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                && string.Equals(action.Id, "complete_new_character_workflow", StringComparison.Ordinal))
            {
                isEnabled = ParseCheckbox(
                    _boundDialogFields.FirstOrDefault(field => string.Equals(field.Id, "newCharacterPriorityWorkflowCanCommit", StringComparison.Ordinal))?.Value
                    ?? "true");
            }

            Button button = new()
            {
                Name = DesktopDialogAccessibility.BuildActionName(action.Id),
                Content = action.Label,
                Tag = action.Id,
                MinWidth = 82,
                IsEnabled = isEnabled,
                Classes = { "shell-action", action.IsPrimary ? "primary" : "quiet" }
            };
            ApplyAccessibility(button, action.AccessibleName, action.ToolTip, action.HelpText);
            if (action.IsPrimary)
            {
                button.FontWeight = FontWeight.SemiBold;
            }

            button.Click += async (_, _) =>
            {
                if (_adapter is null)
                    return;

                await ExecuteSafeAsync(
                    () => _adapter.ExecuteDialogActionAsync(action.Id, CancellationToken.None),
                    $"execute action '{action.Id}'");
            };
            _dialogActionsPanel.Children.Add(button);
        }
    }

    private static DesktopDialogField FindRequiredField(IReadOnlyList<DesktopDialogField> fields, string fieldId)
    {
        return fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Dialog field '{fieldId}' was not available for '{fieldId}'.");
    }

    private static DesktopDialogField? FindOptionalField(IReadOnlyList<DesktopDialogField> fields, string fieldId)
    {
        return fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal));
    }

    private ComboBox BuildSelectComboBox(DesktopDialogField field, double minWidth)
    {
        DesktopDialogFieldOption[] options = (field.Options ?? [])
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();
        ComboBox comboBox = new()
        {
            ItemsSource = options,
            SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, field.Value, StringComparison.Ordinal)),
            IsEnabled = !field.IsReadOnly,
            MinWidth = minWidth
        };
        int comboBoxBindVersion = _dialogBindVersion;
        ApplyShellComboBoxTheme(comboBox);
        PrepareComboBoxForDialogStatePreservation(comboBox, comboBoxBindVersion);
        comboBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            CreateComboBoxOptionText(option?.Label ?? string.Empty, TextWrapping.Wrap));
        if (!field.IsReadOnly)
        {
            comboBox.SelectionChanged += (_, _) =>
            {
                if (_suppressDialogUpdates || comboBoxBindVersion != _dialogBindVersion)
                {
                    return;
                }

                if (comboBox.SelectedItem is DesktopDialogFieldOption selectedOption
                    && !string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                {
                    QueueDialogFieldUpdate(field.Id, selectedOption.Value, comboBox);
                }
            };
        }

        return comboBox;
    }

    private ComboBox BuildReadOnlyComboBox(string value)
    {
        ComboBox comboBox = new()
        {
            ItemsSource = new[] { value },
            SelectedIndex = 0,
            IsEnabled = false
        };
        ApplyShellComboBoxTheme(comboBox);
        return comboBox;
    }

    private static void ApplyShellComboBoxTheme(ComboBox comboBox)
    {
        DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);
    }

    private void PrepareComboBoxForDialogStatePreservation(ComboBox comboBox, int comboBoxBindVersion)
    {
        void CaptureInteractionAnchor()
        {
            if (_suppressDialogUpdates
                || _suppressProgrammaticComboFocusAnchorCapture
                || comboBoxBindVersion != _dialogBindVersion)
            {
                return;
            }

            CaptureTransientDialogState();
            _preferredDialogScrollAnchor ??= _dialogScrollViewer.Offset;
            CapturePreferredDialogViewportAnchor();
            CapturePreferredDialogInteractionAnchor(comboBox);
            RestorePreferredScrollAnchorDuringOriginWizardComboInteraction();
        }

        comboBox.GotFocus += (_, _) => CaptureInteractionAnchor();
        comboBox.DropDownOpened += (_, _) => CaptureInteractionAnchor();
        comboBox.AddHandler(
            InputElement.PointerPressedEvent,
            (_, _) => CaptureInteractionAnchor(),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private static void ApplyShellListBoxTheme(ListBox listBox)
    {
        DesktopShellTheme.ApplyShellListBoxTheme(listBox);
    }

    private static TextBlock CreateComboBoxOptionText(string text, TextWrapping wrapping = TextWrapping.NoWrap)
        => CreateOptionText(text, wrapping);

    private static TextBlock CreateOptionText(string text, TextWrapping wrapping = TextWrapping.NoWrap)
        => DesktopShellTheme.CreateOptionText(text, wrapping);

    private static TextBlock CreateOptionMetaText(string text, TextWrapping wrapping = TextWrapping.Wrap)
        => DesktopShellTheme.CreateOptionMetaText(text, wrapping);

    private ListBox BuildSelectListBox(DesktopDialogField field)
    {
        DesktopDialogFieldOption[] options = (field.Options ?? [])
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();
        ListBox listBox = new()
        {
            ItemsSource = options,
            SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, field.Value, StringComparison.Ordinal)),
            MinHeight = 320
        };
        ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            CreateOptionText(option?.Label ?? string.Empty, TextWrapping.Wrap));
        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is DesktopDialogFieldOption selectedOption
                && !string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
            {
                QueueDialogFieldUpdate(field.Id, selectedOption.Value);
            }
        };
        return listBox;
    }

    private TreeView BuildRosterTree(
        RosterDialogSnapshotDisplay snapshot,
        string selectedRunnerId,
        string selectedWatchFile)
    {
        RosterTreeItem[] roots =
        [
            new RosterTreeItem(
                "Open Characters",
                null,
                null,
                snapshot.Workspaces
                    .Select(workspace => new RosterTreeItem(
                        $"{workspace.Alias} · {workspace.Name} [{workspace.RulesetId}]",
                        workspace.Id,
                        null,
                        []))
                    .ToArray()),
            new RosterTreeItem(
                "Watch Folder",
                null,
                null,
                snapshot.WatchedFiles
                    .Select(file => new RosterTreeItem(file, null, file, []))
                    .ToArray())
        ];

        TreeView treeView = new()
        {
            ItemsSource = roots
        };
        DesktopShellTheme.ApplyShellTreeViewTheme(treeView);
        treeView.ItemTemplate = new FuncTreeDataTemplate<RosterTreeItem>(
            (item, _) => new TextBlock
            {
                Text = item?.Label ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            },
            item => item?.Children ?? []);
        treeView.SelectedItem = FindSelectedRosterTreeNode(roots, selectedRunnerId, selectedWatchFile);
        treeView.SelectionChanged += (_, _) =>
        {
            if (treeView.SelectedItem is not RosterTreeItem selectedNode)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedNode.RunnerId)
                && !string.Equals(selectedNode.RunnerId, selectedRunnerId, StringComparison.Ordinal))
            {
                QueueDialogFieldUpdate("rosterSelectedRunnerId", selectedNode.RunnerId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedNode.WatchFile)
                && !string.Equals(selectedNode.WatchFile, selectedWatchFile, StringComparison.Ordinal))
            {
                QueueDialogFieldUpdate("rosterSelectedWatchFile", selectedNode.WatchFile);
            }
        };
        return treeView;
    }

    private static RosterTreeItem? FindSelectedRosterTreeNode(
        IEnumerable<RosterTreeItem> roots,
        string selectedRunnerId,
        string selectedWatchFile)
    {
        foreach (RosterTreeItem root in roots)
        {
            if (!string.IsNullOrWhiteSpace(selectedRunnerId)
                && string.Equals(root.RunnerId, selectedRunnerId, StringComparison.Ordinal))
            {
                return root;
            }

            if (!string.IsNullOrWhiteSpace(selectedWatchFile)
                && string.Equals(root.WatchFile, selectedWatchFile, StringComparison.Ordinal))
            {
                return root;
            }

            RosterTreeItem? nested = FindSelectedRosterTreeNode(root.Children, selectedRunnerId, selectedWatchFile);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Control CreateLegacyReadOnlyTextBox(string value)
    {
        TextBox textBox = new()
        {
            Text = value,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        DesktopShellTheme.ApplyShellTextInputTheme(textBox);
        ToolTip.SetTip(textBox, null);
        return textBox;
    }

    private static string ReadRosterValue(string rawValue, string prefix, string fallback)
    {
        foreach (string line in rawValue.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return fallback;
    }

    private static string BuildGameNotes(string rawNotes)
    {
        List<string> lines = [];
        foreach (string line in rawNotes.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("Game Notes:", StringComparison.Ordinal))
            {
                lines.Add(line["Game Notes:".Length..].Trim());
                continue;
            }

            if (line.StartsWith("Watch posture:", StringComparison.Ordinal))
            {
                lines.Add(line["Watch posture:".Length..].Trim());
            }
        }

        return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private void RefreshDialogVisuals()
    {
        _dialogTitleText.InvalidateMeasure();
        _dialogTitleText.InvalidateArrange();
        _dialogTitleText.InvalidateVisual();
        _dialogMessageText.InvalidateMeasure();
        _dialogMessageText.InvalidateArrange();
        _dialogMessageText.InvalidateVisual();
        _dialogFieldsPanel.InvalidateMeasure();
        _dialogFieldsPanel.InvalidateArrange();
        _dialogFieldsPanel.InvalidateVisual();
        _dialogActionsPanel.InvalidateMeasure();
        _dialogActionsPanel.InvalidateArrange();
        _dialogActionsPanel.InvalidateVisual();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
        _dialogFieldsPanel.UpdateLayout();
        _dialogActionsPanel.UpdateLayout();
        UpdateLayout();
    }

    private void ApplyDialogSizing(string? dialogId)
    {
        (double width, double height, double minWidth, double minHeight) size = dialogId switch
        {
            "dialog.new_character" => (820d, 250d, 680d, 220d),
            "dialog.new_character.priority_workflow" => (980d, 720d, 820d, 560d),
            "dialog.new_character.karma_workflow" => (820d, 250d, 680d, 220d),
            "dialog.master_index" => (980d, 640d, 760d, 440d),
            "dialog.character_roster" => (900d, 620d, 700d, 420d),
            "dialog.global_settings" => (920d, 600d, 700d, 420d),
            _ when !string.IsNullOrWhiteSpace(dialogId)
                && dialogId.StartsWith("dialog.ui.", StringComparison.Ordinal)
                && dialogId.EndsWith("_add", StringComparison.Ordinal) => (1040d, 670d, 880d, 560d),
            _ => (860d, 560d, 640d, 360d)
        };

        Width = size.width;
        Height = size.height;
        MinWidth = size.minWidth;
        MinHeight = size.minHeight;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            FocusPreferredControlDuringRestore();
        }, DispatcherPriority.Input);
    }

    private void CaptureTransientDialogState()
    {
        Expander? advancedStoryControls = _dialogFieldsPanel.GetVisualDescendants()
            .OfType<Expander>()
            .FirstOrDefault(expander => string.Equals(expander.Name, OriginWizardAdvancedStoryControlsExpanderName, StringComparison.Ordinal));
        if (advancedStoryControls is not null)
        {
            _originWizardAdvancedStoryControlsExpanded = advancedStoryControls.IsExpanded;
        }
    }

    private void CapturePreferredDialogViewportAnchor()
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll())
        {
            _preferredDialogViewportAnchor = null;
            return;
        }

        if (_preferredDialogViewportAnchor is not null)
        {
            return;
        }

        Expander? advancedStoryControls = _dialogFieldsPanel.GetVisualDescendants()
            .OfType<Expander>()
            .FirstOrDefault(expander => string.Equals(expander.Name, OriginWizardAdvancedStoryControlsExpanderName, StringComparison.Ordinal));
        if (advancedStoryControls is null || string.IsNullOrWhiteSpace(advancedStoryControls.Name))
        {
            _preferredDialogViewportAnchor = null;
            return;
        }

        Point? translated = advancedStoryControls.TranslatePoint(default, _dialogScrollViewer);
        if (translated is null)
        {
            _preferredDialogViewportAnchor = null;
            return;
        }

        _preferredDialogViewportAnchor = (advancedStoryControls.Name, translated.Value.Y);
    }

    private void CapturePreferredDialogInteractionAnchor(Control control)
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll()
            || string.IsNullOrWhiteSpace(control.Name))
        {
            _preferredDialogInteractionAnchor = null;
            return;
        }

        if (_preferredDialogInteractionAnchor is { } existingAnchor
            && string.Equals(existingAnchor.ControlName, control.Name, StringComparison.Ordinal))
        {
            return;
        }

        Point? translated = control.TranslatePoint(default, _dialogScrollViewer);
        if (translated is null)
        {
            _preferredDialogInteractionAnchor = null;
            return;
        }

        _preferredDialogInteractionAnchor = (control.Name, translated.Value.Y);
    }

    private void ClearPreferredDialogViewportAnchor()
    {
        _preferredDialogViewportAnchor = null;
        _preferredDialogViewportAnchorVersion++;
    }

    private void ClearPreferredDialogInteractionAnchor()
    {
        _preferredDialogInteractionAnchor = null;
        _preferredDialogInteractionAnchorVersion++;
    }

    private Vector? CapturePreferredScrollOffset(string nextDialogId)
    {
        if (!string.Equals(BoundDialogId, nextDialogId, StringComparison.Ordinal))
        {
            _preferredDialogScrollAnchor = null;
            _preferredDialogScrollAnchorVersion++;
            ClearPreferredDialogViewportAnchor();
            ClearPreferredDialogInteractionAnchor();
            return null;
        }

        Vector preservedOffset = _preferredDialogScrollAnchor ?? _dialogScrollViewer.Offset;
        _preferredDialogScrollAnchor = null;
        _preferredDialogScrollAnchorVersion++;
        return preservedOffset;
    }

    private (string ControlName, double OffsetY)? CapturePreferredDialogViewportAnchorSnapshot(string nextDialogId)
    {
        if (!string.Equals(BoundDialogId, nextDialogId, StringComparison.Ordinal))
        {
            ClearPreferredDialogViewportAnchor();
            return null;
        }

        (string ControlName, double OffsetY)? preservedAnchor = _preferredDialogViewportAnchor;
        if (preservedAnchor is null)
        {
            preservedAnchor = CaptureCurrentOriginWizardViewportAnchor();
        }

        ClearPreferredDialogViewportAnchor();
        return preservedAnchor;
    }

    private (string ControlName, double OffsetY)? CapturePreferredDialogInteractionAnchorSnapshot(string nextDialogId)
    {
        if (!string.Equals(BoundDialogId, nextDialogId, StringComparison.Ordinal))
        {
            ClearPreferredDialogInteractionAnchor();
            return null;
        }

        (string ControlName, double OffsetY)? preservedAnchor = _preferredDialogInteractionAnchor;
        ClearPreferredDialogInteractionAnchor();
        return preservedAnchor;
    }

    private void RestorePreferredScrollOffset(
        string? dialogId,
        Vector? preservedScrollOffset,
        (string ControlName, double OffsetY)? preservedViewportAnchor,
        (string ControlName, double OffsetY)? preservedInteractionAnchor)
    {
        Vector offset = preservedScrollOffset ?? default;
        bool hasPreferredAnchor = (preservedViewportAnchor is not null || preservedInteractionAnchor is not null)
            && string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal);
        if (!hasPreferredAnchor)
        {
            _dialogScrollViewer.Offset = offset;
        }

        if (preservedScrollOffset is null)
        {
            if ((preservedViewportAnchor is null && preservedInteractionAnchor is null)
                || !string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal))
            {
                return;
            }
        }
        else if (!hasPreferredAnchor)
        {
            Dispatcher.UIThread.Post(() => _dialogScrollViewer.Offset = offset, DispatcherPriority.Input);
            Dispatcher.UIThread.Post(() => _dialogScrollViewer.Offset = offset, DispatcherPriority.Loaded);
            Dispatcher.UIThread.Post(() => _dialogScrollViewer.Offset = offset, DispatcherPriority.Background);
            ScheduleDelayedPreferredDialogScrollAnchor(offset, _preferredDialogScrollAnchorVersion);
        }

        bool hasPreferredInteractionAnchor = preservedInteractionAnchor is not null
            && string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal);
        if (hasPreferredInteractionAnchor)
        {
            (string ControlName, double OffsetY) interactionAnchor = preservedInteractionAnchor!.Value;
            int interactionAnchorVersion = ++_preferredDialogInteractionAnchorVersion;
            ApplyPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion, DispatcherPriority.Input);
            ApplyPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion, DispatcherPriority.Loaded);
            ApplyPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion, DispatcherPriority.Background);
            ScheduleDelayedPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion);
        }

        if (hasPreferredInteractionAnchor
            || preservedViewportAnchor is null
            || !string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal))
        {
            return;
        }

        int anchorVersion = ++_preferredDialogViewportAnchorVersion;
        ApplyPreferredDialogViewportAnchor(preservedViewportAnchor.Value.ControlName, preservedViewportAnchor.Value.OffsetY, anchorVersion, DispatcherPriority.Input);
        ApplyPreferredDialogViewportAnchor(preservedViewportAnchor.Value.ControlName, preservedViewportAnchor.Value.OffsetY, anchorVersion, DispatcherPriority.Loaded);
        ApplyPreferredDialogViewportAnchor(preservedViewportAnchor.Value.ControlName, preservedViewportAnchor.Value.OffsetY, anchorVersion, DispatcherPriority.Background);
        ScheduleDelayedPreferredDialogViewportAnchor(preservedViewportAnchor.Value.ControlName, preservedViewportAnchor.Value.OffsetY, anchorVersion);
    }

    private void PrimePreferredScrollOffsetForDialogRebind(
        string? dialogId,
        Vector? preservedScrollOffset,
        (string ControlName, double OffsetY)? preservedViewportAnchor,
        (string ControlName, double OffsetY)? preservedInteractionAnchor)
    {
        if (preservedScrollOffset is { } offset)
        {
            _dialogScrollViewer.Offset = offset;
        }
        else if (preservedViewportAnchor is null && preservedInteractionAnchor is null)
        {
            _dialogScrollViewer.Offset = default;
        }

        if (!string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal))
        {
            return;
        }

        if (preservedScrollOffset is not null)
        {
            return;
        }

        if (preservedInteractionAnchor is { } interactionAnchor)
        {
            ApplyPreferredDialogInteractionAnchorNow(interactionAnchor.ControlName, interactionAnchor.OffsetY);
        }

        if (preservedInteractionAnchor is null && preservedViewportAnchor is { } viewportAnchor)
        {
            ApplyPreferredDialogViewportAnchorNow(viewportAnchor.ControlName, viewportAnchor.OffsetY);
        }
    }

    private void RestoreTransientDialogState(string? dialogId, bool preservedOriginWizardAdvancedStoryControlsExpanded)
    {
        if (!string.Equals(dialogId, OriginWizardDialogId, StringComparison.Ordinal))
        {
            return;
        }

        Expander? advancedStoryControls = _dialogFieldsPanel.GetVisualDescendants()
            .OfType<Expander>()
            .FirstOrDefault(expander => string.Equals(expander.Name, OriginWizardAdvancedStoryControlsExpanderName, StringComparison.Ordinal));
        if (advancedStoryControls is null)
        {
            return;
        }

        advancedStoryControls.IsExpanded = preservedOriginWizardAdvancedStoryControlsExpanded;
        _originWizardAdvancedStoryControlsExpanded = preservedOriginWizardAdvancedStoryControlsExpanded;
    }

    private (string ControlName, double OffsetY)? CaptureCurrentOriginWizardViewportAnchor()
    {
        if (!string.Equals(BoundDialogId, OriginWizardDialogId, StringComparison.Ordinal) || !_originWizardAdvancedStoryControlsExpanded)
        {
            return null;
        }

        Expander? advancedStoryControls = _dialogFieldsPanel.GetVisualDescendants()
            .OfType<Expander>()
            .FirstOrDefault(expander => string.Equals(expander.Name, OriginWizardAdvancedStoryControlsExpanderName, StringComparison.Ordinal));
        if (advancedStoryControls is null || string.IsNullOrWhiteSpace(advancedStoryControls.Name))
        {
            return null;
        }

        Point? translated = advancedStoryControls.TranslatePoint(default, _dialogScrollViewer);
        if (translated is null)
        {
            return null;
        }

        return (advancedStoryControls.Name, translated.Value.Y);
    }

    private void CapturePreferredFocusState()
    {
        _preferredFocusControlName = null;
        _preferredFocusSelectionStart = null;

        if (GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Control focusedControl)
        {
            return;
        }

        if (focusedControl.GetVisualRoot() is not DesktopDialogWindow)
        {
            return;
        }

        _preferredFocusControlName = focusedControl.Name;
        if (focusedControl is TextBox textBox)
        {
            _preferredFocusSelectionStart = textBox.CaretIndex;
        }
    }

    private void RememberPreferredFocus(Control? control)
    {
        _preferredFocusControlName = null;
        _preferredFocusSelectionStart = null;

        if (control is null || control.GetVisualRoot() is not DesktopDialogWindow)
        {
            return;
        }

        _preferredFocusControlName = control.Name;
        if (control is TextBox textBox)
        {
            _preferredFocusSelectionStart = textBox.CaretIndex;
        }
    }

    private async void QueueDialogFieldUpdate(string fieldId, string value, Control? preferredControl = null)
    {
        if (_adapter is null || _suppressDialogUpdates)
            return;

        CaptureTransientDialogState();
        bool suppressOriginWizardCollapseDuringRefresh = false;
        if (preferredControl is ComboBox)
        {
            suppressOriginWizardCollapseDuringRefresh = ShouldPreserveOriginWizardComboInteractionScroll();
            _suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh = suppressOriginWizardCollapseDuringRefresh;
            ArmOriginWizardTransientRefresh();
            _preferredDialogScrollAnchor ??= _dialogScrollViewer.Offset;
            CapturePreferredDialogViewportAnchor();
            CapturePreferredDialogInteractionAnchor(preferredControl);
            RestorePreferredScrollAnchorDuringOriginWizardComboInteraction();
        }
        else
        {
            _preferredDialogScrollAnchor = null;
            _preferredDialogScrollAnchorVersion++;
            ClearPreferredDialogViewportAnchor();
            ClearPreferredDialogInteractionAnchor();
        }

        _skipPreferredFocusRestoreOnNextBind = preferredControl is ComboBox;
        if (preferredControl is null)
        {
            CapturePreferredFocusState();
        }
        else
        {
            RememberPreferredFocus(preferredControl);
        }

        int bindVersionBeforeUpdate = _dialogBindVersion;
        await ExecuteSafeAsync(
            () => _adapter.UpdateDialogFieldAsync(fieldId, value, CancellationToken.None),
            $"update field '{fieldId}'");
        if (suppressOriginWizardCollapseDuringRefresh)
        {
            _suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh = false;
        }

        if (_dialogBindVersion == bindVersionBeforeUpdate)
        {
            _skipPreferredFocusRestoreOnNextBind = false;
            FocusPreferredControlDuringRestore();
        }
    }

    internal bool TryDeferCloseForPendingOriginWizardTransientRefresh()
    {
        if (!ShouldPreservePendingOriginWizardTransientRefresh())
        {
            ClearPendingOriginWizardTransientRefresh();
            return false;
        }

        int closeDeferralVersion = ++_originWizardTransientRefreshCloseDeferralVersion;
        DispatcherTimer.RunOnce(() =>
        {
            if (closeDeferralVersion != _originWizardTransientRefreshCloseDeferralVersion
                || !_originWizardTransientRefreshPending)
            {
                return;
            }

            ClearPendingOriginWizardTransientRefresh();
            CloseFromPresenter();
        }, OriginWizardTransientRefreshCloseGrace);
        return true;
    }

    private void RestoreDialogScrollOffset(Vector offset)
    {
        double maxX = Math.Max(0d, _dialogScrollViewer.Extent.Width - _dialogScrollViewer.Viewport.Width);
        double maxY = Math.Max(0d, _dialogScrollViewer.Extent.Height - _dialogScrollViewer.Viewport.Height);
        _dialogScrollViewer.Offset = new Vector(
            Math.Clamp(offset.X, 0d, maxX),
            Math.Clamp(offset.Y, 0d, maxY));
    }

    private void FocusPreferredControlDuringRestore(bool allowFallback = true)
    {
        _suppressProgrammaticComboFocusAnchorCapture = true;
        try
        {
            FocusPreferredControl(allowFallback);
        }
        finally
        {
            Dispatcher.UIThread.Post(
                () => _suppressProgrammaticComboFocusAnchorCapture = false,
                DispatcherPriority.Background);
        }
    }

    private bool ShouldPreserveOriginWizardComboInteractionScroll()
    {
        return string.Equals(BoundDialogId, OriginWizardDialogId, StringComparison.Ordinal)
            && _originWizardAdvancedStoryControlsExpanded;
    }

    private void ArmOriginWizardTransientRefresh()
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll())
        {
            ClearPendingOriginWizardTransientRefresh();
            return;
        }

        _originWizardTransientRefreshPending = true;
        _originWizardTransientRefreshPendingAtUtc = DateTimeOffset.UtcNow;
        _originWizardTransientRefreshCloseDeferralVersion++;
    }

    private void ClearPendingOriginWizardTransientRefresh()
    {
        _originWizardTransientRefreshPending = false;
        _originWizardTransientRefreshPendingAtUtc = default;
        _originWizardTransientRefreshCloseDeferralVersion++;
    }

    private bool ShouldPreservePendingOriginWizardTransientRefresh()
    {
        if (!_originWizardTransientRefreshPending
            || !ShouldPreserveOriginWizardComboInteractionScroll())
        {
            return false;
        }

        if (_preferredDialogScrollAnchor is null
            && _preferredDialogViewportAnchor is null
            && _preferredDialogInteractionAnchor is null)
        {
            return false;
        }

        return (DateTimeOffset.UtcNow - _originWizardTransientRefreshPendingAtUtc) <= OriginWizardTransientRefreshCloseGrace;
    }

    private void RestorePreferredScrollAnchorDuringOriginWizardComboInteraction()
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll())
        {
            return;
        }

        bool hasPreferredAnchor = _preferredDialogViewportAnchor is { } || _preferredDialogInteractionAnchor is { };
        if (!hasPreferredAnchor && _preferredDialogScrollAnchor is Vector anchor)
        {
            int anchorVersion = ++_preferredDialogScrollAnchorVersion;
            ApplyPreferredDialogScrollAnchor(anchor, anchorVersion, DispatcherPriority.Background);
            ScheduleDelayedPreferredDialogScrollAnchor(anchor, anchorVersion);
        }

        if (_preferredDialogInteractionAnchor is null && _preferredDialogViewportAnchor is { } viewportAnchor)
        {
            int viewportAnchorVersion = ++_preferredDialogViewportAnchorVersion;
            ApplyPreferredDialogViewportAnchor(viewportAnchor.ControlName, viewportAnchor.OffsetY, viewportAnchorVersion, DispatcherPriority.Background);
            ScheduleDelayedPreferredDialogViewportAnchor(viewportAnchor.ControlName, viewportAnchor.OffsetY, viewportAnchorVersion);
        }

        if (_preferredDialogInteractionAnchor is { } interactionAnchor)
        {
            int interactionAnchorVersion = ++_preferredDialogInteractionAnchorVersion;
            ApplyPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion, DispatcherPriority.Background);
            ScheduleDelayedPreferredDialogInteractionAnchor(interactionAnchor.ControlName, interactionAnchor.OffsetY, interactionAnchorVersion);
        }
    }

    private void ScheduleDelayedPreferredDialogScrollAnchor(Vector anchor, int anchorVersion)
    {
        foreach (TimeSpan delay in DelayedOriginWizardComboRestoreDelays)
        {
            DispatcherTimer.RunOnce(
                () => ApplyPreferredDialogScrollAnchor(anchor, anchorVersion, DispatcherPriority.Background),
                delay);
        }
    }

    private void ScheduleDelayedPreferredDialogViewportAnchor(string controlName, double offsetY, int anchorVersion)
    {
        foreach (TimeSpan delay in DelayedOriginWizardComboRestoreDelays)
        {
            DispatcherTimer.RunOnce(
                () => ApplyPreferredDialogViewportAnchor(controlName, offsetY, anchorVersion, DispatcherPriority.Background),
                delay);
        }
    }

    private void ScheduleDelayedPreferredDialogInteractionAnchor(string controlName, double offsetY, int anchorVersion)
    {
        foreach (TimeSpan delay in DelayedOriginWizardComboRestoreDelays)
        {
            DispatcherTimer.RunOnce(
                () => ApplyPreferredDialogInteractionAnchor(controlName, offsetY, anchorVersion, DispatcherPriority.Background),
                delay);
        }
    }

    private void ApplyPreferredDialogScrollAnchor(Vector anchor, int anchorVersion, DispatcherPriority priority)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (anchorVersion != _preferredDialogScrollAnchorVersion
                || !ShouldPreserveOriginWizardComboInteractionScroll())
            {
                return;
            }

            _dialogScrollViewer.Offset = anchor;
        }, priority);
    }

    private void ApplyPreferredDialogViewportAnchor(string controlName, double offsetY, int anchorVersion, DispatcherPriority priority)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (anchorVersion != _preferredDialogViewportAnchorVersion
                || !ShouldPreserveOriginWizardComboInteractionScroll())
            {
                return;
            }

            TryAdjustDialogScrollOffsetForAnchor(controlName, offsetY);
        }, priority);
    }

    private void ApplyPreferredDialogInteractionAnchor(string controlName, double offsetY, int anchorVersion, DispatcherPriority priority)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (anchorVersion != _preferredDialogInteractionAnchorVersion
                || !ShouldPreserveOriginWizardComboInteractionScroll())
            {
                return;
            }

            TryAdjustDialogScrollOffsetForAnchor(controlName, offsetY);
        }, priority);
    }

    private void ApplyPreferredDialogViewportAnchorNow(string controlName, double offsetY)
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll())
        {
            return;
        }

        TryAdjustDialogScrollOffsetForAnchor(controlName, offsetY);
    }

    private void ApplyPreferredDialogInteractionAnchorNow(string controlName, double offsetY)
    {
        if (!ShouldPreserveOriginWizardComboInteractionScroll())
        {
            return;
        }

        TryAdjustDialogScrollOffsetForAnchor(controlName, offsetY);
    }

    private bool TryAdjustDialogScrollOffsetForAnchor(string controlName, double offsetY)
    {
        Control? anchorControl = _dialogFieldsPanel.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => string.Equals(control.Name, controlName, StringComparison.Ordinal));
        if (anchorControl is null)
        {
            return false;
        }

        Point? translated = anchorControl.TranslatePoint(default, _dialogScrollViewer);
        if (translated is null)
        {
            return false;
        }

        double deltaY = translated.Value.Y - offsetY;
        if (Math.Abs(deltaY) <= 0.5d)
        {
            return false;
        }

        Vector currentOffset = _dialogScrollViewer.Offset;
        double maxOffsetY = Math.Max(0d, _dialogScrollViewer.Extent.Height - _dialogScrollViewer.Viewport.Height);
        if (maxOffsetY <= 0d && currentOffset.Y > 0d)
        {
            return false;
        }

        double nextOffsetY = Math.Clamp(currentOffset.Y + deltaY, 0d, maxOffsetY);
        _dialogScrollViewer.Offset = new Vector(currentOffset.X, nextOffsetY);
        return true;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_suppressCloseNotification)
            return;

        if (_adapter is null)
            return;

        _ = ExecuteSafeAsync(
            () => _adapter.CloseDialogAsync(CancellationToken.None),
            "close dialog");
    }

    private sealed record RosterDialogSnapshotDisplay(
        string FallbackAlias,
        string FallbackName,
        string FallbackWorkspace,
        IReadOnlyList<RosterWorkspaceDisplay> Workspaces,
        IReadOnlyList<string> WatchedFiles);

    private sealed record RosterWorkspaceDisplay(
        string Id,
        string Name,
        string Alias,
        DateTimeOffset LastOpenedUtc,
        string RulesetId,
        bool HasSavedWorkspace);

    private sealed record RosterTreeItem(
        string Label,
        string? RunnerId,
        string? WatchFile,
        IReadOnlyList<RosterTreeItem> Children);

    private static bool ParseCheckbox(string value)
    {
        if (bool.TryParse(value, out bool parsed))
            return parsed;

        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteSafeAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Dialog operations are best-effort while users interact with fields and buttons.
        }
        catch (Exception ex)
        {
            _dialogMessageText.Text = DesktopDialogChromeBoundary.BuildFailureMessage(operationName, ex.Message);
            _dialogMessageText.IsVisible = true;
        }
    }

    private void FocusPreferredControl(bool allowFallback = true)
    {
        if (TryRestorePreferredFocus())
        {
            return;
        }

        if (!allowFallback)
        {
            return;
        }

        Button? primaryAction = _dialogActionsPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => button.FontWeight == FontWeight.SemiBold);

        if (primaryAction is not null && primaryAction.IsEnabled)
        {
            primaryAction.Focus();
            return;
        }

        _dialogFieldsPanel.Children
            .SelectMany(row => row is InputElement inputElement
                ? row.GetVisualDescendants().OfType<InputElement>().Prepend(inputElement)
                : row.GetVisualDescendants().OfType<InputElement>())
            .OfType<InputElement>()
            .FirstOrDefault(control => control.Focusable && control.IsEnabled)?
            .Focus();
    }

    private bool TryRestorePreferredFocus()
    {
        if (string.IsNullOrWhiteSpace(_preferredFocusControlName))
        {
            return false;
        }

        Control? control = this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, _preferredFocusControlName, StringComparison.Ordinal));
        if (control is not InputElement inputElement
            || !inputElement.Focusable
            || !control.IsEnabled)
        {
            return false;
        }

        bool focused = inputElement.Focus();
        if (focused && control is TextBox textBox && _preferredFocusSelectionStart is int caretIndex)
        {
            textBox.CaretIndex = Math.Clamp(caretIndex, 0, textBox.Text?.Length ?? 0);
        }

        return focused;
    }

    private void RestorePreferredTextBoxFocus(TextBox textBox)
    {
        if (!string.Equals(_preferredFocusControlName, textBox.Name, StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!textBox.IsEnabled)
            {
                return;
            }

            bool focused = textBox.Focus();
            if (focused && _preferredFocusSelectionStart is int caretIndex)
            {
                textBox.CaretIndex = Math.Clamp(caretIndex, 0, textBox.Text?.Length ?? 0);
            }
        }, DispatcherPriority.Input);
    }

    private static IBrush ResolveThemeBrush(string resourceKey, string fallbackHex)
    {
        if (App.Current?.TryFindResource(resourceKey, out object? resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}

internal static class DesktopDialogWindowExtensions
{
    public static T Also<T>(this T instance, Action<T> configure)
    {
        configure(instance);
        return instance;
    }
}
