using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

public sealed class CatalogOnlyRulesetShellCatalogResolver : IRulesetShellCatalogResolver
{
    private const string DefaultRulesetEnvironmentVariable = "CHUMMER_DEFAULT_RULESET";
    private static readonly string[] ClassicToolbarLeadCommands = ["save_character", "print_character", "copy"];

    private static readonly IReadOnlyList<WorkflowDefinition> WorkflowDefinitions =
    [
        new(WorkflowDefinitionIds.LibraryShell, "Library Shell", ["catalog.shell.menu", "catalog.shell.toolbar"], false),
        new(WorkflowDefinitionIds.CareerWorkbench, "Career Workbench", ["catalog.career.section"], true),
        new(WorkflowDefinitionIds.SelectionDialog, "Selection Dialog", ["catalog.selection.dialog"], false),
        new(WorkflowDefinitionIds.DiceTool, "Utility Tooling", ["catalog.tool.dice", "catalog.tool.roster"], false),
        new(WorkflowDefinitionIds.SessionDashboard, "Session Dashboard", ["catalog.session.summary"], true, true)
    ];

    private static readonly IReadOnlyList<WorkflowSurfaceDefinition> WorkflowSurfaces =
    [
        new("catalog.shell.menu", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.MenuBar, WorkflowLayoutTokens.ShellFrame, ["file", "edit", "special", "tools", "windows", "help"]),
        new("catalog.shell.toolbar", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.ToolStrip, WorkflowLayoutTokens.ShellFrame, ["save_character", "print_character", "copy", "new_character", "open_character", "close_window"]),
        new("catalog.career.section", WorkflowDefinitionIds.CareerWorkbench, WorkflowSurfaceKinds.Workbench, ShellRegionIds.SectionPane, WorkflowLayoutTokens.CareerWorkbench, ["tab-create.build-lab", "tab-info.summary", "tab-info.profile", "tab-skills.skills"]),
        new("catalog.selection.dialog", WorkflowDefinitionIds.SelectionDialog, WorkflowSurfaceKinds.Dialog, ShellRegionIds.DialogHost, WorkflowLayoutTokens.SelectionDialog, ["tab-gear.inventory"]),
        new("catalog.tool.dice", WorkflowDefinitionIds.DiceTool, WorkflowSurfaceKinds.Tool, ShellRegionIds.DialogHost, WorkflowLayoutTokens.ToolPanel, ["dice_roller"]),
        new("catalog.tool.roster", WorkflowDefinitionIds.DiceTool, WorkflowSurfaceKinds.Tool, ShellRegionIds.DialogHost, WorkflowLayoutTokens.ToolPanel, ["character_roster"]),
        new("catalog.session.summary", WorkflowDefinitionIds.SessionDashboard, WorkflowSurfaceKinds.Dashboard, ShellRegionIds.SummaryHeader, WorkflowLayoutTokens.SessionDashboard, ["tab-info.summary", "tab-info.validate"])
    ];

