using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal static class DesktopTrustPanelFactory
{
    public static Control? CreateDialogPanel(DesktopDialogState dialog, string detailText)
    {
        IReadOnlyList<DesktopTrustReceiptSection> sections = DesktopTrustReceiptText.BuildDialogReceiptSections(dialog);
        if (sections.Count == 0)
        {
            sections = BuildFallbackSections(detailText);
        }

        if (sections.Count == 0)
        {
            return null;
        }

        return CreatePanel(
            ResolveDialogHeading(sections),
            sections,
            new DesktopExplainCompanionRequest(
                Title: ResolveDialogCompanionTitle(dialog, sections),
                SurfaceId: ResolveDialogSurfaceId(sections),
                SurfaceLabel: ResolveDialogSurfaceLabel(sections),
                Sections: sections,
                SurfaceFamilyId: "explain_details:desktop",
                RulesetId: ResolveDialogRulesetId(dialog, sections)),
            "OpenDesktopDialogExplainCompanionButton");
    }

    public static Control? CreateDiagnosticsPanel(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<string> rawLines)
    {
        IReadOnlyList<DesktopTrustReceiptSection> sections = DesktopTrustReceiptText.BuildDiagnosticsSections(installState, updateStatus);
        if (sections.Count == 0)
        {
            sections = BuildFallbackSections(rawLines);
        }

        if (sections.Count == 0)
        {
            return null;
        }

        return CreatePanel(
            "Support diagnostics and environment details",
            sections,
            new DesktopExplainCompanionRequest(
                Title: "Support blocker explanation",
                SurfaceId: "explain_receipts:desktop.blocker",
                SurfaceLabel: "Desktop blocker diagnostics explanation",
                Sections: sections,
                SurfaceFamilyId: "explain_details:desktop",
                RuntimeFingerprint: updateStatus.HeadId),
            "OpenDesktopBlockerExplainCompanionButton");
    }

    public static Control? CreateCrashDiagnosticsPanel(
        DesktopCrashReport report,
        IReadOnlyList<string> rawLines)
    {
        IReadOnlyList<DesktopTrustReceiptSection> sections = DesktopTrustReceiptText.BuildCrashDiagnosticsSections(report);
        if (sections.Count == 0)
        {
            sections = BuildFallbackSections(rawLines);
        }

        if (sections.Count == 0)
        {
            return null;
        }

        return CreatePanel(
            "Crash diagnostics and environment details",
            sections,
            new DesktopExplainCompanionRequest(
                Title: "Crash blocker explanation",
                SurfaceId: "explain_receipts:desktop.blocker",
                SurfaceLabel: "Desktop crash blocker explanation",
                Sections: sections,
                SurfaceFamilyId: "explain_details:desktop",
                RuntimeFingerprint: report.HeadId),
            "OpenDesktopCrashBlockerExplainCompanionButton");
    }

    private static Control CreatePanel(
        string heading,
        IReadOnlyList<DesktopTrustReceiptSection> sections,
        DesktopExplainCompanionRequest companionRequest,
        string companionButtonName)
    {
        Border panel = new()
        {
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSelectionPanelBrush", "#F8FAFC"),
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellWarningBrush", "#9A6700"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14)
        };
        StackPanel content = new()
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        content.Children.Add(DesktopExplainCompanionLauncher.CreateLaunchButton(
            panel,
            companionRequest,
            companionButtonName));

        foreach (DesktopTrustReceiptSection section in sections)
        {
            StackPanel sectionPanel = new()
            {
                Spacing = 4
            };
            sectionPanel.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

            foreach (string line in section.Lines)
            {
                sectionPanel.Children.Add(new TextBlock
                {
                    Text = $"- {line}",
                    TextWrapping = TextWrapping.Wrap
                });
            }

            content.Children.Add(sectionPanel);
        }

        panel.Child = content;
        return panel;
    }

    private static string ResolveDialogHeading(IReadOnlyList<DesktopTrustReceiptSection> sections)
        => sections.Any(static section => section.Lines.Any(static line =>
            line.Contains("Import target:", StringComparison.Ordinal)
            || line.Contains("Import rule-environment record:", StringComparison.Ordinal)
            || line.Contains("Import reference:", StringComparison.Ordinal)
            || line.Contains("Import record correlation key:", StringComparison.Ordinal)))
            ? "Import explanation and environment details"
            : "Explanation and environment details";

    private static string ResolveDialogCompanionTitle(
        DesktopDialogState dialog,
        IReadOnlyList<DesktopTrustReceiptSection> sections)
    {
        string title = string.IsNullOrWhiteSpace(dialog.Title)
            ? ResolveDialogSurfaceLabel(sections)
            : dialog.Title.Trim();
        return $"{title} explanation";
    }

    private static string ResolveDialogSurfaceId(IReadOnlyList<DesktopTrustReceiptSection> sections)
        => ContainsLine(sections, "Import reference:")
            || ContainsLine(sections, "Import record correlation key:")
            ? "explain_details:desktop.import"
            : "explain_details:desktop.dialog";

    private static string ResolveDialogSurfaceLabel(IReadOnlyList<DesktopTrustReceiptSection> sections)
        => ContainsLine(sections, "Import reference:")
            || ContainsLine(sections, "Import record correlation key:")
            ? "Desktop import explanation"
            : "Desktop explanation";

    private static string? ResolveDialogRulesetId(
        DesktopDialogState dialog,
        IReadOnlyList<DesktopTrustReceiptSection> sections)
    {
        DesktopDialogField? rulesetField = dialog.Fields.FirstOrDefault(static field =>
            string.Equals(field.Id, "importRulesetId", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(rulesetField?.Value))
        {
            return rulesetField.Value.Trim();
        }

        string? ruleEnvironmentLine = sections
            .SelectMany(static section => section.Lines)
            .FirstOrDefault(static line => line.StartsWith("Import target: ", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(ruleEnvironmentLine))
        {
            return null;
        }

        const string prefix = "Import target: ";
        string tail = ruleEnvironmentLine[prefix.Length..];
        int end = tail.IndexOf(' ', StringComparison.Ordinal);
        return (end >= 0 ? tail[..end] : tail).Trim();
    }

    private static bool ContainsLine(IReadOnlyList<DesktopTrustReceiptSection> sections, string value)
        => sections.Any(section => section.Lines.Any(line => line.Contains(value, StringComparison.Ordinal)));

    private static IReadOnlyList<DesktopTrustReceiptSection> BuildFallbackSections(string? receiptText)
    {
        if (string.IsNullOrWhiteSpace(receiptText))
        {
            return [];
        }

        string[] lines = receiptText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return BuildFallbackSections(lines);
    }

    private static IReadOnlyList<DesktopTrustReceiptSection> BuildFallbackSections(IReadOnlyList<string> lines)
    {
        List<string> normalizedLines = lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Trim())
            .ToList();
        if (normalizedLines.Count == 0)
        {
            return [];
        }

        return [new DesktopTrustReceiptSection("Details", normalizedLines)];
    }
}
