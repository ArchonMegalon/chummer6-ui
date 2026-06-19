using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.AI;
using System.Globalization;

namespace Chummer.Avalonia;

internal static class DesktopExplainCompanionLauncher
{
    public static string BuildLaunchUri(DesktopExplainCompanionRequest request)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: AiRouteTypes.Build,
                RuntimeFingerprint: request.RuntimeFingerprint,
                WorkspaceId: request.WorkspaceId,
                RulesetId: request.RulesetId,
                Message: BuildLaunchMessage(request)));

    public static void Show(Control source, DesktopExplainCompanionRequest request)
    {
        DesktopExplainCompanionWindow window = new(request);
        if (TopLevel.GetTopLevel(source) is Window owner)
        {
            _ = window.ShowDialog(owner);
            return;
        }

        window.Show();
    }

    public static Button CreateLaunchButton(
        Control source,
        DesktopExplainCompanionRequest request,
        string controlName)
    {
        string launchUri = BuildLaunchUri(request);
        Button button = new()
        {
            Name = controlName,
            Content = "Open Explain Companion",
            Tag = launchUri,
            MinWidth = 176,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(button, "Open inspectable explain companion");
        AutomationProperties.SetHelpText(
            button,
            "Opens a desktop companion with the same receipt, blocker, compare, and environment-diff context.");
        button.Click += (_, _) => Show(source, request with { LaunchUri = launchUri });
        return button;
    }

    private static string BuildLaunchMessage(DesktopExplainCompanionRequest request)
    {
        IEnumerable<string> sectionSummaries = request.Sections
            .Select(section => string.Join(
                " ",
                new[] { section.Title }.Concat(section.Lines.Take(2))))
            .Where(static line => !string.IsNullOrWhiteSpace(line));
        return $"{request.SurfaceLabel} ({request.SurfaceId}): inspect {request.Title}. {string.Join(" ", sectionSummaries)}";
    }
}

internal sealed class DesktopExplainCompanionWindow : Window
{
    private readonly string _receiptText;
    private readonly TextBlock _copyStatusText = new()
    {
        TextWrapping = TextWrapping.Wrap
    };

    public DesktopExplainCompanionWindow(DesktopExplainCompanionRequest request)
    {
        _receiptText = DesktopTrustReceiptText.BuildReceiptText(request.Sections);
        Title = request.Title;
        Width = 760;
        Height = 640;
        MinWidth = 560;
        MinHeight = 420;
        Content = CreateContent(request);
    }

    private Control CreateContent(DesktopExplainCompanionRequest request)
    {
        StackPanel content = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };

