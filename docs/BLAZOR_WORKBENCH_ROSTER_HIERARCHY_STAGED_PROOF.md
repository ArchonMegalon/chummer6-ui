# Blazor Workbench Roster Hierarchy Staged Proof

## Purpose

This staged proof keeps the promoted Blazor workbench source aligned around custom character roster hierarchy posture.

It covers visible source posture for user-created virtual folders, nested groups, drag/drop move intent, explicit roster move actions, watched-file virtual links, browser markup/styling, title affordances with row-level keyboard help, aria labels, aria-describedby linkage to live keyboard source feedback, labelled tree containers, vertical tree orientation, tree/treeitem roles, aria-level depth metadata, aria-selected state for selected/source rows, nullable optional ARIA emission for presentation rows, focusability, visible keyboard drag-source state, live keyboard source feedback, visible keyboard operation guidance, and Enter/Space/Escape keyboard handling for actionable roster hierarchy rows, editable folder/target fields with option-backed source and target folder pickers, a custom-only source picker for rename/delete/nesting choices, styled visible hierarchy status counts and pending-move disclosure, hidden source-item carriage, a Blazor drag/drop event bridge that fills `rosterTargetFolder`, carries `rosterSourceFolder` for folder drops, carries `rosterSourceItem` for dragged runner/link rows, preserves full runner labels while stripping visual row suffixes, and invokes the same virtual move/reorder actions, non-destructive metadata mutation for create/rename/delete/move/reorder actions including custom-folder nesting, safe custom-folder deletion that moves runner/link items to Inbox and reparents child folders, cycle prevention for folder drops into their own descendants, reset-to-generated-layout recovery that clears only hierarchy metadata, the shared `RosterHierarchyState` contract, `RosterHierarchyJson` preference staging, staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, plus hidden global-settings carriage on `/blazor/workbench?command=character_roster`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-roster-hierarchy-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, complete drag/drop UX coverage for every source item type, filesystem moves, folder deletion, watched-file relocation, or external `RosterHierarchyState` storage beyond preference-carried layout metadata.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof plus explicit drag/drop persistence implementation.
