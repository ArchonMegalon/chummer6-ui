using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        foreach (DesktopDialogField field in fields)
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
            _dialogFieldsPanel.Children.Add(row);
        }
    }

    private Control CreateFieldControl(DesktopDialogField field)
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
                checkBox.IsCheckedChanged += (_, _) => QueueDialogFieldUpdate(field.Id, checkBox.IsChecked == true ? "true" : "false");
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
            MinHeight = field.IsMultiline ? 120 : 30
        };
        if (!field.IsReadOnly)
        {
            textBox.TextChanged += (_, _) => QueueDialogFieldUpdate(field.Id, textBox.Text ?? string.Empty);
        }

        return textBox;
    }

    private void BuildActions(IReadOnlyList<DesktopDialogAction> actions)
    {
        _dialogActionsPanel.Children.Clear();
        foreach (DesktopDialogAction action in actions)
        {
            Button button = new()
            {
                Content = action.Label,
                MinWidth = 88
            };
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
            .SelectMany(row => row.GetVisualDescendants())
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
