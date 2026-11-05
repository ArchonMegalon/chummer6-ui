using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TextWrapping = global::Avalonia.Media.TextWrapping;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style informational dialog for an interrupted AI completion
/// run whose outcome is unknown (re-audit 2026-11-05). It states exactly what
/// was verified, what remains unknown, and that NO automatic retry exists: the
/// pinned Core has no receipt/replay contract, so the player inspects the
/// character manually and decides. The dialog only acknowledges; it never
/// dispatches anything.
/// </summary>
public sealed class CreationAiFinishResumeWindow : Window
{
    public CreationAiFinishResumeWindow(string uncertainDomain)
    {
        Title = "Interrupted AI completion";
        Width = 520;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        body.Children.Add(new TextBlock
        {
            Text = "Interrupted AI completion",
            Classes = { "shell-section-title" },
        });
        body.Children.Add(new TextBlock
        {
            Text = "A previous AI completion was interrupted while the '"
                + uncertainDomain + "' stage was in progress. What was verified: the"
                + " earlier stages were recorded as confirmed and no later stage was"
                + " started. What remains unknown: whether the '" + uncertainDomain
                + "' stage actually saved - the application lost contact before the"
                + " outcome was recorded.",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "No automatic retry is offered. Repeating the stage automatically"
                + " could apply it a second time, so the record stays exactly as saved."
                + " Inspect the character in the manual editors and decide yourself:"
                + " if the stage is missing, redo it by hand; if it is present twice or"
                + " wrong, correct it there. This window changes nothing.",
            TextWrapping = TextWrapping.Wrap,
            Classes = { "shell-caption" },
        });

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var acknowledgeButton = new Button { Content = "Close" };
        acknowledgeButton.Click += (_, _) => Close(false);
        footer.Children.Add(acknowledgeButton);

        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(body);
        Content = root;
    }
}
