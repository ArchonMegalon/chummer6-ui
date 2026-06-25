# Blazor Combat Support Staged Proof

## Purpose

This staged proof keeps the Chummer Online and proof-compatible Blazor workbench source aligned around combat support utility posture under restored `tab-combat`.

It covers browser-visible utility routes for `combat_add_armor`, `combat_reload`, and `combat_damage_track`, including desktop-shaped Add Armor, Reload, and Damage Track dialogs with visible armor selection, weapon/ammo reload context, damage-track review, and active-combat context.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-combat-support-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json
```

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this staged receipt separately:

```text
combat_support_staged_status=
combat_support_staged_route_count=
combat_support_staged_source_checks=
combat_support_staged_note=source_alignment_only_not_browser_execution
```

These lines are source staging only. They are not browser execution proof and do not replace hosted public-edge execution proof, Docker self-host proof, or browser-lane aggregate proof.

## Boundary

This proof checks product affordances, the shared legacy control catalog, desktop-shaped dialog source, hosted route-entry source, hosted execution runner source, Docker self-host runner source, self-host receipt metadata, release docs, parity docs, status reporting, and example receipt shape. It does not execute hosted browser workflows, Docker self-host workflows, dialog actions, combat-state mutation, reload mutation, damage-track mutation, persistence, or rules-engine calculations.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof that exercise the combat support routes as runtime receipts.
