using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

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
        RebuildDialogFields(fields.ToArray());
        RebuildDialogActions(actions.ToArray());
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

        foreach (DialogFieldDisplayItem field in fields)
        {
            StackPanel row = new()
            {
                Spacing = 4
            };
            row.Children.Add(new TextBlock
            {
                Text = field.Label,
                FontWeight = FontWeight.SemiBold
            });
            row.Children.Add(CreateFieldControl(field));
            DialogFieldsHost.Children.Add(row);
        }

        _suppressDialogUpdates = false;
    }

    private void RebuildDialogActions(DialogActionDisplayItem[] actions)
    {
        DialogActionsHost.Children.Clear();

        foreach (DialogActionDisplayItem action in actions)
        {
            Button button = new()
            {
                Content = action.Label,
                Tag = action.Id,
                MinWidth = 96,
                Classes = { "shell-action", action.IsPrimary ? "primary" : "quiet" }
            };
            button.Click += DialogActionButton_OnClick;
            DialogActionsHost.Children.Add(button);
        }
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

            return checkBox;
        }

        TextBox textBox = new()
        {
            Text = field.Value,
            Watermark = field.Placeholder,
            IsReadOnly = field.IsReadOnly,
            AcceptsReturn = field.IsMultiline,
            TextWrapping = field.IsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = field.IsMultiline ? 120 : 32
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

        return textBox;
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
    string InputType)
{
    public override string ToString()
    {
        return $"{Label}: {Value}";
    }
}

public sealed record DialogActionDisplayItem(string Id, string Label, bool IsPrimary)
{
    public override string ToString()
    {
        return $"{Label} ({Id}){(IsPrimary ? " *" : string.Empty)}";
    }
}

public sealed record DialogFieldValueChangedEventArgs(string FieldId, string Value);
