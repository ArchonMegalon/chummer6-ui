using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Rulesets;

namespace Chummer.Avalonia.Controls;

public partial class NavigatorPaneControl : UserControl
{
    private bool _suppressTreeSelectionEvent;
    private string _openWorkspacesHeading = string.Empty;
    private NavigatorWorkspaceItem[] _openWorkspaces = [];
    private string? _selectedWorkspaceId;
    private string _navigationTabsHeading = string.Empty;
    private NavigatorTabItem[] _navigationTabs = [];
    private string? _activeTabId;
    private string _sectionActionsHeading = string.Empty;
    private NavigatorSectionActionItem[] _sectionActions = [];
    private string? _activeActionId;
    private string _workflowSurfacesHeading = string.Empty;
    private NavigatorWorkflowSurfaceItem[] _workflowSurfaces = [];

    public NavigatorPaneControl()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? WorkspaceSelected;
    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler<string>? SectionActionSelected;
    public event EventHandler<string>? WorkflowSurfaceSelected;

    public void SetState(NavigatorPaneState state)
    {
        CodexHeadingText.Text = BuildCodexHeading(state);
        CodexCaptionText.Text = BuildCodexCaption(state);
        CodexCaptionText.IsVisible = false;
        _openWorkspacesHeading = state.OpenWorkspacesHeading;
        _navigationTabsHeading = state.NavigationTabsHeading;
        _sectionActionsHeading = state.SectionActionsHeading;
        _workflowSurfacesHeading = state.WorkflowSurfacesHeading;
        OpenWorkspacesHeader.Text = state.OpenWorkspacesHeading;
        NavigationTabsHeader.Text = state.NavigationTabsHeading;
        SectionActionsHeader.Text = state.SectionActionsHeading;
        WorkflowSurfacesHeader.Text = state.WorkflowSurfacesHeading;
        ToolTip.SetTip(
            NavigatorTree,
            string.IsNullOrWhiteSpace(CodexCaptionText.Text)
                ? null
                : CodexCaptionText.Text);
        SetOpenWorkspaces(state.OpenWorkspaces, state.SelectedWorkspaceId);
        SetNavigationTabs(state.NavigationTabs, state.ActiveTabId);
        SetSectionActions(state.SectionActions, state.ActiveActionId);
        SetWorkflowSurfaces(state.WorkflowSurfaces);
    }

    public NavigatorTreeItem[] SnapshotTreeItems()
    {
        if (NavigatorTree.ItemsSource is IEnumerable<NavigatorTreeItem> typedItems)
        {
            return typedItems.ToArray();
        }

        if (NavigatorTree.Items is IEnumerable items)
        {
            return items.OfType<NavigatorTreeItem>().ToArray();
        }

        return [];
    }

    private void SetOpenWorkspaces(NavigatorWorkspaceItem[] workspaces, string? selectedWorkspaceId)
    {
        _openWorkspaces = workspaces;
        _selectedWorkspaceId = selectedWorkspaceId;
        RefreshNavigatorTree();
    }

    private void SetNavigationTabs(NavigatorTabItem[] navigationTabs, string? activeTabId)
    {
        _navigationTabs = navigationTabs;
        _activeTabId = activeTabId;
        RefreshNavigatorTree();
    }

    private void SetSectionActions(NavigatorSectionActionItem[] sectionActions, string? activeActionId)
    {
        _sectionActions = sectionActions;
        _activeActionId = activeActionId;
        RefreshNavigatorTree();
    }

    private void SetWorkflowSurfaces(NavigatorWorkflowSurfaceItem[] workflowSurfaces)
    {
        _workflowSurfaces = workflowSurfaces;
        RefreshNavigatorTree();
    }

    private void RefreshNavigatorTree()
    {
        NavigatorTreeItem[] treeItems = BuildTreeItems();
        _suppressTreeSelectionEvent = true;
        NavigatorTree.ItemsSource = treeItems;
        NavigatorTree.SelectedItem = ResolveSelectedTreeItem(treeItems, _selectedWorkspaceId, _activeTabId);
        _suppressTreeSelectionEvent = false;
    }

