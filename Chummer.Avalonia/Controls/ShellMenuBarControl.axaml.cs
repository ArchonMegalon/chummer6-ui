using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia.Controls;

public partial class ShellMenuBarControl : UserControl, IMenuBarSurface
{
    private readonly MenuItem[] _rootMenuItems;
    private readonly Dictionary<string, IReadOnlyList<MenuCommandItem>> _commandsByMenuId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _knownMenuIds = new(StringComparer.Ordinal);
    private string? _lastAppliedSignature;
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
        foreach (MenuItem rootMenuItem in _rootMenuItems)
        {
            rootMenuItem.PropertyChanged += RootMenuItem_OnPropertyChanged;
        }
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
        Dictionary<string, IReadOnlyList<MenuCommandItem>> mergedCommands = new(StringComparer.Ordinal);
        if (menuCommandsByMenuId is not null)
        {
            foreach ((string menuId, IReadOnlyList<MenuCommandItem> commands) in menuCommandsByMenuId)
            {
                mergedCommands[menuId] = commands;
            }
        }

        if (!string.IsNullOrWhiteSpace(openMenuId) && !mergedCommands.ContainsKey(openMenuId))
        {
            mergedCommands[openMenuId] = openMenuCommands.ToArray();
        }

        HashSet<string> knownMenus = knownMenuIds.ToHashSet(StringComparer.Ordinal);

        // Rebuilding a root MenuItem's Items while its submenu popup is open detaches the
        // MenuItems the user is looking at (and may be pressing), which swallows the click.
        // A root click fires MenuSelected up to three times (PointerPressed, Click,
        // SubmenuOpened); each round-trip used to land here and tear the open popup down
        // mid-gesture. Identical state pushes must therefore rebuild nothing, and content
        // that genuinely changed is deferred for open submenus until the submenu closes.
        string signature = BuildStateSignature(openMenuId, knownMenus, isBusy, mergedCommands);
        if (string.Equals(signature, _lastAppliedSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastAppliedSignature = signature;
        _openMenuId = openMenuId;
        _isBusy = isBusy;
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
            bool hasCommands = _commandsByMenuId.TryGetValue(menuId, out IReadOnlyList<MenuCommandItem>? commands) && commands.Count > 0;
            bool active = known && hasCommands && string.Equals(openMenuId, menuId, StringComparison.Ordinal);

            button.IsVisible = known;
            button.IsEnabled = known && hasCommands;
            button.Classes.Set("active-menu", active);
            if (!button.IsSubMenuOpen)
            {
                RebuildMenuItemCommands(button, commandsAvailable: known && hasCommands);
            }
        }
    }

    private void RootMenuItem_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // IsSubMenuOpen true->false is the reliable "submenu closed" signal in this Avalonia
        // version (no SubmenuClosedEvent). Rebuilding here is safe: no pointer gesture can
        // be in flight against a closed popup.
        if (e.Property == MenuItem.IsSubMenuOpenProperty && e.NewValue is false && sender is MenuItem button)
        {
            string menuId = GetMenuId(button);
            bool commandsAvailable = _knownMenuIds.Contains(menuId) && HasVisibleMenuCommands(menuId);
            RebuildMenuItemCommands(button, commandsAvailable);
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
        if (string.IsNullOrWhiteSpace(menuId))
        {
            return;
        }

        if (!HasVisibleMenuCommands(menuId))
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

    private void RebuildMenuItemCommands(MenuItem rootMenuItem, bool commandsAvailable)
    {
        rootMenuItem.Items.Clear();

        string menuId = GetMenuId(rootMenuItem);
        if (!_commandsByMenuId.TryGetValue(menuId, out IReadOnlyList<MenuCommandItem>? commands) || commands.Count == 0)
        {
            rootMenuItem.Items.Add(CreatePlaceholderMenuItem(_isBusy));
            return;
        }

        foreach (MenuCommandItem command in commands)
        {
            MenuItem commandItem = new()
            {
                Header = command.Label,
                Tag = command.Id,
                IsEnabled = commandsAvailable && command.Enabled
            };
            commandItem.Classes.Add("menu-command");
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

    private static string GetMenuId(MenuItem button)
        => button.Tag?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

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
