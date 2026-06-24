using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chummer.Presentation.Overview;
using System.Globalization;
using System.IO;

namespace Chummer.Avalonia.Controls;

public partial class CommandDialogPaneControl : UserControl
{
    private bool _suppressCommandSelectionEvent;
    private bool _suppressDialogUpdates;
    private string _currentDialogTitle = string.Empty;

    public CommandDialogPaneControl()
    {
        InitializeComponent();
        DesktopShellTheme.ApplyShellListBoxTheme(CommandsList);
        CommandsList.SelectionChanged += CommandsList_OnSelectionChanged;
    }

    public event EventHandler<string>? CommandSelected;
    public event EventHandler<string>? DialogActionSelected;
    public event EventHandler<DialogFieldValueChangedEventArgs>? DialogFieldValueChanged;

    public void SetState(CommandDialogPaneState state)
    {
        SetCommands(state.Commands, state.SelectedCommandId);
        SetDialog(
            state.DialogTitle,
            state.DialogMessage,
            state.DialogTrustReceipt,
            state.Fields,
            state.Actions);
    }

    public void SetCommands(IEnumerable<CommandPaletteItem> commands, string? selectedCommandId)
    {
        CommandPaletteItem[] commandItems = commands.ToArray();
        CommandsHostBorder.IsVisible = commandItems.Length > 0;
        _suppressCommandSelectionEvent = true;
        CommandsList.ItemsSource = commandItems;
        CommandsList.SelectedItem = commandItems
            .FirstOrDefault(item => string.Equals(item.Id, selectedCommandId, StringComparison.Ordinal));
        _suppressCommandSelectionEvent = false;
    }

    public void SetDialog(
        string? title,
        string? message,
        string? trustReceipt,
        IEnumerable<DialogFieldDisplayItem> fields,
        IEnumerable<DialogActionDisplayItem> actions)
    {
        _currentDialogTitle = title?.Trim() ?? string.Empty;
        string normalizedMessage = message?.Trim() ?? string.Empty;
        string normalizedTrustReceipt = trustReceipt?.Trim() ?? string.Empty;
        DialogTitleText.Text = _currentDialogTitle;
        DialogTitleText.IsVisible = !string.IsNullOrWhiteSpace(_currentDialogTitle);
        DialogMessageText.Text = normalizedMessage;
        DialogMessageBorder.IsVisible = !string.IsNullOrWhiteSpace(normalizedMessage);
        DialogTrustReceiptText.Text = normalizedTrustReceipt;
        DialogTrustReceiptBorder.IsVisible = !string.IsNullOrWhiteSpace(normalizedTrustReceipt);
        ToolTip.SetTip(DialogFieldsHost, null);
        RebuildDialogFields(fields.ToArray());
        RebuildDialogActions(actions.ToArray());
        RefreshDialogVisuals();
    }