    private void NavigatorTree_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTreeSelectionEvent)
            return;

        if (NavigatorTree.SelectedItem is not NavigatorTreeItem item || !item.Enabled)
            return;

        switch (item.Kind)
        {
            case NavigatorTreeNodeKind.Workspace:
                WorkspaceSelected?.Invoke(this, item.Id);
                break;
            case NavigatorTreeNodeKind.NavigationTab:
                NavigationTabSelected?.Invoke(this, item.Id);
                break;
            case NavigatorTreeNodeKind.SectionAction:
                SectionActionSelected?.Invoke(this, item.Id);
                ClearTreeSelection();
                break;
            case NavigatorTreeNodeKind.WorkflowSurface:
                WorkflowSurfaceSelected?.Invoke(this, item.Id);
                ClearTreeSelection();
                break;
        }
    }

    private void ClearTreeSelection()
    {
        _suppressTreeSelectionEvent = true;
        NavigatorTree.SelectedItem = null;
        _suppressTreeSelectionEvent = false;
    }

    private static string BuildCodexHeading(NavigatorPaneState state)
        => state.OpenWorkspaces.Length == 1
            ? "Character"
            : "Characters";

    private static string BuildCodexCaption(NavigatorPaneState state)
        => string.Empty;

    private NavigatorTreeItem[] BuildTreeItems()
    {
        IGrouping<string, NavigatorWorkspaceItem>[] rulesetGroups = _openWorkspaces
            .GroupBy(static workspace => NormalizeRulesetId(workspace.RulesetId), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToArray();

        List<NavigatorTreeItem> treeItems = rulesetGroups
            .Select(group => new NavigatorTreeItem(
                Id: group.Key,
                Label: BuildRulesetGroupLabel(group.Key),
                Detail: $"{group.Count()} character{(group.Count() == 1 ? string.Empty : "s")}",
                Enabled: false,
                Kind: NavigatorTreeNodeKind.Group,
                Children: group
                    .OrderBy(static workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static workspace => workspace.Alias, StringComparer.OrdinalIgnoreCase)
                    .Select(workspace => new NavigatorTreeItem(
                        workspace.Id,
                        workspace.Name,
                        BuildWorkspaceDetail(workspace),
                        workspace.Enabled,
                        NavigatorTreeNodeKind.Workspace,
                        []))
                    .ToArray()))
            .ToList();

        bool hasOpenWorkspace = _openWorkspaces.Length > 0;

        if (hasOpenWorkspace && _navigationTabs.Length > 0)
        {
            treeItems.Add(new NavigatorTreeItem(
                Id: "navigation-tabs",
                Label: string.IsNullOrWhiteSpace(_navigationTabsHeading) ? "Runner Tabs" : _navigationTabsHeading,
                Detail: $"{_navigationTabs.Length} tab{(_navigationTabs.Length == 1 ? string.Empty : "s")}",
                Enabled: false,
                Kind: NavigatorTreeNodeKind.Group,
                Children: _navigationTabs
                    .Select(tab => new NavigatorTreeItem(
                        tab.Id,
                        tab.Label,
                        tab.Group,
                        tab.Enabled,
                        NavigatorTreeNodeKind.NavigationTab,
                        []))
                    .ToArray()));
        }

        if (hasOpenWorkspace && _sectionActions.Length > 0)
        {
            treeItems.Add(new NavigatorTreeItem(
                Id: "section-actions",
                Label: string.IsNullOrWhiteSpace(_sectionActionsHeading) ? "Section Actions" : _sectionActionsHeading,
                Detail: $"{_sectionActions.Length} action{(_sectionActions.Length == 1 ? string.Empty : "s")}",
                Enabled: false,
                Kind: NavigatorTreeNodeKind.Group,
                Children: _sectionActions
                    .Select(action => new NavigatorTreeItem(
                        action.Id,
                        action.Label,
                        action.Kind.ToString(),
                        true,
                        NavigatorTreeNodeKind.SectionAction,
                        []))
                    .ToArray()));
        }

        if (hasOpenWorkspace && _workflowSurfaces.Length > 0)
        {
            treeItems.Add(new NavigatorTreeItem(
                Id: "workflow-surfaces",
                Label: string.IsNullOrWhiteSpace(_workflowSurfacesHeading) ? "Workflow Surfaces" : _workflowSurfacesHeading,
                Detail: $"{_workflowSurfaces.Length} workflow{(_workflowSurfaces.Length == 1 ? string.Empty : "s")}",
                Enabled: false,
                Kind: NavigatorTreeNodeKind.Group,
                Children: _workflowSurfaces
                    .Select(surface => new NavigatorTreeItem(
                        surface.SurfaceId,
                        surface.Label,
                        surface.WorkflowId,
                        true,
                        NavigatorTreeNodeKind.WorkflowSurface,
                        []))
                    .ToArray()));
        }

        return treeItems.ToArray();
    }

    private static string NormalizeRulesetId(string? rulesetId)
        => string.IsNullOrWhiteSpace(rulesetId)
            ? "shared"
            : RulesetDefaults.NormalizeRequired(rulesetId);

    private static string BuildRulesetGroupLabel(string rulesetId)
        => RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId);

    private static string BuildWorkspaceDetail(NavigatorWorkspaceItem workspace)
        => $"Alias {workspace.Alias} · {(workspace.HasSavedWorkspace ? "saved" : "unsaved")}";

    private static NavigatorTreeItem? ResolveSelectedTreeItem(
        IEnumerable<NavigatorTreeItem> items,
        string? selectedWorkspaceId,
        string? activeTabId)
    {
        if (!string.IsNullOrWhiteSpace(activeTabId))
        {
            NavigatorTreeItem? selectedTab = FindTreeItem(items, NavigatorTreeNodeKind.NavigationTab, activeTabId);
            if (selectedTab is not null)
            {
                return selectedTab;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedWorkspaceId))
        {
            return FindTreeItem(items, NavigatorTreeNodeKind.Workspace, selectedWorkspaceId);
        }

        return null;
    }

    private static NavigatorTreeItem? FindTreeItem(
        IEnumerable<NavigatorTreeItem> items,
        NavigatorTreeNodeKind kind,
        string id)
    {
        foreach (NavigatorTreeItem item in items)
        {
            if (item.Kind == kind && string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                return item;
            }

            NavigatorTreeItem? childMatch = FindTreeItem(item.Children, kind, id);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }
}

