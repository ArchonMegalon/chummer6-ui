# Blazor Workbench Roster Hierarchy Staged Proof

## Purpose

This staged proof keeps the promoted Blazor workbench source aligned around custom character roster hierarchy posture.

It covers visible source posture for user-created virtual folders, nested groups, drag/drop move intent, explicit roster move actions, watched-file virtual links, browser markup/styling for roster hierarchy rows, editable folder/target fields, non-destructive metadata mutation for create/rename/move/reorder actions, the shared `RosterHierarchyState` contract, `RosterHierarchyJson` preference staging, staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, plus hidden global-settings carriage on `/blazor/workbench?command=character_roster`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-roster-hierarchy-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, browser drag/drop event mutation, filesystem moves, folder deletion, watched-file relocation, or external `RosterHierarchyState` storage beyond preference-carried layout metadata.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof plus explicit drag/drop persistence implementation.
