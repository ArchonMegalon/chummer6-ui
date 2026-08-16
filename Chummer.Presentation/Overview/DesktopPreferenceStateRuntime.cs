namespace Chummer.Presentation.Overview;

public static class DesktopPreferenceStateRuntime
{
    private static DesktopPreferenceState _current = DesktopPreferenceState.Default;

    public static DesktopPreferenceState Current
        => _current;

    public static void SetCurrent(DesktopPreferenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _current = Normalize(state);
    }

    public static DesktopPreferenceState Normalize(DesktopPreferenceState state)
        => state with
        {
            Theme = string.IsNullOrWhiteSpace(state.Theme) ? DesktopPreferenceState.Default.Theme : state.Theme.Trim(),
            Language = DesktopLocalizationCatalog.NormalizeOrDefault(state.Language),
            SheetLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(string.IsNullOrWhiteSpace(state.SheetLanguage) ? state.Language : state.SheetLanguage),
            CharacterPriority = string.IsNullOrWhiteSpace(state.CharacterPriority) ? DesktopPreferenceState.Default.CharacterPriority : state.CharacterPriority.Trim(),
            CharacterNotes = state.CharacterNotes ?? string.Empty,
            StartupBehavior = string.IsNullOrWhiteSpace(state.StartupBehavior) ? DesktopPreferenceState.Default.StartupBehavior : state.StartupBehavior.Trim(),
            UpdateChannel = string.IsNullOrWhiteSpace(state.UpdateChannel) ? DesktopPreferenceState.Default.UpdateChannel : state.UpdateChannel.Trim(),
            UpdateMode = NormalizeUpdateMode(state.UpdateMode, state.CheckForUpdatesOnLaunch),
            CheckForUpdatesOnLaunch = NormalizeUpdateMode(state.UpdateMode, state.CheckForUpdatesOnLaunch) != "off",
            CharacterRosterPath = string.IsNullOrWhiteSpace(state.CharacterRosterPath) ? DesktopPreferenceState.Default.CharacterRosterPath : state.CharacterRosterPath.Trim(),
            RosterHierarchyJson = NormalizeRosterHierarchyJson(state.RosterHierarchyJson),
            PdfViewerPath = string.IsNullOrWhiteSpace(state.PdfViewerPath) ? DesktopPreferenceState.Default.PdfViewerPath : state.PdfViewerPath.Trim(),
            VisibleChromePolicy = string.IsNullOrWhiteSpace(state.VisibleChromePolicy) ? DesktopPreferenceState.Default.VisibleChromePolicy : state.VisibleChromePolicy.Trim(),
            CharacterSettingsCatalogJson = state.CharacterSettingsCatalogJson ?? string.Empty
        };

    private static string NormalizeRosterHierarchyJson(string? hierarchyJson)
        => RosterHierarchyStateJson.Normalize(hierarchyJson);

    public static string NormalizeUpdateMode(string? updateMode, bool fallbackCheckForUpdatesOnLaunch = true)
    {
        if (string.IsNullOrWhiteSpace(updateMode))
        {
            return fallbackCheckForUpdatesOnLaunch ? "full" : "off";
        }

        return updateMode.Trim().ToLowerInvariant().Replace("_", "-") switch
        {
            "full" or "auto" or "automatic" or "full-auto" or "full-autoupdate" => "full",
            "notify" or "notification" or "notify-only" or "manual" => "notify",
            "off" or "disabled" or "disable" or "none" => "off",
            _ => fallbackCheckForUpdatesOnLaunch ? "full" : "off"
        };
    }
}
