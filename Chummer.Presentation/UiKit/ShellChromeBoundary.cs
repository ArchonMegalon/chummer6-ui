namespace Chummer.Presentation.UiKit;

public static class ShellChromeBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    public const string LocalAdapterMarker = "TODO: replace presentation-local shell chrome adapter with Chummer.Ui.Kit package consumption.";

    public static string FormatCommandLabel(string commandId)
    {
        return string.IsNullOrWhiteSpace(commandId)
            ? string.Empty
            : commandId.Replace('_', ' ');
    }
}

public static class DesktopDialogChromeBoundary
{
    public const string LocalAdapterMarker = "TODO: replace presentation-local desktop dialog chrome with Chummer.Ui.Kit dialog shell primitives.";

    public static string BuildFailureMessage(string operationName, string message)
    {
        return $"Unable to {operationName}: {message}";
    }
}

public static class AccessibilityPrimitiveBoundary
{
    public const string PackageId = "Chummer.Ui.Kit";
    public const string LocalAdapterMarker = "TODO: replace presentation-local accessibility/state primitives with Chummer.Ui.Kit focus, announcement, and selection helpers.";
    public const string StatusRegionRole = "status";
    public const string DialogRole = "dialog";
    public const string PoliteAnnouncementMode = "polite";

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
}
