using System;
using System.Linq;
using System.Collections.Generic;
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
    /// <summary>
    /// Gen17 (Controller8300, T660): live-instance registry so same-modal
    /// resolution can enumerate exactly the advance windows that exist, in
    /// BOTH the production desktop app and Avalonia headless test sessions
    /// (where no IClassicDesktopStyleApplicationLifetime exists to walk).
    /// Instances register on construction and unregister on Closed.
    /// </summary>
    private static readonly List<Sr6CareerAttributeAdvanceWindow> LiveInstances = new();

    internal static IReadOnlyList<Sr6CareerAttributeAdvanceWindow> LiveInstancesSnapshot
    {
        get
        {
            lock (LiveInstances)
            {
                return LiveInstances.ToList();
            }
        }
    }

    public Sr6CareerAttributeAdvanceWindow(
        Sr6CareerAttributeAdvanceEditorSnapshot editor,
        string requestedAttributeName)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAttributeName);

        lock (LiveInstances)
        {
            LiveInstances.Add(this);
        }
        Closed += (_, _) =>
        {
            lock (LiveInstances)
            {
                LiveInstances.Remove(this);
            }
        };

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
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(detailTitle, "sr6-advance-detail-title");
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
        var blockedNotice = new TextBlock
        {
            Classes = { "shell-caption" },
            Text = selected is { CanAdvance: false }
                ? "Core refused this advance: " + selected.Blocker
                : string.Empty,
            IsVisible = selected is { CanAdvance: false },
            TextWrapping = TextWrapping.Wrap,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(blockedNotice, "sr6-advance-blocker-notice");
        var cancelButton = new Button
        {
            Content = "Close",
            Classes = { "shell-action quiet" },
            MinWidth = 90,
        };
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
        global::Avalonia.Controls.Grid.SetRow(blockedNotice, 4);
        global::Avalonia.Controls.Grid.SetRow(actionsPanel, 5);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            RowSpacing = 10,
            Margin = new Thickness(12),
            Children =
            {
                headerPanel,
                detailTitle,
                quotesScroll,
                detailBody,
                blockedNotice,
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

    /// <summary>
    /// Gen16 (Controller8283 T656): the typed blocker code is the FIRST
    /// whitespace-delimited token after the refusal prefix. Comparison is
    /// exact string equality - unrelated or mixed text that merely CONTAINS
    /// a typed marker can never satisfy a typed refusal.
    /// </summary>
    internal const string AdvanceRefusalPrefix = "Core refused this advance: ";

    private static readonly char[] TypedBlockerCodeTerminators = { ' ', '\t', '\r', '\n', ':' };

    internal static string? ExtractTypedBlockerCode(string? text, string prefix)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        int start = text.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;
        string remainder = text[(start + prefix.Length)..];
        int end = remainder.IndexOfAny(TypedBlockerCodeTerminators);
        string candidate = end < 0 ? remainder : remainder[..end];
        return candidate.Length == 0 ? null : candidate;
    }

    internal static Sr6AdvanceBlockerClassification ClassifyAdvanceBlocker(string? noticeText)
    {
        string? code = ExtractTypedBlockerCode(noticeText, AdvanceRefusalPrefix);
        bool visible = code is not null;
        return new Sr6AdvanceBlockerClassification(
            Visible: visible,
            NaturalCap: string.Equals(code, "sr6_attribute_natural_limit", StringComparison.Ordinal),
            InsufficientKarma: string.Equals(code, "sr6_attribute_insufficient_karma", StringComparison.Ordinal),
            Unrelated: visible
                && !string.Equals(code, "sr6_attribute_natural_limit", StringComparison.Ordinal)
                && !string.Equals(code, "sr6_attribute_insufficient_karma", StringComparison.Ordinal),
            TypedCode: code);
    }

    internal sealed record Sr6AdvanceBlockerClassification(
        bool Visible,
        bool NaturalCap,
        bool InsufficientKarma,
        bool Unrelated,
        string? TypedCode);

    /// <summary>
    /// Gen16 (Controller8283 T656): the SHARED cap-control verdict used by
    /// both the journey runner and the focused tests. The natural-cap leg
    /// only passes when the active advance dialog shows a present,
    /// effectively-visible, DISABLED Apply control plus the visible
    /// "Advance &lt;attribute&gt;" title bound to the requested attribute. A
    /// missing, hidden or enabled Apply must fail - never succeed vacuously.
    /// </summary>
    internal sealed record Sr6CapControlVerdict(
        bool ApplyPresent,
        bool ApplyVisible,
        bool ApplyDisabled,
        bool TitleBound,
        bool Ok,
        string Diagnostics);

    internal static Sr6CapControlVerdict ClassifyCapControls(
        bool applyPresent,
        bool applyEffectivelyVisible,
        bool applyEffectivelyEnabled,
        string? detailTitleText,
        bool detailTitleVisible,
        string requestedAttributeId)
    {
        bool applyDisabled = applyPresent && applyEffectivelyVisible && !applyEffectivelyEnabled;
        bool titleBound = detailTitleVisible
            && string.Equals(detailTitleText, "Advance " + requestedAttributeId, StringComparison.Ordinal);
        bool ok = applyDisabled && titleBound;
        string diagnostics = "applyPresent=" + applyPresent
            + ";applyVisible=" + applyEffectivelyVisible
            + ";applyDisabled=" + applyDisabled
            + ";titleVisible=" + detailTitleVisible
            + ";titleText=" + (detailTitleText ?? "null")
            + ";requested=" + requestedAttributeId;
        return new Sr6CapControlVerdict(applyPresent, applyEffectivelyVisible, applyDisabled, titleBound, ok, diagnostics);
    }

    internal static Sr6CareerAttributeCommand BuildCommand(Sr6CareerAttributeQuote quote)
    {
        return new Sr6CareerAttributeCommand(
            quote,
            Guid.NewGuid(),
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
