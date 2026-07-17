using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    public static IBrush ResolveWindowBackgroundBrush()
        => ResolveThemeBrush("ChummerShellWindowBackgroundBrush", "#050B16");

    public static IBrush ResolveHardPanelBrush()
        => ResolveThemeBrush("ChummerShellHardPanelBrush", "#111827");

    public static IBrush ResolveForegroundBrush()
        => ResolveThemeBrush("ChummerShellForegroundBrush", "#E5E7EB");

    public static IBrush ResolveMutedForegroundBrush()
        => ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#D8E1EC");

    public static IBrush ResolveTextMutedBrush()
        => ResolveThemeBrush("ChummerShellTextMutedBrush", "#94A3B8");

    public static IBrush ResolveBorderBrush()
        => ResolveThemeBrush("ChummerShellBorderBrush", "#334155");

    public static IBrush ResolveSurfaceBrush()
        => ResolveThemeBrush("ChummerShellSurfaceBrush", "#111827");

    public static IBrush ResolveSurfaceAltBrush()
        => ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#020617");

    public static IBrush ResolveChromeBrush()
        => ResolveThemeBrush("ChummerShellChromeBrush", "#0F172A");

    public static IBrush ResolveChromeSubtleBrush()
        => ResolveThemeBrush("ChummerShellChromeSubtleBrush", "#111827");

    public static IBrush ResolvePanelMutedBrush()
        => ResolveThemeBrush("ChummerShellPanelMutedBrush", "#111827");

    public static IBrush ResolveSelectionToolbarBrush()
        => ResolveThemeBrush("ChummerShellSelectionToolbarBrush", "#0B1220");

    public static IBrush ResolveSelectionPanelBrush()
        => ResolveThemeBrush("ChummerShellSelectionPanelBrush", "#111827");

    public static IBrush ResolveSelectionInsetBrush()
        => ResolveThemeBrush("ChummerShellSelectionInsetBrush", "#0F172A");

    public static IBrush ResolveChromeAccentBrush()
        => ResolveThemeBrush("ChummerShellChromeAccentBrush", "#172554");

    public static IBrush ResolveActiveMenuBorderBrush()
        => ResolveThemeBrush("ChummerShellActiveMenuBorderBrush", "#90C39A");

    public static IBrush ResolveInfoBrush()
        => ResolveThemeBrush("ChummerShellInfoBrush", "#60A5FA");

    public static Border CreateWindowSurface(Control child, double padding = 16)
        => new()
        {
            Background = ResolveWindowBackgroundBrush(),
            Padding = new Thickness(padding),
            Child = child
        };

    public static Border CreateUtilityPanel(Control child, double padding = 10, double cornerRadius = 8)
        => new()
        {
            Background = ResolveHardPanelBrush(),
            BorderBrush = ResolveBorderBrush(),
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

        ClearInputBrushes(textBox);
        ApplyTextControlResourceOverrides(textBox);
        ApplyInputBrushes(textBox);
        textBox.MinHeight = Math.Max(textBox.MinHeight, 30d);
        textBox.Padding = new Thickness(8, 4);
        ToolTip.SetTip(textBox, null);
    }

    public static void ApplyShellReadOnlyTextBoxTheme(TextBox textBox)
    {
        ApplyShellTextInputTheme(textBox);
        if (!textBox.Classes.Contains("shell-readonly-input"))
        {
            textBox.Classes.Add("shell-readonly-input");
        }

        ApplyReadOnlyTextControlResourceOverrides(textBox);
        ApplyReadOnlyInputBrushes(textBox);
    }

    public static void ApplyShellComboBoxTheme(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        if (!comboBox.Classes.Contains("shell-combo"))
        {
            comboBox.Classes.Add("shell-combo");
        }

        ClearTemplatedBrushes(comboBox);
        ApplyComboBoxResourceOverrides(comboBox);
        ApplyComboBoxBrushes(comboBox);
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

        ClearTemplatedBrushes(numericUpDown);
        ApplyTextControlResourceOverrides(numericUpDown);
        ApplyInputBrushes(numericUpDown);
        numericUpDown.MinHeight = Math.Max(numericUpDown.MinHeight, 30d);
    }

    public static void ApplyShellRadioButtonTheme(RadioButton radioButton)
    {
        ArgumentNullException.ThrowIfNull(radioButton);
        radioButton.Foreground = ResolveForegroundBrush();
        radioButton.Background = ResolveSurfaceBrush();
        radioButton.BorderBrush = ResolveBorderBrush();
        radioButton.Padding = new Thickness(8, 6);
    }

    public static void ApplyShellCheckBoxTheme(CheckBox checkBox)
    {
        ArgumentNullException.ThrowIfNull(checkBox);
        checkBox.Foreground = ResolveForegroundBrush();
        checkBox.Background = ResolveSurfaceBrush();
        checkBox.BorderBrush = ResolveBorderBrush();
        checkBox.Padding = new Thickness(8, 6);
    }

    public static void ApplyShellListBoxTheme(ListBox listBox)
    {
        ArgumentNullException.ThrowIfNull(listBox);
        ClearTemplatedBrushes(listBox);
        ApplySelectableResourceOverrides(listBox);
        ApplyListBrushes(listBox);
        listBox.BorderThickness = new Thickness(1);
        listBox.Padding = new Thickness(2);
    }

    public static void ApplyShellTreeViewTheme(TreeView treeView)
    {
        ArgumentNullException.ThrowIfNull(treeView);
        ClearTemplatedBrushes(treeView);
        ApplySelectableResourceOverrides(treeView);
        ApplyListBrushes(treeView);
        treeView.BorderThickness = new Thickness(1);
        treeView.Padding = new Thickness(2);
    }

    public static void ApplyShellReadOnlyPanelTheme(Border panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        panel.Background = ResolveSurfaceBrush();
        panel.BorderBrush = ResolveBorderBrush();
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

        string visibleTitle = UndetectableHumanizerCopyAdapter.Humanize(title);
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

        string resolvedLabel = UndetectableHumanizerCopyAdapter.Humanize(ResolveCloseActionLabel(label, closeWindow));
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

    private static void ClearInputBrushes(TextBox textBox)
    {
        ClearTemplatedBrushes(textBox);
        textBox.ClearValue(TextBox.CaretBrushProperty);
        textBox.ClearValue(TextBox.SelectionBrushProperty);
        textBox.ClearValue(TextBox.SelectionForegroundBrushProperty);
    }

    private static void ApplyInputBrushes(TemplatedControl control)
    {
        control.Background = ResolveThemeBrush("ChummerShellInputBackgroundBrush", "#162031");
        control.Foreground = ResolveThemeBrush("ChummerShellInputForegroundBrush", "#F8FAFC");
        control.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#334155");

        if (control is TextBox textBox)
        {
            textBox.CaretBrush = ResolveThemeBrush("ChummerShellInputForegroundBrush", "#F8FAFC");
            textBox.SelectionBrush = ResolveThemeBrush("ChummerShellSelectionBrush", "#1D4ED8");
            textBox.SelectionForegroundBrush = ResolveThemeBrush("ChummerShellSelectionForegroundBrush", "#F8FAFC");
        }
    }

    private static void ApplyReadOnlyInputBrushes(TemplatedControl control)
    {
        control.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#111827");
        control.Foreground = ResolveThemeBrush("ChummerShellForegroundBrush", "#E5E7EB");
        control.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#334155");

        if (control is TextBox textBox)
        {
            textBox.CaretBrush = ResolveThemeBrush("ChummerShellForegroundBrush", "#E5E7EB");
            textBox.SelectionBrush = ResolveThemeBrush("ChummerShellSelectionBrush", "#1D4ED8");
            textBox.SelectionForegroundBrush = ResolveThemeBrush("ChummerShellSelectionForegroundBrush", "#F8FAFC");
        }
    }

    private static void ApplyTextControlResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "TextControlBackground", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "TextControlBackgroundPointerOver", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "TextControlBackgroundFocused", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "TextControlBackgroundDisabled", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TextControlForeground", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlForegroundPointerOver", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlForegroundFocused", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
        SetLocalBrushResource(control, "TextControlCaretBrush", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlSelectionForeground", "ChummerShellSelectionForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlBorderBrush", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "TextControlBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "TextControlBorderBrushFocused", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "TextControlBorderBrushDisabled", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "TextControlPlaceholderForeground", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundPointerOver", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundFocused", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
    }

    private static void ApplyReadOnlyTextControlResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "TextControlBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TextControlBackgroundPointerOver", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TextControlBackgroundFocused", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TextControlBackgroundDisabled", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TextControlForeground", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "TextControlForegroundPointerOver", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "TextControlForegroundFocused", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "TextControlForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
        SetLocalBrushResource(control, "TextControlCaretBrush", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "TextControlSelectionForeground", "ChummerShellSelectionForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "TextControlBorderBrush", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "TextControlBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "TextControlBorderBrushFocused", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "TextControlBorderBrushDisabled", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "TextControlPlaceholderForeground", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundPointerOver", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundFocused", "ChummerShellMutedForegroundBrush", "#D8E1EC");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
    }

    private static void ApplyComboBoxBrushes(TemplatedControl control)
    {
        control.Background = ResolveThemeBrush("ChummerShellInputBackgroundBrush", "#162031");
        control.Foreground = ResolveThemeBrush("ChummerShellInputForegroundBrush", "#F8FAFC");
        control.BorderBrush = ResolveThemeBrush("ComboBoxBorderBrush", "#334155");
    }

    private static void ApplyComboBoxResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "ComboBoxBackground", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "ComboBoxBackgroundPointerOver", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "ComboBoxBackgroundPressed", "ChummerShellInputBackgroundBrush", "#162031");
        SetLocalBrushResource(control, "ComboBoxBackgroundDisabled", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ComboBoxForeground", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ComboBoxForegroundPointerOver", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ComboBoxForegroundPressed", "ChummerShellInputForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ComboBoxForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
        SetLocalBrushResource(control, "ComboBoxBorderBrush", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "ComboBoxBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "ComboBoxBorderBrushPressed", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "ComboBoxBorderBrushDisabled", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "ComboBoxDropDownBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ComboBoxDropDownForeground", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ComboBoxDropDownBorderBrush", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "ComboBoxItemBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundPointerOver", "ChummerShellChromeSubtleBrush", "#111827");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundPressed", "ChummerShellChromeBrush", "#0F172A");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundSelected", "ChummerShellSelectionBrush", "#1D4ED8");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundDisabled", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ComboBoxItemForeground", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ComboBoxItemForegroundPointerOver", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ComboBoxItemForegroundPressed", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ComboBoxItemForegroundSelected", "ChummerShellSelectionForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ComboBoxItemForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
    }

    private static void ApplyListBrushes(TemplatedControl control)
    {
        control.Background = ResolveThemeBrush("ChummerShellSurfaceBrush", "#111827");
        control.Foreground = ResolveThemeBrush("ChummerShellForegroundBrush", "#E5E7EB");
        control.BorderBrush = ResolveThemeBrush("ChummerShellBorderBrush", "#334155");
    }

    private static void ApplySelectableResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "ChummerShellSurfaceBrush", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ChummerShellForegroundBrush", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ChummerShellChromeSubtleBrush", "ChummerShellChromeSubtleBrush", "#111827");
        SetLocalBrushResource(control, "ChummerShellSelectionBrush", "ChummerShellSelectionBrush", "#1D4ED8");
        SetLocalBrushResource(control, "ChummerShellSelectionForegroundBrush", "ChummerShellSelectionForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ChummerShellTextMutedBrush", "ChummerShellTextMutedBrush", "#94A3B8");
        SetLocalBrushResource(control, "ChummerShellBorderBrush", "ChummerShellBorderBrush", "#334155");
        SetLocalBrushResource(control, "ChummerShellActiveMenuBorderBrush", "ChummerShellActiveMenuBorderBrush", "#90C39A");
        SetLocalBrushResource(control, "ListBoxBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ListBoxForeground", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ListBoxItemBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "ListBoxItemBackgroundPointerOver", "ChummerShellChromeSubtleBrush", "#111827");
        SetLocalBrushResource(control, "ListBoxItemBackgroundSelected", "ChummerShellSelectionBrush", "#1D4ED8");
        SetLocalBrushResource(control, "ListBoxItemBackgroundDisabled", "ChummerShellSurfaceAltBrush", "#020617");
        SetLocalBrushResource(control, "ListBoxItemForeground", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ListBoxItemForegroundPointerOver", "ChummerShellForegroundBrush", "#E5E7EB");
        SetLocalBrushResource(control, "ListBoxItemForegroundSelected", "ChummerShellSelectionForegroundBrush", "#F8FAFC");
        SetLocalBrushResource(control, "ListBoxItemForegroundDisabled", "ChummerShellTextMutedBrush", "#94A3B8");
        SetLocalBrushResource(control, "TreeViewBackground", "ChummerShellSurfaceBrush", "#111827");
        SetLocalBrushResource(control, "TreeViewForeground", "ChummerShellForegroundBrush", "#E5E7EB");
    }

    private static void SetLocalBrushResource(Control control, string resourceKey, string themeResourceKey, string fallbackHex)
        => control.Resources[resourceKey] = ResolveThemeBrush(themeResourceKey, fallbackHex);

    private static void ClearTemplatedBrushes(TemplatedControl control)
    {
        control.ClearValue(TemplatedControl.BackgroundProperty);
        control.ClearValue(TemplatedControl.ForegroundProperty);
        control.ClearValue(TemplatedControl.BorderBrushProperty);
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