    private void CommandsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressCommandSelectionEvent)
            return;

        if (CommandsList.SelectedItem is not CommandPaletteItem command || !command.Enabled)
            return;

        CommandSelected?.Invoke(this, command.Id);
        ClearSelection(CommandsList, ref _suppressCommandSelectionEvent);
    }

    private void DialogActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_suppressDialogUpdates || sender is not Button button || button.Tag is not string actionId)
            return;

        DialogActionSelected?.Invoke(this, actionId);
    }

    private static void ClearSelection(ListBox listBox, ref bool suppressSelectionEvent)
    {
        suppressSelectionEvent = true;
        listBox.SelectedItem = null;
        suppressSelectionEvent = false;
    }

    private void RebuildDialogFields(DialogFieldDisplayItem[] fields)
    {
        _suppressDialogUpdates = true;
        DialogFieldsHost.Children.Clear();

        if (TryBuildLegacyGlobalSettingsFields(fields))
        {
            _suppressDialogUpdates = false;
            return;
        }

        if (TryBuildSelectionAddFields(fields))
        {
            _suppressDialogUpdates = false;
            return;
        }

        DialogFieldDisplayItem[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .Where(ShouldRenderField)
            .ToArray();

        for (int index = 0; index < visibleFields.Length; index++)
        {
            DialogFieldDisplayItem field = visibleFields[index];
            if (string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Left, StringComparison.Ordinal)
                && index + 1 < visibleFields.Length
                && string.Equals(visibleFields[index + 1].LayoutSlot, DesktopDialogFieldLayoutSlots.Right, StringComparison.Ordinal))
            {
                DialogFieldsHost.Children.Add(CreateSplitFieldRow(field, visibleFields[index + 1]));
                index++;
                continue;
            }

            DialogFieldsHost.Children.Add(CreateStandaloneFieldRow(field));
        }

        _suppressDialogUpdates = false;
    }

    private bool TryBuildSelectionAddFields(IReadOnlyList<DialogFieldDisplayItem> fields)
    {
        DialogFieldDisplayItem[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .Where(ShouldRenderField)
            .ToArray();
        if (!visibleFields.Any(static field => field.Id.Contains("CandidateList", StringComparison.Ordinal)))
        {
            return false;
        }

        DialogFieldsHost.Children.Add(CreateSelectionAddPane(visibleFields));
        return true;
    }

    private Control CreateSelectionAddPane(IReadOnlyList<DialogFieldDisplayItem> fields)
    {
        DialogFieldDisplayItem? navigationField = fields.FirstOrDefault(static field =>
            string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal));
        DialogFieldDisplayItem? candidateField = fields.FirstOrDefault(static field =>
            field.Id.Contains("CandidateList", StringComparison.Ordinal)
            && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal));
        DialogFieldDisplayItem? browseGridField = fields.FirstOrDefault(static field =>
            field.Id.Contains("BrowseGrid", StringComparison.Ordinal)
            && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal));

        HashSet<string> reservedIds = fields
            .Where(static field =>
                string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal)
                || field.Id.Contains("CandidateList", StringComparison.Ordinal)
                || field.Id.Contains("BrowseGrid", StringComparison.Ordinal))
            .Select(static field => field.Id)
            .ToHashSet(StringComparer.Ordinal);

        DialogFieldDisplayItem[] topFields = fields
            .Where(field => !reservedIds.Contains(field.Id))
            .Where(field => !field.IsReadOnly
                || string.Equals(field.InputType, "select", StringComparison.Ordinal)
                || string.Equals(field.InputType, "number", StringComparison.Ordinal)
                || string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
            .Where(field => !IsSupportSummaryField(field))
            .ToArray();

        DialogFieldDisplayItem[] rightDetails = fields
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
            shell.Children.Add(CreateClassicSelectionToolbar(BuildSelectionTopRows(topFields).ToArray()));
        }

        Grid body = new()
        {
            ColumnDefinitions = new ColumnDefinitions("1.05*,0.95*"),
            ColumnSpacing = 14
        };

        StackPanel leftColumn = new() { Spacing = 10 };
        if (navigationField is not null)
        {
            leftColumn.Children.Add(CreateSelectionSurfaceCard(ResolveSelectionNavigationTitle(navigationField), CreateSelectionCategoryTreePanel(navigationField, fields), 112));
        }

        if (candidateField is not null)
        {
            leftColumn.Children.Add(CreateSelectionSurfaceCard("Available", CreateSelectionCandidatePanel(candidateField, fields), 248));
        }

        if (leftColumn.Children.Count > 0)
        {
            Grid.SetColumn(leftColumn, 0);
            body.Children.Add(leftColumn);
        }

        StackPanel rightColumn = new() { Spacing = 10 };
        if (browseGridField is not null)
        {
            rightColumn.Children.Add(CreateSelectionSurfaceCard(browseGridField.Label, CreateFieldControl(browseGridField), 132));
        }

        foreach (DialogFieldDisplayItem detailField in rightDetails)
        {
            rightColumn.Children.Add(CreateSelectionSurfaceCard(detailField.Label, CreateFieldControl(detailField), ResolveSelectionPanelMinHeight(detailField)));
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

    private static string ResolveSelectionNavigationTitle(DialogFieldDisplayItem field)
    {
        if (!string.Equals(field.Label, "Navigation", StringComparison.Ordinal))
        {
            return field.Label;
        }

        return field.Id.Contains("CategoryTree", StringComparison.Ordinal)
            ? "Categories"
            : "Current selection";
    }

    private bool TryBuildLegacyGlobalSettingsFields(IReadOnlyList<DialogFieldDisplayItem> fields)
    {
        if (!string.Equals(_currentDialogTitle, "Global Settings", StringComparison.Ordinal))
        {
            return false;
        }

        DialogFieldDisplayItem? themeField = FindOptionalField(fields, "globalTheme");
        DialogFieldDisplayItem? uiScaleField = FindOptionalField(fields, "globalUiScale");
        DialogFieldDisplayItem? languageField = FindOptionalField(fields, "globalLanguage");
        DialogFieldDisplayItem? sheetLanguageField = FindOptionalField(fields, "globalSheetLanguage");
        DialogFieldDisplayItem? compactModeField = FindOptionalField(fields, "globalCompactMode");
        DialogFieldDisplayItem? characterPriorityField = FindOptionalField(fields, "globalCharacterPriority");
        DialogFieldDisplayItem? updateModeField = FindOptionalField(fields, "globalUpdateMode");
        DialogFieldDisplayItem? preferNightlyField = FindOptionalField(fields, "globalPreferNightlyBuilds");
        DialogFieldDisplayItem? rosterPathField = FindOptionalField(fields, "globalCharacterRosterPath");
        DialogFieldDisplayItem? hideMasterIndexField = FindOptionalField(fields, "globalHideMasterIndex");
        DialogFieldDisplayItem? analyticsOptInField = FindOptionalField(fields, "globalAnalyticsOptIn");

        if (themeField is null
            || uiScaleField is null
            || languageField is null
            || sheetLanguageField is null
            || compactModeField is null
            || characterPriorityField is null
            || updateModeField is null
            || preferNightlyField is null
            || rosterPathField is null
            || hideMasterIndexField is null
            || analyticsOptInField is null)
        {
            return false;
        }

        DialogFieldsHost.Children.Add(CreateLegacySettingsPairRow(themeField, uiScaleField));
        DialogFieldsHost.Children.Add(CreateLegacySettingsPairRow(languageField, sheetLanguageField));
        DialogFieldsHost.Children.Add(CreateLegacySettingsPairRow(characterPriorityField, compactModeField));
        DialogFieldsHost.Children.Add(CreateLegacySettingsPairRow(updateModeField, preferNightlyField));
        DialogFieldsHost.Children.Add(CreateLegacySettingsPairRow(hideMasterIndexField, analyticsOptInField));
        DialogFieldsHost.Children.Add(CreateStandaloneFieldRow(rosterPathField));
        return true;
    }

    private void RebuildDialogActions(DialogActionDisplayItem[] actions)
    {
        DialogActionsHost.Children.Clear();
        DialogActionsBorder.IsVisible = actions.Length > 0;

        foreach (DialogActionDisplayItem action in actions)
        {
            Button button = new()
            {
                Name = DesktopDialogAccessibility.BuildActionName(action.Id),
                Content = action.Label,
                Tag = action.Id,
                MinWidth = 82,
                Classes = { "shell-action", action.IsPrimary ? "primary" : "quiet" }
            };
            ApplyAccessibility(button, action.AccessibleName, action.ToolTip, action.HelpText);
            button.Click += DialogActionButton_OnClick;
            DialogActionsHost.Children.Add(button);
        }
    }

    private void RefreshDialogVisuals()
    {
        DialogTitleText.InvalidateMeasure();
        DialogTitleText.InvalidateArrange();
        DialogTitleText.InvalidateVisual();
        DialogMessageText.InvalidateMeasure();
        DialogMessageText.InvalidateArrange();
        DialogMessageText.InvalidateVisual();
        DialogMessageBorder.InvalidateMeasure();
        DialogMessageBorder.InvalidateArrange();
        DialogMessageBorder.InvalidateVisual();
        DialogFieldsHost.InvalidateMeasure();
        DialogFieldsHost.InvalidateArrange();
        DialogFieldsHost.InvalidateVisual();
        DialogActionsHost.InvalidateMeasure();
        DialogActionsHost.InvalidateArrange();
        DialogActionsHost.InvalidateVisual();
        DialogActionsBorder.InvalidateMeasure();
        DialogActionsBorder.InvalidateArrange();
        DialogActionsBorder.InvalidateVisual();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private Control CreateStandaloneFieldRow(DialogFieldDisplayItem field)
    {
        return CreateFieldPane(field);
    }

    private Control CreateSplitFieldRow(DialogFieldDisplayItem left, DialogFieldDisplayItem right)
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

    private Control CreateLegacySettingsPairRow(DialogFieldDisplayItem left, DialogFieldDisplayItem? right)
    {
        Grid row = new()
        {
            ColumnDefinitions = new ColumnDefinitions(right is null ? "156,*" : "156,*,156,*"),
            ColumnSpacing = 8
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

    private Control CreateLegacySettingsLabel(DialogFieldDisplayItem field, int column)
    {
        TextBlock label = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
            Text = field.Label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        Grid.SetColumn(label, column);
        return label;
    }

    private Control CreateLegacySettingsInput(DialogFieldDisplayItem field, int column)
    {
        Control input = CreateFieldControl(field);
        input.Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id);
        ApplyAccessibility(input, field.AccessibleName, field.ToolTip, field.HelpText);
        Grid.SetColumn(input, column);
        return input;
    }

    private IEnumerable<Control> BuildSelectionTopRows(IReadOnlyList<DialogFieldDisplayItem> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            DialogFieldDisplayItem current = fields[index];
            if (index + 1 < fields.Count && CanPairSelectionTopField(current, fields[index + 1]))
            {
                yield return CreateSplitFieldRow(current, fields[index + 1]);
                index++;
                continue;
            }

            yield return CreateStandaloneFieldRow(current);
        }
    }

    private static bool CanPairSelectionTopField(DialogFieldDisplayItem left, DialogFieldDisplayItem right)
    {
        return !left.IsMultiline
            && !right.IsMultiline
            && !string.Equals(left.InputType, "checkbox", StringComparison.Ordinal)
            && !string.Equals(right.InputType, "checkbox", StringComparison.Ordinal);
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
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B7C4D5"),
            Background = ResolveThemeBrush("ChummerShellSelectionToolbarBrush", "#EEF2F6"),
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
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B7C4D5"),
            Background = ResolveThemeBrush("ChummerShellSelectionPanelBrush", "#F8FAFC"),
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
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
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

    private static bool IsSupportSummaryField(DialogFieldDisplayItem field)
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

    private Control CreateSelectionCandidatePanel(
        DialogFieldDisplayItem candidateField,
        IReadOnlyList<DialogFieldDisplayItem> allFields)
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
        DesktopShellTheme.ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate = new FuncDataTemplate<SelectionCandidateItem>((item, _) => BuildClassicSelectionCandidateRow(item));
        listBox.SelectionChanged += (_, _) =>
        {
            if (_suppressDialogUpdates || primaryFieldId is null || listBox.SelectedItem is not SelectionCandidateItem selectedItem)
            {
                return;
            }

            DialogFieldValueChanged?.Invoke(this, new DialogFieldValueChangedEventArgs(primaryFieldId, selectedItem.Value));
        };

        SelectionCandidateItem? selectedItem = items.FirstOrDefault(item =>
            string.Equals(item.Value, selectedName, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(static item => item.IsSelected)
            ?? items.FirstOrDefault();
        if (selectedItem is not null)
        {
            listBox.SelectedItem = selectedItem;
        }

        StackPanel shell = new()
        {
            Spacing = 8
        };
        shell.Children.Add(CreateClassicSelectionSectionHeader(candidateField.Label, items.Count));
        shell.Children.Add(listBox);
        return shell;
    }

    private Control CreateSelectionCategoryTreePanel(
        DialogFieldDisplayItem categoryTreeField,
        IReadOnlyList<DialogFieldDisplayItem> allFields)
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
        DesktopShellTheme.ApplyShellListBoxTheme(listBox);
        listBox.ItemTemplate = new FuncDataTemplate<SelectionCandidateItem>((item, _) => BuildClassicSelectionCandidateRow(item));
        listBox.SelectionChanged += (_, _) =>
        {
            if (_suppressDialogUpdates || categoryFieldId is null || listBox.SelectedItem is not SelectionCandidateItem selectedItem)
            {
                return;
            }

            DialogFieldValueChanged?.Invoke(this, new DialogFieldValueChangedEventArgs(categoryFieldId, selectedItem.Value));
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
        DialogFieldDisplayItem categoryTreeField,
        IReadOnlyList<DialogFieldDisplayItem> allFields,
        string? categoryFieldId)
    {
        DialogFieldDisplayItem? categoryField = allFields.FirstOrDefault(field =>
            string.Equals(field.Id, categoryFieldId, StringComparison.Ordinal));
        DialogFieldOptionDisplayItem[] options = categoryField?.Options?
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
            Foreground = ResolveThemeBrush("ChummerShellTextMutedBrush", "#53657D"),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(badge, 1);
        header.Children.Add(title);
        header.Children.Add(badge);

        return new Border
        {
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
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
            body.Children.Add(DesktopShellTheme.CreateOptionMetaText(meta));
        }

        return new Border
        {
            Padding = new Thickness(5, 4),
            Margin = new Thickness(0),
            Child = body
        };
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

    private static void SplitSelectionCandidateDisplayText(string displayText, out string title, out string meta)
    {
        string[] segments = displayText
            .Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        title = segments.FirstOrDefault() ?? string.Empty;
        meta = segments.Length > 1
            ? string.Join("  |  ", segments.Skip(1))
            : string.Empty;
    }

    private static string? ResolveSelectionPrimaryFieldId(
        DialogFieldDisplayItem candidateField,
        IReadOnlyList<DialogFieldDisplayItem> allFields)
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
        DialogFieldDisplayItem categoryTreeField,
        IReadOnlyList<DialogFieldDisplayItem> allFields)
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
        DialogFieldDisplayItem categoryTreeField,
        IReadOnlyList<DialogFieldDisplayItem> allFields,
        string? categoryFieldId)
    {
        string prefix = categoryTreeField.Id.EndsWith("CategoryTree", StringComparison.Ordinal)
            ? categoryTreeField.Id[..^"CategoryTree".Length]
            : categoryTreeField.Id;
        DialogFieldDisplayItem? selectedBranchField = allFields.FirstOrDefault(field =>
            string.Equals(field.Id, prefix + "SelectedBranch", StringComparison.Ordinal));
        return selectedBranchField?.Value
            ?? allFields.FirstOrDefault(field => string.Equals(field.Id, categoryFieldId, StringComparison.Ordinal))?.Value;
    }

    private static double ResolveSelectionPanelMinHeight(DialogFieldDisplayItem field)
    {
        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal))
        {
            return 120d;
        }

        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Snippet, StringComparison.Ordinal))
        {
            return 68d;
        }

        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal))
        {
            return 92d;
        }

        return field.IsMultiline ? 96d : 60d;
    }

    private Control CreateFieldPane(DialogFieldDisplayItem field)
    {
        if (string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
        {
            CheckBox checkBox = new()
            {
                Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id),
                Content = field.Label,
                IsChecked = ParseCheckbox(field.Value),
                IsEnabled = !field.IsReadOnly
            };
            if (!field.IsReadOnly)
            {
                checkBox.IsCheckedChanged += (_, _) =>
                {
                    if (_suppressDialogUpdates)
                    {
                        return;
                    }

                    string nextValue = checkBox.IsChecked == true ? "true" : "false";
                    if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    DialogFieldValueChanged?.Invoke(
                        this,
                        new DialogFieldValueChangedEventArgs(field.Id, nextValue));
                };
            }

            ApplyAccessibility(checkBox, field.AccessibleName, field.ToolTip, field.HelpText);
            return checkBox;
        }

        StackPanel row = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldContainerName(field.Id),
            Spacing = 4
        };
        TextBlock label = new()
        {
            Name = DesktopDialogAccessibility.BuildFieldLabelName(field.Id),
            Text = field.Label,
            FontWeight = FontWeight.SemiBold
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(label);

        Control fieldControl = CreateFieldControl(field);
        fieldControl.Name = DesktopDialogAccessibility.BuildFieldInputName(field.Id);
        ApplyAccessibility(fieldControl, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(fieldControl);
        return row;
    }

    private static bool ShouldRenderField(DialogFieldDisplayItem field)
    {
        if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tabs, StringComparison.Ordinal))
        {
            // Chummer5a parity posture: synthetic dialog tab strips and section rails
            // stay out of the visible surface even if the presenter still carries them.
            return false;
        }

        return true;
    }

    private Control CreateFieldControl(DialogFieldDisplayItem field)
    {
        if (string.Equals(field.InputType, "select", StringComparison.Ordinal)
            && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal))
        {
            DialogFieldOptionDisplayItem[] options = (field.Options ?? [])
                .DistinctBy(option => option.Value, StringComparer.Ordinal)
                .ToArray();
            ListBox listBox = new()
            {
                ItemsSource = options,
                SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, field.Value, StringComparison.Ordinal)),
                IsEnabled = !field.IsReadOnly,
                MinHeight = 160
            };
            DesktopShellTheme.ApplyShellListBoxTheme(listBox);
            listBox.ItemTemplate = new FuncDataTemplate<DialogFieldOptionDisplayItem>((option, _) =>
                DesktopShellTheme.CreateOptionText(option?.Label ?? string.Empty, TextWrapping.Wrap));
            if (!field.IsReadOnly)
            {
                listBox.SelectionChanged += (_, _) =>
                {
                    if (_suppressDialogUpdates)
                    {
                        return;
                    }

                    if (listBox.SelectedItem is not DialogFieldOptionDisplayItem selectedOption)
                    {
                        return;
                    }

                    if (string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    DialogFieldValueChanged?.Invoke(
                        this,
                        new DialogFieldValueChangedEventArgs(field.Id, selectedOption.Value));
                };
            }

            ApplyAccessibility(listBox, field.AccessibleName, field.ToolTip, field.HelpText);
            return listBox;
        }

        if (string.Equals(field.InputType, "select", StringComparison.Ordinal))
        {
            DialogFieldOptionDisplayItem[] options = (field.Options ?? [])
                .DistinctBy(option => option.Value, StringComparer.Ordinal)
                .ToArray();
            ComboBox comboBox = new()
            {
                ItemsSource = options,
                SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, field.Value, StringComparison.Ordinal)),
                IsEnabled = !field.IsReadOnly,
                MinWidth = 180
            };
            DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);
            comboBox.ItemTemplate = new FuncDataTemplate<DialogFieldOptionDisplayItem>((option, _) =>
                DesktopShellTheme.CreateComboBoxOptionText(option?.Label ?? string.Empty));
            if (!field.IsReadOnly)
            {
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (_suppressDialogUpdates)
                    {
                        return;
                    }

                    if (comboBox.SelectedItem is not DialogFieldOptionDisplayItem selectedOption)
                    {
                        return;
                    }

                    if (string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    DialogFieldValueChanged?.Invoke(
                        this,
                        new DialogFieldValueChangedEventArgs(field.Id, selectedOption.Value));
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
            else if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Grid, StringComparison.Ordinal))
            {
                visualControl = CreateGridPanel(field.Value);
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
                TextWrapping = TextWrapping.Wrap
            };
            if (string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal))
            {
                textBlock.FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace");
            }

            Border panel = new()
            {
                BorderThickness = new Thickness(1),
                BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
                Padding = new Thickness(6, 4),
                MinHeight = string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.List, StringComparison.Ordinal)
                    || string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal)
                    ? 160
                    : 120,
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
                ? string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Detail, StringComparison.Ordinal) ? 140 : 120
                : 32
        };
        ApplyTextBoxAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
        if (!field.IsReadOnly)
        {
            textBox.TextChanged += (_, _) =>
            {
                if (_suppressDialogUpdates)
                {
                    return;
                }

                string nextValue = textBox.Text ?? string.Empty;
                if (string.Equals(nextValue, field.Value, StringComparison.Ordinal))
                {
                    return;
                }

                DialogFieldValueChanged?.Invoke(
                    this,
                    new DialogFieldValueChangedEventArgs(field.Id, nextValue));
            };
        }

        return textBox;
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

    private static DialogFieldDisplayItem FindRequiredField(IReadOnlyList<DialogFieldDisplayItem> fields, string fieldId)
    {
        return fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Dialog field '{fieldId}' was not available.");
    }

    private static DialogFieldDisplayItem? FindOptionalField(IReadOnlyList<DialogFieldDisplayItem> fields, string fieldId)
    {
        return fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal));
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
                BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
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
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
            MinHeight = 136,
            Child = previewControl
        });

        if (lines.Length > 1)
        {
            panel.Children.Add(new TextBlock
            {
                Text = string.Join(Environment.NewLine, lines.Skip(1)),
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false
            });
        }

        return panel;
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

    private static Control CreateGridPanel(string value)
    {
        StackPanel rows = new()
        {
            Spacing = 3
        };

        foreach ((string key, string data) in ParseGridRows(value))
        {
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
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
            Padding = new Thickness(6, 4),
            Child = rows
        };
    }

    private static Control CreateSnippetPanel(string value)
    {
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
            Padding = new Thickness(6, 4),
            Child = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control CreateStructuredTextPanel(string value, bool useMonospace, double minHeight)
    {
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
            Padding = new Thickness(6, 4),
            MinHeight = minHeight,
            Child = new TextBlock
            {
                Text = value,
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

    private static bool ParseCheckbox(string value)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static IBrush ResolveThemeBrush(string resourceKey, string fallbackHex)
    {
        if (global::Avalonia.Application.Current?.TryFindResource(resourceKey, out object? resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}

public sealed record CommandPaletteItem(string Id, string Label, string Group, bool Enabled)
{
    public override string ToString()
    {
        return $"{Label} [{Group}] {(Enabled ? "enabled" : "disabled")}";
    }
}

public sealed record CommandDialogPaneState(
    CommandPaletteItem[] Commands,
    string? SelectedCommandId,
    string? ActiveDialogId,
    string? DialogTitle,
    string? DialogMessage,
    string? DialogTrustReceipt,
    DialogFieldDisplayItem[] Fields,
    DialogActionDisplayItem[] Actions);

public sealed record DialogFieldDisplayItem(
    string Id,
    string Label,
    string Value,
    string Placeholder,
    bool IsMultiline,
    bool IsReadOnly,
    string InputType,
    IReadOnlyList<DialogFieldOptionDisplayItem>? Options = null,
    string VisualKind = DesktopDialogFieldVisualKinds.Default,
    string LayoutSlot = DesktopDialogFieldLayoutSlots.Full)
{
    public string AccessibleName => DesktopDialogAccessibility.BuildFieldAccessibleName(Label);
    public string ToolTip => DesktopDialogAccessibility.BuildFieldToolTip(Label, Placeholder, Value);
    public string HelpText => DesktopDialogAccessibility.BuildFieldHelpText(
        Label,
        Placeholder,
        Value,
        InputType,
        IsReadOnly,
        IsMultiline,
        VisualKind);

    public override string ToString()
    {
        return $"{Label}: {Value}";
    }
}

public sealed record DialogFieldOptionDisplayItem(
    string Value,
    string Label);

public sealed record DialogActionDisplayItem(string Id, string Label, bool IsPrimary)
{
    public string AccessibleName => DesktopDialogAccessibility.BuildActionAccessibleName(Label);
    public string ToolTip => DesktopDialogAccessibility.BuildActionToolTip(Label);
    public string HelpText => DesktopDialogAccessibility.BuildActionHelpText(Label, IsPrimary);

    public override string ToString()
    {
        return $"{Label} ({Id}){(IsPrimary ? " *" : string.Empty)}";
    }
}

public sealed record DialogFieldValueChangedEventArgs(string FieldId, string Value);

public sealed record SelectionCandidateItem(
    string Value,
    string DisplayText,
    bool IsSelected);
