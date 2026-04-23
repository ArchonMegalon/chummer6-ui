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
            "gear" or "inventory" or "gearlocations" => PrimaryOnly("gear_add", "Add Gear"),
            "weapons" or "weaponaccessories" or "weaponlocations" => PrimaryOnly("combat_add_weapon", "Add Weapon"),
            "armors" or "armormods" or "armorlocations" => PrimaryOnly("combat_add_armor", "Add Armor"),
            "cyberwares" => PrimaryOnly("cyberware_add", "Add Cyberware"),
            "drugs" => PrimaryOnly("drug_add", "Add Drug"),
            "spells" => PrimaryOnly("spell_add", "Add Spell"),
            "powers" => PrimaryOnly("adept_power_add", "Add Adept Power"),
            "complexforms" => PrimaryOnly("complex_form_add", "Add Complex Form"),
            "initiationgrades" => PrimaryOnly("initiation_add", "Add Initiation"),
            "spirits" => PrimaryOnly("spirit_add", "Add Spirit"),
            "critterpowers" => PrimaryOnly("critter_power_add", "Add Critter Power"),
            "aiprograms" => PrimaryOnly("matrix_program_add", "Add Program"),
            "vehicles" => PrimaryOnly("vehicle_add", "Add Vehicle"),
            "contacts" => PrimaryOnly("contact_add", "Add Contact"),
            "skills" => PrimaryOnly("skill_add", "Add Skill"),
            "qualities" => PrimaryOnly("quality_add", "Add Quality"),
            "profile" => PrimaryOnly("open_notes", "Open Notes"),
            _ => Array.Empty<SectionQuickActionDefinition>()
        };
    }

    private static IReadOnlyList<SectionQuickActionDefinition> PrimaryOnly(string controlId, string label)
        => [new SectionQuickActionDefinition(controlId, label, true)];

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