        content.Children.Add(new TextBlock
        {
            Text = request.SurfaceLabel,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = request.Title,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(CreateMetadataGrid(request));
        string launchUri = request.LaunchUri ?? DesktopExplainCompanionLauncher.BuildLaunchUri(request);
        content.Children.Add(new TextBlock
        {
            Text = "Companion launch link",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        TextBox launchUriTextBox = new()
        {
            Name = "ExplainCompanionLaunchUriTextBox",
            Text = launchUri,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 34
        };
        DesktopShellTheme.ApplyShellTextInputTheme(launchUriTextBox);
        AutomationProperties.SetName(launchUriTextBox, "Inspectable explain companion launch link");
        AutomationProperties.SetHelpText(launchUriTextBox, "Copy-safe launch link for reopening the same receipt, blocker, compare, and environment-diff context.");
        content.Children.Add(launchUriTextBox);
        Button copyLaunchUriButton = new()
        {
            Name = "CopyExplainCompanionLaunchUriButton",
            Content = "Copy Companion Link",
            MinWidth = 160,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(copyLaunchUriButton, "Copy explain companion link");
        AutomationProperties.SetHelpText(copyLaunchUriButton, "Copies the companion launch link for the same receipt, blocker, compare, and environment-diff context.");
        copyLaunchUriButton.Click += async (_, _) => await CopyTextAsync(launchUri, "Explain companion link copied.").ConfigureAwait(true);
        content.Children.Add(copyLaunchUriButton);
        content.Children.Add(new TextBlock
        {
            Text = "Receipt text",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        TextBox receiptTextBox = new()
        {
            Name = "ExplainCompanionReceiptTextBox",
            Text = _receiptText,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            AcceptsReturn = true
        };
        DesktopShellTheme.ApplyShellTextInputTheme(receiptTextBox);
        AutomationProperties.SetName(receiptTextBox, "Inspectable explain companion receipt text");
        AutomationProperties.SetHelpText(receiptTextBox, "Copy-safe receipt text for support, blocker, compare, and environment-diff review.");
        content.Children.Add(receiptTextBox);
        Button copyReceiptButton = new()
        {
            Name = "CopyExplainCompanionReceiptButton",
            Content = "Copy Explain Receipt",
            MinWidth = 160,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(copyReceiptButton, "Copy explain companion receipt");
        AutomationProperties.SetHelpText(copyReceiptButton, "Copies the companion receipt, blocker, compare, and environment-diff context.");
        copyReceiptButton.Click += async (_, _) => await CopyTextAsync(_receiptText, "Explain receipt copied.").ConfigureAwait(true);
        content.Children.Add(copyReceiptButton);
        content.Children.Add(_copyStatusText);

        foreach (DesktopTrustReceiptSection section in request.Sections)
        {
            Border sectionBorder = new()
            {
                BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700"),
                Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSelectionInsetBrush", "#F1F5F9"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = CreateSectionContent(section)
            };
            content.Children.Add(sectionBorder);
        }

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
    }

    private static Control CreateMetadataGrid(DesktopExplainCompanionRequest request)
    {
        List<(string Label, string? Value, string ValueName)> metadataRows =
        [
            ("Surface", request.SurfaceId, "ExplainCompanionSurfaceIdText"),
            ("Owned surface", request.SurfaceFamilyId ?? request.SurfaceId, "ExplainCompanionSurfaceFamilyIdText"),
            ("Ruleset", request.RulesetId, "ExplainCompanionRulesetIdText"),
            ("Workspace", request.WorkspaceId, "ExplainCompanionWorkspaceIdText"),
            ("Runtime", request.RuntimeFingerprint, "ExplainCompanionRuntimeFingerprintText"),
            ("Correlation", TryExtractSectionValue(request.Sections, "correlation key:"), "ExplainCompanionCorrelationKeyText"),
            ("Support handoff", TryExtractSectionValue(request.Sections, "handoff receipt:"), "ExplainCompanionSupportHandoffText"),
            ("Sections", request.Sections.Count.ToString(CultureInfo.InvariantCulture), "ExplainCompanionSectionCountText")
        ];

        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Margin = new Thickness(0, 4, 0, 0)
        };

        int rowIndex = 0;
        foreach ((string label, string? value, string valueName) in metadataRows)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddMetadataRow(grid, rowIndex, label, value, valueName);
            rowIndex++;
        }

        return grid;
    }

    private static string? TryExtractSectionValue(
        IReadOnlyList<DesktopTrustReceiptSection> sections,
        string marker)
    {
        foreach (string line in sections.SelectMany(static section => section.Lines))
        {
            int markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            return line[(markerIndex + marker.Length)..].Trim();
        }

        return null;
    }

    private static void AddMetadataRow(Grid grid, int row, string label, string? value, string valueName)
    {
        TextBlock labelBlock = new()
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 12, 6),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        TextBlock valueBlock = new()
        {
            Name = valueName,
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
    }

    private static Control CreateSectionContent(DesktopTrustReceiptSection section)
    {
        StackPanel content = new()
        {
            Spacing = 6
        };
        content.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (string line in section.Lines)
        {
            content.Children.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return content;
    }

    private async Task CopyTextAsync(string text, string successMessage)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
            _copyStatusText.Text = successMessage;
            return;
        }

        _copyStatusText.Text = "Clipboard is unavailable in this desktop host.";
    }
}
