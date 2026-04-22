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
    public void SectionQuickActionCatalog_standard_sections_are_ruleset_stable_and_primary_first()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (SectionExpectation expectation in StandardSectionExpectations)
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
    public void SectionQuickActionCatalog_sr6_adapted_sections_use_guided_entry_posture_only_for_sr6()
    {
        foreach (SectionExpectation expectation in Sr6AdaptedSectionExpectations)
        {
            SectionQuickActionDefinition[] sr6Actions = SectionQuickActionCatalog.ForSection(RulesetDefaults.Sr6, expectation.SectionId).ToArray();

            CollectionAssert.AreEqual(
                expectation.ControlIds,
                sr6Actions.Select(action => action.ControlId).ToArray(),
                $"Unexpected SR6 quick action controls for '{expectation.SectionId}'.");
            CollectionAssert.AreEqual(
                expectation.Labels,
                sr6Actions.Select(action => action.Label).ToArray(),
                $"Unexpected SR6 quick action labels for '{expectation.SectionId}'.");
            Assert.AreEqual(1, sr6Actions.Count(action => action.IsPrimary), $"'{expectation.SectionId}' must keep exactly one SR6 primary quick action.");
            Assert.IsTrue(sr6Actions[0].IsPrimary, $"'{expectation.SectionId}' must keep its SR6 primary quick action first.");
            Assert.IsTrue(sr6Actions.All(action => LegacyUiControlCatalog.IsKnown(action.ControlId)), $"'{expectation.SectionId}' must only expose legacy quick action controls.");

            Assert.IsEmpty(SectionQuickActionCatalog.ForSection(RulesetDefaults.Sr4, expectation.SectionId), $"'{expectation.SectionId}' must stay hidden for SR4.");
            Assert.IsEmpty(SectionQuickActionCatalog.ForSection(RulesetDefaults.Sr5, expectation.SectionId), $"'{expectation.SectionId}' must stay hidden for SR5.");
        }
    }

    private sealed record SectionExpectation(string SectionId, string[] ControlIds, string[] Labels);

    private static readonly string[] SupportedRulesets =
    [
        RulesetDefaults.Sr4,
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6
    ];

    private static readonly SectionExpectation[] StandardSectionExpectations =
    [
        new("gear", ["gear_add", "gear_edit", "gear_delete", "gear_source"], ["Add Gear", "Edit Gear", "Remove Gear", "Show Source"]),
        new("inventory", ["gear_add", "gear_edit", "gear_delete", "gear_source"], ["Add Gear", "Edit Gear", "Remove Gear", "Show Source"]),
        new("gearlocations", ["gear_add", "gear_edit", "gear_delete", "gear_source"], ["Add Gear", "Edit Gear", "Remove Gear", "Show Source"]),
        new("weapons", ["combat_add_weapon", "gear_edit", "combat_reload", "gear_source"], ["Add Weapon", "Edit Weapon", "Reload", "Show Source"]),
        new("weaponaccessories", ["combat_add_weapon", "gear_edit", "combat_reload", "gear_source"], ["Add Weapon", "Edit Weapon", "Reload", "Show Source"]),
        new("weaponlocations", ["combat_add_weapon", "gear_edit", "combat_reload", "gear_source"], ["Add Weapon", "Edit Weapon", "Reload", "Show Source"]),
        new("armors", ["combat_add_armor", "gear_edit", "gear_delete", "gear_source"], ["Add Armor", "Edit Armor", "Remove Armor", "Show Source"]),
        new("armormods", ["combat_add_armor", "gear_edit", "gear_delete", "gear_source"], ["Add Armor", "Edit Armor", "Remove Armor", "Show Source"]),
        new("armorlocations", ["combat_add_armor", "gear_edit", "gear_delete", "gear_source"], ["Add Armor", "Edit Armor", "Remove Armor", "Show Source"]),
        new("cyberwares", ["cyberware_add", "cyberware_edit", "cyberware_delete", "gear_source"], ["Add Cyberware", "Edit Cyberware", "Remove Cyberware", "Show Source"]),
        new("drugs", ["drug_add", "drug_delete", "gear_source"], ["Add Drug", "Remove Drug", "Show Source"]),
        new("vehicles", ["vehicle_add", "vehicle_edit", "vehicle_delete", "vehicle_mod_add"], ["Add Vehicle", "Edit Vehicle", "Remove Vehicle", "Add Vehicle Mod"]),
        new("vehiclemods", ["vehicle_mod_add", "vehicle_edit", "vehicle_delete"], ["Add Vehicle Mod", "Edit Vehicle", "Remove Vehicle"]),
        new("vehiclelocations", ["vehicle_mod_add", "vehicle_edit", "vehicle_delete"], ["Add Vehicle Mod", "Edit Vehicle", "Remove Vehicle"]),
        new("contacts", ["contact_add", "contact_edit", "contact_connection", "contact_remove"], ["Add Contact", "Edit Contact", "Connection / Loyalty", "Remove Contact"]),
        new("skills", ["skill_add", "skill_specialize", "skill_remove"], ["Add Skill", "Add Specialization", "Remove Skill"]),
        new("qualities", ["quality_add", "quality_delete", "show_source"], ["Add Quality", "Remove Quality", "Show Source"]),
        new("spells", ["spell_add", "magic_delete", "magic_source"], ["Add Spell", "Remove Spell", "Show Source"]),
        new("powers", ["adept_power_add", "magic_delete", "magic_source"], ["Add Adept Power", "Remove Power", "Show Source"]),
        new("complexforms", ["complex_form_add", "magic_delete", "matrix_program_add"], ["Add Complex Form", "Remove Complex Form", "Add Program"]),
        new("metamagics", ["initiation_add", "magic_bind"], ["Add Initiation / Submersion", "Bind / Link"]),
        new("initiationgrades", ["initiation_add", "magic_bind"], ["Add Initiation / Submersion", "Bind / Link"]),
        new("spirits", ["spirit_add", "magic_bind"], ["Add Spirit / Ally", "Bind Spirit"]),
        new("foci", ["spirit_add", "magic_bind"], ["Add Focus / Familiar", "Bind Focus"]),
        new("mentorspirits", ["spirit_add", "contact_add"], ["Add Mentor / Familiar", "Add Contact"]),
        new("critterpowers", ["critter_power_add", "magic_source"], ["Add Critter Power", "Show Source"]),
        new("aiprograms", ["matrix_program_add", "gear_source"], ["Add Program / Deck Item", "Show Source"]),
        new("calendar", ["create_entry", "edit_entry", "delete_entry"], ["Add Diary Entry", "Edit Entry", "Remove Entry"]),
        new("expenses", ["create_entry", "edit_entry", "delete_entry"], ["Add Diary Entry", "Edit Entry", "Remove Entry"]),
        new("progress", ["create_entry", "edit_entry", "delete_entry"], ["Add Diary Entry", "Edit Entry", "Remove Entry"]),
        new("improvements", ["create_entry", "edit_entry", "delete_entry"], ["Add Diary Entry", "Edit Entry", "Remove Entry"]),
        new("profile", ["open_notes", "edit_entry"], ["Open Notes", "Edit Entry"])
    ];

    private static readonly SectionExpectation[] Sr6AdaptedSectionExpectations =
    [
        new("build-lab", ["create_entry", "show_source"], ["Add Guided Entry", "Show Source"]),
        new("rules", ["create_entry", "show_source"], ["Add Guided Entry", "Show Source"]),
        new("summary", ["create_entry", "show_source"], ["Add Guided Entry", "Show Source"])
    ];
}
