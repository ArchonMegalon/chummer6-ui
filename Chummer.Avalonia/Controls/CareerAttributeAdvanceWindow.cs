using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using TextWrapping = global::Avalonia.Media.TextWrapping;
using ScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style modal dialog for advancing an SR5 career attribute with
/// karma: quote rows (current, target, karma cost, available karma), blocker
/// reasons, an explicit confirmation, and an Undo affordance for the most recent
/// recoverable advance. Applies only after the user reads the quote and confirms;
/// mutations go exclusively through the revision-guarded Load→Preview→Confirm
/// presenter pipeline (CareerAttributeAdvanceRequest with Confirmed=true).
/// </summary>
public sealed class CareerAttributeAdvanceWindow : Window
{
    public CareerAttributeAdvanceWindow(
        CareerAttributeAdvanceEditorState editor,
        string requestedAttributeName)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAttributeName);

        Title = "Advance Attribute with Karma";
        Width = 620;
        Height = 520;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        CharacterCareerAttributeAdvanceQuote? selected = ResolveSelectedQuote(editor, requestedAttributeName);
        CareerAttributeAdvanceRequest? pendingRequest = selected is null ? null : BuildRequest(editor, selected);

        var headerText = new TextBlock
        {
            Classes = { "shell-section-title" },
            Text = "Advance Attribute with Karma",
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
        foreach (CharacterCareerAttributeAdvanceQuote quote in editor.Attributes)
        {
            quotesPanel.Children.Add(BuildQuoteCard(quote, selected));
        }

        var detailTitle = new TextBlock
        {
            Classes = { "shell-section-title" },
            Text = selected is null ? "No advanceable attribute found" : "Advance " + selected.DisplayName,
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
            IsEnabled = pendingRequest is not null && selected is { CanAdvance: true },
        };
        var cancelButton = new Button
        {
            Content = "Close",
            Classes = { "shell-action quiet" },
            MinWidth = 90,
        };
        var undoButton = new Button
        {
            Content = BuildUndoLabel(editor),
            Classes = { "shell-action quiet" },
            MinWidth = 140,
            IsEnabled = editor.RecoverableReceipts.Count > 0,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(applyButton, "career-attribute-advance-apply");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(cancelButton, "career-attribute-advance-cancel");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(undoButton, "career-attribute-advance-undo");
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(this, "career-attribute-advance-window");

        applyButton.Click += (_, _) =>
        {
            if (pendingRequest is null || selected is not { CanAdvance: true })
            {
                return;
            }

            Close(new CareerAttributeAdvanceOutcome(
                Request: pendingRequest with { Confirmed = true },
                DisplayAttribute: selected.DisplayName,
                KarmaCost: selected.KarmaCost,
                TargetValue: selected.TargetValue));
        };
        undoButton.Click += (_, _) =>
        {
            if (editor.RecoverableReceipts.Count == 0)
            {
                return;
            }

            CharacterCareerAttributeAdvanceReceipt receipt = editor.RecoverableReceipts[0];
            Close(new CareerAttributeCorrectionOutcome(
                Request: new CareerAttributeCorrectionRequest(
                    editor.WorkspaceId,
                    editor.ContentRevision,
                    receipt,
                    receipt.ReceiptDigest,
                    Confirmed: true,
                    Guid.NewGuid(),
                    "User-requested undo of the last attribute advance."),
                DisplayAttribute: receipt.Identity.Abbreviation,
                KarmaRefund: receipt.ExpenseAmount));
        };
        cancelButton.Click += (_, _) => Close(null);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            RowSpacing = 10,
            Margin = new Thickness(12),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children = { headerText, metaText },
                },
                detailTitle,
                new ScrollViewer
                {
                    Content = quotesPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                detailBody,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { undoButton, applyButton, cancelButton },
                },
            },
        };
    }

    /// <summary>Confirmed advancement, read by the owner after ShowDialog.</summary>
    public sealed record CareerAttributeAdvanceOutcome(
        CareerAttributeAdvanceRequest Request,
        string DisplayAttribute,
        int KarmaCost,
        int TargetValue);

    /// <summary>Confirmed undo of the most recent recoverable advance.</summary>
    public sealed record CareerAttributeCorrectionOutcome(
        CareerAttributeCorrectionRequest Request,
        string DisplayAttribute,
        int KarmaRefund);

    private static string BuildUndoLabel(CareerAttributeAdvanceEditorState editor)
    {
        if (editor.RecoverableReceipts.Count == 0)
        {
            return "Undo last advance";
        }

        CharacterCareerAttributeAdvanceReceipt receipt = editor.RecoverableReceipts[0];
        return "Undo last advance (" + receipt.Identity.Abbreviation + ", "
            + receipt.ExpenseAmount + " karma)";
    }

    private static CharacterCareerAttributeAdvanceQuote? ResolveSelectedQuote(
        CareerAttributeAdvanceEditorState editor,
        string requestedAttributeName)
    {
        foreach (CharacterCareerAttributeAdvanceQuote quote in editor.Attributes)
        {
            if (string.Equals(quote.Identity.Abbreviation, requestedAttributeName, StringComparison.OrdinalIgnoreCase))
            {
                return quote;
            }
        }

        return editor.Attributes.Count > 0 ? editor.Attributes[0] : null;
    }

    private static string BuildQuoteSummaryText(CharacterCareerAttributeAdvanceQuote quote)
    {
        string blockerText = quote.Blocker switch
        {
            CharacterCareerAttributeAdvanceBlocker.None => "Ready to advance.",
            CharacterCareerAttributeAdvanceBlocker.NotCareerCharacter => "Open a saved career runner before advancing attributes.",
            CharacterCareerAttributeAdvanceBlocker.UnsupportedRuleset => "This ruleset does not support karma advancement here.",
            CharacterCareerAttributeAdvanceBlocker.ForeignTarget => "This attribute belongs to another runner.",
            CharacterCareerAttributeAdvanceBlocker.SpecialAttributeDisabled => "Special attribute advancement is not enabled for this runner.",
            CharacterCareerAttributeAdvanceBlocker.AtNaturalMaximum => "Already at the natural maximum for this metatype.",
            CharacterCareerAttributeAdvanceBlocker.InsufficientKarma => "Not enough unspent karma for this advance.",
            _ => quote.Blocker.ToString(),
        };

        return "Current " + quote.EffectiveValue + " -> target " + quote.TargetValue
            + "  |  cost " + quote.KarmaCost + " karma"
            + "  |  available " + quote.AvailableKarma
            + (quote.RepairsBurnedEdge ? "  |  repairs burned Edge" : string.Empty)
            + Environment.NewLine + blockerText;
    }

    private static CareerAttributeAdvanceRequest BuildRequest(
        CareerAttributeAdvanceEditorState editor,
        CharacterCareerAttributeAdvanceQuote quote)
    {
        return new CareerAttributeAdvanceRequest(
            editor.WorkspaceId,
            editor.ContentRevision,
            quote,
            quote.LogicalRevision,
            quote.SourceRevision,
            quote.RuleDigest,
            Confirmed: false,
            Guid.NewGuid(),
            DateTime.Now);
    }

    private static Control BuildQuoteCard(
        CharacterCareerAttributeAdvanceQuote quote,
        CharacterCareerAttributeAdvanceQuote? selected)
    {
        bool isSelected = selected is not null && string.Equals(
            selected.Identity.Abbreviation,
            quote.Identity.Abbreviation,
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
                        Text = (isSelected ? "> " : string.Empty) + quote.DisplayName
                            + "  |  " + quote.EffectiveValue + " -> " + quote.TargetValue
                            + "  |  " + quote.KarmaCost + " karma",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock
                    {
                        Classes = { "shell-caption" },
                        Text = (quote.CanAdvance ? "Ready" : "Blocked: " + quote.Blocker)
                            + (quote.RepairsBurnedEdge ? "  |  repairs burned Edge" : string.Empty),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(
            card, "career-attribute-advance-quote-" + quote.Identity.Abbreviation);
        return card;
    }
}
