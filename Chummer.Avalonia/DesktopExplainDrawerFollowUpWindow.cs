using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Avalonia.Controls;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopExplainDrawerFollowUpWindow : Window
{
    private readonly ExplainDrawerContext _context;
    private readonly TextBlock _statusText;

    private DesktopExplainDrawerFollowUpWindow(ExplainDrawerContext context)
    {
        _context = context;

        Title = "Explain Follow-up";
        Width = 760;
        Height = 520;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _statusText = new TextBlock
        {
            Text = "Follow-up stays text-first, packet-backed, and scoped to the current desktop snapshot.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Explain Follow-up",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = "Use the current explain packet, cited source anchor, and stale-state posture before taking the next safe action.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        _statusText,
                        CreateSection("Explain packet", _context.ExplainPacket),
                        CreateSection("Source anchor", FirstNonBlank(_context.SourceAnchor, "No source anchor is attached to this packet.")),
                        CreateSection("Stale-state posture", FirstNonBlank(_context.StaleState, "No stale-state warning is attached to this packet.")),
                        CreateSection("Bounded follow-up", FirstNonBlank(_context.FollowUp, "No bounded follow-up is attached to this packet.")),
                        CreateActionBar()
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, ExplainDrawerContext context)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);

        DesktopExplainDrawerFollowUpWindow dialog = new(context);
        await dialog.ShowDialog(owner);
    }

    private Control CreateSection(string title, string body)
    {
        return new Border
        {
            Classes = { "section-card" },
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = body,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private Control[] CreateActions()
    {
        List<Control> actions =
        [
            CreateButton("Close", static () => Task.CompletedTask, closeWindow: true, isPrimary: true)
        ];

        if (!string.IsNullOrWhiteSpace(_context.SourceLaunchTarget))
        {
            actions.Insert(0, CreateButton("Open Source Anchor", OpenSourceAnchorAsync));
        }

        return actions.ToArray();
    }

    private Control CreateActionBar()
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        foreach (Control action in CreateActions())
        {
            panel.Children.Add(action);
        }

        return panel;
    }

    private Task OpenSourceAnchorAsync()
    {
        string? target = _context.SourceLaunchTarget;
        if (!string.IsNullOrWhiteSpace(target)
            && DesktopCrashRuntime.TryOpenPathInShell(target))
        {
            _statusText.Text = $"Opened source anchor for {_context.ExplainPacket}.";
        }
        else
        {
            _statusText.Text = "Source anchor launch is unavailable for this packet.";
        }

        return Task.CompletedTask;
    }

    private Button CreateButton(
        string text,
        Func<Task> action,
        bool closeWindow = false,
        bool isPrimary = false)
    {
        Button button = new()
        {
            Content = text,
            MinWidth = 156
        };
        button.Classes.Add("shell-action");
        button.Classes.Add(isPrimary ? "primary" : "quiet");
        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow)
            {
                Close();
            }
        };
        return button;
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
