# Blazor Workbench Layout-Presets Staged Proof

## Purpose

This source-staged proof keeps browser-safe layout mode affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

The browser client should expose dense dossier, split review, output, mobile-safe, focus-pane, and help modes as intentional client states rather than accidental responsive behavior.

## Source-Staged Scope

The staged layout-presets lane covers:

- a layout-presets rail on the user-facing Chummer Online route and /blazor/workbench compatibility route
- dense dossier, split review, output, mobile safe, focus pane, and help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-layout-presets-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_LAYOUT_PRESETS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route layout-presets source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, pane-resizing proof, portal-help-runtime proof, persisted-layout proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
