using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.AI;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
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
        if (DesktopPreferenceStateRuntime.Current.DisableAiFeatures)
        {
            Button suppressedButton = new()
            {
                Name = controlName,
                Content = string.Empty,
                IsEnabled = false,
                IsVisible = false,
                MinWidth = 0,
                Width = 0,
                Height = 0
            };
            AutomationProperties.SetName(suppressedButton, "Explain companion unavailable");
            return suppressedButton;
        }

        string launchUri = BuildLaunchUri(request);
        Button button = new()
        {
            Name = controlName,
            Content = "Open details",
            Tag = launchUri,
            MinWidth = 176,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(button, "Open explanation details");
        AutomationProperties.SetHelpText(
            button,
            "Opens the same blocker, comparison, and system details.");
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
        return UndetectableHumanizerCopyAdapter.Humanize(
            $"{request.SurfaceLabel}: inspect {request.Title}. {string.Join(" ", sectionSummaries)}");
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
        Title = UndetectableHumanizerCopyAdapter.Humanize(request.Title);
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
            Text = UndetectableHumanizerCopyAdapter.Humanize(request.SurfaceLabel),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = UndetectableHumanizerCopyAdapter.Humanize(request.Title),
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(CreateMetadataGrid(request));
        string launchUri = request.LaunchUri ?? DesktopExplainCompanionLauncher.BuildLaunchUri(request);
        content.Children.Add(new TextBlock
        {
            Text = "Help link",
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
        AutomationProperties.SetName(launchUriTextBox, "Explanation help link");
        AutomationProperties.SetHelpText(launchUriTextBox, "Copy-safe link for reopening the same blocker, comparison, and system details.");
        content.Children.Add(launchUriTextBox);
        Button copyLaunchUriButton = new()
        {
            Name = "CopyExplainCompanionLaunchUriButton",
            Content = "Copy help link",
            MinWidth = 160,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(copyLaunchUriButton, "Copy explanation help link");
        AutomationProperties.SetHelpText(copyLaunchUriButton, "Copies the help link for the same blocker, comparison, and system details.");
        copyLaunchUriButton.Click += async (_, _) => await CopyTextAsync(launchUri, "Help link copied.").ConfigureAwait(true);
        content.Children.Add(copyLaunchUriButton);
        content.Children.Add(new TextBlock
        {
            Text = "Details",
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
        AutomationProperties.SetName(receiptTextBox, "Explanation details");
        AutomationProperties.SetHelpText(receiptTextBox, "Copy-safe details for support, blocker, comparison, and system review.");
        content.Children.Add(receiptTextBox);
        Button copyReceiptButton = new()
        {
            Name = "CopyExplainCompanionReceiptButton",
            Content = "Copy details",
            MinWidth = 160,
            Classes = { "shell-action", "quiet" }
        };
        AutomationProperties.SetName(copyReceiptButton, "Copy explanation details");
        AutomationProperties.SetHelpText(copyReceiptButton, "Copies the same blocker, comparison, and system details.");
        copyReceiptButton.Click += async (_, _) => await CopyTextAsync(_receiptText, "Details copied.").ConfigureAwait(true);
        content.Children.Add(copyReceiptButton);
        content.Children.Add(_copyStatusText);

        foreach (DesktopTrustReceiptSection section in request.Sections)
        {
            Border sectionBorder = new()
            {
                BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700"),
                Background = DesktopShellTheme.ResolveSelectionInsetBrush(),
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
            ("Ruleset", request.RulesetId, "ExplainCompanionRulesetIdText"),
            ("Workspace", request.WorkspaceId, "ExplainCompanionWorkspaceIdText"),
            ("Runtime", request.RuntimeFingerprint, "ExplainCompanionRuntimeFingerprintText"),
            ("Reference", TryExtractSectionValue(request.Sections, "reference:")
                ?? TryExtractSectionValue(request.Sections, "correlation key:"), "ExplainCompanionCorrelationKeyText"),
            ("Support note", TryExtractSectionValue(request.Sections, "next step record:")
                ?? TryExtractSectionValue(request.Sections, "handoff record:")
                ?? TryExtractSectionValue(request.Sections, "handoff receipt:"), "ExplainCompanionSupportHandoffText"),
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
            Text = UndetectableHumanizerCopyAdapter.Humanize(value),
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
            Text = UndetectableHumanizerCopyAdapter.Humanize(section.Title),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (string line in section.Lines)
        {
            content.Children.Add(new TextBlock
            {
                Text = UndetectableHumanizerCopyAdapter.Humanize(line),
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
