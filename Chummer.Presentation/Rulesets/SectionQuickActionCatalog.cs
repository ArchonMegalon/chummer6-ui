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
            "gear" or "inventory" or "gearlocations" => Standard(
                primary: new SectionQuickActionDefinition("gear_add", "Add Gear", true),
                new SectionQuickActionDefinition("gear_edit", "Edit Gear"),
                new SectionQuickActionDefinition("gear_delete", "Remove Gear"),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "weapons" or "weaponaccessories" or "weaponlocations" => Standard(
                primary: new SectionQuickActionDefinition("combat_add_weapon", "Add Weapon", true),
                new SectionQuickActionDefinition("gear_edit", "Edit Weapon"),
                new SectionQuickActionDefinition("combat_reload", "Reload"),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "armors" or "armormods" or "armorlocations" => Standard(
                primary: new SectionQuickActionDefinition("combat_add_armor", "Add Armor", true),
                new SectionQuickActionDefinition("gear_edit", "Edit Armor"),
                new SectionQuickActionDefinition("gear_delete", "Remove Armor"),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "cyberwares" => Standard(
                primary: new SectionQuickActionDefinition("cyberware_add", "Add Cyberware", true),
                new SectionQuickActionDefinition("cyberware_edit", "Edit Cyberware"),
                new SectionQuickActionDefinition("cyberware_delete", "Remove Cyberware"),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "drugs" => Standard(
                primary: new SectionQuickActionDefinition("drug_add", "Add Drug", true),
                new SectionQuickActionDefinition("drug_delete", "Remove Drug"),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "vehicles" => Standard(
                primary: new SectionQuickActionDefinition("vehicle_add", "Add Vehicle", true),
                new SectionQuickActionDefinition("vehicle_edit", "Edit Vehicle"),
                new SectionQuickActionDefinition("vehicle_delete", "Remove Vehicle"),
                new SectionQuickActionDefinition("vehicle_mod_add", "Add Vehicle Mod")),
            "vehiclemods" or "vehiclelocations" => Standard(
                primary: new SectionQuickActionDefinition("vehicle_mod_add", "Add Vehicle Mod", true),
                new SectionQuickActionDefinition("vehicle_edit", "Edit Vehicle"),
                new SectionQuickActionDefinition("vehicle_delete", "Remove Vehicle")),
            "contacts" => Standard(
                primary: new SectionQuickActionDefinition("contact_add", "Add Contact", true),
                new SectionQuickActionDefinition("contact_edit", "Edit Contact"),
                new SectionQuickActionDefinition("contact_connection", "Connection / Loyalty"),
                new SectionQuickActionDefinition("contact_remove", "Remove Contact")),
            "skills" => Standard(
                primary: new SectionQuickActionDefinition("skill_add", "Add Skill", true),
                new SectionQuickActionDefinition("skill_specialize", "Add Specialization"),
                new SectionQuickActionDefinition("skill_remove", "Remove Skill")),
            "qualities" => Standard(
                primary: new SectionQuickActionDefinition("quality_add", "Add Quality", true),
                new SectionQuickActionDefinition("quality_delete", "Remove Quality"),
                new SectionQuickActionDefinition("show_source", "Show Source")),
            "spells" => Standard(
                primary: new SectionQuickActionDefinition("spell_add", "Add Spell", true),
                new SectionQuickActionDefinition("magic_delete", "Remove Spell"),
                new SectionQuickActionDefinition("magic_source", "Show Source")),
            "powers" => Standard(
                primary: new SectionQuickActionDefinition("adept_power_add", "Add Adept Power", true),
                new SectionQuickActionDefinition("magic_delete", "Remove Power"),
                new SectionQuickActionDefinition("magic_source", "Show Source")),
            "complexforms" => Standard(
                primary: new SectionQuickActionDefinition("complex_form_add", "Add Complex Form", true),
                new SectionQuickActionDefinition("magic_delete", "Remove Complex Form"),
                new SectionQuickActionDefinition("matrix_program_add", "Add Program")),
            "metamagics" or "initiationgrades" => Standard(
                primary: new SectionQuickActionDefinition("initiation_add", "Add Initiation / Submersion", true),
                new SectionQuickActionDefinition("magic_bind", "Bind / Link")),
            "spirits" => Standard(
                primary: new SectionQuickActionDefinition("spirit_add", "Add Spirit / Ally", true),
                new SectionQuickActionDefinition("magic_bind", "Bind Spirit")),
            "foci" => Standard(
                primary: new SectionQuickActionDefinition("spirit_add", "Add Focus / Familiar", true),
                new SectionQuickActionDefinition("magic_bind", "Bind Focus")),
            "mentorspirits" => Standard(
                primary: new SectionQuickActionDefinition("spirit_add", "Add Mentor / Familiar", true),
                new SectionQuickActionDefinition("contact_add", "Add Contact")),
            "critterpowers" => Standard(
                primary: new SectionQuickActionDefinition("critter_power_add", "Add Critter Power", true),
                new SectionQuickActionDefinition("magic_source", "Show Source")),
            "aiprograms" => Standard(
                primary: new SectionQuickActionDefinition("matrix_program_add", "Add Program / Deck Item", true),
                new SectionQuickActionDefinition("gear_source", "Show Source")),
            "calendar" or "expenses" or "progress" or "improvements" => Standard(
                primary: new SectionQuickActionDefinition("create_entry", "Add Diary Entry", true),
                new SectionQuickActionDefinition("edit_entry", "Edit Entry"),
                new SectionQuickActionDefinition("delete_entry", "Remove Entry")),
            "profile" => Standard(
                primary: new SectionQuickActionDefinition("open_notes", "Open Notes", true),
                new SectionQuickActionDefinition("edit_entry", "Edit Entry")),
            _ when IsSr6AdaptedSection(normalizedSectionId, rulesetId) => Standard(
                primary: new SectionQuickActionDefinition("create_entry", "Add Guided Entry", true),
                new SectionQuickActionDefinition("show_source", "Show Source")),
            _ => Array.Empty<SectionQuickActionDefinition>()
        };
    }

    private static IReadOnlyList<SectionQuickActionDefinition> Standard(
        SectionQuickActionDefinition primary,
        params SectionQuickActionDefinition[] additional)
    {
        SectionQuickActionDefinition[] actions = new SectionQuickActionDefinition[additional.Length + 1];
        actions[0] = primary;
        additional.CopyTo(actions, 1);
        return actions;
    }

    private static bool IsSr6AdaptedSection(string normalizedSectionId, string? rulesetId)
    {
        string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        if (!string.Equals(effectiveRulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal))
        {
            return false;
        }

        return normalizedSectionId is "build-lab" or "rules" or "summary";
    }

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
