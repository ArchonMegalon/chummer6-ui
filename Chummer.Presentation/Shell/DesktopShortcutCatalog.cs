namespace Chummer.Presentation.Shell;

public static class DesktopShortcutCatalog
{
    public static bool TryResolveCommandId(
        string? key,
        bool commandModifier,
        bool shiftModifier,
        bool altModifier,
        out string commandId)
    {
        commandId = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || altModifier)
        {
            return false;
        }

        string normalizedKey = key.Trim().ToLowerInvariant();
        if (commandModifier)
        {
            commandId = normalizedKey switch
            {
                "s" when shiftModifier => "save_character_as",
                "s" => "save_character",
                "w" when !shiftModifier => "close_window",
                "g" when !shiftModifier => "global_settings",
                "o" when !shiftModifier => "open_character",
                "n" when shiftModifier => "new_critter",
                "n" => "new_character",
                "p" when !shiftModifier => "print_character",
                "r" when !shiftModifier => "refresh_character",
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(commandId);
        }

        if (!shiftModifier && string.Equals(normalizedKey, "f5", StringComparison.Ordinal))
        {
            commandId = "refresh_character";
            return true;
        }

        return false;
    }
}
