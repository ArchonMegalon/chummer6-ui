using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Presentation;

namespace Chummer.Avalonia;

internal static class DesktopHorizonWindowScaffold
{
    public static ScrollViewer CreateScroller(string title, string intro, params Control[] sections)
    {
        StackPanel root = new()
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 22,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = intro,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        foreach (Control section in sections)
        {
            root.Children.Add(section);
        }

        return new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = root
            }
        };
    }

    public static Border CreateCard(string title, string summary, Control? leadControl, params Button[] actions)
    {
        StackPanel stack = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = summary,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (leadControl is not null)
        {
            stack.Children.Add(leadControl);
        }

        WrapPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        foreach (Button action in actions)
        {
            action.Margin = new Thickness(0, 0, 8, 8);
            actionRow.Children.Add(action);
        }

        stack.Children.Add(actionRow);

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#BBC7D4")),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#F7FAFD")),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    public static TextBlock CreateDetailText(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };

    public static Control CreateBadgeStrip(params Control[] badges)
    {
        WrapPanel strip = new()
        {
            Orientation = Orientation.Horizontal
        };

        foreach (Control badge in badges)
        {
            badge.Margin = new Thickness(0, 0, 8, 8);
            strip.Children.Add(badge);
        }

        return strip;
    }

    public static Border CreateMetricBadge(string name, string label, string value)
    {
        return new Border
        {
            Name = name,
            Background = new SolidColorBrush(Color.Parse("#E4EDF5")),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = $"{label}: {value}",
                Foreground = new SolidColorBrush(Color.Parse("#24527A")),
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    public static Button CreateStaticButton(string label, Func<bool> action, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 132,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (isPrimary)
        {
            button.Background = new SolidColorBrush(Color.Parse("#24527A"));
            button.Foreground = Brushes.White;
        }

        button.Click += (_, _) => action();
        return button;
    }

    public static Button CreateAsyncButton(Window owner, string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => CreateAsyncButton(
            owner,
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary);

    public static Button CreateAsyncButton(Window owner, string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 132,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (isPrimary)
        {
            button.Background = new SolidColorBrush(Color.Parse("#24527A"));
            button.Foreground = Brushes.White;
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow)
            {
                owner.Close();
            }
        };

        return button;
    }

    public static async Task<AccountCampaignSummary?> TryReadAccountCampaignSummaryAsync(string requiredMessage)
    {
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException(requiredMessage));
            return await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }
}
