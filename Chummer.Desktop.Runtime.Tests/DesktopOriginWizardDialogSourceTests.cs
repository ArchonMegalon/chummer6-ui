#nullable enable

using System;
using System.IO;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopOriginWizardDialogSourceTests
{
    [TestMethod]
    public void Origin_wizard_advanced_story_controls_keep_expanded_state_across_rerenders()
    {
        string repoRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        string source = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Blazor",
            "Components",
            "Shell",
            "DialogHost.razor"));

        Assert.Contains("@if (GetRenderedDialog() is { } dialog)", source, StringComparison.Ordinal);
        Assert.Contains("data-origin-advanced-controls", source, StringComparison.Ordinal);
        Assert.Contains("data-expanded=\"@(CurrentOriginWizardAdvancedControlsOpen ? \"true\" : \"false\")\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"@(CurrentOriginWizardAdvancedControlsOpen ? \"true\" : \"false\")\"", source, StringComparison.Ordinal);
        Assert.Contains("hidden=\"@(!CurrentOriginWizardAdvancedControlsOpen)\"", source, StringComparison.Ordinal);
        Assert.Contains("ToggleOriginWizardAdvancedControls", source, StringComparison.Ordinal);
        Assert.Contains("RememberOriginWizardTransientState();", source, StringComparison.Ordinal);
        Assert.Contains("CaptureDialogScrollForFieldInteractionAsync", source, StringComparison.Ordinal);
        Assert.Contains("RestorePendingDialogScrollAsync", source, StringComparison.Ordinal);
        Assert.Contains("builder.SetKey($\"dialog-select:{field.Id}\");", source, StringComparison.Ordinal);
        Assert.Contains("builder.SetKey($\"dialog-origin-field:{field.Id}\");", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Origin_wizard_advanced_story_controls_preserve_avalonia_scroll_anchor_across_combo_interactions()
    {
        string repoRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        string xamlSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "DesktopDialogWindow.axaml"));
        string codeSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "DesktopDialogWindow.axaml.cs"));
        string appSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Blazor",
            "Components",
            "App.razor"));

        Assert.Contains("Name=\"DialogScrollViewer\"", xamlSource, StringComparison.Ordinal);
        Assert.Contains("private const string OriginWizardDialogId = \"dialog.new_character.origin_wizard\";", codeSource, StringComparison.Ordinal);
        Assert.Contains("OriginWizardAdvancedStoryControlsExpanderName", codeSource, StringComparison.Ordinal);
        Assert.Contains("_dialogScrollViewer = this.FindControl<ScrollViewer>(\"DialogScrollViewer\")!;", codeSource, StringComparison.Ordinal);
        Assert.Contains("PrepareComboBoxForDialogStatePreservation(comboBox, comboBoxBindVersion);", codeSource, StringComparison.Ordinal);
        Assert.Contains("_preferredDialogScrollAnchor ??= _dialogScrollViewer.Offset;", codeSource, StringComparison.Ordinal);
        Assert.Contains("CapturePreferredDialogViewportAnchor();", codeSource, StringComparison.Ordinal);
        Assert.Contains("CapturePreferredDialogInteractionAnchor(", codeSource, StringComparison.Ordinal);
        Assert.Contains("RestorePreferredScrollAnchorDuringOriginWizardComboInteraction();", codeSource, StringComparison.Ordinal);
        Assert.Contains("RestorePreferredScrollOffset(dialog.Id, preservedScrollOffset, preservedViewportAnchor, preservedInteractionAnchor);", codeSource, StringComparison.Ordinal);
        Assert.Contains("bool hasPreferredInteractionAnchor = preservedInteractionAnchor is not null", codeSource, StringComparison.Ordinal);
        Assert.Contains("if (hasPreferredInteractionAnchor", codeSource, StringComparison.Ordinal);
        Assert.Contains("if (preservedInteractionAnchor is null && preservedViewportAnchor is { } viewportAnchor)", codeSource, StringComparison.Ordinal);
        Assert.Contains("ShouldPreserveOriginWizardComboInteractionScroll()", codeSource, StringComparison.Ordinal);
        Assert.Contains("bool hasPreferredAnchor = (preservedViewportAnchor is not null || preservedInteractionAnchor is not null)", codeSource, StringComparison.Ordinal);
        Assert.Contains("bool hasPreferredAnchor = _preferredDialogViewportAnchor is { } || _preferredDialogInteractionAnchor is { };", codeSource, StringComparison.Ordinal);
        Assert.Contains("if (!hasPreferredAnchor)", codeSource, StringComparison.Ordinal);
        Assert.Contains("if (!hasPreferredAnchor && _preferredDialogScrollAnchor is Vector anchor)", codeSource, StringComparison.Ordinal);
        Assert.Contains("ApplyPreferredDialogViewportAnchor(", codeSource, StringComparison.Ordinal);
        Assert.Contains("ApplyPreferredDialogInteractionAnchor(", codeSource, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer.RunOnce(", codeSource, StringComparison.Ordinal);
        Assert.Contains("const hasOriginAnchor = function()", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs.clearPendingOriginAnchors = function()", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs.hasPendingOriginAnchor = function(dialogId)", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs._pendingOriginAnchorCapturedAtMs", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs._originSameDialogAnchorGraceWindowMs", appSource, StringComparison.Ordinal);
        Assert.Contains("return !!window.chummerDialogs._pendingOriginFieldAnchor", appSource, StringComparison.Ordinal);
        Assert.Contains("const restore = function()", appSource, StringComparison.Ordinal);
        Assert.Contains("const needsOriginAnchorFallback = hasOriginAnchor()", appSource, StringComparison.Ordinal);
        Assert.Contains("if (needsOriginAnchorFallback && !restoreOriginFieldAnchor())", appSource, StringComparison.Ordinal);
        Assert.Contains("restoreOriginAdvancedAnchor();", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs.hasPendingOriginAnchor(window.chummerDialogs._lastRevealedDialogId || null)", appSource, StringComparison.Ordinal);
        Assert.Contains("window.chummerDialogs.hasPendingOriginAnchor(dialogId)", appSource, StringComparison.Ordinal);
        Assert.Contains("fieldAnchorElement.closest('[data-origin-wizard]')", appSource, StringComparison.Ordinal);
        Assert.Contains("restore();", appSource, StringComparison.Ordinal);
        Assert.Contains("window.requestAnimationFrame(() => restore());", appSource, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout(() => restore(), 96);", appSource, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout(() => restore(), 192);", appSource, StringComparison.Ordinal);
    }
}
