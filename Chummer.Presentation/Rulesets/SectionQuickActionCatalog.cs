using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Rulesets;

public static class SectionQuickActionCatalog
{
    public static IReadOnlyList<SectionQuickActionDefinition> ForSection(string? rulesetId, string? sectionId)
    {
        string? normalizedSectionId = Normalize(sectionId);
        if (normalizedSectionId is null)
        {
            return Array.Empty<SectionQuickActionDefinition>();
        }

        return normalizedSectionId switch
        {
            "gear" or "inventory" or "gearlocations" => Actions(
                Primary("gear_add", "Add Gear"),
                Secondary("gear_edit", "Edit Gear"),
                Secondary("gear_delete", "Remove Gear"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("gear_source", "Source"),
                Secondary("toggle_free_paid", "Free / Paid"),
                Secondary("gear_mount", "Mount Gear")),
            "weapons" or "weaponaccessories" or "weaponlocations" => Actions(
                Primary("combat_add_weapon", "Add Weapon"),
                Secondary("combat_reload", "Reload"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("show_source", "Source")),
            "armors" or "armormods" or "armorlocations" => Actions(
                Primary("combat_add_armor", "Add Armor"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("show_source", "Source")),
            "cyberwares" => Actions(
                Primary("cyberware_add", "Add Cyberware"),
                Secondary("cyberware_edit", "Edit Cyberware"),
                Secondary("cyberware_delete", "Remove Cyberware"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("show_source", "Source")),
            "drugs" => Actions(
                Primary("drug_add", "Add Drug"),
                Secondary("drug_delete", "Remove Drug")),
            "spells" => Actions(
                Primary("spell_add", "Add Spell"),
                Secondary("magic_source", "Source")),
            "powers" => Actions(
                Primary("adept_power_add", "Add Adept Power"),
                Secondary("magic_source", "Source")),
            "complexforms" => Actions(
                Primary("complex_form_add", "Add Complex Form"),
                Secondary("show_source", "Source")),
            "initiationgrades" => Actions(
                Primary("initiation_add", "Add Initiation"),
                Secondary("show_source", "Source")),
            "spirits" => Actions(
                Primary("spirit_add", "Add Spirit"),
                Secondary("show_source", "Source")),
            "sprites" => Actions(
                Primary("sprite_add", "Add Sprite"),
                Secondary("show_source", "Source")),
            "conditionmonitor" => PrimaryOnly("combat_damage_track", "Damage Track"),
            "critterpowers" => Actions(
                Primary("critter_power_add", "Add Critter Power"),
                Secondary("show_source", "Source")),
            "aiprograms" => Actions(
                Primary("matrix_program_add", "Add Program"),
                Secondary("show_source", "Source")),
            "vehicles" => Actions(
                Primary("vehicle_add", "Add Vehicle"),
                Secondary("vehicle_edit", "Edit Vehicle"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("vehicle_mod_add", "Add Vehicle Mod"),
                Secondary("vehicle_delete", "Remove Vehicle")),
            "vehiclemods" => Actions(
                Primary("vehicle_mod_add", "Add Vehicle Mod"),
                Secondary("combat_damage_track", "Damage Track"),
                Secondary("show_source", "Source")),
            "relationships" or "contacts" or "enemies" or "pets" => Actions(
                Primary("contact_add", "Add Contact"),
                Secondary("contact_edit", "Edit Contact"),
                Secondary("contact_connection", "Connection / Loyalty"),
                Secondary("contact_remove", "Remove Contact")),
            "skills" => Actions(
                Primary("skill_add", "Add Skill"),
                Secondary("skill_specialize", "Specialize"),
                Secondary("skill_group", "Skill Group"),
                Secondary("skill_remove", "Remove Skill")),
            "foci" => Actions(
                Primary("magic_bind", "Bind Focus"),
                Secondary("magic_delete", "Remove Focus"),
                Secondary("magic_source", "Source")),
            "metamagics" => Actions(
                Primary("initiation_add", "Add Grade"),
                Secondary("magic_delete", "Remove Grade"),
                Secondary("magic_source", "Source")),
            "expenses" => Actions(
                Primary("create_entry", "Add Expense"),
                Secondary("edit_entry", "Edit Expense"),
                Secondary("delete_entry", "Remove Expense")),
            "qualities" => Actions(
                Primary("quality_add", "Add Quality"),
                Secondary("quality_delete", "Remove Quality"),
                Secondary("show_source", "Source")),
            "progress" or "calendar" or "diary" => Actions(
                Primary("create_entry", "Add Entry"),
                Secondary("edit_entry", "Edit Entry"),
                Secondary("delete_entry", "Remove Entry"),
                Secondary("move_up", "Move Up"),
                Secondary("move_down", "Move Down")),
            "improvements" => PrimaryOnly("show_source", "Source"),
            "sources" => PrimaryOnly("show_source", "Source"),
            "profile" => Actions(
                Primary("open_notes", "Open Notes"),
                Secondary("identity_license_add", "Add SIN / License"),
                Secondary("identity_license_edit", "Edit SIN / License"),
                Secondary("identity_license_delete", "Remove SIN / License")),
            _ => Array.Empty<SectionQuickActionDefinition>()
        };
    }

    private static IReadOnlyList<SectionQuickActionDefinition> Actions(params SectionQuickActionDefinition[] actions)
        => actions;

    private static IReadOnlyList<SectionQuickActionDefinition> PrimaryOnly(string controlId, string label)
        => [Primary(controlId, label)];

    private static SectionQuickActionDefinition Primary(string controlId, string label)
        => new(controlId, label, true);

    private static SectionQuickActionDefinition Secondary(string controlId, string label)
        => new(controlId, label, false);

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}

public sealed record SectionQuickActionDefinition(string ControlId, string Label, bool IsPrimary = false);
