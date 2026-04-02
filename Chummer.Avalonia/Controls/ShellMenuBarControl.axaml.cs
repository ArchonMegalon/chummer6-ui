using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class ShellMenuBarControl : UserControl
{
    private readonly Button[] _menuButtons;
    private MenuCommandItem[] _openMenuCommands = [];

    public ShellMenuBarControl()
    {
        InitializeComponent();
        _menuButtons = MenuBarPanel.Children
            .OfType<Button>()
            .ToArray();
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
            isBusy: state.IsBusy);
    }

    public void SetMenuState(
        string? openMenuId,
        IEnumerable<string> knownMenuIds,
        IEnumerable<MenuCommandItem> openMenuCommands,
        bool isBusy)
    {
        HashSet<string> knownMenus = knownMenuIds.ToHashSet(StringComparer.Ordinal);
        _openMenuCommands = openMenuCommands.ToArray();

        foreach (Button menuButton in _menuButtons)
        {
            string menuId = GetMenuId(menuButton);
            bool known = knownMenus.Contains(menuId);
            bool active = known && string.Equals(openMenuId, menuId, StringComparison.Ordinal);

            menuButton.IsEnabled = known && !isBusy;
            menuButton.Classes.Set("active-menu", active);
        }

        RebuildMenuCommandsHost(!isBusy);
    }

    private void MenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        string menuId = GetMenuId(button);
        if (string.IsNullOrWhiteSpace(menuId))
        {
            return;
        }

        MenuSelected?.Invoke(this, menuId);
    }

    private void MenuCommandButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        string? commandId = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return;
        }

        MenuCommandSelected?.Invoke(this, commandId);
    }

    private void RebuildMenuCommandsHost(bool commandsEnabled)
    {
        MenuCommandsHost.Children.Clear();

        foreach (MenuCommandItem command in _openMenuCommands)
        {
            Button button = new()
            {
                Content = command.Label,
                Tag = command.Id,
                IsEnabled = commandsEnabled && command.Enabled,
                Classes = { "shell-action", command.IsPrimary ? "primary" : "quiet" }
            };
            button.Click += MenuCommandButton_OnClick;
            MenuCommandsHost.Children.Add(button);
        }

        MenuCommandsRegion.IsVisible = _openMenuCommands.Length > 0;
        MenuCommandsTitle.Text = _openMenuCommands.Length > 0
            ? "Menu commands"
            : "Menu commands unavailable";
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        FileMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.file", language);
        EditMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.edit", language);
        SpecialMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.special", language);
        ToolsMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.tools", language);
        WindowsMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.windows", language);
        HelpMenuButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.menu.help", language);
        BannerText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.banner", language);
    }

    private static string GetMenuId(Button button)
        => button.Tag?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
}

public sealed record MenuBarState(
    string? OpenMenuId,
    IReadOnlyList<string> KnownMenuIds,
    IReadOnlyList<MenuCommandItem> OpenMenuCommands,
    bool IsBusy);

public sealed record MenuCommandItem(
    string Id,
    string Label,
    bool Enabled,
    bool IsPrimary = false);
