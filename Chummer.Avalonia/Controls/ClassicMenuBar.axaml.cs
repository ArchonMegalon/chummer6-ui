using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia.Controls;

public partial class ClassicMenuBar : UserControl, IMenuBarSurface
{
    private readonly IReadOnlyList<MenuItem> _rootMenuItems;
    private readonly Dictionary<string, IReadOnlyList<MenuCommandItem>> _commandsByMenuId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _knownMenuIds = new(StringComparer.Ordinal);
    private string? _lastAppliedSignature;
    private bool _isBusy;

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
        foreach (MenuItem rootMenuItem in _rootMenuItems)
        {
            rootMenuItem.PropertyChanged += RootMenuItem_OnPropertyChanged;
        }
    }

    public event EventHandler<string>? MenuSelected;
    public event EventHandler<string>? MenuCommandSelected;

    public void SetState(MenuBarState state)
    {
        Dictionary<string, IReadOnlyList<MenuCommandItem>> mergedCommands = new(StringComparer.Ordinal);
        foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in state.MenuCommandsByMenuId)
        {
            mergedCommands[menuId] = commands;
        }

        if (!string.IsNullOrWhiteSpace(state.OpenMenuId) && !mergedCommands.ContainsKey(state.OpenMenuId))
        {
            mergedCommands[state.OpenMenuId] = state.OpenMenuCommands.ToArray();
        }

        HashSet<string> knownMenus = state.KnownMenuIds.ToHashSet(StringComparer.Ordinal);

        // Mirrors ShellMenuBarControl: identical state pushes are no-ops and genuinely
        // changed content rebuilds only closed submenus, so an open popup is never torn
        // down mid-gesture (which detached the item under the user's press).
        string signature = BuildStateSignature(state.OpenMenuId, knownMenus, state.IsBusy, mergedCommands);
        if (string.Equals(signature, _lastAppliedSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastAppliedSignature = signature;
        _isBusy = state.IsBusy;
        _knownMenuIds.Clear();
        _knownMenuIds.UnionWith(knownMenus);
        _commandsByMenuId.Clear();
        foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in mergedCommands)
        {
            _commandsByMenuId[menuId] = commands;
        }

        foreach (MenuItem button in _rootMenuItems)
        {
            string menuId = GetMenuId(button);
            bool known = knownMenus.Contains(menuId);
            bool hasCommands = _commandsByMenuId.TryGetValue(menuId, out IReadOnlyList<MenuCommandItem>? commands)
                && commands.Count > 0;
            button.IsVisible = known;
            button.IsEnabled = known && hasCommands;
            button.Classes.Set("active-menu", known && hasCommands && string.Equals(state.OpenMenuId, menuId, StringComparison.Ordinal));
            if (!button.IsSubMenuOpen)
            {
                RebuildMenuCommands(button);
            }
        }
    }

    private void RootMenuItem_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // IsSubMenuOpen true->false is the reliable "submenu closed" signal in this Avalonia
        // version. Rebuilding here is safe: no pointer gesture is in flight against a closed popup.
        if (e.Property == MenuItem.IsSubMenuOpenProperty && e.NewValue is false && sender is MenuItem button)
        {
            RebuildMenuCommands(button);
        }
    }

    private void RootMenuItem_OnSubmenuOpened(object? sender, RoutedEventArgs e) => SelectRootMenuItem(sender);
    private void RootMenuItem_OnClick(object? sender, RoutedEventArgs e) => SelectRootMenuItem(sender);
    private void RootMenuItem_OnPointerPressed(object? sender, PointerPressedEventArgs e) => SelectRootMenuItem(sender);

    private void SelectRootMenuItem(object? sender)
    {
        if (sender is MenuItem item)
        {
            string menuId = GetMenuId(item);
            if (!string.IsNullOrWhiteSpace(menuId) && HasVisibleMenuCommands(menuId))
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

    private void RebuildMenuCommands(MenuItem rootMenuItem)
    {
        rootMenuItem.Items.Clear();

        if (!_commandsByMenuId.TryGetValue(GetMenuId(rootMenuItem), out IReadOnlyList<MenuCommandItem>? commands) || commands.Count == 0)
        {
            rootMenuItem.Items.Add(CreatePlaceholderMenuItem(_isBusy));
            return;
        }

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

    private bool HasVisibleMenuCommands(string menuId)
        => _commandsByMenuId.TryGetValue(menuId, out IReadOnlyList<MenuCommandItem>? commands)
            && commands.Count > 0;

    private static string BuildStateSignature(
        string? openMenuId,
        HashSet<string> knownMenus,
        bool isBusy,
        Dictionary<string, IReadOnlyList<MenuCommandItem>> mergedCommands)
    {
        StringBuilder signature = new();
        signature.Append(openMenuId ?? "\0").Append('|').Append(isBusy ? '1' : '0').Append('|');
        signature.AppendJoin(",", knownMenus.OrderBy(static id => id, StringComparer.Ordinal));
        signature.Append('|');
        foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in mergedCommands.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            signature.Append(menuId).Append('=');
            foreach (MenuCommandItem command in commands)
            {
                signature.Append(command.Id).Append(':')
                    .Append(command.Label).Append(':')
                    .Append(command.Enabled ? '1' : '0')
                    .Append(command.IsPrimary ? '1' : '0')
                    .Append(';');
            }

            signature.Append('#');
        }

        return signature.ToString();
    }

    private static MenuItem CreatePlaceholderMenuItem(bool isBusy)
    {
        MenuItem item = new()
        {
            Header = isBusy ? "Loading actions..." : "No actions available",
            IsEnabled = false
        };
        item.Classes.Add("menu-command");
        return item;
    }
}
