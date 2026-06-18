using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

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
        textBox.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE");
        textBox.Foreground = ResolveThemeBrush("ChummerShellForegroundBrush", "#111827");
        textBox.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF");
        textBox.CaretBrush = ResolveThemeBrush("ChummerShellForegroundBrush", "#111827");
        textBox.MinHeight = Math.Max(textBox.MinHeight, 30d);
        textBox.Padding = new Thickness(8, 4);
    }

    public static void ApplyShellComboBoxTheme(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        comboBox.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE");
        comboBox.Foreground = ResolveThemeBrush("ChummerShellForegroundBrush", "#111827");
        comboBox.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF");
        comboBox.MinHeight = Math.Max(comboBox.MinHeight, 30d);
        comboBox.Padding = new Thickness(8, 4);
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
        => new()
        {
            Text = text,
            TextWrapping = wrapping
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

        ToolTip.SetTip(body, title);

        StackPanel content = new() { Spacing = spacing };
        if (includeHeading)
        {
            content.Children.Add(new TextBlock
            {
                Text = title,
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

        Button button = new()
        {
            Content = label,
            MinWidth = minWidth,
            Padding = new Thickness(10, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("shell-action");
        ToolTip.SetTip(button, label);

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
