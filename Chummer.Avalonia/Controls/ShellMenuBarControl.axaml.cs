using Avalonia.Controls;
using Avalonia.Interactivity;

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
            string menuId = menuButton.Content?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            bool known = knownMenus.Contains(menuId);
            bool active = known && string.Equals(openMenuId, menuId, StringComparison.Ordinal);

            menuButton.IsEnabled = known && !isBusy;
            menuButton.Classes.Set("active-menu", active);
        }
    }

    private void MenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is null)
            return;

        string menuId = button.Content.ToString()!.Trim().ToLowerInvariant();
        MenuSelected?.Invoke(this, menuId);
    }
}

public sealed record MenuBarState(
    string? OpenMenuId,
    IReadOnlyList<string> KnownMenuIds,
    bool IsBusy);