public sealed record NavigatorWorkspaceItem(
    string Id,
    string Name,
    string Alias,
    string RulesetId,
    bool HasSavedWorkspace,
    bool Enabled)
{
    public override string ToString()
    {
        string label = RulesetUiDirectiveCatalog.BuildWorkspaceNavigatorLabel(RulesetId, Name, Alias, HasSavedWorkspace);
        return $"{label} [{Id}] {(Enabled ? "enabled" : "disabled")}";
    }
}

public sealed record NavigatorPaneState(
    string OpenWorkspacesHeading,
    NavigatorWorkspaceItem[] OpenWorkspaces,
    string? SelectedWorkspaceId,
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    string SectionActionsHeading,
    NavigatorSectionActionItem[] SectionActions,
    string? ActiveActionId,
    string WorkflowSurfacesHeading,
    NavigatorWorkflowSurfaceItem[] WorkflowSurfaces);

public sealed record NavigatorTabItem(
    string Id,
    string Label,
    string SectionId,
    string Group,
    bool Enabled)
{
    public override string ToString()
    {
        return $"{Label} ({Id}) -> {SectionId}";
    }
}

public sealed record NavigatorSectionActionItem(string Id, string Label, WorkspaceSurfaceActionKind Kind)
{
    public override string ToString()
    {
        return $"{Label} [{Kind}]";
    }
}

public sealed record NavigatorWorkflowSurfaceItem(
    string SurfaceId,
    string WorkflowId,
    string Label,
    string ActionId)
{
    public override string ToString()
    {
        return $"{Label} ({WorkflowId})";
    }
}

public enum NavigatorTreeNodeKind
{
    Group,
    Workspace,
    NavigationTab,
    SectionAction,
    WorkflowSurface,
}

public sealed record NavigatorTreeItem(
    string Id,
    string Label,
    string Detail,
    bool Enabled,
    NavigatorTreeNodeKind Kind,
    NavigatorTreeItem[] Children)
{
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public override string ToString()
    {
        return HasDetail ? $"{Label} · {Detail}" : Label;
    }
}
