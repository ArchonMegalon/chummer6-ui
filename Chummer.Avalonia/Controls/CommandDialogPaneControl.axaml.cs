using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chummer.Presentation.Overview;
using System.IO;

namespace Chummer.Avalonia.Controls;

public partial class CommandDialogPaneControl : UserControl
{
    private bool _suppressCommandSelectionEvent;
    private bool _suppressDialogUpdates;

    public CommandDialogPaneControl()
    {
        InitializeComponent();
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
        IEnumerable<DialogFieldDisplayItem> fields,
        IEnumerable<DialogActionDisplayItem> actions)
    {
        DialogTitleText.Text = string.IsNullOrWhiteSpace(title) ? "(none)" : title;
        DialogMessageText.Text = string.IsNullOrWhiteSpace(message) ? "(none)" : message;
        DialogMessageBorder.IsVisible = !string.IsNullOrWhiteSpace(message);
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
        DialogFieldDisplayItem[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
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

    private void RebuildDialogActions(DialogActionDisplayItem[] actions)
    {
        DialogActionsHost.Children.Clear();
        DialogActionsBorder.IsVisible = actions.Length > 0;

        foreach (DialogActionDisplayItem action in actions)
        {
            Button button = new()
            {
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

    private Control CreateFieldPane(DialogFieldDisplayItem field)
    {
        StackPanel row = new()
        {
            Spacing = 4
        };
        TextBlock label = new()
        {
            Text = field.Label,
            FontWeight = FontWeight.SemiBold
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(label);

        Control fieldControl = CreateFieldControl(field);
        ApplyAccessibility(fieldControl, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(fieldControl);
        return row;
    }

    private Control CreateFieldControl(DialogFieldDisplayItem field)
    {
        if (string.Equals(field.InputType, "checkbox", StringComparison.Ordinal))
        {
            CheckBox checkBox = new()
            {
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
                    : 120,
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
                ? string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Detail, StringComparison.Ordinal) ? 140 : 120
                : 32
        };
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

        ApplyAccessibility(textBox, field.AccessibleName, field.ToolTip, field.HelpText);
        return textBox;
    }

    private static void ApplyAccessibility(Control control, string accessibleName, string toolTip, string helpText)
    {
        AutomationProperties.SetName(control, accessibleName);
        AutomationProperties.SetHelpText(control, helpText);
        ToolTip.SetTip(control, toolTip);
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
                previewControl = new TextBlock
                {
                    Text = lines.Length > 0 ? lines[0] : "Mugshot Preview",
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
                };
            }
        }
        else
        {
            previewControl = new TextBlock
            {
                Text = lines.Length > 0 ? lines[0] : "Mugshot Preview",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
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
    string? DialogTitle,
    string? DialogMessage,
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
