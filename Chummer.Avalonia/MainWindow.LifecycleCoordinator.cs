using Avalonia.Controls;
using Avalonia.Threading;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

internal sealed class MainWindowLifecycleCoordinator
{
    private readonly Window _window;
    private readonly CharacterOverviewViewModelAdapter _adapter;
    private readonly IShellPresenter _shellPresenter;
    private readonly Action _refreshState;
    private readonly EventHandler _onOpened;

    public MainWindowLifecycleCoordinator(
        Window window,
        CharacterOverviewViewModelAdapter adapter,
        IShellPresenter shellPresenter,
        Action refreshState,
        EventHandler onOpened)
    {
        _window = window;
        _adapter = adapter;
        _shellPresenter = shellPresenter;
        _refreshState = refreshState;
        _onOpened = onOpened;
    }

    public void Attach()
    {
        _adapter.Updated += Adapter_OnUpdated;
        _shellPresenter.StateChanged += ShellPresenter_OnStateChanged;
        _window.Opened += _onOpened;
    }

    public DesktopDialogWindow? Detach(DesktopDialogWindow? dialogWindow)
    {
        _window.Opened -= _onOpened;
        _adapter.Updated -= Adapter_OnUpdated;
        _shellPresenter.StateChanged -= ShellPresenter_OnStateChanged;

        if (dialogWindow is not null)
        {
            dialogWindow.CloseFromPresenter();
        }

        _adapter.Dispose();
        return null;
    }

    private void Adapter_OnUpdated(object? sender, EventArgs e)
    {
        _refreshState();
    }

    private void ShellPresenter_OnStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(_refreshState);
    }
}
