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
        new("gear", ["gear_add"], ["Add Gear"]),
        new("inventory", ["gear_add"], ["Add Gear"]),
        new("gearlocations", ["gear_add"], ["Add Gear"]),
        new("weapons", ["combat_add_weapon"], ["Add Weapon"]),
        new("weaponaccessories", ["combat_add_weapon"], ["Add Weapon"]),
        new("weaponlocations", ["combat_add_weapon"], ["Add Weapon"]),
        new("armors", ["combat_add_armor"], ["Add Armor"]),
        new("armormods", ["combat_add_armor"], ["Add Armor"]),
        new("armorlocations", ["combat_add_armor"], ["Add Armor"]),
        new("cyberwares", ["cyberware_add"], ["Add Cyberware"]),
        new("drugs", ["drug_add"], ["Add Drug"]),
        new("spells", ["spell_add"], ["Add Spell"]),
        new("powers", ["adept_power_add"], ["Add Adept Power"]),
        new("complexforms", ["complex_form_add"], ["Add Complex Form"]),
        new("initiationgrades", ["initiation_add"], ["Add Initiation"]),
        new("spirits", ["spirit_add"], ["Add Spirit"]),
        new("critterpowers", ["critter_power_add"], ["Add Critter Power"]),
        new("aiprograms", ["matrix_program_add"], ["Add Program"]),
        new("vehicles", ["vehicle_add"], ["Add Vehicle"]),
        new("contacts", ["contact_add"], ["Add Contact"]),
        new("skills", ["skill_add"], ["Add Skill"]),
        new("qualities", ["quality_add"], ["Add Quality"]),
        new("profile", ["open_notes"], ["Open Notes"])
    ];

    private static readonly string[] HiddenSections =
    [
        "vehiclemods",
        "vehiclelocations",
        "metamagics",
        "foci",
        "mentorspirits",
        "calendar",
        "expenses",
        "progress",
        "improvements",
        "build-lab",
        "rules",
        "summary"
    ];
}