    private static readonly IReadOnlyList<AppCommandDefinition> CompatibilityCommands =
    [
        Command("file", "command.file", "menu", false),
        Command("edit", "command.edit", "menu", false),
        Command("special", "command.special", "menu", false),
        Command("tools", "command.tools", "menu", false),
        Command("windows", "command.windows", "menu", false),
        Command("help", "command.help", "menu", false),
        Command("new_character", "command.new_character", "file", false),
        Command("new_critter", "command.new_critter", "file", false),
        Command("open_character", "command.open_character", "file", false),
        Command("open_for_printing", "command.open_for_printing", "file", false),
        Command("open_for_export", "command.open_for_export", "file", false),
        Command("save_character", "command.save_character", "file", true),
        Command("save_character_as", "command.save_character_as", "file", true),
        Command("refresh_character", "command.refresh_character", "file", true),
        Command("print_character", "command.print_character", "file", true),
        Command("export_character", "command.export_character", "file", true),
        Command("copy", "command.copy", "edit", true),
        Command("paste", "command.paste", "edit", true),
        Command("dice_roller", "command.dice_roller", "tools", false),
        Command(DesktopAliceAssistant.CommandId, "command.auto_alice", "tools", false),
        Command("new_character_origin", "command.new_character_origin", "tools", false),
        Command("global_settings", "command.global_settings", "tools", false),
        Command("switch_ruleset", "command.switch_ruleset", "special", false),
        Command(AppCommandIds.RuntimeInspector, "command.runtime_inspector", "tools", false),
        Command("character_settings", "command.character_settings", "tools", true),
        Command("translator", "command.translator", "tools", false),
        Command("hero_lab_importer", "command.hero_lab_importer", "tools", false),
        Command("xml_editor", "command.xml_editor", "tools", false),
        Command("open_sourcebooks", "command.open_sourcebooks", "tools", false),
        Command("open_errata", "command.open_errata", "tools", false),
        Command("open_custom_data", "command.open_custom_data", "tools", false),
        Command("update_data_packs", "command.update_data_packs", "tools", false),
        Command("validate_data_scope", "command.validate_data_scope", "tools", false),
        Command("open_data_folder", "command.open_data_folder", "tools", false),
        Command("master_index", "command.master_index", "tools", false),
        Command("character_roster", "command.character_roster", "tools", false),
        Command("data_exporter", "command.data_exporter", "tools", true),
        Command("print_setup", "command.print_setup", "file", false),
        Command("print_multiple", "command.print_multiple", "file", false),
        Command("exit", "command.exit", "file", false),
        Command("new_window", "command.new_window", "windows", false),
        Command("close_window", "command.close_window", "windows", false),
        Command("close_all", "command.close_all", "windows", false),
        Command("wiki", "command.wiki", "help", false),
        Command("discord", "command.discord", "help", false),
        Command("show_login_video", "command.show_login_video", "help", false),
        Command("revision_history", "command.revision_history", "help", false),
        Command("dumpshock", "command.dumpshock", "help", false),
        Command("about", "command.about", "help", false),
        Command("report_bug", "command.report_bug", "help", false),
        Command("update", "command.update", "help", false),
        Command("restart", "command.restart", "help", false)
    ];

    private static readonly IReadOnlyList<NavigationTabDefinition> CompatibilityTabs =
    [
        Tab("tab-create", "Create", "build-lab", "character"),
        Tab("tab-info", "Info", "profile", "character"),
        Tab("tab-attributes", "Attributes", "attributes", "character"),
        Tab("tab-skills", "Skills", "skills", "character"),
        Tab("tab-qualities", "Qualities", "qualities", "character"),
        Tab("tab-magician", "Magician", "spells", "character"),
        Tab("tab-adept", "Adept", "powers", "character"),
        Tab("tab-technomancer", "Technomancer", "complexforms", "character"),
        Tab("tab-combat", "Combat", "weapons", "character"),
        Tab("tab-streetgear", "Street Gear", "gear", "character"),
        Tab("tab-gear", "Gear", "gear", "character"),
        Tab("tab-armor", "Armor", "armors", "character"),
        Tab("tab-cyberware", "Cyberware/Bioware", "cyberwares", "character"),
        Tab("tab-vehicles", "Vehicles", "vehicles", "character"),
        Tab("tab-lifestyle", "Lifestyle", "lifestyles", "character"),
        Tab("tab-relationships", "Relationships", "relationships", "character"),
        Tab("tab-contacts", "Contacts", "contacts", "character"),
        Tab("tab-rules", "Rules", "rules", "character"),
        Tab("tab-notes", "Notes", "profile", "character"),
        Tab("tab-karma", "Karma & Nuyen", "karmasummary", "character"),
        Tab("tab-calendar", "Calendar", "calendar", "character"),
        Tab("tab-improvements", "Improvements", "improvements", "character")
    ];

