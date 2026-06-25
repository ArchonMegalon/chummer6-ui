# Blazor Workbench Context-Actions Staged Proof

## Purpose

This source-staged proof keeps selection-style context actions visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should not depend on hidden right-click behavior for common Chummer actions. Add, edit, remove, duplicate, source lookup, help, and recovery lanes should remain visible for mouse, touch, and keyboard users.

## Source-Staged Scope

The staged context-actions lane covers:

- a context-action rail on the user-facing Chummer Online route and /blazor/workbench compatibility route
- add, edit, remove, duplicate, source lookup, help, and recover shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-context-actions-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_CONTEXT_ACTIONS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route context-actions source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, right-click menu proof, portal-help-runtime proof, selection-state runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
