using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
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
        => ResolveThemeBrush("ChummerShellWindowBackgroundBrush", "#01010D");

    public static IBrush ResolveHardPanelBrush()
        => ResolveThemeBrush("ChummerShellHardPanelBrush", "#050516");

    public static IBrush ResolveForegroundBrush()
        => ResolveThemeBrush("ChummerShellForegroundBrush", "#EDEDF8");

    public static IBrush ResolveMutedForegroundBrush()
        => ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#B8EDEDF8");

    public static IBrush ResolveTextMutedBrush()
        => ResolveThemeBrush("ChummerShellTextMutedBrush", "#A3EDEDF8");

    public static IBrush ResolveBorderBrush()
        => ResolveThemeBrush("ChummerShellBorderBrush", "#CC1C1C3B");

    public static IBrush ResolveSurfaceBrush()
        => ResolveThemeBrush("ChummerShellSurfaceBrush", "#050516");

    public static IBrush ResolveSurfaceAltBrush()
        => ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#000008");

    public static IBrush ResolveChromeBrush()
        => ResolveThemeBrush("ChummerShellChromeBrush", "#050516");

    public static IBrush ResolveChromeSubtleBrush()
        => ResolveThemeBrush("ChummerShellChromeSubtleBrush", "#080819");

    public static IBrush ResolvePanelMutedBrush()
        => ResolveThemeBrush("ChummerShellPanelMutedBrush", "#080819");

    public static IBrush ResolveSelectionToolbarBrush()
        => ResolveThemeBrush("ChummerShellSelectionToolbarBrush", "#0C0C21");

    public static IBrush ResolveSelectionPanelBrush()
        => ResolveThemeBrush("ChummerShellSelectionPanelBrush", "#050516");

    public static IBrush ResolveSelectionInsetBrush()
        => ResolveThemeBrush("ChummerShellSelectionInsetBrush", "#080819");

    public static IBrush ResolveChromeAccentBrush()
        => ResolveThemeBrush("ChummerShellChromeAccentBrush", "#24526DFF");

    public static IBrush ResolveActiveMenuBorderBrush()
        => ResolveThemeBrush("ChummerShellActiveMenuBorderBrush", "#9EA2FD");

    public static IBrush ResolveInfoBrush()
        => ResolveThemeBrush("ChummerShellInfoBrush", "#9EB7F8");

    public static Border CreateWindowSurface(Control child, double padding = 16)
    {
        Border panel = new() { Padding = new Thickness(padding), Child = child };
        panel.Bind(Border.BackgroundProperty, new DynamicResourceExtension("ChummerShellWindowBackgroundBrush"));
        return panel;
    }

    public static Border CreateUtilityPanel(Control child, double padding = 10, double cornerRadius = 16)
    {
        Border panel = new()
        {
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(cornerRadius),
            Padding = new Thickness(padding), Child = child
        };
        panel.Bind(Border.BackgroundProperty, new DynamicResourceExtension("ChummerShellHardPanelBrush"));
        panel.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));
        return panel;
    }

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
        radioButton.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellForegroundBrush"));
        radioButton.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellSurfaceBrush"));
        radioButton.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));
        radioButton.Padding = new Thickness(8, 6);
    }

    public static void ApplyShellCheckBoxTheme(CheckBox checkBox)
    {
        ArgumentNullException.ThrowIfNull(checkBox);
        checkBox.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellForegroundBrush"));
        checkBox.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellSurfaceBrush"));
        checkBox.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));
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
        panel.Bind(Border.BackgroundProperty, new DynamicResourceExtension("ChummerShellSurfaceBrush"));
        panel.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));
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
            ItemHeight = double.NaN,
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
        control.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellInputBackgroundBrush"));
        control.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellInputForegroundBrush"));
        control.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));

        if (control is TextBox textBox)
        {
            textBox.Bind(TextBox.CaretBrushProperty, new DynamicResourceExtension("ChummerShellInputForegroundBrush"));
            textBox.Bind(TextBox.SelectionBrushProperty, new DynamicResourceExtension("ChummerShellSelectionBrush"));
            textBox.Bind(TextBox.SelectionForegroundBrushProperty, new DynamicResourceExtension("ChummerShellSelectionForegroundBrush"));
        }
    }

    private static void ApplyReadOnlyInputBrushes(TemplatedControl control)
    {
        control.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellSurfaceBrush"));
        control.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellForegroundBrush"));
        control.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));

        if (control is TextBox textBox)
        {
            textBox.Bind(TextBox.CaretBrushProperty, new DynamicResourceExtension("ChummerShellForegroundBrush"));
            textBox.Bind(TextBox.SelectionBrushProperty, new DynamicResourceExtension("ChummerShellSelectionBrush"));
            textBox.Bind(TextBox.SelectionForegroundBrushProperty, new DynamicResourceExtension("ChummerShellSelectionForegroundBrush"));
        }
    }

    private static void ApplyTextControlResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "TextControlBackground", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "TextControlBackgroundPointerOver", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "TextControlBackgroundFocused", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "TextControlBackgroundDisabled", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TextControlForeground", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundPointerOver", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundFocused", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
        SetLocalBrushResource(control, "TextControlCaretBrush", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlSelectionForeground", "ChummerShellSelectionForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlBorderBrush", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "TextControlBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "TextControlBorderBrushFocused", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "TextControlBorderBrushDisabled", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "TextControlPlaceholderForeground", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundPointerOver", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundFocused", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
    }

    private static void ApplyReadOnlyTextControlResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "TextControlBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TextControlBackgroundPointerOver", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TextControlBackgroundFocused", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TextControlBackgroundDisabled", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TextControlForeground", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundPointerOver", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundFocused", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
        SetLocalBrushResource(control, "TextControlCaretBrush", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlSelectionForeground", "ChummerShellSelectionForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "TextControlBorderBrush", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "TextControlBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "TextControlBorderBrushFocused", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "TextControlBorderBrushDisabled", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "TextControlPlaceholderForeground", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundPointerOver", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundFocused", "ChummerShellMutedForegroundBrush", "#B8EDEDF8");
        SetLocalBrushResource(control, "TextControlPlaceholderForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
    }

    private static void ApplyComboBoxBrushes(TemplatedControl control)
    {
        control.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellInputBackgroundBrush"));
        control.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellInputForegroundBrush"));
        control.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ComboBoxBorderBrush"));
    }

    private static void ApplyComboBoxResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "ComboBoxBackground", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "ComboBoxBackgroundPointerOver", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "ComboBoxBackgroundPressed", "ChummerShellInputBackgroundBrush", "#080819");
        SetLocalBrushResource(control, "ComboBoxBackgroundDisabled", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ComboBoxForeground", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxForegroundPointerOver", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxForegroundPressed", "ChummerShellInputForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
        SetLocalBrushResource(control, "ComboBoxBorderBrush", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "ComboBoxBorderBrushPointerOver", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "ComboBoxBorderBrushPressed", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "ComboBoxBorderBrushDisabled", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "ComboBoxDropDownBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ComboBoxDropDownForeground", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxDropDownBorderBrush", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "ComboBoxItemBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundPointerOver", "ChummerShellChromeSubtleBrush", "#080819");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundPressed", "ChummerShellChromeBrush", "#050516");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundSelected", "ChummerShellSelectionBrush", "#40526DFF");
        SetLocalBrushResource(control, "ComboBoxItemBackgroundDisabled", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ComboBoxItemForeground", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxItemForegroundPointerOver", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxItemForegroundPressed", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxItemForegroundSelected", "ChummerShellSelectionForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ComboBoxItemForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
    }

    private static void ApplyListBrushes(TemplatedControl control)
    {
        control.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("ChummerShellSurfaceBrush"));
        control.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("ChummerShellForegroundBrush"));
        control.Bind(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("ChummerShellBorderBrush"));
    }

    private static void ApplySelectableResourceOverrides(Control control)
    {
        SetLocalBrushResource(control, "ChummerShellSurfaceBrush", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ChummerShellForegroundBrush", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ChummerShellChromeSubtleBrush", "ChummerShellChromeSubtleBrush", "#080819");
        SetLocalBrushResource(control, "ChummerShellSelectionBrush", "ChummerShellSelectionBrush", "#40526DFF");
        SetLocalBrushResource(control, "ChummerShellSelectionForegroundBrush", "ChummerShellSelectionForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ChummerShellTextMutedBrush", "ChummerShellTextMutedBrush", "#A3EDEDF8");
        SetLocalBrushResource(control, "ChummerShellBorderBrush", "ChummerShellBorderBrush", "#CC1C1C3B");
        SetLocalBrushResource(control, "ChummerShellActiveMenuBorderBrush", "ChummerShellActiveMenuBorderBrush", "#9EA2FD");
        SetLocalBrushResource(control, "ListBoxBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ListBoxForeground", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ListBoxItemBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "ListBoxItemBackgroundPointerOver", "ChummerShellChromeSubtleBrush", "#080819");
        SetLocalBrushResource(control, "ListBoxItemBackgroundSelected", "ChummerShellSelectionBrush", "#40526DFF");
        SetLocalBrushResource(control, "ListBoxItemBackgroundDisabled", "ChummerShellSurfaceAltBrush", "#000008");
        SetLocalBrushResource(control, "ListBoxItemForeground", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ListBoxItemForegroundPointerOver", "ChummerShellForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ListBoxItemForegroundSelected", "ChummerShellSelectionForegroundBrush", "#EDEDF8");
        SetLocalBrushResource(control, "ListBoxItemForegroundDisabled", "ChummerShellTextMutedBrush", "#A3EDEDF8");
        SetLocalBrushResource(control, "TreeViewBackground", "ChummerShellSurfaceBrush", "#050516");
        SetLocalBrushResource(control, "TreeViewForeground", "ChummerShellForegroundBrush", "#EDEDF8");
    }

    private static void SetLocalBrushResource(Control control, string resourceKey, string themeResourceKey, string fallbackHex)
        {
        if (resourceKey == themeResourceKey) return;
        control.GetResourceObservable(themeResourceKey).Subscribe(new ResourceObserver(value =>
        {
            if (value is IBrush brush
                && (!control.Resources.TryGetResource(resourceKey, null, out object? current)
                    || !ReferenceEquals(current, brush)))
                control.Resources[resourceKey] = brush;
        }));
    }

    private sealed class ResourceObserver(Action<object?> update) : IObserver<object?>
    {
        public void OnNext(object? value) => update(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

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