    private static readonly IReadOnlyList<WorkspaceSurfaceActionDefinition> CompatibilityActions =
    [
        Action("tab-create.build-lab", "Build Lab", "tab-create", WorkspaceSurfaceActionKind.Section, "build-lab"),
        Action("tab-info.summary", "Summary", "tab-info", WorkspaceSurfaceActionKind.Summary, "summary"),
        Action("tab-info.validate", "Validate", "tab-info", WorkspaceSurfaceActionKind.Validate, "validate"),
        Action("tab-info.metadata", "Apply Metadata", "tab-info", WorkspaceSurfaceActionKind.Metadata, "metadata"),
        Action("tab-info.profile", "Profile", "tab-info", WorkspaceSurfaceActionKind.Section, "profile"),
        Action("tab-info.progress", "Progress", "tab-info", WorkspaceSurfaceActionKind.Section, "progress"),
        Action("tab-info.rules", "Rules", "tab-info", WorkspaceSurfaceActionKind.Section, "rules"),
        Action("tab-info.build", "Build", "tab-info", WorkspaceSurfaceActionKind.Section, "build"),
        Action("tab-info.movement", "Movement", "tab-info", WorkspaceSurfaceActionKind.Section, "movement"),
        Action("tab-info.awakening", "Awakening", "tab-info", WorkspaceSurfaceActionKind.Section, "awakening"),
        Action("tab-info.spelldefense", "Spell Defense", "tab-info", WorkspaceSurfaceActionKind.Section, "spelldefense"),
        Action("tab-info.attributes", "Attributes", "tab-info", WorkspaceSurfaceActionKind.Section, "attributes"),
        Action("tab-info.attributedetails", "Attribute Details", "tab-info", WorkspaceSurfaceActionKind.Section, "attributedetails"),
        Action("tab-info.skills", "Skills", "tab-info", WorkspaceSurfaceActionKind.Section, "skills"),
        Action("tab-info.qualities", "Qualities", "tab-info", WorkspaceSurfaceActionKind.Section, "qualities"),
        Action("tab-info.contacts", "Contacts", "tab-info", WorkspaceSurfaceActionKind.Section, "contacts"),
        Action("tab-info.spells", "Spells", "tab-info", WorkspaceSurfaceActionKind.Section, "spells"),
        Action("tab-info.powers", "Powers", "tab-info", WorkspaceSurfaceActionKind.Section, "powers"),
        Action("tab-info.complexforms", "Complex Forms", "tab-info", WorkspaceSurfaceActionKind.Section, "complexforms"),
        Action("tab-info.martialarts", "Martial Arts", "tab-info", WorkspaceSurfaceActionKind.Section, "martialarts"),

        Action("tab-skills.skills", "Skills", "tab-skills", WorkspaceSurfaceActionKind.Section, "skills"),
        Action("tab-skills.martialarts", "Martial Arts", "tab-skills", WorkspaceSurfaceActionKind.Section, "martialarts"),
        Action("tab-qualities.qualities", "Qualities", "tab-qualities", WorkspaceSurfaceActionKind.Section, "qualities"),
        Action("tab-qualities.improvements", "Improvements", "tab-qualities", WorkspaceSurfaceActionKind.Section, "improvements"),
        Action("tab-magician.spells", "Spells", "tab-magician", WorkspaceSurfaceActionKind.Section, "spells"),
        Action("tab-magician.spirits", "Spirits", "tab-magician", WorkspaceSurfaceActionKind.Section, "spirits"),
        Action("tab-magician.foci", "Foci", "tab-magician", WorkspaceSurfaceActionKind.Section, "foci"),
        Action("tab-magician.aiprograms", "AI Programs", "tab-magician", WorkspaceSurfaceActionKind.Section, "aiprograms"),
        Action("tab-magician.limitmodifiers", "Limit Modifiers", "tab-magician", WorkspaceSurfaceActionKind.Section, "limitmodifiers"),
        Action("tab-magician.metamagics", "Metamagics", "tab-magician", WorkspaceSurfaceActionKind.Section, "metamagics"),
        Action("tab-magician.arts", "Arts", "tab-magician", WorkspaceSurfaceActionKind.Section, "arts"),
        Action("tab-magician.initiationgrades", "Initiation Grades", "tab-magician", WorkspaceSurfaceActionKind.Section, "initiationgrades"),
        Action("tab-magician.critterpowers", "Critter Powers", "tab-magician", WorkspaceSurfaceActionKind.Section, "critterpowers"),
        Action("tab-magician.mentorspirits", "Mentor Spirits", "tab-magician", WorkspaceSurfaceActionKind.Section, "mentorspirits"),
        Action("tab-magician.expenses", "Expenses", "tab-magician", WorkspaceSurfaceActionKind.Section, "expenses"),
        Action("tab-magician.calendar", "Calendar", "tab-magician", WorkspaceSurfaceActionKind.Section, "calendar"),
        Action("tab-magician.improvements", "Improvements", "tab-magician", WorkspaceSurfaceActionKind.Section, "improvements"),
        Action("tab-combat.weapons", "Weapons", "tab-combat", WorkspaceSurfaceActionKind.Section, "weapons"),
        Action("tab-combat.armors", "Armor", "tab-combat", WorkspaceSurfaceActionKind.Section, "armors"),
        Action("tab-combat.drugs", "Drugs", "tab-combat", WorkspaceSurfaceActionKind.Section, "drugs"),
        Action("tab-combat.movement", "Movement", "tab-combat", WorkspaceSurfaceActionKind.Section, "movement"),
        Action("tab-combat.conditionmonitor", "Condition Monitor", "tab-combat", WorkspaceSurfaceActionKind.Section, "conditionmonitor"),
        Action("tab-gear.inventory", "Inventory", "tab-gear", WorkspaceSurfaceActionKind.Section, "inventory"),
        Action("tab-gear.gear", "Gear", "tab-gear", WorkspaceSurfaceActionKind.Section, "gear"),
        Action("tab-gear.gearlocations", "Gear Locations", "tab-gear", WorkspaceSurfaceActionKind.Section, "gearlocations"),
        Action("tab-gear.weapons", "Weapons", "tab-gear", WorkspaceSurfaceActionKind.Section, "weapons"),
        Action("tab-gear.weaponaccessories", "Weapon Accessories", "tab-gear", WorkspaceSurfaceActionKind.Section, "weaponaccessories"),
        Action("tab-gear.weaponlocations", "Weapon Locations", "tab-gear", WorkspaceSurfaceActionKind.Section, "weaponlocations"),
        Action("tab-gear.armors", "Armors", "tab-gear", WorkspaceSurfaceActionKind.Section, "armors"),
        Action("tab-gear.armormods", "Armor Mods", "tab-gear", WorkspaceSurfaceActionKind.Section, "armormods"),
        Action("tab-gear.armorlocations", "Armor Locations", "tab-gear", WorkspaceSurfaceActionKind.Section, "armorlocations"),
        Action("tab-gear.cyberwares", "Cyberwares", "tab-gear", WorkspaceSurfaceActionKind.Section, "cyberwares"),
        Action("tab-gear.drugs", "Drugs", "tab-gear", WorkspaceSurfaceActionKind.Section, "drugs"),
        Action("tab-gear.lifestyles", "Lifestyles", "tab-gear", WorkspaceSurfaceActionKind.Section, "lifestyles"),
        Action("tab-gear.vehicles", "Vehicles", "tab-gear", WorkspaceSurfaceActionKind.Section, "vehicles"),
        Action("tab-gear.vehiclemods", "Vehicle Mods", "tab-gear", WorkspaceSurfaceActionKind.Section, "vehiclemods"),
        Action("tab-gear.vehiclelocations", "Vehicle Locations", "tab-gear", WorkspaceSurfaceActionKind.Section, "vehiclelocations"),
        Action("tab-gear.sources", "Sources", "tab-gear", WorkspaceSurfaceActionKind.Section, "sources"),
        Action("tab-gear.customdatadirectorynames", "Custom Data Dirs", "tab-gear", WorkspaceSurfaceActionKind.Section, "customdatadirectorynames"),

        Action("tab-attributes.attributes", "Attributes Summary", "tab-attributes", WorkspaceSurfaceActionKind.Section, "attributes"),
        Action("tab-attributes.attributedetails", "Attribute Details", "tab-attributes", WorkspaceSurfaceActionKind.Section, "attributedetails"),
        Action("tab-attributes.limitmodifiers", "Limit Modifiers", "tab-attributes", WorkspaceSurfaceActionKind.Section, "limitmodifiers"),

        Action("tab-adept.powers", "Adept Powers", "tab-adept", WorkspaceSurfaceActionKind.Section, "powers"),
        Action("tab-adept.metamagics", "Metamagics", "tab-adept", WorkspaceSurfaceActionKind.Section, "metamagics"),
        Action("tab-adept.initiationgrades", "Initiation/Submersion", "tab-adept", WorkspaceSurfaceActionKind.Section, "initiationgrades"),
        Action("tab-technomancer.complexforms", "Complex Forms", "tab-technomancer", WorkspaceSurfaceActionKind.Section, "complexforms"),
        Action("tab-technomancer.sprites", "Sprites", "tab-technomancer", WorkspaceSurfaceActionKind.Section, "sprites"),
        Action("tab-technomancer.aiprograms", "Advanced Programs", "tab-technomancer", WorkspaceSurfaceActionKind.Section, "aiprograms"),

        Action("tab-armor.armors", "Armor Items", "tab-armor", WorkspaceSurfaceActionKind.Section, "armors"),
        Action("tab-armor.armormods", "Armor Mods", "tab-armor", WorkspaceSurfaceActionKind.Section, "armormods"),
        Action("tab-armor.armorlocations", "Armor Locations", "tab-armor", WorkspaceSurfaceActionKind.Section, "armorlocations"),
        Action("tab-streetgear.gear", "Gear", "tab-streetgear", WorkspaceSurfaceActionKind.Section, "gear"),
        Action("tab-streetgear.armors", "Armor", "tab-streetgear", WorkspaceSurfaceActionKind.Section, "armors"),
        Action("tab-streetgear.weapons", "Weapons", "tab-streetgear", WorkspaceSurfaceActionKind.Section, "weapons"),
        Action("tab-streetgear.drugs", "Drugs", "tab-streetgear", WorkspaceSurfaceActionKind.Section, "drugs"),
        Action("tab-streetgear.lifestyles", "Lifestyles", "tab-streetgear", WorkspaceSurfaceActionKind.Section, "lifestyles"),
        Action("tab-cyberware.cyberwares", "Cyberware/Bioware", "tab-cyberware", WorkspaceSurfaceActionKind.Section, "cyberwares"),
        Action("tab-cyberware.foci", "Foci", "tab-cyberware", WorkspaceSurfaceActionKind.Section, "foci"),
        Action("tab-vehicles.vehicles", "Vehicles", "tab-vehicles", WorkspaceSurfaceActionKind.Section, "vehicles"),
        Action("tab-vehicles.vehiclemods", "Vehicle Mods", "tab-vehicles", WorkspaceSurfaceActionKind.Section, "vehiclemods"),
        Action("tab-vehicles.vehiclelocations", "Vehicle Locations", "tab-vehicles", WorkspaceSurfaceActionKind.Section, "vehiclelocations"),
        Action("tab-lifestyle.lifestyles", "Lifestyles", "tab-lifestyle", WorkspaceSurfaceActionKind.Section, "lifestyles"),
        Action("tab-lifestyle.expenses", "Expenses", "tab-lifestyle", WorkspaceSurfaceActionKind.Section, "expenses"),
        Action("tab-lifestyle.sources", "Sources", "tab-lifestyle", WorkspaceSurfaceActionKind.Section, "sources"),
        Action("tab-relationships.relationships", "Relationships", "tab-relationships", WorkspaceSurfaceActionKind.Section, "relationships"),
        Action("tab-relationships.contacts", "Contacts", "tab-relationships", WorkspaceSurfaceActionKind.Section, "contacts"),
        Action("tab-relationships.enemies", "Enemies", "tab-relationships", WorkspaceSurfaceActionKind.Section, "enemies"),
        Action("tab-relationships.pets", "Pets & Cohorts", "tab-relationships", WorkspaceSurfaceActionKind.Section, "pets"),
        Action("tab-contacts.contacts", "Contacts", "tab-contacts", WorkspaceSurfaceActionKind.Section, "contacts"),
        Action("tab-contacts.mentorspirits", "Mentors/Spirits", "tab-contacts", WorkspaceSurfaceActionKind.Section, "mentorspirits"),
        Action("tab-rules.rules", "Rules", "tab-rules", WorkspaceSurfaceActionKind.Section, "rules"),
        Action("tab-notes.metadata", "Save Notes", "tab-notes", WorkspaceSurfaceActionKind.Metadata, "metadata"),
        Action("tab-notes.data_exporter", "Export Notes Snapshot", "tab-notes", WorkspaceSurfaceActionKind.Command, "data_exporter"),
        Action("tab-karma.summary", "Karma Summary", "tab-karma", WorkspaceSurfaceActionKind.Section, "karmasummary"),
        Action("tab-karma.expenses", "Expenses", "tab-karma", WorkspaceSurfaceActionKind.Section, "expenses"),
        Action("tab-karma.calendar", "Calendar", "tab-karma", WorkspaceSurfaceActionKind.Section, "calendar"),
        Action("tab-karma.progress", "Progress", "tab-karma", WorkspaceSurfaceActionKind.Section, "progress"),
        Action("tab-calendar.calendar", "Calendar Entries", "tab-calendar", WorkspaceSurfaceActionKind.Section, "calendar"),
        Action("tab-calendar.expenses", "Expense Timeline", "tab-calendar", WorkspaceSurfaceActionKind.Section, "expenses"),
        Action("tab-improvements.improvements", "Improvements", "tab-improvements", WorkspaceSurfaceActionKind.Section, "improvements"),
        Action("tab-improvements.build", "Build Snapshot", "tab-improvements", WorkspaceSurfaceActionKind.Section, "build"),
        Action("tab-improvements.progress", "Career Progress", "tab-improvements", WorkspaceSurfaceActionKind.Section, "progress")
    ];

