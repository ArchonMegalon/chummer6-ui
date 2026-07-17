#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class LegacySr5DesktopParityAuditTests
{
    [TestMethod]
    public void Legacy_sr5_desktop_tab_ledger_covers_every_unique_character_create_and_career_label()
    {
        string[] extractedLabels = ExtractLegacyTabLabels().OrderBy(label => label, StringComparer.Ordinal).ToArray();
        string[] ledgerLabels = TabExpectations.Keys.OrderBy(label => label, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            extractedLabels,
            ledgerLabels,
            "Every legacy SR5 desktop tab label must carry an explicit SR6 parity disposition.");
    }

    [TestMethod]
    public void Legacy_sr5_desktop_non_missing_pendants_point_only_to_real_modern_surfaces_or_controls()
    {
        HashSet<string> validActionIds = BuildValidModernPendantIds();

        foreach (LegacySurfaceParityExpectation expectation in TabExpectations.Values)
        {
            if (expectation.Disposition == LegacySurfaceParityDisposition.Missing)
            {
                Assert.AreEqual(0, expectation.ModernPendants.Length, $"Missing legacy surface '{expectation.LegacyLabel}' must not pretend to have a modern pendant.");
                continue;
            }

            Assert.IsTrue(expectation.ModernPendants.Length > 0, $"Legacy surface '{expectation.LegacyLabel}' must point to at least one real modern pendant.");

            foreach (string pendantId in expectation.ModernPendants)
            {
                Assert.IsTrue(
                    validActionIds.Contains(pendantId),
                    $"Legacy surface '{expectation.LegacyLabel}' points to unknown modern pendant '{pendantId}'.");
            }
        }
    }

    [TestMethod]
    public void Legacy_sr5_desktop_partial_and_missing_surface_gaps_stay_explicit()
    {
        string[] partialLabels = TabExpectations.Values
            .Where(expectation => expectation.Disposition == LegacySurfaceParityDisposition.Partial)
            .Select(expectation => expectation.LegacyLabel)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
        string[] missingLabels = TabExpectations.Values
            .Where(expectation => expectation.Disposition == LegacySurfaceParityDisposition.Missing)
            .Select(expectation => expectation.LegacyLabel)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            partialLabels,
            "The partial legacy-to-modern parity ledger drifted; review the SR5 audit before changing this list.");
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            missingLabels,
            "The missing legacy-to-modern parity ledger drifted; implement or reclassify intentionally before changing this list.");
    }

    [TestMethod]
    public void Legacy_sr5_control_ledger_covers_every_known_legacy_ui_control()
    {
        string[] discoveredControls = LegacyUiControlCatalog.All
            .OrderBy(controlId => controlId, StringComparer.Ordinal)
            .ToArray();
        string[] ledgerControls = ControlExpectations.Keys
            .OrderBy(controlId => controlId, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            discoveredControls,
            ledgerControls,
            "Every legacy SR5 control/function must carry an explicit SR6 parity disposition.");
    }

    [TestMethod]
    public void Legacy_sr5_control_non_missing_pendants_point_only_to_real_modern_surfaces_or_controls()
    {
        HashSet<string> validActionIds = BuildValidModernPendantIds();

        foreach (LegacySurfaceParityExpectation expectation in ControlExpectations.Values)
        {
            if (expectation.Disposition == LegacySurfaceParityDisposition.Missing)
            {
                Assert.AreEqual(0, expectation.ModernPendants.Length, $"Missing legacy control '{expectation.LegacyLabel}' must not pretend to have a modern pendant.");
                continue;
            }

            Assert.IsTrue(expectation.ModernPendants.Length > 0, $"Legacy control '{expectation.LegacyLabel}' must point to at least one real modern pendant.");

            foreach (string pendantId in expectation.ModernPendants)
            {
                Assert.IsTrue(
                    validActionIds.Contains(pendantId),
                    $"Legacy control '{expectation.LegacyLabel}' points to unknown modern pendant '{pendantId}'.");
            }
        }
    }

    [TestMethod]
    public void Legacy_sr5_control_partial_and_missing_gaps_stay_explicit()
    {
        string[] partialControls = ControlExpectations.Values
            .Where(expectation => expectation.Disposition == LegacySurfaceParityDisposition.Partial)
            .Select(expectation => expectation.LegacyLabel)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
        string[] missingControls = ControlExpectations.Values
            .Where(expectation => expectation.Disposition == LegacySurfaceParityDisposition.Missing)
            .Select(expectation => expectation.LegacyLabel)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            partialControls,
            "The partial legacy-control parity ledger drifted; review the SR5 audit before changing this list.");
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            missingControls,
            "The missing legacy-control parity ledger drifted; implement or reclassify intentionally before changing this list.");
    }

    private static string[] ExtractLegacyTabLabels()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string[] designerPaths =
        [
            Path.Combine(repoRoot, "Chummer", "Forms", "Character Forms", "CharacterCreate.Designer.cs"),
            Path.Combine(repoRoot, "Chummer", "Forms", "Character Forms", "CharacterCareer.Designer.cs")
        ];

        HashSet<string> labels = new(StringComparer.Ordinal);
        Regex regex = new(@"this\.tab\w+\.Text = ""([^""]+)"";", RegexOptions.Compiled);

        foreach (string path in designerPaths)
        {
            string text = File.ReadAllText(path);
            foreach (Match match in regex.Matches(text))
            {
                string label = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }
        }

        return labels.ToArray();
    }

    private static HashSet<string> BuildValidModernPendantIds()
    {
        CatalogOnlyRulesetShellCatalogResolver resolver = new();

        HashSet<string> validIds = new(StringComparer.Ordinal);
        foreach (string tabId in resolver.ResolveNavigationTabs(RulesetDefaults.Sr6).Select(tab => tab.Id))
        {
            foreach (string actionId in resolver.ResolveWorkspaceActionsForTab(tabId, RulesetDefaults.Sr6).Select(action => action.Id))
            {
                validIds.Add(actionId);
            }
        }

        foreach (string controlId in LegacyUiControlCatalog.All)
        {
            validIds.Add(controlId);
        }

        return validIds;
    }

    private enum LegacySurfaceParityDisposition
    {
        Backed,
        Consolidated,
        Partial,
        Missing
    }

    private sealed record LegacySurfaceParityExpectation(
        string LegacyLabel,
        LegacySurfaceParityDisposition Disposition,
        string[] ModernPendants);

    private static readonly IReadOnlyDictionary<string, LegacySurfaceParityExpectation> TabExpectations =
        new[]
        {
            new LegacySurfaceParityExpectation("Adept Powers", LegacySurfaceParityDisposition.Backed, ["tab-adept.powers"]),
            new LegacySurfaceParityExpectation("Advanced Programs", LegacySurfaceParityDisposition.Backed, ["tab-technomancer.aiprograms"]),
            new LegacySurfaceParityExpectation("Background", LegacySurfaceParityDisposition.Consolidated, ["tab-info.profile"]),
            new LegacySurfaceParityExpectation("Calendar", LegacySurfaceParityDisposition.Backed, ["tab-calendar.calendar"]),
            new LegacySurfaceParityExpectation("Character Info", LegacySurfaceParityDisposition.Backed, ["tab-info.profile"]),
            new LegacySurfaceParityExpectation("Character Notes", LegacySurfaceParityDisposition.Consolidated, ["tab-info.profile", "tab-notes.metadata", "open_notes"]),
            new LegacySurfaceParityExpectation("Clothing & Armor", LegacySurfaceParityDisposition.Backed, ["tab-armor.armors", "tab-combat.armors"]),
            new LegacySurfaceParityExpectation("Common", LegacySurfaceParityDisposition.Consolidated, ["tab-info.summary", "tab-info.profile", "tab-info.build"]),
            new LegacySurfaceParityExpectation("Concept", LegacySurfaceParityDisposition.Consolidated, ["tab-info.profile"]),
            new LegacySurfaceParityExpectation("Condition Monitor", LegacySurfaceParityDisposition.Backed, ["tab-combat.conditionmonitor"]),
            new LegacySurfaceParityExpectation("Contacts", LegacySurfaceParityDisposition.Backed, ["tab-contacts.contacts"]),
            new LegacySurfaceParityExpectation("Critter Powers", LegacySurfaceParityDisposition.Backed, ["tab-magician.critterpowers"]),
            new LegacySurfaceParityExpectation("Cyberware & Bioware", LegacySurfaceParityDisposition.Backed, ["tab-cyberware.cyberwares"]),
            new LegacySurfaceParityExpectation("Description", LegacySurfaceParityDisposition.Consolidated, ["tab-info.profile"]),
            new LegacySurfaceParityExpectation("Drugs", LegacySurfaceParityDisposition.Backed, ["tab-combat.drugs", "tab-gear.drugs"]),
            new LegacySurfaceParityExpectation("Enemies", LegacySurfaceParityDisposition.Backed, ["tab-relationships.enemies"]),
            new LegacySurfaceParityExpectation("Game Notes", LegacySurfaceParityDisposition.Consolidated, ["tab-notes.metadata", "open_notes"]),
            new LegacySurfaceParityExpectation("Gear", LegacySurfaceParityDisposition.Backed, ["tab-gear.inventory", "tab-gear.gear"]),
            new LegacySurfaceParityExpectation("Improvements", LegacySurfaceParityDisposition.Backed, ["tab-improvements.improvements"]),
            new LegacySurfaceParityExpectation("Initiation & Submersion", LegacySurfaceParityDisposition.Consolidated, ["tab-magician.metamagics", "tab-magician.arts", "tab-magician.initiationgrades", "tab-adept.metamagics", "tab-adept.initiationgrades"]),
            new LegacySurfaceParityExpectation("Karma & Nuyen", LegacySurfaceParityDisposition.Backed, ["tab-karma.expenses", "tab-karma.calendar", "tab-karma.progress"]),
            new LegacySurfaceParityExpectation("Karma Summary", LegacySurfaceParityDisposition.Backed, ["tab-karma.summary"]),
            new LegacySurfaceParityExpectation("Limits", LegacySurfaceParityDisposition.Backed, ["tab-attributes.limitmodifiers"]),
            new LegacySurfaceParityExpectation("Lifestyles", LegacySurfaceParityDisposition.Backed, ["tab-lifestyle.lifestyles"]),
            new LegacySurfaceParityExpectation("Martial Arts", LegacySurfaceParityDisposition.Backed, ["tab-skills.martialarts"]),
            new LegacySurfaceParityExpectation("Matrix Condition Monitor", LegacySurfaceParityDisposition.Consolidated, ["combat_damage_track"]),
            new LegacySurfaceParityExpectation("Other Info", LegacySurfaceParityDisposition.Consolidated, ["tab-info.profile", "identity_license_add", "identity_license_edit", "identity_license_delete"]),
            new LegacySurfaceParityExpectation("Pets & Cohorts", LegacySurfaceParityDisposition.Backed, ["tab-relationships.pets"]),
            new LegacySurfaceParityExpectation("Physical Condition Monitor", LegacySurfaceParityDisposition.Consolidated, ["combat_damage_track"]),
            new LegacySurfaceParityExpectation("Relationships", LegacySurfaceParityDisposition.Backed, ["tab-relationships.relationships"]),
            new LegacySurfaceParityExpectation("Skills", LegacySurfaceParityDisposition.Backed, ["tab-skills.skills"]),
            new LegacySurfaceParityExpectation("Spell Defense", LegacySurfaceParityDisposition.Backed, ["tab-info.spelldefense"]),
            new LegacySurfaceParityExpectation("Spells & Spirits", LegacySurfaceParityDisposition.Backed, ["tab-magician.spells", "tab-magician.spirits"]),
            new LegacySurfaceParityExpectation("Sprites & Complex Forms", LegacySurfaceParityDisposition.Backed, ["tab-technomancer.complexforms", "tab-technomancer.sprites"]),
            new LegacySurfaceParityExpectation("Street Gear", LegacySurfaceParityDisposition.Backed, ["tab-streetgear.gear", "tab-streetgear.armors", "tab-streetgear.weapons", "tab-streetgear.drugs", "tab-streetgear.lifestyles"]),
            new LegacySurfaceParityExpectation("Vehicles & Drones", LegacySurfaceParityDisposition.Backed, ["tab-vehicles.vehicles"]),
            new LegacySurfaceParityExpectation("Weapons", LegacySurfaceParityDisposition.Backed, ["tab-combat.weapons", "tab-gear.weapons"])
        }.ToDictionary(expectation => expectation.LegacyLabel, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, LegacySurfaceParityExpectation> ControlExpectations =
        new[]
        {
            new LegacySurfaceParityExpectation("create_entry", LegacySurfaceParityDisposition.Backed, ["create_entry"]),
            new LegacySurfaceParityExpectation("edit_entry", LegacySurfaceParityDisposition.Backed, ["edit_entry"]),
            new LegacySurfaceParityExpectation("delete_entry", LegacySurfaceParityDisposition.Backed, ["delete_entry"]),
            new LegacySurfaceParityExpectation("open_notes", LegacySurfaceParityDisposition.Backed, ["open_notes"]),
            new LegacySurfaceParityExpectation("identity_license_add", LegacySurfaceParityDisposition.Backed, ["identity_license_add"]),
            new LegacySurfaceParityExpectation("identity_license_edit", LegacySurfaceParityDisposition.Backed, ["identity_license_edit"]),
            new LegacySurfaceParityExpectation("identity_license_delete", LegacySurfaceParityDisposition.Backed, ["identity_license_delete"]),
            new LegacySurfaceParityExpectation("move_up", LegacySurfaceParityDisposition.Backed, ["move_up"]),
            new LegacySurfaceParityExpectation("move_down", LegacySurfaceParityDisposition.Backed, ["move_down"]),
            new LegacySurfaceParityExpectation("toggle_free_paid", LegacySurfaceParityDisposition.Backed, ["toggle_free_paid"]),
            new LegacySurfaceParityExpectation("show_source", LegacySurfaceParityDisposition.Backed, ["show_source"]),
            new LegacySurfaceParityExpectation("gear_add", LegacySurfaceParityDisposition.Backed, ["gear_add"]),
            new LegacySurfaceParityExpectation("gear_edit", LegacySurfaceParityDisposition.Backed, ["gear_edit"]),
            new LegacySurfaceParityExpectation("gear_delete", LegacySurfaceParityDisposition.Backed, ["gear_delete"]),
            new LegacySurfaceParityExpectation("runner_benchmark", LegacySurfaceParityDisposition.Backed, ["runner_benchmark"]),
            new LegacySurfaceParityExpectation("runner_what_if", LegacySurfaceParityDisposition.Backed, ["runner_what_if"]),
            new LegacySurfaceParityExpectation("runner_cohort_privacy", LegacySurfaceParityDisposition.Backed, ["runner_cohort_privacy"]),
            new LegacySurfaceParityExpectation("gear_mount", LegacySurfaceParityDisposition.Backed, ["gear_mount"]),
            new LegacySurfaceParityExpectation("gear_source", LegacySurfaceParityDisposition.Backed, ["gear_source"]),
            new LegacySurfaceParityExpectation("cyberware_add", LegacySurfaceParityDisposition.Backed, ["cyberware_add"]),
            new LegacySurfaceParityExpectation("cyberware_edit", LegacySurfaceParityDisposition.Backed, ["cyberware_edit"]),
            new LegacySurfaceParityExpectation("cyberware_delete", LegacySurfaceParityDisposition.Backed, ["cyberware_delete"]),
            new LegacySurfaceParityExpectation("drug_add", LegacySurfaceParityDisposition.Backed, ["drug_add"]),
            new LegacySurfaceParityExpectation("drug_delete", LegacySurfaceParityDisposition.Backed, ["drug_delete"]),
            new LegacySurfaceParityExpectation("magic_add", LegacySurfaceParityDisposition.Backed, ["magic_add"]),
            new LegacySurfaceParityExpectation("magic_delete", LegacySurfaceParityDisposition.Backed, ["magic_delete"]),
            new LegacySurfaceParityExpectation("magic_bind", LegacySurfaceParityDisposition.Backed, ["magic_bind"]),
            new LegacySurfaceParityExpectation("magic_source", LegacySurfaceParityDisposition.Backed, ["magic_source"]),
            new LegacySurfaceParityExpectation("spell_add", LegacySurfaceParityDisposition.Backed, ["spell_add"]),
            new LegacySurfaceParityExpectation("adept_power_add", LegacySurfaceParityDisposition.Backed, ["adept_power_add"]),
            new LegacySurfaceParityExpectation("complex_form_add", LegacySurfaceParityDisposition.Backed, ["complex_form_add"]),
            new LegacySurfaceParityExpectation("sprite_add", LegacySurfaceParityDisposition.Backed, ["sprite_add"]),
            new LegacySurfaceParityExpectation("initiation_add", LegacySurfaceParityDisposition.Backed, ["initiation_add"]),
            new LegacySurfaceParityExpectation("spirit_add", LegacySurfaceParityDisposition.Backed, ["spirit_add"]),
            new LegacySurfaceParityExpectation("critter_power_add", LegacySurfaceParityDisposition.Backed, ["critter_power_add"]),
            new LegacySurfaceParityExpectation("matrix_program_add", LegacySurfaceParityDisposition.Backed, ["matrix_program_add"]),
            new LegacySurfaceParityExpectation("skill_add", LegacySurfaceParityDisposition.Backed, ["skill_add"]),
            new LegacySurfaceParityExpectation("skill_specialize", LegacySurfaceParityDisposition.Backed, ["skill_specialize"]),
            new LegacySurfaceParityExpectation("skill_remove", LegacySurfaceParityDisposition.Backed, ["skill_remove"]),
            new LegacySurfaceParityExpectation("skill_group", LegacySurfaceParityDisposition.Backed, ["skill_group"]),
            new LegacySurfaceParityExpectation("combat_add_weapon", LegacySurfaceParityDisposition.Backed, ["combat_add_weapon"]),
            new LegacySurfaceParityExpectation("combat_add_armor", LegacySurfaceParityDisposition.Backed, ["combat_add_armor"]),
            new LegacySurfaceParityExpectation("combat_reload", LegacySurfaceParityDisposition.Backed, ["combat_reload"]),
            new LegacySurfaceParityExpectation("combat_damage_track", LegacySurfaceParityDisposition.Backed, ["combat_damage_track"]),
            new LegacySurfaceParityExpectation("vehicle_add", LegacySurfaceParityDisposition.Backed, ["vehicle_add"]),
            new LegacySurfaceParityExpectation("vehicle_edit", LegacySurfaceParityDisposition.Backed, ["vehicle_edit"]),
            new LegacySurfaceParityExpectation("vehicle_delete", LegacySurfaceParityDisposition.Backed, ["vehicle_delete"]),
            new LegacySurfaceParityExpectation("vehicle_mod_add", LegacySurfaceParityDisposition.Backed, ["vehicle_mod_add"]),
            new LegacySurfaceParityExpectation("contact_add", LegacySurfaceParityDisposition.Backed, ["contact_add"]),
            new LegacySurfaceParityExpectation("contact_edit", LegacySurfaceParityDisposition.Backed, ["contact_edit"]),
            new LegacySurfaceParityExpectation("contact_remove", LegacySurfaceParityDisposition.Backed, ["contact_remove"]),
            new LegacySurfaceParityExpectation("contact_connection", LegacySurfaceParityDisposition.Backed, ["contact_connection"]),
            new LegacySurfaceParityExpectation("quality_add", LegacySurfaceParityDisposition.Backed, ["quality_add"]),
            new LegacySurfaceParityExpectation("quality_delete", LegacySurfaceParityDisposition.Backed, ["quality_delete"])
        }.ToDictionary(expectation => expectation.LegacyLabel, StringComparer.Ordinal);
}
