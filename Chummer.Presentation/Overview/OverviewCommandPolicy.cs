using Chummer.Contracts.Presentation;

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
        "new_window",
        "wiki",
        "discord",
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
        "report_bug",
        "about",
        "hero_lab_importer",
        "update"
    };

    private static readonly HashSet<string> EditorRelayCommandIds = new(StringComparer.Ordinal)
    {
        "copy", "paste"
    };

    private static readonly HashSet<string> CoreCommandIds = new(StringComparer.Ordinal)
    {
        "save_character",
        "save_character_as",
        "print_character",
        "refresh_character",
        "new_character",
        "new_critter",
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

    public static bool IsKnownSharedCommand(string commandId)
    {
        return CoreCommandIds.Contains(commandId)
            || IsMenuCommand(commandId)
            || IsImportHintCommand(commandId)
            || IsDialogCommand(commandId)
            || IsEditorRelayCommand(commandId);
    }
}
