using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;
using System.IO;
using System.Text.Json;

namespace Chummer.Avalonia;

public partial class DesktopDialogWindow : Window
{
    private static readonly string UiKitAccessibilityAdapterMarker = AccessibilityPrimitiveBoundary.RootClass;
    private CharacterOverviewViewModelAdapter? _adapter;
    private readonly TextBlock _dialogTitleText;
    private readonly TextBlock _dialogMessageText;
    private readonly StackPanel _dialogFieldsPanel;
    private readonly Border _dialogActionsBorder;
    private readonly StackPanel _dialogActionsPanel;
    private bool _suppressCloseNotification;

    public DesktopDialogWindow()
    {
        InitializeComponent();

        _dialogTitleText = this.FindControl<TextBlock>("DialogTitleText")!;
        _dialogMessageText = this.FindControl<TextBlock>("DialogMessageText")!;
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

    public void AttachAdapter(CharacterOverviewViewModelAdapter adapter)
    {
        _adapter = adapter;
    }

    public void BindDialog(DesktopDialogState dialog)
    {
        BoundDialogId = dialog.Id;
        ApplyDialogSizing(dialog.Id);
        Title = dialog.Title;
        _dialogTitleText.Text = dialog.Title;
        string visibleMessage = SuppressDialogBanner(dialog.Id) ? string.Empty : dialog.Message ?? string.Empty;
        _dialogMessageText.Text = visibleMessage;
        _dialogMessageText.IsVisible = !string.IsNullOrWhiteSpace(visibleMessage);

        BuildFields(dialog.Fields);
        BuildActions(dialog.Actions);
        RefreshDialogVisuals();
        if (IsVisible)
        {
            FocusPreferredControl();
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
        _dialogFieldsPanel.Children.Clear();
        if (TryBuildLegacyParityDialog(fields))
        {
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
        DesktopDialogField languageField = FindRequiredField(fields, "globalLanguage");
        DesktopDialogField sheetLanguageField = FindRequiredField(fields, "globalSheetLanguage");
        DesktopDialogField characterPriorityField = FindRequiredField(fields, "globalCharacterPriority");
        DesktopDialogField updateCheckField = FindRequiredField(fields, "globalCheckForUpdates");
        DesktopDialogField preferNightlyField = FindRequiredField(fields, "globalPreferNightlyBuilds");
        DesktopDialogField rosterPathField = FindRequiredField(fields, "globalCharacterRosterPath");
        DesktopDialogField hideMasterIndexField = FindRequiredField(fields, "globalHideMasterIndex");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        shell.Children.Add(CreateLegacyFieldGroup(
            "Global Options",
            CreateSplitFieldRow(languageField, sheetLanguageField),
            CreateSplitFieldRow(characterPriorityField, updateCheckField),
            CreateSplitFieldRow(preferNightlyField, hideMasterIndexField),
            CreateLegacyRosterPathRow(rosterPathField)));

        return shell;
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
            Watermark = field.Placeholder,
            IsReadOnly = field.IsReadOnly
        };
        ApplyAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
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
        DesktopDialogField rulesetField = FindRequiredField(fields, "newCharacterRulesetId");
        DesktopDialogField buildMethodField = FindRequiredField(fields, "newCharacterBuildMethod");

        StackPanel shell = new()
        {
            Spacing = 12
        };

        Grid settingRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };
        TextBlock settingLabel = new()
        {
            Text = "Use Setting:",
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
            Content = "Modify...",
            MinWidth = 88
        };
        ApplyAccessibility(
            modifyButton,
            "Modify character settings",
            "Open Character Settings.",
            "Open the legacy Character Settings dialog before creating a new character.");
        modifyButton.Click += async (_, _) =>
        {
            await ExecuteSafeAsync(
                () => _adapter!.ExecuteCommandAsync("character_settings", CancellationToken.None),
                "execute command 'character_settings'");
        };
        Grid.SetColumn(modifyButton, 2);
        settingRow.Children.Add(modifyButton);

        Grid rulesetRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("156,*"),
            ColumnSpacing = 8
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

        Grid summaryRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            ColumnSpacing = 8
        };
        summaryRow.Children.Add(new TextBlock
        {
            Text = "Build Method",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });
        TextBlock buildMethodSummary = new()
        {
            Text = buildMethodCombo.SelectedItem is DesktopDialogFieldOption buildOption
                ? buildOption.Label
                : buildMethodField.Value,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(buildMethodSummary, 1);
        summaryRow.Children.Add(buildMethodSummary);

        TextBlock rulesetSummaryLabel = new()
        {
            Text = "Ruleset",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(rulesetSummaryLabel, 2);
        summaryRow.Children.Add(rulesetSummaryLabel);

        TextBlock rulesetSummary = new()
        {
            Text = rulesetCombo.SelectedItem is DesktopDialogFieldOption rulesetOption
                ? rulesetOption.Label
                : rulesetField.Value.ToUpperInvariant(),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(rulesetSummary, 3);
        summaryRow.Children.Add(rulesetSummary);

        shell.Children.Add(CreateLegacyFieldGroup(
            "Select Build Method",
            settingRow,
            rulesetRow,
            summaryRow));

        return shell;
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

        TextBox diceCountTextBox = BuildLegacyInlineTextBox(diceCountField, width: 56);
        Grid.SetColumn(diceCountTextBox, 1);
        topBar.Children.Add(diceCountTextBox);

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
        resultsList.ItemTemplate = new FuncDataTemplate<string>((line, _) =>
            new TextBlock
            {
                Text = line ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            });
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
        AddLabeledValueRow(rightPane, 5, "Threshold:", BuildLegacyInlineTextBox(thresholdField, width: 64));
        AddLabeledValueRow(rightPane, 6, "Gremlins:", BuildLegacyInlineTextBox(gremlinsField, width: 64));

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
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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
        foreach (Control child in children)
        {
            body.Children.Add(child);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold
                    },
                    body
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
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
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
            Watermark = string.IsNullOrWhiteSpace(searchField.Placeholder) ? "Search" : searchField.Placeholder
        };
        ApplyAccessibility(searchBox, searchField.AccessibleName, searchField.ToolTip, searchField.HelpText);
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
            sourceValueText.Foreground = Brushes.DodgerBlue;
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
                IsVisible = sourceAvailable
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

        TextBox notesBox = new()
        {
            Text = notesField.Value,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 280,
            IsVisible = notesAvailable
        };
        Grid.SetRow(notesBox, 2);
        right.Children.Add(notesBox);

        Grid settingsRow = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };
        settingsRow.Children.Add(new TextBlock
        {
            Text = "Use Setting:",
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
        ApplyAccessibility(settingsCombo, settingsField.AccessibleName, settingsField.ToolTip, settingsField.HelpText);
        settingsCombo.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            new TextBlock
            {
                Text = option?.Label ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            });
        Grid.SetColumn(settingsCombo, 1);
        settingsRow.Children.Add(settingsCombo);
        Button modifySettingsButton = new()
        {
            Name = "masterIndexSettingsModifyButton",
            Content = "Modify...",
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
            IsVisible = !string.IsNullOrWhiteSpace(statusField.Value)
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
                IsEnabled = !field.IsReadOnly
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
            ColumnDefinitions = new ColumnDefinitions("156,*"),
            RowDefinitions = field.IsMultiline ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto"),
            ColumnSpacing = 8,
            RowSpacing = 4
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
        Grid.SetColumn(fieldControl, field.IsMultiline ? 0 : 1);
        if (field.IsMultiline)
        {
            Grid.SetColumnSpan(fieldControl, 2);
            Grid.SetRow(fieldControl, 1);
        }

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
            comboBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
                new TextBlock
                {
                    Text = option?.Label ?? string.Empty
                });
            if (!field.IsReadOnly)
            {
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (comboBox.SelectedItem is not DesktopDialogFieldOption selectedOption)
                    {
                        return;
                    }

                    if (string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                    {
                        return;
                    }

                    QueueDialogFieldUpdate(field.Id, selectedOption.Value);
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
                BorderBrush = Brushes.Gray,
                Background = Brushes.Transparent,
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
            Watermark = field.Placeholder,
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

        ApplyAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
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
                BorderBrush = Brushes.Gray,
                Background = Brushes.Transparent,
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
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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
            Grid.SetColumn(keyText, 0);
            Grid.SetColumn(valueText, 1);
            row.Children.Add(keyText);
            row.Children.Add(valueText);
            rows.Children.Add(row);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
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

    private static void AddLegacyRosterSummaryRow(Grid grid, int rowIndex, string label, string value)
    {
        TextBlock labelText = new()
        {
            Text = label,
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
            Content = string.IsNullOrWhiteSpace(textOverride) ? field.Label : textOverride,
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
            Watermark = field.Placeholder,
            IsReadOnly = field.IsReadOnly,
            Width = width
        };
        ApplyAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
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
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetRow(labelText, rowIndex);
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

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
            Button button = new()
            {
                Name = DesktopDialogAccessibility.BuildActionName(action.Id),
                Content = action.Label,
                Tag = action.Id,
                MinWidth = 82,
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
        comboBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            new TextBlock
            {
                Text = option?.Label ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            });
        if (!field.IsReadOnly)
        {
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is DesktopDialogFieldOption selectedOption
                    && !string.Equals(selectedOption.Value, field.Value, StringComparison.Ordinal))
                {
                    QueueDialogFieldUpdate(field.Id, selectedOption.Value);
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
        return comboBox;
    }

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
        listBox.ItemTemplate = new FuncDataTemplate<DesktopDialogFieldOption>((option, _) =>
            new TextBlock
            {
                Text = option?.Label ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            });
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
        return new TextBox
        {
            Text = value,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
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
            "dialog.master_index" => (980d, 640d, 760d, 440d),
            "dialog.character_roster" => (900d, 620d, 700d, 420d),
            "dialog.global_settings" => (920d, 600d, 700d, 420d),
            _ => (860d, 560d, 640d, 360d)
        };

        Width = size.width;
        Height = size.height;
        MinWidth = size.minWidth;
        MinHeight = size.minHeight;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        FocusPreferredControl();
    }

    private async void QueueDialogFieldUpdate(string fieldId, string value)
    {
        if (_adapter is null)
            return;

        await ExecuteSafeAsync(
            () => _adapter.UpdateDialogFieldAsync(fieldId, value, CancellationToken.None),
            $"update field '{fieldId}'");
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

    private void FocusPreferredControl()
    {
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
}

internal static class DesktopDialogWindowExtensions
{
    public static T Also<T>(this T instance, Action<T> configure)
    {
        configure(instance);
        return instance;
    }
}
