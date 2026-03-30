using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

public partial class ShellMenuBarControl : UserControl
{
    private readonly Button[] _menuButtons;

    public ShellMenuBarControl()
    {
        InitializeComponent();
        _menuButtons = MenuBarPanel.Children
            .OfType<Button>()
            .ToArray();
        ApplyLocalization();
    }

    public event EventHandler<string>? MenuSelected;

    public void SetState(MenuBarState state)
    {
        SetMenuState(
            openMenuId: state.OpenMenuId,
            knownMenuIds: state.KnownMenuIds,
            isBusy: state.IsBusy);
    }

    public void SetMenuState(string? openMenuId, IEnumerable<string> knownMenuIds, bool isBusy)
    {
        HashSet<string> knownMenus = knownMenuIds.ToHashSet(StringComparer.Ordinal);

        foreach (Button menuButton in _menuButtons)
        {
            string menuId = GetMenuId(menuButton);
            bool known = knownMenus.Contains(menuId);
            bool active = known && string.Equals(openMenuId, menuId, StringComparison.Ordinal);

            menuButton.IsEnabled = known && !isBusy;
            menuButton.Classes.Set("active-menu", active);
        }
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
    bool IsBusy);
