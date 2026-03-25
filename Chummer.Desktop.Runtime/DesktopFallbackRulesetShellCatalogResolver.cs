using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;

namespace Chummer.Desktop.Runtime;

public sealed class DesktopFallbackRulesetShellCatalogResolver : IRulesetShellCatalogResolver
{
    private static readonly IReadOnlyList<WorkflowDefinition> WorkflowDefinitions =
    [
        new(WorkflowDefinitionIds.LibraryShell, "Library Shell", ["catalog.shell.menu", "catalog.shell.toolbar"], false),
        new(WorkflowDefinitionIds.CareerWorkbench, "Career Workbench", ["catalog.career.section"], true),
        new(WorkflowDefinitionIds.SelectionDialog, "Selection Dialog", ["catalog.selection.dialog"], false),
        new(WorkflowDefinitionIds.DiceTool, "Dice Tool", ["catalog.tool.dice"], false),
        new(WorkflowDefinitionIds.SessionDashboard, "Session Dashboard", ["catalog.session.summary"], true, true)
    ];

    private static readonly IReadOnlyList<WorkflowSurfaceDefinition> WorkflowSurfaces =
    [
        new("catalog.shell.menu", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.MenuBar, WorkflowLayoutTokens.ShellFrame, ["file", "edit", "tools"]),
        new("catalog.shell.toolbar", WorkflowDefinitionIds.LibraryShell, WorkflowSurfaceKinds.ShellRegion, ShellRegionIds.ToolStrip, WorkflowLayoutTokens.ShellFrame, ["new_character", "open_character", "save_character"]),
        new("catalog.career.section", WorkflowDefinitionIds.CareerWorkbench, WorkflowSurfaceKinds.Workbench, ShellRegionIds.SectionPane, WorkflowLayoutTokens.CareerWorkbench, ["tab-info.summary", "tab-info.profile", "tab-skills.skills"]),
        new("catalog.selection.dialog", WorkflowDefinitionIds.SelectionDialog, WorkflowSurfaceKinds.Dialog, ShellRegionIds.DialogHost, WorkflowLayoutTokens.SelectionDialog, ["tab-gear.inventory"]),
        new("catalog.tool.dice", WorkflowDefinitionIds.DiceTool, WorkflowSurfaceKinds.Tool, ShellRegionIds.DialogHost, WorkflowLayoutTokens.ToolPanel, ["dice_roller"]),
        new("catalog.session.summary", WorkflowDefinitionIds.SessionDashboard, WorkflowSurfaceKinds.Dashboard, ShellRegionIds.SummaryHeader, WorkflowLayoutTokens.SessionDashboard, ["tab-info.summary", "tab-info.validate"])
    ];

    public IReadOnlyList<AppCommandDefinition> ResolveCommands(string? rulesetId)
    {
        return Array.Empty<AppCommandDefinition>();
    }

    public IReadOnlyList<NavigationTabDefinition> ResolveNavigationTabs(string? rulesetId)
    {
        return Array.Empty<NavigationTabDefinition>();
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
        return Array.Empty<WorkspaceSurfaceActionDefinition>();
    }
}
