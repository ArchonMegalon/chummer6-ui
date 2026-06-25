# Blazor Workbench Accessibility Staged Proof

## Purpose

This source-staged proof keeps accessibility posture visible on the user-facing Chummer App route and proof-compatible Blazor workbench route.

The browser client should preserve Chummer's dense desktop workflow without hiding controls, trapping users in dialogs, or requiring animation-dependent context to continue work.

## Source-Staged Scope

The staged accessibility lane covers:

- an accessibility strip on the user-facing Chummer App route and proof-compatible workbench route
- keyboard order, dialog fit, readable density, reduced motion, and support escape affordances
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-accessibility-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_ACCESSIBILITY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and proof-compatible workbench accessibility source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, screen-reader proof, keyboard-event proof, screenshot proof, browser accessibility validation, or desktop-equivalent workflow parity.
