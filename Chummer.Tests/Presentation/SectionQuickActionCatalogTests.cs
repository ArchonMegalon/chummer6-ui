#nullable enable annotations

using System;
using System.Linq;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class SectionQuickActionCatalogTests
{
    [TestMethod]
    public void SectionQuickActionCatalog_backed_sections_keep_only_real_primary_actions()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (SectionExpectation expectation in BackedSectionExpectations)
            {
                SectionQuickActionDefinition[] actions = SectionQuickActionCatalog.ForSection(rulesetId, expectation.SectionId).ToArray();

                CollectionAssert.AreEqual(
                    expectation.ControlIds,
                    actions.Select(action => action.ControlId).ToArray(),
                    $"Unexpected quick action controls for '{expectation.SectionId}' under '{rulesetId}'.");
                CollectionAssert.AreEqual(
                    expectation.Labels,
                    actions.Select(action => action.Label).ToArray(),
                    $"Unexpected quick action labels for '{expectation.SectionId}' under '{rulesetId}'.");
                Assert.AreEqual(1, actions.Count(action => action.IsPrimary), $"'{expectation.SectionId}' must keep exactly one primary quick action.");
                Assert.IsTrue(actions[0].IsPrimary, $"'{expectation.SectionId}' must keep its primary quick action first.");
                Assert.AreEqual(
                    actions.Length,
                    actions.Select(action => action.ControlId).Distinct(StringComparer.Ordinal).Count(),
                    $"'{expectation.SectionId}' must not duplicate quick action control ids.");
                Assert.IsTrue(
                    actions.All(action => LegacyUiControlCatalog.IsKnown(action.ControlId)),
                    $"'{expectation.SectionId}' must only expose legacy quick action controls.");
            }
        }
    }

    [TestMethod]
    public void SectionQuickActionCatalog_unbacked_sections_stay_hidden()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (string sectionId in HiddenSections)
            {
                Assert.IsEmpty(
                    SectionQuickActionCatalog.ForSection(rulesetId, sectionId),
                    $"'{sectionId}' must stay hidden under '{rulesetId}' until the action surface is runtime-backed.");
            }
        }
    }

    private sealed record SectionExpectation(string SectionId, string[] ControlIds, string[] Labels);

    private static readonly string[] SupportedRulesets =
    [
        RulesetDefaults.Sr4,
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6
    ];

    private static readonly SectionExpectation[] BackedSectionExpectations =
    [
        new("gear", ["gear_add", "gear_edit", "gear_delete", "combat_damage_track", "gear_source", "toggle_free_paid", "gear_mount"], ["Add Gear", "Edit Gear", "Remove Gear", "Damage Track", "Source", "Free / Paid", "Mount Gear"]),
        new("inventory", ["gear_add", "gear_edit", "gear_delete", "combat_damage_track", "gear_source", "toggle_free_paid", "gear_mount"], ["Add Gear", "Edit Gear", "Remove Gear", "Damage Track", "Source", "Free / Paid", "Mount Gear"]),
        new("gearlocations", ["gear_add", "gear_edit", "gear_delete", "combat_damage_track", "gear_source", "toggle_free_paid", "gear_mount"], ["Add Gear", "Edit Gear", "Remove Gear", "Damage Track", "Source", "Free / Paid", "Mount Gear"]),
        new("weapons", ["combat_add_weapon", "combat_reload", "combat_damage_track", "show_source"], ["Add Weapon", "Reload", "Damage Track", "Source"]),
        new("weaponaccessories", ["combat_add_weapon", "combat_reload", "combat_damage_track", "show_source"], ["Add Weapon", "Reload", "Damage Track", "Source"]),
        new("weaponlocations", ["combat_add_weapon", "combat_reload", "combat_damage_track", "show_source"], ["Add Weapon", "Reload", "Damage Track", "Source"]),
        new("armors", ["combat_add_armor", "combat_damage_track", "show_source"], ["Add Armor", "Damage Track", "Source"]),
        new("armormods", ["combat_add_armor", "combat_damage_track", "show_source"], ["Add Armor", "Damage Track", "Source"]),
        new("armorlocations", ["combat_add_armor", "combat_damage_track", "show_source"], ["Add Armor", "Damage Track", "Source"]),
        new("cyberwares", ["cyberware_add", "cyberware_edit", "cyberware_delete", "combat_damage_track", "show_source"], ["Add Cyberware", "Edit Cyberware", "Remove Cyberware", "Damage Track", "Source"]),
        new("drugs", ["drug_add", "drug_delete"], ["Add Drug", "Remove Drug"]),
        new("spells", ["spell_add", "magic_source"], ["Add Spell", "Source"]),
        new("powers", ["adept_power_add", "magic_source"], ["Add Adept Power", "Source"]),
        new("complexforms", ["complex_form_add", "show_source"], ["Add Complex Form", "Source"]),
        new("initiationgrades", ["initiation_add", "show_source"], ["Add Initiation", "Source"]),
        new("spirits", ["spirit_add", "show_source"], ["Add Spirit", "Source"]),
        new("sprites", ["sprite_add", "show_source"], ["Add Sprite", "Source"]),
        new("conditionmonitor", ["combat_damage_track"], ["Damage Track"]),
        new("critterpowers", ["critter_power_add", "show_source"], ["Add Critter Power", "Source"]),
        new("aiprograms", ["matrix_program_add", "show_source"], ["Add Program", "Source"]),
        new("vehicles", ["vehicle_add", "vehicle_edit", "combat_damage_track", "vehicle_mod_add", "vehicle_delete"], ["Add Vehicle", "Edit Vehicle", "Damage Track", "Add Vehicle Mod", "Remove Vehicle"]),
        new("vehiclemods", ["vehicle_mod_add", "combat_damage_track", "show_source"], ["Add Vehicle Mod", "Damage Track", "Source"]),
        new("relationships", ["contact_add", "contact_edit", "contact_connection", "contact_remove"], ["Add Contact", "Edit Contact", "Connection / Loyalty", "Remove Contact"]),
        new("contacts", ["contact_add", "contact_edit", "contact_connection", "contact_remove"], ["Add Contact", "Edit Contact", "Connection / Loyalty", "Remove Contact"]),
        new("enemies", ["contact_add", "contact_edit", "contact_connection", "contact_remove"], ["Add Contact", "Edit Contact", "Connection / Loyalty", "Remove Contact"]),
        new("pets", ["contact_add", "contact_edit", "contact_connection", "contact_remove"], ["Add Contact", "Edit Contact", "Connection / Loyalty", "Remove Contact"]),
        new("skills", ["skill_add", "skill_specialize", "skill_group", "skill_remove"], ["Add Skill", "Specialize", "Skill Group", "Remove Skill"]),
        new("foci", ["magic_bind", "magic_delete", "magic_source"], ["Bind Focus", "Remove Focus", "Source"]),
        new("metamagics", ["initiation_add", "magic_delete", "magic_source"], ["Add Grade", "Remove Grade", "Source"]),
        new("expenses", ["create_entry", "edit_entry", "delete_entry"], ["Add Expense", "Edit Expense", "Remove Expense"]),
        new("qualities", ["quality_add", "quality_delete", "show_source"], ["Add Quality", "Remove Quality", "Source"]),
        new("progress", ["create_entry", "edit_entry", "delete_entry", "move_up", "move_down"], ["Add Entry", "Edit Entry", "Remove Entry", "Move Up", "Move Down"]),
        new("calendar", ["create_entry", "edit_entry", "delete_entry", "move_up", "move_down"], ["Add Entry", "Edit Entry", "Remove Entry", "Move Up", "Move Down"]),
        new("diary", ["create_entry", "edit_entry", "delete_entry", "move_up", "move_down"], ["Add Entry", "Edit Entry", "Remove Entry", "Move Up", "Move Down"]),
        new("improvements", ["show_source"], ["Source"]),
        new("sources", ["show_source"], ["Source"]),
        new("profile", ["open_notes", "identity_license_add", "identity_license_edit", "identity_license_delete"], ["Open Notes", "Add SIN / License", "Edit SIN / License", "Remove SIN / License"])
    ];

    private static readonly string[] HiddenSections =
    [
        "vehiclelocations",
        "mentorspirits",
        "build-lab",
        "rules",
        "summary",
        "customdatadirectorynames"
    ];
}