    public IReadOnlyList<AppCommandDefinition> ResolveCommands(string? rulesetId)
    {
        // Compatibility target: AppCommandCatalog.ForRuleset(rulesetId)
        return CloneCommands(ResolveCompatibilityRulesetId(rulesetId));
    }

    public IReadOnlyList<NavigationTabDefinition> ResolveNavigationTabs(string? rulesetId)
    {
        // Compatibility target: NavigationTabCatalog.ForRuleset(rulesetId)
        return CloneTabs(ResolveCompatibilityRulesetId(rulesetId));
    }

    public IReadOnlyList<WorkflowDefinition> ResolveWorkflowDefinitions(string? rulesetId)
    {
        return WorkflowDefinitions;
    }

    public IReadOnlyList<WorkflowSurfaceDefinition> ResolveWorkflowSurfaces(string? rulesetId)
    {
        return WorkflowSurfaces;
    }

    public IReadOnlyList<WorkspaceSurfaceActionDefinition> ResolveWorkspaceActionsForTab(string? tabId, string? rulesetId)
    {
        // Compatibility target: WorkspaceSurfaceActionCatalog.ForTab(tabId, rulesetId)
        string effectiveRulesetId = ResolveCompatibilityRulesetId(rulesetId);
        string effectiveTabId = string.IsNullOrWhiteSpace(tabId) ? "tab-info" : tabId;
        WorkspaceSurfaceActionDefinition[] actions = CloneActions(effectiveRulesetId)
            .Where(action => string.Equals(action.TabId, effectiveTabId, StringComparison.Ordinal))
            .ToArray();
        return actions.Length == 0
            ? CloneActions(effectiveRulesetId)
                .Where(action => string.Equals(action.TabId, "tab-info", StringComparison.Ordinal))
                .ToArray()
            : actions;
    }

