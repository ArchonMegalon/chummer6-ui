# Blazor character roster hierarchy contract

The Chummer Online roster must let users organize characters into custom folders and arbitrary hierarchy, similar to a desktop file tree. The UI may be rendered in Blazor, but the behavior should be deterministic, accessible, and persistable.

## Goals

- Users can create custom folders.
- Users can nest folders.
- Users can move runners into folders.
- Users can move folders within the tree.
- Users can reorder siblings.
- The same hierarchy restores across sessions for the same workspace/owner scope.
- Drag/drop and keyboard commands share the same move engine.

## DOM contract

The roster tree root exposes:

- `data-roster-tree="custom-directories"`
- `data-roster-schema-version="1"`
- `data-roster-storage-scope="workspace"`
- `data-roster-persistence-key="chummer-online-roster-tree"`
- `data-roster-reorder-mode="nested"`
- `data-roster-selected-node`

Every roster node exposes:

- `data-roster-node-id`
- `data-roster-parent-id`
- `data-roster-order`
- `data-tree-kind`: `folder` or `runner`
- `data-roster-draggable-kind`: `folder` or `runner`

Folder nodes additionally expose:

- `data-roster-accepts-children="true"`
- `data-roster-drop-zone`
- `aria-expanded`

Runner nodes additionally expose:

- A link to the runner/dossier workflow.
- `aria-selected="true"` only for the selected runner.

## Persistence model

Persist a flat list of nodes, not nested HTML.

Required persisted fields:

- `nodeId`
- `parentId`
- `order`
- `kind`
- `label`
- `runnerReference` for runner nodes
- `isExpanded` for folder nodes
- `createdAtUtc`
- `updatedAtUtc`

Recommended persisted envelope:

```json
{
  "schemaVersion": 1,
  "scope": "workspace",
  "workspaceId": "...",
  "ownerScope": "user-or-local",
  "selectedNodeId": "runner-active",
  "nodes": []
}
```

Do not persist visual indentation, generated DOM order, CSS classes, or display-only text as authoritative structure.

## Move engine

All drag/drop and keyboard move commands should call the same move operation.

Move operation inputs:

- `sourceNodeId`
- `targetParentId`
- `targetOrder`
- `moveMode`: `into-folder`, `before-node`, or `after-node`
- `requestedBy`: `drag-drop`, `keyboard`, `toolbar`, or `context-menu`

Move operation outputs:

- Updated node list.
- Updated selected node ID.
- Validation result.
- Optional announcement text for screen readers.

## Invalid moves

Reject these moves:

- Moving root.
- Moving a node into itself.
- Moving a folder into its own descendant.
- Moving a runner under a runner.
- Moving into a node that does not accept children.
- Producing duplicate sibling order without rebalancing.
- Moving a node that does not exist.
- Moving into a parent that does not exist.

Invalid moves should set a blocked drop state, not silently fail.

## Ordering

Use spaced numeric order values by default, for example `10`, `20`, `30`.

When inserting between siblings:

- If there is space, use the midpoint.
- If there is no space, rebalance sibling order values.
- Rebalancing must preserve relative order.

## Folder commands

Toolbar commands use stable command IDs:

- `new-folder`
- `move-selection`
- `rename-selection`
- `show-inbox`

Expected command behavior:

- New Folder creates a folder under the selected folder, or under the selected node's parent if a runner is selected.
- Move Selection opens a keyboard-accessible move target picker.
- Rename Selection renames folders and local roster labels, but must not rename the underlying character file unless explicitly supported by a separate command.
- Inbox focuses or creates the inbox folder.

## Accessibility

The tree should preserve:

- `role="tree"`
- `role="treeitem"`
- `role="group"`
- `aria-expanded` for folders
- `aria-selected` for the selected node
- `aria-level` for visual depth
- `aria-describedby` linking to movement/help text

Keyboard expectations:

- Arrow keys move selection.
- Right expands folder or enters child group.
- Left collapses folder or moves to parent.
- Enter opens runner or toggles folder.
- F2 renames selection.
- Ctrl+Shift+N creates folder.
- Ctrl+M starts move selection.
- Escape cancels move/rename mode.

## Privacy

Roster persistence may store local labels and runner references, but analytics must not receive:

- Runner names.
- Character XML.
- File paths.
- Owner identifiers.
- Inventory or build contents.

Analytics may receive only coarse events such as `roster-folder-created`, `roster-node-moved`, and `roster-tree-restored`, with route/workflow metadata.
