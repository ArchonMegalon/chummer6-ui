using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using TextWrapping = global::Avalonia.Media.TextWrapping;
using ScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility;

using Chummer.Application.Owners;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style modal dialog for advancing an SR6 career attribute with
/// karma, routed through Core's owner-bound advancement service (Core owns the
/// rules and performs the atomic graph/expense/receipt commit). Quote rows show
/// only Core-provided fields (natural rating, target, cost, available karma,
/// blocker); the apply action issues a Core command with ExplicitlyConfirmed=true
/// and never mutates dossier XML from the UI.
/// </summary>
public sealed class Sr6CareerAttributeAdvanceWindow : Window
{
    public Sr6CareerAttributeAdvanceWindow(
        Sr6CareerAttributeAdvanceEditorSnapshot editor,
        string requestedAttributeName)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAttributeName);

        Title = "Advance Attribute with Karma (SR6)";
        Width = 620;
        Height = 520;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Sr6CareerAttributeQuote? selected = ResolveSelectedQuote(editor, requestedAttributeName);
        Sr6CareerAttributeCommand? pendingCommand = selected is null ? null : BuildCommand(selected);

        var headerText = new TextBlock
        {
            Classes = { "shell-section-title" },
            Text = "Advance Attribute with Karma (SR6)",
        };
        var metaText = new TextBlock
        {
            Classes = { "shell-caption" },
            Text = "Workspace: " + editor.WorkspaceId.Value
                + "  |  revision " + editor.ContentRevision.ToString(CultureInfo.InvariantCulture)
                + "  |  " + editor.Attributes.Count + " advanceable attribute(s)"
                + (editor.OmittedAttributeCount > 0
                    ? " (" + editor.OmittedAttributeCount + " omitted)"
                    : string.Empty),
            TextWrapping = TextWrapping.Wrap,
        };

        var quotesPanel = new StackPanel { Spacing = 6 };
        foreach (Sr6CareerAttributeQuote quote in editor.Attributes)
        {
            quotesPanel.Children.Add(BuildQuoteCard(quote, selected));
        }

        var detailTitle = new TextBlock
        {
            Classes = { "shell-section-title" },
            Text = selected is null ? "No advanceable attribute found" : "Advance " + selected.AttributeId,
        };
        var detailBody = new TextBlock
        {
            Classes = { "shell-caption" },
            Text = selected is null
                ? "This runner has no karma-advanceable attributes (or none matched the requested one)."
                : BuildQuoteSummaryText(selected),
            TextWrapping = TextWrapping.Wrap,
        };

        var applyButton = new Button
        {
            Content = "Advance (spend karma)",
            Classes = { "shell-action" },
            MinWidth = 150,
            IsEnabled = pendingCommand is not null && selected is { CanAdvance: true },
        };
        var cancelButton = new Button
        {
            Content = "Close",
            Classes = { "shell-action quiet" },
            MinWidth = 90,
        };
        // T492: diagnostics - the runner captures the quote summary (incl. any blocker).
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(detailBody, "sr6-career-attribute-advance-detail");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(applyButton, "sr6-career-attribute-advance-apply");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(cancelButton, "sr6-career-attribute-advance-cancel");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(this, "sr6-career-attribute-advance-window");

        applyButton.Click += (_, _) =>
        {
            if (pendingCommand is null || selected is not { CanAdvance: true })
            {
                return;
            }

            Close(new Sr6CareerAttributeAdvanceOutcome(
                Command: pendingCommand with { ExplicitlyConfirmed = true },
                Owner: editor.Owner,
                DisplayAttribute: selected.AttributeId,
                KarmaCost: selected.KarmaCost,
                TargetNaturalRating: selected.TargetNaturalRating));
        };
        cancelButton.Click += (_, _) => Close(null);

        var headerPanel = new StackPanel
        {
            Spacing = 2,
            Children = { headerText, metaText },
        };
        var quotesScroll = new ScrollViewer
        {
            Content = quotesPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { applyButton, cancelButton },
        };
        global::Avalonia.Controls.Grid.SetRow(headerPanel, 0);
        global::Avalonia.Controls.Grid.SetRow(detailTitle, 1);
        global::Avalonia.Controls.Grid.SetRow(quotesScroll, 2);
        global::Avalonia.Controls.Grid.SetRow(detailBody, 3);
        global::Avalonia.Controls.Grid.SetRow(actionsPanel, 4);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            RowSpacing = 10,
            Margin = new Thickness(12),
            Children =
            {
                headerPanel,
                detailTitle,
                quotesScroll,
                detailBody,
                actionsPanel,
            },
        };
    }

    /// <summary>Confirmed advancement, read by the owner after ShowDialog.</summary>
    public sealed record Sr6CareerAttributeAdvanceOutcome(
        Sr6CareerAttributeCommand Command,
        OwnerContextStamp Owner,
        string DisplayAttribute,
        int KarmaCost,
        int TargetNaturalRating);

    private static Sr6CareerAttributeQuote? ResolveSelectedQuote(
        Sr6CareerAttributeAdvanceEditorSnapshot editor,
        string requestedAttributeName)
    {
        string requested = NormalizeAttributeIdentity(requestedAttributeName);
        foreach (Sr6CareerAttributeQuote quote in editor.Attributes)
        {
            if (string.Equals(
                    NormalizeAttributeIdentity(quote.AttributeId),
                    requested,
                    StringComparison.OrdinalIgnoreCase))
            {
                return quote;
            }
        }

        // Unknown or unmatched requests must not silently select a different
        // attribute: refuse (null) so the caller reports "not found" instead of
        // offering another attribute's quote.
        return null;
    }

    /// <summary>
    /// Maps supported attribute identities (full names and the standard
    /// three-letter abbreviations used by saved projections) to the canonical
    /// full name. Unknown identities are returned unchanged so the comparison
    /// simply fails and the request is refused.
    /// </summary>
    private static string NormalizeAttributeIdentity(string name)
    {
        return name.Trim().ToUpperInvariant() switch
        {
            "AGI" => "Agility",
            "REA" => "Reaction",
            "STR" => "Strength",
            "CHA" => "Charisma",
            "WIL" => "Willpower",
            "INT" => "Intuition",
            "LOG" => "Logic",
            "BOD" => "Body",
            _ => name.Trim(),
        };
    }

    private static string BuildQuoteSummaryText(Sr6CareerAttributeQuote quote)
    {
        string blockerText = string.IsNullOrEmpty(quote.Blocker)
            ? "Ready to advance."
            : "Blocked: " + quote.Blocker;

        return "Current " + quote.EffectiveRating + " (natural " + quote.NaturalRating
            + (quote.AugmentationBonus > 0 ? " +" + quote.AugmentationBonus + " augmentation" : string.Empty)
            + ") -> target " + quote.TargetNaturalRating
            + "  |  cost " + quote.KarmaCost + " karma"
            + "  |  available " + quote.AvailableKarma
            + Environment.NewLine + blockerText;
    }

    private static Sr6CareerAttributeCommand BuildCommand(Sr6CareerAttributeQuote quote)
    {
        return new Sr6CareerAttributeCommand(
            quote,
            Guid.NewGuid(),
            // Core's Sr6CareerAttributeIntegrity.IsConfirmed requires ExpenseDateLocal.Kind == Unspecified; a Local-kind DateTime fails validation (R109 finding).
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            ExplicitlyConfirmed: false);
    }

    private static Control BuildQuoteCard(
        Sr6CareerAttributeQuote quote,
        Sr6CareerAttributeQuote? selected)
    {
        bool isSelected = selected is not null && string.Equals(
            selected.AttributeId,
            quote.AttributeId,
            StringComparison.OrdinalIgnoreCase);
        var card = new Border
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
                        Text = (isSelected ? "> " : string.Empty) + quote.AttributeId
                            + "  |  " + quote.EffectiveRating + " -> " + quote.TargetNaturalRating
                            + "  |  " + quote.KarmaCost + " karma",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock
                    {
                        Classes = { "shell-caption" },
                        Text = (quote.CanAdvance ? "Ready" : "Blocked: " + quote.Blocker),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(
            card, "sr6-career-attribute-advance-quote-" + quote.AttributeId);
        return card;
    }
}
