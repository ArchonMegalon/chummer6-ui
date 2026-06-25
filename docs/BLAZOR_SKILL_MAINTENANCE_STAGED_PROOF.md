# Blazor Skill Maintenance Staged Proof

## Purpose

This staged proof keeps Chummer Online and /blazor/workbench sources aligned around skill maintenance utility posture under restored `tab-skills`.

It covers browser-visible utility routes for `skill_specialize`, `skill_remove`, and `skill_group`, including desktop-shaped Specialization, Remove Skill, and Skill Group dialogs with visible specialization, removal/recovery, group-composition, and current-rating context.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-skill-maintenance-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json
```

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this staged receipt separately:

```text
skill_maintenance_staged_status=
skill_maintenance_staged_route_count=
skill_maintenance_staged_source_checks=
skill_maintenance_staged_note=source_alignment_only_not_browser_execution
```

These lines are source staging only. They are not browser execution proof and do not replace hosted public-edge execution proof, Docker self-host proof, or browser-lane aggregate proof.

## Boundary

This proof checks product affordances, the shared legacy control catalog, desktop-shaped dialog source, hosted route-entry source, hosted execution runner source, Docker self-host runner source, self-host receipt metadata, release docs, parity docs, status reporting, and example receipt shape. It does not execute hosted browser workflows, Docker self-host workflows, dialog actions, skill-state mutation, specialization persistence, group-edit mutation, removal behavior, or rules-engine calculations.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof that exercise the skill maintenance routes as runtime receipts.
