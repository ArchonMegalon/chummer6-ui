#nullable enable annotations

using System;
using System.Linq;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CatalogOnlyRulesetShellCatalogResolverTests
{
    [TestMethod]
    public void ResolveCommands_and_navigation_tabs_clone_requested_ruleset()
    {
        CatalogOnlyRulesetShellCatalogResolver resolver = new();

        foreach (string rulesetId in SupportedRulesets)
        {
            CollectionAssert.AreEqual(
                ExpectedCommandIds,
                resolver.ResolveCommands(rulesetId).Select(command => command.Id).ToArray(),
                $"Unexpected command inventory for '{rulesetId}'.");
            Assert.IsTrue(
                resolver.ResolveCommands(rulesetId).All(command => string.Equals(command.RulesetId, rulesetId, StringComparison.Ordinal)),
                $"All commands must clone the requested ruleset '{rulesetId}'.");

            CollectionAssert.AreEqual(
                ExpectedTabIds,
                resolver.ResolveNavigationTabs(rulesetId).Select(tab => tab.Id).ToArray(),
                $"Unexpected navigation tab inventory for '{rulesetId}'.");
            Assert.IsTrue(
                resolver.ResolveNavigationTabs(rulesetId).All(tab => string.Equals(tab.RulesetId, rulesetId, StringComparison.Ordinal)),
                $"All navigation tabs must clone the requested ruleset '{rulesetId}'.");
        }
    }

    [TestMethod]
    public void ResolveWorkspaceActionsForTab_returns_ruleset_cloned_tab_scoped_inventory()
    {
        CatalogOnlyRulesetShellCatalogResolver resolver = new();

        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (TabActionExpectation expectation in ExpectedTabActionInventory)
            {
                var actions = resolver.ResolveWorkspaceActionsForTab(expectation.TabId, rulesetId).ToArray();

                CollectionAssert.AreEqual(
                    expectation.ActionIds,
                    actions.Select(action => action.Id).ToArray(),
                    $"Unexpected workspace action inventory for '{expectation.TabId}' under '{rulesetId}'.");
                Assert.IsTrue(
                    actions.All(action => string.Equals(action.RulesetId, rulesetId, StringComparison.Ordinal)),
                    $"All workspace actions for '{expectation.TabId}' must clone the requested ruleset '{rulesetId}'.");
                Assert.IsTrue(
                    actions.All(action => string.Equals(action.TabId, expectation.TabId, StringComparison.Ordinal)),
                    $"Workspace actions for '{expectation.TabId}' must remain tab-scoped.");
            }
        }
    }

    [TestMethod]
    public void ResolveWorkspaceActionsForTab_falls_back_to_tab_info_when_requested_tab_is_unknown()
    {
        CatalogOnlyRulesetShellCatalogResolver resolver = new();

        var actions = resolver.ResolveWorkspaceActionsForTab("tab-unknown", RulesetDefaults.Sr6).ToArray();

        CollectionAssert.AreEqual(
            ExpectedInfoActionIds,
            actions.Select(action => action.Id).ToArray(),
            "Unknown tabs must fall back to the tab-info action inventory.");
        Assert.IsTrue(actions.All(action => string.Equals(action.RulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal)));
    }

    private sealed record TabActionExpectation(string TabId, string[] ActionIds);

    private static readonly string[] SupportedRulesets =
    [
        RulesetDefaults.Sr4,
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6
    ];

    private static readonly string[] ExpectedCommandIds =
    [
        "file",
        "edit",
        "special",
        "tools",
        "windows",
        "help",
        "new_character",
        "new_critter",
        "open_character",
        "open_for_printing",
        "open_for_export",
        "save_character",
        "save_character_as",
        "print_character",
        "export_character",
        "copy",
        "paste",
        "dice_roller",
        "auto_alice",
        "global_settings",
        "character_settings",
        "update",
        "restart",
        "switch_ruleset",
        "translator",
        "xml_editor",
        "hero_lab_importer",
        "master_index",
        "character_roster",
        "data_exporter",
        "report_bug",
        "print_setup",
        "print_multiple",
        "exit",
        "new_window",
        "close_window",
        "close_all",
        "wiki",
        "discord",
        "show_login_video",
        "revision_history",
        "dumpshock",
        "about"
    ];

    private static readonly string[] ExpectedTabIds =
    [
        "tab-info",
        "tab-attributes",
        "tab-skills",
        "tab-qualities",
        "tab-magician",
        "tab-combat",
        "tab-gear",
        "tab-cyberware",
        "tab-adept",
        "tab-contacts",
        "tab-rules",
        "tab-notes"
    ];

    private static readonly string[] ExpectedInfoActionIds =
    [
        "tab-info.summary",
        "tab-info.validate",
        "tab-info.profile",
        "tab-info.rules",
        "tab-info.attributes"
    ];

    private static readonly TabActionExpectation[] ExpectedTabActionInventory =
    [
        new("tab-info", ["tab-info.summary", "tab-info.validate", "tab-info.profile", "tab-info.rules", "tab-info.attributes"]),
        new("tab-skills", ["tab-skills.skills"]),
        new("tab-qualities", ["tab-qualities.qualities"]),
        new("tab-magician", ["tab-magician.spells", "tab-magician.spirits", "tab-magician.critterpowers"]),
        new("tab-combat", ["tab-combat.weapons", "tab-combat.armors"]),
        new("tab-gear", ["tab-gear.inventory", "tab-gear.drugs", "tab-gear.vehicles"]),
        new("tab-cyberware", ["tab-cyberware.cyberwares"]),
        new("tab-adept", ["tab-adept.powers", "tab-adept.complexforms", "tab-adept.aiprograms", "tab-adept.metamagics", "tab-adept.initiationgrades"]),
        new("tab-contacts", ["tab-contacts.contacts"]),
        new("tab-rules", ["tab-rules.rules"]),
        new("tab-notes", ["tab-notes.metadata"])
    ];
}
