using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia;

public partial class DesktopDialogWindow : Window
{
    private static readonly string UiKitAccessibilityAdapterMarker = AccessibilityPrimitiveBoundary.RootClass;
    private CharacterOverviewViewModelAdapter? _adapter;
    private readonly TextBlock _dialogTitleText;
    private readonly TextBlock _dialogMessageText;
    private readonly StackPanel _dialogFieldsPanel;
    private readonly StackPanel _dialogActionsPanel;
    private bool _suppressCloseNotification;

    public DesktopDialogWindow()
    {
        InitializeComponent();

        _dialogTitleText = this.FindControl<TextBlock>("DialogTitleText")!;
        _dialogMessageText = this.FindControl<TextBlock>("DialogMessageText")!;
        _dialogFieldsPanel = this.FindControl<StackPanel>("DialogFieldsPanel")!;
        _dialogActionsPanel = this.FindControl<StackPanel>("DialogActionsPanel")!;
        Closing += OnClosing;
        Opened += OnOpened;
    }

    public DesktopDialogWindow(CharacterOverviewViewModelAdapter adapter)
        : this()
    {
        _adapter = adapter;
    }

    public void AttachAdapter(CharacterOverviewViewModelAdapter adapter)
    {
        _adapter = adapter;
    }

    public void BindDialog(DesktopDialogState dialog)
    {
        Title = dialog.Title;
        _dialogTitleText.Text = dialog.Title;
        _dialogMessageText.Text = dialog.Message ?? string.Empty;
        _dialogMessageText.IsVisible = !string.IsNullOrWhiteSpace(dialog.Message);

        BuildFields(dialog.Fields);
        BuildActions(dialog.Actions);
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
        DesktopDialogField[] visibleFields = fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
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
            ColumnDefinitions = new ColumnDefinitions("156,*"),
            RowDefinitions = field.IsMultiline ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto"),
            ColumnSpacing = 8,
            RowSpacing = 4
        };

        TextBlock label = new()
        {
            Text = field.Label,
            VerticalAlignment = field.IsMultiline ? global::Avalonia.Layout.VerticalAlignment.Top : global::Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        };
        ApplyAccessibility(label, field.AccessibleName, field.ToolTip, field.HelpText);
        row.Children.Add(label);

        Control fieldControl = CreateFieldControl(field);
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

    private Control CreateFieldControl(DesktopDialogField field)
    {
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
        StackPanel panel = new()
        {
            Spacing = 4
        };
        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = Brushes.Transparent,
            MinHeight = 136,
            Child = new TextBlock
            {
                Text = lines.Length > 0 ? lines[0] : "Mugshot Preview",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            }
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

    private static void ApplyAccessibility(Control control, string accessibleName, string toolTip, string helpText)
    {
        AutomationProperties.SetName(control, accessibleName);
        AutomationProperties.SetHelpText(control, helpText);
        ToolTip.SetTip(control, toolTip);
    }

    private void BuildActions(IReadOnlyList<DesktopDialogAction> actions)
    {
        _dialogActionsPanel.Children.Clear();
        foreach (DesktopDialogAction action in actions)
        {
            Button button = new()
            {
                Content = action.Label,
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

    private void OnOpened(object? sender, EventArgs e)
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
}

internal static class DesktopDialogWindowExtensions
{
    public static T Also<T>(this T instance, Action<T> configure)
    {
        configure(instance);
        return instance;
    }
}
