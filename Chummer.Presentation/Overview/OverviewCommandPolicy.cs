using Chummer.Contracts.Presentation;
using System.Linq;

namespace Chummer.Presentation.Overview;

public static class OverviewCommandPolicy
{
    public const string RuntimeInspectorCommandId = AppCommandIds.RuntimeInspector;

    private static readonly HashSet<string> MenuCommandIds = new(StringComparer.Ordinal)
    {
        "file", "edit", "special", "tools", "windows", "help"
    };

    private static readonly HashSet<string> ImportHintCommandIds = new(StringComparer.Ordinal)
    {
        "open_character", "open_for_printing", "open_for_export"
    };

    private static readonly HashSet<string> DialogCommandIds = new(StringComparer.Ordinal)
    {
        RuntimeInspectorCommandId,
        "new_character",
        "new_character_origin",
        "new_window",
        "wiki",
        "discord",
        "show_login_video",
        "revision_history",
        "dumpshock",
        "print_setup",
        "print_multiple",
        "dice_roller",
        "global_settings",
        "switch_ruleset",
        "character_settings",
        "translator",
        "xml_editor",
        "master_index",
        "character_roster",
        "data_exporter",
        "export_character",
        DesktopAliceAssistant.CommandId,
        "report_bug",
        "about",
        "hero_lab_importer",
        "update"
    };

    private static readonly HashSet<string> EditorRelayCommandIds = new(StringComparer.Ordinal)
    {
        "copy", "paste"
    };

    private static readonly HashSet<string> AiFeatureCommandIds = new(StringComparer.Ordinal)
    {
        DesktopAliceAssistant.CommandId,
        "new_character_origin"
    };

    private static readonly HashSet<string> AiFeatureCharacterOrCompanionOptionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AI",
        "A.I",
        "A.I.",
        "Artificial Intelligence",
        "E-Ghost",
        "Xenosapient",
        "Explain Companion",
        "Open Explain Companion"
    };

    private static readonly HashSet<string> AiFeatureHorizonIds = new(StringComparer.Ordinal)
    {
        "alice",
        "local_co_processor"
    };

    private static readonly string[] AiFeatureRoutePrefixes =
    [
        "/alice",
        "/account/alice",
        "/local-co-processor",
        "/account/local-co-processor"
    ];

    private static readonly HashSet<string> CoreCommandIds = new(StringComparer.Ordinal)
    {
        "save_character",
        "save_character_as",
        "print_character",
        "refresh_character",
        "new_critter",
        "exit",
        "close_all",
        "restart",
        "close_window"
    };

    public static bool IsMenuCommand(string commandId) => MenuCommandIds.Contains(commandId);

    public static bool IsImportHintCommand(string commandId) => ImportHintCommandIds.Contains(commandId);

    public static bool IsDialogCommand(string commandId) => DialogCommandIds.Contains(commandId);

    public static bool IsRuntimeInspectorCommand(string commandId)
        => string.Equals(commandId, RuntimeInspectorCommandId, StringComparison.Ordinal);

    public static bool IsEditorRelayCommand(string commandId) => EditorRelayCommandIds.Contains(commandId);

    public static bool IsAiFeatureCommand(string commandId) => AiFeatureCommandIds.Contains(commandId);

    public static bool IsBlockedByAiFeaturePreference(string commandId, DesktopPreferenceState preferences)
        => preferences.DisableAiFeatures && IsAiFeatureCommand(commandId);

    public static bool IsAiFeatureCharacterOrCompanionOption(string optionId)
    {
        string token = (optionId ?? string.Empty).Trim();
        return AiFeatureCharacterOrCompanionOptionIds.Contains(token)
            || token.StartsWith("A.I. -", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("AI -", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption(
        string optionId,
        DesktopPreferenceState preferences)
        => preferences.DisableAiFeatures && IsAiFeatureCharacterOrCompanionOption(optionId);

    public static bool IsAiFeatureHorizon(string horizonId) => AiFeatureHorizonIds.Contains(horizonId);

    public static bool IsBlockedByAiFeaturePreferenceForHorizon(string horizonId, DesktopPreferenceState preferences)
        => preferences.DisableAiFeatures && IsAiFeatureHorizon(horizonId);

    public static bool IsAiFeatureRoute(string relativeHref)
        => AiFeatureRoutePrefixes.Any(prefix =>
            relativeHref.Equals(prefix, StringComparison.Ordinal)
            || relativeHref.StartsWith(prefix + "/", StringComparison.Ordinal)
            || relativeHref.StartsWith(prefix + "#", StringComparison.Ordinal)
            || relativeHref.StartsWith(prefix + "?", StringComparison.Ordinal));

    public static bool IsBlockedByAiFeaturePreferenceForRoute(string relativeHref, DesktopPreferenceState preferences)
        => preferences.DisableAiFeatures && IsAiFeatureRoute(relativeHref);

    public static bool IsKnownSharedCommand(string commandId)
    {
        return CoreCommandIds.Contains(commandId)
            || IsMenuCommand(commandId)
            || IsImportHintCommand(commandId)
            || IsDialogCommand(commandId)
            || IsEditorRelayCommand(commandId);
    }
}
