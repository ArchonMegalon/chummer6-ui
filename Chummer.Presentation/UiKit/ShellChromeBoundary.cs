using Chummer.Ui.Kit.Adapters;
using Chummer.Ui.Kit.Blazor.Adapters;
using System.Collections.Generic;

namespace Chummer.Presentation.UiKit;

public static class ShellChromeBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    public static readonly string RootClass = BlazorUiKitAdapter
        .AdaptShellChrome(new ShellChrome("shell", "shell"))
        .RootClass;

    public static string FormatCommandLabel(string commandId)
    {
        return string.IsNullOrWhiteSpace(commandId)
            ? string.Empty
            : commandId.Replace('_', ' ');
    }
}

public static class DesktopDialogChromeBoundary
{
    public static string BuildFailureMessage(string operationName, string message)
    {
        return $"Unable to {operationName}: {message}";
    }
}

public static class AccessibilityPrimitiveBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    private static readonly UiAdapterPayload DefaultAccessibilityPayload =
        BlazorUiKitAdapter.AdaptAccessibilityState(new AccessibilityState());
    public static readonly string RootClass = DefaultAccessibilityPayload.RootClass;
    public static readonly string StatusRegionRole = ResolveAccessibilityAttribute(DefaultAccessibilityPayload.Attributes, "role", "status");
    public const string DialogRole = "dialog";
    public static readonly string PoliteAnnouncementMode = ResolveAccessibilityAttribute(DefaultAccessibilityPayload.Attributes, "aria-live", "polite");

    public static string BuildDialogDescriptionId(string dialogId)
    {
        return string.IsNullOrWhiteSpace(dialogId)
            ? "dialog-description"
            : $"dialog-description-{dialogId}";
    }

    public static string BuildStatusAnnouncement(
        string characterState,
        string serviceState,
        string timeState,
        string complianceState)
    {
        return string.Join(
            " | ",
            new[]
            {
                characterState,
                serviceState,
                timeState,
                complianceState
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string ResolveAccessibilityAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        string fallback)
    {
        if (attributes.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }
}
