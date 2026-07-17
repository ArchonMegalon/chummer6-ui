using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Shell;

internal static class DesktopMenuProjectionCatalog
{
    private static readonly CatalogOnlyRulesetShellCatalogResolver CompatibilityShellCatalogResolver = new();
    private static readonly string[] VisibleMenuIds = ["file", "edit", "special", "tools", "windows", "help"];
    private static readonly IReadOnlyDictionary<string, string[]> VisibleMenuCommandsByMenuId =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["file"] =
            [
                "new_character",
                "new_critter",
                "open_character",
                "open_for_printing",
                "open_for_export",
                "save_character",
                "save_character_as",
                "refresh_character",
                "print_character",
                "export_character",
                "print_setup",
                "print_multiple",
                "exit"
            ],
            ["edit"] =
            [
                "copy",
                "paste"
            ],
            ["special"] =
            [
                "switch_ruleset"
            ],
            ["tools"] =
            [
                "auto_alice",
                "dice_roller",
                "global_settings",
                "character_settings",
                AppCommandIds.RuntimeInspector,
                "update",
                "translator",
                "xml_editor",
                "hero_lab_importer",
                "master_index",
                "character_roster",
                "data_exporter",
                "report_bug"
            ],
            ["windows"] =
            [
                "new_window",
                "close_window",
                "close_all"
            ],
            ["help"] =
            [
                "wiki",
                "discord",
                "show_login_video",
                "revision_history",
                "dumpshock",
                "about"
            ]
        };

    public static IReadOnlyList<string> GetVisibleMenuIds() => VisibleMenuIds;

    public static IReadOnlyList<string> GetVisibleMenuCommandIds(string menuId)
        => VisibleMenuCommandsByMenuId.TryGetValue(menuId, out string[]? commandIds)
            ? commandIds
            : Array.Empty<string>();

    public static string ResolveProjectedMenuGroupId(AppCommandDefinition command)
    {
        if (string.Equals(command.Id, "switch_ruleset", StringComparison.Ordinal))
        {
            return "special";
        }

        if (string.Equals(command.Id, "report_bug", StringComparison.Ordinal))
        {
            return "tools";
        }

        if (string.Equals(command.Id, "update", StringComparison.Ordinal))
        {
            return "tools";
        }

        return command.Group;
    }

    public static IReadOnlyList<AppCommandDefinition> ResolveVisibleMenuCommands(
        string? rulesetId,
        IReadOnlyList<AppCommandDefinition> commands,
        string menuId)
    {
        IReadOnlyList<string> visibleCommandIds = GetVisibleMenuCommandIds(menuId);
        if (visibleCommandIds.Count == 0)
        {
            return [];
        }

        Dictionary<string, AppCommandDefinition> runtimeCommandsById = new(StringComparer.Ordinal);
        foreach (AppCommandDefinition command in commands)
        {
            if (!string.Equals(ResolveProjectedMenuGroupId(command), menuId, StringComparison.Ordinal)
                || !IsVisibleMenuCommand(menuId, command.Id))
            {
                continue;
            }

            runtimeCommandsById.TryAdd(command.Id, command);
        }

        if (runtimeCommandsById.Count > 0)
        {
            return visibleCommandIds
                .Where(runtimeCommandsById.ContainsKey)
                .Select(commandId => runtimeCommandsById[commandId])
                .ToArray();
        }

        string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
            ?? commands
                .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
                .FirstOrDefault(candidate => candidate is not null)
            ?? RulesetDefaults.Sr5;
        Dictionary<string, AppCommandDefinition> compatibilityCommandsById = new(StringComparer.Ordinal);
        foreach (AppCommandDefinition compatibilityCommand in CompatibilityShellCatalogResolver.ResolveCommands(effectiveRulesetId))
        {
            if (!string.Equals(ResolveProjectedMenuGroupId(compatibilityCommand), menuId, StringComparison.Ordinal)
                || !IsVisibleMenuCommand(menuId, compatibilityCommand.Id))
            {
                continue;
            }

            compatibilityCommandsById.TryAdd(compatibilityCommand.Id, compatibilityCommand);
        }

        return visibleCommandIds
            .Where(compatibilityCommandsById.ContainsKey)
            .Select(commandId => compatibilityCommandsById[commandId])
            .ToArray();
    }

    public static bool IsVisibleMenuCommand(string menuId, string commandId)
        => GetVisibleMenuCommandIds(menuId).Contains(commandId, StringComparer.Ordinal);
}
