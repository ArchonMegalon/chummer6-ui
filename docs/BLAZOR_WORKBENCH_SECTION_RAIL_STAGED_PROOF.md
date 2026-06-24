# Blazor Workbench Section-Rail Staged Proof

## Purpose

This source-staged proof keeps desktop-style character sheet navigation visible on the promoted Blazor workbench route.

The browser client should feel like another Chummer client head: dense sheet sections stay one click away instead of becoming disconnected web pages.

## Source-Staged Scope

The staged section-rail lane covers:

- a sheet-section rail on the promoted workbench route
- profile, build, skills, gear, combat, magic, matrix, contacts, and career shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-section-rail-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SECTION_RAIL_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench section-rail source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, section rendering proof, state-restoration proof, route-click proof, or desktop-equivalent workflow parity.
