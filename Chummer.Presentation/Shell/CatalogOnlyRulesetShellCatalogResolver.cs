using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;

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
        new("catalog.shell.menu", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.MenuBar, WorkflowLayoutTokens.ShellFrame, ["file", "tools", "windows", "help"]),
        new("catalog.shell.toolbar", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.ToolStrip, WorkflowLayoutTokens.ShellFrame, ["save_character", "print_character", "copy", "new_character", "open_character", "close_window"]),
        new("catalog.career.section", WorkflowDefinitionIds.CareerWorkbench, WorkflowSurfaceKinds.Workbench, ShellRegionIds.SectionPane, WorkflowLayoutTokens.CareerWorkbench, ["tab-info.summary", "tab-info.profile", "tab-skills.skills"]),
        new("catalog.selection.dialog", WorkflowDefinitionIds.SelectionDialog, WorkflowSurfaceKinds.Dialog, ShellRegionIds.DialogHost, WorkflowLayoutTokens.SelectionDialog, ["tab-gear.inventory"]),
        new("catalog.tool.dice", WorkflowDefinitionIds.DiceTool, WorkflowSurfaceKinds.Tool, ShellRegionIds.DialogHost, WorkflowLayoutTokens.ToolPanel, ["dice_roller"]),
        new("catalog.tool.roster", WorkflowDefinitionIds.DiceTool, WorkflowSurfaceKinds.Tool, ShellRegionIds.DialogHost, WorkflowLayoutTokens.ToolPanel, ["character_roster"]),
        new("catalog.session.summary", WorkflowDefinitionIds.SessionDashboard, WorkflowSurfaceKinds.Dashboard, ShellRegionIds.SummaryHeader, WorkflowLayoutTokens.SessionDashboard, ["tab-info.summary", "tab-info.validate"])
    ];

    private static readonly IReadOnlyList<AppCommandDefinition> CompatibilityCommands =
    [
        Command("file", "command.file", "menu", false),
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
        Command("print_character", "command.print_character", "file", true),
        Command("export_character", "command.export_character", "file", true),
        Command("copy", "command.copy", "edit", true),
        Command("paste", "command.paste", "edit", true),
        Command("dice_roller", "command.dice_roller", "tools", false),
        Command("global_settings", "command.global_settings", "tools", false),
        Command("character_settings", "command.character_settings", "tools", true),
        Command("update", "command.update", "tools", false),
        Command("restart", "command.restart", "tools", false),
        Command("switch_ruleset", "command.switch_ruleset", "special", false),
        Command("translator", "command.translator", "tools", false),
        Command("xml_editor", "command.xml_editor", "tools", false),
        Command("hero_lab_importer", "command.hero_lab_importer", "tools", false),
        Command("master_index", "command.master_index", "tools", false),
        Command("character_roster", "command.character_roster", "tools", false),
        Command("data_exporter", "command.data_exporter", "tools", false),
        Command("report_bug", "command.report_bug", "tools", false),
        Command("print_setup", "command.print_setup", "file", false),
        Command("print_multiple", "command.print_multiple", "file", false),
        Command("exit", "command.exit", "file", false),
        Command("new_window", "command.new_window", "windows", false),
        Command("close_window", "command.close_window", "windows", false),
        Command("close_all", "command.close_all", "windows", false),
        Command("wiki", "command.wiki", "help", false),
        Command("discord", "command.discord", "help", false),
        Command("revision_history", "command.revision_history", "help", false),
        Command("dumpshock", "command.dumpshock", "help", false),
        Command("about", "command.about", "help", false)
    ];

    private static readonly IReadOnlyList<NavigationTabDefinition> CompatibilityTabs =
    [
        Tab("tab-info", "Info", "profile", "character"),
        Tab("tab-attributes", "Attributes", "attributes", "character"),
        Tab("tab-skills", "Skills", "skills", "character"),
        Tab("tab-qualities", "Qualities", "qualities", "character"),
        Tab("tab-magician", "Magician", "spells", "character"),
        Tab("tab-combat", "Combat", "weapons", "character"),
        Tab("tab-gear", "Gear", "gear", "character"),
        Tab("tab-contacts", "Contacts", "contacts", "character"),
        Tab("tab-rules", "Rules", "rules", "character"),
        Tab("tab-notes", "Notes", "profile", "character")
    ];

    private static readonly IReadOnlyList<WorkspaceSurfaceActionDefinition> CompatibilityActions =
    [
        Action("tab-info.summary", "Summary", "tab-info", WorkspaceSurfaceActionKind.Summary, "summary"),
        Action("tab-info.validate", "Validate", "tab-info", WorkspaceSurfaceActionKind.Validate, "validate"),
        Action("tab-info.profile", "Profile", "tab-info", WorkspaceSurfaceActionKind.Section, "profile"),
        Action("tab-info.rules", "Rules", "tab-info", WorkspaceSurfaceActionKind.Section, "rules"),
        Action("tab-info.attributes", "Attributes", "tab-info", WorkspaceSurfaceActionKind.Section, "attributes"),
        Action("tab-skills.skills", "Skills", "tab-skills", WorkspaceSurfaceActionKind.Section, "skills"),
        Action("tab-qualities.qualities", "Qualities", "tab-qualities", WorkspaceSurfaceActionKind.Section, "qualities"),
        Action("tab-magician.spells", "Spells", "tab-magician", WorkspaceSurfaceActionKind.Section, "spells"),
        Action("tab-combat.weapons", "Weapons", "tab-combat", WorkspaceSurfaceActionKind.Section, "weapons"),
        Action("tab-gear.inventory", "Inventory", "tab-gear", WorkspaceSurfaceActionKind.Section, "inventory"),
        Action("tab-contacts.contacts", "Contacts", "tab-contacts", WorkspaceSurfaceActionKind.Section, "contacts"),
        Action("tab-rules.rules", "Rules", "tab-rules", WorkspaceSurfaceActionKind.Section, "rules"),
        Action("tab-notes.metadata", "Save Notes", "tab-notes", WorkspaceSurfaceActionKind.Metadata, "metadata")
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
