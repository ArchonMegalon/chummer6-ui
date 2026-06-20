using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class DesktopShellTheme
{
    public static IBrush ResolveThemeBrush(string resourceKey, string fallbackHex)
        => App.Current?.TryFindResource(resourceKey, out object? resource) == true && resource is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallbackHex));

    public static Border CreateWindowSurface(Control child, double padding = 16)
        => new()
        {
            Background = ResolveThemeBrush("ChummerShellWindowBackgroundBrush", "#E3EAF3"),
            Padding = new Thickness(padding),
            Child = child
        };

    public static Border CreateUtilityPanel(Control child, double padding = 10, double cornerRadius = 8)
        => new()
        {
            Background = ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F2F5FA"),
            BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cornerRadius),
            Padding = new Thickness(padding),
            Child = child
        };

    public static void ApplyPrimaryButton(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!button.Classes.Contains("shell-action"))
        {
            button.Classes.Add("shell-action");
        }

        button.Classes.Add("primary");
    }

    public static void ApplyShellTextInputTheme(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        if (!textBox.Classes.Contains("shell-input"))
        {
            textBox.Classes.Add("shell-input");
        }

        textBox.Background = ResolveThemeBrush("TextControlBackground", "#FFFFFF");
        textBox.Foreground = ResolveThemeBrush("TextControlForeground", "#111111");
        textBox.BorderBrush = ResolveThemeBrush("TextControlBorderBrush", "#B5C0CF");
        textBox.CaretBrush = ResolveThemeBrush("TextControlCaretBrush", "#111111");
        textBox.SelectionBrush = ResolveThemeBrush("ChummerShellSelectionBrush", "#2C5FB8");
        textBox.SelectionForegroundBrush = ResolveThemeBrush("TextControlSelectionForeground", "#FFFFFF");
        textBox.MinHeight = Math.Max(textBox.MinHeight, 30d);
        textBox.Padding = new Thickness(8, 4);
        ToolTip.SetTip(textBox, null);
    }

    public static void ApplyShellComboBoxTheme(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        if (!comboBox.Classes.Contains("shell-combo"))
        {
            comboBox.Classes.Add("shell-combo");
        }

        comboBox.Background = ResolveThemeBrush("ComboBoxBackground", "#FBFCFE");
        comboBox.Foreground = ResolveThemeBrush("ComboBoxForeground", "#111827");
        comboBox.BorderBrush = ResolveThemeBrush("ComboBoxBorderBrush", "#B5C0CF");
        comboBox.MinHeight = Math.Max(comboBox.MinHeight, 30d);
        comboBox.Padding = new Thickness(8, 4);
    }

    public static void ApplyShellNumericUpDownTheme(NumericUpDown numericUpDown)
    {
        ArgumentNullException.ThrowIfNull(numericUpDown);
        if (!numericUpDown.Classes.Contains("shell-numeric"))
        {
            numericUpDown.Classes.Add("shell-numeric");
        }

        numericUpDown.MinHeight = Math.Max(numericUpDown.MinHeight, 30d);
    }

    public static void ApplyShellListBoxTheme(ListBox listBox)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        listBox.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE");
        listBox.Foreground = ResolveThemeBrush("ChummerShellForegroundBrush", "#111827");
        listBox.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF");
        listBox.BorderThickness = new Thickness(1);
        listBox.Padding = new Thickness(2);
    }

    public static void ApplyShellReadOnlyPanelTheme(Border panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        panel.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE");
        panel.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF");
    }

    public static TextBlock CreateComboBoxOptionText(string text, TextWrapping wrapping = TextWrapping.NoWrap)
        => CreateOptionText(text, wrapping);

    public static TextBlock CreateOptionText(string text, TextWrapping wrapping = TextWrapping.NoWrap)
        => new()
        {
            Text = text,
            TextWrapping = wrapping,
            Classes = { "shell-option-label" }
        };

    public static TextBlock CreateOptionMetaText(string text, TextWrapping wrapping = TextWrapping.Wrap)
        => new()
        {
            Text = text,
            TextWrapping = wrapping,
            Classes = { "shell-option-meta" }
        };

    public static Border CreateSection(
        string title,
        Control body,
        Control? actionContent,
        double padding = 10,
        double cornerRadius = 4,
        bool includeHeading = true,
        double spacing = 8)
    {
        ArgumentNullException.ThrowIfNull(body);

        string visibleTitle = PlayerFacingCopyHumanizer.Clean(title);
        ToolTip.SetTip(body, visibleTitle);

        StackPanel content = new() { Spacing = spacing };
        if (includeHeading)
        {
            content.Children.Add(new TextBlock
            {
                Text = visibleTitle,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
        }

        content.Children.Add(body);

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return CreateUtilityPanel(content, padding, cornerRadius);
    }

    public static WrapPanel CreateWrapActionRow(IReadOnlyList<Button> actions, Thickness? itemMargin = null)
    {
        WrapPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = 32,
            ItemWidth = double.NaN
        };
        AppendButtons(actionRow, actions, itemMargin ?? new Thickness(0, 0, 6, 6));
        return actionRow;
    }

    public static StackPanel CreateStackActionRow(IReadOnlyList<Button> actions, double spacing = 6)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing
        };
        AppendButtons(actionRow, actions, null);
        return actionRow;
    }

    public static void ResetActionRow(Panel actionRow, IReadOnlyList<Button> actions, Thickness? itemMargin = null)
    {
        ArgumentNullException.ThrowIfNull(actionRow);
        actionRow.Children.Clear();
        AppendButtons(actionRow, actions, itemMargin);
    }

    public static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false, double minWidth = 104)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary,
            minWidth);

    public static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false, double minWidth = 104)
    {
        ArgumentNullException.ThrowIfNull(action);

        string resolvedLabel = PlayerFacingCopyHumanizer.Clean(ResolveCloseActionLabel(label, closeWindow));
        Button button = new()
        {
            Content = resolvedLabel,
            MinWidth = minWidth,
            Padding = new Thickness(10, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("shell-action");
        ToolTip.SetTip(button, resolvedLabel);

        if (isPrimary)
        {
            button.FontWeight = FontWeight.SemiBold;
            ApplyPrimaryButton(button);
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow && TopLevel.GetTopLevel(button) is Window window)
            {
                window.Close();
            }
        };

        return button;
    }

    private static string ResolveCloseActionLabel(string label, bool closeWindow)
        => closeWindow && string.Equals(label, "Close", StringComparison.Ordinal)
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.action.close")
            : label;

    private static void AppendButtons(Panel panel, IReadOnlyList<Button> actions, Thickness? itemMargin)
    {
        foreach (Button action in actions)
        {
            if (itemMargin is not null)
            {
                action.Margin = itemMargin.Value;
            }

            panel.Children.Add(action);
        }
    }
}
