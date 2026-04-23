using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Chummer.Avalonia;

internal sealed class DesktopVersionHistoryWindow : Window
{
    public DesktopVersionHistoryWindow()
    {
        Title = "Chummer Revision History";
        Width = 784;
        Height = 561;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        string historyText = LoadHistoryText();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EEF2F6")),
            Padding = new Thickness(12),
            Child = new TextBox
            {
                Text = historyText,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#BBC7D4")),
                BorderThickness = new Thickness(1)
            }
        };
    }

    public static async Task ShowAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DesktopVersionHistoryWindow dialog = new();
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static string LoadHistoryText()
    {
        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "changelog.txt"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Chummer", "changelog.txt"))
        ];

        foreach (string candidatePath in candidatePaths)
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                return File.ReadAllText(candidatePath);
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return "Revision history is unavailable because changelog.txt was not found in the desktop payload.";
    }
}
