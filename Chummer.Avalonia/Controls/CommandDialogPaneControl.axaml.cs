using Avalonia.Controls;

namespace Chummer.Avalonia.Controls;

public partial class CommandDialogPaneControl : UserControl
{
    private bool _suppressCommandSelectionEvent;
    private bool _suppressDialogActionSelectionEvent;

    public CommandDialogPaneControl()
    {
        InitializeComponent();
        CommandsList.SelectionChanged += CommandsList_OnSelectionChanged;
        DialogActionsList.SelectionChanged += DialogActionsList_OnSelectionChanged;
    }

    public event EventHandler<string>? CommandSelected;
    public event EventHandler<string>? DialogActionSelected;

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
        DialogFieldsList.ItemsSource = fields.ToArray();
        _suppressDialogActionSelectionEvent = true;
        DialogActionsList.ItemsSource = actions.ToArray();
        DialogActionsList.SelectedItem = null;
        _suppressDialogActionSelectionEvent = false;
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

    private void DialogActionsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDialogActionSelectionEvent)
            return;

        if (DialogActionsList.SelectedItem is not DialogActionDisplayItem action)
            return;

        DialogActionSelected?.Invoke(this, action.Id);
        ClearSelection(DialogActionsList, ref _suppressDialogActionSelectionEvent);
    }

    private static void ClearSelection(ListBox listBox, ref bool suppressSelectionEvent)
    {
        suppressSelectionEvent = true;
        listBox.SelectedItem = null;
        suppressSelectionEvent = false;
    }
}

public sealed record CommandPaletteItem(string Id, string Group, bool Enabled)
{
    public override string ToString()
    {
        return $"{Id} [{Group}] {(Enabled ? "enabled" : "disabled")}";
    }
}

public sealed record CommandDialogPaneState(
    CommandPaletteItem[] Commands,
    string? SelectedCommandId,
    string? DialogTitle,
    string? DialogMessage,
    DialogFieldDisplayItem[] Fields,
    DialogActionDisplayItem[] Actions);

public sealed record DialogFieldDisplayItem(string Id, string Label, string Value)
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
