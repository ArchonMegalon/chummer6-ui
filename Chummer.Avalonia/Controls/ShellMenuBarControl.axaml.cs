using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class ShellMenuBarControl : UserControl
{
    private readonly MenuItem[] _rootMenuItems;
    private readonly Dictionary<string, IReadOnlyList<MenuCommandItem>> _commandsByMenuId = new(StringComparer.Ordinal);
    private string? _openMenuId;
    private bool _isBusy;

    public ShellMenuBarControl()
    {
        InitializeComponent();
        _rootMenuItems =
        [
            FileMenuButton,
            EditMenuButton,
            SpecialMenuButton,
            ToolsMenuButton,
            WindowsMenuButton,
            HelpMenuButton
        ];
        ApplyLocalization();
    }

    public event EventHandler<string>? MenuSelected;
    public event EventHandler<string>? MenuCommandSelected;

    public void SetState(MenuBarState state)
    {
        SetMenuState(
            openMenuId: state.OpenMenuId,
            knownMenuIds: state.KnownMenuIds,
            openMenuCommands: state.OpenMenuCommands,
            isBusy: state.IsBusy,
            menuCommandsByMenuId: state.MenuCommandsByMenuId);
    }

    public void SetMenuState(
        string? openMenuId,
        IEnumerable<string> knownMenuIds,
        IEnumerable<MenuCommandItem> openMenuCommands,
        bool isBusy,
        IReadOnlyDictionary<string, IReadOnlyList<MenuCommandItem>>? menuCommandsByMenuId = null)
    {
        _openMenuId = openMenuId;
        _isBusy = isBusy;
        _commandsByMenuId.Clear();

        if (menuCommandsByMenuId is not null)
        {
            foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in menuCommandsByMenuId)
            {
                _commandsByMenuId[menuId] = commands;
            }
        }

        if (!string.IsNullOrWhiteSpace(openMenuId) && !_commandsByMenuId.ContainsKey(openMenuId))
        {
            _commandsByMenuId[openMenuId] = openMenuCommands.ToArray();
        }

        HashSet<string> knownMenus = knownMenuIds.ToHashSet(StringComparer.Ordinal);

        foreach (MenuItem menuItem in _rootMenuItems)
        {
            string menuId = GetMenuId(menuItem);
            bool known = knownMenus.Contains(menuId);
            bool active = known && string.Equals(openMenuId, menuId, StringComparison.Ordinal);

            menuItem.IsEnabled = known;
            menuItem.Classes.Set("active-menu", active);
            RebuildMenuItemCommands(menuItem, commandsEnabled: known);
        }
    }

    private void RootMenuItem_OnSubmenuOpened(object? sender, RoutedEventArgs e)
        => SelectRootMenuItem(sender);

    private void RootMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectRootMenuItem(sender);
    }

    private void RootMenuItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        => SelectRootMenuItem(sender);

    private void SelectRootMenuItem(object? sender)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        string menuId = GetMenuId(menuItem);
        if (string.IsNullOrWhiteSpace(menuId) || string.Equals(_openMenuId, menuId, StringComparison.Ordinal))
        {
            return;
        }

        _openMenuId = menuId;
        MenuSelected?.Invoke(this, menuId);
    }

    private void MenuCommandItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        string? commandId = menuItem.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return;
        }

        MenuCommandSelected?.Invoke(this, commandId);
    }

    private void RebuildMenuItemCommands(MenuItem rootMenuItem, bool commandsEnabled)
    {
        rootMenuItem.Items.Clear();

        string menuId = GetMenuId(rootMenuItem);
        if (!_commandsByMenuId.TryGetValue(menuId, out IReadOnlyList<MenuCommandItem>? commands) || commands.Count == 0)
        {
            rootMenuItem.Items.Add(new MenuItem
            {
                Header = "(No commands)",
                IsEnabled = false
            });
            return;
        }

        foreach (MenuCommandItem command in commands)
        {
            MenuItem commandItem = new()
            {
                Header = command.Label,
                Tag = command.Id,
                IsEnabled = commandsEnabled && command.Enabled
            };
            if (command.IsPrimary)
            {
                commandItem.Classes.Add("primary-menu-command");
            }

            commandItem.Click += MenuCommandItem_OnClick;
            rootMenuItem.Items.Add(commandItem);
        }
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        FileMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.file", language);
        EditMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.edit", language);
        SpecialMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.special", language);
        ToolsMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.tools", language);
        WindowsMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.windows", language);
        HelpMenuButton.Header = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.help", language);
        _ = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.banner", language);
    }

    private static string GetMenuId(MenuItem menuItem)
        => menuItem.Tag?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
}

public sealed record MenuBarState(
    string? OpenMenuId,
    IReadOnlyList<string> KnownMenuIds,
    IReadOnlyList<MenuCommandItem> OpenMenuCommands,
    IReadOnlyDictionary<string, IReadOnlyList<MenuCommandItem>> MenuCommandsByMenuId,
    bool IsBusy);

public sealed record MenuCommandItem(
    string Id,
    string Label,
    bool Enabled,
    bool IsPrimary = false);
