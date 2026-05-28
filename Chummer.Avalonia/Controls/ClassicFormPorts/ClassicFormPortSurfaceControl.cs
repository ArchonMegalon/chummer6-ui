using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Chummer.Avalonia.Controls;

public abstract class ClassicFormPortSurfaceControl : UserControl
{
    private readonly string _legacyDesignerPath;

    protected ClassicFormPortSurfaceControl(string surfaceId, string surfaceTitle, IReadOnlyList<string> tabs, string legacyDesignerPath)
    {
        SurfaceId = surfaceId;
        SurfaceTitle = surfaceTitle;
        Tabs = tabs;
        _legacyDesignerPath = legacyDesignerPath;
    }

    public string SurfaceId { get; }
    public string SurfaceTitle { get; }
    protected IReadOnlyList<string> Tabs { get; }
    protected ClassicFormPortState? CurrentState { get; private set; }
    protected ClassicFormDesignerSnapshot CurrentSnapshot { get; private set; } = new(
        string.Empty,
        Exists: false,
        Controls: [],
        RootControls: [],
        Tabs: [],
        Groups: [],
        ToolStrips: [],
        ContextMenus: [],
        EventHandlers: []);

    public void SetState(ClassicFormPortState state)
    {
        CurrentState = state;
        CurrentSnapshot = ClassicFormDesignerParser.Parse(_legacyDesignerPath);
        ApplyState(state, CurrentSnapshot);
    }

    protected abstract void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot);

    protected static IReadOnlyList<string> ResolveTabLabels(IReadOnlyList<string> designTabs, ClassicFormDesignerSnapshot snapshot)
        => designTabs
            .Concat(snapshot.Tabs.Where(tab => !designTabs.Contains(tab, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

    protected static IReadOnlyList<string> ResolveActionLabels(ClassicFormPortState state)
        => state.QuickActions
            .Select(action => action.Label)
            .Concat(state.SectionActions.Select(action => action.Label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

    protected static IReadOnlyList<ClassicPortLineItem> SelectRows(
        IReadOnlyList<SectionRowDisplayItem> rows,
        int maxCount,
        params string[] pathTokens)
    {
        IEnumerable<SectionRowDisplayItem> filtered = rows;
        if (pathTokens.Length > 0)
        {
            filtered = rows.Where(row => pathTokens.Any(token =>
                row.Path.Contains(token, StringComparison.OrdinalIgnoreCase)
                || row.DisplayPath.Contains(token, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered
            .Take(maxCount)
            .Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue))
            .ToArray();
    }

    protected static IReadOnlyList<ClassicPortLineItem> BuildLegacyChromeLines(ClassicFormDesignerSnapshot snapshot, int maxCount)
    {
        List<ClassicPortLineItem> lines = [];
        lines.AddRange(snapshot.Groups.Take(maxCount).Select(group => new ClassicPortLineItem("Group", group)));
        lines.AddRange(snapshot.ToolStrips.Take(Math.Max(0, maxCount - lines.Count)).Select(strip => new ClassicPortLineItem("Strip", strip)));
        lines.AddRange(snapshot.ContextMenus.Take(Math.Max(0, maxCount - lines.Count)).Select(menu => new ClassicPortLineItem("Menu", menu)));
        return lines.Take(maxCount).ToArray();
    }

    protected static string FindValue(IReadOnlyList<SectionRowDisplayItem> rows, params string[] pathTokens)
    {
        foreach (string token in pathTokens)
        {
            SectionRowDisplayItem? row = rows.FirstOrDefault(candidate =>
                candidate.Path.Contains(token, StringComparison.OrdinalIgnoreCase)
                || candidate.DisplayPath.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (row is not null && !string.IsNullOrWhiteSpace(row.DisplayValue))
            {
                return row.DisplayValue;
            }
        }

        return "n/a";
    }

    protected static void PopulateChipStrip(Panel panel, IEnumerable<string> labels, string? selectedLabel = null)
    {
        panel.Children.Clear();
        foreach (string label in labels.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            bool selected = string.Equals(label, selectedLabel, StringComparison.OrdinalIgnoreCase);
            panel.Children.Add(CreateChip(label, selected));
        }
    }

    protected static void PopulateFactStrip(Panel panel, IEnumerable<ClassicSheetFactDisplayItem> facts)
    {
        panel.Children.Clear();
        foreach (ClassicSheetFactDisplayItem fact in facts.Where(static fact => !string.IsNullOrWhiteSpace(fact.Value)))
        {
            panel.Children.Add(CreateFactTile(fact));
        }
    }

    protected static void PopulateLineStack(Panel panel, IEnumerable<ClassicPortLineItem> lines, string emptyMessage)
    {
        panel.Children.Clear();
        ClassicPortLineItem[] materialized = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Detail)).ToArray();
        if (materialized.Length == 0)
        {
            panel.Children.Add(CreateEmptyState(emptyMessage));
            return;
        }

        foreach (ClassicPortLineItem line in materialized)
        {
            panel.Children.Add(CreateLineCard(line));
        }
    }

    protected static void SetNotice(TextBlock textBlock, string notice, string fallback)
    {
        textBlock.Text = string.IsNullOrWhiteSpace(notice) ? fallback : notice;
    }

    protected static Border CreateSectionCard(string heading, Control content)
    {
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.Parse("#3f4b53")),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = heading,
                        FontWeight = FontWeight.SemiBold
                    },
                    content
                }
            }
        };
    }

    private static Control CreateChip(string label, bool selected)
    {
        return new Border
        {
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(selected ? Color.Parse("#d8a74f") : Color.Parse("#5d666d")),
            Background = new SolidColorBrush(selected ? Color.Parse("#2e2414") : Color.Parse("#1c242b")),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal
            }
        };
    }

    private static Control CreateFactTile(ClassicSheetFactDisplayItem fact)
    {
        return new Border
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#43515a")),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = fact.Label,
                        FontSize = 11,
                        Opacity = 0.72
                    },
                    new TextBlock
                    {
                        Text = fact.Value,
                        FontWeight = FontWeight.SemiBold
                    }
                }
            }
        };
    }

    private static Control CreateLineCard(ClassicPortLineItem line)
    {
        return new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8, 6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#334048")),
            CornerRadius = new CornerRadius(4),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = line.Label,
                        FontSize = 11,
                        Opacity = 0.72
                    },
                    new TextBlock
                    {
                        Text = line.Detail,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control CreateEmptyState(string text)
    {
        return new TextBlock
        {
            Text = text,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        };
    }
}

public sealed record ClassicPortLineItem(string Label, string Detail);
