using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Character roster tree control matching Chummer5a treCharacterList structure.
/// Provides multi-character navigation with drag-drop support for roster operations.
/// </summary>
public partial class CharacterRosterControl : UserControl
{
    public static readonly StyledProperty<IList<CharacterRosterNode>?> RosterItemsProperty =
        AvaloniaProperty.Register<CharacterRosterControl, IList<CharacterRosterNode>?>(nameof(RosterItems));

    public static readonly StyledProperty<string?> SelectedWorkspaceIdProperty =
        AvaloniaProperty.Register<CharacterRosterControl, string?>(nameof(SelectedWorkspaceId));

    public static readonly RoutedEvent<RosterSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<CharacterRosterControl, RosterSelectionChangedEventArgs>(
            nameof(SelectionChanged),
            RoutingStrategies.Bubble);

    private bool _suppressSelectionChanged;

    public IList<CharacterRosterNode>? RosterItems
    {
        get => GetValue(RosterItemsProperty);
        set
        {
            SetValue(RosterItemsProperty, value);
            UpdateTreeData(value);
        }
    }

    public string? SelectedWorkspaceId
    {
        get => GetValue(SelectedWorkspaceIdProperty);
        set
        {
            SetValue(SelectedWorkspaceIdProperty, value);
            UpdateSelectedItem(value);
        }
    }

    public event EventHandler<RosterSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public CharacterRosterControl()
    {
        InitializeComponent();

        RosterTree.PointerReleased += OnRosterPointerReleased;
    }

    internal void SetState(RosterPaneState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        RosterItems = state.Items;
        SelectedWorkspaceId = state.SelectedWorkspaceId;
    }

    private void UpdateTreeData(IList<CharacterRosterNode>? items)
    {
        RosterTree.ItemsSource = items;
        UpdateSelectedItem(SelectedWorkspaceId);
    }

    private void UpdateSelectedItem(string? workspaceId)
    {
        _suppressSelectionChanged = true;
        RosterTree.SelectedItem = FindNode(RosterItems, workspaceId);
        _suppressSelectionChanged = false;
    }

    private void RosterTree_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (RosterTree.SelectedItem is CharacterRosterNode node)
        {
            if (node.IsGroup)
            {
                return;
            }

            SelectedWorkspaceId = node.Id;
            RaiseEvent(new RosterSelectionChangedEventArgs(SelectionChangedEvent, node));
        }
    }

    private void RosterTree_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Move | DragDropEffects.Copy;
        e.Handled = true;
    }

    private void RosterTree_OnDrop(object? sender, DragEventArgs e)
    {
        // Handle drag-drop for character roster operations
        e.Handled = true;
    }

    private static CharacterRosterNode? FindNode(IEnumerable<CharacterRosterNode>? items, string? workspaceId)
    {
        if (items is null || string.IsNullOrWhiteSpace(workspaceId))
        {
            return null;
        }

        foreach (CharacterRosterNode item in items)
        {
            if (string.Equals(item.Id, workspaceId, StringComparison.Ordinal))
            {
                return item;
            }

            CharacterRosterNode? childMatch = FindNode(item.Children, workspaceId);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }
    private CharacterRosterNode? _pendingRosterNode;

    private void OnRosterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
        {
            return;
        }

        if (e.Source is not Visual visual)
        {
            return;
        }

        var item = visual
            .GetSelfAndVisualAncestors()
            .OfType<TreeViewItem>()
            .FirstOrDefault();

        if (item?.DataContext is not CharacterRosterNode node || node.IsGroup)
        {
            return;
        }

        _pendingRosterNode = node;
        ShowRosterContextMenu(node, e.GetPosition(this));
    }

    private void ShowRosterContextMenu(CharacterRosterNode node, Point position)
    {
        var menu = new ContextMenu();
        AddRosterMenuItem(menu, RosterActionIds.Open, "Open");
        AddRosterMenuItem(menu, RosterActionIds.NewRunner, "New Runner...");
        AddRosterMenuItem(menu, RosterActionIds.NewRunnerOrigin, "New Runner (Origin Dossier)...");
        AddRosterMenuItem(menu, RosterActionIds.Delete, "Delete Runner...");
        menu.Open(this);
    }

    private void AddRosterMenuItem(ContextMenu menu, RosterActionIds actionId, string header)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            var node = _pendingRosterNode;
            if (node is null)
            {
                menu.Close();
                return;
            }

            RosterActionRequested?.Invoke(this, new CharacterRosterActionEventArgs(actionId, node.Id, node.Name));
        };
        menu.Items.Add(item);
    }


    /// <summary>
    /// Per-runner roster context-menu actions (Chummer5a parity).
    /// </summary>
    /// <summary>Raised when the user picks an action from the roster context menu.</summary>
    public event EventHandler<CharacterRosterActionEventArgs>? RosterActionRequested;

    public enum RosterActionIds
    {
        Open,
        NewRunner,
        NewRunnerOrigin,
        Delete,
    }


    /// <summary>
    /// Per-runner roster context-menu action event args.
    /// </summary>
    public sealed class CharacterRosterActionEventArgs : EventArgs
    {
        public CharacterRosterActionEventArgs(RosterActionIds actionId, string workspaceId, string displayName)
        {
            ActionId = actionId;
            WorkspaceId = workspaceId;
            DisplayName = displayName;
        }

        public RosterActionIds ActionId { get; }

        public string WorkspaceId { get; }

        public string DisplayName { get; }
    }

}

/// <summary>
/// Character roster tree node matching Chummer5a treCharacterList structure.
/// </summary>
public sealed class CharacterRosterNode
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Meta { get; init; }
    public string? Initials { get; init; }
    public bool IsGroup { get; init; }
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);
    public bool ShowMeta => HasMeta;
    public bool ShowInitialBadge => !IsGroup && !string.IsNullOrWhiteSpace(Initials);
    public IList<CharacterRosterNode> Children { get; init; } = Array.Empty<CharacterRosterNode>();
}

/// <summary>
/// Roster selection changed event args.
/// </summary>
public sealed class RosterSelectionChangedEventArgs : RoutedEventArgs
{
    public CharacterRosterNode SelectedNode { get; }

    public RosterSelectionChangedEventArgs(RoutedEvent? eventRoutedEvent, CharacterRosterNode selectedNode)
        : base(eventRoutedEvent)
    {
        SelectedNode = selectedNode;
    }
}
