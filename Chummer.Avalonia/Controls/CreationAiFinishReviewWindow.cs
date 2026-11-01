using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TextWrapping = global::Avalonia.Media.TextWrapping;
using FontStyle = global::Avalonia.Media.FontStyle;
using ScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility;
using Chummer.Avalonia.Creation;
using Chummer.Presentation.Creation;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style modal review dialog for the Build Ghost AI-finish
/// proposal: recommendation rows with per-row rationale, manual follow-ups,
/// and an explicit Apply/Cancel footer. No auto-apply: the user reads and
/// explicitly confirms each run.
/// </summary>
public sealed class CreationAiFinishReviewWindow : Window
{
    public CreationAiFinishReviewWindow(CreationAiFinishService.AiFinishProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        Title = "Build Ghost — finish plan review";
        Width = 560;
        Height = 440;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var archetype = proposal.Archetype.ArchetypeId + " · " +
            proposal.Archetype.Evidence.Count + " evidence items · " +
            (proposal.Archetype.HasRecommendationPack ? "pack available" : "no pack — generic advice only");

        var rowsPanel = new StackPanel { Spacing = 6 };
        foreach (var row in proposal.Recommendations)
        {
            rowsPanel.Children.Add(new Border
            {
                Classes = { "shell-card" },
                Padding = new Thickness(10, 8),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Classes = { "shell-section-title" },
                            Text = row.Domain + " — " + row.Label,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Classes = { "shell-caption" },
                            Text = row.Rationale,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Classes = { "shell-caption" },
                            FontStyle = FontStyle.Italic,
                            Text = row.ApplyAction,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            });
        }

        if (proposal.ManualFollowUps.Length > 0)
        {
            rowsPanel.Children.Add(new TextBlock
            {
                Classes = { "shell-caption" },
                Text = "Manual follow-ups: " + string.Join("; ", proposal.ManualFollowUps),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var applyButton = new Button
        {
            Content = "Apply plan",
            Classes = { "shell-action" },
            MinWidth = 110,
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "shell-action quiet" },
            MinWidth = 90,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(applyButton, "creation-ai-finish-apply");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(cancelButton, "creation-ai-finish-cancel");

        applyButton.Click += (_, _) => { Close(true); };
        cancelButton.Click += (_, _) => { Close(false); };

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Classes = { "shell-section-title" },
                            Text = "Recommended missing-only plan",
                        },
                        new TextBlock
                        {
                            Classes = { "shell-caption" },
                            Text = "Archetype: " + archetype +
                                " · fills only untouched gaps; your existing choices are kept.",
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
                new ScrollViewer
                {
                    Content = rowsPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, applyButton },
                },
            },
        };
    }

    public void Close(bool result) => base.Close(result);
}
