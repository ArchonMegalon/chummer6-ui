# Blazor Workbench Inspector-Rail Staged Proof

## Purpose

This source-staged proof keeps side-context affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should keep summary, build checks, inventory, notes, and sources close to the active task instead of forcing users into disconnected web-page detours.

## Source-Staged Scope

The staged inspector-rail lane covers:

- an inspector rail on the user-facing Chummer Online route and /blazor/workbench compatibility route
- summary, build checks, inventory, notes, and sources shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-inspector-rail-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_INSPECTOR_RAIL_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route inspector-rail source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, live inspector-state proof, split-pane persistence proof, screenshot proof, or desktop-equivalent workflow parity.