    private static string ResolveCompatibilityRulesetId(string? rulesetId)
        => RulesetDefaults.NormalizeOptional(rulesetId)
            ?? RulesetDefaults.NormalizeOptional(Environment.GetEnvironmentVariable(DefaultRulesetEnvironmentVariable))
            ?? RulesetDefaults.Sr5;

    private static IReadOnlyList<AppCommandDefinition> CloneCommands(string rulesetId)
        => CompatibilityCommands
            .Select(command => command with { RulesetId = rulesetId })
            .ToArray();

    private static IReadOnlyList<NavigationTabDefinition> CloneTabs(string rulesetId)
        => CompatibilityTabs
            .Select(tab => tab with { RulesetId = rulesetId })
            .ToArray();

    private static IReadOnlyList<WorkspaceSurfaceActionDefinition> CloneActions(string rulesetId)
        => CompatibilityActions
            .Select(action => action with { RulesetId = rulesetId })
            .ToArray();

    private static AppCommandDefinition Command(string id, string labelKey, string group, bool requiresOpenCharacter)
        => new(id, labelKey, group, requiresOpenCharacter, true, RulesetDefaults.Sr5);

    private static NavigationTabDefinition Tab(string id, string label, string sectionId, string group)
        => new(id, label, sectionId, group, true, true, RulesetDefaults.Sr5);

    private static WorkspaceSurfaceActionDefinition Action(
        string id,
        string label,
        string tabId,
        WorkspaceSurfaceActionKind kind,
        string targetId)
        => new(id, label, tabId, kind, targetId, true, true, RulesetDefaults.Sr5);
}
