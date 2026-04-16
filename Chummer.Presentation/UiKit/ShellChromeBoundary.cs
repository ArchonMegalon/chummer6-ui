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

    private static readonly IReadOnlyDictionary<string, string> CommandLabels = new Dictionary<string, string>(System.StringComparer.Ordinal)
    {
        ["file"] = "File",
        ["edit"] = "Edit",
        ["special"] = "Special",
        ["tools"] = "Tools",
        ["windows"] = "Windows",
        ["help"] = "Help",
        ["new_character"] = "New Character",
        ["open_character"] = "Open...",
        ["save_character"] = "Save",
        ["save_character_as"] = "Save As...",
        ["print_character"] = "Print...",
        ["copy"] = "Copy",
        ["paste"] = "Paste",
        ["close_window"] = "Close",
        ["close_all"] = "Close All",
        ["export_character"] = "Export...",
        ["dice_roller"] = "Dice Roller",
        ["global_settings"] = "Options",
        ["master_index"] = "Master Index",
        ["character_roster"] = "Character Roster",
        ["report_bug"] = "Report Issue",
        ["about"] = "About",
        ["update"] = "Update"
    };

    public static string FormatCommandLabel(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return string.Empty;
        }

        return CommandLabels.TryGetValue(commandId, out string? label)
            ? label
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
