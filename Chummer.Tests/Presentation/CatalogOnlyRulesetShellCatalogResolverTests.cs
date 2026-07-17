#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
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

    [TestMethod]
    public void ResolveCommands_tabs_and_workspace_actions_keep_provider_backed_contract_shape_for_sr5_and_sr6()
    {
        CatalogOnlyRulesetShellCatalogResolver resolver = new();

        AssertSharedCommandContractParity(
            RulesetDefaults.Sr5,
            new Sr5RulesetShellDefinitionProvider().GetCommands(),
            resolver.ResolveCommands(RulesetDefaults.Sr5));
        AssertSharedCommandContractParity(
            RulesetDefaults.Sr6,
            new Sr6RulesetShellDefinitionProvider().GetCommands(),
            resolver.ResolveCommands(RulesetDefaults.Sr6));

        AssertSharedTabContractParity(
            RulesetDefaults.Sr5,
            new Sr5RulesetShellDefinitionProvider().GetNavigationTabs(),
            resolver.ResolveNavigationTabs(RulesetDefaults.Sr5));
        AssertSharedTabContractParity(
            RulesetDefaults.Sr6,
            new Sr6RulesetShellDefinitionProvider().GetNavigationTabs(),
            resolver.ResolveNavigationTabs(RulesetDefaults.Sr6));

        AssertSharedWorkspaceActionContractParity(
            RulesetDefaults.Sr5,
            new Sr5RulesetCatalogProvider().GetWorkspaceActions(),
            FlattenResolverActions(resolver, RulesetDefaults.Sr5));
        AssertSharedWorkspaceActionContractParity(
            RulesetDefaults.Sr6,
            new Sr6RulesetCatalogProvider().GetWorkspaceActions(),
            FlattenResolverActions(resolver, RulesetDefaults.Sr6));
    }

    private static IReadOnlyList<Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition> FlattenResolverActions(
        CatalogOnlyRulesetShellCatalogResolver resolver,
        string rulesetId)
        => resolver.ResolveNavigationTabs(rulesetId)
            .SelectMany(tab => resolver.ResolveWorkspaceActionsForTab(tab.Id, rulesetId))
            .GroupBy(action => action.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(action => action.Id, StringComparer.Ordinal)
            .ToArray();

    private static void AssertSharedCommandContractParity(
        string rulesetId,
        IReadOnlyList<Chummer.Contracts.Presentation.AppCommandDefinition> providerCommands,
        IReadOnlyList<Chummer.Contracts.Presentation.AppCommandDefinition> resolverCommands)
    {
        IReadOnlyDictionary<string, Chummer.Contracts.Presentation.AppCommandDefinition> resolverById = resolverCommands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        foreach (Chummer.Contracts.Presentation.AppCommandDefinition providerCommand in providerCommands.OrderBy(command => command.Id, StringComparer.Ordinal))
        {
            Assert.IsTrue(
                resolverById.TryGetValue(providerCommand.Id, out Chummer.Contracts.Presentation.AppCommandDefinition? resolverCommand),
                $"Resolver is missing provider-backed command '{providerCommand.Id}' for '{rulesetId}'.");
            Assert.AreEqual(providerCommand.LabelKey, resolverCommand.LabelKey, $"Label key drifted for command '{providerCommand.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerCommand.Group, resolverCommand.Group, $"Menu group drifted for command '{providerCommand.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerCommand.RequiresOpenCharacter, resolverCommand.RequiresOpenCharacter, $"Open-character requirement drifted for command '{providerCommand.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerCommand.EnabledByDefault, resolverCommand.EnabledByDefault, $"Default enablement drifted for command '{providerCommand.Id}' under '{rulesetId}'.");
            Assert.AreEqual(rulesetId, resolverCommand.RulesetId, $"Resolver command '{providerCommand.Id}' must stay scoped to '{rulesetId}'.");
        }
    }

    private static void AssertSharedTabContractParity(
        string rulesetId,
        IReadOnlyList<Chummer.Contracts.Presentation.NavigationTabDefinition> providerTabs,
        IReadOnlyList<Chummer.Contracts.Presentation.NavigationTabDefinition> resolverTabs)
    {
        IReadOnlyDictionary<string, Chummer.Contracts.Presentation.NavigationTabDefinition> resolverById = resolverTabs.ToDictionary(tab => tab.Id, StringComparer.Ordinal);

        foreach (Chummer.Contracts.Presentation.NavigationTabDefinition providerTab in providerTabs.OrderBy(tab => tab.Id, StringComparer.Ordinal))
        {
            Assert.IsTrue(
                resolverById.TryGetValue(providerTab.Id, out Chummer.Contracts.Presentation.NavigationTabDefinition? resolverTab),
                $"Resolver is missing provider-backed tab '{providerTab.Id}' for '{rulesetId}'.");
            Assert.AreEqual(providerTab.Label, resolverTab.Label, $"Visible label drifted for tab '{providerTab.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerTab.SectionId, resolverTab.SectionId, $"Section drifted for tab '{providerTab.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerTab.Group, resolverTab.Group, $"Group drifted for tab '{providerTab.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerTab.RequiresOpenCharacter, resolverTab.RequiresOpenCharacter, $"Open-character requirement drifted for tab '{providerTab.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerTab.EnabledByDefault, resolverTab.EnabledByDefault, $"Default enablement drifted for tab '{providerTab.Id}' under '{rulesetId}'.");
            Assert.AreEqual(rulesetId, resolverTab.RulesetId, $"Resolver tab '{providerTab.Id}' must stay scoped to '{rulesetId}'.");
        }
    }

    private static void AssertSharedWorkspaceActionContractParity(
        string rulesetId,
        IReadOnlyList<Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition> providerActions,
        IReadOnlyList<Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition> resolverActions)
    {
        IReadOnlyDictionary<string, Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition> resolverById = resolverActions.ToDictionary(action => action.Id, StringComparer.Ordinal);

        foreach (Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition providerAction in providerActions.OrderBy(action => action.Id, StringComparer.Ordinal))
        {
            string resolverActionId = ResolveCompatibilityResolverActionId(providerAction.Id);
            Assert.IsTrue(
                resolverById.TryGetValue(resolverActionId, out Chummer.Contracts.Presentation.WorkspaceSurfaceActionDefinition? resolverAction),
                $"Resolver is missing provider-backed workspace action '{providerAction.Id}' for '{rulesetId}'.");
            Assert.AreEqual(providerAction.Label, resolverAction.Label, $"Visible label drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerAction.TabId, resolverAction.TabId, $"Tab drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerAction.Kind, resolverAction.Kind, $"Kind drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerAction.TargetId, resolverAction.TargetId, $"Target drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerAction.RequiresOpenCharacter, resolverAction.RequiresOpenCharacter, $"Open-character requirement drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(providerAction.EnabledByDefault, resolverAction.EnabledByDefault, $"Default enablement drifted for workspace action '{providerAction.Id}' under '{rulesetId}'.");
            Assert.AreEqual(rulesetId, resolverAction.RulesetId, $"Resolver workspace action '{providerAction.Id}' must stay scoped to '{rulesetId}'.");
        }
    }

    private static string ResolveCompatibilityResolverActionId(string providerActionId)
        => string.Equals(providerActionId, "tab-create.intake", StringComparison.Ordinal)
            ? "tab-create.build-lab"
            : providerActionId;

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
        "refresh_character",
        "print_character",
        "export_character",
        "copy",
        "paste",
        "dice_roller",
        "auto_alice",
        "new_character_origin",
        "global_settings",
        "switch_ruleset",
        "runtime_inspector",
        "character_settings",
        "translator",
        "hero_lab_importer",
        "xml_editor",
        "open_sourcebooks",
        "open_errata",
        "open_custom_data",
        "update_data_packs",
        "validate_data_scope",
        "open_data_folder",
        "master_index",
        "character_roster",
        "data_exporter",
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
        "about",
        "report_bug",
        "update",
        "restart"
    ];

    private static readonly string[] ExpectedTabIds =
    [
        "tab-create",
        "tab-info",
        "tab-attributes",
        "tab-skills",
        "tab-qualities",
        "tab-magician",
        "tab-adept",
        "tab-technomancer",
        "tab-combat",
        "tab-streetgear",
        "tab-gear",
        "tab-armor",
        "tab-cyberware",
        "tab-vehicles",
        "tab-lifestyle",
        "tab-relationships",
        "tab-contacts",
        "tab-rules",
        "tab-notes",
        "tab-karma",
        "tab-calendar",
        "tab-improvements"
    ];

    private static readonly string[] ExpectedInfoActionIds =
    [
        "tab-info.summary",
        "tab-info.validate",
        "tab-info.metadata",
        "tab-info.profile",
        "tab-info.progress",
        "tab-info.rules",
        "tab-info.build",
        "tab-info.movement",
        "tab-info.awakening",
        "tab-info.spelldefense",
        "tab-info.attributes",
        "tab-info.attributedetails",
        "tab-info.skills",
        "tab-info.qualities",
        "tab-info.contacts",
        "tab-info.spells",
        "tab-info.powers",
        "tab-info.complexforms",
        "tab-info.martialarts"
    ];

    private static readonly TabActionExpectation[] ExpectedTabActionInventory =
    [
        new("tab-create", ["tab-create.build-lab"]),
        new("tab-info", ["tab-info.summary", "tab-info.validate", "tab-info.metadata", "tab-info.profile", "tab-info.progress", "tab-info.rules", "tab-info.build", "tab-info.movement", "tab-info.awakening", "tab-info.spelldefense", "tab-info.attributes", "tab-info.attributedetails", "tab-info.skills", "tab-info.qualities", "tab-info.contacts", "tab-info.spells", "tab-info.powers", "tab-info.complexforms", "tab-info.martialarts"]),
        new("tab-attributes", ["tab-attributes.attributes", "tab-attributes.attributedetails", "tab-attributes.limitmodifiers"]),
        new("tab-skills", ["tab-skills.skills", "tab-skills.martialarts"]),
        new("tab-qualities", ["tab-qualities.qualities", "tab-qualities.improvements"]),
        new("tab-magician", ["tab-magician.spells", "tab-magician.spirits", "tab-magician.foci", "tab-magician.aiprograms", "tab-magician.limitmodifiers", "tab-magician.metamagics", "tab-magician.arts", "tab-magician.initiationgrades", "tab-magician.critterpowers", "tab-magician.mentorspirits", "tab-magician.expenses", "tab-magician.calendar", "tab-magician.improvements"]),
        new("tab-adept", ["tab-adept.powers", "tab-adept.metamagics", "tab-adept.initiationgrades"]),
        new("tab-technomancer", ["tab-technomancer.complexforms", "tab-technomancer.sprites", "tab-technomancer.aiprograms"]),
        new("tab-combat", ["tab-combat.weapons", "tab-combat.armors", "tab-combat.drugs", "tab-combat.movement", "tab-combat.conditionmonitor"]),
        new("tab-streetgear", ["tab-streetgear.gear", "tab-streetgear.armors", "tab-streetgear.weapons", "tab-streetgear.drugs", "tab-streetgear.lifestyles"]),
        new("tab-gear", ["tab-gear.inventory", "tab-gear.gear", "tab-gear.gearlocations", "tab-gear.weapons", "tab-gear.weaponaccessories", "tab-gear.weaponlocations", "tab-gear.armors", "tab-gear.armormods", "tab-gear.armorlocations", "tab-gear.cyberwares", "tab-gear.drugs", "tab-gear.lifestyles", "tab-gear.vehicles", "tab-gear.vehiclemods", "tab-gear.vehiclelocations", "tab-gear.sources", "tab-gear.customdatadirectorynames"]),
        new("tab-armor", ["tab-armor.armors", "tab-armor.armormods", "tab-armor.armorlocations"]),
        new("tab-cyberware", ["tab-cyberware.cyberwares", "tab-cyberware.foci"]),
        new("tab-vehicles", ["tab-vehicles.vehicles", "tab-vehicles.vehiclemods", "tab-vehicles.vehiclelocations"]),
        new("tab-lifestyle", ["tab-lifestyle.lifestyles", "tab-lifestyle.expenses", "tab-lifestyle.sources"]),
        new("tab-relationships", ["tab-relationships.relationships", "tab-relationships.contacts", "tab-relationships.enemies", "tab-relationships.pets"]),
        new("tab-contacts", ["tab-contacts.contacts", "tab-contacts.mentorspirits"]),
        new("tab-rules", ["tab-rules.rules"]),
        new("tab-notes", ["tab-notes.metadata", "tab-notes.data_exporter"]),
        new("tab-karma", ["tab-karma.summary", "tab-karma.expenses", "tab-karma.calendar", "tab-karma.progress"]),
        new("tab-calendar", ["tab-calendar.calendar", "tab-calendar.expenses"]),
        new("tab-improvements", ["tab-improvements.improvements", "tab-improvements.build", "tab-improvements.progress"])
    ];
}
