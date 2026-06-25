# Blazor Gear Maintenance Staged Proof

## Purpose

This staged proof keeps Chummer Online and /blazor/workbench sources aligned around gear maintenance utility posture under restored `tab-gear`.

It covers browser-visible utility routes for `gear_add`, `gear_edit`, and `gear_delete`, including desktop-shaped Add Gear, Edit Gear, and Remove Gear dialog posture with visible catalog, edit context, removal/recovery, and inventory-list posture.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-gear-maintenance-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json
```

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this staged receipt separately:

```text
gear_maintenance_staged_status=
gear_maintenance_staged_route_count=
gear_maintenance_staged_source_checks=
gear_maintenance_staged_note=source_alignment_only_not_browser_execution
```

These lines are source staging only. They are not browser execution proof and do not replace hosted public-edge execution proof, Docker self-host proof, or browser-lane aggregate proof.

## Boundary

This proof checks product affordances, the shared legacy control catalog, desktop-shaped dialog source, hosted route-entry source, hosted execution runner source, Docker self-host runner source, self-host receipt metadata, release docs, parity docs, status reporting, and example receipt shape. It does not execute hosted browser workflows, Docker self-host workflows, dialog actions, gear-state mutation, inventory persistence, removal behavior, pricing, availability, or rules-engine calculations.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof that exercise the gear maintenance routes as runtime receipts.
