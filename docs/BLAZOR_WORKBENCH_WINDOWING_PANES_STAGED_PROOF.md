# Blazor Workbench Windowing-Panes Staged Proof

## Purpose

This source-staged proof keeps desktop-like pane and windowing affordances visible on the promoted Blazor workbench route.

The browser client should expose split view, pop-out detail panes, pinned inspectors, focus mode, second-screen table views, and layout restore posture near dense sheet workflows.

## Source-Staged Scope

The staged windowing-panes lane covers:

- a windowing/pane management strip on the promoted workbench route
- split view, pop-out, pinned inspector, focus mode, second screen, and restore layout shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-windowing-panes-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_WINDOWING_PANES_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench windowing-panes source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, multi-window proof, focus-handling proof, second-screen proof, layout-persistence proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
