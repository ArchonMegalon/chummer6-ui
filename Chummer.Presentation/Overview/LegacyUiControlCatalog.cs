using System;

namespace Chummer.Presentation.Overview;

public static class LegacyUiControlCatalog
{
    public static IReadOnlyList<string> All { get; } =
    [
        "create_entry",
        "edit_entry",
        "delete_entry",
        "open_notes",
        "move_up",
        "move_down",
        "toggle_free_paid",
        "show_source",
        "gear_add",
        "gear_edit",
        "gear_delete",
        "gear_mount",
        "gear_source",
        "cyberware_add",
        "cyberware_edit",
        "cyberware_delete",
        "drug_add",
        "drug_delete",
        "magic_add",
        "magic_delete",
        "magic_bind",
        "magic_source",
        "spell_add",
        "adept_power_add",
        "complex_form_add",
        "initiation_add",
        "spirit_add",
        "critter_power_add",
        "matrix_program_add",
        "skill_add",
        "skill_specialize",
        "skill_remove",
        "skill_group",
        "combat_add_weapon",
        "combat_add_armor",
        "combat_reload",
        "combat_damage_track",
        "vehicle_add",
        "vehicle_edit",
        "vehicle_delete",
        "vehicle_mod_add",
        "contact_add",
        "contact_edit",
        "contact_remove",
        "contact_connection",
        "quality_add",
        "quality_delete"
    ];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string controlId)
        => !string.IsNullOrWhiteSpace(controlId) && AllSet.Contains(controlId);
}
