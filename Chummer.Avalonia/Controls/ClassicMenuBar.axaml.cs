using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia.Controls;

public partial class ClassicMenuBar : UserControl, IMenuBarSurface
{
    private readonly IReadOnlyList<MenuItem> _rootMenuItems;
    private readonly Dictionary<string, IReadOnlyList<MenuCommandItem>> _commandsByMenuId = new(StringComparer.Ordinal);
    private readonly Button? _autoAliceButton;
    private bool _hasAutoAliceCommand;

    public ClassicMenuBar()
    {
        AvaloniaXamlLoader.Load(this);
        _rootMenuItems = new[]
        {
            this.FindControl<MenuItem>("FileMenuButton"),
            this.FindControl<MenuItem>("EditMenuButton"),
            this.FindControl<MenuItem>("SpecialMenuButton"),
            this.FindControl<MenuItem>("ToolsMenuButton"),
            this.FindControl<MenuItem>("WindowsMenuButton"),
            this.FindControl<MenuItem>("HelpMenuButton")
        }
        .Where(static item => item is not null)
        .Cast<MenuItem>()
        .ToArray();
        _autoAliceButton = this.FindControl<Button>("AutoAliceButton");
        if (_autoAliceButton is not null)
        {
            _autoAliceButton.Content = ShellChromeBoundary.FormatCommandLabel(DesktopAliceAssistant.CommandId);
        }
    }

    public event EventHandler<string>? MenuSelected;
    public event EventHandler<string>? MenuCommandSelected;

    public void SetState(MenuBarState state)
    {
        _commandsByMenuId.Clear();
        foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in state.MenuCommandsByMenuId)
        {
            _commandsByMenuId[menuId] = commands;
        }

        if (!string.IsNullOrWhiteSpace(state.OpenMenuId) && !_commandsByMenuId.ContainsKey(state.OpenMenuId))
        {
            _commandsByMenuId[state.OpenMenuId] = state.OpenMenuCommands.ToArray();
        }

        HashSet<string> knownMenus = state.KnownMenuIds.ToHashSet(StringComparer.Ordinal);
        foreach (MenuItem button in _rootMenuItems)
        {
            string menuId = GetMenuId(button);
            bool known = knownMenus.Contains(menuId);
            button.IsVisible = known;
            button.IsEnabled = known;
            button.Classes.Set("active-menu", string.Equals(state.OpenMenuId, menuId, StringComparison.Ordinal));
            RebuildMenuCommands(button);
        }

        _hasAutoAliceCommand = _commandsByMenuId.Values
            .SelectMany(static commands => commands)
            .Any(command => string.Equals(command.Id, DesktopAliceAssistant.CommandId, StringComparison.Ordinal));
        if (_autoAliceButton is not null)
        {
            _autoAliceButton.IsVisible = _hasAutoAliceCommand;
            _autoAliceButton.IsEnabled = _hasAutoAliceCommand && !state.IsBusy;
        }
    }

    private void RootMenuItem_OnSubmenuOpened(object? sender, RoutedEventArgs e) => SelectRootMenuItem(sender);
    private void RootMenuItem_OnClick(object? sender, RoutedEventArgs e) => SelectRootMenuItem(sender);

    private void SelectRootMenuItem(object? sender)
    {
        if (sender is MenuItem item)
        {
            string menuId = GetMenuId(item);
            if (!string.IsNullOrWhiteSpace(menuId))
            {
                MenuSelected?.Invoke(this, menuId);
            }
        }
    }

    private void MenuCommandItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string commandId && !string.IsNullOrWhiteSpace(commandId))
        {
            MenuCommandSelected?.Invoke(this, commandId);
        }
    }

    private void AutoAliceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_hasAutoAliceCommand)
        {
            return;
        }

        MenuCommandSelected?.Invoke(this, DesktopAliceAssistant.CommandId);
    }

    private void RebuildMenuCommands(MenuItem rootMenuItem)
    {
        if (!_commandsByMenuId.TryGetValue(GetMenuId(rootMenuItem), out IReadOnlyList<MenuCommandItem>? commands) || commands.Count == 0)
        {
            return;
        }

        rootMenuItem.Items.Clear();
        foreach (MenuCommandItem command in commands)
        {
            MenuItem item = new()
            {
                Header = command.Label,
                Tag = command.Id,
                IsEnabled = command.Enabled
            };
            item.Classes.Add("menu-command");
            item.Click += MenuCommandItem_OnClick;
            rootMenuItem.Items.Add(item);
        }
    }

    private static string GetMenuId(MenuItem item)
        => item?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
}
